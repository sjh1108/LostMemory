using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Chobomb Explosion Knockback")]
    public sealed class ChobombExplosionKnockback : MonoBehaviour, MMEventListener<MMDamageTakenEvent>
    {
        [SerializeField, Min(0f)] private float knockbackForce = 500f;
        [SerializeField] private bool debugLogging;

        public void Configure(float configuredKnockbackForce, bool configuredDebugLogging = false)
        {
            knockbackForce = Mathf.Max(0f, configuredKnockbackForce);
            debugLogging = configuredDebugLogging;
        }

        private void OnEnable()
        {
            this.MMEventStartListening<MMDamageTakenEvent>();
        }

        private void OnDisable()
        {
            this.MMEventStopListening<MMDamageTakenEvent>();
        }

        private void OnValidate()
        {
            knockbackForce = Mathf.Max(0f, knockbackForce);
        }

        public void OnMMEvent(MMDamageTakenEvent damageEvent)
        {
            if (!enabled
                || knockbackForce <= 0f
                || damageEvent.AffectedHealth == null
                || !WasDamageCausedByThisChobomb(damageEvent.Instigator)
                || IsOwnedHealth(damageEvent.AffectedHealth)
                || !CanApplyKnockback(damageEvent))
            {
                return;
            }

            TopDownController controller = ResolveTargetController(damageEvent.AffectedHealth);
            if (controller == null)
            {
                return;
            }

            Vector3 direction = ResolveKnockbackDirection(damageEvent.AffectedHealth);
            Vector3 knockback = direction * knockbackForce;
            knockback *= damageEvent.AffectedHealth.KnockbackForceMultiplier;
            knockback = damageEvent.AffectedHealth.ComputeKnockbackForce(knockback, damageEvent.TypedDamages);

            if (knockback.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            controller.Impact(knockback.normalized, knockback.magnitude);

            if (debugLogging)
            {
                Debug.Log(
                    $"[ChobombExplosionKnockback] Applied knockback to {damageEvent.AffectedHealth.name}, force={knockback.magnitude:0.##}.",
                    this);
            }
        }

        private bool WasDamageCausedByThisChobomb(GameObject instigator)
        {
            if (instigator == null)
            {
                return false;
            }

            return instigator == gameObject || instigator.transform.IsChildOf(transform);
        }

        private bool IsOwnedHealth(Health health)
        {
            return health.transform == transform || health.transform.IsChildOf(transform);
        }

        private static bool CanApplyKnockback(MMDamageTakenEvent damageEvent)
        {
            Health health = damageEvent.AffectedHealth;
            if (health.ImmuneToKnockbackIfZeroDamage && damageEvent.DamageCaused <= 0f)
            {
                return false;
            }

            return health.CanGetKnockback(damageEvent.TypedDamages);
        }

        private static TopDownController ResolveTargetController(Health health)
        {
            TopDownController controller = health.GetComponent<TopDownController>();
            if (controller != null)
            {
                return controller;
            }

            return health.GetComponentInParent<TopDownController>();
        }

        private Vector3 ResolveKnockbackDirection(Health health)
        {
            Vector3 direction = health.LastDamageDirection;
            direction.z = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                return direction.normalized;
            }

            direction = health.transform.position - transform.position;
            direction.z = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                return direction.normalized;
            }

            return Vector3.right;
        }
    }
}
