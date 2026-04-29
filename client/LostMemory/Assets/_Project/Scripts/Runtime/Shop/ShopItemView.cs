using System;
using LostMemory.Relics;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LostMemory.Shop
{
    /// <summary>
    /// 상점 목록에서 아이템 1행을 담당하는 뷰 컴포넌트.
    /// ShopPanelView가 Init()을 호출해 데이터를 주입한다.
    /// </summary>
    public class ShopItemView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image             _iconImage;
        [SerializeField] private TextMeshProUGUI   _nameText;
        [SerializeField] private TextMeshProUGUI   _descriptionText;
        [SerializeField] private TextMeshProUGUI   _priceText;
        [SerializeField] private Button            _buyButton;

        [Tooltip("구매 완료 후 표시할 오버레이 (없으면 무시)")]
        [SerializeField] private GameObject _soldOutOverlay;

        private ShopItemData         _data;
        private Action<ShopItemData> _onBuyRequested;
        private Action<ShopItemData> _onSelected;
        private bool                 _isSoldOut = false;
        private bool                 _isOwned   = false;   // 이미 보유 중
        private bool                 _canAfford = true;
        private Color                _originalBgColor;
        private Color                _originalBtnColor;

        private void Awake()
        {
            var bgImg  = GetComponent<Image>();
            if (bgImg  != null) _originalBgColor  = bgImg.color;

            var btnImg = _buyButton?.GetComponent<Image>();
            if (btnImg != null) _originalBtnColor = btnImg.color;
        }

        /// <summary>컴포넌트 추가 / Reset 시 자식 참조를 자동으로 찾는다.</summary>
        private void Reset()
        {
            _iconImage       = transform.Find("IconImage/Icon")?.GetComponent<Image>();
            _nameText        = transform.Find("InfoArea/NameText")?.GetComponent<TextMeshProUGUI>();
            _descriptionText = transform.Find("InfoArea/DescText")?.GetComponent<TextMeshProUGUI>();
            _priceText       = transform.Find("PriceArea/PriceText")?.GetComponent<TextMeshProUGUI>();
            _buyButton       = transform.Find("PriceArea/BuyButton")?.GetComponent<Button>();
        }

        /// <summary>아이템 데이터를 주입하고 UI를 갱신한다.</summary>
        /// <param name="onSelected">클릭 시 Detail 영역 갱신 콜백 (옵션)</param>
        public void Init(ShopItemData data, Action<ShopItemData> onBuyRequested,
                         Action<ShopItemData> onSelected = null)
        {
            _data           = data;
            _onBuyRequested = onBuyRequested;
            _onSelected     = onSelected;

            _nameText.text        = data.Relic.DisplayName;
            _nameText.color       = GetRarityColor(data.Relic);
            _descriptionText.text = GetRarityTagLabel(data.Relic); // "[전설 수호]" 형식
            _priceText.text       = data.Price.ToString("N0");

            _iconImage.sprite  = data.Relic.Icon;
            _iconImage.color   = Color.white;          // 스프라이트가 회색으로 물들지 않게
            _iconImage.enabled = data.Relic.Icon != null;

            SetSoldOut(false);

            _buyButton.onClick.RemoveAllListeners();
            _buyButton.onClick.AddListener(() => _onBuyRequested?.Invoke(_data));
        }

        /// <summary>마우스가 아이템 위에 올라오면 Detail 영역을 갱신한다.</summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_data != null) _onSelected?.Invoke(_data);
        }

        /// <summary>마우스가 아이템을 벗어나도 마지막 선택 상태를 유지한다 (sticky).</summary>
        public void OnPointerExit(PointerEventData eventData) { }

        /// <summary>이미 보유 중 상태를 설정한다. 버튼을 "보유중"으로 비활성화한다.</summary>
        public void SetOwned(bool isOwned)
        {
            _isOwned = isOwned;
            RefreshButtonState();
        }

        /// <summary>구매완료 상태를 설정한다.</summary>
        public void SetSoldOut(bool isSoldOut)
        {
            _isSoldOut = isSoldOut;
            RefreshButtonState();

            // 아이템 배경: 구매완료 시 어둡게
            var bgImg = GetComponent<Image>();
            if (bgImg != null)
                bgImg.color = isSoldOut
                    ? new Color(_originalBgColor.r * 0.5f,
                                _originalBgColor.g * 0.5f,
                                _originalBgColor.b * 0.5f)
                    : _originalBgColor;

            if (_soldOutOverlay != null)
                _soldOutOverlay.SetActive(isSoldOut);
        }

        /// <summary>골드 부족 여부에 따라 버튼을 활성/비활성화한다.</summary>
        public void SetAffordable(bool canAfford)
        {
            _canAfford = canAfford;
            RefreshButtonState();
        }

        /// <summary>sold-out / owned / affordable 상태를 조합해 버튼 외관을 결정한다.</summary>
        private void RefreshButtonState()
        {
            if (_buyButton == null) return;

            _buyButton.interactable = !_isSoldOut && !_isOwned && _canAfford;

            // 버튼 텍스트 — 우선순위: 구매완료 > 보유중 > 구매
            var buyText = _buyButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buyText != null)
                buyText.text = _isSoldOut ? "구매완료"
                             : _isOwned   ? "보유중"
                             :              "구매";

            // 버튼 색상
            var btnImg = _buyButton.GetComponent<Image>();
            if (btnImg != null)
            {
                if (_isSoldOut || _isOwned)
                    btnImg.color = new Color(_originalBtnColor.r * 0.5f,
                                            _originalBtnColor.g * 0.5f,
                                            _originalBtnColor.b * 0.5f);   // 어두운 초록
                else if (!_canAfford)
                    btnImg.color = new Color(0.35f, 0.35f, 0.35f);         // 회색
                else
                    btnImg.color = _originalBtnColor;                       // 정상 초록
            }
        }

        /// <summary>[등급 태그] 형식의 짧은 레이블을 반환한다. 예: "[전설 수호]"</summary>
        private static string GetRarityTagLabel(RelicData relic)
        {
            if (relic.IsConsumable)
                return "[소모품]";

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

        private static Color GetRarityColor(RelicData relic)
        {
            if (relic.IsConsumable)
                return new Color(0.7f, 0.7f, 0.7f);

            return relic.Rarity switch
            {
                RelicRarity.Common    => Color.white,
                RelicRarity.Rare      => new Color(0.31f, 0.59f, 0.96f), // #4F97F5
                RelicRarity.Unique    => new Color(1f,    0.30f, 0.99f), // #FF4DFC
                RelicRarity.Legendary => new Color(0.96f, 0.77f, 0.26f), // #F5C542
                _                     => Color.white
            };
        }
    }
}
