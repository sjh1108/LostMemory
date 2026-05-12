using System.Collections;
using System.Collections.Generic;
using LostMemory.Relics;
using LostMemory.Rewards;
using LostMemory.Shop;
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
        [Tooltip("CL-110: 보상 패널 떠있는 동안 마우스 조준 (칼 따라가기) 차단용. Player GameObject 의 KhiPlayerAim. (참고: KhiPlayerAim 자체엔 Update 가 없어 enabled 토글 효과 없음 — 실제 칼 회전 차단은 playerWeaponPresenter 슬롯)")]
        [SerializeField] private KhiPlayerAim playerAim;
        [Tooltip("보상 패널 표시 동안 칼이 마우스를 따라 회전하지 않도록 차단. KhiWeaponPresenter.Update 가 매 프레임 GetAimDirection 으로 칼을 갱신하므로 이 컴포넌트 enabled 토글이 실제 차단점.")]
        [SerializeField] private KhiWeaponPresenter playerWeaponPresenter;
        [Tooltip("보상 패널 표시 동안 공격 input 차단. ExternalBlock = true 로 토글.")]
        [SerializeField] private KhiMeleeComboController playerMeleeCombo;
        [Tooltip("보상 패널 표시 동안 대쉬 input 차단. PermitAbility(false) 로 토글.")]
        [SerializeField] private KhiDashController playerDash;
        [Tooltip("보상 패널 표시 동안 패링 input 차단. ExternalBlock = true 로 토글.")]
        [SerializeField] private KhiParryController playerParry;
        [Tooltip("CL-115: 보상 패널 떠오를 때 인벤토리 패널이 열려있으면 강제로 닫기 위함. null 허용 — 단독 씬 호환. 가드는 InventoryToggleController.Update 가 IsShowing 을 직접 체크하는 방식과 짝.")]
        [SerializeField] private InventoryToggleController inventoryToggle;

        [Tooltip("CL-146: BuildManager — 행운 set tier 조회 (LuckPoints 카운트 / 5스택 다중픽 / 7스택 forceLegendary).")]
        [SerializeField] private BuildManager buildManager;

        [Header("Timing")]
        [SerializeField, Min(0f), Tooltip("방 클리어 후 보상 패널 표시까지 대기 (초). 플레이어 공격 모션 도중 패널 등장 방지. WaitForSecondsRealtime 사용 → timeScale 영향 없음.")]
        private float rewardShowDelay = 0.5f;

        [Header("Debug")]
        [SerializeField] private bool logRewardFlow = false;

        private float _savedTimeScale = 1f;

        private readonly Dictionary<RoomEntryRuntimeController, System.Action<RoomClearedPayload>> _subscribed
            = new Dictionary<RoomEntryRuntimeController, System.Action<RoomClearedPayload>>();
        private RoomEntryRuntimeController _pendingController;
        private bool _isShowingReward;

        /// <summary>CL-115: 보상 패널 표시 중 여부. InventoryToggleController 가 I 키 가드에 사용.</summary>
        public bool IsShowing => _isShowingReward;

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
                SetCombatInputsBlocked(false);
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
            // 가드 선점 — delay 동안 다른 방 클리어가 끼어들어 reward 가 큐잉되지 않도록 _isShowingReward 즉시 true.
            _isShowingReward = true;
            if (logRewardFlow) Debug.Log($"[RewardController] Pending controller set: '{_pendingController.name}' (RoomId={_pendingController.RoomId}). Showing reward in {rewardShowDelay}s.");

            StartCoroutine(DelayedShowReward());
        }

        // 보상 패널 표시 직전 짧은 지연 — 플레이어 공격 모션이 끝나기 전 패널이 등장해 입력이 끊기는 UX 방지.
        // WaitForSecondsRealtime 으로 timeScale 무관 (ShowReward 가 timeScale=0 만들기 전이라 이론상 영향 없으나 안전 차원).
        private IEnumerator DelayedShowReward()
        {
            if (rewardShowDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(rewardShowDelay);
            }
            ShowReward();
        }

        /// <summary>
        /// CL-110 정정 3: public — 미래 외부 트리거 (보스 처치 후 마지막 보상 / Shop / 이벤트 / 재능 보상 등) 가
        /// 직접 호출 가능. 외부 트리거 시 _pendingController == null 이면 HandleRewardSelected 의 OpenExits 가 skip.
        /// </summary>
        public void ShowReward()
        {
            // 씬 전환 시 RewardController 가 DontDestroyOnLoad GameObject 에 부착되어 따라오면
            // rewardPanelView / playerRelicInventory 인스펙터 reference 가 씬 로컬이라 stale 이 됨.
            // 매번 lazy resolve 로 새 씬의 객체를 자동 재 wiring.
            ResolveRewardPanelView();
            ResolvePlayerRelicInventory();

            if (rewardPanelView == null || playerRelicInventory == null)
            {
                Debug.LogError("[RewardController] rewardPanelView 또는 playerRelicInventory 가 null. wiring 확인.", this);
                return;
            }
            // 외부 직접 호출 (HandleRoomClearedFromController 미경유) 시에도 가드가 켜지도록 idempotent.
            _isShowingReward = true;

            // CL-115: 보상 패널 진입 시 인벤토리 단독 토글 패널이 열려있으면 강제 닫기 — 화면 중첩 방지.
            // InventoryToggleController.Update 가드가 IsShowing 을 체크하므로 *재오픈도 차단됨*.
            if (inventoryToggle != null && inventoryToggle.IsOpen)
            {
                if (logRewardFlow) Debug.Log("[RewardController] Force-closing InventoryToggle for reward focus.");
                inventoryToggle.Close();
            }

            // CL-110: 보상 패널 떠있는 동안 게임 시간 정지 + 마우스 조준 차단.
            // timeScale=0 으로 적 AI / Player 이동 / 코루틴 deltaTime stop.
            // playerAim.enabled=false 로 Update 가 안 돌아 *마지막 frame 의 조준 방향* 그대로 freeze.
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            if (playerAim != null) playerAim.enabled = false;
            // 공격 / 대쉬 / 패링 input 은 timeScale 무관하게 Update 로 읽혀 새 액션이 발화될 수 있음 → 명시적 차단.
            SetCombatInputsBlocked(true);

            // CL-146: 행운 set tier 조회 — 1스택+ 가중치 / 3스택 슬롯 (SetEffectApplicator 처리, 여기선 무관) /
            // 5스택 (T2, idx=2) 다중픽 / 7스택 (T3, idx=3) forceLegendary.
            // BuildSet_행운 tier 인덱스: T0(1스택)=LuckPoints, T1(3)=LuckSlotExpand, T2(5)=다중픽, T3(7)=LuckLegendaryGuarantee
            int luckCount = buildManager != null ? buildManager.GetTagCount(RelicTag.Luck) : 0;
            int luckTier = buildManager != null ? buildManager.GetActiveTier(RelicTag.Luck) : -1;
            bool forceLegendary = luckTier >= 3;     // T3+ = 7스택+
            bool picksDouble    = luckTier >= 2;     // T2+ = 5스택+
            int picksAllowed    = picksDouble ? 2 : 1;
            int count           = picksDouble ? 5 : 3;

            rewardPanelView.Show(playerRelicInventory, count, picksAllowed, luckCount, forceLegendary);
            if (logRewardFlow) Debug.Log($"[RewardController] Reward panel shown. timeScale=0, aim locked. luck={luckCount} tier={luckTier} count={count} picks={picksAllowed} forceLegendary={forceLegendary}");
        }

        private void HandleRewardSelected(RelicData selected)
        {
            // CL-146: 다중 픽 모드 (행운 5스택+) 에서 패널이 아직 열려있으면 — 추가 선택 대기. 복원 X.
            // RewardPanelView 가 picksAllowed 도달 시 gameObject.SetActive(false) 호출 → activeSelf=false.
            if (rewardPanelView != null && rewardPanelView.gameObject.activeSelf)
            {
                if (logRewardFlow) Debug.Log($"[RewardController] Reward picked: {selected?.DisplayName} — 다중 픽 진행 중, 패널 유지.");
                return;
            }

            if (logRewardFlow) Debug.Log($"[RewardController] Reward selected: {selected?.DisplayName}. Restoring timeScale + aim.");
            _isShowingReward = false;

            // CL-110: 보상 선택 후 게임 시간 + 조준 복원.
            Time.timeScale = _savedTimeScale;
            if (playerAim != null) playerAim.enabled = true;
            SetCombatInputsBlocked(false);

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

        /// <summary>
        /// 씬 전환 시 stale 이 된 rewardPanelView 참조를 활성 씬의 새 객체로 갈아끼움.
        /// RewardSelected 구독도 새 객체로 재등록.
        /// </summary>
        private RewardPanelView ResolveRewardPanelView()
        {
            if (rewardPanelView != null) return rewardPanelView;

            RewardPanelView[] views = FindObjectsByType<RewardPanelView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (views.Length == 0) return null;

            UnityEngine.SceneManagement.Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            RewardPanelView found = null;
            for (int i = 0; i < views.Length; i++)
            {
                if (views[i] != null && views[i].gameObject.scene == activeScene)
                {
                    found = views[i];
                    break;
                }
            }
            if (found == null) found = views[0];

            rewardPanelView = found;
            rewardPanelView.RewardSelected += HandleRewardSelected;
            return rewardPanelView;
        }

        private PlayerRelicInventory ResolvePlayerRelicInventory()
        {
            if (playerRelicInventory != null) return playerRelicInventory;
            playerRelicInventory = FindAnyObjectByType<PlayerRelicInventory>();
            return playerRelicInventory;
        }

        // 보상 패널 동안 공격 / 대쉬 / 패링 input + 칼 회전 일괄 토글. timeScale=0 만으론 Update 기반 동작이 막히지 않아 별도 차단.
        private void SetCombatInputsBlocked(bool block)
        {
            if (playerMeleeCombo != null) playerMeleeCombo.ExternalBlock = block;
            if (playerDash != null) playerDash.PermitAbility(!block);
            if (playerParry != null) playerParry.ExternalBlock = block;
            // 칼이 마우스 따라가는 Update 차단 — enabled=false 면 Update 가 안 돌아 마지막 프레임 위치/회전 그대로 freeze.
            if (playerWeaponPresenter != null) playerWeaponPresenter.enabled = !block;
        }
    }
}
