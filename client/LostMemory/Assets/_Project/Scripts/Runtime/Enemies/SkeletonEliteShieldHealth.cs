using System.Collections.Generic;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Skeleton Elite Shield Health")]
    public sealed class SkeletonEliteShieldHealth : Health
    {
        private SkeletonEliteEnemyController _skeletonElite;

        protected override void Awake()
        {
            base.Awake();
            ResolveSkeletonElite();
        }

        public override void Damage(
            float damage,
            GameObject instigator,
            float flickerDuration,
            float invincibilityDuration,
            Vector3 damageDirection,
            List<TypedDamage> typedDamages = null)
        {
            if (!CanTakeDamageThisFrame())
            {
                return;
            }

            ResolveSkeletonElite();
            if (_skeletonElite != null && _skeletonElite.CanAbsorbShieldDamage)
            {
                float shieldDamage = ComputeDamageOutput(damage, typedDamages, true);
                if (_skeletonElite.TryAbsorbShieldDamage(shieldDamage, instigator, damageDirection))
                {
                    return;
                }
            }

            base.Damage(damage, instigator, flickerDuration, invincibilityDuration, damageDirection, typedDamages);
        }

        private void ResolveSkeletonElite()
        {
            if (_skeletonElite == null)
            {
                _skeletonElite = GetComponent<SkeletonEliteEnemyController>();
            }
        }
    }
}
