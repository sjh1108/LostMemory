using UnityEngine;

namespace LostMemory.Relics
{
    /// <summary>
    /// RelicEffectType → 사용자용 한국어 라벨 + 수치 포맷터.
    /// Tooltip / SetSummaryBar 가 동일 매핑을 공유한다.
    /// </summary>
    public static class RelicEffectLabels
    {
        /// <summary>한 줄짜리 효과 표기. 미구현/0 수치는 빈 문자열.</summary>
        public static string Format(EffectEntry entry)
        {
            string name = LabelFor(entry.Type);
            if (string.IsNullOrEmpty(name)) return "";
            string value = FormatValue(entry);
            return string.IsNullOrEmpty(value) ? name : $"{name} {value}";
        }

        public static string LabelFor(RelicEffectType type) => type switch
        {
            RelicEffectType.None                         => "",
            RelicEffectType.AttackPowerPercent           => "공격력",
            RelicEffectType.AttackPowerConditional       => "조건부 공격력",
            RelicEffectType.AttackSpeedPercent           => "공격 속도",
            RelicEffectType.AttackSpeedOnKillTimed       => "처치 시 공격 속도",
            RelicEffectType.FinisherDamagePercent        => "마무리 일격 피해",
            RelicEffectType.MaxHealthPercent             => "최대 체력",
            RelicEffectType.ShieldOnParry                => "패링 시 보호막",
            RelicEffectType.HealReceivedPercent          => "받는 회복량",
            RelicEffectType.MoveSpeedPercent             => "이동 속도",
            RelicEffectType.DashCooldownPercent          => "대시 쿨타임",
            RelicEffectType.MoveSpeedAfterDashTimed      => "대시 후 이동 속도",
            RelicEffectType.HealConsumablePercent        => "체력 회복",
            RelicEffectType.CriticalChancePercent        => "치명타 확률",
            RelicEffectType.CriticalDamagePercent        => "치명타 피해",
            RelicEffectType.CooldownReductionPercent     => "쿨타임 감소",
            RelicEffectType.AttackRangePercent           => "공격 범위",
            RelicEffectType.DodgeChancePercent           => "회피 확률",
            RelicEffectType.DefenseFlat                  => "방어력",
            RelicEffectType.GoldGainPercent              => "골드 획득",
            RelicEffectType.LuckPoints                   => "행운",
            RelicEffectType.BurnOnHit                    => "적중 시 화상",
            RelicEffectType.SlowOnHit                    => "적중 시 둔화",
            RelicEffectType.FreezeOnHit                  => "적중 시 빙결",
            RelicEffectType.ChainOnHit                   => "적중 시 연쇄",
            RelicEffectType.WindAOE                      => "바람 광역 공격",
            RelicEffectType.MagicalGirlSummon            => "미소녀 소환",
            RelicEffectType.MagicalGirlFusion            => "미소녀 합체",
            RelicEffectType.MagicalGirlElementalAttack   => "미소녀 속성 공격",
            RelicEffectType.MagicalGirlElementalEnhanced => "미소녀 강화 속성 공격",
            RelicEffectType.TarotProc                    => "타로 발동",
            RelicEffectType.LuckSlotExpand               => "인벤토리 슬롯 확장",
            RelicEffectType.LuckLegendaryGuarantee       => "전설 등급 확정",
            RelicEffectType.TarotEffectMultiplier        => "타로 효과 강화",
            _                                            => type.ToString(),
        };

        private static bool IsFlatValue(RelicEffectType type) => type switch
        {
            RelicEffectType.DefenseFlat              => true,
            RelicEffectType.LuckPoints               => true,
            RelicEffectType.ShieldOnParry            => true,
            RelicEffectType.LuckSlotExpand           => true,
            RelicEffectType.LuckLegendaryGuarantee   => true,
            RelicEffectType.MagicalGirlSummon        => true,
            RelicEffectType.MagicalGirlFusion        => true,
            RelicEffectType.MagicalGirlElementalAttack    => true,
            RelicEffectType.MagicalGirlElementalEnhanced  => true,
            RelicEffectType.TarotProc                => true,
            _                                        => false,
        };

        private static string FormatValue(EffectEntry entry)
        {
            if (Mathf.Approximately(entry.Magnitude, 0f)) return "";

            if (IsFlatValue(entry.Type))
            {
                string sign = entry.Magnitude > 0f ? "+" : "";
                return $"{sign}{entry.Magnitude:0.#}";
            }

            float pct  = entry.Magnitude * 100f;
            string s   = pct > 0f ? "+" : "";
            return $"{s}{pct:0.#}%";
        }
    }
}
