namespace LostMemory.Talents
{
    /// <summary>
    /// 재능에서 계산된 런 시작 스탯 증가분.
    /// TalentCalculator.Calculate()로 생성하며, 런이 시작될 때 캐릭터 스탯에 더해진다.
    /// </summary>
    public struct RunStartStats
    {
        /// <summary>치명타 확률 증가량</summary>
        public float CriticalRate;

        /// <summary>공격 속도 증가량</summary>
        public float AttackSpeed;

        /// <summary>방어력 증가량</summary>
        public float Defense;

        /// <summary>마나 회복 증가량</summary>
        public float ManaRegen;

        /// <summary>최대 체력 증가량</summary>
        public float MaxHealth;
    }
}
