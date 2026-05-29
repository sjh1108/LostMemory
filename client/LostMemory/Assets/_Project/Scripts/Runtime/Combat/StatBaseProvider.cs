using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Combat
{
    /// <summary>
    /// Phase A: 상태창 표시용으로 (Base, Bonus, Total) 을 산출.
    /// PlayerHealthStatApplier.CaptureBaseIfNeeded() / PlayerMovementStatApplier 와 동일한
    /// 역산 패턴 — TDE 컴포넌트의 inspector-set base 값 + container modifier 로 effective 값을 만든다.
    ///
    /// Phase A 에서 HasBase=true 로 처리하는 stat 는 HP / MoveSpeed 둘 뿐.
    /// AttackPower / Critical / Defense / CriticalDamage 등은 "base" 가 100% 또는 0 으로 의미 약해
    /// HasBase=false 로 폴백 (bonus 만 표시).
    /// </summary>
    public static class StatBaseProvider
    {
        public readonly struct StatDisplayValue
        {
            public readonly float Base;
            public readonly float Bonus;
            public readonly float Total;
            public readonly bool HasBase;

            public StatDisplayValue(float baseValue, float bonus, float total, bool hasBase)
            {
                Base = baseValue;
                Bonus = bonus;
                Total = total;
                HasBase = hasBase;
            }

            public static StatDisplayValue BonusOnly(float bonus) => new(0f, bonus, bonus, false);
        }

        /// <summary>
        /// stat 별 effective 값 산출. character 또는 container 가 null 이면 안전하게 BonusOnly fallback.
        /// </summary>
        public static StatDisplayValue Resolve(StatId stat, Character character, PlayerStatModifierContainer container)
        {
            switch (stat)
            {
                case StatId.MaxHealth:
                case StatId.MaxHealthFlat:
                    return ResolveMaxHealth(character, container);
                case StatId.MoveSpeed:
                    return ResolveMoveSpeed(character, container);
                default:
                    return ResolveBonusOnly(stat, container);
            }
        }

        // PlayerHealthStatApplier 공식 그대로:
        //   MaximumHealth = (baseMaxHealth + flatBonus) * multiplier
        // base = MaximumHealth / multiplier - flatBonus
        private static StatDisplayValue ResolveMaxHealth(Character character, PlayerStatModifierContainer container)
        {
            if (character == null) return StatDisplayValue.BonusOnly(0f);
            Health health = character.GetComponentInChildren<Health>(includeInactive: true)
                            ?? character.GetComponentInParent<Health>();
            if (health == null) return StatDisplayValue.BonusOnly(0f);

            float flatBonus = container != null ? container.GetTotalFlat(StatId.MaxHealthFlat) : 0f;
            float mul = container != null ? container.GetTotalMultiplier(StatId.MaxHealth) : 1f;
            float total = health.MaximumHealth;
            float baseValue = mul > 0f ? (total / mul) - flatBonus : total;
            float bonus = total - baseValue;
            return new StatDisplayValue(baseValue, bonus, total, hasBase: true);
        }

        // PlayerMovementStatApplier 가 매 LateUpdate 마다 CharacterMovement.MovementSpeedMultiplier 갱신.
        // effective = MovementSpeed (inspector base) × MovementSpeedMultiplier (container 산물).
        private static StatDisplayValue ResolveMoveSpeed(Character character, PlayerStatModifierContainer container)
        {
            if (character == null) return StatDisplayValue.BonusOnly(0f);
            CharacterMovement cm = character.GetComponentInChildren<CharacterMovement>(includeInactive: true)
                                   ?? character.GetComponentInParent<CharacterMovement>();
            if (cm == null) return StatDisplayValue.BonusOnly(0f);

            float mul = container != null ? container.GetTotalMultiplier(StatId.MoveSpeed) : 1f;
            float baseValue = cm.MovementSpeed;
            float total = baseValue * mul;
            float bonus = total - baseValue;
            return new StatDisplayValue(baseValue, bonus, total, hasBase: true);
        }

        // base 가 의미 없는 stat — bonus 만 산출.
        // flat stat (Defense / MaxHealthFlat — StatIdLabels.IsFlat 와 동일 목록): bonus = GetTotalFlat
        // 그 외 (% 류): bonus = GetTotalMultiplier - 1
        // 의존성 방향상 UI 의 StatIdLabels 를 참조하지 않고 같은 분기를 inline.
        private static StatDisplayValue ResolveBonusOnly(StatId stat, PlayerStatModifierContainer container)
        {
            if (container == null) return StatDisplayValue.BonusOnly(0f);
            bool isFlat = stat == StatId.Defense || stat == StatId.MaxHealthFlat;
            float bonus = isFlat
                ? container.GetTotalFlat(stat)
                : container.GetTotalMultiplier(stat) - 1f;
            return StatDisplayValue.BonusOnly(bonus);
        }
    }
}
