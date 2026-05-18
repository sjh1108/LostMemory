using System;
using LostMemory.Relics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Events
{
    /// <summary>
    /// CL-228 (A 옵션) — 1 아이템 vending machine 패널.
    /// 시각: 아이템 아이콘 + 이름 + 효과 설명 + 가격 + 골드 표시 + 구매/건너뛰기 버튼.
    /// 정체 가림 X — 모든 정보 공개.
    /// </summary>
    public sealed class VendingMachinePanelView : MonoBehaviour
    {
        [Header("아이템 표시")]
        [SerializeField] private Image itemIcon;
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI itemDescText;
        [SerializeField] private TextMeshProUGUI priceText;

        [Header("HUD")]
        [SerializeField] private TextMeshProUGUI goldText;

        [Header("Buttons")]
        [SerializeField] private Button buyButton;
        [SerializeField] private Button skipButton;

        [Header("Sold-out (옵션)")]
        [SerializeField] private GameObject soldOutOverlay;

        public event Action OnBuyPressed;
        public event Action OnSkipPressed;

        private VendingMachineConfig _config;
        private bool _isSoldOut;
        private int _gold;

        private void Awake()
        {
            if (buyButton != null)
            {
                buyButton.onClick.RemoveAllListeners();
                buyButton.onClick.AddListener(() => OnBuyPressed?.Invoke());
            }
            if (skipButton != null)
            {
                skipButton.onClick.RemoveAllListeners();
                skipButton.onClick.AddListener(() => OnSkipPressed?.Invoke());
            }
        }

        public void Init(VendingMachineConfig config, int gold)
        {
            _config = config;
            _isSoldOut = false;
            _gold = gold;
            if (soldOutOverlay != null) soldOutOverlay.SetActive(false);

            RelicData item = config != null ? config.Item : null;
            if (itemIcon != null)
            {
                itemIcon.sprite = item != null ? item.Icon : null;
                itemIcon.color = Color.white;
                itemIcon.enabled = item != null && item.Icon != null;
            }
            if (itemNameText != null)
            {
                itemNameText.text = item != null ? item.DisplayName : (config != null ? config.FallbackLabel : "");
            }
            if (itemDescText != null)
            {
                itemDescText.text = item != null ? item.EffectDescription : "";
            }
            if (priceText != null)
            {
                priceText.text = (config != null ? config.Price : 0).ToString("N0") + " G";
            }
            UpdateGold(gold);
        }

        public void UpdateGold(int gold)
        {
            _gold = gold;
            if (goldText != null) goldText.text = $"보유 골드: {gold:N0}";
            RefreshAffordability();
        }

        public void MarkSoldOut()
        {
            _isSoldOut = true;
            if (soldOutOverlay != null) soldOutOverlay.SetActive(true);
            RefreshAffordability();
        }

        private void RefreshAffordability()
        {
            if (buyButton == null || _config == null) return;
            buyButton.interactable = !_isSoldOut && _gold >= _config.Price;
        }
    }
}
