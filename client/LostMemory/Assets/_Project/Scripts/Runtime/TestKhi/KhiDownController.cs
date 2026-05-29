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
        [SerializeField, Min(0f)] private float downDuration = 30f;
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
        [Tooltip("시연/테스트용 강제 Down 진입 키. owner 자신에게만 적용. Normal 상태에서만 작동. " +
            "협력 부활 (R hold) 검증에 필요한 Down state 를 즉시 만든다. " +
            "EDITOR/DEVELOPMENT_BUILD 빌드에서만 활성. Release 빌드 자동 제외.")]
        [SerializeField] private KeyCode debugForceDownKey = KeyCode.F5;
        [SerializeField] private bool logStateTransitions = false;
        // 주: Defeat 시 Health.Kill()이 GameObject를 비활성화해 이 컴포넌트 Update도 멈춘다.
        // Respawn 디버그 키는 씬 레벨 TestKhiSceneBootstrap이 폴링해 DebugRespawn()을 호출한다.

        [Header("Animation")]
        [SerializeField] private string downAnimatorTriggerName = "Down";
        [SerializeField] private string reviveAnimatorTriggerName = "Revive";
        [SerializeField, Min(0f)] private float defeatObjectDisableDelay = 5f;

        [Header("Cooperative Revive")]
        [Tooltip("살아있는 player 가 Down player 를 부활시키기 위해 접근해야 하는 최대 거리(m).")]
        [SerializeField, Min(0.1f)] private float cooperativeReviveDistance = 1.5f;
        [Tooltip("R 키를 누르고 있어야 하는 시간(초). 도달하면 ForceRevive 발동.")]
        [SerializeField, Min(0.1f)] private float cooperativeReviveChargeSeconds = 2.0f;
        [Tooltip("근처 Down player 검색 시 사용할 LayerMask. 비어있으면 모든 KhiDownController fallback 검색.")]
        [SerializeField] private LayerMask cooperativeReviveLayerMask;

        [Header("Cooperative Revive Bar (auto-spawn)")]
        [Tooltip("Down 캐릭터 머리 위에 차징바를 자동으로 spawn 한다. prefab 수정 0.")]
        [SerializeField] private bool autoSpawnCoopReviveBar = true;
        [Tooltip("차징바 머리 위 offset (worldspace, parent local). 캐릭터 sprite 크기에 따라 0.5~2.0 사이 튜닝.")]
        [SerializeField] private Vector3 coopReviveBarLocalOffset = new Vector3(0f, 1.1f, 0f);
        [Tooltip("차징바 width (worldspace m). 캐릭터 폭 정도가 자연스러움.")]
        [SerializeField, Min(0.1f)] private float coopReviveBarWidth = 0.9f;
        [Tooltip("차징바 height (worldspace m). 0.08~0.20 권장.")]
        [SerializeField, Min(0.01f)] private float coopReviveBarHeight = 0.12f;
        [Tooltip("차징바 background 색 (어두운 반투명 권장).")]
        [SerializeField] private Color coopReviveBarBackgroundColor = new Color(0.05f, 0.05f, 0.05f, 0.75f);
        [Tooltip("차징 중 fill 색 (시안/노랑 등 진행 의미).")]
        [SerializeField] private Color coopReviveBarFillColor = new Color(0.25f, 0.85f, 1f, 0.95f);
        [Tooltip("완료 직전 ready 색 (밝게 빛나는 펄스).")]
        [SerializeField] private Color coopReviveBarReadyColor = new Color(0.6f, 1f, 0.85f, 1f);
        [Tooltip("차징바 sorting layer 이름. 빈 문자열이면 캐릭터의 메인 SpriteRenderer 와 동일 layer 자동 적용. " +
            "정확히 컨트롤하려면 'UI' 또는 'Player' 등 사용. " +
            "주의: 여기 입력한 layer 이름이 프로젝트 Sorting Layers 에 등록되어 있어야 함.")]
        [SerializeField] private string coopReviveBarSortingLayer = "";
        [Tooltip("차징바 background sortingOrder. 캐릭터 메인 SR order 보다 충분히 커야 위에 보임. " +
            "+10 정도면 캐릭터 무기/이펙트 위로. fill 은 자동으로 +1.")]
        [SerializeField] private int coopReviveBarSortingOrder = 100;
        [Tooltip("true 면 캐릭터 메인 SpriteRenderer 의 sortingLayer 를 자동 따라가고, sortingOrder 는 위 값을 추가 offset 으로 사용. " +
            "false 면 위에서 지정한 sortingLayer/order 절대값 사용.")]
        [SerializeField] private bool coopReviveBarAutoFollowCharacterSortingLayer = true;

        private float _coopReviveProgress;
        private KhiDownController _coopReviveTarget;
        private static readonly Collider2D[] _coopReviveBuffer = new Collider2D[8];

        // 자기 머리 위에 자동 spawn 된 차징바 (자기가 부활 대상일 때 fill 됨).
        private KhiCoopReviveBarView _coopReviveBar;
        // 살리는 사람 측 (자기 Normal 상태) — target 에게 progress sync 보낼 throttle 타이머.
        private float _nextCoopProgressBroadcastTime;
        private float _lastBroadcastProgress = -1f;

        private KhiDownState _state = KhiDownState.Normal;
        private float _downEnterTime;
        private float _nextTickEventTime;
        private float _nextCoopDiagTime;

        private bool _hasCachedPermits;
        private bool _cachedHandleWeaponPermitted;
        private bool _cachedDashPermitted;
        private bool _cachedMeleeExternalBlock;
        private bool _cachedParryExternalBlock;
        private bool _cachedMovementForbidden;

        // 사망 (Down/Defeated) 시 외부 충돌 force 차단용 Rigidbody2D 캐시.
        // CharacterMovement.MovementForbidden 만으론 외부 knockback / push 가 그대로 적용됨 →
        // Rigidbody2D 를 Kinematic 으로 잠시 전환해 force 무시. 부활 시 원래 bodyType 으로 복구.
        private Rigidbody2D _cachedRigidbody2D;
        private RigidbodyType2D _cachedRigidbody2DBodyType;
        private bool _hasCachedRigidbody2DState;

        // Down/Defeated 동안 EnemyContactDamage 등의 외부 넉백을 Health.CanGetKnockback 게이트로 차단.
        private bool _cachedImmuneToKnockback;
        private bool _hasCachedImmuneToKnockback;

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

        /// <summary>
        /// 임의의 KhiDownController 가 Defeated 로 전이될 때 발화. instance event 와 별도로 등록.
        /// StageRouteManager 가 서버 측에서 구독해 ready 게이트 재평가에 사용.
        /// owner-side ExecuteDefeat + non-owner mirror 의 ApplyDownStateFromNetwork 양쪽에서 발화 —
        /// 각 경로 진입 전 `_state == newState` 가드가 있어 중복 발화 없음.
        /// </summary>
        public static event Action<KhiDownController> AnyPlayerDefeated;

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

            // 협력 부활 차징바 자동 spawn — 자기 머리 위.
            // ReviveProgressChanged event 구독 → 자기가 살아나는 사람(target) 일 때 살리는 사람 측에서
            // _coopReviveTarget.ReviveProgressChanged.Invoke(ratio) 호출 → 본 핸들러 호출 → bar fill.
            if (autoSpawnCoopReviveBar && _coopReviveBar == null)
            {
                _coopReviveBar = KhiCoopReviveBarView.CreateOnTransform(
                    transform, coopReviveBarLocalOffset, coopReviveBarWidth, coopReviveBarHeight);
                _coopReviveBar.SetColors(coopReviveBarBackgroundColor, coopReviveBarFillColor, coopReviveBarReadyColor);

                // sorting layer/order — auto follow 면 캐릭터 메인 SR 의 layer + offset, 아니면 SerializeField 절대값.
                string finalLayer = coopReviveBarSortingLayer;
                int finalOrder = coopReviveBarSortingOrder;
                if (coopReviveBarAutoFollowCharacterSortingLayer)
                {
                    var mainSR = FindMainCharacterSpriteRenderer();
                    if (mainSR != null)
                    {
                        finalLayer = mainSR.sortingLayerName;
                        finalOrder = mainSR.sortingOrder + coopReviveBarSortingOrder; // SerializeField 값을 offset 으로
                    }
                }
                _coopReviveBar.SetSorting(finalLayer, finalOrder);

                _coopReviveBar.SetProgress(0f);
                // 자기 측 event — 살리는 사람의 KhiDownController.TickCooperativeReviveSearch 가
                // _coopReviveTarget.ReviveProgressChanged?.Invoke(ratio) 발사 → target 의 본 KhiDownController
                // 가 receiver. 살리는 사람의 자기 화면 mirror 에서도 정상 (event 가 같은 process 내 호출).
                ReviveProgressChanged += HandleReviveProgressForSelfBar;
            }
        }

        private void HandleReviveProgressForSelfBar(float ratio)
        {
            if (_coopReviveBar != null) _coopReviveBar.SetProgress(ratio);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspector 에서 SerializeField 변경 시 자동 호출 (Editor only).
        /// Play Mode 중에도 호출되므로 색상/sorting 라이브 튜닝 가능.
        /// width/height/offset 은 transform 의 baseline 까지 다시 잡아야 해서 별도 처리.
        /// </summary>
        private void OnValidate()
        {
            if (!Application.isPlaying) return;
            if (_coopReviveBar == null) return;

            _coopReviveBar.SetColors(coopReviveBarBackgroundColor, coopReviveBarFillColor, coopReviveBarReadyColor);

            // sorting 도 라이브 반영.
            string finalLayer = coopReviveBarSortingLayer;
            int finalOrder = coopReviveBarSortingOrder;
            if (coopReviveBarAutoFollowCharacterSortingLayer)
            {
                var mainSR = FindMainCharacterSpriteRenderer();
                if (mainSR != null)
                {
                    finalLayer = mainSR.sortingLayerName;
                    finalOrder = mainSR.sortingOrder + coopReviveBarSortingOrder;
                }
            }
            _coopReviveBar.SetSorting(finalLayer, finalOrder);
        }
#endif

        /// <summary>
        /// 자식 SpriteRenderer 중 "main" 추정 — 가장 큰 bounds 또는 _CoopReviveBar 자기 자신 제외 중 첫째.
        /// auto follow sorting 용. 정확도 떨어져도 시연용 fallback 으로 OK.
        /// </summary>
        private SpriteRenderer FindMainCharacterSpriteRenderer()
        {
            SpriteRenderer[] all = GetComponentsInChildren<SpriteRenderer>(true);
            SpriteRenderer best = null;
            float bestArea = 0f;
            for (int i = 0; i < all.Length; i++)
            {
                SpriteRenderer sr = all[i];
                if (sr == null) continue;
                // 차징바 자기 자신 제외.
                if (sr.transform.IsChildOf(_coopReviveBar != null ? _coopReviveBar.transform : null)) continue;
                if (sr.sprite == null) continue;
                Vector2 size = sr.sprite.bounds.size;
                float area = size.x * size.y;
                if (area > bestArea)
                {
                    bestArea = area;
                    best = sr;
                }
            }
            return best;
        }

        /// <summary>
        /// 외부 (PlayerHealthSync.ClientRpc) 호출 — 모든 client 에서 자기 머리 위 bar fill 갱신.
        /// 자기 측 ReviveProgressChanged 와 idempotent — 같은 값 들어와도 SetProgress 만 호출.
        /// </summary>
        public void ApplyCoopReviveProgressFromNetwork(float ratio)
        {
            if (_coopReviveBar != null) _coopReviveBar.SetProgress(ratio);
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

        // F5 양보 — CursorToggleDebug 가 F5 로 마우스 visible 토글에 사용.
        // 재진단 필요하면 true 로 바꾸고 debugForceDownKey 를 다른 키로 변경.
        private const bool _forceDownDebugEnabled = false;

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 시연/테스트 debug — F5 (configurable) 로 자기 자신 즉시 Down 진입.
            // 협력 부활 검증 시 Talent MaxHealth 부풀려진 환경에서도 빠르게 Down state 생성.
            // owner 만 (자기 자신만 죽임), Normal 상태에서만, IsSpawned 가드.
            if (_forceDownDebugEnabled && IsForceDownPressed())
            {
                var noDbg = GetComponentInParent<Unity.Netcode.NetworkObject>();
                bool isOwnerActor = noDbg == null || !noDbg.IsSpawned || noDbg.IsOwner;
                if (isOwnerActor && _state == KhiDownState.Normal && health != null)
                {
                    // CRITICAL: 직접 EnterDown() 호출하면 _state 가 NetworkVariable 이 아니라서
                    // 게스트 측 mirror 까지 sync 안 됨 → 동료가 R hold 해도 FindNearestDownAlly 가
                    // mirror.IsDown=false 라서 target null → 부활 차징 0.
                    // Health.Damage 로 production 흐름 (Health.OnHit → HandleHealthHit → EnterDown)
                    // 거치면 PlayerHealthSync NetworkVariable sync → 게스트 측 mirror 도 EnterDown 호출 → sync 달성.
                    // F6 God Mode 가 ON 이면 Health.Invulnerable=true 라 Damage 무시됨 → 임시 해제 후 호출.
                    bool wasInvulnerable = health.Invulnerable;
                    if (wasInvulnerable) health.Invulnerable = false;
                    Debug.Log($"[KhiDownController] DEBUG ForceDown — F5 key (via Health.Damage, wasInvulnerable={wasInvulnerable}). go={gameObject.name}");
                    health.Damage(99999f, gameObject, 0f, 0f, Vector3.zero);
                    if (wasInvulnerable) health.Invulnerable = true;
                }
            }
#endif

            // R 키 입력 — New Input System (Keyboard.current) 우선, legacy Input fallback.
            bool rPressed = IsRevivePressed();
            bool rHeld = IsReviveHeld();

            if (rPressed)
            {
                var no = GetComponentInParent<Unity.Netcode.NetworkObject>();
                Debug.Log($"[KhiDownController] R 키 감지 — state={_state} soloBehavior={soloBehavior} IsOwner={(no != null ? no.IsOwner.ToString() : "noNetObj")} IsSpawned={(no != null ? no.IsSpawned.ToString() : "noNetObj")} go={gameObject.name}");
            }

            if (_state == KhiDownState.Normal)
            {
                // 협력 부활 — 살아있는 player 가 자기 player 만 R hold 로 근처 Down 동료 부활.
                // 자기 자신은 부활 대상에서 제외 (this 비교).
                var no = GetComponentInParent<Unity.Netcode.NetworkObject>();
                bool isLocalActor = no == null || !no.IsSpawned || no.IsOwner;
                if (isLocalActor)
                {
                    TickCooperativeReviveSearch(rHeld);
                }
            }
            else if (_state == KhiDownState.Down)
            {
                // Down 상태에서는 R 키 자기 부활 차단 — 동료가 근처에서 R hold 해야 함.
                TickDown();
            }
        }

        /// <summary>
        /// New Input System (Keyboard.current.rKey.wasPressedThisFrame) 우선.
        /// legacy Input.GetKeyDown(debugReviveKey) fallback — 둘 다 활성화된 환경.
        /// </summary>
        private bool IsRevivePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame)
            {
                return true;
            }
#endif
            // Legacy fallback — Old Input Manager 환경.
            try
            {
                if (Input.GetKeyDown(debugReviveKey)) return true;
            }
            catch (System.InvalidOperationException)
            {
                // New Input System 단독 모드에선 legacy Input 호출 시 예외. 무시.
            }
            return false;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// 시연/테스트용 강제 Down 키 (default F5) 감지. New Input System + legacy fallback.
        /// IsRevivePressed 와 동일 패턴.
        /// </summary>
        private bool IsForceDownPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                // debugForceDownKey (KeyCode) → InputSystem Key 변환 — F5 / F6 만 지원.
                switch (debugForceDownKey)
                {
                    case KeyCode.F5:
                        if (kb.f5Key.wasPressedThisFrame) return true;
                        break;
                    case KeyCode.F6:
                        if (kb.f6Key.wasPressedThisFrame) return true;
                        break;
                    case KeyCode.F7:
                        if (kb.f7Key.wasPressedThisFrame) return true;
                        break;
                    case KeyCode.F8:
                        if (kb.f8Key.wasPressedThisFrame) return true;
                        break;
                    // 다른 키는 legacy fallback 만 의존.
                }
            }
#endif
            try
            {
                if (Input.GetKeyDown(debugForceDownKey)) return true;
            }
            catch (System.InvalidOperationException)
            {
                // New Input System 단독 모드 — 무시.
            }
            return false;
        }
#endif

        private void LateUpdate()
        {
            // 라이브 튜닝 — Inspector 에서 색/sorting 바꾸면 즉시 반영.
            // OnValidate 가 동적 생성 인스턴스에선 신뢰성 떨어져서 매 프레임 push 가 가장 안전.
            // sprite.color set 은 가볍고, dirty 시에만 GPU sync 라 성능 영향 미미.
            if (_coopReviveBar != null)
            {
                _coopReviveBar.SetColors(coopReviveBarBackgroundColor, coopReviveBarFillColor, coopReviveBarReadyColor);
            }

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

            // Down 상태에서 자기 부활 차단 — 동료가 근처에서 R hold 해야 함 (cooperative revive).
            // 타임아웃 시 Defeated.
            if (remaining <= 0f)
            {
                EnterDefeatedByTimeout();
            }
        }

        /// <summary>
        /// 협력 부활 — 살아있는(Normal) player 가 매 frame 근처 Down 동료 검색.
        /// R hold 중이면서 거리 1.5m 내면 progress 누적. 2초 도달 시 ForceRevive 발동.
        /// R release / 거리 이탈 / target Defeated 시 progress reset.
        /// 자기 자신은 검색 대상 제외 (this 비교).
        /// </summary>
        private void TickCooperativeReviveSearch(bool rHeld)
        {
            // 1. 현재 target 검증 — 사라졌거나 Down 이 아니면 reset.
            if (_coopReviveTarget != null)
            {
                if (_coopReviveTarget == null || _coopReviveTarget.gameObject == null
                    || !_coopReviveTarget.IsDown)
                {
                    ResetCoopReviveProgress();
                }
                else
                {
                    float distSqr = (_coopReviveTarget.transform.position - transform.position).sqrMagnitude;
                    if (distSqr > cooperativeReviveDistance * cooperativeReviveDistance)
                    {
                        ResetCoopReviveProgress();
                    }
                }
            }

            // 2. target 이 없으면 새로 검색.
            if (_coopReviveTarget == null)
            {
                _coopReviveTarget = FindNearestDownAlly();
                if (_coopReviveTarget != null)
                {
                    _coopReviveProgress = 0f;
                }
            }

            // DIAG — rHeld 일 때 0.5초마다 상태 dump (target/dist/progress). 시연 후 제거 또는 verboseLog 조건으로 변경.
            if (rHeld && Time.time >= _nextCoopDiagTime)
            {
                _nextCoopDiagTime = Time.time + 0.5f;
                string targetInfo;
                if (_coopReviveTarget != null && _coopReviveTarget.gameObject != null)
                {
                    float dist = Vector2.Distance(_coopReviveTarget.transform.position, transform.position);
                    targetInfo = $"target={_coopReviveTarget.gameObject.name} isDown={_coopReviveTarget.IsDown} dist={dist:F2}m (limit={cooperativeReviveDistance:F2})";
                }
                else
                {
                    // target null — 검색 결과 0개. 가능한 사유 진단:
                    // - layer mask 비어있으면 모든 KhiDownController 검색
                    // - mirror collider 가 layer 안 맞음
                    // - 거리 초과
                    int totalInScene = UnityEngine.Object.FindObjectsByType<KhiDownController>(FindObjectsSortMode.None).Length;
                    targetInfo = $"target=NULL (scene KhiDownController count={totalInScene}, layerMask={cooperativeReviveLayerMask.value})";
                }
                Debug.Log($"[CoopRevive] rHeld=true myPos={transform.position} {targetInfo} progress={_coopReviveProgress:F2}/{cooperativeReviveChargeSeconds:F2}");
            }

            // 3. R hold 미입력이면 진행 reset (target 은 유지).
            if (!rHeld)
            {
                if (_coopReviveProgress > 0f)
                {
                    _coopReviveProgress = 0f;
                    if (_coopReviveTarget != null)
                    {
                        _coopReviveTarget.ReviveProgressChanged?.Invoke(0f);
                    }
                }
                return;
            }

            // 4. target 이 없으면 진행 불가.
            if (_coopReviveTarget == null) return;

            // 5. progress 누적 + target 의 진행률 이벤트 발사 (UI hookup 호환).
            _coopReviveProgress += Time.deltaTime;
            float ratio = Mathf.Clamp01(_coopReviveProgress / Mathf.Max(0.01f, cooperativeReviveChargeSeconds));
            _coopReviveTarget.ReviveProgressChanged?.Invoke(ratio);

            // 모든 client sync — throttle 0.1s 또는 ratio 변화량 ≥ 0.05 시 broadcast.
            // 호스트면 직접 ClientRpc, 게스트면 ServerRpc 거쳐 host 가 ClientRpc 발사.
            BroadcastCoopReviveProgressIfDue(ratio);

            // 6. 완료 — host/guest 분기.
            //    host 면 직접 ForceRevive (NetworkVariable sync 정상).
            //    guest 면 ServerRpc 위임 → host 권위로 ForceRevive → sync.
            if (_coopReviveProgress >= cooperativeReviveChargeSeconds)
            {
                var target = _coopReviveTarget;
                var nm = Unity.Netcode.NetworkManager.Singleton;

                if (nm != null && nm.IsServer)
                {
                    // host — 직접 호출. server-authoritative Health → NetworkVariable sync 정상.
                    Debug.Log($"[KhiDownController] Cooperative revive 완료 (host) — reviver={gameObject.name} target={target.gameObject.name}");
                    target.ForceRevive();
                }
                else
                {
                    // guest — ServerRpc 위임. host 가 권위로 ForceRevive 처리 + 검증.
                    var targetNo = target.GetComponentInParent<Unity.Netcode.NetworkObject>();
                    var reviverSync = GetComponentInParent<LostMemory.Networking.Player.PlayerMovementSync>();
                    if (targetNo != null && targetNo.IsSpawned && reviverSync != null)
                    {
                        Debug.Log($"[KhiDownController] Cooperative revive 완료 (guest) — ServerRpc 발사 target={targetNo.NetworkObjectId} ({target.gameObject.name})");
                        reviverSync.RequestCooperativeReviveServerRpc(targetNo.NetworkObjectId);
                    }
                    else
                    {
                        Debug.LogWarning($"[KhiDownController] Cooperative revive — guest ServerRpc 위임 실패 targetNo={(targetNo != null ? "OK" : "null")} reviverSync={(reviverSync != null ? "OK" : "null")}");
                    }
                }
                ResetCoopReviveProgress();
            }
        }

        private void ResetCoopReviveProgress()
        {
            if (_coopReviveTarget != null && _coopReviveProgress > 0f)
            {
                _coopReviveTarget.ReviveProgressChanged?.Invoke(0f);
                // 모든 client 의 target 머리 위 bar 도 즉시 hide.
                BroadcastCoopReviveProgressImmediate(_coopReviveTarget, 0f);
            }
            _coopReviveProgress = 0f;
            _coopReviveTarget = null;
            _lastBroadcastProgress = -1f;
        }

        /// <summary>
        /// 살리는 사람 측에서 호출 — throttle 0.1s 또는 ratio 변화량 ≥ 0.05 시 broadcast.
        /// 호스트는 직접 ClientRpc, 게스트는 ServerRpc 거쳐 host 가 ClientRpc.
        /// </summary>
        private void BroadcastCoopReviveProgressIfDue(float ratio)
        {
            if (_coopReviveTarget == null) return;
            bool timeDue = Time.time >= _nextCoopProgressBroadcastTime;
            bool deltaDue = Mathf.Abs(ratio - _lastBroadcastProgress) >= 0.05f;
            if (!timeDue && !deltaDue) return;
            _nextCoopProgressBroadcastTime = Time.time + 0.1f;
            _lastBroadcastProgress = ratio;
            BroadcastCoopReviveProgressImmediate(_coopReviveTarget, ratio);
        }

        /// <summary>
        /// 즉시 broadcast (throttle 무시). reset / 시작 / 완료 직전 등.
        /// </summary>
        private void BroadcastCoopReviveProgressImmediate(KhiDownController target, float ratio)
        {
            if (target == null) return;
            var targetNo = target.GetComponentInParent<Unity.Netcode.NetworkObject>();
            if (targetNo == null || !targetNo.IsSpawned) return;
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null) return;

            if (nm.IsServer)
            {
                // 호스트 — 직접 target.PlayerHealthSync.BroadcastCoopReviveProgressClientRpc.
                var targetHs = targetNo.GetComponent<LostMemory.Networking.Player.PlayerHealthSync>();
                if (targetHs != null) targetHs.BroadcastCoopReviveProgressClientRpc(ratio);
            }
            else
            {
                // 게스트 — 자기 PlayerMovementSync.ServerRpc 발사 → host 가 broadcast.
                var reviverSync = GetComponentInParent<LostMemory.Networking.Player.PlayerMovementSync>();
                if (reviverSync != null) reviverSync.SubmitCoopReviveProgressServerRpc(targetNo.NetworkObjectId, ratio);
            }
        }

        /// <summary>
        /// 근처 Down 동료 검색. cooperativeReviveLayerMask 우선, 비어있으면 모든 KhiDownController fallback.
        /// 자기 자신 제외. Down 상태인 것만.
        /// </summary>
        private KhiDownController FindNearestDownAlly()
        {
            float searchRadius = cooperativeReviveDistance;
            float bestDistSqr = float.MaxValue;
            KhiDownController best = null;

            if (cooperativeReviveLayerMask.value != 0)
            {
                int hits = Physics2D.OverlapCircleNonAlloc(
                    transform.position,
                    searchRadius,
                    _coopReviveBuffer,
                    cooperativeReviveLayerMask);

                for (int i = 0; i < hits; i++)
                {
                    var col = _coopReviveBuffer[i];
                    if (col == null) continue;
                    var dc = col.GetComponentInParent<KhiDownController>();
                    if (dc == null || dc == this || !dc.IsDown) continue;

                    float dSqr = (dc.transform.position - transform.position).sqrMagnitude;
                    if (dSqr < bestDistSqr)
                    {
                        bestDistSqr = dSqr;
                        best = dc;
                    }
                }
                if (best != null) return best;
            }

            // Fallback — LayerMask 비어있거나 hit 없음. 씬 내 모든 KhiDownController 검색.
            var all = UnityEngine.Object.FindObjectsByType<KhiDownController>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                var dc = all[i];
                if (dc == null || dc == this || !dc.IsDown) continue;
                float dSqr = (dc.transform.position - transform.position).sqrMagnitude;
                if (dSqr <= searchRadius * searchRadius && dSqr < bestDistSqr)
                {
                    bestDistSqr = dSqr;
                    best = dc;
                }
            }
            return best;
        }

        /// <summary>
        /// R 키 hold 감지 — wasPressedThisFrame 이 아닌 매 frame isPressed.
        /// 협력 부활 차징용. New Input System 우선, legacy fallback.
        /// </summary>
        private bool IsReviveHeld()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.rKey.isPressed)
            {
                return true;
            }
#endif
            try
            {
                if (Input.GetKey(debugReviveKey)) return true;
            }
            catch (System.InvalidOperationException)
            {
                // New Input System 단독 모드 — 무시.
            }
            return false;
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

            // 결과창 진입 후에는 부활 거부 — 10s wait 끝나 Resulting 전이된 후 들어오는 부활 시도 차단.
            // wait 중(=RunFailed) 에는 IsResulting==false 라 정상 부활 가능.
            if (LostMemory.Stage.RunManager.Instance != null && LostMemory.Stage.RunManager.Instance.IsResulting)
            {
                Debug.Log("[KhiDownController] ForceRevive 거부 — RunManager 가 Resulting 상태(결과창 표시 중).", this);
                return;
            }

            CompleteRevive(null, healthOverride);
        }

        /// <summary>
        /// Network sync 진입점 — non-owner mirror 측에서 PlayerHealthSync._syncedDownState OnValueChanged 가
        /// 호출. _state 만 갱신해 동료 R hold 시 FindNearestDownAlly 가 IsDown=true 로 발견 가능하게 함.
        ///
        /// CRITICAL: visual (Animator trigger, Collider 비활성, UI) 은 PlayerHealthSync 가 이미 처리하므로
        /// 본 메서드는 _state 만 set. EnterDown / ForceRevive 자체 호출하면 이중 처리됨 (Hit trigger 등 중복).
        /// </summary>
        public void ApplyDownStateFromNetwork(KhiDownState newState)
        {
            if (_state == newState) return;
            _state = newState;
            // mirror 측 Defeated 전이도 static event 발화 — 서버가 게스트 player 의 Defeated 를 감지 가능.
            if (newState == KhiDownState.Defeated) AnyPlayerDefeated?.Invoke(this);

            // CRITICAL: mirror 측 _downEnterTime 을 Time.time 으로 set 해야 함.
            // 이걸 안 하면 _downEnterTime=0 (기본값) → TickDown 의
            //   remaining = downDuration - (Time.time - 0)
            //   = downDuration - Time.time
            // 가 음수 / 작은 양수 → 즉시 EnterDefeatedByTimeout 호출.
            // 호스트 측은 EnterDown 안에서 _downEnterTime = Time.time set 되므로 정상,
            // 게스트 화면 mirror 만 이 path (Update.TickDown) 에서 즉사하던 버그.
            if (newState == KhiDownState.Down)
            {
                _downEnterTime = Time.time;
                _nextTickEventTime = Time.time;
            }

            if (logStateTransitions) Debug.Log($"[KhiDownController] ApplyDownStateFromNetwork — _state set to {newState} (mirror sync), _downEnterTime={_downEnterTime:F2}. {gameObject.name}", this);
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
            AnyPlayerDefeated?.Invoke(this);

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

            // 외부 knockback / 충돌 force 차단 — Rigidbody2D 를 Kinematic 으로 잠시 전환.
            // collider 는 유지 (협력 부활 R 키 거리 체크용). transform 이동은 가능 (텔레포트 등).
            FreezeKnockback();
            FreezeRigidbody2D();
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

            // Rigidbody2D 원래 bodyType 복구 + 잔여 velocity 제거 (부활 시 휙 튀지 않게).
            UnfreezeKnockback();
            UnfreezeRigidbody2D();

            _hasCachedPermits = false;
        }

        private void FreezeRigidbody2D()
        {
            if (_cachedRigidbody2D == null)
            {
                _cachedRigidbody2D = GetComponent<Rigidbody2D>();
            }
            if (_cachedRigidbody2D == null || _hasCachedRigidbody2DState) return;

            _cachedRigidbody2DBodyType = _cachedRigidbody2D.bodyType;
            _hasCachedRigidbody2DState = true;
            _cachedRigidbody2D.linearVelocity = Vector2.zero;
            _cachedRigidbody2D.angularVelocity = 0f;
            _cachedRigidbody2D.bodyType = RigidbodyType2D.Kinematic;
        }

        private void UnfreezeRigidbody2D()
        {
            if (_cachedRigidbody2D == null || !_hasCachedRigidbody2DState) return;

            _cachedRigidbody2D.bodyType = _cachedRigidbody2DBodyType;
            _cachedRigidbody2D.linearVelocity = Vector2.zero;
            _cachedRigidbody2D.angularVelocity = 0f;
            _hasCachedRigidbody2DState = false;
        }

        private void FreezeKnockback()
        {
            if (health == null || _hasCachedImmuneToKnockback) return;
            _cachedImmuneToKnockback = health.ImmuneToKnockback;
            health.ImmuneToKnockback = true;
            _hasCachedImmuneToKnockback = true;
        }

        private void UnfreezeKnockback()
        {
            if (health == null || !_hasCachedImmuneToKnockback) return;
            health.ImmuneToKnockback = _cachedImmuneToKnockback;
            _hasCachedImmuneToKnockback = false;
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
