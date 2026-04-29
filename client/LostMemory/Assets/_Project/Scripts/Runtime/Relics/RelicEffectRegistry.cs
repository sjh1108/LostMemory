using System.Collections.Generic;
using LostMemory.Combat;
using LostMemory.Data;
using LostMemory.TestKhi;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Relics
{
    /// <summary>
    /// CL-107 Wave B + CL-108 Wave C + CL-109 Wave D 통합. 정식 *Relic Effect Registry* — effect 표 (effect table)
    /// 의 정식 진입점. PlayerRelicInventory.OnRelicAcquired 구독 → EffectType 에 따라 PlayerStatModifierContainer
    /// / PlayerShield 에 modifier 등록. 시간부 효과는 외부 이벤트 (EnemyKilledByPlayer / ParrySucceeded / OnDashEnded)
    /// 발화 시 AddTimed.
    ///
    /// CL-109 변경:
    /// - 이름 RelicEffectApplier → RelicEffectRegistry (정식 격상)
    /// - <see cref="IRelicEffectAuthority"/> 게이트 도입 (HandleAcquired + 모든 이벤트 핸들러 최상단)
    /// - PlayerRelicInventory.OnCleared 구독 → Run 종료 시 container/shield 일괄 정리
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Relics/Relic Effect Registry")]
    public sealed class RelicEffectRegistry : MonoBehaviour
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

        // CL-109: future-proof 권위 게이트. 현재 단일 플레이어라 항상 true.
        // 멀티 framework 도입 시 NetworkRelicEffectAuthority 로 이 라인만 교체.
        private readonly IRelicEffectAuthority _authority = new LocalRelicEffectAuthority();

        private readonly List<OnKillSubscription> _onKillSubscriptions = new();
        private readonly List<TimedSubscription> _onParrySuccessSubscriptions = new();
        private readonly List<TimedSubscription> _onDashEndSubscriptions = new();

        private void OnEnable()
        {
            if (inventory != null)
            {
                inventory.OnRelicAcquired += HandleAcquired;
                inventory.OnCleared += HandleRunCleared;
            }
            if (combatController != null) combatController.EnemyKilledByPlayer += HandleEnemyKilled;
            if (parryController != null) parryController.ParrySucceeded += HandleParrySuccess;
            if (dashController != null) dashController.OnDashEnded += HandleDashEnded;
        }

        private void OnDisable()
        {
            if (inventory != null)
            {
                inventory.OnRelicAcquired -= HandleAcquired;
                inventory.OnCleared -= HandleRunCleared;
            }
            if (combatController != null) combatController.EnemyKilledByPlayer -= HandleEnemyKilled;
            if (parryController != null) parryController.ParrySucceeded -= HandleParrySuccess;
            if (dashController != null) dashController.OnDashEnded -= HandleDashEnded;
        }

        private void HandleAcquired(RelicData relic)
        {
            if (!_authority.IsAuthority) return;
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
            if (!_authority.IsAuthority) return;
            if (container == null) return;
            for (int i = 0; i < _onKillSubscriptions.Count; i++)
            {
                OnKillSubscription s = _onKillSubscriptions[i];
                container.AddTimed(StatId.AttackSpeed, s.Magnitude, s.Duration, s.Source);
            }
        }

        private void HandleParrySuccess()
        {
            if (!_authority.IsAuthority) return;
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
            if (!_authority.IsAuthority) return;
            if (container == null) return;
            for (int i = 0; i < _onDashEndSubscriptions.Count; i++)
            {
                TimedSubscription s = _onDashEndSubscriptions[i];
                container.AddTimed(StatId.MoveSpeed, s.Magnitude, s.Duration, s.Source);
            }
        }

        /// <summary>
        /// CL-109: PlayerRelicInventory.OnCleared 구독. Run 종료 시 모든 modifier/shield 일괄 정리.
        /// </summary>
        private void HandleRunCleared()
        {
            if (!_authority.IsAuthority) return;
            if (container != null) container.ClearAll();
            if (playerShield != null) playerShield.ClearShield();
            _onKillSubscriptions.Clear();
            _onParrySuccessSubscriptions.Clear();
            _onDashEndSubscriptions.Clear();
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
