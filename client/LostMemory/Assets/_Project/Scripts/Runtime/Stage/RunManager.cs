using System.Collections;
using System.Collections.Generic;
using LostMemory.Memory;
using LostMemory.Networking.Common;
using LostMemory.Networking.Player;
using LostMemory.Player;
using LostMemory.Relics;
using LostMemory.SceneFlow;
using LostMemory.TestKhi;
using LostMemory.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace LostMemory.Stage
{
    /// <summary>
    /// CL-048 런 매니저 — 런 단위 상태 머신 보유 + 외부 시스템 wiring.
    ///
    /// 책임:
    ///   1. <see cref="RunStateMachine"/> 생성 / 보유
    ///   2. <see cref="DungeonRunBootstrap.DungeonBuilt"/> 구독 → InRun 전이
    ///   3. <see cref="RoomEntryRuntimeController.RoomCleared"/> 구독 → 보스방 클리어 시 포탈 진입 대기
    ///   4. <see cref="KhiPlayerStateAggregator.StateChanged"/> 구독 → Defeated 검출 시 RunFailed 전이
    ///   5. RunCleared / RunFailed 도달 후 일정 지연 → Resulting 전이 + UI 활성 (stub)
    ///   6. 외부 (UI 닫기 버튼 등) 가 <see cref="CloseResulting"/> 호출 → None 복귀
    ///
    /// Singleton + DontDestroyOnLoad. 향후 Bootstrap scene 도입 시 *해당 scene 으로 GameObject 이동*만.
    /// 멀티 도입 시 <see cref="IsAuthority"/> 게이트 한 줄만 호스트 권위 분기로 전환.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Stage/Run Manager")]
    public sealed class RunManager : MonoBehaviour
    {
        public static RunManager Instance { get; private set; }

        [Header("Refs")]
        [SerializeField] private DungeonRunBootstrap dungeonRunBootstrap;
        [SerializeField] private KhiPlayerStateAggregator playerStateAggregator;
        [SerializeField] private KhiDownController playerDownController;
        [SerializeField] private RunResultPanelView runResultPanelView;
        [Tooltip("CL-109: Run 종료 시 Clear() 호출 → OnCleared 이벤트 발화 → RelicEffectRegistry 가 modifier/shield 정리.")]
        [SerializeField] private PlayerRelicInventory playerRelicInventory;
        [Tooltip("CL-110: 방 클리어 → 보상 3택 흐름의 오케스트레이터. HandleDungeonBuilt 시 RoomCleared 구독, CloseResulting 시 해제.")]
        [SerializeField] private RewardController rewardController;
        [Tooltip("CL-113: Run 한정 골드 지갑. Combat 클리어 시 +50, CloseResulting 시 Reset.")]
        [SerializeField] private GoldWallet goldWallet;
        [Tooltip("기억 파편 누적·저장 서비스. 룸 클리어 시 파편 지급, CloseResulting 시 영구 저장.")]
        [SerializeField] private MemoryProgressTracker memoryProgressTracker;

        [Header("Behavior")]
        [SerializeField, Tooltip("Awake 후 자동으로 StartRun() 호출. 디버그 / 검증 시 편의용.")]
        private bool autoStartOnAwake = false;

        [SerializeField, Tooltip("RunCleared/RunFailed 도달 후 Resulting 까지의 지연 시간(초)")]
        private float resultingDelaySeconds = 2f;

        [SerializeField, Tooltip("RunFailed 도달 후 Resulting 까지의 지연 시간(초). 사망 애니메이션 확인용.")]
        private float failureResultingDelaySeconds = 5f;

        [SerializeField, Tooltip("결과창의 마을로 버튼을 눌렀을 때 로드할 씬 이름.")]
        private string townSceneName = "Town";

        [SerializeField, Tooltip("Editor Play Mode에서 Build Settings 이름 로드가 막힐 때 사용할 Town 씬 경로.")]
        private string townScenePath = "Assets/_Project/Scenes/Town/Town.unity";

        [SerializeField, Tooltip("멀티 세션 활성 시 결과창 마을로 버튼을 눌렀을 때 로드할 로비 씬 이름. " +
            "비워두면 기존 townSceneName 흐름 (솔로 경로) 유지. NGO SceneManager.LoadScene 으로 호스트가 트리거 → 모든 클라 sync, 세션 유지.")]
        private string lobbySceneName = string.Empty;

        [SerializeField, Tooltip("Editor Play Mode 또는 build 안 등록 시 fallback 으로 사용할 로비 씬 경로.")]
        private string lobbyScenePath = string.Empty;

        [SerializeField, Tooltip("Result Restart button target dungeon start scene name.")]
        private string restartDungeonSceneName = "Dungeon_1F_1R";

        [SerializeField, Tooltip("Editor Play Mode fallback path for the restart dungeon start scene.")]
        private string restartDungeonScenePath = "Assets/_Project/Scenes/Dungeon/Dungeon_1F_1R.unity";

        [Header("Stage Progression")]
        [SerializeField, Min(1), Tooltip("이번 런에서 진행할 스테이지 수. 보스 클리어 포탈 진입 시 다음 스테이지가 없으면 결과 화면으로 간다.")]
        private int totalStageCount = 1;

        [SerializeField, Tooltip("2-1 개발 전 임시 운영값. false 면 보스 클리어 포탈은 다음 스테이지 대신 결과 화면으로 간다.")]
        private bool allowBossPortalStageAdvance;

        [SerializeField, Tooltip("보스 클리어 포탈/스테이지 진행 로그 출력.")]
        private bool logStageProgression = true;

        public RunStateMachine StateMachine { get; private set; }

        // Phase B-1: HostAuthority.IsHost 로 통일. 싱글 실행 시 NetworkManager 비활성 → true 반환.
        // 멀티 실행 시 호스트(=서버)만 true. 클라이언트는 false 로 진입 차단.
        public bool IsAuthority => HostAuthority.IsHost;

        public int CurrentStageIndex { get; private set; }
        public int CurrentStageNumber => CurrentStageIndex + 1;
        public int TotalStageCount => Mathf.Max(1, totalStageCount);
        public bool HasNextStage => CurrentStageIndex + 1 < TotalStageCount;

        private readonly HashSet<RoomEntryRuntimeController> _subscribedControllers = new HashSet<RoomEntryRuntimeController>();
        private float runStartedAt;
        private int killCount;
        private int bossKillCount;
        private int totalDamage;
        private int memoryFragments;
        private bool bossClearPortalReady;
        private bool townReturnInProgress;
        private bool restartSceneLoadPending;
        // 멀티 환경에서 ShowResultingUI 가 두 경로(호스트 측 DelayedTransitionToResulting 코루틴 + 게스트 측 ClientRpc)
        // 에서 호출될 수 있어 idempotent guard 필요. CleanupRunResultingState 에서 reset.
        private bool _resultingUiShown;
        private Coroutine refreshSceneSubscriptionsRoutine;
        private DungeonRunBootstrap subscribedDungeonRunBootstrap;
        private KhiPlayerStateAggregator subscribedPlayerStateAggregator;
        private KhiDownController subscribedPlayerDownController;
        private RunResultPanelView subscribedRunResultPanelView;

        // 파티 전원 전투불능 판정용 폴링 + 결과 전환 코루틴 중복 방지.
        private Coroutine resultTransitionRoutine;
        private float nextPartyDefeatCheckAt;
        private const float PartyDefeatCheckInterval = 0.25f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[RunManager] Duplicate instance detected. Destroying.", this);
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            ResolveEconomyRefs();

            StateMachine = new RunStateMachine();
            StateMachine.StateChanged += LogStateChange;
        }

        private void OnEnable()
        {
            ResolveEconomyRefs();
            ResolveSceneRefs();
            // CL-113: RunResult 버튼 wiring. develop 의 단순 += CloseResulting 대신 우리 핸들러 채택 —
            // HandleRestartRequested 가 SceneManager.LoadScene 으로 씬 재로드까지 수행 (superset).
            BindRunResultPanelView(runResultPanelView);

            SceneManager.sceneLoaded += HandleSceneLoaded;
            QueueSceneRefresh();
        }

        private void OnDisable()
        {
            UnbindDungeonRunBootstrap();
            UnbindPlayerStateAggregator();
            UnbindPlayerDownController();
            UnbindRunResultPanelView();

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (refreshSceneSubscriptionsRoutine != null)
            {
                StopCoroutine(refreshSceneSubscriptionsRoutine);
                refreshSceneSubscriptionsRoutine = null;
            }

            UnsubscribeAllRoomControllers();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Instance != this || StateMachine == null)
            {
                return;
            }

            if (StateMachine.Current != RunState.None &&
                StateMachine.Current != RunState.Initializing &&
                StateMachine.Current != RunState.InRun)
            {
                return;
            }

            QueueSceneRefresh();
        }

        private void QueueSceneRefresh()
        {
            if (refreshSceneSubscriptionsRoutine != null)
            {
                StopCoroutine(refreshSceneSubscriptionsRoutine);
            }

            refreshSceneSubscriptionsRoutine = StartCoroutine(RefreshSceneSubscriptionsNextFrame());
        }

        private IEnumerator RefreshSceneSubscriptionsNextFrame()
        {
            yield return null;
            refreshSceneSubscriptionsRoutine = null;

            ResolveSceneRefs();

            bool adoptedRouteRun = TryBeginLoadedRouteRun();
            if (StateMachine.Current != RunState.Initializing &&
                StateMachine.Current != RunState.InRun)
            {
                yield break;
            }

            SubscribeAllRoomControllers();
            if (rewardController != null)
            {
                rewardController.SubscribeAllRoomControllers();
            }

            ResolveRunResultPanelView();

            if (adoptedRouteRun && StateMachine.Current == RunState.Initializing)
            {
                StateMachine.TryTransition(RunState.InRun);
            }
        }

        private void ResolveSceneRefs()
        {
            BindDungeonRunBootstrap(ResolveActiveSceneComponent(dungeonRunBootstrap, FindObjectsInactive.Include));
            BindPlayerStateAggregator(ResolveActiveSceneComponent(playerStateAggregator, FindObjectsInactive.Exclude));
            BindPlayerDownController(ResolveActiveSceneComponent(playerDownController, FindObjectsInactive.Exclude));

            if (playerRelicInventory == null)
            {
                playerRelicInventory = ResolveActiveSceneComponent<PlayerRelicInventory>(null, FindObjectsInactive.Include);
            }

            ResolveRunResultPanelView();
        }

        private static T ResolveActiveSceneComponent<T>(T current, FindObjectsInactive inactive) where T : Component
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (current != null && current.gameObject.scene == activeScene)
            {
                return current;
            }

            T[] components = FindObjectsByType<T>(inactive, FindObjectsSortMode.None);
            for (int i = 0; i < components.Length; i++)
            {
                T component = components[i];
                if (component != null && component.gameObject.scene == activeScene)
                {
                    return component;
                }
            }

            return null;
        }

        private void BindDungeonRunBootstrap(DungeonRunBootstrap bootstrap)
        {
            if (subscribedDungeonRunBootstrap == bootstrap)
            {
                return;
            }

            UnbindDungeonRunBootstrap();
            dungeonRunBootstrap = bootstrap;

            if (bootstrap == null)
            {
                return;
            }

            subscribedDungeonRunBootstrap = bootstrap;
            subscribedDungeonRunBootstrap.DungeonBuilt += HandleDungeonBuilt;
        }

        private void UnbindDungeonRunBootstrap()
        {
            if (subscribedDungeonRunBootstrap != null)
            {
                subscribedDungeonRunBootstrap.DungeonBuilt -= HandleDungeonBuilt;
            }

            subscribedDungeonRunBootstrap = null;
        }

        private void BindPlayerStateAggregator(KhiPlayerStateAggregator aggregator)
        {
            if (subscribedPlayerStateAggregator == aggregator)
            {
                return;
            }

            UnbindPlayerStateAggregator();
            playerStateAggregator = aggregator;

            if (aggregator == null)
            {
                return;
            }

            subscribedPlayerStateAggregator = aggregator;
            subscribedPlayerStateAggregator.StateChanged += HandlePlayerStateChanged;
        }

        private void UnbindPlayerStateAggregator()
        {
            if (subscribedPlayerStateAggregator != null)
            {
                subscribedPlayerStateAggregator.StateChanged -= HandlePlayerStateChanged;
            }

            subscribedPlayerStateAggregator = null;
        }

        private void BindPlayerDownController(KhiDownController downController)
        {
            if (subscribedPlayerDownController == downController)
            {
                return;
            }

            UnbindPlayerDownController();
            playerDownController = downController;

            if (downController == null)
            {
                return;
            }

            subscribedPlayerDownController = downController;
            subscribedPlayerDownController.DefeatedByTimeout += HandlePlayerDefeatedDirect;
            subscribedPlayerDownController.DefeatedSolo += HandlePlayerDefeatedDirect;
        }

        private void UnbindPlayerDownController()
        {
            if (subscribedPlayerDownController != null)
            {
                subscribedPlayerDownController.DefeatedByTimeout -= HandlePlayerDefeatedDirect;
                subscribedPlayerDownController.DefeatedSolo -= HandlePlayerDefeatedDirect;
            }

            subscribedPlayerDownController = null;
        }

        private RunResultPanelView ResolveRunResultPanelView()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (runResultPanelView != null && runResultPanelView.gameObject.scene == activeScene)
            {
                BindRunResultPanelView(runResultPanelView);
                return runResultPanelView;
            }

            RunResultPanelView[] views = FindObjectsByType<RunResultPanelView>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < views.Length; i++)
            {
                RunResultPanelView view = views[i];
                if (view != null && view.gameObject.scene == activeScene)
                {
                    BindRunResultPanelView(view);
                    return view;
                }
            }

            if (views.Length > 0)
            {
                BindRunResultPanelView(views[0]);
            }

            return runResultPanelView;
        }

        private void BindRunResultPanelView(RunResultPanelView view)
        {
            if (subscribedRunResultPanelView == view)
            {
                return;
            }

            if (subscribedRunResultPanelView != null)
            {
                subscribedRunResultPanelView.OnLobby -= ReturnToTown;
                subscribedRunResultPanelView.OnRestart -= HandleRestartRequested;
            }

            subscribedRunResultPanelView = null;
            runResultPanelView = view;

            if (view == null)
            {
                return;
            }

            subscribedRunResultPanelView = view;
            subscribedRunResultPanelView.OnLobby += ReturnToTown;
            subscribedRunResultPanelView.OnRestart += HandleRestartRequested;
        }

        private void UnbindRunResultPanelView()
        {
            if (subscribedRunResultPanelView != null)
            {
                subscribedRunResultPanelView.OnLobby -= ReturnToTown;
                subscribedRunResultPanelView.OnRestart -= HandleRestartRequested;
            }

            subscribedRunResultPanelView = null;
        }

        private bool TryBeginLoadedRouteRun()
        {
            if (StateMachine.Current != RunState.None)
            {
                return false;
            }

            if (!IsAuthority)
            {
                return false;
            }

            StageRouteManager routeManager = StageRouteManager.Instance;
            if (routeManager == null && !HasActiveDungeonRunRefs())
            {
                return false;
            }

            if (restartSceneLoadPending && routeManager != null)
            {
                routeManager.InitializeRouteNode(0, string.Empty, false);
            }
            restartSceneLoadPending = false;

            CurrentStageIndex = 0;
            bossClearPortalReady = false;
            ResetRunResultTracking();

            if (!StateMachine.TryTransition(RunState.Initializing))
            {
                return false;
            }

            if (logStageProgression)
            {
                Debug.Log("[RunManager] Adopted loaded dungeon scene as active run.", this);
            }

            return true;
        }

        private bool HasActiveDungeonRunRefs()
        {
            return dungeonRunBootstrap != null
                && playerDownController != null
                && ResolveRunResultPanelView() != null;
        }

        private void Start()
        {
            if (autoStartOnAwake)
            {
                StartRun();
            }
        }

        private void Update()
        {
            if (Instance != this || StateMachine == null)
            {
                return;
            }
            if (StateMachine.Current != RunState.InRun)
            {
                return;
            }
            if (Time.unscaledTime < nextPartyDefeatCheckAt)
            {
                return;
            }
            nextPartyDefeatCheckAt = Time.unscaledTime + PartyDefeatCheckInterval;

            if (AreAllActivePlayersDownOrDefeated())
            {
                FailRunFromPartyDefeat();
            }
        }

        /// <summary>
        /// 활성 플레이어 모두가 Down 또는 Defeated 인지 검사.
        /// 싱글: 1명이 다운/사망이면 true.
        /// 멀티: 파티 전원 다운/사망이면 true.
        /// 활성 플레이어가 0명이면 false (런 시작 전/씬 전환 중 등).
        /// </summary>
        private bool AreAllActivePlayersDownOrDefeated()
        {
            KhiDownController[] players = FindObjectsByType<KhiDownController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            if (players.Length == 0)
            {
                return false;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            bool foundPlayer = false;
            for (int i = 0; i < players.Length; i++)
            {
                KhiDownController player = players[i];
                if (player == null || player.gameObject.scene != activeScene)
                {
                    continue;
                }
                foundPlayer = true;
                if (!player.IsDown && !player.IsDefeated)
                {
                    return false;
                }
            }
            return foundPlayer;
        }

        /// <summary>이벤트 핸들러용 — 즉시 파티 상태 검사 후 실패 트리거 여부 판정.</summary>
        private void CheckPartyDefeatNow()
        {
            if (!EnsureRunActiveForDefeat())
            {
                return;
            }
            if (!AreAllActivePlayersDownOrDefeated())
            {
                return;
            }
            FailRunFromPartyDefeat();
        }

        /// <summary>파티 전원 전투불능 → RunFailed 전이 + 결과 패널 전환 예약. 단일 진입점.</summary>
        private void FailRunFromPartyDefeat()
        {
            if (!EnsureRunActiveForDefeat())
            {
                return;
            }
            if (!StateMachine.TryTransition(RunState.RunFailed))
            {
                return;
            }
            bossClearPortalReady = false;
            BeginDelayedTransitionToResulting(failureResultingDelaySeconds);
        }

        /// <summary>결과 전환 코루틴을 시작하며 중복을 방지.</summary>
        private void BeginDelayedTransitionToResulting(float delaySeconds)
        {
            if (resultTransitionRoutine != null)
            {
                StopCoroutine(resultTransitionRoutine);
            }
            resultTransitionRoutine = StartCoroutine(DelayedTransitionToResulting(delaySeconds));
        }

        /// <summary>
        /// 런 시작 — None → Initializing 전이 + DungeonRunBootstrap.BuildRun() 호출.
        /// 호스트 권위 가드 적용. 외부 (UI 시작 버튼 / autoStartOnAwake) 가 호출.
        /// </summary>
        public void StartRun()
        {
            if (!IsAuthority)
            {
                return;
            }
            if (!StateMachine.TryTransition(RunState.Initializing))
            {
                return;
            }
            CurrentStageIndex = 0;
            bossClearPortalReady = false;
            ResetRunResultTracking();
            BuildCurrentStage();
        }

        /// <summary>
        /// 보스 클리어 포탈 진입 시 호출. 다음 스테이지가 있으면 새 스테이지를 빌드하고, 없으면 결과 화면으로 진입한다.
        /// </summary>
        public bool NotifyBossClearPortalEntered()
        {
            if (!IsAuthority)
            {
                return false;
            }
            if (StateMachine.Current != RunState.InRun)
            {
                Debug.LogWarning($"[RunManager] Boss clear portal ignored. Current state is {StateMachine.Current}.", this);
                return false;
            }
            if (!bossClearPortalReady)
            {
                Debug.LogWarning("[RunManager] Boss clear portal ignored before boss room clear.", this);
                return false;
            }

            bossClearPortalReady = false;

            bool handled = allowBossPortalStageAdvance && HasNextStage
                ? AdvanceToNextStage()
                : CompleteRunFromBossPortal();
            if (!handled)
            {
                bossClearPortalReady = true;
            }

            return handled;
        }

        private void BuildCurrentStage()
        {
            if (dungeonRunBootstrap == null)
            {
                Debug.LogWarning("[RunManager] dungeonRunBootstrap reference is null. Cannot build run.", this);
                return;
            }
            if (logStageProgression)
            {
                Debug.Log($"[RunManager] Build stage {CurrentStageNumber}/{TotalStageCount}.");
            }
            dungeonRunBootstrap.BuildRun();
        }

        /// <summary>
        /// 결과 화면 닫기 — Resulting → None.
        /// 외부 (RunResultPanelView 의 OnLobby / OnRestart 또는 디버그 키) 가 호출.
        /// </summary>
        public void CloseResulting()
        {
            TryCloseResulting();
        }

        public void ReturnToTown()
        {
            ReturnToTown(saveRunRewards: true);
        }

        public void AbandonRunAndReturnToTown()
        {
            ReturnToTown(saveRunRewards: false);
        }

        private void ReturnToTown(bool saveRunRewards)
        {
            if (townReturnInProgress)
            {
                Debug.LogWarning("[RunManager] Town return is already in progress.", this);
                return;
            }

            if (!IsAuthority)
            {
                Debug.LogWarning("[RunManager] Town return ignored because this instance has no authority.", this);
                return;
            }

            NetworkManager networkManager = NetworkManager.Singleton;
            bool networkSessionActive = networkManager != null && networkManager.IsListening;
            if (networkSessionActive && !networkManager.IsServer)
            {
                Debug.LogWarning("[RunManager] Town return must be requested on the server/host. Client request RPC is not wired yet.", this);
                return;
            }

            // 멀티 분기 — lobbySceneName 이 set 된 경우만 lobby 경로. 솔로 또는 lobby 미지정 시 기존 town 흐름 그대로.
            bool useLobbyScene = networkSessionActive && !string.IsNullOrWhiteSpace(lobbySceneName);
            string targetSceneName = useLobbyScene ? lobbySceneName : townSceneName;
            string targetScenePath = useLobbyScene ? lobbyScenePath : townScenePath;

            if (string.IsNullOrWhiteSpace(targetSceneName))
            {
                Debug.LogWarning($"[RunManager] Return scene name is empty. (useLobbyScene={useLobbyScene})", this);
                return;
            }

            bool canLoadSceneName = Application.CanStreamedLevelBeLoaded(targetSceneName);
            if (!canLoadSceneName && !CanUseEditorScenePath(networkSessionActive, targetScenePath))
            {
                Debug.LogWarning($"[RunManager] Scene '{targetSceneName}' is not available. Add it to Build Settings or set a valid scene path.", this);
                return;
            }

            townReturnInProgress = true;
            // TownSpawnRouter 는 town 씬 spawn 전용 — lobby 분기 시 skip (lobby 씬 자체 spawn 로직 사용).
            if (!useLobbyScene)
            {
                TownSpawnRouter.RequestTownReturn(targetSceneName);
            }
            if (!TryCloseResulting(saveRunRewards))
            {
                CleanupRunResultingState(saveRunRewards);
            }
            Time.timeScale = 1f;

            // 멀티 환경:
            //   lobby 분기 (마을→로비 복귀) → 정공법으로 PlayerObject Despawn + 새 씬에서 fresh 재 spawn.
            //     DontDestroyOnLoad 인 player 의 사망 잔재 문제 근본 해결.
            //   townScene 분기 (기존 솔로 town 로 복귀) → 기존 reset broadcast 유지 (회귀 차단).
            if (networkSessionActive)
            {
                if (useLobbyScene)
                {
                    PlayerHealthSync.RespawnPlayersAfterSceneLoad();
                }
                else
                {
                    PlayerHealthSync.BroadcastResetDeathStateForAll();
                }
            }

            if (networkSessionActive)
            {
                networkManager.SceneManager.LoadScene(targetSceneName, LoadSceneMode.Single);
            }
            else if (canLoadSceneName)
            {
                SceneManager.LoadScene(targetSceneName, LoadSceneMode.Single);
            }
            else
            {
                LoadSceneInEditorPlayMode(targetScenePath, targetSceneName);
            }

            Destroy(gameObject);
        }

        private static bool CanUseEditorScenePath(bool networkSessionActive, string scenePath)
        {
#if UNITY_EDITOR
            return !networkSessionActive && !string.IsNullOrWhiteSpace(scenePath);
#else
            return false;
#endif
        }

        private static void LoadSceneInEditorPlayMode(string scenePath, string sceneName)
        {
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Debug.LogWarning($"[RunManager] Scene '{sceneName}' is not available in this build.");
#endif
        }

        private bool CanUseEditorRestartDungeonScenePath(bool networkSessionActive)
        {
#if UNITY_EDITOR
            return !networkSessionActive && !string.IsNullOrWhiteSpace(restartDungeonScenePath);
#else
            return false;
#endif
        }

        private void LoadRestartDungeonSceneInEditorPlayMode()
        {
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode(restartDungeonScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Debug.LogWarning($"[RunManager] Restart dungeon scene '{restartDungeonSceneName}' is not available in this build.", this);
#endif
        }

        private bool TryCloseResulting(bool saveRunRewards = true)
        {
            if (StateMachine.Current == RunState.None)
            {
                return true;
            }

            if (!StateMachine.TryTransition(RunState.None))
            {
                return false;
            }

            CleanupRunResultingState(saveRunRewards);
            return true;
        }

        private void CleanupRunResultingState(bool saveRunRewards = true)
        {
            ResolveEconomyRefs();
            bossClearPortalReady = false;
            _resultingUiShown = false;
            RunResultPanelView resultPanelView = ResolveRunResultPanelView();
            if (resultPanelView != null)
            {
                resultPanelView.Hide();
            }
            UnsubscribeAllRoomControllers();
            // CL-110: RewardController 의 RoomCleared 구독도 해제.
            if (rewardController != null)
            {
                rewardController.UnsubscribeAllRoomControllers();
            }
            // CL-109: 인벤토리 비우기 → OnCleared 이벤트 → RelicEffectRegistry 가 modifier/shield 일괄 정리.
            if (playerRelicInventory != null)
            {
                playerRelicInventory.Clear();
            }
            // CL-113: 골드 Run 한정 — 다음 Run 시작 시 0 부터.
            if (goldWallet != null)
            {
                goldWallet.ResetToInitial();
            }
            // 결과창 경로는 파편을 저장하고, ESC 포기 경로는 이번 런 pending 파편을 버린다.
            if (memoryProgressTracker != null)
            {
                if (saveRunRewards)
                {
                    memoryProgressTracker.SaveRunShards();
                }
                else
                {
                    memoryProgressTracker.DiscardRunShards();
                }
            }
            // 씬 전환 시 캐리오버용 Player 스냅샷도 함께 초기화 — 다음 런은 빈 인벤토리/풀HP.
            PlayerRunState.Instance?.Clear();
        }

        private void HandleDungeonBuilt()
        {
            if (StateMachine.Current != RunState.Initializing)
            {
                return;
            }
            SubscribeAllRoomControllers();
            // CL-110: RewardController 도 RoomCleared 구독 (Combat 방 보상 3택 흐름).
            if (rewardController != null)
            {
                rewardController.SubscribeAllRoomControllers();
            }
            StateMachine.TryTransition(RunState.InRun);

            // 기억 시스템 메타 보너스 적용 — 던전 진입 시점에 골드 보너스 / 시작 유물 추첨.
            // (조각 해금 시 MemoryPieceUnlockService 가 누적 저장 → 매 런 시작 시 여기서 소비/적용)
            MemorySaveData memorySave = MemoryMetaService.Load();

            // StartingGold 보상 — BonusStartingGold 만큼 GoldWallet 에 추가.
            // GoldWallet.Awake 에서 initialGold 로 이미 초기화됨 → 그 위에 더함.
            // (탐욕 set 의 GainMultiplier 가 적용될 수 있음 — 의도된 일관성)
            int bonusGold = memorySave.BonusStartingGold;
            if (bonusGold > 0 && goldWallet != null)
            {
                goldWallet.Add(bonusGold);
                Debug.Log($"[RunManager] StartingGold bonus 적용 — +{bonusGold}");
            }

            // StartingRelicCount 보상 — N번 연속 보상 패널.
            int relicCount = memorySave.BonusStartingRelicCount;
            if (relicCount > 0 && rewardController != null)
            {
                Debug.Log($"[RunManager] StartingRelicReward 트리거 — count={relicCount}");
                rewardController.ShowStartingRelicReward(relicCount);
            }

            // ReviveOnce 보상 — HasRevive=true 면 런 1회 자동 부활 활성화.
            // KhiDownController.EnterDown 에서 _memoryReviveAvailable 체크 → ForceRevive 자동 호출.
            if (memorySave.HasRevive)
            {
                var downController = FindAnyObjectByType<TestKhi.KhiDownController>();
                if (downController != null)
                {
                    downController.SetMemoryReviveAvailable(true);
                    Debug.Log("[RunManager] ReviveOnce 활성화 — 런 1회 자동 부활 준비");
                }
                else
                {
                    Debug.LogWarning("[RunManager] ReviveOnce 활성화 실패 — KhiDownController 미발견");
                }
            }
        }

        private void SubscribeAllRoomControllers()
        {
            UnsubscribeAllRoomControllers();
            // DA 가 spawn 한 module 들 안에 있는 controller 모두 검색
            RoomEntryRuntimeController[] controllers = FindObjectsOfType<RoomEntryRuntimeController>();
            for (int i = 0; i < controllers.Length; i++)
            {
                RoomEntryRuntimeController c = controllers[i];
                if (c == null)
                {
                    continue;
                }
                c.RoomCleared += HandleRoomCleared;
                _subscribedControllers.Add(c);
            }
            Debug.Log($"[RunManager] Subscribed to {_subscribedControllers.Count} room controllers.");
        }

        private void UnsubscribeAllRoomControllers()
        {
            foreach (RoomEntryRuntimeController c in _subscribedControllers)
            {
                if (c != null)
                {
                    c.RoomCleared -= HandleRoomCleared;
                }
            }
            _subscribedControllers.Clear();
        }

        private void HandleRoomCleared(RoomClearedPayload payload)
        {
            ResolveEconomyRefs();

            if (StateMachine.Current != RunState.InRun)
            {
                return;
            }
            if (payload.Data == null)
            {
                Debug.LogWarning("[RunManager] RoomCleared payload has null RoomData; ignoring.", this);
                return;
            }
            // CL-113: Combat 방 클리어 시 보상 골드 +50.
            // CL-115 B: 의도 로그 — GoldWallet 자동 로그가 *왜* 는 안 알려주므로.
            bool isRewardEligibleCombatRoom = payload.Data.RoomType == StageRoomType.Combat && payload.HasSpawnedEnemies;
            if (isRewardEligibleCombatRoom && goldWallet != null)
            {
                Debug.Log("[RunManager] Combat clear reward gold +50.");
                goldWallet.Add(50);
            }
            // 룸 타입/크기에 따른 파편 정산 카운트 기록. 영구 지급은 런 결과 정리 시점에 수행한다.
            if (isRewardEligibleCombatRoom || payload.Data.RoomType == StageRoomType.Boss)
            {
                memoryProgressTracker?.RecordRoomClear(payload.Data);
            }
            // develop: 보스방 외 클리어는 런 흐름에 영향 X (다음 방 자연 진입).
            if (payload.Data.RoomType != StageRoomType.Boss)
            {
                return;
            }
            // 보스방 클리어 = 보스 클리어 포탈 활성화 (즉시 RunCleared 전이 X — 포탈 진입 시 NotifyBossClearPortalEntered 가 트리거).
            bossKillCount++;
            bossClearPortalReady = true;
            if (logStageProgression)
            {
                Debug.Log($"[RunManager] Boss room cleared. Boss clear portal is ready. Stage={CurrentStageNumber}/{TotalStageCount}.", this);
            }
        }

        private bool AdvanceToNextStage()
        {
            if (!StateMachine.TryTransition(RunState.Initializing))
            {
                return false;
            }

            UnsubscribeAllRoomControllers();
            if (rewardController != null)
            {
                rewardController.UnsubscribeAllRoomControllers();
            }

            CurrentStageIndex++;
            BuildCurrentStage();
            return true;
        }

        private bool CompleteRunFromBossPortal()
        {
            if (!StateMachine.TryTransition(RunState.RunCleared))
            {
                return false;
            }

            BeginDelayedTransitionToResulting(resultingDelaySeconds);
            return true;
        }

        private void HandlePlayerStateChanged(KhiPlayerState prev, KhiPlayerState current)
        {
            // 정책: 파티 전원 Down/Defeated 시점에만 RunFailed.
            // 본 이벤트는 즉시 검사 트리거. Update() 폴링이 누락 케이스도 잡음.
            if (current != KhiPlayerState.Down && current != KhiPlayerState.Defeated)
            {
                return;
            }
            CheckPartyDefeatNow();
        }

        // KhiPlayerStateAggregator 는 Player GameObject 가 Defeated 와 동일 프레임에 비활성화되어
        // Update 폴링이 StateChanged(Defeated) 를 발행하지 못한다.
        // KhiDownController 의 DefeatedByTimeout / DefeatedSolo 는 GameObject 비활성화 *직전* 에 발행되므로
        // RunFailed 트리거의 신뢰 경로. Aggregator 구독은 잔존시켜 비치명 경로(있을 시) 보호.
        //
        // public 변경: 멀티 환경에서 PlayerHealthSync 가 "팀 전체 사망" 감지 시 직접 호출.
        // RunManager 는 local player 한 명의 KhiDownController 만 hook 하므로,
        // 다른 player 가 죽는 경로는 외부 트리거가 필요함.
        public void HandlePlayerDefeatedDirect()
        {
            CheckPartyDefeatNow();
        }

        private bool EnsureRunActiveForDefeat()
        {
            if (StateMachine.Current == RunState.InRun)
            {
                return true;
            }

            if (StateMachine.Current == RunState.None)
            {
                ResolveSceneRefs();
                TryBeginLoadedRouteRun();
            }

            if (StateMachine.Current == RunState.Initializing)
            {
                SubscribeAllRoomControllers();
                if (rewardController != null)
                {
                    rewardController.SubscribeAllRoomControllers();
                }
                ResolveRunResultPanelView();
                StateMachine.TryTransition(RunState.InRun);
            }

            return StateMachine.Current == RunState.InRun;
        }

        private IEnumerator DelayedTransitionToResulting(float delaySeconds)
        {
            // 일시정지(timeScale=0) 상태에서도 결과 전환이 진행되도록 Realtime 사용.
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, delaySeconds));
            resultTransitionRoutine = null;

            if (!StateMachine.TryTransition(RunState.Resulting))
            {
                yield break;
            }
            ShowResultingUI();
        }

        // public 변경: 멀티 환경에서 PlayerHealthSync 의 ClientRpc 가 게스트 화면에도 결과 패널 표시하도록 외부 호출.
        // _resultingUiShown idempotent guard — 호스트 측은 DelayedTransitionToResulting 코루틴 + ClientRpc 양쪽에서 호출될 수 있음.
        public void ShowResultingUI()
        {
            // 호스트 측은 DelayedTransitionToResulting 코루틴 + PlayerHealthSync ClientRpc 양쪽에서 호출될 수 있음 — idempotent 가드.
            if (_resultingUiShown)
            {
                return;
            }
            _resultingUiShown = true;

            // 다시 시작 후 timeScale 이 0 으로 남아있을 가능성 대비 — 결과 패널 표시 직전 1 로 복구.
            Time.timeScale = 1f;

            RunResultPanelView resultPanelView = ResolveRunResultPanelView();
            if (resultPanelView == null)
            {
                Debug.LogWarning("[RunManager] Resulting state — no RunResultPanelView wired (stub).", this);
                return;
            }

            resultPanelView.Show(BuildRunResultData());
            Debug.Log("[RunManager] Resulting state — RunResultPanelView shown.");
        }

        private void ResetRunResultTracking()
        {
            ResolveEconomyRefs();
            runStartedAt = Time.time;
            killCount = 0;
            bossKillCount = 0;
            totalDamage = 0;
            memoryFragments = 0;
            memoryProgressTracker?.ResetRunShards();
        }

        private RunResultData BuildRunResultData()
        {
            float playTime = Mathf.Max(0f, Time.time - runStartedAt);
            return new RunResultData
            {
                KillCount = killCount,
                BossKillCount = bossKillCount,
                TotalDamage = totalDamage,
                PlayTime = playTime,
                MemoryFragments = memoryProgressTracker != null
                    ? memoryProgressTracker.ThisRunShards
                    : memoryFragments,
            };
        }

        private void ResolveEconomyRefs()
        {
            if (goldWallet == null)
            {
                goldWallet = GetComponent<GoldWallet>();
                if (goldWallet == null)
                {
                    goldWallet = gameObject.AddComponent<GoldWallet>();
                }
            }

            if (memoryProgressTracker == null)
            {
                memoryProgressTracker = GetComponent<MemoryProgressTracker>();
                if (memoryProgressTracker == null)
                {
                    memoryProgressTracker = gameObject.AddComponent<MemoryProgressTracker>();
                }
            }
        }

        private void LogStateChange(RunState prev, RunState current)
        {
            Debug.Log($"[RunManager] {prev} -> {current}");
        }

        // Result Restart button: restart the run from the first dungeon route scene.
        private void HandleRestartRequested()
        {
            if (!IsAuthority)
            {
                Debug.LogWarning("[RunManager] Restart ignored because this instance has no authority.", this);
                return;
            }

            if (string.IsNullOrWhiteSpace(restartDungeonSceneName))
            {
                Debug.LogWarning("[RunManager] Restart dungeon scene name is empty.", this);
                return;
            }

            NetworkManager networkManager = NetworkManager.Singleton;
            bool networkSessionActive = networkManager != null && networkManager.IsListening;
            if (networkSessionActive && !networkManager.IsServer)
            {
                Debug.LogWarning("[RunManager] Restart must be requested on the server/host. Client request RPC is not wired yet.", this);
                return;
            }

            bool canLoadSceneName = Application.CanStreamedLevelBeLoaded(restartDungeonSceneName);
            if (!canLoadSceneName && !CanUseEditorRestartDungeonScenePath(networkSessionActive))
            {
                Debug.LogWarning($"[RunManager] Restart dungeon scene '{restartDungeonSceneName}' is not available. Add it to Build Settings or set a valid restart scene path.", this);
                return;
            }

            Debug.Log($"[RunManager] Restart requested. Loading '{restartDungeonSceneName}'.");
            if (!TryCloseResulting())
            {
                CleanupRunResultingState();
            }
            restartSceneLoadPending = true;
            Time.timeScale = 1f;

            if (networkSessionActive)
            {
                networkManager.SceneManager.LoadScene(restartDungeonSceneName, LoadSceneMode.Single);
            }
            else if (canLoadSceneName)
            {
                SceneManager.LoadScene(restartDungeonSceneName, LoadSceneMode.Single);
            }
            else
            {
                LoadRestartDungeonSceneInEditorPlayMode();
            }
        }

        // CL-113: 결산 창의 Lobby 버튼. 마을 씬 도입 (CL-117) 까지 CloseResulting 만.
        private void HandleLobbyRequested()
        {
            Debug.Log("[RunManager] Lobby requested. (TODO: CL-117 마을 씬 로드. 현재는 CloseResulting 만.)");
            CloseResulting();
        }
    }
}
