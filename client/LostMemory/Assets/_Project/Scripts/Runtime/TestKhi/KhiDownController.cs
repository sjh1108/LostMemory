using System;
using System.Collections;
using LostMemory.Combat;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.TestKhi
{
    public enum KhiDownState
    {
        Normal,
        Down,
        Defeated
    }

    public enum KhiDownSoloBehavior
    {
        ImmediateDefeat,
        DownWithDebugRevive,
        DownNoRevive
    }

    /// <summary>
    /// CL-014 플레이어 다운·부활 기본 흐름 컨트롤러.
    /// Health.OnHit에서 치명타를 가로채 Kill() 대신 Down 상태로 전이하고,
    /// 타이머 만료 또는 수동 ForceDefeat로 Defeat(명시적 Kill) 처리한다.
    /// Revive 성공 시 지정된 HP로 복귀 + 짧은 post-revive i-frame.
    /// TDE Health 원본은 수정하지 않는다.
    /// </summary>
    [AddComponentMenu("Lost Memory/Test Khi/Khi Down Controller")]
    [DefaultExecutionOrder(70)]
    public class KhiDownController : MonoBehaviour
    {
        [Header("Refs (optional, auto-resolved in Awake)")]
        [SerializeField] private Health health;
        [SerializeField] private Character character;
        [SerializeField] private CharacterHandleWeapon handleWeapon;
        [SerializeField] private KhiDashController dashController;
        [SerializeField] private KhiMeleeComboController meleeCombo;
        [SerializeField] private CharacterMovement characterMovement;
        [SerializeField] private KhiHitStunController hitStun;
        [SerializeField] private KhiParryController parryController;
        [SerializeField] private Animator animator;
        [Tooltip("CL-108: 부활 회복을 PlayerHealing 경로로 위임 (HealReceivedPercent 적용 가능). null 이면 SetHealth fallback.")]
        [SerializeField] private PlayerHealing playerHealing;

        [Header("Down")]
        [SerializeField, Min(0f)] private float downDuration = 10f;
        [SerializeField, Min(0f)] private float downHealthFloor = 1f;

        [Header("Revive")]
        [SerializeField, Range(0f, 1f)] private float reviveHealthFraction = 0.2f;
        [SerializeField, Min(1f)] private float reviveHealthAbsoluteMin = 1f;
        [SerializeField, Min(0f)] private float reviveInteractionHoldDuration = 0f;
        [SerializeField, Min(0f)] private float reviveInvulnerabilityAfter = 1.5f;

        [Header("Solo Behavior")]
        [SerializeField] private KhiDownSoloBehavior soloBehavior = KhiDownSoloBehavior.DownWithDebugRevive;

        [Header("Debug")]
        [SerializeField] private KeyCode debugReviveKey = KeyCode.R;
        [SerializeField] private bool logStateTransitions = false;
        // 주: Defeat 시 Health.Kill()이 GameObject를 비활성화해 이 컴포넌트 Update도 멈춘다.
        // Respawn 디버그 키는 씬 레벨 TestKhiSceneBootstrap이 폴링해 DebugRespawn()을 호출한다.

        [Header("Animation")]
        [SerializeField] private bool setAnimatorTriggers = true;
        [SerializeField] private string downAnimatorTriggerName = "Down";
        [SerializeField] private string reviveAnimatorTriggerName = "Revive";

        private KhiDownState _state = KhiDownState.Normal;
        private float _downEnterTime;
        private float _nextTickEventTime;

        private bool _hasCachedPermits;
        private bool _cachedHandleWeaponPermitted;
        private bool _cachedDashPermitted;
        private bool _cachedMeleeExternalBlock;
        private bool _cachedMovementForbidden;

        public event Action<float> DownEntered;
        public event Action<float> DownTimerTicked;
        public event Action<GameObject> ReviveStarted;
        public event Action<float> ReviveProgressChanged;
        public event Action<GameObject> ReviveCompleted;
        public event Action DefeatedByTimeout;
        public event Action DefeatedSolo;
        public event Action DebugRespawned;

        public KhiDownState CurrentState => _state;
        public bool IsDown => _state == KhiDownState.Down;
        public bool IsDefeated => _state == KhiDownState.Defeated;
        public float DownDuration => downDuration;
        public float ReviveInteractionHoldDuration => reviveInteractionHoldDuration;
        public float DownTimeElapsed => _state == KhiDownState.Down ? Mathf.Max(0f, Time.time - _downEnterTime) : 0f;
        public float DownTimeRemaining => _state == KhiDownState.Down ? Mathf.Max(0f, downDuration - (Time.time - _downEnterTime)) : 0f;

        private void Awake()
        {
            health ??= GetComponent<Health>() ?? GetComponentInChildren<Health>();
            character ??= GetComponent<Character>() ?? GetComponentInChildren<Character>();
            handleWeapon ??= GetComponent<CharacterHandleWeapon>();
            dashController ??= GetComponent<KhiDashController>();
            meleeCombo ??= GetComponent<KhiMeleeComboController>();
            characterMovement ??= GetComponent<CharacterMovement>();
            hitStun ??= GetComponent<KhiHitStunController>();
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
            if (_state == KhiDownState.Down)
            {
                TickDown();
            }
        }

        private void LateUpdate()
        {
            if (_state == KhiDownState.Normal && _hasCachedPermits)
            {
                RestorePermits();
            }
        }

        private void TickDown()
        {
            float remaining = DownTimeRemaining;

            if (Time.time >= _nextTickEventTime)
            {
                _nextTickEventTime = Time.time + 0.1f;
                DownTimerTicked?.Invoke(remaining);
            }

            if (soloBehavior == KhiDownSoloBehavior.DownWithDebugRevive
                && Input.GetKeyDown(debugReviveKey))
            {
                ForceRevive();
                return;
            }

            if (remaining <= 0f)
            {
                EnterDefeatedByTimeout();
            }
        }

        private void HandleHealthHit()
        {
            if (health == null)
            {
                return;
            }

            // Defeated: 이미 죽음 처리 완료, intercept 안 함.
            if (_state == KhiDownState.Defeated)
            {
                return;
            }

            // Down 중 후속 hit (적 OnTriggerStay2D 등) 으로 HP 가 0 이하 떨어지면
            // Health.Damage 의 `if (CurrentHealth <= 0) Kill()` 가 발화해 GameObject 가 비활성화되고
            // Down 타이머가 멈춘다. floor 로 즉시 복원해 Kill 트리거를 막는다.
            if (_state == KhiDownState.Down)
            {
                if (health.CurrentHealth <= 0f)
                {
                    health.SetHealth(Mathf.Max(0.0001f, downHealthFloor));
                }
                return;
            }

            // _state == Normal: 첫 치명타 intercept.
            if (health.CurrentHealth > 0f)
            {
                return;
            }

            if (!ResolveAllyContext())
            {
                EnterDefeatedSolo();
                return;
            }

            // Kill 차단 핵심: OnHit 직후 Kill 체크 전에 HP를 downHealthFloor로 되돌린다.
            health.SetHealth(Mathf.Max(0.0001f, downHealthFloor));
            EnterDown();
        }

        private bool ResolveAllyContext()
        {
            switch (soloBehavior)
            {
                case KhiDownSoloBehavior.ImmediateDefeat:
                    return false;
                case KhiDownSoloBehavior.DownWithDebugRevive:
                case KhiDownSoloBehavior.DownNoRevive:
                    return true;
                default:
                    return false;
            }

            // 프로덕션 경로 TODO (CL-020대):
            // Physics2D.OverlapCircleNonAlloc으로 Player 레이어 주변 탐지,
            // self 제외 + KhiDownController.IsDefeated=false 인 플레이어 1명 이상 존재 시 true.
        }

        private void EnterDown()
        {
            parryController?.ForceIdle();
            hitStun?.ForceExit();

            CachePermitsIfNeeded();
            ApplyBlockingPermits();

            meleeCombo?.AbortCurrentAttack();

            if (health != null)
            {
                health.DamageDisabled();
            }

            TrySetAnimatorTrigger(downAnimatorTriggerName);

            _state = KhiDownState.Down;
            _downEnterTime = Time.time;
            _nextTickEventTime = Time.time;

            LogTransition($"EnterDown duration={downDuration:F2}");
            DownEntered?.Invoke(downDuration);
            DownTimerTicked?.Invoke(downDuration);
        }

        public bool TryBeginRevive(GameObject reviver)
        {
            if (_state != KhiDownState.Down)
            {
                return false;
            }

            ReviveStarted?.Invoke(reviver);

            if (reviveInteractionHoldDuration <= 0f)
            {
                ReviveProgressChanged?.Invoke(1f);
                CompleteRevive(reviver);
            }

            return true;
        }

        public void CancelRevive(GameObject reviver)
        {
            ReviveProgressChanged?.Invoke(0f);
        }

        public void ForceRevive(float healthOverride = -1f)
        {
            if (_state != KhiDownState.Down)
            {
                return;
            }

            CompleteRevive(null, healthOverride);
        }

        private void CompleteRevive(GameObject reviver, float healthOverride = -1f)
        {
            if (_state != KhiDownState.Down)
            {
                return;
            }

            float reviveHp = healthOverride > 0f
                ? healthOverride
                : Mathf.Max(reviveHealthAbsoluteMin, (health != null ? health.MaximumHealth : 1f) * reviveHealthFraction);

            if (health != null)
            {
                health.Invulnerable = false;
            }

            RestorePermits();

            if (health != null)
            {
                // CL-108: 부활 회복을 PlayerHealing 경로로 위임 (철의 깃 등 HealReceivedPercent 적용).
                // playerHealing 미부착 시 SetHealth fallback. 다운 시점 currentHp ≈ downHealthFloor 라
                // Heal(reviveHp) 의 final hp 가 (currentHp + reviveHp×mul) ≈ reviveHp×mul 로 의도와 일치.
                if (playerHealing != null)
                {
                    playerHealing.Heal(reviveHp, this);
                }
                else
                {
                    health.SetHealth(reviveHp);
                }
            }

            if (health != null && reviveInvulnerabilityAfter > 0f)
            {
                health.DamageDisabled();
                StartCoroutine(health.DamageEnabled(reviveInvulnerabilityAfter));
            }

            TrySetAnimatorTrigger(reviveAnimatorTriggerName);

            _state = KhiDownState.Normal;

            LogTransition($"CompleteRevive hp={reviveHp:F1}");
            ReviveCompleted?.Invoke(reviver);
        }

        public void ForceDefeat()
        {
            if (_state == KhiDownState.Defeated)
            {
                return;
            }

            ExecuteDefeat(null);
        }

        private void EnterDefeatedByTimeout()
        {
            LogTransition("Down timer expired -> Defeated");
            DefeatedByTimeout?.Invoke();
            ExecuteDefeat(DefeatedByTimeout);
        }

        private void EnterDefeatedSolo()
        {
            LogTransition("Solo lethal -> Defeated");
            DefeatedSolo?.Invoke();
            ExecuteDefeat(DefeatedSolo);
        }

        private void ExecuteDefeat(Action fallbackEvent)
        {
            _state = KhiDownState.Defeated;

            RestorePermits();

            if (health != null)
            {
                health.Invulnerable = false;
                if (health.CurrentHealth > 0f)
                {
                    health.CurrentHealth = 0f;
                }
                health.Kill();
            }
        }

        public void DebugRespawn()
        {
            if (_state != KhiDownState.Defeated)
            {
                return;
            }

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (health != null)
            {
                health.Revive();
                health.SetHealth(health.MaximumHealth);
            }

            _state = KhiDownState.Normal;

            LogTransition("DebugRespawn");
            DebugRespawned?.Invoke();
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

        private void TrySetAnimatorTrigger(string triggerName)
        {
            if (!setAnimatorTriggers || animator == null || string.IsNullOrEmpty(triggerName))
            {
                return;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter p = parameters[i];
                if (p.type == AnimatorControllerParameterType.Trigger && p.name == triggerName)
                {
                    animator.SetTrigger(triggerName);
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

            Debug.Log($"[KhiDown] {label} t={Time.time:F3}");
        }
    }
}
