using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Combat
{
    /// <summary>
    /// CL-107: PlayerStatModifierContainer 의 MaxHealth multiplier 변경 시 TDE Health 의
    /// MaximumHealth 재계산 + CurrentHealth 비례 증가 (수호의 파편 등). CL-107 결정 #3.
    /// 정책: MaxHP × ratio 유지. 예) MaxHP 100→112 + CurrentHP 50 → CurrentHP 56 (50% 비율 유지).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Combat/Player Health Stat Applier")]
    public sealed class PlayerHealthStatApplier : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private PlayerStatModifierContainer container;

        private float _baseMaxHealth;
        private float _lastAppliedMul = 1f;

        private void OnEnable()
        {
            if (health == null || container == null) return;
            _baseMaxHealth = health.MaximumHealth;
            container.ModifiersChanged += HandleChanged;
        }

        private void OnDisable()
        {
            if (container != null) container.ModifiersChanged -= HandleChanged;
        }

        private void HandleChanged(StatId stat)
        {
            if (health == null || container == null) return;
            float newMul = container.GetTotalMultiplier(StatId.MaxHealth);
            if (Mathf.Approximately(newMul, _lastAppliedMul)) return;

            float oldMax = health.MaximumHealth;
            float ratio = oldMax > 0f ? health.CurrentHealth / oldMax : 1f;
            health.MaximumHealth = _baseMaxHealth * newMul;
            health.SetHealth(health.MaximumHealth * ratio);
            _lastAppliedMul = newMul;
        }
    }
}
