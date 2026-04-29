using System.Collections.Generic;
using LostMemory.Combat;
using LostMemory.Data;
using LostMemory.TestKhi;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Relics
{
    /// <summary>
    /// CL-107 Wave B 임시 어댑터. PlayerRelicInventory.OnRelicAcquired 구독 →
    /// EffectType 에 따라 PlayerStatModifierContainer 에 modifier 등록.
    /// AttackSpeedOnKillTimed (붉은송곳니) 는 KhiMeleeComboController.EnemyKilledByPlayer 발화 시 AddTimed.
    /// CL-109 의 RelicEffectRegistry 가 본 컴포넌트를 흡수 예정.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Relics/Relic Effect Applier")]
    public sealed class RelicEffectApplier : MonoBehaviour
    {
        [SerializeField] private PlayerRelicInventory inventory;
        [SerializeField] private PlayerStatModifierContainer container;
        [Tooltip("AttackPowerConditional (전투북) 의 HP 비율 평가용. TDE Health.")]
        [SerializeField] private Health playerHealth;
        [Tooltip("AttackSpeedOnKillTimed (붉은송곳니) 의 적 처치 신호 구독용.")]
        [SerializeField] private KhiMeleeComboController combatController;

        private readonly List<OnKillSubscription> _onKillSubscriptions = new();

        private void OnEnable()
        {
            if (inventory != null) inventory.OnRelicAcquired += HandleAcquired;
            if (combatController != null) combatController.EnemyKilledByPlayer += HandleEnemyKilled;
        }

        private void OnDisable()
        {
            if (inventory != null) inventory.OnRelicAcquired -= HandleAcquired;
            if (combatController != null) combatController.EnemyKilledByPlayer -= HandleEnemyKilled;
        }

        private void HandleAcquired(RelicData relic)
        {
            if (relic == null || container == null) return;
            switch (relic.EffectType)
            {
                case RelicEffectType.AttackPowerPercent:
                    container.AddPermanent(StatId.AttackPower, relic.Magnitude, relic);
                    break;
                case RelicEffectType.FinisherDamagePercent:
                    container.AddPermanent(StatId.FinisherDamage, relic.Magnitude, relic);
                    break;
                case RelicEffectType.AttackPowerConditional:
                    float threshold = relic.Threshold;
                    container.AddConditional(
                        StatId.AttackPower, relic.Magnitude,
                        () => playerHealth != null && playerHealth.MaximumHealth > 0f
                              && playerHealth.CurrentHealth / playerHealth.MaximumHealth >= threshold,
                        relic);
                    break;
                case RelicEffectType.MoveSpeedPercent:
                    container.AddPermanent(StatId.MoveSpeed, relic.Magnitude, relic);
                    break;
                case RelicEffectType.MaxHealthPercent:
                    container.AddPermanent(StatId.MaxHealth, relic.Magnitude, relic);
                    break;
                case RelicEffectType.AttackSpeedOnKillTimed:
                    _onKillSubscriptions.Add(new OnKillSubscription
                    {
                        Magnitude = relic.Magnitude,
                        Duration = relic.Duration,
                        Source = relic,
                    });
                    break;
                // 그 외 (None / Shield / Heal / Dash / 회복약 등) 은 후속 Wave (CL-108 ~ 109) 분담.
            }
        }

        private void HandleEnemyKilled(KhiAttackRequest req, AttackStepData step, Health victim)
        {
            if (container == null) return;
            for (int i = 0; i < _onKillSubscriptions.Count; i++)
            {
                OnKillSubscription s = _onKillSubscriptions[i];
                container.AddTimed(StatId.AttackSpeed, s.Magnitude, s.Duration, s.Source);
            }
        }

        private struct OnKillSubscription
        {
            public float Magnitude;
            public float Duration;
            public RelicData Source;
        }
    }
}
