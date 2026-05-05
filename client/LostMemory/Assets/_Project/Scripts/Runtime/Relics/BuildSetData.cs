using System;
using System.Collections.Generic;
using UnityEngine;

namespace LostMemory.Relics
{
    /// <summary>
    /// CL-138: 16세트 각각의 티어별 효과 정의.
    /// BuildManager(CL-139) 가 PlayerRelicInventory 의 보유 아이템 듀얼 태그를 카운트 →
    /// 도달 티어 만큼의 SetTier 효과를 적용한다.
    /// 본 SO 는 *데이터*만 책임 — 카운트·적용 로직은 CL-139/140.
    /// </summary>
    [CreateAssetMenu(fileName = "BuildSet_New",
                     menuName = "LostMemory/Build Set Data")]
    public class BuildSetData : ScriptableObject
    {
        [Tooltip("이 세트가 어떤 태그(공속/치명타/...)에 해당하는지.")]
        [SerializeField] private RelicTag _setTag;

        [Tooltip("표시명 — '공속', '치명타', '행운', '타로' 등.")]
        [SerializeField] private string _displayName;

        [Tooltip("티어 배열. RequiredCount 오름차순으로 입력 권장 (예: 행운 1/3/5/7).")]
        [SerializeField] private SetTier[] _tiers;

        public RelicTag                  SetTag      => _setTag;
        public string                    DisplayName => _displayName;
        public IReadOnlyList<SetTier>    Tiers       => _tiers;
    }

    /// <summary>
    /// 1 세트의 1 티어 — "필요 아이템 수" 도달 시 적용되는 효과.
    /// EffectType / Magnitude / Description 의 의미는 RelicEffectRegistry 와 동일 규약.
    /// </summary>
    [Serializable]
    public struct SetTier
    {
        [Tooltip("이 티어 발동에 필요한 아이템 수 (예: 행운 1/3/5/7).")]
        [Min(0)] public int RequiredCount;

        [Tooltip("티어 발동 시 적용 효과 분류. None = 미구현 / 특수 핸들러(미소녀 합체 등).")]
        public RelicEffectType EffectType;

        [Tooltip("효과 수치. 예: 0.25 = 25%, 4 = +4 flat.")]
        public float Magnitude;

        [Tooltip("시간성 효과의 지속시간 (초). 예: BurnOnHit 의 도트 지속. Stat 효과는 0.")]
        [Min(0)] public float Duration;

        [TextArea(1, 3)]
        public string Description;
    }
}
