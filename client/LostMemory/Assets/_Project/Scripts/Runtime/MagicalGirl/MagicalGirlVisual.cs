using System.Collections.Generic;
using LostMemory.Relics;
using UnityEngine;

namespace LostMemory.MagicalGirl
{
    /// <summary>
    /// CL-204: 5종 효과 카테고리. 1:1 매핑 — 각 visual = 한 종류 미소녀 sprite + 공격 패턴.
    ///
    /// girl.png 좌→우 = Fire(1) / Ice(2) / Star(3) / Blackhole(4) / Arrow(5)
    /// CL-144 7속성 enum 폐기. RelicTag → 5종으로 수렴 (FromTag 참고).
    /// </summary>
    public enum MagicalGirlVisual
    {
        Default,    // 매핑 전 placeholder
        Fire,       // 1번 — 화염 투사체
        Ice,        // 2번 — 얼음 장판 (적 추적)
        Star,       // 3번 — 별 투사체 + 주변 별 ambient
        Blackhole,  // 4번 — 블랙홀 장판 (정지, 끌어당김)
        Arrow,      // 5번 — 일반 화살 (= 빛의 미소녀)
    }

    /// <summary>
    /// RelicTag → MagicalGirlVisual 매핑 + 색상(레거시 sprite tint, sprite 자체 교체 후 폐기 대상).
    /// 14개 미소녀-태그 RelicData 의 secondary tag 가 5종 visual 중 하나로 결정됨.
    /// </summary>
    public static class MagicalGirlVisualPalette
    {
        // CL-204: sprite 자체가 visual 별로 다른 도트라 색상은 레거시. 향후 sprite 교체 후 본 dict 제거 가능.
        private static readonly Dictionary<MagicalGirlVisual, Color> _colors = new()
        {
            { MagicalGirlVisual.Default,    new Color(1f, 0.5f, 0.8f) },
            { MagicalGirlVisual.Fire,       new Color(1f, 0.4f, 0.4f) },
            { MagicalGirlVisual.Ice,        new Color(0.6f, 0.45f, 0.3f) },
            { MagicalGirlVisual.Star,       new Color(0.6f, 0.85f, 1f) },
            { MagicalGirlVisual.Blackhole,  new Color(1f, 0.95f, 0.4f) },
            { MagicalGirlVisual.Arrow,      new Color(1f, 0.4f, 0.5f) },
        };

        public static Color Get(MagicalGirlVisual v) =>
            _colors.TryGetValue(v, out Color c) ? c : Color.white;

        /// <summary>
        /// RelicData secondary tag → 5종 visual.
        /// Fire→Fire, Ice→Ice, Range→Star, Critical→Blackhole, 그 외 미소녀 부수태그→Arrow.
        /// </summary>
        public static MagicalGirlVisual FromTag(RelicTag tag) => tag switch
        {
            RelicTag.Fire     => MagicalGirlVisual.Fire,
            RelicTag.Ice      => MagicalGirlVisual.Ice,
            RelicTag.Range    => MagicalGirlVisual.Star,
            RelicTag.Critical => MagicalGirlVisual.Blackhole,
            // Health(빛의 미소녀/포근한 가호), Wind(바람의 깃털/약속), Lightning(전기 안경), Luck(행운) → Arrow(5)
            RelicTag.Health    => MagicalGirlVisual.Arrow,
            RelicTag.Wind      => MagicalGirlVisual.Arrow,
            RelicTag.Lightning => MagicalGirlVisual.Arrow,
            RelicTag.Luck      => MagicalGirlVisual.Arrow,
            _                  => MagicalGirlVisual.Default,
        };
    }
}
