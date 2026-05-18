using System.Collections.Generic;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Enemy Contact Damage")]
    public sealed class EnemyContactDamage : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private GameObject owner;
        [SerializeField] private Health ownerHealth;
        [SerializeField] private LayerMask targetLayerMask = 1 << 10;
        [SerializeField] private Collider2D contactCollider;
        [SerializeField] private bool usePhysicsCallbacks;
        [SerializeField] private bool requireColliderOnTargetHealthObject = true;
        [SerializeField] private bool scanOverlapsInFixedUpdate = true;
        [SerializeField] private bool keepContactColliderEnabled = true;
        [SerializeField, Min(1)] private int overlapBufferSize = 8;

        [Header("Damage")]
        [SerializeField, Min(0f)] private float damage = 10f;
        [SerializeField, Min(0f)] private float damageCooldown = 1f;
        [SerializeField, Min(0f)] private float invincibilityDuration = 1f;

        [Header("Knockback")]
        [SerializeField] private bool applyKnockback = true;
        [SerializeField, Min(0f)] private float knockbackForce = 500f;

        [Header("Debug")]
        [SerializeField] private bool debugLogging;

        private readonly Dictionary<Health, float> _nextDamageTimes = new();
        private readonly Dictionary<Health, float> _nextKnockbackTimes = new();
        private Collider2D[] _overlapBuffer;

        private void Reset()
        {
            owner = ResolveDefaultOwner();
            ownerHealth = owner.GetComponent<Health>();
            contactCollider = ResolveDefaultContactCollider();
        }

        private void Awake()
        {
            RefreshReferences();
            EnsureOverlapBuffer();
        }

        private void OnEnable()
        {
            RefreshReferences();
            EnsureOverlapBuffer();
        }

        private void OnDisable()
        {
            _nextDamageTimes.Clear();
            _nextKnockbackTimes.Clear();
        }

        private void OnValidate()
        {
            damage = Mathf.Max(0f, damage);
            damageCooldown = Mathf.Max(0f, damageCooldown);
            invincibilityDuration = Mathf.Max(0f, invincibilityDuration);
            knockbackForce = Mathf.Max(0f, knockbackForce);
            overlapBufferSize = Mathf.Max(1, overlapBufferSize);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!usePhysicsCallbacks)
            {
                return;
            }

            TryApplyContactDamage(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!usePhysicsCallbacks)
            {
                return;
            }

            TryApplyContactDamage(other);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!usePhysicsCallbacks)
            {
                return;
            }

            TryApplyContactDamage(collision.collider);
            TryApplyContactDamage(collision.otherCollider);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (!usePhysicsCallbacks)
            {
                return;
            }

            TryApplyContactDamage(collision.collider);
            TryApplyContactDamage(collision.otherCollider);
        }

        private void FixedUpdate()
        {
            if (!scanOverlapsInFixedUpdate)
            {
                return;
            }

            RefreshReferences();

            if (keepContactColliderEnabled && contactCollider != null && !contactCollider.enabled)
            {
                contactCollider.enabled = true;
            }

            // Trigger callbacks can be missed after hit reaction or physics state changes,
            // so the contact area is polled as the authoritative check.
            ScanOverlappingTargets();
        }

        private void TryApplyContactDamage(Collider2D other)
        {
            if (other == null
                || damage <= 0f
                || !OwnerCanDealContactDamage()
                || !MMLayers.LayerInLayerMask(other.gameObject.layer, targetLayerMask))
            {
                return;
            }

            Health targetHealth = ResolveHealth(other);
            if (targetHealth == null
                || IsOwnerHealth(targetHealth)
                || !IsValidTargetCollider(other, targetHealth)
                || targetHealth.CurrentHealth <= 0f)
            {
                return;
            }

            Vector3 direction = ResolveDirection(targetHealth);
            TryApplyContactKnockback(targetHealth, direction);

            if (_nextDamageTimes.TryGetValue(targetHealth, out float nextAllowedDamageTime)
                && Time.time < nextAllowedDamageTime)
            {
                return;
            }

            if (!targetHealth.CanTakeDamageThisFrame())
            {
                if (debugLogging)
                {
                    Debug.Log($"[EnemyContactDamage] {name} pushed {targetHealth.name}, but damage was skipped because the target is temporarily invulnerable.", this);
                }

                return;
            }

            _nextDamageTimes[targetHealth] = Time.time + damageCooldown;
            targetHealth.Damage(damage, ResolveDamageInstigator(), invincibilityDuration, invincibilityDuration, direction);

            if (debugLogging)
            {
                Debug.Log($"[EnemyContactDamage] {name} hit {targetHealth.name}, damage={damage:0.##}.", this);
            }
        }

        private void TryApplyContactKnockback(Health targetHealth, Vector3 direction)
        {
            if (_nextKnockbackTimes.TryGetValue(targetHealth, out float nextAllowedKnockbackTime)
                && Time.time < nextAllowedKnockbackTime)
            {
                return;
            }

            _nextKnockbackTimes[targetHealth] = Time.time + damageCooldown;
            ApplyContactKnockback(targetHealth, direction);
        }

        private void ScanOverlappingTargets()
        {
            if (contactCollider == null)
            {
                return;
            }

            EnsureOverlapBuffer();
            int hitCount = OverlapContactAreaNonAlloc();
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hitCollider = _overlapBuffer[i];
                if (hitCollider == null || hitCollider == contactCollider)
                {
                    continue;
                }

                TryApplyContactDamage(hitCollider);
            }
        }

        private int OverlapContactAreaNonAlloc()
        {
            if (contactCollider is BoxCollider2D boxCollider)
            {
                Vector2 center = boxCollider.transform.TransformPoint(boxCollider.offset);
                Vector2 size = Vector2.Scale(boxCollider.size, AbsScale(boxCollider.transform.lossyScale));
                return Physics2D.OverlapBoxNonAlloc(center, size, boxCollider.transform.eulerAngles.z, _overlapBuffer, targetLayerMask);
            }

            if (contactCollider is CircleCollider2D circleCollider)
            {
                Vector2 center = circleCollider.transform.TransformPoint(circleCollider.offset);
                Vector2 scale = AbsScale(circleCollider.transform.lossyScale);
                float radius = circleCollider.radius * Mathf.Max(scale.x, scale.y);
                return Physics2D.OverlapCircleNonAlloc(center, radius, _overlapBuffer, targetLayerMask);
            }

            Bounds bounds = contactCollider.bounds;
            return Physics2D.OverlapBoxNonAlloc(bounds.center, bounds.size, 0f, _overlapBuffer, targetLayerMask);
        }

        private void RefreshReferences()
        {
            if (owner == null)
            {
                owner = ResolveDefaultOwner();
            }

            if (contactCollider == null)
            {
                contactCollider = ResolveDefaultContactCollider();
            }

            if (ownerHealth == null)
            {
                ownerHealth = owner.GetComponent<Health>();
                if (ownerHealth == null)
                {
                    ownerHealth = owner.GetComponentInParent<Health>();
                }
            }
        }

        private Collider2D ResolveDefaultContactCollider()
        {
            Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
            foreach (Collider2D candidate in colliders)
            {
                if (candidate != null && candidate.isTrigger && candidate.gameObject != gameObject)
                {
                    return candidate;
                }
            }

            foreach (Collider2D candidate in colliders)
            {
                if (candidate != null && candidate.isTrigger)
                {
                    return candidate;
                }
            }

            return GetComponent<Collider2D>();
        }

        private GameObject ResolveDefaultOwner()
        {
            Health health = GetComponent<Health>();
            if (health == null)
            {
                health = GetComponentInParent<Health>();
            }

            return health != null ? health.gameObject : gameObject;
        }

        private void EnsureOverlapBuffer()
        {
            int size = Mathf.Max(1, overlapBufferSize);
            if (_overlapBuffer == null || _overlapBuffer.Length != size)
            {
                _overlapBuffer = new Collider2D[size];
            }
        }

        private static Vector2 AbsScale(Vector3 scale)
        {
            return new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        }

        private bool OwnerCanDealContactDamage()
        {
            return ownerHealth == null || (ownerHealth.enabled && ownerHealth.CurrentHealth > 0f);
        }

        private static Health ResolveHealth(Component target)
        {
            Health health = target.GetComponent<Health>();
            if (health != null)
            {
                return health;
            }

            return target.GetComponentInParent<Health>();
        }

        private bool IsOwnerHealth(Health health)
        {
            GameObject resolvedOwner = owner != null ? owner : gameObject;
            Transform ownerTransform = resolvedOwner.transform;
            return health.transform == ownerTransform || health.transform.IsChildOf(ownerTransform);
        }

        private bool IsValidTargetCollider(Collider2D targetCollider, Health targetHealth)
        {
            return !requireColliderOnTargetHealthObject
                   || targetCollider.gameObject == targetHealth.gameObject;
        }

        private GameObject ResolveDamageInstigator()
        {
            return contactCollider != null ? contactCollider.gameObject : gameObject;
        }

        private Vector3 ResolveDirection(Health targetHealth)
        {
            Transform origin = owner != null ? owner.transform : transform;
            Vector3 direction = targetHealth.transform.position - origin.position;
            direction.z = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                return direction.normalized;
            }

            return Vector3.right;
        }

        private void ApplyContactKnockback(Health targetHealth, Vector3 direction)
        {
            if (!applyKnockback
                || knockbackForce <= 0f
                || targetHealth == null
                || !targetHealth.CanGetKnockback(null))
            {
                return;
            }

            TopDownController controller = targetHealth.GetComponent<TopDownController>();
            if (controller == null)
            {
                controller = targetHealth.GetComponentInParent<TopDownController>();
            }

            if (controller == null)
            {
                return;
            }

            Vector3 knockback = direction.normalized * knockbackForce;
            knockback *= targetHealth.KnockbackForceMultiplier;
            knockback = targetHealth.ComputeKnockbackForce(knockback);

            if (knockback.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            controller.Impact(knockback.normalized, knockback.magnitude);
        }
    }
}
