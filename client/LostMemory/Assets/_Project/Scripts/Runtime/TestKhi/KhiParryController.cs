using System;
using MoreMountains.TopDownEngine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LostMemory.TestKhi
{
    public enum KhiParryState
    {
        Idle,
        ParryWindow,
        FailureRecovery,
        Cooldown
    }

    /// <summary>
    /// CL-012 타이밍 패링 컨트롤러.
    /// 우클릭으로 짧은 패링 창을 열어 그 시간 안에 들어온 피해를 무효화한다.
    /// 실패하면 긴 후딜에 들어가고, 후딜 중 피해는 원래 피해의 일부만 적용된다.
    /// TDE Health/DamageOnTouch 원본은 수정하지 않는다.
    /// 피해 소스(KhiParryDamageOnTouch)가 피해 적용 전에 TryResolveIncomingDamage를 호출하는 방식.
    /// </summary>
    [AddComponentMenu("Lost Memory/Test Khi/Khi Parry Controller")]
    [DefaultExecutionOrder(60)]
    public class KhiParryController : MonoBehaviour
    {
        [Header("Refs (optional, auto-resolved in Awake)")]
        [SerializeField] private CharacterHandleWeapon handleWeapon;
        [SerializeField] private KhiDashController dashController;
        [SerializeField] private KhiMeleeComboController meleeCombo;
        [SerializeField] private Health health;
        [SerializeField] private InputActionReference parryAction;

        [Header("Timings")]
        [SerializeField, Min(0f)] private float parryWindow = 0.16f;
        [SerializeField, Min(0f)] private float parryFailureRecovery = 0.45f;
        [SerializeField, Min(0f)] private float parryCooldown = 0.45f;
        [SerializeField, Min(0f)] private float successfulParryInvulnerability = 0.12f;
        [SerializeField, Range(0f, 1f)] private float failedParryDamageRatio = 0.2f;
        [SerializeField, Min(0f)] private float minFailedParryDamage = 1f;

        [Header("Policy")]
        [SerializeField] private bool allowParryDuringAttack = false;
        [SerializeField] private bool allowParryDuringAttackRecovery = false;
        [SerializeField] private bool allowParryDuringDash = false;

        [Header("Debug")]
        [SerializeField] private bool logStateTransitions = false;

        private KhiParryState _state = KhiParryState.Idle;
        private float _stateEndTime;

        private bool _hasCachedPermits;
        private bool _cachedHandleWeaponPermitted;
        private bool _cachedDashPermitted;
        private bool _cachedMeleeExternalBlock;

        public event Action ParryStarted;
        public event Action ParrySucceeded;
        public event Action ParryFailed;
        public event Action ParryEnded;

        public KhiParryState CurrentState => _state;
        public bool IsParryWindowActive => _state == KhiParryState.ParryWindow;
        public bool IsParryRecovering => _state == KhiParryState.FailureRecovery;
        public bool IsParryInCooldown => _state == KhiParryState.Cooldown;
        public float CurrentStateRemaining => _state == KhiParryState.Idle ? 0f : Mathf.Max(0f, _stateEndTime - Time.time);

        private void Awake()
        {
            handleWeapon ??= GetComponent<CharacterHandleWeapon>();
            dashController ??= GetComponent<KhiDashController>();
            meleeCombo ??= GetComponent<KhiMeleeComboController>();
            health ??= GetComponent<Health>();
        }

        private void OnEnable()
        {
            if (parryAction != null && parryAction.action != null && !parryAction.action.enabled)
            {
                parryAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            RestorePermits();
        }

        private void OnDestroy()
        {
            RestorePermits();
        }

        private void Update()
        {
            TickState();
            HandleParryInput();
        }

        private void LateUpdate()
        {
            // Safety: Idle 상태인데 permits가 남아있으면 복원
            if (_state == KhiParryState.Idle && _hasCachedPermits)
            {
                RestorePermits();
            }
        }

        private void TickState()
        {
            if (_state == KhiParryState.Idle)
            {
                return;
            }

            if (Time.time < _stateEndTime)
            {
                return;
            }

            switch (_state)
            {
                case KhiParryState.ParryWindow:
                    TransitionToFailureRecovery();
                    break;
                case KhiParryState.FailureRecovery:
                    TransitionToCooldown();
                    break;
                case KhiParryState.Cooldown:
                    TransitionToIdle();
                    break;
            }
        }

        private void HandleParryInput()
        {
            if (_state != KhiParryState.Idle)
            {
                return;
            }

            if (!WasParryPressedThisFrame())
            {
                return;
            }

            if (IsBlockedByPlayerState())
            {
                return;
            }

            EnterParryWindow();
        }

        private bool IsBlockedByPlayerState()
        {
            if (!allowParryDuringAttack && meleeCombo != null && meleeCombo.IsAttacking)
            {
                return true;
            }

            if (!allowParryDuringAttackRecovery && meleeCombo != null && meleeCombo.IsInAttackRecovery)
            {
                return true;
            }

            if (!allowParryDuringDash && dashController != null && dashController.IsDashing)
            {
                return true;
            }

            return false;
        }

        private bool WasParryPressedThisFrame()
        {
            if (parryAction != null && parryAction.action != null)
            {
                if (!parryAction.action.enabled)
                {
                    parryAction.action.Enable();
                }

                if (parryAction.action.WasPressedThisFrame())
                {
                    return true;
                }
            }

            Mouse mouse = Mouse.current;
            return mouse != null && mouse.rightButton.wasPressedThisFrame;
        }

        /// <summary>
        /// 피해 소스가 피해 적용 직전에 호출한다.
        /// </summary>
        /// <returns>
        /// true이면 피해 소스는 자체 기본 피해 경로를 실행하지 말고 resolvedDamage에 따라 처리해야 한다.
        /// resolvedDamage가 0이면 피해 적용 스킵, 0보다 크면 감쇠 피해 적용.
        /// false이면 피해 소스는 기존 기본 경로를 그대로 실행한다.
        /// </returns>
        /// <summary>
        /// 외부 시스템(예: KhiDownController)이 패링 상태를 강제로 Idle로 되돌려야 할 때 사용한다.
        /// permits 복원까지 idempotent하게 수행. 이미 Idle이면 no-op.
        /// </summary>
        public void ForceIdle()
        {
            if (_state == KhiParryState.Idle)
            {
                RestorePermits();
                return;
            }

            RestorePermits();
            _state = KhiParryState.Idle;
            LogTransition("ForceIdle (external request)");
            ParryEnded?.Invoke();
        }

        public bool TryResolveIncomingDamage(
            GameObject instigator,
            Vector2 incomingDirection,
            float incomingDamage,
            out float resolvedDamage)
        {
            switch (_state)
            {
                case KhiParryState.ParryWindow:
                    resolvedDamage = 0f;
                    HandleParrySuccess();
                    return true;

                case KhiParryState.FailureRecovery:
                    resolvedDamage = Mathf.Max(minFailedParryDamage, incomingDamage * failedParryDamageRatio);
                    return true;

                default:
                    resolvedDamage = incomingDamage;
                    return false;
            }
        }

        private void EnterParryWindow()
        {
            CachePermitsIfNeeded();
            ApplyBlockingPermits();

            _state = KhiParryState.ParryWindow;
            _stateEndTime = Time.time + parryWindow;

            LogTransition("EnterParryWindow");
            ParryStarted?.Invoke();
        }

        private void HandleParrySuccess()
        {
            // 공격/대시 즉시 복원
            RestorePermits();

            if (health != null && successfulParryInvulnerability > 0f)
            {
                health.DamageDisabled();
                StartCoroutine(health.DamageEnabled(successfulParryInvulnerability));
            }

            _state = KhiParryState.Cooldown;
            _stateEndTime = Time.time + parryCooldown;

            LogTransition("ParrySuccess -> Cooldown");
            ParrySucceeded?.Invoke();
        }

        private void TransitionToFailureRecovery()
        {
            // permits는 그대로 유지 (실패 후딜 동안 공격/대시 차단)
            _state = KhiParryState.FailureRecovery;
            _stateEndTime = Time.time + parryFailureRecovery;

            LogTransition("ParryWindow timeout -> FailureRecovery");
            ParryFailed?.Invoke();
        }

        private void TransitionToCooldown()
        {
            RestorePermits();

            _state = KhiParryState.Cooldown;
            _stateEndTime = Time.time + parryCooldown;

            LogTransition("FailureRecovery -> Cooldown");
        }

        private void TransitionToIdle()
        {
            RestorePermits();

            _state = KhiParryState.Idle;

            LogTransition("Cooldown -> Idle");
            ParryEnded?.Invoke();
        }

        private void CachePermitsIfNeeded()
        {
            if (_hasCachedPermits)
            {
                return;
            }

            _cachedHandleWeaponPermitted = handleWeapon != null ? handleWeapon.AbilityPermitted : true;
            _cachedDashPermitted = dashController != null ? dashController.AbilityPermitted : true;
            _cachedMeleeExternalBlock = meleeCombo != null && meleeCombo.ExternalBlock;
            _hasCachedPermits = true;
        }

        private void ApplyBlockingPermits()
        {
            if (handleWeapon != null)
            {
                handleWeapon.AbilityPermitted = false;
            }

            if (dashController != null)
            {
                dashController.PermitAbility(false);
            }

            if (meleeCombo != null)
            {
                meleeCombo.ExternalBlock = true;
            }
        }

        private void RestorePermits()
        {
            if (!_hasCachedPermits)
            {
                return;
            }

            if (handleWeapon != null)
            {
                handleWeapon.AbilityPermitted = _cachedHandleWeaponPermitted;
            }

            if (dashController != null)
            {
                dashController.PermitAbility(_cachedDashPermitted);
            }

            if (meleeCombo != null)
            {
                meleeCombo.ExternalBlock = _cachedMeleeExternalBlock;
            }

            _hasCachedPermits = false;
        }

        private void LogTransition(string label)
        {
            if (!logStateTransitions)
            {
                return;
            }

            Debug.Log($"[KhiParry] {label} t={Time.time:F3}");
        }

        // ── 임시 디버그 (race condition 추적용. 진단 끝나면 제거 또는 #if UNITY_EDITOR 로 감싸기) ──

        [ContextMenu("Debug — Dump permit state")]
        private void DebugDumpPermitState()
        {
            string weaponState = handleWeapon != null ? handleWeapon.AbilityPermitted.ToString() : "null";
            string dashState = dashController != null ? dashController.AbilityPermitted.ToString() : "null";
            string meleeState = meleeCombo != null ? meleeCombo.ExternalBlock.ToString() : "null";
            Debug.Log($"[KhiParry-DBG] state={_state}, hasCache={_hasCachedPermits}, " +
                      $"cached(weapon={_cachedHandleWeaponPermitted}, dash={_cachedDashPermitted}, melee={_cachedMeleeExternalBlock}), " +
                      $"current(weapon={weaponState}, dash={dashState}, melee={meleeState}), " +
                      $"timeScale={Time.timeScale}");
        }

        [ContextMenu("Debug — Force restore permits (panic)")]
        private void DebugForceRestorePermits()
        {
            if (handleWeapon != null) handleWeapon.AbilityPermitted = true;
            if (dashController != null) dashController.PermitAbility(true);
            if (meleeCombo != null) meleeCombo.ExternalBlock = false;
            _hasCachedPermits = false;
            Debug.Log("[KhiParry-DBG] All permits force-restored. state was=" + _state);
        }
    }
}
