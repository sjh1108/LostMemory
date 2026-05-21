using System;
using LostMemory.Relics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostMemory.Rewards
{
    /// <summary>
    /// 보상 3택 화면에서 카드 하나를 담당하는 뷰 컴포넌트.
    /// RewardPanelView가 Init()을 호출해 데이터를 주입한다.
    /// </summary>
    public class RewardCardView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _rarityText;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private Button _selectButton;

        [Header("Disabled visual (선택 불가 시 카드 전체 dim)")]
        [Tooltip("비할당이면 Awake 에서 카드 root 에 자동 추가. interactable=false 시 alpha 를 disabledAlpha 로.")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [Tooltip("interactable=false 일 때 카드 전체 alpha. Icon 포함 자식 전부 동시에 dim.")]
        [SerializeField, Range(0f, 1f)] private float _disabledAlpha = 0.4f;

        private RelicData _data;
        private Action<RelicData> _onSelected;

        private void Awake()
        {
            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        /// <summary>CL-146: 다중 픽 모드에서 선택된 카드 식별용 (RewardPanelView 가 disable 처리).</summary>
        public RelicData Data => _data;

        /// <summary>카드 데이터를 주입하고 UI를 갱신한다.</summary>
        public void Init(RelicData data, Action<RelicData> onSelected)
        {
            _data = data;
            _onSelected = onSelected;

            _nameText.text        = data.DisplayName;
            _nameText.color       = GetRarityColor(data);
            _descriptionText.text = data.EffectDescription;
            _rarityText.text      = data.IsConsumable
                ? "[소모품]"
                : $"[{ToKorean(data.Rarity)}] {RelicTagLabels.ToKorean(data.TagPrimary)}";

            _icon.sprite  = data.Icon != null ? data.Icon : null;
            _icon.enabled = data.Icon != null;

            _selectButton.onClick.RemoveAllListeners();
            _selectButton.onClick.AddListener(() => _onSelected?.Invoke(_data));
        }

        /// <summary>버튼 상호작용 토글 + 카드 전체 dim. RewardPanelView 가 열림 직후 잠시 false 로 두어 즉시 선택 방지.</summary>
        public void SetInteractable(bool interactable)
        {
            if (_selectButton != null) _selectButton.interactable = interactable;
            if (_canvasGroup != null) _canvasGroup.alpha = interactable ? 1f : _disabledAlpha;
        }

        private static string ToKorean(RelicRarity rarity) => rarity switch
        {
            RelicRarity.Common    => "일반",
            RelicRarity.Rare      => "레어",
            RelicRarity.Unique    => "유니크",
            RelicRarity.Legendary => "전설",
            _                     => rarity.ToString()
        };

        private static Color GetRarityColor(RelicData data)
        {
            if (data.IsConsumable)
                return new Color(0.7f, 0.7f, 0.7f); // 회색

            return data.Rarity switch
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
