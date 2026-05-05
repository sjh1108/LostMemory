using System.Collections.Generic;
using LostMemory.Relics;
using UnityEngine;

namespace LostMemory.MagicalGirl
{
    /// <summary>
    /// CL-144: 미소녀 시각 변형. 메커니즘은 모두 동일, 색상만 다름.
    /// 정식 sprite/VFX 는 별도 ticket 에서 교체.
    /// 새 속성 추가 시 enum + _colors + FromTag 3곳만 수정.
    /// </summary>
    public enum MagicalGirlVisual
    {
        Default,    // 분홍 (기본)
        Fire,       // 빨강
        Ice,        // 파랑
        Lightning,  // 노랑
        Wind,       // 초록
        Light,      // 흰색
        Dark,       // 검정
        Pink,       // 분홍 (Default 와 동일)
    }

    /// <summary>
    /// 미소녀 시각 ↔ 색상 매핑.
    /// RelicTag → MagicalGirlVisual 변환은 FromTag 한 곳에 집약.
    /// </summary>
    public static class MagicalGirlVisualPalette
    {
        private static readonly Dictionary<MagicalGirlVisual, Color> _colors = new()
        {
            { MagicalGirlVisual.Default,   new Color(1f, 0.5f, 0.8f) },
            { MagicalGirlVisual.Fire,      new Color(1f, 0.3f, 0.1f) },
            { MagicalGirlVisual.Ice,       new Color(0.4f, 0.7f, 1f) },
            { MagicalGirlVisual.Lightning, new Color(1f, 1f, 0.2f) },
            { MagicalGirlVisual.Wind,      new Color(0.4f, 1f, 0.5f) },
            { MagicalGirlVisual.Light,     Color.white },
            { MagicalGirlVisual.Dark,      new Color(0.2f, 0.2f, 0.3f) },
            { MagicalGirlVisual.Pink,      new Color(1f, 0.5f, 0.8f) },
        };

        public static Color Get(MagicalGirlVisual v) =>
            _colors.TryGetValue(v, out Color c) ? c : Color.white;

        public static MagicalGirlVisual FromTag(RelicTag tag) => tag switch
        {
            RelicTag.Fire        => MagicalGirlVisual.Fire,
            RelicTag.Ice         => MagicalGirlVisual.Ice,
            RelicTag.Lightning   => MagicalGirlVisual.Lightning,
            RelicTag.Wind        => MagicalGirlVisual.Wind,
            RelicTag.Health      => MagicalGirlVisual.Light,
            RelicTag.Critical    => MagicalGirlVisual.Dark,
            _                    => MagicalGirlVisual.Default,
        };
    }
}
