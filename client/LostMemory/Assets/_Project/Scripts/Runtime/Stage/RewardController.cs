using System.Collections;
using System.Collections.Generic;
using LostMemory.Memory;
using LostMemory.Networking.Player;
using LostMemory.Relics;
using LostMemory.Rewards;
using LostMemory.Shop;
using LostMemory.TestKhi;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        [Tooltip("Phase B 진단: 멀티에서 guest 보상 패널 미표시 문제 추적. 안정화 후 false 권장.")]
        [SerializeField] private bool logRewardFlow = false;

        private float _savedTimeScale = 1f;

        private readonly Dictionary<RoomEntryRuntimeController, System.Action<RoomClearedPayload>> _subscribed
            = new Dictionary<RoomEntryRuntimeController, System.Action<RoomClearedPayload>>();
        private RoomEntryRuntimeController _pendingController;
        private bool _isShowingReward;

        // 기억 시스템 '시작 유물 +N' 용 체이닝 카운터.
        // 던전 진입 시 RunManager 가 ShowStartingRelicReward(N) 호출 → N번 연속 보상 패널.
        // > 0 이면 HandleRewardSelected 분기에서 OpenExits 대신 다음 패널 재호출.
        private int _startingRelicRemaining;

        /// <summary>CL-115: 보상 패널 표시 중 여부. InventoryToggleController 가 I 키 가드에 사용.</summary>
        public bool IsShowing => _isShowingReward;

        /// <summary>
        /// 시연 안전망 — Emergency 복구 시스템이 호출. _isShowingReward 가 어떤 이유로 stale 인 경우
        /// timeScale + aim + input 강제 복원. OnDisable 의 panic restore 와 동일 로직이지만 외부 진입점.
        /// </summary>
        public void PanicRestore()
        {
            if (!_isShowingReward) return;

            if (logRewardFlow) Debug.LogWarning("[RewardController] PanicRestore 호출 — _isShowingReward 강제 reset");
            Time.timeScale = _savedTimeScale > 0.001f ? _savedTimeScale : 1f;
            if (playerAim != null) playerAim.enabled = true;
            SetCombatInputsBlocked(false);
            _isShowingReward = false;
            if (rewardPanelView != null && rewardPanelView.gameObject.activeSelf)
            {
                rewardPanelView.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (rewardPanelView != null)
            {
                rewardPanelView.RewardSelected += HandleRewardSelected;
            }
            // [PhaseF fix] guest 측 RewardController 가 RoomCleared 이벤트를 못 받는 문제 fix.
            // RunManager.HandleDungeonBuilt 는 host 측만 호출되므로 SubscribeAllRoomControllers 도 host 만 호출됨.
            // → guest 의 RewardController 가 RoomEntryRuntimeController.RoomClearedBroadcast 를 구독 안 함.
            // 자체적으로 SceneManager.sceneLoaded 구독해서 양측 모두 던전 씬 진입 후 자동 구독.
            SceneManager.sceneLoaded += HandleSceneLoaded;
            // 이미 활성 씬에 RoomEntryRuntimeController 가 spawn 되어 있을 수도 있음 (씬 로드 후 OnEnable 인 케이스) → 즉시 1회 시도.
            StartCoroutine(SubscribeNextFrame());
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
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
        /// [PhaseF fix] 씬 로드 후 1 frame 뒤 SubscribeAllRoomControllers 호출.
        /// host 는 RunManager 가 별도로도 호출하므로 idempotent (SubscribeAllRoomControllers 진입부에서 Unsubscribe 후 재구독).
        /// guest 는 RunManager.HandleDungeonBuilt 가 호출 안 되므로 이게 유일한 구독 경로.
        /// </summary>
        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StartCoroutine(SubscribeNextFrame());
        }

        private IEnumerator SubscribeNextFrame()
        {
            // RoomEntryRuntimeController 가 OnNetworkSpawn 으로 완전 초기화되기 전에 FindObjectsOfType 가 empty 일 수 있음.
            // 1 frame 대기 → NGO scene sweep 후 보장.
            yield return null;
            SubscribeAllRoomControllers();
            if (logRewardFlow)
            {
                var nm = NetworkManager.Singleton;
                Debug.Log($"[RewardController] HandleSceneLoaded → SubscribeAllRoomControllers triggered. scene='{SceneManager.GetActiveScene().name}' localId={nm?.LocalClientId}", this);
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
                // A-3: RoomClearedBroadcast (4인 모두) 사용 — 보상 UI 가 각 클라에서 독립 발화.
                captured.RoomClearedBroadcast += handler;
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
                if (kvp.Key != null) kvp.Key.RoomClearedBroadcast -= kvp.Value;
            }
            _subscribed.Clear();
        }

        /// <summary>
        /// CL-110 fix: 각 controller 의 RoomCleared 핸들러 closure 가 자기 자신을 인자로 전달.
        /// 같은 RoomId 의 모듈 인스턴스가 여러 개일 때 *실제 발화한* controller 를 정확히 식별.
        /// </summary>
        private void HandleRoomClearedFromController(RoomEntryRuntimeController source, RoomClearedPayload payload)
        {
            // Phase B 진단: NGO 컨텍스트 (host/guest 구분) + payload 내용 로깅 — guest 측 패널 미표시 원인 추적.
            if (logRewardFlow)
            {
                var nm = NetworkManager.Singleton;
                string ngoCtx = nm == null ? "NM=null"
                    : $"NM(host={nm.IsHost},server={nm.IsServer},client={nm.IsClient},listening={nm.IsListening},localId={nm.LocalClientId})";
                Debug.Log($"[RewardController] RoomCleared event arrived. source='{(source != null ? source.name : "null")}' " +
                          $"roomId={payload.RoomId} dataNull={payload.Data == null} " +
                          $"roomType={(payload.Data != null ? payload.Data.RoomType.ToString() : "?")} " +
                          $"hasSpawnedEnemies={payload.HasSpawnedEnemies} " +
                          $"isShowingReward={_isShowingReward} " +
                          $"{ngoCtx}", this);
            }

            // [DiagPhaseF] Log 4 — 각 STOP 가드에서 localId 와 가드 사유 명시. host vs guest 어느 측에서 어느 가드가 막는지 분석.
            var nmLog4 = NetworkManager.Singleton;
            if (payload.Data == null)
            {
                Debug.LogError($"[RewardController] STOP — payload.Data NULL. roomId={payload.RoomId} " +
                               $"source='{(source != null ? source.name : "null")}' localId={nmLog4?.LocalClientId}", this);
                return;
            }
            // Boss/Shop/Event 등은 보상 X (CL-110 결정 #2).
            if (payload.Data.RoomType != StageRoomType.Combat)
            {
                Debug.Log($"[RewardController] STOP — non-Combat room {payload.RoomId} type={payload.Data.RoomType} localId={nmLog4?.LocalClientId}");
                return;
            }

            if (!payload.HasSpawnedEnemies)
            {
                Debug.Log($"[RewardController] STOP — no spawned enemies. {payload.RoomId} localId={nmLog4?.LocalClientId}");
                return;
            }

            // 동시 다중 클리어 보호 (cl110_plan 결정 #6 / 위험 #5).
            if (_isShowingReward)
            {
                Debug.LogWarning($"[RewardController] STOP — already showing reward, ignoring room {payload.RoomId} localId={nmLog4?.LocalClientId}", this);
                return;
            }
            Debug.Log($"[RewardController] PASS all gates — scheduling DelayedShowReward in {rewardShowDelay}s. roomId={payload.RoomId} localId={nmLog4?.LocalClientId}");

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

            // [DiagPhaseF] Log 5 — ShowReward 진입 시 panel/inventory + Canvas 활성 상태까지 진단. SetActive(true) 직전 hierarchy 점검.
            var nmLog5 = NetworkManager.Singleton;
            string panelActive = rewardPanelView != null
                ? $"goActive={rewardPanelView.gameObject.activeSelf} hier={rewardPanelView.gameObject.activeInHierarchy} parent={(rewardPanelView.transform.parent != null ? rewardPanelView.transform.parent.name : "ROOT")}"
                : "panel=null";
            string canvasState = "no-canvas";
            if (rewardPanelView != null)
            {
                var canvas = rewardPanelView.GetComponentInParent<Canvas>(true);
                if (canvas != null) canvasState = $"canvas='{canvas.name}' enabled={canvas.enabled} hier={canvas.gameObject.activeInHierarchy}";
            }
            Debug.Log($"[RewardController] ShowReward entered. localId={nmLog5?.LocalClientId} " +
                      $"panelResolved={rewardPanelView != null} inventoryResolved={playerRelicInventory != null} " +
                      $"inventoryHost='{(playerRelicInventory != null ? playerRelicInventory.gameObject.name : "null")}' " +
                      $"localChar='{(LocalPlayerResolver.LocalCharacter != null ? LocalPlayerResolver.LocalCharacter.gameObject.name : "null")}' " +
                      $"panel({panelActive}) {canvasState}", this);

            if (rewardPanelView == null || playerRelicInventory == null)
            {
                Debug.LogError($"[RewardController] ShowReward STOP — rewardPanelView or inventory null. localId={nmLog5?.LocalClientId}", this);
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
            // 5스택 (T2, idx=2) 선택지 +1 / 7스택 (T3, idx=3) forceLegendary.
            // BuildSet_행운 tier 인덱스: T0(1스택)=LuckPoints, T1(3)=LuckSlotExpand, T2(5)=보상 선택지 +1, T3(7)=LuckLegendaryGuarantee
            // TODO(추후 복귀): 원래 의도는 T2 = 5택 2픽 다중 픽 모드. 임시로 4택 1픽으로 변경 — _cards 배열 5장 미만 환경 대응.
            int luckCount = buildManager != null ? buildManager.GetTagCount(RelicTag.Luck) : 0;
            int luckTier = buildManager != null ? buildManager.GetActiveTier(RelicTag.Luck) : -1;
            bool forceLegendary = luckTier >= 3;     // T3+ = 7스택+
            bool luckSlotExpand = luckTier >= 2;     // T2+ = 5스택+ → 선택지 +1
            int picksAllowed    = 1;                 // (임시) 항상 1픽. 추후 다중 픽 복귀 시 luckSlotExpand ? 2 : 1.
            int count           = luckSlotExpand ? 4 : 3;

            // 기억 시스템 RewardRarityBoost — 보상 등급 상향 확률 반영 (1-4, 3-2 조각).
            float rarityBoost = MemoryMetaService.Load().BonusRewardRarityPercent;

            rewardPanelView.Show(playerRelicInventory, count, picksAllowed, luckCount, forceLegendary, relicOnly: false, rarityBoostPercent: rarityBoost);
            if (logRewardFlow) Debug.Log($"[RewardController] Reward panel shown. timeScale=0, aim locked. luck={luckCount} tier={luckTier} count={count} picks={picksAllowed} forceLegendary={forceLegendary} rarityBoost={rarityBoost:F2}");
        }

        /// <summary>
        /// 기억 시스템 '시작 유물 +N' 보상. 던전 진입 시 RunManager 가 호출.
        /// totalCount 번 만큼 보상 패널을 연속으로 띄움 (매번 3택 1픽, 유물만).
        /// 매 픽 후 HandleRewardSelected 가 카운터를 감소시키고 다음 패널 호출.
        /// _pendingController == null 흐름이라 마지막 픽 후 OpenExits 호출되지 않음 (정상).
        /// </summary>
        public void ShowStartingRelicReward(int totalCount)
        {
            if (totalCount <= 0) return;

            _startingRelicRemaining = totalCount;
            if (logRewardFlow)
                Debug.Log($"[RewardController] StartingRelicReward begin — totalCount={totalCount}");

            ShowNextStartingRelicPanel();
        }

        private void ShowNextStartingRelicPanel()
        {
            ResolveRewardPanelView();
            ResolvePlayerRelicInventory();

            if (rewardPanelView == null || playerRelicInventory == null)
            {
                Debug.LogError("[RewardController] StartingRelicReward — rewardPanelView/playerRelicInventory null. wiring 확인.", this);
                _startingRelicRemaining = 0;
                return;
            }

            // 첫 패널이면 가드/timeScale 세팅. 이미 켜져있으면 idempotent.
            if (!_isShowingReward)
            {
                _isShowingReward = true;

                if (inventoryToggle != null && inventoryToggle.IsOpen)
                {
                    if (logRewardFlow) Debug.Log("[RewardController] Force-closing InventoryToggle for starting-relic reward focus.");
                    inventoryToggle.Close();
                }

                _savedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                if (playerAim != null) playerAim.enabled = false;
                SetCombatInputsBlocked(true);
            }

            // 시작 유물 보상은 행운 무관 — 매 런 동일하게 3택 1픽, 유물만.
            rewardPanelView.Show(
                playerRelicInventory,
                count: 3,
                picksAllowed: 1,
                luckPoints: 0,
                forceLegendary: false,
                relicOnly: true);

            if (logRewardFlow)
                Debug.Log($"[RewardController] StartingRelicReward panel shown. remaining={_startingRelicRemaining}");
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

            // 기억 시스템 '시작 유물 +N' 체이닝 — 남은 횟수가 있으면 다음 패널 재호출, 복원 보류.
            if (_startingRelicRemaining > 0)
            {
                _startingRelicRemaining--;
                if (logRewardFlow)
                    Debug.Log($"[RewardController] StartingRelic picked: {selected?.DisplayName}. remaining={_startingRelicRemaining}");

                if (_startingRelicRemaining > 0)
                {
                    // 다음 패널 — _isShowingReward / timeScale 유지한 채 재호출.
                    ShowNextStartingRelicPanel();
                    return;
                }
                // 마지막 픽 — 가드/timeScale 복원으로 fall-through.
                if (logRewardFlow) Debug.Log("[RewardController] StartingRelicReward chain complete.");
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
                if (logRewardFlow) Debug.Log("[RewardController] _pendingController is null; OpenExits skip. (정상: 외부 ShowReward / StartingRelic 흐름)");
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

        /// <summary>
        /// 멀티 환경에서 host & guest 두 PlayerObject 가 동시에 존재 → 각자의 PlayerRelicInventory 도 두 개.
        /// FindAnyObjectByType 은 어느 인스턴스를 잡을지 비결정적 → guest 측에서 host 인벤토리를 잡으면
        /// reward 패널이 host inventory 를 가리키게 되어 게스트 화면에 표시가 깨짐.
        ///
        /// 우선순위:
        ///   1. LocalPlayerResolver.LocalCharacter 의 자식에서 PlayerRelicInventory — 본인 인벤토리 보장.
        ///   2. fallback — FindAnyObjectByType (싱글환경 / LocalPlayerResolver 미초기화 시).
        /// </summary>
        private PlayerRelicInventory ResolvePlayerRelicInventory()
        {
            if (playerRelicInventory != null) return playerRelicInventory;

            // 1) 로컬 플레이어 우선 — 멀티에서 host/guest 본인 인벤토리만 잡음.
            var localCharacter = LocalPlayerResolver.LocalCharacter;
            if (localCharacter != null)
            {
                playerRelicInventory = localCharacter.GetComponentInChildren<PlayerRelicInventory>(includeInactive: true);
                if (playerRelicInventory == null)
                {
                    // 같은 GameObject 가 아니라 root 위쪽일 가능성 — 위로도 탐색.
                    playerRelicInventory = localCharacter.GetComponentInParent<PlayerRelicInventory>();
                }
                if (logRewardFlow && playerRelicInventory != null)
                {
                    Debug.Log($"[RewardController] ResolvePlayerRelicInventory via LocalPlayerResolver → '{playerRelicInventory.gameObject.name}'", this);
                }
            }

            // 2) fallback — LocalPlayerResolver 가 아직 초기화 안 됐거나 싱글환경.
            if (playerRelicInventory == null)
            {
                playerRelicInventory = FindAnyObjectByType<PlayerRelicInventory>();
                if (logRewardFlow && playerRelicInventory != null)
                {
                    Debug.LogWarning($"[RewardController] ResolvePlayerRelicInventory fallback (LocalPlayer null) → '{playerRelicInventory.gameObject.name}'", this);
                }
            }

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
