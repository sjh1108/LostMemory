using LostMemory.Relics;
using LostMemory.Stage;
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
            if (panel != null && startHidden)
            {
                panel.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                Toggle();
            }
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (panel == null || playerRelicInventory == null)
            {
                Debug.LogError("[InventoryToggleController] panel 또는 playerRelicInventory 가 null. wiring 확인.", this);
                return;
            }
            int gold = goldWallet != null ? goldWallet.Current : 0;
            panel.gameObject.SetActive(true);
            panel.Init(playerRelicInventory, gold);
            if (logToggle) Debug.Log($"[InventoryToggleController] Opened. gold={gold}");
        }

        public void Close()
        {
            if (panel == null) return;
            panel.gameObject.SetActive(false);
            if (logToggle) Debug.Log("[InventoryToggleController] Closed.");
        }
    }
}
