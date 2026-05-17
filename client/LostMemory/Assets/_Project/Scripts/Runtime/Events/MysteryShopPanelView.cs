using System;
using System.Collections.Generic;
using LostMemory.Relics;
using LostMemory.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Events
{
    /// <summary>
    /// CL-228 — 미스터리 구매 패널.
    /// MysteryCardView N개 (씬에 미리 배치) 의 Init/Reveal/Affordability 를 묶어 관리.
    /// 골드 차감 + 인벤토리 추가는 OnSlotPurchased 이벤트를 받은 controller 가 처리.
    ///
    /// ShopPanelView ([Shop/ShopPanelView.cs]) 와 책임 동일하나:
    /// - Detail 영역 X (구매 전엔 보여줄 정보 X, 구매 후엔 카드 자체에 표시)
    /// - Hover 처리 X (호버 정보 누설 방지)
    /// - "Skip" 버튼 노출 — 구매 안 하고 떠나기
    /// </summary>
    public sealed class MysteryShopPanelView : MonoBehaviour
    {
        [Header("슬롯 (씬에 미리 배치)")]
        [SerializeField] private MysteryCardView[] cardViews;

        [Header("HUD")]
        [Tooltip("(옵션) 패널 안에서 현재 골드를 표시. ShopPanelView 와 별개로 패널 자체 표시. null 허용.")]
        [SerializeField] private TextMeshProUGUI goldText;
        [Tooltip("Skip 버튼 — 구매 안 하고 패널 닫고 출구 해제.")]
        [SerializeField] private Button skipButton;

        /// <summary>슬롯 클릭 시 발화. controller 가 GoldWallet.Spend + Inventory.TryAdd + 카드 reveal 처리.</summary>
        public event Action<ShopItemData> OnSlotPurchased;
        /// <summary>Skip 버튼 클릭 시 발화. controller 가 Close + 출구 해제.</summary>
        public event Action OnSkipPressed;

        private ShopData _shopData;
        private PlayerRelicInventory _inventory;
        private int _gold;
        private readonly HashSet<int> _soldOutIndices = new HashSet<int>();

        private void Awake()
        {
            if (skipButton != null)
            {
                skipButton.onClick.RemoveAllListeners();
                skipButton.onClick.AddListener(() => OnSkipPressed?.Invoke());
            }
        }

        /// <summary>
        /// 패널 초기화. 같은 ShopData 인스턴스 재오픈 시 sold-out 보존 — ShopPanelView 와 동일 패턴.
        /// </summary>
        public void Init(ShopData shopData, PlayerRelicInventory inventory, int gold)
        {
            bool sameShop = ReferenceEquals(_shopData, shopData);

            _shopData = shopData;
            _inventory = inventory;
            _gold = gold;

            if (!sameShop)
            {
                _soldOutIndices.Clear();
            }

            for (int i = 0; i < cardViews.Length; i++)
            {
                if (cardViews[i] == null) continue;

                if (i < shopData.Items.Length)
                {
                    cardViews[i].gameObject.SetActive(true);
                    int capturedIndex = i;
                    ShopItemData capturedItem = shopData.Items[i];
                    cardViews[i].Init(capturedItem, item => HandleSlotClicked(capturedIndex, item));

                    // 같은 인스턴스 재오픈 시 이전 sold-out 시각 복원.
                    if (sameShop && _soldOutIndices.Contains(i))
                    {
                        cardViews[i].SetSoldOutVisual(true);
                    }
                }
                else
                {
                    cardViews[i].gameObject.SetActive(false);
                }
            }

            UpdateGold(gold);
        }

        /// <summary>외부에서 골드가 바뀌었을 때 호출 — 슬롯 affordability + HUD 갱신.</summary>
        public void UpdateGold(int gold)
        {
            _gold = gold;
            if (goldText != null) goldText.text = $"보유 골드: {gold:N0}";
            RefreshAffordability();
        }

        private void HandleSlotClicked(int index, ShopItemData item)
        {
            if (_soldOutIndices.Contains(index))
            {
                Debug.Log("[MysteryShopPanel] 이미 구매한 슬롯");
                return;
            }
            if (_gold < item.Price)
            {
                Debug.Log($"[MysteryShopPanel] 골드 부족 (보유 {_gold} / 필요 {item.Price})");
                return;
            }
            if (_inventory == null || !_inventory.TryAdd(item.Relic))
            {
                Debug.Log($"[MysteryShopPanel] 인벤토리 추가 실패: {item.Relic?.DisplayName}");
                return;
            }

            // 구매 확정 — 시각 reveal + sold-out 등록
            _gold -= item.Price;
            _soldOutIndices.Add(index);
            cardViews[index].RevealAndMarkSoldOut();
            UpdateGold(_gold);   // 우리 쪽 _gold 는 이미 차감했으니 affordability 만 다시 계산. controller 가 GoldWallet.Spend 후 다시 UpdateGold 호출 → 동일 값 → idempotent.

            OnSlotPurchased?.Invoke(item);
        }

        /// <summary>현재 골드 + sold-out 상태로 각 슬롯 affordability 갱신.</summary>
        private void RefreshAffordability()
        {
            if (_shopData == null) return;
            for (int i = 0; i < cardViews.Length; i++)
            {
                if (cardViews[i] == null || !cardViews[i].gameObject.activeSelf) continue;
                if (i >= _shopData.Items.Length) continue;
                cardViews[i].SetAffordable(_gold >= _shopData.Items[i].Price);
            }
        }
    }
}
