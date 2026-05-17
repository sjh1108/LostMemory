using System.Collections;
using System.Collections.Generic;
using LostMemory.Combat;
using LostMemory.Data;
using MoreMountains.TopDownEngine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LostMemory.TestKhi
{
    /// <summary>
    /// CL-230: 단검 모드 (Sword_Dagger / Sword_DaggerNinja) 일 때 우클릭 →
    /// aim 방향 짧은 거리 텔레포트 + 도착 지점에 슬래시 + 마나 소비 + 짧은 무적.
    /// 회피기 겸 어쌔신 시그니처 무브.
    ///
    /// 우클릭 충돌: KhiParryController(패링) 가 우클릭 사용 → WeaponUpgradeService 가
    /// 단검 모드 진입 시 swordParry.enabled = false 로 비활성화 (별도 처리).
    /// 본 컴포넌트는 항상 enabled. 단검 모드 체크는 매 Update 마다 WeaponUpgradeService.CurrentKind 로.
    /// </summary>
    [AddComponentMenu("Lost Memory/Test Khi/Khi Dagger Teleport Controller")]
    [DefaultExecutionOrder(60)]
    public class KhiDaggerTeleportController : MonoBehaviour
    {
        [Header("Refs (Awake 에서 자동 resolve 가능)")]
        [SerializeField] private KhiPlayerAim aim;
        [SerializeField] private KhiMeleeComboController combo;
        [SerializeField] private KhiMeleeHitbox hitbox;
        [SerializeField] private KhiSlashAnimator slashAnimator;
        [SerializeField] private LostMemory.Combat.PlayerMana mana;
        [SerializeField] private Health health;
        [SerializeField] private LostMemory.Combat.PlayerStatModifierContainer statContainer;

        [Header("Teleport")]
        [Tooltip("aim 방향으로 텔레포트할 거리 (unit).")]
        [SerializeField, Min(0f)] private float teleportDistance = 3f;
        [Tooltip("텔레포트 직후 무적 시간 (초).")]
        [SerializeField, Min(0f)] private float invulnerabilityDuration = 0.15f;
        [Tooltip("텔레포트 쿨다운 (초). 마지막 텔레포트 이후 이 시간 동안 우클릭 무시.")]
        [SerializeField, Min(0f)] private float cooldown = 0.8f;

        [Header("Mana")]
        [Tooltip("텔레포트 1회당 마나 소비량. 마나 부족 시 텔레포트 안 됨.")]
        [SerializeField, Min(0)] private int manaCost = 15;

        [Header("Slash")]
        [Tooltip("도착 지점에 표시할 슬래시 step (1/2/3). 단검 1타 슬래시를 재사용 권장. " +
                 "teleportSlashStep 이 valid (slashFrames 비어있지 않음) 면 무시됨.")]
        [SerializeField, Range(1, 3)] private int slashComboStep = 1;
        [Tooltip("ON: 도착 지점에 슬래시 + hitbox 데미지 판정. OFF: 시각만.")]
        [SerializeField] private bool applyDamageOnArrive = true;

        [Header("Teleport-Only Slash Step (CL-230: 우클릭 전용 슬래시)")]
        [Tooltip("우클릭 텔레포트 전용 AttackStepData. 비어있으면 (slashFrames 없음) 위 slashComboStep 의 SO step 사용. " +
                 "여기에 frames/tint/transform/extra slashes/hitbox/damage 설정하면 일반 공격과 독립된 슬래시.")]
        [SerializeField] private LostMemory.Data.AttackStepData teleportSlashStep;

        [Header("Debug")]
        [SerializeField] private bool logTeleport = false;

        private float _nextAllowedAt = -1f;
        private readonly HashSet<Health> _alreadyHit = new HashSet<Health>();
        private readonly List<Health> _hitsThisSample = new List<Health>(8);

        private void Awake()
        {
            aim ??= GetComponent<KhiPlayerAim>();
            combo ??= GetComponent<KhiMeleeComboController>();
            hitbox ??= GetComponent<KhiMeleeHitbox>();
            slashAnimator ??= GetComponent<KhiSlashAnimator>();
            mana ??= GetComponent<LostMemory.Combat.PlayerMana>() ?? GetComponentInParent<LostMemory.Combat.PlayerMana>();
            health ??= GetComponent<Health>() ?? GetComponentInParent<Health>();
            statContainer ??= GetComponent<LostMemory.Combat.PlayerStatModifierContainer>()
                              ?? GetComponentInParent<LostMemory.Combat.PlayerStatModifierContainer>();
        }

        private void Update()
        {
            // 1. 단검 모드 체크 (WeaponUpgradeService)
            if (!IsDaggerMode())
            {
                return;
            }

            // 2. 쿨다운 체크
            if (Time.time < _nextAllowedAt)
            {
                return;
            }

            // 3. 우클릭 입력 체크
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.rightButton.wasPressedThisFrame)
            {
                return;
            }

            // 4. 마나 체크
            if (mana != null && !mana.HasEnough(manaCost))
            {
                if (logTeleport) Debug.Log($"[KhiDaggerTeleport] 마나 부족 ({mana.CurrentMana}/{manaCost}) — 텔레포트 취소", this);
                return;
            }

            ExecuteTeleport();
        }

        private bool IsDaggerMode()
        {
            var service = WeaponUpgradeService.Instance;
            if (service == null) return false;
            return service.CurrentKind == WeaponUpgradeService.WeaponKind.Dagger
                || service.CurrentKind == WeaponUpgradeService.WeaponKind.DaggerNinja;
        }

        private void ExecuteTeleport()
        {
            // 1. aim 방향
            Vector2 aimDir = aim != null ? aim.GetAimDirection() : Vector2.right;
            if (aimDir.sqrMagnitude <= Mathf.Epsilon)
            {
                aimDir = Vector2.right;
            }

            // 2. 위치 이동
            Vector3 newPos = transform.position + (Vector3)(aimDir * teleportDistance);
            transform.position = newPos;

            // 3. 마나 소비
            if (mana != null)
            {
                mana.Consume(manaCost);
            }

            // 4. 무적 (Health.DamageDisabled + 코루틴으로 복귀)
            if (health != null && invulnerabilityDuration > 0f)
            {
                health.DamageDisabled();
                StartCoroutine(EnableDamageLater());
            }

            // 5. 어느 step 사용할지 결정 (텔레포트 전용 step 우선, 없으면 SO step fallback)
            AttackStepData stepToUse = ResolveStep();

            // 6. 슬래시 시각 표시
            if (slashAnimator != null && stepToUse != null)
            {
                slashAnimator.PlaySlashFromExternal(aimDir, stepToUse);
            }

            // 7. hitbox 데미지 판정 (옵션)
            if (applyDamageOnArrive && hitbox != null && stepToUse != null && combo != null && combo.WeaponData != null)
            {
                ApplyDamageAtArrive(aimDir, stepToUse);
            }

            // 7. 쿨다운 갱신
            _nextAllowedAt = Time.time + cooldown;

            if (logTeleport)
            {
                Debug.Log($"[KhiDaggerTeleport] 텔레포트 → dir={aimDir} dist={teleportDistance} mana={mana?.CurrentMana ?? -1}", this);
            }
        }

        /// <summary>
        /// 우클릭 텔레포트 전용 step (teleportSlashStep) 이 valid 면 그걸 사용.
        /// 아니면 SO 의 slashComboStep (1/2/3) 에 해당하는 step fallback.
        /// </summary>
        private AttackStepData ResolveStep()
        {
            // 텔레포트 전용 step 이 valid (slashFrames 있음) 면 우선
            if (teleportSlashStep != null && teleportSlashStep.slashFrames != null && teleportSlashStep.slashFrames.Length > 0)
            {
                return teleportSlashStep;
            }
            // fallback: SO 의 step
            if (combo != null && combo.WeaponData != null && combo.WeaponData.Steps != null && combo.WeaponData.Steps.Length > 0)
            {
                int slotIndex = Mathf.Clamp(slashComboStep - 1, 0, combo.WeaponData.Steps.Length - 1);
                return combo.WeaponData.Steps[slotIndex];
            }
            return null;
        }

        private void ApplyDamageAtArrive(Vector2 aimDir, AttackStepData step)
        {
            // 데미지 계산 (KhiMeleeComboController.RunAttack 패턴 차용).
            // baseDamage 는 combo.WeaponData 기준 (단검 무기의 기본 데미지). teleportSlashStep 의 damageMultiplier 가 그 위에 곱해짐.
            float attackMul = statContainer != null ? statContainer.GetTotalMultiplier(StatId.AttackPower) : 1f;
            float finalDamage = combo.WeaponData.BaseDamage * step.damageMultiplier * attackMul;

            // KhiAttackRequest 생성 (가짜 request)
            KhiAttackRequest request = new KhiAttackRequest
            {
                SequenceId = 0,
                ComboStep = step.comboStep,
                AimDirection = aimDir,
                AimAngleDegrees = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg,
                Origin = transform.position,
                StartedAt = Time.time,
                Attacker = gameObject
            };

            _alreadyHit.Clear();
            hitbox.Sample(request, step, combo.WeaponData.GlobalHitboxPostRotationOffset, finalDamage, _alreadyHit, _hitsThisSample);
        }

        private IEnumerator EnableDamageLater()
        {
            yield return new WaitForSeconds(invulnerabilityDuration);
            if (health != null) health.DamageEnabled();
        }
    }
}
