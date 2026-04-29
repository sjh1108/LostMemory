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

        [Header("Effect (CL-106 Wave A)")]
        [Tooltip("효과 분류. None = Wave A 시점 미구현 효과 (비-MVP 등).")]
        [SerializeField] private RelicEffectType _effectType = RelicEffectType.None;

        [Tooltip("효과의 주 수치. % 는 0.05 형태 (5%). 회복약은 0.25 = 25% 회복. 음수 가능 (질풍 장화 -0.12).")]
        [SerializeField] private float _magnitude;

        [Tooltip("임시 효과의 지속 시간(초). 영구/조건부면 0.")]
        [SerializeField, Min(0f)] private float _duration;

        [Tooltip("조건부 효과의 임계치 (예: 전투 북의 0.5 = HP 50%). 없으면 0.")]
        [SerializeField, Min(0f)] private float _threshold;

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

        // 효과 분류 (CL-106 Wave A)
        public RelicEffectType EffectType => _effectType;
        // 효과 주 수치 (CL-106 Wave A)
        public float           Magnitude  => _magnitude;
        // 임시 효과 지속 시간 (CL-106 Wave A)
        public float           Duration   => _duration;
        // 조건부 효과 임계치 (CL-106 Wave A)
        public float           Threshold  => _threshold;
    }
}
