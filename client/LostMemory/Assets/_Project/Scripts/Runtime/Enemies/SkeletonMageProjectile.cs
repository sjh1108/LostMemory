using System.Collections.Generic;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Skeleton Mage Projectile")]
    public sealed class SkeletonMageProjectile : MonoBehaviour
    {
        [Header("Bindings")]
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private CircleCollider2D hitCollider;
        [SerializeField] private Transform visualRoot;

        [Header("Defaults")]
        [SerializeField, Min(0f)] private float defaultSpeed = 6f;
        [SerializeField, Min(0f)] private float defaultDamage = 9f;
        [SerializeField, Min(0f)] private float defaultHitRadius = 0.28f;
        [SerializeField, Min(0f)] private float defaultLifetime = 3f;
        [SerializeField, Min(0.01f)] private float defaultVisualScale = 1.4f;
        [SerializeField, Min(0f)] private float defaultInvincibilityDuration = 0.35f;
        [SerializeField] private bool defaultApplyKnockback = true;
        [SerializeField, Min(0f)] private float defaultKnockbackForce = 130f;
        [SerializeField] private LayerMask defaultTargetLayerMask = 1 << 10;
        [SerializeField] private LayerMask defaultWallBlockMask = (1 << 8) | (1 << 11) | (1 << 24);
        [SerializeField] private bool defaultHomingEnabled = true;
        [SerializeField, Min(0f)] private float defaultHomingTurnSpeed = 160f;
        [SerializeField, Min(0f)] private float defaultHomingDuration = 1.4f;
        [SerializeField, Min(0f)] private float defaultHomingScanRadius = 7f;

        private readonly HashSet<Health> _hitTargets = new HashSet<Health>();
        private GameObject _owner;
        private Health _ownerHealth;
        private Transform _homingTarget;
        private LayerMask _targetLayerMask;
        private LayerMask _wallBlockMask;
        private Vector2 _direction = Vector2.right;
        private float _speed;
        private float _damage;
        private float _hitRadius;
        private float _expiresAt;
        private float _visualScale;
        private float _invincibilityDuration;
        private bool _applyKnockback;
        private float _knockbackForce;
        private bool _homingEnabled;
        private float _homingTurnSpeed;
        private float _homingEndsAt;
        private float _homingScanRadius;
        private bool _initialized;

        private void Reset()
        {
            ResolveBindings();
        }

        private void Awake()
        {
            ResolveBindings();
        }

        private void OnEnable()
        {
            _hitTargets.Clear();
            if (!_initialized)
            {
                InitializeDefaults();
            }
        }

        public void Initialize(
            GameObject owner,
            Health ownerHealth,
            LayerMask targetLayerMask,
            LayerMask wallBlockMask,
            Transform homingTarget,
            Vector2 direction,
            float speed,
            float damage,
            float hitRadius,
            float lifetime,
            float visualScale,
            float invincibilityDuration,
            bool applyKnockback,
            float knockbackForce,
            bool homingEnabled,
            float homingTurnSpeed,
            float homingDuration,
            float homingScanRadius)
        {
            ResolveBindings();
            _initialized = true;
            _owner = owner;
            _ownerHealth = ownerHealth;
            _homingTarget = homingTarget;
            _targetLayerMask = targetLayerMask;
            _wallBlockMask = wallBlockMask;
            _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            _speed = Mathf.Max(0f, speed);
            _damage = Mathf.Max(0f, damage);
            _hitRadius = Mathf.Max(0f, hitRadius);
            _expiresAt = Time.time + Mathf.Max(0.01f, lifetime);
            _visualScale = Mathf.Max(0.01f, visualScale);
            _invincibilityDuration = Mathf.Max(0f, invincibilityDuration);
            _applyKnockback = applyKnockback;
            _knockbackForce = Mathf.Max(0f, knockbackForce);
            _homingEnabled = homingEnabled;
            _homingTurnSpeed = Mathf.Max(0f, homingTurnSpeed);
            _homingEndsAt = Time.time + Mathf.Max(0f, homingDuration);
            _homingScanRadius = Mathf.Max(0f, homingScanRadius);

            if (hitCollider != null)
            {
                hitCollider.radius = _hitRadius;
            }

            ApplyVisualScale();
            ApplyRotation();
        }

        private void InitializeDefaults()
        {
            _owner = null;
            _ownerHealth = null;
            _homingTarget = null;
            _targetLayerMask = defaultTargetLayerMask;
            _wallBlockMask = defaultWallBlockMask;
            _direction = transform.right.sqrMagnitude > 0.0001f ? (Vector2)transform.right.normalized : Vector2.right;
            _speed = defaultSpeed;
            _damage = defaultDamage;
            _hitRadius = defaultHitRadius;
            _expiresAt = Time.time + Mathf.Max(0.01f, defaultLifetime);
            _visualScale = Mathf.Max(0.01f, defaultVisualScale);
            _invincibilityDuration = defaultInvincibilityDuration;
            _applyKnockback = defaultApplyKnockback;
            _knockbackForce = defaultKnockbackForce;
            _homingEnabled = defaultHomingEnabled;
            _homingTurnSpeed = Mathf.Max(0f, defaultHomingTurnSpeed);
            _homingEndsAt = Time.time + Mathf.Max(0f, defaultHomingDuration);
            _homingScanRadius = Mathf.Max(0f, defaultHomingScanRadius);
            ApplyVisualScale();
        }

        private void FixedUpdate()
        {
            if (Time.time >= _expiresAt)
            {
                Destroy(gameObject);
                return;
            }

            UpdateHoming();
            MoveProjectile();

            if (HitWall())
            {
                Destroy(gameObject);
                return;
            }

            ScanTargets();
        }

        private void MoveProjectile()
        {
            Vector2 movement = _direction * (_speed * Time.fixedDeltaTime);
            if (body != null)
            {
                body.MovePosition(body.position + movement);
                return;
            }

            transform.position += (Vector3)movement;
        }

        private void UpdateHoming()
        {
            if (!_homingEnabled
                || _homingTurnSpeed <= 0f
                || Time.time > _homingEndsAt
                || _speed <= 0f)
            {
                return;
            }

            Transform target = ResolveHomingTarget();
            if (target == null)
            {
                return;
            }

            Vector2 toTarget = (Vector2)target.position - ResolveHitCenter();
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector3 rotatedDirection = Vector3.RotateTowards(
                _direction,
                toTarget.normalized,
                _homingTurnSpeed * Mathf.Deg2Rad * Time.fixedDeltaTime,
                0f);

            if (rotatedDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            _direction = ((Vector2)rotatedDirection).normalized;
            ApplyRotation();
        }

        private Transform ResolveHomingTarget()
        {
            if (IsHomingTargetValid(_homingTarget))
            {
                return _homingTarget;
            }

            _homingTarget = FindClosestHomingTarget();
            return _homingTarget;
        }

        private Transform FindClosestHomingTarget()
        {
            if (_homingScanRadius <= 0f || _targetLayerMask.value == 0)
            {
                return null;
            }

            Collider2D[] candidates = Physics2D.OverlapCircleAll(transform.position, _homingScanRadius, _targetLayerMask);
            Health bestHealth = null;
            float bestSqrDistance = float.PositiveInfinity;
            Vector3 origin = transform.position;
            for (int i = 0; i < candidates.Length; i++)
            {
                Health candidateHealth = ResolveHealth(candidates[i]);
                if (!CanHomeTo(candidateHealth))
                {
                    continue;
                }

                float sqrDistance = (candidateHealth.transform.position - origin).sqrMagnitude;
                if (sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    bestHealth = candidateHealth;
                }
            }

            return bestHealth != null ? bestHealth.transform : null;
        }

        private bool IsHomingTargetValid(Transform target)
        {
            return target != null
                   && target.gameObject.activeInHierarchy
                   && CanHomeTo(ResolveHealth(target));
        }

        private bool CanHomeTo(Health targetHealth)
        {
            return targetHealth != null
                   && targetHealth != _ownerHealth
                   && targetHealth.CurrentHealth > 0f
                   && targetHealth.gameObject.activeInHierarchy;
        }

        private bool HitWall()
        {
            return _wallBlockMask.value != 0
                   && _hitRadius > 0f
                   && Physics2D.OverlapCircle(ResolveHitCenter(), _hitRadius, _wallBlockMask) != null;
        }

        private void ScanTargets()
        {
            if (_damage <= 0f || _hitRadius <= 0f)
            {
                return;
            }

            Collider2D targetCollider = Physics2D.OverlapCircle(ResolveHitCenter(), _hitRadius, _targetLayerMask);
            if (targetCollider == null)
            {
                return;
            }

            Health targetHealth = ResolveHealth(targetCollider);
            if (!CanDamage(targetHealth))
            {
                return;
            }

            Damage(targetHealth);
            Destroy(gameObject);
        }

        private Vector2 ResolveHitCenter()
        {
            return hitCollider != null
                ? hitCollider.transform.TransformPoint(hitCollider.offset)
                : transform.position;
        }

        private bool CanDamage(Health targetHealth)
        {
            return targetHealth != null
                   && targetHealth != _ownerHealth
                   && targetHealth.CurrentHealth > 0f
                   && targetHealth.CanTakeDamageThisFrame()
                   && !_hitTargets.Contains(targetHealth);
        }

        private void Damage(Health targetHealth)
        {
            Vector3 direction = targetHealth.transform.position - transform.position;
            direction.z = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = _direction;
            }

            direction.Normalize();
            _hitTargets.Add(targetHealth);
            targetHealth.Damage(_damage, _owner, 0f, _invincibilityDuration, direction);
            ApplyKnockback(targetHealth, direction);
        }

        private void ApplyKnockback(Health targetHealth, Vector3 direction)
        {
            if (!_applyKnockback
                || _knockbackForce <= 0f
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

            Vector3 knockback = direction.normalized * _knockbackForce;
            knockback *= targetHealth.KnockbackForceMultiplier;
            knockback = targetHealth.ComputeKnockbackForce(knockback);
            if (knockback.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            controller.Impact(knockback.normalized, knockback.magnitude);
        }

        private void ResolveBindings()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            if (hitCollider == null)
            {
                hitCollider = GetComponent<CircleCollider2D>();
            }

            if (visualRoot == null)
            {
                SpriteRenderer visualRenderer = GetComponentInChildren<SpriteRenderer>(includeInactive: true);
                if (visualRenderer != null)
                {
                    visualRoot = visualRenderer.transform;
                }
            }
        }

        private void ApplyVisualScale()
        {
            if (visualRoot == null)
            {
                return;
            }

            visualRoot.localScale = Vector3.one * Mathf.Max(0.01f, _visualScale);
        }

        private void ApplyRotation()
        {
            if (_direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }

        private static Health ResolveHealth(Component target)
        {
            if (target == null)
            {
                return null;
            }

            Health targetHealth = target.GetComponent<Health>();
            return targetHealth != null ? targetHealth : target.GetComponentInParent<Health>();
        }
    }
}
