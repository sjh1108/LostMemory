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

        private RelicData _data;
        private Action<RelicData> _onSelected;

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
                : $"[{ToKorean(data.Rarity)}] {ToKoreanTag(data.Tag)}";

            _icon.sprite  = data.Icon != null ? data.Icon : null;
            _icon.enabled = data.Icon != null;

            _selectButton.onClick.RemoveAllListeners();
            _selectButton.onClick.AddListener(() => _onSelected?.Invoke(_data));
        }

        private static string ToKorean(RelicRarity rarity) => rarity switch
        {
            RelicRarity.Common    => "일반",
            RelicRarity.Rare      => "레어",
            RelicRarity.Unique    => "유니크",
            RelicRarity.Legendary => "전설",
            _                     => rarity.ToString()
        };

        private static string ToKoreanTag(RelicTag tag) => tag switch
        {
            RelicTag.Assault  => "맹공",
            RelicTag.Guardian => "수호",
            RelicTag.Sprint   => "질주",
            _                 => tag.ToString()
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
