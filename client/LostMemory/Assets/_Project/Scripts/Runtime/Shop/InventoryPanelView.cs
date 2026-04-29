using LostMemory.Relics;
using TMPro;
using UnityEngine;

namespace LostMemory.Shop
{
    /// <summary>
    /// 상점 오른쪽 패널 — 플레이어 유물 인벤토리 슬롯 그리드와 소지 골드를 표시한다.
    /// ShopPanelView의 OnItemPurchased 이벤트를 받아 Refresh()를 호출한다.
    /// </summary>
    public class InventoryPanelView : MonoBehaviour
    {
        [Header("슬롯 (Inspector에서 순서대로 연결)")]
        [SerializeField] private InventorySlotView[] _slots;

        [Header("소지 골드")]
        [SerializeField] private TextMeshProUGUI _goldText;

        private void Reset()
        {
            _slots    = GetComponentsInChildren<InventorySlotView>();
            _goldText = GetComponentInChildren<TextMeshProUGUI>();
        }

        /// <summary>현재 인벤토리와 골드를 화면에 반영한다.</summary>
        public void Refresh(PlayerRelicInventory inventory, int gold)
        {
            var relics = inventory.OwnedRelics;

            for (int i = 0; i < _slots.Length; i++)
            {
                if (i < relics.Count)
                    _slots[i].SetRelic(relics[i]);
                else
                    _slots[i].Clear();
            }

            if (_goldText != null)
                _goldText.text = gold.ToString("N0");
        }
    }
}
