using System;
using System.Collections.Generic;
using LostMemory.Relics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Shop
{
    /// <summary>
    /// 상점 왼쪽 패널 전체를 담당하는 뷰 컴포넌트.
    /// Init()으로 데이터를 주입하고, 구매 성공 시 OnItemPurchased 이벤트를 발생시킨다.
    /// 골드 차감·인벤토리 갱신은 이벤트를 받은 호출자가 처리한다.
    /// </summary>
    public class ShopPanelView : MonoBehaviour
    {
        [Header("상점 아이템 행 (미리 씬에 배치)")]
        [SerializeField] private ShopItemView[] _itemViews;

        [Header("하단 상세 정보 (선택)")]
        [SerializeField] private Image            _detailIconImage;
        [SerializeField] private TextMeshProUGUI  _detailNameText;
        [SerializeField] private TextMeshProUGUI  _detailRarityText;  // "[유니크] 맹공" 형식
        [SerializeField] private TextMeshProUGUI  _detailDescText;    // EffectDescription

        /// <summary>구매 성공 이벤트 — 인자: 구매된 아이템 데이터</summary>
        public event Action<ShopItemData> OnItemPurchased;

        /// <summary>컴포넌트 추가 / Reset 시 자식 참조를 자동으로 찾는다.</summary>
        private void Reset()
        {
            _itemViews      = GetComponentsInChildren<ShopItemView>();
            _detailNameText = transform.Find("DetailArea/DetailNameText")
                                       ?.GetComponent<TextMeshProUGUI>();
            _detailDescText = transform.Find("DetailArea/DetailDescText")
                                       ?.GetComponent<TextMeshProUGUI>();
        }

        private PlayerRelicInventory    _inventory;
        private ShopData                _shopData;
        private int                     _gold;
        private readonly HashSet<int>   _soldOutIndices = new();

        /// <summary>
        /// 상점을 초기화한다.
        /// </summary>
        /// <param name="shopData">판매 목록 ScriptableObject</param>
        /// <param name="inventory">플레이어 유물 인벤토리</param>
        /// <param name="gold">현재 보유 골드</param>
        public void Init(ShopData shopData, PlayerRelicInventory inventory, int gold)
        {
            _shopData  = shopData;
            _inventory = inventory;
            _gold      = gold;
            _soldOutIndices.Clear();

            for (int i = 0; i < _itemViews.Length; i++)
            {
                if (i < shopData.Items.Length)
                {
                    _itemViews[i].gameObject.SetActive(true);
                    int          capturedIndex = i;
                    ShopItemData capturedItem  = shopData.Items[i];
                    _itemViews[i].Init(
                        capturedItem,
                        item => TryBuy(capturedIndex, item),
                        item => ShowDetail(item.Relic));
                }
                else
                {
                    _itemViews[i].gameObject.SetActive(false);
                }
            }

            RefreshAffordability();
            ClearDetail();
        }

        /// <summary>외부에서 골드가 바뀌었을 때 호출 — 구매 가능 여부를 즉시 갱신한다.</summary>
        public void UpdateGold(int gold)
        {
            _gold = gold;
            RefreshAffordability();
        }

        // ──────────────────────────────────────────────────────────
        // 내부 로직
        // ──────────────────────────────────────────────────────────

        private void TryBuy(int index, ShopItemData item)
        {
            if (_soldOutIndices.Contains(index))
            {
                Debug.Log("[ShopPanel] 이미 구매한 아이템");
                return;
            }

            if (_gold < item.Price)
            {
                Debug.Log($"[ShopPanel] 골드 부족 (보유: {_gold}, 필요: {item.Price})");
                return;
            }

            bool added = _inventory.TryAdd(item.Relic);
            if (!added && !item.Relic.IsConsumable)
            {
                // 비소모품인데 추가 실패 = 이미 보유 중 (랜덤박스로 받은 경우 등)
                Debug.Log($"[ShopPanel] 이미 보유 중인 유물: {item.Relic.DisplayName}");
                return;
            }
            // 소모품(IsConsumable=true)은 TryAdd가 false를 반환하지만 구매는 정상 진행

            // 구매 성공
            _gold -= item.Price;
            _soldOutIndices.Add(index);
            _itemViews[index].SetSoldOut(true);
            ShowDetail(item.Relic);
            RefreshAffordability();   // 차감된 골드로 나머지 아이템 구매 가능 여부 갱신

            OnItemPurchased?.Invoke(item);
        }

        /// <summary>현재 골드와 보유 여부로 각 아이템의 구매 가능 상태를 갱신한다.</summary>
        private void RefreshAffordability()
        {
            if (_shopData == null) return;
            for (int i = 0; i < _itemViews.Length; i++)
            {
                if (i >= _shopData.Items.Length || !_itemViews[i].gameObject.activeSelf)
                    continue;

                var relic = _shopData.Items[i].Relic;

                // 소모품이 아닌 유물이고 이미 보유 중이면 "보유중" 표시
                bool owned = !relic.IsConsumable && _inventory.Has(relic);
                _itemViews[i].SetOwned(owned);
                _itemViews[i].SetAffordable(_gold >= _shopData.Items[i].Price);
            }
        }

        private void ShowDetail(RelicData relic)
        {
            if (_detailIconImage != null)
            {
                _detailIconImage.sprite  = relic.Icon;
                _detailIconImage.color   = Color.white;
                _detailIconImage.enabled = relic.Icon != null;
            }
            if (_detailNameText   != null) _detailNameText.text   = relic.DisplayName;
            if (_detailRarityText != null) _detailRarityText.text = GetRarityTagLabel(relic);
            if (_detailDescText   != null) _detailDescText.text   = relic.EffectDescription;
        }

        private static string GetRarityTagLabel(RelicData relic)
        {
            if (relic.IsConsumable) return "[소모품]";

            string rarityName = relic.Rarity switch
            {
                RelicRarity.Common    => "일반",
                RelicRarity.Rare      => "레어",
                RelicRarity.Unique    => "유니크",
                RelicRarity.Legendary => "전설",
                _                     => ""
            };
            string tagName = relic.Tag switch
            {
                RelicTag.Assault  => "맹공",
                RelicTag.Guardian => "수호",
                RelicTag.Sprint   => "질주",
                _                 => ""
            };
            return $"[{rarityName}] {tagName}";
        }

        private void ClearDetail()
        {
            if (_detailIconImage  != null) _detailIconImage.enabled = false;
            if (_detailNameText   != null) _detailNameText.text     = string.Empty;
            if (_detailRarityText != null) _detailRarityText.text   = string.Empty;
            if (_detailDescText   != null) _detailDescText.text     = string.Empty;
        }
    }
}
