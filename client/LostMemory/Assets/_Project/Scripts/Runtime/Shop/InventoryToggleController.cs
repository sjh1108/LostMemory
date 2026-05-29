using LostMemory.Networking.Player;
using LostMemory.Relics;
using LostMemory.Stage;
using LostMemory.TestKhi;
using LostMemory.UI.Status;
using UnityEngine;

namespace LostMemory.Shop
{
    /// <summary>
    /// CL-113 — 인벤토리 패널 단독 토글 컨트롤러.
    /// 키 입력 (default I) 으로 InventoryPanelView 를 열고 닫는다. Shop 열림 상태와 무관하게 동작 가능.
    /// Shop 이 자체적으로 InventoryPanel 을 동시에 여는 흐름은 ShopController 가 별도로 관리.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Shop/Inventory Toggle Controller")]
    public sealed class InventoryToggleController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private InventoryPanelView panel;
        [SerializeField] private PlayerRelicInventory playerRelicInventory;
        [Tooltip("골드 표시용. null 이어도 동작 (gold=0 으로 고정 표시).")]
        [SerializeField] private GoldWallet goldWallet;
        [Tooltip("인벤토리 패널 옆에 띄울 세트효과 패널. null 이면 표시 생략 (단독 테스트 씬 호환).")]
        [SerializeField] private SetEffectPanelView setEffectPanel;
        [Tooltip("setEffectPanel 데이터 소스. setEffectPanel 이 할당돼 있으면 함께 채워야 함.")]
        [SerializeField] private BuildManager buildManager;
        [Tooltip("인벤토리 패널 옆에 함께 띄울 플레이어 상태창. null 이면 statusPanelResourcePath 의 Resources prefab 을 자동 Instantiate.")]
        [SerializeField] private PlayerStatusPanelView statusPanel;
        [Tooltip("statusPanel 슬롯이 비어있을 때 Awake 에서 Resources.Load 로 가져올 경로 (Assets/_Project/Resources/ 기준, 확장자 제외). " +
                 "특정 씬에서 자동 생성 비활성하려면 빈 문자열로.")]
        [SerializeField] private string statusPanelResourcePath = "UI/PlayerStatusPanel";
        [Tooltip("CL-115 C: Shop 열림 중 I/ESC 입력을 무시하기 위한 우선 가드. null 이면 가드 없음 (단독 인벤토리 테스트 씬 호환).")]
        [SerializeField] private ShopController shopController;
        [Tooltip("CL-115: 보상 패널 떠있는 중 I/ESC 입력을 무시하기 위한 가드. RewardController.ShowReward 가 진입 시 본 패널을 강제 Close 도 함. null 이면 가드 없음.")]
        [SerializeField] private RewardController rewardController;
        [SerializeField] private KhiDownController downController;

        [Header("Input")]
        [SerializeField] private KeyCode toggleKey = KeyCode.I;

        [Header("Behavior")]
        [Tooltip("씬 시작 시 패널을 숨길지. 보통 true.")]
        [SerializeField] private bool startHidden = true;

        [Header("Debug")]
        [SerializeField] private bool logToggle = false;

        public bool IsOpen => panel != null && panel.gameObject.activeSelf;

        private void Awake()
        {
            if (panel != null) panel.OnCloseRequested += Close;

            // statusPanel 슬롯이 비어있으면 Resources 에서 자동 로드해 Canvas 자식으로 인스턴스화.
            // 17 개 던전 씬마다 수동 배치/와이어링 회피용. 씬 unload 시 인스턴스도 함께 destroy 되므로 누적 없음.
            EnsureStatusPanelInstantiated();

            if (startHidden)
            {
                if (panel != null)          panel.gameObject.SetActive(false);
                if (setEffectPanel != null) setEffectPanel.gameObject.SetActive(false);
                if (statusPanel != null)    statusPanel.gameObject.SetActive(false);
            }
        }

        private void EnsureStatusPanelInstantiated()
        {
            if (statusPanel != null) return;
            if (string.IsNullOrEmpty(statusPanelResourcePath)) return;

            PlayerStatusPanelView prefab = Resources.Load<PlayerStatusPanelView>(statusPanelResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning(
                    $"[InventoryToggleController] Resources.Load 실패: '{statusPanelResourcePath}'. " +
                    $"파일이 Assets/_Project/Resources/{statusPanelResourcePath}.prefab 에 있는지 확인.",
                    this);
                return;
            }

            // 본 controller GO 의 부모(=Canvas) 자식으로 spawn — 인벤토리/세트효과 패널과 동일 계층.
            // controller GO 자체에 자식으로 넣으면 100x100 marker rect 안에 끼어버릴 수 있음.
            Transform parentForPanel = transform.parent != null ? transform.parent : transform;
            statusPanel = Instantiate(prefab, parentForPanel);
            statusPanel.name = "PlayerStatusPanel (Auto)";
        }

        private void OnDestroy()
        {
            if (panel != null) panel.OnCloseRequested -= Close;
        }

        private void Update()
        {
            if (IsPlayerActionBlocked())
            {
                if (IsOpen)
                {
                    Close();
                }

                return;
            }
            // CL-115 C: Shop 열림 중에는 I / ESC 입력 무시 — ShopController 가 자체 Close 책임.
            if (shopController != null && shopController.IsOpen) return;
            // CL-115: 보상 패널 떠있는 중에도 I / ESC 무시 — RewardController 가 자체 Close 책임 (선택 후 자동 종료).
            if (rewardController != null && rewardController.IsShowing) return;

            if (Input.GetKeyDown(toggleKey))
            {
                Toggle();
            }
            // CL-115 D: 인벤토리 단독 열림 시 ESC → Close.
            else if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                if (logToggle) Debug.Log("[InventoryToggleController] ESC → Close.");
                Close();
            }
        }

        public void Toggle()
        {
            if (IsPlayerActionBlocked()) return;
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (IsPlayerActionBlocked()) return;
            // 멀티 안전: scene-placed InventoryToggleController 의 SerializeField 는 host PlayerRelicInventory 만
            // 가리키므로 guest 에선 null. LocalPlayer 기반 lazy resolve 로 본인 player 컴포넌트 잡기.
            EnsurePlayerSideRefsResolved();
            if (panel == null || playerRelicInventory == null)
            {
                Debug.LogError("[InventoryToggleController] panel 또는 playerRelicInventory 가 null. wiring 확인.", this);
                return;
            }
            int gold = goldWallet != null ? goldWallet.Current : 0;
            // CL-234 (A-1/A-2): 인벤토리 열린 동안 모든 게임플레이 입력 차단 (Space 대시 등 키보드 포함).
            // UIInputBlockerSource 가 prefab 에 부착되어 있다면 OnEnable 로도 잡히지만, prefab 변경이
            // 미반영된 환경 안전망으로 코드 흐름에서도 Acquire/Release.
            if (!_uiBlockerAcquired)
            {
                LostMemory.UI.UIInputBlocker.Acquire();
                _uiBlockerAcquired = true;
            }
            panel.gameObject.SetActive(true);
            panel.Init(playerRelicInventory, gold);

            if (setEffectPanel != null)
            {
                setEffectPanel.gameObject.SetActive(true);
                if (buildManager != null)
                {
                    setEffectPanel.Init(buildManager);
                }
                else
                {
                    Debug.LogWarning("[InventoryToggleController] setEffectPanel 은 할당됐는데 buildManager 가 null — 세트효과 패널이 비어 보일 수 있음.", this);
                }
            }

            // 상태창 패널 — PlayerStatusPanelPresenter 가 OnEnable 에서 자체 resolve/refresh.
            if (statusPanel != null)
            {
                statusPanel.gameObject.SetActive(true);
            }

            if (logToggle) Debug.Log($"[InventoryToggleController] Opened. gold={gold}");
        }

        public void Close()
        {
            if (panel != null) panel.gameObject.SetActive(false);
            if (setEffectPanel != null) setEffectPanel.gameObject.SetActive(false);
            if (statusPanel != null) statusPanel.gameObject.SetActive(false);
            // CL-234 (A-1/A-2): Open 에서 Acquire 한 카운터 해제.
            if (_uiBlockerAcquired)
            {
                LostMemory.UI.UIInputBlocker.Release();
                _uiBlockerAcquired = false;
            }
            if (logToggle) Debug.Log("[InventoryToggleController] Closed.");
        }

        private bool _uiBlockerAcquired;

        private void OnDisable()
        {
            // 컨트롤러가 비활성되면 (씬 전환 등) 카운터 잔재 방지.
            if (_uiBlockerAcquired)
            {
                LostMemory.UI.UIInputBlocker.Release();
                _uiBlockerAcquired = false;
            }
        }

        private bool IsPlayerActionBlocked()
        {
            if (downController == null && playerRelicInventory != null)
            {
                KhiPlayerActionGate.TryResolveDownController(playerRelicInventory, out downController);
            }

            return KhiPlayerActionGate.IsBlocked(downController);
        }

        /// <summary>
        /// player-side 컴포넌트(PlayerRelicInventory / GoldWallet / BuildManager) 를 LocalPlayer 기반으로 lazy resolve.
        /// scene-placed Canvas 자식의 SerializeField 는 host 측 PlayerObject 만 가리키므로 guest 에선 null.
        /// 멀티에서 본인 인벤토리만 정확히 잡도록 LocalPlayerResolver 위임.
        /// </summary>
        private void EnsurePlayerSideRefsResolved()
        {
            if (playerRelicInventory == null)
                playerRelicInventory = LocalPlayerResolver.GetComponentOnLocalPlayer<PlayerRelicInventory>();
            if (goldWallet == null)
                goldWallet = LocalPlayerResolver.GetComponentOnLocalPlayer<GoldWallet>();
            if (buildManager == null)
                buildManager = LocalPlayerResolver.GetComponentOnLocalPlayer<BuildManager>();
        }
    }
}
