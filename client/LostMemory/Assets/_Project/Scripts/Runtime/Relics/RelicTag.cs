namespace LostMemory.Relics
{
    /// <summary>
    /// 아이템(유물) 세트 태그. CL-138: 16세트 + None.
    /// 1 아이템 = 2 태그(듀얼) 기여 (RelicData.TagPrimary / TagSecondary).
    /// None = 0 → 단일 태그 아이템 또는 소모품.
    /// 값 변경 시 기존 SO 직렬화 영향 — 마이그레이션 필요.
    /// </summary>
    public enum RelicTag
    {
        None = 0,        // 단일 태그 / 소모품

        // 평타 친화 (5)
        AttackSpeed,     // 공속
        Critical,        // 치명타
        AttackPower,     // 일반뎀
        Ice,             // 얼음
        Lightning,       // 전기

        // 스킬 친화 (3)
        Cooldown,        // 쿨감
        Fire,            // 불
        Wind,            // 바람

        // 자동 발동 (1)
        MagicalGirl,     // 미소녀

        // 공통 (7)
        Health,          // 체력
        Defense,         // 방어력
        Dodge,           // 회피
        Range,           // 범위
        Luck,            // 행운
        Greed,           // 탐욕
        Tarot,           // 타로
    }
}
