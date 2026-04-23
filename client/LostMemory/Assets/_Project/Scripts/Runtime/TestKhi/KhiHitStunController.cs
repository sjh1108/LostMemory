using System;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.TestKhi
{
    public enum KhiHitStunState
    {
        Idle,
        HitStun,
        PostHitIFrame
    }

    /// <summary>
    /// CL-013 플레이어 피격 경직 컨트롤러.
    /// Health.OnHit 콜백을 구독해 피해가 실제로 적용된 순간 짧은 제어 불가 상태(경직)와
    /// 후속 i-frame을 부여한다. Parry가 permits를 소유 중이면 충돌 회피를 위해 진입하지 않는다.
    /// TDE Health/CharacterMovement 원본은 수정하지 않는다.
    /// </summary>
    [AddComponentMenu("Lost Memory/Test Khi/Khi Hit Stun Controller")]
    [DefaultExecutionOrder(65)]
    public class KhiHitStunController : MonoBehaviour
    {
        [Header("Refs (optional, auto-resolved in Awake)")]
        [SerializeField] private Health health;
        [SerializeField] private CharacterHandleWeapon handleWeapon;
        [SerializeField] private KhiDashController dashController;
        [SerializeField] private KhiMeleeComboController meleeCombo;
        [SerializeField] private CharacterMovement characterMovement;
        [SerializeField] private KhiParryController parryController;
        [SerializeField] private Animator animator;

        [Header("Timings")]
        [SerializeField, Min(0f)] private float hitStunDuration = 0.12f;
        [SerializeField, Min(0f)] private float postHitInvulnerability = 0.15f;

        [Header("Behavior")]
        [SerializeField] private bool interruptMeleeComboOnHit = true;
        [SerializeField] private bool setAnimatorTrigger = true;
        [SerializeField] private string hitAnimatorTriggerName = "Hit";

        [Header("Debug")]
        [SerializeField] private bool logStateTransitions = false;

        private KhiHitStunState _state = KhiHitStunState.Idle;
        private float _stateEndTime;

        private bool _hasCachedPermits;
        private bool _cachedHandleWeaponPermitted;
        private bool _cachedDashPermitted;
        private bool _cachedMeleeExternalBlock;
        private bool _cachedMovementForbidden;

        /// <summary>경직 진입 시 경직 duration을 페이로드로 발사. CL-017 히트스톱/플래시용.</summary>
        public event Action<float> HitStunStarted;
        public event Action HitStunEnded;
        /// <summary>경직 진입 시 한 번 발사. PlayerCombatReporter가 Player.Hit 글로벌 이벤트로 승격 예정.</summary>
        public event Action HitReceived;

        public KhiHitStunState CurrentState => _state;
        public bool IsStunned => _state == KhiHitStunState.HitStun;
        public bool IsInvulnerable => _state == KhiHitStunState.HitStun || _state == KhiHitStunState.PostHitIFrame;
        public float CurrentStateRemaining => _state == KhiHitStunState.Idle ? 0f : Mathf.Max(0f, _stateEndTime - Time.time);

        private void Awake()
        {
            health ??= GetComponent<Health>() ?? GetComponentInChildren<Health>();
            handleWeapon ??= GetComponent<CharacterHandleWeapon>();
            dashController ??= GetComponent<KhiDashController>();
            meleeCombo ??= GetComponent<KhiMeleeComboController>();
            characterMovement ??= GetComponent<CharacterMovement>();
            parryController ??= GetComponent<KhiParryController>();

            if (animator == null)
            {
                if (health != null && health.TargetAnimator != null)
                {
                    animator = health.TargetAnimator;
                }
                else
                {
                    animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
                }
            }
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.OnHit += HandleHealthHit;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.OnHit -= HandleHealthHit;
            }

            RestorePermits();
            _state = KhiHitStunState.Idle;
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.OnHit -= HandleHealthHit;
            }

            RestorePermits();
        }

        private void Update()
        {
            TickState();
        }

        private void LateUpdate()
        {
            // Safety: Idle 상태인데 permits가 남아있으면 복원
            if (_state == KhiHitStunState.Idle && _hasCachedPermits)
            {
                RestorePermits();
            }
        }

        private void TickState()
        {
            if (_state == KhiHitStunState.Idle)
            {
                return;
            }

            if (Time.time < _stateEndTime)
            {
                return;
            }

            switch (_state)
            {
                case KhiHitStunState.HitStun:
                    TransitionToPostHitIFrame();
                    break;
                case KhiHitStunState.PostHitIFrame:
                    TransitionToIdle();
                    break;
            }
        }

        /// <summary>
        /// Health.OnHit 구독 엔트리. 선행 조건을 모두 통과하면 경직에 진입한다.
        /// </summary>
        private void HandleHealthHit()
        {
            TryEnterHitStun(hitStunDuration);
        }

        /// <summary>
        /// 외부 시스템(CL-014 다운 진입, 보스 패턴 등)이 수동으로 경직을 유발할 때 사용한다.
        /// overrideDuration이 0 이하이면 SerializeField의 hitStunDuration을 사용한다.
        /// </summary>
        public void TriggerHitStun(float overrideDuration = -1f)
        {
            float duration = overrideDuration > 0f ? overrideDuration : hitStunDuration;
            TryEnterHitStun(duration);
        }

        private void TryEnterHitStun(float duration)
        {
            if (!CanEnterHitStun())
            {
                return;
            }

            EnterHitStun(duration);
        }

        private bool CanEnterHitStun()
        {
            if (health == null)
            {
                return false;
            }

            if (health.CurrentHealth <= 0f)
            {
                return false;
            }

            if (health.LastDamage <= 0f)
            {
                return false;
            }

            if (_state != KhiHitStunState.Idle)
            {
                return false;
            }

            if (parryController != null
                && (parryController.IsParryWindowActive || parryController.IsParryRecovering))
            {
                return false;
            }

            return true;
        }

        private void EnterHitStun(float duration)
        {
            CachePermitsIfNeeded();
            ApplyBlockingPermits();

            if (interruptMeleeComboOnHit && meleeCombo != null)
            {
                meleeCombo.AbortCurrentAttack();
            }

            float totalInvulnerability = duration + Mathf.Max(0f, postHitInvulnerability);
            if (totalInvulnerability > 0f)
            {
                health.DamageDisabled();
                StartCoroutine(health.DamageEnabled(totalInvulnerability));
            }

            TrySetAnimatorTrigger();

            _state = KhiHitStunState.HitStun;
            _stateEndTime = Time.time + duration;

            LogTransition($"EnterHitStun duration={duration:F3}");
            HitReceived?.Invoke();
            HitStunStarted?.Invoke(duration);
        }

        private void TransitionToPostHitIFrame()
        {
            RestorePermits();

            LogTransition("HitStun -> PostHitIFrame");
            HitStunEnded?.Invoke();

            if (postHitInvulnerability <= 0f)
            {
                _state = KhiHitStunState.Idle;
                LogTransition("PostHitIFrame skipped (duration 0) -> Idle");
                return;
            }

            _state = KhiHitStunState.PostHitIFrame;
            _stateEndTime = Time.time + postHitInvulnerability;
        }

        private void TransitionToIdle()
        {
            _state = KhiHitStunState.Idle;
            LogTransition("PostHitIFrame -> Idle");
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
            _cachedMovementForbidden = characterMovement != null && characterMovement.MovementForbidden;
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

            if (characterMovement != null)
            {
                characterMovement.MovementForbidden = true;
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

            if (characterMovement != null)
            {
                characterMovement.MovementForbidden = _cachedMovementForbidden;
            }

            _hasCachedPermits = false;
        }

        private void TrySetAnimatorTrigger()
        {
            if (!setAnimatorTrigger || animator == null || string.IsNullOrEmpty(hitAnimatorTriggerName))
            {
                return;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter p = parameters[i];
                if (p.type == AnimatorControllerParameterType.Trigger && p.name == hitAnimatorTriggerName)
                {
                    animator.SetTrigger(hitAnimatorTriggerName);
                    return;
                }
            }
        }

        private void LogTransition(string label)
        {
            if (!logStateTransitions)
            {
                return;
            }

            Debug.Log($"[KhiHitStun] {label} t={Time.time:F3}");
        }
    }
}
