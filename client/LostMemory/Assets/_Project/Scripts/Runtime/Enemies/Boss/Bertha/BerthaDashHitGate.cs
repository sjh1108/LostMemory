using System.Collections.Generic;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies.Boss.Bertha
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Boss/Bertha/Bertha Dash Hit Gate")]
    public sealed class BerthaDashHitGate : DamageOnTouch
    {
        [SerializeField] private bool requireSuccessfulDamageToLockTarget = true;

        private readonly HashSet<Health> _hitTargetsThisDash = new HashSet<Health>();

        protected override void OnEnable()
        {
            _hitTargetsThisDash.Clear();
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            _hitTargetsThisDash.Clear();
            base.OnDisable();
        }

        protected override void OnCollideWithDamageable(Health health)
        {
            if (health == null || _hitTargetsThisDash.Contains(health))
            {
                return;
            }

            if (requireSuccessfulDamageToLockTarget && !health.CanTakeDamageThisFrame())
            {
                return;
            }

            base.OnCollideWithDamageable(health);
            _hitTargetsThisDash.Add(health);
        }
    }
}
