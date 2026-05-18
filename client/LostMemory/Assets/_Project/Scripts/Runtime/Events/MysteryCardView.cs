using System;
using LostMemory.Relics;
using LostMemory.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Events
{
    /// <summary>
    /// CL-228 — 미스터리 구매 슬롯 1장.
    /// 구매 전: 카드 뒷면 + 가격만 표시. 등급/이름/효과 모두 가림.
    /// 구매 후: 앞면으로 reveal — Icon / Name / Rarity 표시.
    ///
    /// 시각 구조:
    ///   _backRoot  — 뒷면 (가림 sprite + 가격 텍스트)
    ///   _frontRoot — 앞면 (Icon, NameText, RarityTagText, PriceText[옵션])
    ///   _buyButton — 항상 _backRoot 위에 있는 구매 버튼
    ///
    /// Phase A: reveal 은 즉시 swap (backRoot SetActive(false), frontRoot SetActive(true)).
    /// 후속 폴리시에서 카드 뒤집기 코루틴 추가.
    /// </summary>
    public sealed class MysteryCardView : MonoBehaviour
    {
        [Header("Roots")]
        [Tooltip("뒷면 root — 카드 뒷면 sprite + 가격 텍스트 + 구매 버튼이 자식.")]
        [SerializeField] private GameObject backRoot;
        [Tooltip("앞면 root — reveal 시 활성. Icon/Name/Rarity 자식.")]
        [SerializeField] private GameObject frontRoot;

        [Header("Back (구매 전)")]
        [SerializeField] private TextMeshProUGUI backPriceText;
        [SerializeField] private Button buyButton;

        [Header("Front (reveal 후)")]
        [SerializeField] private Image frontIcon;
        [SerializeField] private TextMeshProUGUI frontNameText;
        [SerializeField] private TextMeshProUGUI frontRarityTagText;
        [Tooltip("(옵션) 가격을 reveal 후에도 보여줄 때 사용. null 허용.")]
        [SerializeField] private TextMeshProUGUI frontPriceText;

        [Header("Sold-out (옵션)")]
        [Tooltip("(옵션) 구매 완료 표시 오버레이. reveal 후 활성. null 허용.")]
        [SerializeField] private GameObject soldOutOverlay;

        private ShopItemData _data;
        private Action<ShopItemData> _onBuyRequested;
        private bool _isSoldOut;
        private bool _canAfford = true;

        private Color _originalBtnColor;

        private void Awake()
        {
            if (buyButton != null)
            {
                var btnImg = buyButton.GetComponent<Image>();
                if (btnImg != null) _originalBtnColor = btnImg.color;
            }
        }

        /// <summary>
        /// 슬롯 초기화. 뒷면 + 가격 텍스트만 표시. 클릭 시 onBuyRequested 콜백.
        /// 같은 인스턴스에 다른 data 로 재 Init 하면 reveal 상태 리셋.
        /// </summary>
        public void Init(ShopItemData data, Action<ShopItemData> onBuyRequested)
        {
            _data = data;
            _onBuyRequested = onBuyRequested;
            _isSoldOut = false;

            // 뒷면 표시 — 가격만
            if (backRoot != null) backRoot.SetActive(true);
            if (frontRoot != null) frontRoot.SetActive(false);
            if (soldOutOverlay != null) soldOutOverlay.SetActive(false);
            if (backPriceText != null) backPriceText.text = data.Price.ToString("N0") + " G";

            if (buyButton != null)
            {
                buyButton.onClick.RemoveAllListeners();
                buyButton.onClick.AddListener(() => _onBuyRequested?.Invoke(_data));
            }
            RefreshButtonState();
        }

        /// <summary>구매 성공 시 패널 뷰가 호출. 카드 reveal + sold-out 표시.</summary>
        public void RevealAndMarkSoldOut()
        {
            if (_data == null) return;

            if (backRoot != null) backRoot.SetActive(false);
            if (frontRoot != null) frontRoot.SetActive(true);

            RelicData relic = _data.Relic;
            if (frontIcon != null)
            {
                frontIcon.sprite = relic.Icon;
                frontIcon.color = Color.white;
                frontIcon.enabled = relic.Icon != null;
            }
            if (frontNameText != null)
            {
                frontNameText.text = relic.DisplayName;
                frontNameText.color = GetRarityColor(relic);
            }
            if (frontRarityTagText != null) frontRarityTagText.text = GetRarityTagLabel(relic);
            if (frontPriceText != null) frontPriceText.text = _data.Price.ToString("N0") + " G";

            _isSoldOut = true;
            if (soldOutOverlay != null) soldOutOverlay.SetActive(true);

            RefreshButtonState();
        }

        /// <summary>같은 ShopData 인스턴스 재오픈 시 sold-out 시각 상태 복원용. 데이터 없이 호출 가능.</summary>
        public void SetSoldOutVisual(bool isSoldOut)
        {
            _isSoldOut = isSoldOut;
            if (isSoldOut)
            {
                RevealAndMarkSoldOut();
            }
            else
            {
                if (soldOutOverlay != null) soldOutOverlay.SetActive(false);
            }
            RefreshButtonState();
        }

        /// <summary>골드 부족 여부 — 패널 뷰가 RefreshAffordability 호출 시 갱신.</summary>
        public void SetAffordable(bool canAfford)
        {
            _canAfford = canAfford;
            RefreshButtonState();
        }

        public bool IsSoldOut => _isSoldOut;
        public ShopItemData Data => _data;

        private void RefreshButtonState()
        {
            if (buyButton == null) return;

            buyButton.interactable = !_isSoldOut && _canAfford;

            var btnText = buyButton.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                btnText.text = _isSoldOut ? "구매완료" : "구매";
            }

            var btnImg = buyButton.GetComponent<Image>();
            if (btnImg != null)
            {
                if (_isSoldOut)
                {
                    btnImg.color = new Color(_originalBtnColor.r * 0.5f, _originalBtnColor.g * 0.5f, _originalBtnColor.b * 0.5f);
                }
                else if (!_canAfford)
                {
                    btnImg.color = new Color(0.35f, 0.35f, 0.35f);
                }
                else
                {
                    btnImg.color = _originalBtnColor;
                }
            }
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
            string tagName = RelicTagLabels.ToKorean(relic.TagPrimary);
            return $"[{rarityName}] {tagName}";
        }

        private static Color GetRarityColor(RelicData relic)
        {
            if (relic.IsConsumable) return new Color(0.7f, 0.7f, 0.7f);
            return relic.Rarity switch
            {
                RelicRarity.Common    => Color.white,
                RelicRarity.Rare      => new Color(0.31f, 0.59f, 0.96f),
                RelicRarity.Unique    => new Color(1f,    0.30f, 0.99f),
                RelicRarity.Legendary => new Color(0.96f, 0.77f, 0.26f),
                _                     => Color.white
            };
        }
    }
}
