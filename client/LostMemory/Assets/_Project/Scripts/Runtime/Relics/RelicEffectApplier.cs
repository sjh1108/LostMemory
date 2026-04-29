using System.Collections.Generic;
using LostMemory.Combat;
using LostMemory.Data;
using LostMemory.TestKhi;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Relics
{
    /// <summary>
    /// CL-107 Wave B + CL-108 Wave C 통합 어댑터. PlayerRelicInventory.OnRelicAcquired 구독 →
    /// EffectType 에 따라 PlayerStatModifierContainer / PlayerShield 에 modifier 등록.
    /// 시간부 효과는 외부 이벤트 (EnemyKilledByPlayer / ParrySucceeded / OnDashEnded) 발화 시 AddTimed.
    /// CL-109 의 RelicEffectRegistry 가 본 컴포넌트를 흡수 예정.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Relics/Relic Effect Applier")]
    public sealed class RelicEffectApplier : MonoBehaviour
    {
        [SerializeField] private PlayerRelicInventory inventory;
        [SerializeField] private PlayerStatModifierContainer container;
        [Tooltip("AttackPowerConditional (전투북) 의 HP 비율 평가용 + ShieldOnParry 의 maxHp 기준값. TDE Health.")]
        [SerializeField] private Health playerHealth;
        [Tooltip("AttackSpeedOnKillTimed (붉은송곳니) 의 적 처치 신호 구독용.")]
        [SerializeField] private KhiMeleeComboController combatController;
        [Tooltip("CL-108: ShieldOnParry (반격의 표식) 의 패링 성공 신호 구독용.")]
        [SerializeField] private KhiParryController parryController;
        [Tooltip("CL-108: MoveSpeedAfterDashTimed (추적자의 망토) 의 대시 종료 신호 구독용.")]
        [SerializeField] private KhiDashController dashController;
        [Tooltip("CL-108: ShieldOnParry 의 보호막 부여 대상.")]
        [SerializeField] private PlayerShield playerShield;

        private readonly List<OnKillSubscription> _onKillSubscriptions = new();
        private readonly List<TimedSubscription> _onParrySuccessSubscriptions = new();
        private readonly List<TimedSubscription> _onDashEndSubscriptions = new();

        private void OnEnable()
        {
            if (inventory != null) inventory.OnRelicAcquired += HandleAcquired;
            if (combatController != null) combatController.EnemyKilledByPlayer += HandleEnemyKilled;
            if (parryController != null) parryController.ParrySucceeded += HandleParrySuccess;
            if (dashController != null) dashController.OnDashEnded += HandleDashEnded;
        }

        private void OnDisable()
        {
            if (inventory != null) inventory.OnRelicAcquired -= HandleAcquired;
            if (combatController != null) combatController.EnemyKilledByPlayer -= HandleEnemyKilled;
            if (parryController != null) parryController.ParrySucceeded -= HandleParrySuccess;
            if (dashController != null) dashController.OnDashEnded -= HandleDashEnded;
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

                // CL-108 Wave C
                case RelicEffectType.ShieldOnParry:
                    _onParrySuccessSubscriptions.Add(new TimedSubscription
                    {
                        Magnitude = relic.Magnitude,
                        Duration = relic.Duration,
                        Source = relic,
                    });
                    break;
                case RelicEffectType.HealReceivedPercent:
                    container.AddPermanent(StatId.HealReceived, relic.Magnitude, relic);
                    break;
                case RelicEffectType.DashCooldownPercent:
                    // 음수 magnitude (-0.12) 정상. PlayerStatModifierContainer 합산 정책상 1 + (-0.12) = 0.88.
                    container.AddPermanent(StatId.DashCooldown, relic.Magnitude, relic);
                    break;
                case RelicEffectType.MoveSpeedAfterDashTimed:
                    _onDashEndSubscriptions.Add(new TimedSubscription
                    {
                        Magnitude = relic.Magnitude,
                        Duration = relic.Duration,
                        Source = relic,
                    });
                    break;
                case RelicEffectType.HealConsumablePercent:
                    // CL-108 옵션 C: 회복약은 인벤토리 보관만. 사용 트리거는 PlayerHealing.UseConsumable
                    // (후속 CL 의 사용 UI 또는 본 CL 의 디버그 ContextMenu). 여기 case 는 no-op.
                    break;
                // None: Wave A 시점 미구현 효과 (비-MVP, 랜덤박스 등). 후속 ticket 분담.
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

        private void HandleParrySuccess()
        {
            if (playerShield == null || playerHealth == null) return;
            float maxHp = playerHealth.MaximumHealth;
            for (int i = 0; i < _onParrySuccessSubscriptions.Count; i++)
            {
                TimedSubscription s = _onParrySuccessSubscriptions[i];
                playerShield.GrantShield(maxHp * s.Magnitude, s.Duration);
            }
        }

        private void HandleDashEnded()
        {
            if (container == null) return;
            for (int i = 0; i < _onDashEndSubscriptions.Count; i++)
            {
                TimedSubscription s = _onDashEndSubscriptions[i];
                container.AddTimed(StatId.MoveSpeed, s.Magnitude, s.Duration, s.Source);
            }
        }

        private struct OnKillSubscription
        {
            public float Magnitude;
            public float Duration;
            public RelicData Source;
        }

        private struct TimedSubscription
        {
            public float Magnitude;
            public float Duration;
            public RelicData Source;
        }
    }
}
