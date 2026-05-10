using System;
using LostMemory.Relics;
using UnityEngine;

namespace LostMemory.Shop
{
    /// <summary>
    /// 단축키바 전체를 담당하는 뷰 컴포넌트.
    /// Init()으로 초기화하고, Refresh()로 화면을 갱신한다.
    /// 슬롯 간 드래그 앤 드롭으로 소모품 위치를 교환할 수 있다.
    /// </summary>
    public class ShortcutBarView : MonoBehaviour
    {
        [SerializeField] private ConsumableSlotView[] _slots;

        private PlayerConsumableInventory _inventory;

        private void Reset() => _slots = GetComponentsInChildren<ConsumableSlotView>();

        /// <summary>최초 1회 초기화 후 화면 갱신</summary>
        public void Init(PlayerConsumableInventory inventory)
        {
            UnsubscribeSlots();
            _inventory = inventory;

            SubscribeSlots();

            Refresh();
        }

        private void OnDestroy()
        {
            UnsubscribeSlots();
        }

        private void SubscribeSlots()
        {
            foreach (var slot in _slots)
            {
                if (slot != null)
                {
                    slot.OnSlotDropReceived += HandleSlotDrop;
                }
            }
        }

        private void UnsubscribeSlots()
        {
            foreach (var slot in _slots)
            {
                if (slot != null)
                {
                    slot.OnSlotDropReceived -= HandleSlotDrop;
                }
            }
        }

        /// <summary>현재 인벤토리 상태를 화면에 반영한다.</summary>
        public void Refresh()
        {
            if (_inventory == null) return;

            var consumables = _inventory.Slots;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (i < consumables.Count && consumables[i] != null)
                    _slots[i].SetConsumable(consumables[i], i);
                else
                    _slots[i].Clear(i);
            }
        }

        // ── 드롭 처리 ─────────────────────────────────────────────

        private void HandleSlotDrop(ConsumableSlotView from, ConsumableSlotView to)
        {
            int fromIndex = Array.IndexOf(_slots, from);
            int toIndex   = Array.IndexOf(_slots, to);
            if (fromIndex < 0 || toIndex < 0) return;

            _inventory.Swap(fromIndex, toIndex);
            Refresh();
        }
    }
}
