namespace LostMemory.Relics
{
    /// <summary>
    /// CL-151: 인벤토리 정리 (Sort) 기준.
    ///
    /// - RarityDesc: 등급 내림차순 (전설 → 유니크 → 레어 → 일반)
    /// - SizeDesc: 점유 칸 수 내림차순 (큰 것 먼저, fragmentation 회피)
    /// - RarityThenSize: 등급 ↓ 후 같은 등급 내 사이즈 ↓ (기본값, 회의록 추천 "둘 다 토글" 의 합리적 디폴트)
    /// </summary>
    public enum InventorySortMode
    {
        RarityDesc,
        SizeDesc,
        RarityThenSize,
    }
}
