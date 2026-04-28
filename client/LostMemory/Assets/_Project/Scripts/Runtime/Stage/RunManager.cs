using System.Collections;
using System.Collections.Generic;
using LostMemory.TestKhi;
using LostMemory.UI;
using UnityEngine;

namespace LostMemory.Stage
{
    /// <summary>
    /// CL-048 런 매니저 — 런 단위 상태 머신 보유 + 외부 시스템 wiring.
    ///
    /// 책임:
    ///   1. <see cref="RunStateMachine"/> 생성 / 보유
    ///   2. <see cref="DungeonRunBootstrap.DungeonBuilt"/> 구독 → InRun 전이
    ///   3. <see cref="RoomEntryRuntimeController.RoomCleared"/> 구독 → 보스방 클리어 시 RunCleared 전이
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

        [Header("Behavior")]
        [SerializeField, Tooltip("Awake 후 자동으로 StartRun() 호출. 디버그 / 검증 시 편의용.")]
        private bool autoStartOnAwake = false;

        [SerializeField, Tooltip("RunCleared/RunFailed 도달 후 Resulting 까지의 지연 시간(초)")]
        private float resultingDelaySeconds = 2f;

        public RunStateMachine StateMachine { get; private set; }

        // 후속 네트워크 CL 이 한 줄만 바꾸면 호스트 권위 분기로 전환된다.
        public bool IsAuthority => true;

        private readonly HashSet<RoomEntryRuntimeController> _subscribedControllers = new HashSet<RoomEntryRuntimeController>();

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
            if (dungeonRunBootstrap == null)
            {
                Debug.LogWarning("[RunManager] dungeonRunBootstrap reference is null. Cannot build run.", this);
                return;
            }
            dungeonRunBootstrap.BuildRun();
        }

        /// <summary>
        /// 결과 화면 닫기 — Resulting → None.
        /// 외부 (RunResultPanelView 의 OnLobby / OnRestart 또는 디버그 키) 가 호출.
        /// </summary>
        public void CloseResulting()
        {
            if (!StateMachine.TryTransition(RunState.None))
            {
                return;
            }
            if (runResultPanelView != null)
            {
                runResultPanelView.Hide();
            }
            UnsubscribeAllRoomControllers();
        }

        private void HandleDungeonBuilt()
        {
            if (StateMachine.Current != RunState.Initializing)
            {
                return;
            }
            SubscribeAllRoomControllers();
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
            // 보스방 클리어 = 런 클리어
            if (payload.Data.RoomType == StageRoomType.Boss)
            {
                if (StateMachine.TryTransition(RunState.RunCleared))
                {
                    StartCoroutine(DelayedTransitionToResulting());
                }
            }
            // 일반방 클리어는 *다음 방 진입* 으로 자연 진행 (RoomEntryZone OnTriggerEnter2D 영역 — CL-105).
            // 본 CL 은 *런 단위 전이* 만 책임.
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
            // TODO: RunResultData 집계 → runResultPanelView.Show(data) 호출.
            // 본 CL 범위 = *상태 추적까지*. RunResultData 집계 / 데이터 wiring 은 후속 CL.
            if (runResultPanelView == null)
            {
                Debug.Log("[RunManager] Resulting state — no RunResultPanelView wired (stub).");
                return;
            }
            runResultPanelView.gameObject.SetActive(true);
            Debug.Log("[RunManager] Resulting state — RunResultPanelView activated (stub, no data binding).");
        }

        private void LogStateChange(RunState prev, RunState current)
        {
            Debug.Log($"[RunManager] {prev} -> {current}");
        }
    }
}
