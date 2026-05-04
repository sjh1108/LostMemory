using LostMemory.Combat;
using UnityEngine;

namespace LostMemory.Relics
{
    /// <summary>
    /// CL-140: 세트 효과 라우터. <see cref="BuildManager.OnSetTierChanged"/> 이벤트를 받아
    /// <see cref="SetTier.EffectType"/> 별로 적절한 시스템에 modifier 등록/해제를 위임한다.
    ///
    /// 처리 흐름:
    /// 1. oldTier ≥ 0 이면 이전 티어 효과를 SetEffectSource(set, oldTier) 매칭으로 제거
    /// 2. newTier ≥ 0 이면 새 티어 효과를 EffectType 분기로 적용
    ///
    /// 본 CL 시점 라우팅 본격 처리: StatModifier 8개 EffectType (AttackPower/MaxHealth/MoveSpeed/
    /// Critical/Cooldown/Range/Dodge/DefenseFlat). 그 외 OnHit / 미소녀 / 시스템 hook 11개는
    /// <c>Debug.LogWarning</c> 골격만 남기고 CL-142~147 에서 각자 case 채움.
    ///
    /// Run 종료 처리는 BuildManager 가 모든 활성 티어를 -1 로 떨어뜨리며 OnSetTierChanged 다중
    /// 발화하므로 자연스럽게 RemoveTierEffect 가 각 set 마다 호출됨 → 별도 ClearAll 불필요.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Relics/Set Effect Applicator")]
    public sealed class SetEffectApplicator : MonoBehaviour
    {
        [Tooltip("같은 GameObject 의 BuildManager.")]
        [SerializeField] private BuildManager buildManager;

        [Tooltip("같은 GameObject 의 PlayerStatModifierContainer. StatModifier 라우팅 대상.")]
        [SerializeField] private PlayerStatModifierContainer statContainer;

        [Tooltip("티어 변경 시 라우팅 로그 (APPLY/REMOVE).")]
        [SerializeField] private bool _logEffectDispatch = false;

        // RelicEffectRegistry / BuildManager 와 동일 패턴.
        private readonly IRelicEffectAuthority _authority = new NetworkRelicEffectAuthority();

        private void OnEnable()
        {
            if (buildManager == null)
            {
                Debug.LogError($"[SetEffectApplicator] buildManager null. Inspector wiring 필요. host={gameObject.name}", this);
                return;
            }
            if (statContainer == null)
            {
                Debug.LogError($"[SetEffectApplicator] statContainer null. Inspector wiring 필요. host={gameObject.name}", this);
                return;
            }
            buildManager.OnSetTierChanged += HandleSetTierChanged;
        }

        private void OnDisable()
        {
            if (buildManager == null) return;
            buildManager.OnSetTierChanged -= HandleSetTierChanged;
        }

        private void HandleSetTierChanged(RelicTag tag, int oldTier, int newTier)
        {
            if (!_authority.IsAuthority) return;
            BuildSetData set = buildManager.GetSetForTag(tag);
            if (set == null) return;

            // 1) 이전 티어 효과 제거 (활성이었으면)
            if (oldTier >= 0 && oldTier < set.Tiers.Count)
            {
                RemoveTierEffect(set, oldTier, set.Tiers[oldTier]);
            }
            // 2) 새 티어 효과 적용 (활성이면)
            if (newTier >= 0 && newTier < set.Tiers.Count)
            {
                ApplyTierEffect(set, newTier, set.Tiers[newTier]);
            }
        }

        private void ApplyTierEffect(BuildSetData set, int tierIndex, SetTier tier)
        {
            if (_logEffectDispatch)
                Debug.Log($"[SetEffect] APPLY {set.SetTag} t{tierIndex}: {tier.EffectType} mag={tier.Magnitude}");

            var source = new SetEffectSource(set, tierIndex);
            switch (tier.EffectType)
            {
                // ── StatModifier 라우팅 (CL-140 본격 처리, CL-141 에서 AttackSpeed 추가) ──
                case RelicEffectType.AttackPowerPercent:
                    statContainer.AddPermanent(StatId.AttackPower, tier.Magnitude, source); break;
                case RelicEffectType.AttackSpeedPercent:
                    statContainer.AddPermanent(StatId.AttackSpeed, tier.Magnitude, source); break;
                case RelicEffectType.MaxHealthPercent:
                    statContainer.AddPermanent(StatId.MaxHealth, tier.Magnitude, source); break;
                case RelicEffectType.MoveSpeedPercent:
                    statContainer.AddPermanent(StatId.MoveSpeed, tier.Magnitude, source); break;
                case RelicEffectType.CriticalChancePercent:
                    statContainer.AddPermanent(StatId.Critical, tier.Magnitude, source); break;
                case RelicEffectType.CooldownReductionPercent:
                    statContainer.AddPermanent(StatId.Cooldown, tier.Magnitude, source); break;
                case RelicEffectType.AttackRangePercent:
                    statContainer.AddPermanent(StatId.Range, tier.Magnitude, source); break;
                case RelicEffectType.DodgeChancePercent:
                    statContainer.AddPermanent(StatId.Dodge, tier.Magnitude, source); break;
                case RelicEffectType.DefenseFlat:
                    // 주의: PlayerStatModifierContainer 는 % 합산. flat 의미는 CL-146 에서 정책 결정.
                    statContainer.AddPermanent(StatId.Defense, tier.Magnitude, source); break;

                // ── 미존재 시스템 (TODO — 후속 CL) ──
                case RelicEffectType.BurnOnHit:
                case RelicEffectType.SlowOnHit:
                case RelicEffectType.FreezeOnHit:
                case RelicEffectType.ChainOnHit:
                case RelicEffectType.WindAOE:
                    Debug.LogWarning($"[SetEffectApplicator] {tier.EffectType} OnHit 라우팅 미구현 (CL-142/143)");
                    break;
                case RelicEffectType.MagicalGirlSummon:
                case RelicEffectType.MagicalGirlFusion:
                case RelicEffectType.MagicalGirlElementalAttack:
                case RelicEffectType.MagicalGirlElementalEnhanced:
                    Debug.LogWarning($"[SetEffectApplicator] {tier.EffectType} 미소녀 라우팅 미구현 (CL-144/145)");
                    break;
                case RelicEffectType.GoldGainPercent:
                case RelicEffectType.LuckPoints:
                case RelicEffectType.TarotProc:
                case RelicEffectType.LuckSlotExpand:
                case RelicEffectType.LuckLegendaryGuarantee:
                    Debug.LogWarning($"[SetEffectApplicator] {tier.EffectType} 시스템 hook 미구현 (CL-146/147/148)");
                    break;

                case RelicEffectType.None:
                    // 의도된 미설정 — 무동작
                    break;

                // RelicEffectRegistry 가 처리하는 단일 유물 효과 enum (세트 효과로는 사용 안 함):
                // AttackSpeedOnKillTimed / FinisherDamagePercent / AttackPowerConditional /
                // ShieldOnParry / HealReceivedPercent / DashCooldownPercent /
                // MoveSpeedAfterDashTimed / HealConsumablePercent
                default:
                    // 본 CL 책임 외. 미경고 (스팸 방지).
                    break;
            }
        }

        private void RemoveTierEffect(BuildSetData set, int tierIndex, SetTier tier)
        {
            if (_logEffectDispatch)
                Debug.Log($"[SetEffect] REMOVE {set.SetTag} t{tierIndex}: {tier.EffectType}");

            var source = new SetEffectSource(set, tierIndex);

            // StatModifier 케이스: source 매칭으로 일괄 제거.
            // 본 CL 시점 미존재 시스템 (OnHit/MagicalGirl/Tarot 등) 의 정리는 후속 CL 이
            // 각 시스템에 RemoveBySource 등 추가하며 본 메서드에 분기 추가 예정.
            statContainer.RemoveBySource(source);
        }
    }
}
