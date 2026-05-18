using System;
using LostMemory.Relics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Shop
{
    /// <summary>
    /// 상점 오른쪽 패널 — 플레이어 유물 인벤토리 슬롯 그리드와 소지 골드를 표시한다.
    /// Init()으로 초기화하면 슬롯 스왑·버리기 이벤트를 자동 구독한다.
    /// 버리기: 슬롯을 인벤토리 패널 밖으로 드래그해서 드롭.
    /// </summary>
    public class InventoryPanelView : MonoBehaviour
    {
        [Header("슬롯 (Inspector에서 순서대로 연결)")]
        [SerializeField] private InventorySlotView[] _slots;

        [Header("소지 골드")]
        [SerializeField] private TextMeshProUGUI _goldText;

        [Header("닫기")]
        [SerializeField] private Button _closeButton;

        /// <summary>X 버튼 클릭 이벤트 — InventoryToggleController 가 구독해 Close() 를 호출한다.</summary>
        public event Action OnCloseRequested;

        private void Start()
        {
            _closeButton?.onClick.AddListener(() => OnCloseRequested?.Invoke());
        }

        private void Reset()
        {
            _slots    = GetComponentsInChildren<InventorySlotView>();
            _goldText = GetComponentInChildren<TextMeshProUGUI>();
        }

        private PlayerRelicInventory _inventory;
        private int                  _gold;

        /// <summary>
        /// 최초 1회 초기화.
        /// 슬롯 스왑·버리기 이벤트를 구독한 후 화면을 갱신한다.
        /// </summary>
        public void Init(PlayerRelicInventory inventory, int gold)
        {
            _inventory = inventory;
            _gold      = gold;

            foreach (var slot in _slots)
            {
                slot.OnSlotDropReceived    -= HandleSlotSwap;
                slot.OnSlotDropReceived    += HandleSlotSwap;

                slot.OnSlotDiscardRequested -= HandleSlotDiscard;
                slot.OnSlotDiscardRequested += HandleSlotDiscard;
            }

            Refresh(inventory, gold);
        }

        /// <summary>현재 인벤토리와 골드를 화면에 반영한다.</summary>
        public void Refresh(PlayerRelicInventory inventory, int gold)
        {
            _inventory = inventory;
            _gold      = gold;

            var relics = inventory.OwnedRelics;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (i < relics.Count && relics[i] != null)
                    _slots[i].SetRelic(relics[i]);
                else
                    _slots[i].Clear();
            }

            if (_goldText != null)
                _goldText.text = gold.ToString("N0");
        }

        // ── 이벤트 처리 ──────────────────────────────────────────────

        private void HandleSlotSwap(InventorySlotView from, InventorySlotView to)
        {
            if (_inventory == null) return;
            int fromIdx = System.Array.IndexOf(_slots, from);
            int toIdx   = System.Array.IndexOf(_slots, to);
            _inventory.Swap(fromIdx, toIdx);
            Refresh(_inventory, _gold);
        }

        private void HandleSlotDiscard(InventorySlotView slot)
        {
            if (_inventory == null || slot.CurrentRelic == null) return;
            if (_inventory.Remove(slot.CurrentRelic))
            {
                Debug.Log($"[InventoryPanel] 버림: {slot.CurrentRelic.DisplayName}");
                Refresh(_inventory, _gold);
            }
        }
    }
}
