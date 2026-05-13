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
        [Tooltip("RestorePermits 시 ExternalBlock cache 가 race condition 으로 꼬이지 않도록 HitStun 상태를 확인하는 용도.")]
        [SerializeField] private KhiHitStunController hitStun;

        [Header("Parry Range")]
        [SerializeField, Min(0f), Tooltip("패링 입력 유효 시간 (초). 짧을수록 어려움. (시간 범위)")]
        private float parryWindow = 0.16f;
        [SerializeField, Tooltip("Player 의 메인 hurtbox Collider2D (Health 와 같은 collider). ParryWindow 상태에서만 일시적으로 parryHitboxScale 배수로 확대되어 빗나간 적 공격도 잡힘. Idle 시 원래 size 로 복원. BoxCollider2D / CircleCollider2D 지원.")]
        private Collider2D parryHitbox;
        [SerializeField, Min(1f), Tooltip("ParryWindow 동안 parryHitbox 의 size 확대 배수. 1=원본 (스케일 없음), 1.5=50% 확대 → 약간 빗나가도 잡힘. 너무 크면 (>2) 의도하지 않은 데미지도 같이 잡혀버려 부자연스러움. 1.2~1.5 권장.")]
        private float parryHitboxScale = 1.3f;

        [Header("Timings")]
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

        // parryHitbox 의 원본 size/radius 캐시 — parryHitboxScale 곱셈 base.
        private bool _hasCachedHitboxBase;
        private Vector2 _cachedHitboxBoxSize;
        private float _cachedHitboxCircleRadius;

        public event Action ParryStarted;
        public event Action ParrySucceeded;
        public event Action ParryFailed;
        public event Action ParryEnded;

        public KhiParryState CurrentState => _state;
        public bool IsParryWindowActive => _state == KhiParryState.ParryWindow;
        public bool IsParryRecovering => _state == KhiParryState.FailureRecovery;
        public bool IsParryInCooldown => _state == KhiParryState.Cooldown;
        public float CurrentStateRemaining => _state == KhiParryState.Idle ? 0f : Mathf.Max(0f, _stateEndTime - Time.time);

        // 외부 시스템 (보상 패널 등) 이 패링 input 을 일시 차단할 때 사용. KhiMeleeComboController.ExternalBlock 과 동일 패턴.
        public bool ExternalBlock { get; set; }

        /// <summary>패링 쿨다운 (Cooldown 상태) 총 길이. UI 진행률 계산용.</summary>
        public float ParryCooldownDuration => parryCooldown;
        /// <summary>패링 실패 후 회복 (FailureRecovery 상태) 총 길이. UI 진행률 계산용.</summary>
        public float ParryFailureRecoveryDuration => parryFailureRecovery;

        private void Awake()
        {
            handleWeapon ??= GetComponent<CharacterHandleWeapon>();
            dashController ??= GetComponent<KhiDashController>();
            meleeCombo ??= GetComponent<KhiMeleeComboController>();
            health ??= GetComponent<Health>();
            hitStun ??= GetComponent<KhiHitStunController>();
            CacheHitboxBaseIfNeeded();
            // Idle 상태로 시작 — base size 유지 (scale 미적용).
            ResetHitboxToBase();
        }

        /// <summary>BoxCollider2D / CircleCollider2D 의 원본 size/radius 를 1회 캐시. _hasCachedHitboxBase 가 true 면 no-op.</summary>
        private void CacheHitboxBaseIfNeeded()
        {
            if (_hasCachedHitboxBase || parryHitbox == null)
            {
                return;
            }

            if (parryHitbox is BoxCollider2D box)
            {
                _cachedHitboxBoxSize = box.size;
                _hasCachedHitboxBase = true;
            }
            else if (parryHitbox is CircleCollider2D circle)
            {
                _cachedHitboxCircleRadius = circle.radius;
                _hasCachedHitboxBase = true;
            }
            // 다른 collider 타입은 scale 미지원 (캐시 안 함). parryHitbox reference 자체는 사용자 노출용으로 유지.
        }

        /// <summary>ParryWindow 진입 시 호출 — parryHitboxScale 배수로 hurtbox 일시 확대.</summary>
        private void ApplyHitboxScale()
        {
            if (!_hasCachedHitboxBase || parryHitbox == null)
            {
                return;
            }

            float scale = Mathf.Max(1f, parryHitboxScale);
            if (parryHitbox is BoxCollider2D box)
            {
                box.size = _cachedHitboxBoxSize * scale;
            }
            else if (parryHitbox is CircleCollider2D circle)
            {
                circle.radius = _cachedHitboxCircleRadius * scale;
            }
        }

        /// <summary>ParryWindow 종료 시 호출 — 캐시된 base size 로 복원.</summary>
        private void ResetHitboxToBase()
        {
            if (!_hasCachedHitboxBase || parryHitbox == null)
            {
                return;
            }

            if (parryHitbox is BoxCollider2D box)
            {
                box.size = _cachedHitboxBoxSize;
            }
            else if (parryHitbox is CircleCollider2D circle)
            {
                circle.radius = _cachedHitboxCircleRadius;
            }
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
            // Safety A: 비-Idle 상태가 _stateEndTime 을 2초 이상 초과 → stuck 으로 간주, 강제 복구.
            // 정상 흐름이면 TickState 에서 이미 전이됐어야 함. timeScale=0 / 코루틴 정지 / 이벤트 핸들러 throw 등이 원인 후보.
            // 보너스 안전장치 — 진짜 fix 는 원인 추적 별도 진행 (cf. plan: dash_invuln_parry_feedback_plan.md Known Issues).
            if (_state != KhiParryState.Idle && Time.time > _stateEndTime + 2f)
            {
                Debug.LogWarning($"[KhiParryController] state stuck ({_state}) for {Time.time - _stateEndTime:F1}s past _stateEndTime. ForceIdle as safety.");
                ForceIdle();
            }

            // Safety B: Idle 상태인데 permits가 남아있으면 복원
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
            if (ExternalBlock)
            {
                return;
            }

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
                ResetHitboxToBase();
                return;
            }

            RestorePermits();
            ResetHitboxToBase();
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
            // B-lite: 윈도우 동안만 hurtbox 일시 확대 — 빗나간 공격도 잡힘. 윈도우 종료 시 reset.
            ApplyHitboxScale();

            _state = KhiParryState.ParryWindow;
            _stateEndTime = Time.time + parryWindow;

            LogTransition("EnterParryWindow");
            ParryStarted?.Invoke();
        }

        private void HandleParrySuccess()
        {
            // 공격/대시 즉시 복원 + hurtbox base size 로 reset.
            RestorePermits();
            ResetHitboxToBase();

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
            // hurtbox 는 base size 로 reset — 윈도우 끝났으니 확대 유지하면 후딜 중에도 큰 hitbox 로 데미지 받음.
            ResetHitboxToBase();
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

            // [Race condition fix] cache 신뢰 X — HitStun 이 같은 4개 권한
            // (handleWeapon / dashController / meleeCombo.ExternalBlock / movement) 을 동시에 토글하므로,
            // 패링 진입 시 cache 한 값이 "HitStun 이 만든 차단 상태" 일 수 있음 → 복원 시 잘못된 차단이 영구히 남음.
            // 대신 HitStun 이 active 면 차단 유지(HitStun 이 자기 종료 시 풀어줌), 아니면 정상 상태로 복원.
            bool hitStunStillBlocking = hitStun != null && hitStun.IsStunned;

            if (handleWeapon != null)
            {
                handleWeapon.AbilityPermitted = !hitStunStillBlocking;
            }

            if (dashController != null)
            {
                dashController.PermitAbility(!hitStunStillBlocking);
            }

            if (meleeCombo != null)
            {
                meleeCombo.ExternalBlock = hitStunStillBlocking;
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
