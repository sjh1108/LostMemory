using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace LostMemory.Relics
{
    /// <summary>
    /// 유물 또는 소모품 하나의 데이터.
    /// IsConsumable = true 이면 회복약·랜덤박스 등 비유물 보상이다.
    ///
    /// CL-138: 듀얼 태그 + 사이즈 + 다중 효과(Effects[]) 도입.
    /// 기존 _tag/_effectType/_magnitude/_duration/_threshold 는 FormerlySerializedAs 로 보존
    /// (18개 기존 SO 직렬화 안전). RelicEffectRegistry 호환을 위해 legacy 프로퍼티는
    /// _effects[0] 우선, 비어 있으면 legacy 필드를 fallback 으로 반환.
    /// </summary>
    [CreateAssetMenu(fileName = "RelicData_New",
                     menuName = "LostMemory/Relic Data")]
    public class RelicData : ScriptableObject
    {
        [SerializeField] private string _displayName;

        [Tooltip("true = 소모품(회복약·랜덤박스), false = 유물")]
        [SerializeField] private bool _isConsumable;

        [Tooltip("true = 구매 즉시 효과 발동 후 사라짐 (랜덤박스 등)\n" +
                 "false = 인벤토리에 보관해 뒀다가 사용 (물약 등)\n" +
                 "IsConsumable = false 이면 이 값은 무시됨")]
        [SerializeField] private bool _isInstantUse;

        [Tooltip("소모품일 때는 무시")]
        [SerializeField] private RelicRarity _rarity;

        [Header("Tag (CL-138 듀얼)")]
        [Tooltip("듀얼 태그 첫 번째. 소모품·단일 태그 아이템은 None 사용.")]
        [FormerlySerializedAs("_tag")]
        [SerializeField] private RelicTag _tagPrimary;

        [Tooltip("듀얼 태그 두 번째. 단일 태그 아이템이면 None.")]
        [SerializeField] private RelicTag _tagSecondary;

        [Header("Inventory (CL-138)")]
        [Tooltip("인벤토리 점유 사이즈 (B-Lite 1). 등급별 자동 매핑은 CL-150 에서.")]
        [SerializeField] private Vector2Int _size = new Vector2Int(1, 1);

        [SerializeField, TextArea(2, 4)] private string _effectDescription;

        [SerializeField] private Sprite _icon;

        [Header("Effects (CL-138 V0.4 — 다중 효과)")]
        [Tooltip("이 아이템이 부여하는 효과 목록. 빈 배열이면 legacy 단일 효과 필드(_effectTypeLegacy 등) fallback.")]
        [SerializeField] private EffectEntry[] _effects;

        [Header("Legacy (CL-106 단일 효과 — deprecated, 마이그레이션 후 제거 예정)")]
        [FormerlySerializedAs("_effectType")]
        [SerializeField] private RelicEffectType _effectTypeLegacy = RelicEffectType.None;

        [FormerlySerializedAs("_magnitude")]
        [SerializeField] private float _magnitudeLegacy;

        [FormerlySerializedAs("_duration")]
        [SerializeField, Min(0f)] private float _durationLegacy;

        [FormerlySerializedAs("_threshold")]
        [SerializeField, Min(0f)] private float _thresholdLegacy;

        // 유물이름
        public string      DisplayName       => _displayName;
        // 비유물 여부
        public bool        IsConsumable      => _isConsumable;
        // 구매 즉시 사용 여부 (랜덤박스 등 — IsConsumable=true 일 때만 유효)
        public bool        IsInstantUse      => _isInstantUse;
        // 등급
        public RelicRarity Rarity            => _rarity;
        // 효과 설명
        public string      EffectDescription => _effectDescription;
        // 아이콘
        public Sprite      Icon              => _icon;

        // CL-138 신규
        public RelicTag                  TagPrimary    => _tagPrimary;
        public RelicTag                  TagSecondary  => _tagSecondary;
        public Vector2Int                Size          => _size;
        public IReadOnlyList<EffectEntry> Effects       => _effects;

        /// <summary>CL-138 이전 단일 태그 호환. <see cref="TagPrimary"/> 와 동일.</summary>
        [Obsolete("Use TagPrimary instead. Kept for legacy view code.")]
        public RelicTag Tag => _tagPrimary;

        // RelicEffectRegistry 호환 (CL-106). _effects[0] 우선, 없으면 legacy 필드.
        [Obsolete("Use Effects[0].Type instead. Kept for RelicEffectRegistry single-effect path.")]
        public RelicEffectType EffectType =>
            (_effects != null && _effects.Length > 0) ? _effects[0].Type : _effectTypeLegacy;

        [Obsolete("Use Effects[0].Magnitude instead.")]
        public float Magnitude =>
            (_effects != null && _effects.Length > 0) ? _effects[0].Magnitude : _magnitudeLegacy;

        [Obsolete("Use Effects[0].Duration instead.")]
        public float Duration =>
            (_effects != null && _effects.Length > 0) ? _effects[0].Duration : _durationLegacy;

        [Obsolete("Use Effects[0].Threshold instead.")]
        public float Threshold =>
            (_effects != null && _effects.Length > 0) ? _effects[0].Threshold : _thresholdLegacy;
    }
}
