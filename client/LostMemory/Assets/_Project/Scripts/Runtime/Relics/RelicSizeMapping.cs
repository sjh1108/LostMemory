using UnityEngine;

namespace LostMemory.Relics
{
    /// <summary>
    /// CL-150: 등급(Rarity) → 인벤토리 점유 사이즈 (Vector2Int) 정적 매핑.
    ///
    /// 컨벤션: x = 가로(Width), y = 세로(Height). CL-151 자동 배치 알고리즘과 동일.
    ///
    /// 매핑 표 (회의록):
    /// - Common    → (1,1)  — 일반
    /// - Rare      → (2,1)  — 레어 (가로 우선)
    /// - Unique    → (2,2)  — 유니크
    /// - Legendary → (2,2)  — 전설
    ///
    /// 본 매핑은 디폴트 — RelicData._sizeLocked == true 인 SO 는 Editor 일괄 적용에서 제외 (수동 사이즈 유지).
    /// </summary>
    public static class RelicSizeMapping
    {
        public static Vector2Int GetDefaultSize(RelicRarity rarity) => rarity switch
        {
            RelicRarity.Common    => new Vector2Int(1, 1),
            RelicRarity.Rare      => new Vector2Int(2, 1),
            RelicRarity.Unique    => new Vector2Int(2, 2),
            RelicRarity.Legendary => new Vector2Int(2, 2),
            _                     => new Vector2Int(1, 1),
        };
    }
}
