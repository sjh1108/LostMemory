using LostMemory.Combat;
using LostMemory.MagicalGirl;
using LostMemory.Stage;
using LostMemory.Tarot;
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

        [Tooltip("같은 GameObject 의 OnHitEffectRegistry. CL-142 OnHit 라우팅 대상 (Slow/Freeze/Chain).")]
        [SerializeField] private OnHitEffectRegistry onHitRegistry;

        [Tooltip("같은 GameObject 의 MagicalGirlSpawner. CL-144 미소녀 라우팅 대상 (MagicalGirlSummon).")]
        [SerializeField] private MagicalGirlSpawner magicalGirlSpawner;

        [Tooltip("CL-146 탐욕 set 효과 (GoldGainPercent) 라우팅 — GoldWallet.SetGainMultiplier 호출용.")]
        [SerializeField] private GoldWallet goldWallet;

        [Tooltip("CL-146 행운 3스택 (LuckSlotExpand) 라우팅 — PlayerRelicInventory.AddSlots 호출용.")]
        [SerializeField] private PlayerRelicInventory playerRelicInventory;

        [Tooltip("CL-147 타로 (TarotProc/TarotEffectMultiplier) 라우팅 — TarotSystem 활성/multiplier 전달.")]
        [SerializeField] private TarotSystem tarotSystem;

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
            // CL-142 진단: OnHit wiring 상태 즉시 표시
            string onHitWiring = onHitRegistry != null
                ? $"OK (host={onHitRegistry.gameObject.name})"
                : "❌ NULL (Slow/Freeze/Chain 동작 안 함)";
            string girlWiring = magicalGirlSpawner != null
                ? $"OK (host={magicalGirlSpawner.gameObject.name})"
                : "❌ NULL (미소녀 동작 안 함)";
            string goldWiring = goldWallet != null ? "OK" : "⚠ NULL (탐욕 적용 X)";
            string invWiring = playerRelicInventory != null ? "OK" : "⚠ NULL (행운 슬롯 적용 X)";
            string tarotWiring = tarotSystem != null ? "OK" : "⚠ NULL (타로 적용 X)";
            Debug.Log($"[SetEffectApplicator] OnEnable — wiring: buildManager=OK, statContainer=OK, onHitRegistry={onHitWiring}, magicalGirlSpawner={girlWiring}, goldWallet={goldWiring}, playerRelicInventory={invWiring}, tarotSystem={tarotWiring}", this);
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
                case RelicEffectType.CriticalDamagePercent:
                    statContainer.AddPermanent(StatId.CriticalDamage, tier.Magnitude, source); break;
                case RelicEffectType.CooldownReductionPercent:
                    // SO 의 Magnitude 는 양수(0.15 = -15% 감소). multiplier 로는 1 + (-0.15) = 0.85 가 되도록 부호 반전.
                    statContainer.AddPermanent(StatId.Cooldown, -tier.Magnitude, source);
                    Debug.LogWarning("[CL-143] StatId.Cooldown 적용됨, but no skill system yet — CL-160 후속 hook 에서 GetTotalMultiplier(Cooldown) 로 적용 예정.");
                    break;
                case RelicEffectType.AttackRangePercent:
                    statContainer.AddPermanent(StatId.Range, tier.Magnitude, source); break;
                case RelicEffectType.DodgeChancePercent:
                    statContainer.AddPermanent(StatId.Dodge, tier.Magnitude, source); break;
                case RelicEffectType.DefenseFlat:
                    // 주의: PlayerStatModifierContainer 는 % 합산. flat 의미는 CL-146 에서 정책 결정.
                    statContainer.AddPermanent(StatId.Defense, tier.Magnitude, source); break;

                // ── OnHit 라우팅 (CL-142 본격 처리, CL-143 에서 BurnOnHit/WindAOE 추가) ──
                case RelicEffectType.SlowOnHit:
                case RelicEffectType.FreezeOnHit:
                case RelicEffectType.ChainOnHit:
                case RelicEffectType.BurnOnHit:
                case RelicEffectType.WindAOE:
                    if (onHitRegistry != null)
                        onHitRegistry.Register(tier.EffectType, tier.Magnitude, tier.Duration, source);
                    else
                        Debug.LogWarning($"[SetEffectApplicator] onHitRegistry null — {tier.EffectType} 적용 X. Inspector wiring 필요.");
                    break;
                // ── 미소녀 라우팅 (CL-204: SetCount 의미 = setBonus/ultimate flag) ──
                // CL-144 → CL-204 변화: SetCount(N) 가 더 이상 미소녀 spawn/despawn 하지 않음.
                // Spawner 가 inventory.OnRelicAcquired hook 에서 visual 단위로 spawn 관리.
                // SetCount 는 N>=5 → setBonus + ultimate available 플래그만 갱신.
                case RelicEffectType.MagicalGirlSummon:
                    if (magicalGirlSpawner != null)
                        magicalGirlSpawner.SetCount(tier.RequiredCount);
                    else
                        Debug.LogWarning("[SetEffectApplicator] magicalGirlSpawner null — MagicalGirlSummon 적용 X. Inspector wiring 필요.");
                    break;
                case RelicEffectType.MagicalGirlFusion:
                    // CL-204: T5 의 RequiredCount=5 → SetCount(5) → 5세트 강화 + ultimate(T/Y) 활성
                    if (magicalGirlSpawner != null)
                        magicalGirlSpawner.SetCount(tier.RequiredCount);
                    else
                        Debug.LogWarning("[SetEffectApplicator] magicalGirlSpawner null — MagicalGirlFusion 적용 X. Inspector wiring 필요.");
                    break;
                case RelicEffectType.MagicalGirlElementalAttack:
                case RelicEffectType.MagicalGirlElementalEnhanced:
                    // 본 CL: set 효과로는 처리 안 함. 시각은 PlayerRelicInventory.OnRelicAcquired 직접 hook (MagicalGirlSpawner 내부).
                    // RelicData 개별 효과로 등록될 때 의미 있음 — 본 CL 의 SetEffectApplicator 분기는 no-op.
                    break;
                // ── CL-146 시스템 hook ──
                case RelicEffectType.GoldGainPercent:
                    if (goldWallet != null)
                        goldWallet.SetGainMultiplier(1f + tier.Magnitude);
                    else
                        Debug.LogWarning("[SetEffectApplicator] goldWallet null — GoldGainPercent 적용 X. Inspector wiring 필요.");
                    break;
                case RelicEffectType.LuckPoints:
                case RelicEffectType.LuckLegendaryGuarantee:
                    // RewardController 가 BuildManager 에서 직접 조회 (보상 시점) — case 자체는 no-op.
                    break;
                case RelicEffectType.LuckSlotExpand:
                    if (playerRelicInventory != null)
                        playerRelicInventory.AddSlots(1);
                    else
                        Debug.LogWarning("[SetEffectApplicator] playerRelicInventory null — LuckSlotExpand 적용 X. Inspector wiring 필요.");
                    break;
                case RelicEffectType.TarotProc:
                    if (tarotSystem != null)
                        tarotSystem.OnTarotActivated(tier);
                    else
                        Debug.LogWarning("[SetEffectApplicator] tarotSystem null — TarotProc 적용 X. Inspector wiring 필요.");
                    break;
                case RelicEffectType.TarotEffectMultiplier:
                    if (tarotSystem != null)
                        tarotSystem.SetEffectMultiplier(tier.Magnitude);
                    else
                        Debug.LogWarning("[SetEffectApplicator] tarotSystem null — TarotEffectMultiplier 적용 X.");
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
            statContainer.RemoveBySource(source);

            // CL-142: OnHit (Slow/Freeze/Chain) 도 같은 source 로 unregister.
            if (onHitRegistry != null)
                onHitRegistry.UnregisterBySource(source);

            // CL-204: MagicalGirlSummon/Fusion 의 SetCount 는 미소녀 destroy 안 함 (flag-only).
            // Tier 전환 시 일시적으로 SetCount(0) → setBonus/ultimate off → 직후 ApplyTierEffect 가 재계산.
            // 미소녀 본체 destroy 는 inventory.OnCleared (Run 종료) 가 처리.
            if ((tier.EffectType == RelicEffectType.MagicalGirlSummon
                 || tier.EffectType == RelicEffectType.MagicalGirlFusion)
                && magicalGirlSpawner != null)
                magicalGirlSpawner.SetCount(0);

            // CL-146: GoldGainPercent 비활성화 — multiplier 1.0 (기본) 으로 복구.
            // 다른 tier 가 동시 활성이면 ApplyTierEffect 가 즉시 새 multiplier 적용 → 안전.
            if (tier.EffectType == RelicEffectType.GoldGainPercent && goldWallet != null)
                goldWallet.SetGainMultiplier(1f);

            // CL-146: LuckSlotExpand 비활성화 — 보너스 슬롯 -1.
            if (tier.EffectType == RelicEffectType.LuckSlotExpand && playerRelicInventory != null)
                playerRelicInventory.AddSlots(-1);

            // CL-147: 타로 비활성화 — TarotProc 해제, multiplier 0 으로 복구.
            if (tarotSystem != null)
            {
                if (tier.EffectType == RelicEffectType.TarotProc)
                    tarotSystem.OnTarotDeactivated();
                else if (tier.EffectType == RelicEffectType.TarotEffectMultiplier)
                    tarotSystem.SetEffectMultiplier(0f);
            }
        }
    }
}
