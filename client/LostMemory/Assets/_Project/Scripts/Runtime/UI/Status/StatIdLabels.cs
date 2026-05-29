using LostMemory.Combat;
using LostMemory.Relics;
using UnityEngine;

namespace LostMemory.UI.Status
{
    /// <summary>
    /// StatId / RelicEffectType → 사용자용 한국어 라벨 + 1줄 설명 + 수치 포맷터.
    /// 상태창 Presenter 가 ViewModel 빌드 시 사용. 정적 switch 패턴 (RelicEffectLabels 와 동일).
    /// </summary>
    public static class StatIdLabels
    {
        /// <summary>StatId → 한국어 라벨. 예: AttackPower → "공격력"</summary>
        public static string LabelFor(StatId stat) => stat switch
        {
            StatId.AttackPower      => "공격력",
            StatId.AttackSpeed      => "공격 속도",
            StatId.MoveSpeed        => "이동 속도",
            StatId.MaxHealth        => "최대 체력",
            StatId.FinisherDamage   => "마무리 일격 피해",
            StatId.DashCooldown     => "대시 쿨타임",
            StatId.HealReceived     => "받는 회복량",
            StatId.Critical         => "치명타 확률",
            StatId.Cooldown         => "스킬 쿨타임",
            StatId.Range            => "공격 범위",
            StatId.Dodge            => "회피 확률",
            StatId.Defense          => "방어력",
            StatId.CriticalDamage   => "치명타 피해",
            StatId.ManaRegen        => "마나 회복",
            StatId.MaxHealthFlat    => "최대 체력",
            _                       => stat.ToString(),
        };

        /// <summary>StatId → 1줄 설명.</summary>
        public static string DescriptionFor(StatId stat) => stat switch
        {
            StatId.AttackPower      => "평타 데미지 배율",
            StatId.AttackSpeed      => "콤보 속도 단축",
            StatId.MoveSpeed        => "이동 속도",
            StatId.MaxHealth        => "최대 체력 배율 (유물)",
            StatId.FinisherDamage   => "3타 마무리 일격 추가 배율",
            StatId.DashCooldown     => "대시 쿨타임 단축 (음수 = 감소)",
            StatId.HealReceived     => "받는 회복량 배율",
            StatId.Critical         => "치명타 발생 확률",
            StatId.Cooldown         => "스킬 쿨타임 단축",
            StatId.Range            => "공격 범위 (체인 반경 / 바람 검기 길이 등)",
            StatId.Dodge            => "공격 회피 확률",
            StatId.Defense          => "받는 피해 감소 (flat)",
            StatId.CriticalDamage   => "치명타 적중 시 추가 피해",
            StatId.ManaRegen        => "초당 마나 회복량",
            StatId.MaxHealthFlat    => "재능으로 추가된 최대 체력 (flat)",
            _                       => "",
        };

        /// <summary>flat track 으로 합산해야 하는 StatId. true → GetTotalFlat, false → (GetTotalMultiplier-1)*100.</summary>
        public static bool IsFlat(StatId stat) => stat switch
        {
            StatId.Defense          => true,
            StatId.MaxHealthFlat    => true,
            _                       => false,
        };

        /// <summary>합산값을 사용자 표시 문자열로 포맷. magnitude=0 이면 빈 문자열.</summary>
        public static string FormatTotal(StatId stat, float total)
        {
            if (Mathf.Approximately(total, 0f)) return "";

            if (IsFlat(stat))
            {
                string sign = total > 0f ? "+" : "";
                return $"{sign}{total:0.#}";
            }

            float pct = total * 100f;
            string s = pct > 0f ? "+" : "";
            return $"{s}{pct:0.#}%";
        }

        /// <summary>
        /// base 가 의미 있는 stat 용 'Total (Base+Bonus)' 포맷터. 메이플 스타일.
        /// bonus 가 0 이면 분해 괄호 생략하고 total 만. 예: "100", "112 (100+12)".
        /// </summary>
        public static string FormatBaseAndBonus(StatId stat, float baseValue, float bonus, float total)
        {
            if (Mathf.Approximately(bonus, 0f))
                return $"{total:0.#}";

            string sign = bonus > 0f ? "+" : "";
            return $"{total:0.#} ({baseValue:0.#}{sign}{bonus:0.#})";
        }

        /// <summary>bonus 만 보이는 stat 가 항상 표시 대상일 때 0 도 명시. 예: "+0%", "+0".</summary>
        public static string FormatBonusOrZero(StatId stat, float bonus)
        {
            if (IsFlat(stat))
            {
                string sign = bonus >= 0f ? "+" : "";
                return $"{sign}{bonus:0.#}";
            }
            float pct = bonus * 100f;
            string s = pct >= 0f ? "+" : "";
            return $"{s}{pct:0.#}%";
        }

        /// <summary>OnHit 효과 1줄 설명 (RelicEffectLabels.LabelFor 가 이름은 제공, 본 메서드는 설명).</summary>
        public static string OnHitDescriptionFor(RelicEffectType type) => type switch
        {
            RelicEffectType.BurnOnHit       => "공격 시 적에게 화상 도트 부여",
            RelicEffectType.SlowOnHit       => "공격 시 적 이동속도 둔화",
            RelicEffectType.FreezeOnHit     => "공격 시 적 빙결 (행동 정지)",
            RelicEffectType.ChainOnHit      => "공격 시 주변 적에게 전기 연쇄",
            RelicEffectType.WindAOE         => "공격 시 적 너머로 바람 검기",
            _                               => "",
        };

        /// <summary>RelicEffectType 이 OnHit 류인지. 상태창 효과 섹션 필터링용.</summary>
        public static bool IsOnHitEffect(RelicEffectType type) => type switch
        {
            RelicEffectType.BurnOnHit       => true,
            RelicEffectType.SlowOnHit       => true,
            RelicEffectType.FreezeOnHit     => true,
            RelicEffectType.ChainOnHit      => true,
            RelicEffectType.WindAOE         => true,
            _                               => false,
        };
    }
}
