using System.Collections;
using System.Collections.Generic;
using LostMemory.Memory;
using LostMemory.Networking.Common;
using LostMemory.Relics;
using LostMemory.TestKhi;
using LostMemory.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        [SerializeField, Tooltip("결과창의 마을로 버튼을 눌렀을 때 로드할 씬 이름.")]
        private string townSceneName = "Town";

        [Header("Stage Progression")]
        [SerializeField, Min(1), Tooltip("이번 런에서 진행할 스테이지 수. 보스 클리어 포탈 진입 시 다음 스테이지가 없으면 결과 화면으로 간다.")]
        private int totalStageCount = 1;

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

            StateMachine = new RunStateMachine();
            StateMachine.StateChanged += LogStateChange;
        }

        private void OnEnable()
        {
            if (dungeonRunBootstrap != null)
            {
                dungeonRunBootstrap.DungeonBuilt += HandleDungeonBuilt;
            }
            if (playerStateAggregator != null)
            {
                playerStateAggregator.StateChanged += HandlePlayerStateChanged;
            }
            if (playerDownController != null)
            {
                playerDownController.DefeatedByTimeout += HandlePlayerDefeatedDirect;
                playerDownController.DefeatedSolo += HandlePlayerDefeatedDirect;
            }
            // CL-113: RunResult 버튼 wiring. develop 의 단순 += CloseResulting 대신 우리 핸들러 채택 —
            // HandleRestartRequested 가 SceneManager.LoadScene 으로 씬 재로드까지 수행 (superset).
            if (runResultPanelView != null)
            {
                runResultPanelView.OnLobby += ReturnToTown;
                runResultPanelView.OnRestart += HandleRestartRequested;
            }
        }

        private void OnDisable()
        {
            if (dungeonRunBootstrap != null)
            {
                dungeonRunBootstrap.DungeonBuilt -= HandleDungeonBuilt;
            }
            if (playerStateAggregator != null)
            {
                playerStateAggregator.StateChanged -= HandlePlayerStateChanged;
            }
            if (playerDownController != null)
            {
                playerDownController.DefeatedByTimeout -= HandlePlayerDefeatedDirect;
                playerDownController.DefeatedSolo -= HandlePlayerDefeatedDirect;
            }
            if (runResultPanelView != null)
            {
                runResultPanelView.OnLobby -= ReturnToTown;
                runResultPanelView.OnRestart -= HandleRestartRequested;
            }
            UnsubscribeAllRoomControllers();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            if (autoStartOnAwake)
            {
                StartRun();
            }
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

            bool handled = HasNextStage ? AdvanceToNextStage() : CompleteRunFromBossPortal();
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
            if (!IsAuthority || townReturnInProgress)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(townSceneName))
            {
                Debug.LogWarning("[RunManager] Town scene name is empty.", this);
                return;
            }

            NetworkManager networkManager = NetworkManager.Singleton;
            bool networkSessionActive = networkManager != null && networkManager.IsListening;
            if (networkSessionActive && !networkManager.IsServer)
            {
                Debug.LogWarning("[RunManager] Town return must be requested on the server/host. Client request RPC is not wired yet.", this);
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(townSceneName))
            {
                Debug.LogWarning($"[RunManager] Town scene '{townSceneName}' is not available. Add it to Build Settings.", this);
                return;
            }

            townReturnInProgress = true;
            if (!TryCloseResulting())
            {
                CleanupRunResultingState();
            }
            Time.timeScale = 1f;

            if (networkSessionActive)
            {
                networkManager.SceneManager.LoadScene(townSceneName, LoadSceneMode.Single);
            }
            else
            {
                SceneManager.LoadScene(townSceneName, LoadSceneMode.Single);
            }

            Destroy(gameObject);
        }

        private bool TryCloseResulting()
        {
            if (!StateMachine.TryTransition(RunState.None))
            {
                return false;
            }

            CleanupRunResultingState();
            return true;
        }

        private void CleanupRunResultingState()
        {
            bossClearPortalReady = false;
            if (runResultPanelView != null)
            {
                runResultPanelView.Hide();
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
            // 이번 런 파편 영구 저장.
            memoryProgressTracker?.SaveRunShards();
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
            if (payload.Data.RoomType == StageRoomType.Combat && goldWallet != null)
            {
                Debug.Log("[RunManager] Combat clear reward gold +50.");
                goldWallet.Add(50);
            }
            // 룸 타입에 따른 파편 지급.
            memoryProgressTracker?.AddShardsForRoom(payload.Data.RoomType);
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

            StartCoroutine(DelayedTransitionToResulting());
            return true;
        }

        private void HandlePlayerStateChanged(KhiPlayerState prev, KhiPlayerState current)
        {
            // TODO(CL-014): 부활 deadline 처리. 현재는 *솔로 = Player Defeated 즉시 RunFailed* 임시 정책.
            // 멀티 도입 시 *파티 전원 다운 + 부활 deadline 만료* 로 변경.
            if (current != KhiPlayerState.Defeated)
            {
                return;
            }
            if (StateMachine.Current != RunState.InRun)
            {
                return;
            }
            if (StateMachine.TryTransition(RunState.RunFailed))
            {
                bossClearPortalReady = false;
                StartCoroutine(DelayedTransitionToResulting());
            }
        }

        // KhiPlayerStateAggregator 는 Player GameObject 가 Defeated 와 동일 프레임에 비활성화되어
        // Update 폴링이 StateChanged(Defeated) 를 발행하지 못한다.
        // KhiDownController 의 DefeatedByTimeout / DefeatedSolo 는 GameObject 비활성화 *직전* 에 발행되므로
        // RunFailed 트리거의 신뢰 경로. Aggregator 구독은 잔존시켜 비치명 경로(있을 시) 보호.
        private void HandlePlayerDefeatedDirect()
        {
            // TODO(CL-014): 부활 deadline 도입 시 즉시가 아닌 deadline 만료 후로 변경.
            if (StateMachine.Current != RunState.InRun)
            {
                return;
            }
            if (StateMachine.TryTransition(RunState.RunFailed))
            {
                bossClearPortalReady = false;
                StartCoroutine(DelayedTransitionToResulting());
            }
        }

        private IEnumerator DelayedTransitionToResulting()
        {
            yield return new WaitForSeconds(resultingDelaySeconds);
            if (!StateMachine.TryTransition(RunState.Resulting))
            {
                yield break;
            }
            ShowResultingUI();
        }

        private void ShowResultingUI()
        {
            if (runResultPanelView == null)
            {
                Debug.Log("[RunManager] Resulting state — no RunResultPanelView wired (stub).");
                return;
            }

            runResultPanelView.Show(BuildRunResultData());
            Debug.Log("[RunManager] Resulting state — RunResultPanelView shown.");
        }

        private void ResetRunResultTracking()
        {
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

        private void LogStateChange(RunState prev, RunState current)
        {
            Debug.Log($"[RunManager] {prev} -> {current}");
        }

        // CL-113: 결산 창의 Restart 버튼. CloseResulting → 현재 씬 재로드.
        // SceneManager.LoadScene 은 *현재 활성 씬* 의 buildIndex 를 사용 — 별도 마을 씬 도입 (CL-117) 까지 단순.
        private void HandleRestartRequested()
        {
            Debug.Log("[RunManager] Restart requested. Reloading current scene.");
            CloseResulting();
            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.buildIndex);
        }

        // CL-113: 결산 창의 Lobby 버튼. 마을 씬 도입 (CL-117) 까지 CloseResulting 만.
        private void HandleLobbyRequested()
        {
            Debug.Log("[RunManager] Lobby requested. (TODO: CL-117 마을 씬 로드. 현재는 CloseResulting 만.)");
            CloseResulting();
        }
    }
}
