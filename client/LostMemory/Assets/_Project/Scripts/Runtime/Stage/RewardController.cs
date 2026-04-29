using System.Collections.Generic;
using LostMemory.Relics;
using LostMemory.Rewards;
using LostMemory.TestKhi;
using UnityEngine;

namespace LostMemory.Stage
{
    /// <summary>
    /// CL-110 — 방 클리어 → 보상 3택 → 선택 → 문 열림 흐름의 오케스트레이터.
    /// Combat 타입 방만 처리 (Boss 는 RunManager 가 RunCleared 로 직행).
    /// 외부 (보스 처치 후 마지막 보상 / Shop / 이벤트 등) 가 ShowReward() 직접 호출 가능 — 미래 확장.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Stage/Reward Controller")]
    public sealed class RewardController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private RewardPanelView rewardPanelView;
        [SerializeField] private PlayerRelicInventory playerRelicInventory;
        [Tooltip("CL-110: 보상 패널 떠있는 동안 마우스 조준 (칼 따라가기) 차단용. Player GameObject 의 KhiPlayerAim.")]
        [SerializeField] private KhiPlayerAim playerAim;

        [Header("Debug")]
        [SerializeField] private bool logRewardFlow = false;

        private float _savedTimeScale = 1f;

        private readonly Dictionary<RoomEntryRuntimeController, System.Action<RoomClearedPayload>> _subscribed
            = new Dictionary<RoomEntryRuntimeController, System.Action<RoomClearedPayload>>();
        private RoomEntryRuntimeController _pendingController;
        private bool _isShowingReward;

        private void OnEnable()
        {
            if (rewardPanelView != null)
            {
                rewardPanelView.RewardSelected += HandleRewardSelected;
            }
        }

        private void OnDisable()
        {
            UnsubscribeAllRoomControllers();
            if (rewardPanelView != null)
            {
                rewardPanelView.RewardSelected -= HandleRewardSelected;
            }
            // CL-110: panic restore — 보상 떠있는 채 disable 시 timeScale=0 잠금 방지.
            if (_isShowingReward)
            {
                Time.timeScale = _savedTimeScale;
                if (playerAim != null) playerAim.enabled = true;
                _isShowingReward = false;
            }
        }

        /// <summary>
        /// RunManager.HandleDungeonBuilt 끝에 호출. 모든 spawn 된 RoomEntryRuntimeController 를 RoomCleared 구독.
        /// CL-110 fix: closure 로 sourceController 캡처 — 같은 RoomId 의 모듈 인스턴스가 여러 개일 때
        /// (DungeonArchitect 가 같은 prefab 을 여러 번 spawn) RoomId 매칭만으로는 *플레이어가 실제로 들어간*
        /// controller 를 못 찾는 버그 해결. 각 controller 마다 자기 자신을 캡처한 핸들러 1 개 보유.
        /// </summary>
        public void SubscribeAllRoomControllers()
        {
            UnsubscribeAllRoomControllers();
            RoomEntryRuntimeController[] controllers = FindObjectsOfType<RoomEntryRuntimeController>();
            for (int i = 0; i < controllers.Length; i++)
            {
                RoomEntryRuntimeController c = controllers[i];
                if (c == null) continue;
                RoomEntryRuntimeController captured = c;
                System.Action<RoomClearedPayload> handler = payload => HandleRoomClearedFromController(captured, payload);
                captured.RoomCleared += handler;
                _subscribed[captured] = handler;
            }
            if (logRewardFlow)
            {
                Debug.Log($"[RewardController] Subscribed to {_subscribed.Count} room controllers.");
            }
        }

        public void UnsubscribeAllRoomControllers()
        {
            foreach (var kvp in _subscribed)
            {
                if (kvp.Key != null) kvp.Key.RoomCleared -= kvp.Value;
            }
            _subscribed.Clear();
        }

        /// <summary>
        /// CL-110 fix: 각 controller 의 RoomCleared 핸들러 closure 가 자기 자신을 인자로 전달.
        /// 같은 RoomId 의 모듈 인스턴스가 여러 개일 때 *실제 발화한* controller 를 정확히 식별.
        /// </summary>
        private void HandleRoomClearedFromController(RoomEntryRuntimeController source, RoomClearedPayload payload)
        {
            if (payload.Data == null)
            {
                Debug.LogWarning("[RewardController] RoomCleared payload has null RoomData; ignoring.", this);
                return;
            }
            // Boss/Shop/Event 등은 보상 X (CL-110 결정 #2).
            if (payload.Data.RoomType != StageRoomType.Combat)
            {
                if (logRewardFlow) Debug.Log($"[RewardController] Skip non-Combat room: {payload.RoomId} type={payload.Data.RoomType}");
                return;
            }

            // 동시 다중 클리어 보호 (cl110_plan 결정 #6 / 위험 #5).
            if (_isShowingReward)
            {
                if (logRewardFlow) Debug.LogWarning($"[RewardController] Already showing reward; ignoring room {payload.RoomId}.");
                return;
            }

            _pendingController = source;
            if (logRewardFlow) Debug.Log($"[RewardController] Pending controller set: '{_pendingController.name}' (RoomId={_pendingController.RoomId}).");

            ShowReward();
        }

        /// <summary>
        /// CL-110 정정 3: public — 미래 외부 트리거 (보스 처치 후 마지막 보상 / Shop / 이벤트 / 재능 보상 등) 가
        /// 직접 호출 가능. 외부 트리거 시 _pendingController == null 이면 HandleRewardSelected 의 OpenExits 가 skip.
        /// </summary>
        public void ShowReward()
        {
            if (rewardPanelView == null || playerRelicInventory == null)
            {
                Debug.LogError("[RewardController] rewardPanelView 또는 playerRelicInventory 가 null. wiring 확인.", this);
                return;
            }
            _isShowingReward = true;

            // CL-110: 보상 패널 떠있는 동안 게임 시간 정지 + 마우스 조준 차단.
            // timeScale=0 으로 적 AI / Player 이동 / 코루틴 deltaTime stop.
            // playerAim.enabled=false 로 Update 가 안 돌아 *마지막 frame 의 조준 방향* 그대로 freeze.
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            if (playerAim != null) playerAim.enabled = false;

            rewardPanelView.Show(playerRelicInventory);
            if (logRewardFlow) Debug.Log("[RewardController] Reward panel shown. timeScale=0, aim locked.");
        }

        private void HandleRewardSelected(RelicData selected)
        {
            if (logRewardFlow) Debug.Log($"[RewardController] Reward selected: {selected?.DisplayName}. Restoring timeScale + aim.");
            _isShowingReward = false;

            // CL-110: 보상 선택 후 게임 시간 + 조준 복원.
            Time.timeScale = _savedTimeScale;
            if (playerAim != null) playerAim.enabled = true;

            if (_pendingController != null)
            {
                if (logRewardFlow) Debug.Log($"[RewardController] Calling OpenExits on '{_pendingController.name}' (RoomId={_pendingController.RoomId}).");
                _pendingController.OpenExits();
                _pendingController = null;
            }
            else
            {
                Debug.LogWarning("[RewardController] _pendingController is null; cannot open exits. (정상: 외부 ShowReward 호출 흐름)");
            }
            // _pendingController == null 이면 RoomCleared 경유 안 한 외부 트리거 (정상).
        }
    }
}
