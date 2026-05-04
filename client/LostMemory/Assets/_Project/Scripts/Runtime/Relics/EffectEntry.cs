using System;
using UnityEngine;

namespace LostMemory.Relics
{
    /// <summary>
    /// CL-138 V0.4: 1 RelicData 가 가질 수 있는 단일 효과.
    /// items_draft.md V0.4 분석 — 77개 아이템 대부분이 2개 이상 효과 보유 →
    /// RelicData._effects[] 배열로 다중 효과 표현.
    /// </summary>
    [Serializable]
    public struct EffectEntry
    {
        [Tooltip("효과 분류. RelicEffectRegistry / BuildManager 의 switch 가 분기.")]
        public RelicEffectType Type;

        [Tooltip("효과의 주 수치. % 는 0.05 형태 (5%). 음수 가능 (질풍 장화 -0.12).")]
        public float Magnitude;

        [Tooltip("임시 효과의 지속 시간(초). 영구/조건부면 0.")]
        [Min(0f)] public float Duration;

        [Tooltip("조건부 효과의 임계치 (예: 전투 북의 0.5 = HP 50%). 없으면 0.")]
        [Min(0f)] public float Threshold;
    }
}
