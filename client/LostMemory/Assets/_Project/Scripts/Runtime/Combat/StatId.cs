namespace LostMemory.Combat
{
    /// <summary>
    /// PlayerStatModifierContainer 가 관리하는 스탯 종류.
    /// 후속 Wave 의 hook 이 GetTotalMultiplier(StatId) 로 조회.
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
    }
}
