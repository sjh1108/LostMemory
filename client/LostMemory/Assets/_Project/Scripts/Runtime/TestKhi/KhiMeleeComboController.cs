using System;
using System.Collections;
using System.Collections.Generic;
using LostMemory.Combat;
using LostMemory.Data;
using MoreMountains.TopDownEngine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LostMemory.TestKhi
{
    [DefaultExecutionOrder(50)]
    public class KhiMeleeComboController : MonoBehaviour
    {
        [SerializeField] private KhiPlayerAim aim;
        [SerializeField] private KhiMeleeHitbox hitbox;
        [SerializeField] private KhiAttackVisualPresenter visualPresenter;
        [SerializeField] private KhiDashController dash;
        [Tooltip("무기 데이터 SO. 필수. baseDamage / 콤보 윈도우 / 콤보 step 데이터를 모두 담음.")]
        [SerializeField] private WeaponData weaponData;
        [SerializeField] private bool allowInputDuringRecovery = true;
        [SerializeField] private bool allowAttackDuringDash = false;
        [SerializeField] private bool bufferAttackDuringDash = false;
        [SerializeField] private bool disableTdeHandleWeapon = true;
        [SerializeField] private bool logHitsToConsole = false;
        [Tooltip("CL-107: AttackPower / AttackSpeed / FinisherDamage multiplier 조회용. 같은 GameObject 또는 Player root 의 컴포넌트.")]
        [SerializeField] private PlayerStatModifierContainer statContainer;

        private readonly HashSet<Health> _alreadyHitThisSwing = new HashSet<Health>();
        private readonly List<Health> _hitsThisSample = new List<Health>(8);

        private CharacterHandleWeapon _tdeHandleWeapon;
        private bool _isAttacking;
        private bool _isInAttackRecovery;
        private bool _externalAbortRequested;
        private int _nextComboStep = 1;
        private int _currentComboStep;
        private int _sequenceId;
        private float _comboExpiresAt = -1f;
        private float _chainInputAllowedAt = -1f;
        private float _bufferedAttackExpiresAt = -1f;

        public event Action<KhiAttackRequest, AttackStepData> AttackStarted;
        public event Action<KhiAttackRequest, AttackStepData> AttackActiveStarted;
        public event Action<KhiAttackRequest, AttackStepData> AttackActiveEnded;
        public event Action<KhiAttackRequest, AttackStepData, Health> TargetHit;
        public event Action<KhiAttackRequest, AttackStepData> FinisherHit;

        /// <summary>CL-107: 본 컨트롤러의 공격이 대상을 사망시켰을 때 발화. RelicEffectApplier 의 붉은송곳니 등이 구독.</summary>
        public event Action<KhiAttackRequest, AttackStepData, Health> EnemyKilledByPlayer;

        public bool IsAttacking => _isAttacking;
        public bool IsInAttackRecovery => _isInAttackRecovery;
        public bool BlocksDash => _isAttacking;

        /// <summary>
        /// 외부 시스템에서 본 컨트롤러가 참조하는 WeaponData를 읽기 위한 getter.
        /// 예: KhiSlashAnimator 가 같은 SO를 공유하고 싶을 때.
        /// </summary>
        public WeaponData WeaponData => weaponData;

        /// <summary>
        /// 외부 시스템(예: KhiParryController)이 공격 입력을 일시적으로 차단하기 위한 플래그.
        /// true인 동안 Update의 공격 입력 감지와 RequestAttack 진입을 모두 무시한다.
        /// 기본값 false이며, 설정한 시스템이 반드시 false로 복원해야 한다.
        /// </summary>
        public bool ExternalBlock { get; set; }

        /// <summary>
        /// 진행 중인 공격 코루틴을 즉시 중단 요청한다. (CL-013 피격 경직용)
        /// 공격 중이 아니면 아무 일도 하지 않는다.
        /// 중단 시 콤보 상태는 초기화되고 버퍼된 공격은 폐기된다.
        /// </summary>
        public void AbortCurrentAttack()
        {
            if (_isAttacking)
            {
                _externalAbortRequested = true;
            }
        }

        private void Awake()
        {
            aim ??= GetComponent<KhiPlayerAim>();
            hitbox ??= GetComponent<KhiMeleeHitbox>();
            visualPresenter ??= GetComponent<KhiAttackVisualPresenter>();
            dash ??= GetComponent<KhiDashController>();
            _tdeHandleWeapon = GetComponent<CharacterHandleWeapon>();

            if (weaponData == null)
            {
                Debug.LogError($"[KhiMeleeComboController] WeaponData 가 할당되지 않음. {name} 의 Inspector 에서 SO 자산을 드래그하세요.", this);
                enabled = false;
                return;
            }

            if (weaponData.Steps == null || weaponData.Steps.Length == 0)
            {
                Debug.LogError($"[KhiMeleeComboController] WeaponData '{weaponData.name}' 의 Steps 가 비어있음.", this);
                enabled = false;
            }
        }

        private IEnumerator Start()
        {
            if (!disableTdeHandleWeapon)
            {
                yield break;
            }

            yield return null;
            DisableTdeWeaponHandling();
        }

        private void Update()
        {
            if (!ExternalBlock && WasAttackPressedThisFrame())
            {
                RequestAttack();
            }

            if (!_isAttacking && _nextComboStep != 1 && Time.time > _comboExpiresAt)
            {
                _nextComboStep = 1;
            }
        }

        public void RequestAttack()
        {
            if (ExternalBlock)
            {
                return;
            }

            if (ShouldBlockAttackForDash())
            {
                return;
            }

            if (_isAttacking)
            {
                TryBufferAttack();
                return;
            }

            if (_nextComboStep != 1 && Time.time > _comboExpiresAt)
            {
                _nextComboStep = 1;
            }

            StartCoroutine(RunAttack(_nextComboStep));
        }

        private IEnumerator RunAttack(int comboStep)
        {
            _isAttacking = true;
            _isInAttackRecovery = false;
            _externalAbortRequested = false;
            ClearBufferedAttack();
            _alreadyHitThisSwing.Clear();

            AttackStepData step = GetStep(comboStep);
            _currentComboStep = step.comboStep;

            // CL-107: AttackSpeed multiplier 적용. AttackStepData 는 [Serializable] (WeaponData.Steps 배열 항목)
            // → in-place 변경 시 SO 데이터 변조됨. 반드시 local 변수로 캡처.
            float speedMul = statContainer != null ? statContainer.GetTotalMultiplier(StatId.AttackSpeed) : 1f;
            if (speedMul <= 0f) speedMul = 1f;
            float startupDur = step.startupDuration / speedMul;
            float activeDur = step.activeDuration / speedMul;
            float recoveryDur = step.recoveryDuration / speedMul;

            Vector2 aimDirection = aim != null ? aim.GetAimDirection() : Vector2.right;
            if (aimDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                aimDirection = Vector2.right;
            }
            float aimAngleDeg = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
            KhiAttackRequest request = new KhiAttackRequest
            {
                SequenceId = ++_sequenceId,
                ComboStep = step.comboStep,
                AimDirection = aimDirection,
                AimAngleDegrees = aimAngleDeg,
                Origin = transform.position,
                StartedAt = Time.time,
                Attacker = gameObject
            };

            AttackStarted?.Invoke(request, step);
            _chainInputAllowedAt = Mathf.Max(
                request.StartedAt + weaponData.MinimumChainInputDelay,
                request.StartedAt + startupDur + activeDur * 0.5f);

            if (startupDur > 0f)
            {
                yield return new WaitForSeconds(startupDur);
            }

            if (_externalAbortRequested)
            {
                FinalizeAbortedAttack();
                yield break;
            }

            bool hitAnyTarget = false;
            float activeEndsAt = Time.time + activeDur;
            // active 시작 시점의 위치를 request.Origin에 반영 (windup 중 플레이어 이동 보정).
            request.Origin = transform.position;
            AttackActiveStarted?.Invoke(request, step);

            while (Time.time < activeEndsAt && !_externalAbortRequested)
            {
                KhiAttackRequest sampleRequest = request;
                sampleRequest.Origin = transform.position;
                // CL-107: AttackPower multiplier (전사의끈/전투북 등) + FinisherDamage multiplier (분쇄의팔찌, 3타에만).
                float attackMul = statContainer != null ? statContainer.GetTotalMultiplier(StatId.AttackPower) : 1f;
                float finisherMul = (statContainer != null && step.comboStep == 3)
                    ? statContainer.GetTotalMultiplier(StatId.FinisherDamage) : 1f;
                float finalDamage = weaponData.BaseDamage * step.damageMultiplier * attackMul * finisherMul;

                // CL-142: 치명타 처리 — Magnitude 가 critChance (0.25 = 25%), 적중 시 ×2 데미지
                float critChance = statContainer != null
                    ? Mathf.Max(0f, statContainer.GetTotalMultiplier(StatId.Critical) - 1f)
                    : 0f;
                if (critChance > 0f && UnityEngine.Random.value < critChance)
                {
                    finalDamage *= 2f;
                }
                int sampledHitCount = hitbox != null
                    ? hitbox.Sample(sampleRequest, step, finalDamage, _alreadyHitThisSwing, _hitsThisSample)
                    : 0;
                hitAnyTarget |= sampledHitCount > 0;

                for (int i = 0; i < _hitsThisSample.Count; i++)
                {
                    Health hit = _hitsThisSample[i];
                    TargetHit?.Invoke(sampleRequest, step, hit);
                    // CL-107: 본 hit 으로 사망한 적 → EnemyKilledByPlayer 발화 (붉은송곳니용).
                    if (hit != null && hit.CurrentHealth <= 0f)
                    {
                        EnemyKilledByPlayer?.Invoke(sampleRequest, step, hit);
                    }
                }

                yield return null;
            }

            AttackActiveEnded?.Invoke(request, step);
            hitbox?.HideRuntimePreview();

            if (_externalAbortRequested)
            {
                FinalizeAbortedAttack();
                yield break;
            }

            if (hitAnyTarget && step.comboStep == 3)
            {
                FinisherHit?.Invoke(request, step);
                if (logHitsToConsole)
                {
                    Debug.Log($"[KhiMelee] Finisher hit sequence={request.SequenceId}");
                }
            }

            _isInAttackRecovery = true;
            float recoveryEndsAt = Time.time + recoveryDur;
            while (Time.time < recoveryEndsAt && !_externalAbortRequested)
            {
                yield return null;
            }

            if (_externalAbortRequested)
            {
                FinalizeAbortedAttack();
                yield break;
            }

            bool shouldChainBufferedAttack = HasValidBufferedAttack(step.comboStep);
            ClearBufferedAttack();
            AdvanceCombo(step.comboStep);
            _isAttacking = false;
            _isInAttackRecovery = false;
            _currentComboStep = 0;

            if (shouldChainBufferedAttack)
            {
                RequestAttack();
            }
        }

        private void FinalizeAbortedAttack()
        {
            ClearBufferedAttack();
            _nextComboStep = 1;
            _comboExpiresAt = -1f;
            _isAttacking = false;
            _isInAttackRecovery = false;
            _currentComboStep = 0;
            _externalAbortRequested = false;
        }

        private bool ShouldBlockAttackForDash()
        {
            if (dash == null || !dash.IsDashing || allowAttackDuringDash)
            {
                return false;
            }

            if (bufferAttackDuringDash && _isAttacking)
            {
                TryBufferAttack();
            }

            return true;
        }

        private void TryBufferAttack()
        {
            if (!allowInputDuringRecovery || Time.time < _chainInputAllowedAt)
            {
                return;
            }

            if (_currentComboStep >= 3)
            {
                return;
            }

            _bufferedAttackExpiresAt = Time.time + weaponData.InputBufferDuration;
        }

        private bool HasValidBufferedAttack(int completedStep)
        {
            return completedStep < 3 && _bufferedAttackExpiresAt >= Time.time;
        }

        private void ClearBufferedAttack()
        {
            _bufferedAttackExpiresAt = -1f;
        }

        private void AdvanceCombo(int completedStep)
        {
            if (completedStep >= 3)
            {
                _nextComboStep = 1;
                _comboExpiresAt = -1f;
                return;
            }

            _nextComboStep = completedStep + 1;
            _comboExpiresAt = Time.time + weaponData.ComboInputWindow;
        }

        private AttackStepData GetStep(int comboStep)
        {
            AttackStepData[] steps = weaponData.Steps;
            for (int i = 0; i < steps.Length; i++)
            {
                if (steps[i] != null && steps[i].comboStep == comboStep)
                {
                    return steps[i];
                }
            }
            // Fallback: 첫 번째 step 사용 (Awake 검증으로 null 보장됨)
            return steps[0];
        }

        private void DisableTdeWeaponHandling()
        {
            _tdeHandleWeapon ??= GetComponent<CharacterHandleWeapon>();
            if (_tdeHandleWeapon == null)
            {
                return;
            }

            _tdeHandleWeapon.ChangeWeapon(null, string.Empty);
            _tdeHandleWeapon.InitialWeapon = null;
            _tdeHandleWeapon.PermitAbility(false);
        }

        private static bool WasAttackPressedThisFrame()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                return true;
            }

            Gamepad gamepad = Gamepad.current;
            return gamepad != null && gamepad.buttonWest.wasPressedThisFrame;
        }
    }
}
