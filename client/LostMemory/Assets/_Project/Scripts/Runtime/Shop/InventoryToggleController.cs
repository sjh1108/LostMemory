using LostMemory.Relics;
using LostMemory.Stage;
using LostMemory.TestKhi;
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
            if (startHidden)
            {
                if (panel != null)          panel.gameObject.SetActive(false);
                if (setEffectPanel != null) setEffectPanel.gameObject.SetActive(false);
            }
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
            if (panel == null || playerRelicInventory == null)
            {
                Debug.LogError("[InventoryToggleController] panel 또는 playerRelicInventory 가 null. wiring 확인.", this);
                return;
            }
            int gold = goldWallet != null ? goldWallet.Current : 0;
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

            if (logToggle) Debug.Log($"[InventoryToggleController] Opened. gold={gold}");
        }

        public void Close()
        {
            if (panel != null) panel.gameObject.SetActive(false);
            if (setEffectPanel != null) setEffectPanel.gameObject.SetActive(false);
            if (logToggle) Debug.Log("[InventoryToggleController] Closed.");
        }

        private bool IsPlayerActionBlocked()
        {
            if (downController == null && playerRelicInventory != null)
            {
                KhiPlayerActionGate.TryResolveDownController(playerRelicInventory, out downController);
            }

            return KhiPlayerActionGate.IsBlocked(downController);
        }
    }
}
