namespace LostMemory.Combat
{
    /// <summary>
    /// PlayerStatModifierContainer 가 관리하는 스탯 종류.
    /// 후속 Wave 의 hook 이 GetTotalMultiplier(StatId) 로 조회.
    /// 값 추가만 (인덱스 시프트 X) — 기존 SO 직렬화 안전.
    /// </summary>
    public enum StatId
    {
        AttackPower,        // 평타 데미지 배율
        AttackSpeed,        // 콤보 step duration 분모 단축 (Wave B)
        MoveSpeed,          // CharacterMovement.MovementSpeedMultiplier
        MaxHealth,          // Health.MaximumHealth (Wave B 시 1회 적용)
        FinisherDamage,     // 3타 마무리 추가 배율
        DashCooldown,       // CharacterDash2D Cooldown 단축
        HealReceived,       // PlayerHealing 회복량 배율 (Wave C)

        // ── CL-140 추가 (세트 효과 라우팅용) ──
        // 실제 소비처 hook (KhiMeleeComboController.Critical 등) 은 CL-142, CL-146 책임.
        Critical,           // 치명타 확률
        Cooldown,           // 스킬 쿨감
        Range,              // 공격 범위
        Dodge,              // 회피 확률
        Defense,            // 방어력 (flat — magnitude 의미가 % 와 다름. CL-146 정책 결정)
    }
}
