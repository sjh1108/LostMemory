using System;
using System.Text;
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

        [Header("활성 세트 요약 (옵션 — null 이면 미사용)")]
        [Tooltip("현재 활성화된 세트 효과들을 한 줄로 표시. null 허용.")]
        [SerializeField] private TextMeshProUGUI _setSummaryText;

        private BuildManager _buildManager;

        /// <summary>X 버튼 클릭 이벤트 — InventoryToggleController 가 구독해 Close() 를 호출한다.</summary>
        public event Action OnCloseRequested;

        private void Start()
        {
            _closeButton?.onClick.AddListener(() => OnCloseRequested?.Invoke());
            ResolveBuildManager();
            RefreshSetSummary();
        }

        private void OnDestroy()
        {
            if (_buildManager != null)
                _buildManager.OnSetTierChanged -= HandleSetTierChanged;
        }

        private void OnEnable()
        {
            // 패널이 다시 활성화될 때 (씬 전환 등) BuildManager 가 재구독돼야 함.
            if (_buildManager == null)
            {
                ResolveBuildManager();
            }
            RefreshSetSummary();
        }

        private void ResolveBuildManager()
        {
            if (_buildManager != null) return;
            _buildManager = FindFirstObjectByType<BuildManager>();
            if (_buildManager != null)
            {
                _buildManager.OnSetTierChanged -= HandleSetTierChanged;
                _buildManager.OnSetTierChanged += HandleSetTierChanged;
            }
        }

        private void HandleSetTierChanged(RelicTag _, int __, int ___) => RefreshSetSummary();

        private void RefreshSetSummary()
        {
            if (_setSummaryText == null) return;
            if (_buildManager == null)
            {
                _setSummaryText.text = "활성 세트: 없음";
                return;
            }

            var sb = new StringBuilder("활성 세트: ");
            bool any = false;
            var registered = _buildManager.RegisteredSetsInOrder;
            if (registered != null)
            {
                for (int i = 0; i < registered.Count; i++)
                {
                    BuildSetData set = registered[i];
                    if (set == null) continue;
                    int tier = _buildManager.GetActiveTier(set.SetTag);
                    if (tier < 0) continue;
                    if (any) sb.Append(", ");
                    sb.Append('[').Append(RelicTagLabels.ToKorean(set.SetTag))
                      .Append(" T").Append(tier).Append(']');
                    any = true;
                }
            }
            if (!any) sb.Append("없음");
            _setSummaryText.text = sb.ToString();
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
