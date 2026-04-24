namespace LostMemory.Talents
{
    /// <summary>
    /// TalentModel의 투자 현황을 RunStartStats(런 시작 스탯 증가분)으로 변환하는 계산기.
    /// 공식: 스탯 증가량 = 투자 포인트 × IncreasePerPoint
    /// </summary>
    public static class TalentCalculator
    {
        /// <summary>
        /// TalentModel의 현재 투자 상태를 런 시작 스탯으로 변환한다.
        /// </summary>
        /// <param name="model">재능 포인트 분배 상태</param>
        /// <returns>5개 스탯의 증가분을 담은 RunStartStats</returns>
        public static RunStartStats Calculate(TalentModel model)
        {
            return new RunStartStats
            {
                CriticalRate = model.GetStatValue(TalentType.CriticalRate),
                AttackSpeed  = model.GetStatValue(TalentType.AttackSpeed),
                Defense      = model.GetStatValue(TalentType.Defense),
                ManaRegen    = model.GetStatValue(TalentType.ManaRegen),
                MaxHealth    = model.GetStatValue(TalentType.MaxHealth),
            };
        }
    }
}
