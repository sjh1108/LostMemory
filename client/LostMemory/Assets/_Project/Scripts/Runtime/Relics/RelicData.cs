using UnityEngine;

namespace LostMemory.Relics
{
    /// <summary>
    /// 유물 또는 소모품 하나의 데이터.
    /// IsConsumable = true 이면 회복약·랜덤박스 등 비유물 보상이다.
    /// </summary>
    [CreateAssetMenu(fileName = "RelicData_New",
                     menuName = "LostMemory/Relic Data")]
    public class RelicData : ScriptableObject
    {
        [SerializeField] private string _displayName;

        [Tooltip("true = 소모품(회복약·랜덤박스), false = 유물")]
        [SerializeField] private bool _isConsumable;

        [Tooltip("소모품일 때는 무시")]
        [SerializeField] private RelicRarity _rarity;

        [Tooltip("소모품일 때는 무시")]
        [SerializeField] private RelicTag _tag;

        [SerializeField, TextArea(2, 4)] private string _effectDescription;

        [SerializeField] private Sprite _icon;
        
        // 유물이름
        public string      DisplayName       => _displayName;
        // 비유물 여부 
        public bool        IsConsumable      => _isConsumable;
        // 등급
        public RelicRarity Rarity            => _rarity;
        // 태그
        public RelicTag    Tag               => _tag;
        // 효과 설명
        public string      EffectDescription => _effectDescription;
        // 아이콘
        public Sprite      Icon              => _icon;
    }
}
