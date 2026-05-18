using System;
using System.Collections;
using LostMemory.Combat;
using LostMemory.Networking.Common;
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

        [Header("Memory Revive")]
        [SerializeField, Min(0f), Tooltip("기억 시스템 ReviveOnce 보상 — Down 진입 후 자동 부활까지의 대기 시간(초). 다운 애니메이션을 잠깐 보여준 뒤 부활 애니메이션으로 자연스럽게 전환.")]
        private float memoryReviveDelay = 1.5f;

        [Header("Solo Behavior")]
        [SerializeField] private KhiDownSoloBehavior soloBehavior = KhiDownSoloBehavior.DownWithDebugRevive;

        [Header("Debug")]
        [SerializeField] private KeyCode debugReviveKey = KeyCode.R;
        [SerializeField] private bool logStateTransitions = false;
        // 주: Defeat 시 Health.Kill()이 GameObject를 비활성화해 이 컴포넌트 Update도 멈춘다.
        // Respawn 디버그 키는 씬 레벨 TestKhiSceneBootstrap이 폴링해 DebugRespawn()을 호출한다.

        [Header("Animation")]
        [SerializeField] private string downAnimatorTriggerName = "Down";
        [SerializeField] private string reviveAnimatorTriggerName = "Revive";
        [SerializeField, Min(0f)] private float defeatObjectDisableDelay = 5f;

        private KhiDownState _state = KhiDownState.Normal;
        private float _downEnterTime;
        private float _nextTickEventTime;

        private bool _hasCachedPermits;
        private bool _cachedHandleWeaponPermitted;
        private bool _cachedDashPermitted;
        private bool _cachedMeleeExternalBlock;
        private bool _cachedParryExternalBlock;
        private bool _cachedMovementForbidden;

        // 기억 시스템 ReviveOnce 보상 — 런마다 1회 자동 부활 가능. RunManager 가 던전 빌드 시 활성화.
        private bool _memoryReviveAvailable;

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

            // Defeated 동안 외부 컴포넌트(TDE Character.UpdateAnimators 등)가 Animator 파라미터를 흔들어
            // Dead state 가 다른 state 로 밀려나는 것을 매 프레임 catch — 진행 중인 transition 도 취소함.
            if (_state == KhiDownState.Defeated && animator != null && animator.isActiveAndEnabled)
            {
                bool nextIsDead = animator.IsInTransition(0)
                    && animator.GetNextAnimatorStateInfo(0).IsName("Dead");
                bool currentIsDead = !animator.IsInTransition(0)
                    && animator.GetCurrentAnimatorStateInfo(0).IsName("Dead");

                if (!nextIsDead && !currentIsDead)
                {
                    animator.Play("Dead", 0, 0f);
                }
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
            if (!HostAuthority.IsNetworkSessionActive)
            {
                return false;
            }

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

            // 기억 시스템 ReviveOnce 보상 — Down 애니메이션을 잠깐 보여준 후 자동 부활.
            // 즉시 ForceRevive 호출 시 Down 트리거 + Revive 트리거가 같은 프레임에 충돌해
            // 애니메이터가 Down 에 멈추는 문제 회피. memoryReviveDelay 만큼 대기 후 부활.
            if (_memoryReviveAvailable)
            {
                _memoryReviveAvailable = false;
                LogTransition($"Memory ReviveOnce 예약 — {memoryReviveDelay:F2}초 후 자동 부활");
                StartCoroutine(MemoryReviveAfterDelayCoroutine());
            }
        }

        private IEnumerator MemoryReviveAfterDelayCoroutine()
        {
            // WaitForSeconds 사용 (Time.timeScale 영향) — 게임 일시정지 시 부활 대기도 함께 일시정지.
            // downDuration 보다 짧아야 타임아웃 패배보다 먼저 발화. (default downDuration=10s, delay=1.5s)
            yield return new WaitForSeconds(memoryReviveDelay);

            // 대기 중 다른 경로로 상태가 바뀌었을 수 있음 (동료 부활 / 타임아웃 패배 등) — 다운 중일 때만 부활.
            if (_state == KhiDownState.Down)
            {
                LogTransition("Memory ReviveOnce 발동 — ForceRevive");
                ForceRevive();
            }
            else
            {
                LogTransition($"Memory ReviveOnce 취소 — state 가 이미 {_state}");
            }
        }

        /// <summary>
        /// 기억 시스템 ReviveOnce 보상 — 런 시작 시 RunManager 가 HasRevive 플래그에 따라 호출.
        /// true 면 다음 다운 시 자동 부활. 1회 소비되면 false 로 리셋.
        /// </summary>
        public void SetMemoryReviveAvailable(bool available)
        {
            _memoryReviveAvailable = available;
            if (logStateTransitions)
                Debug.Log($"[KhiDownController] MemoryReviveAvailable = {available}", this);
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

            // Fallback: Animator 상태머신에 'Revive 트리거 → Down 빠져나가는 transition' 이 누락된 경우
            // 부활 후에도 캐릭터가 Down 애니메이션에 시각적으로 멈춰있는 버그 방지.
            // 옵션 A (Animator 에 transition 추가) 가 작동하면 이 코루틴은 stuck 감지를 못 해 no-op.
            if (animator != null)
            {
                StartCoroutine(EnsureNotStuckInDownAnimationCoroutine());
            }
        }

        private IEnumerator EnsureNotStuckInDownAnimationCoroutine()
        {
            // transition 평가 시간 확보 후 fallback 발동.
            yield return new WaitForSeconds(0.3f);

            if (animator == null) yield break;
            // 부활 후 다시 다운되었거나 패배했으면 fallback 미실행.
            if (_state != KhiDownState.Normal) yield break;

            // 무조건 강제 리셋 — Animator transition 누락 여부와 state 이름 차이에 영향받지 않음.
            // 옵션 A (Animator 에 Revive transition 추가) 가 있으면 이미 정상 state 이고,
            // 여기서 Rebind 해도 default state(보통 Idle) 로 자연스럽게 복귀 — 결과 동일하여 안전.
            Debug.Log("[KhiDownController] Memory revive 후 Animator 강제 리셋 (transition 누락 fallback).", this);

            // 1) Down 트리거 잔재 제거 (Animator 가 Down 진입 트리거를 다시 평가하지 않도록).
            animator.ResetTrigger(downAnimatorTriggerName);

            // 2) Hero_Animator 의 Idle/Walking Bool 분기 패턴 강제 설정.
            //    (parameter 가 없으면 SetBool 호출은 무해히 무시됨)
            if (HasAnimatorParameter("Idle"))      animator.SetBool("Idle", true);
            if (HasAnimatorParameter("Walking"))   animator.SetBool("Walking", false);

            // 3) Animator state 머신을 default state(보통 Idle)로 강제 복귀.
            //    parameter 값도 default 로 리셋되지만 이동 입력이 매 프레임 다시 set 하므로 영향 없음.
            animator.Rebind();
            animator.Update(0f);
        }

        private bool HasAnimatorParameter(string name)
        {
            if (animator == null) return false;
            var parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == name) return true;
            }
            return false;
        }

        public void ForceDefeat()
        {
            if (_state == KhiDownState.Defeated)
            {
                return;
            }

            ExecuteDefeat(true);
        }

        private void EnterDefeatedByTimeout()
        {
            LogTransition("Down timer expired -> Defeated");
            DefeatedByTimeout?.Invoke();
            ExecuteDefeat(true);
        }

        private void EnterDefeatedSolo()
        {
            LogTransition("Solo lethal -> Defeated");
            DefeatedSolo?.Invoke();
            ExecuteDefeat(false);
        }

        private void ExecuteDefeat(bool killHealthImmediately)
        {
            _state = KhiDownState.Defeated;

            parryController?.ForceIdle();
            hitStun?.ForceExit();

            CachePermitsIfNeeded();
            ApplyBlockingPermits();
            meleeCombo?.AbortCurrentAttack();

            // 솔로 즉사 경로(EnterDown 우회)에서도 Dead 애니메이션이 재생되도록 Down 트리거 발동.
            TrySetAnimatorTrigger(downAnimatorTriggerName);

            if (health != null)
            {
                health.Invulnerable = false;
                PrepareHealthForVisibleDefeat();

                if (health.CurrentHealth > 0f)
                {
                    health.CurrentHealth = 0f;
                }

                if (killHealthImmediately)
                {
                    health.Kill();
                }
            }

            // health.Kill() 내부에서 Character.Reset() 이 Animator 파라미터를 초기화하면서
            // Dead state 가 다른 state(Front_Idle 등)로 밀려나는 케이스가 관찰됨.
            // Animator.Play 로 Dead state 를 강제 고정 — outgoing transition 이 없으므로 그대로 유지됨.
            if (killHealthImmediately)
            {
                ForcePlayDeadState();
            }
        }

        private void PrepareHealthForVisibleDefeat()
        {
            if (health == null)
            {
                return;
            }

            health.DisableModelOnDeath = false;
            if (defeatObjectDisableDelay > 0f)
            {
                health.DelayBeforeDestruction = Mathf.Max(
                    health.DelayBeforeDestruction,
                    defeatObjectDisableDelay);
            }
        }

        private void ForcePlayDeadState()
        {
            if (animator == null || !animator.isActiveAndEnabled)
            {
                return;
            }
            animator.Play("Dead", 0, 0f);
            animator.Update(0f);
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
            _cachedParryExternalBlock = parryController != null && parryController.ExternalBlock;
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

            if (parryController != null)
            {
                parryController.ExternalBlock = true;
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

            if (parryController != null)
            {
                parryController.ExternalBlock = _cachedParryExternalBlock;
            }

            if (characterMovement != null)
            {
                characterMovement.MovementForbidden = _cachedMovementForbidden;
            }

            _hasCachedPermits = false;
        }

        private void TrySetAnimatorTrigger(string triggerName)
        {
            if (animator == null || string.IsNullOrEmpty(triggerName))
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

    public static class KhiPlayerActionGate
    {
        public static bool IsBlocked(KhiDownController downController)
        {
            return downController != null && (downController.IsDown || downController.IsDefeated);
        }

        public static bool IsBlocked(Character character)
        {
            return character != null
                && TryResolveDownController(character, out KhiDownController downController)
                && IsBlocked(downController);
        }

        public static bool IsBlocked(Component source)
        {
            return source != null
                && TryResolveDownController(source, out KhiDownController downController)
                && IsBlocked(downController);
        }

        public static bool TryResolveDownController(Component source, out KhiDownController downController)
        {
            downController = null;
            if (source == null)
            {
                return false;
            }

            downController = source.GetComponent<KhiDownController>();
            if (downController != null)
            {
                return true;
            }

            downController = source.GetComponentInParent<KhiDownController>();
            if (downController != null)
            {
                return true;
            }

            downController = source.GetComponentInChildren<KhiDownController>();
            return downController != null;
        }
    }
}
