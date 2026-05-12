using System.Collections.Generic;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies.Boss.Bertha
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Boss/Bertha/Bertha Boss Projectile")]
    public sealed class BerthaBossProjectile : MonoBehaviour
    {
        [SerializeField] private GameObject owner;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private LayerMask targetLayerMask = 1 << 10;
        [SerializeField] private LayerMask obstacleLayerMask = 1 << 8;
        [SerializeField, Min(0.01f)] private float speed = 6f;
        [SerializeField, Min(0.01f)] private float lifetime = 1.2f;
        [SerializeField, Min(0f)] private float damage = 8f;
        [SerializeField, Min(0f)] private float targetInvincibilityDuration = 0.5f;
        [SerializeField, Min(0.05f)] private float hitRadius = 0.35f;
        [SerializeField, Min(1)] private int maximumHits = 8;
        [SerializeField] private bool destroyOnHit = true;
        [SerializeField] private bool faceMovementDirection = true;
        [SerializeField, Min(0f)] private float animationFrameRate = 12f;
        [SerializeField] private Sprite[] animationFrames = System.Array.Empty<Sprite>();
        [SerializeField] private Transform homingTarget;
        [SerializeField, Min(0f)] private float homingTurnSpeedDegrees;
        [SerializeField, Min(0f)] private float homingDuration;
        [SerializeField] private bool debugLogging;

        private readonly HashSet<Health> _hitTargets = new HashSet<Health>();
        private Collider2D[] _overlapBuffer;
        private Vector2 _direction = Vector2.right;
        private float _elapsedLifetime;
        private float _animationElapsed;
        private float _homingElapsed;

        private void Reset()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            EnsureBuffer();
        }

        private void OnValidate()
        {
            speed = Mathf.Max(0.01f, speed);
            lifetime = Mathf.Max(0.01f, lifetime);
            hitRadius = Mathf.Max(0.05f, hitRadius);
            maximumHits = Mathf.Max(1, maximumHits);
            homingTurnSpeedDegrees = Mathf.Max(0f, homingTurnSpeedDegrees);
            homingDuration = Mathf.Max(0f, homingDuration);
            spriteRenderer ??= GetComponent<SpriteRenderer>();
            EnsureBuffer();
        }

        private void Awake()
        {
            spriteRenderer ??= GetComponent<SpriteRenderer>();
            EnsureBuffer();
            ApplyVisualState();
        }

        public void Configure(
            GameObject configuredOwner,
            SpriteRenderer configuredSpriteRenderer,
            Vector2 configuredDirection,
            LayerMask configuredTargetLayerMask,
            LayerMask configuredObstacleLayerMask,
            float configuredSpeed,
            float configuredLifetime,
            float configuredDamage,
            float configuredTargetInvincibilityDuration,
            float configuredHitRadius,
            int configuredMaximumHits,
            bool configuredDestroyOnHit,
            bool configuredFaceMovementDirection,
            Sprite[] configuredAnimationFrames,
            float configuredAnimationFrameRate,
            Transform configuredHomingTarget = null,
            float configuredHomingTurnSpeedDegrees = 0f,
            float configuredHomingDuration = 0f)
        {
            owner = configuredOwner;
            spriteRenderer = configuredSpriteRenderer;
            _direction = configuredDirection.sqrMagnitude > 0.0001f ? configuredDirection.normalized : Vector2.right;
            targetLayerMask = configuredTargetLayerMask;
            obstacleLayerMask = configuredObstacleLayerMask;
            speed = Mathf.Max(0.01f, configuredSpeed);
            lifetime = Mathf.Max(0.01f, configuredLifetime);
            damage = Mathf.Max(0f, configuredDamage);
            targetInvincibilityDuration = Mathf.Max(0f, configuredTargetInvincibilityDuration);
            hitRadius = Mathf.Max(0.05f, configuredHitRadius);
            maximumHits = Mathf.Max(1, configuredMaximumHits);
            destroyOnHit = configuredDestroyOnHit;
            faceMovementDirection = configuredFaceMovementDirection;
            animationFrames = configuredAnimationFrames ?? System.Array.Empty<Sprite>();
            animationFrameRate = Mathf.Max(0f, configuredAnimationFrameRate);
            homingTarget = configuredHomingTarget;
            homingTurnSpeedDegrees = Mathf.Max(0f, configuredHomingTurnSpeedDegrees);
            homingDuration = Mathf.Max(0f, configuredHomingDuration);
            _elapsedLifetime = 0f;
            _animationElapsed = 0f;
            _homingElapsed = 0f;
            _hitTargets.Clear();
            EnsureBuffer();
            ApplyVisualState();
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            UpdateHoming(deltaTime);
            float movementDistance = speed * deltaTime;
            if (ProcessObstacleCollision(movementDistance))
            {
                return;
            }

            transform.position += (Vector3)(_direction * movementDistance);
            _elapsedLifetime += deltaTime;

            ApplyVisualState();
            UpdateAnimation(deltaTime);
            ProcessHits();

            if (_elapsedLifetime >= lifetime)
            {
                Destroy(gameObject);
            }
        }

        private void ProcessHits()
        {
            EnsureBuffer();

            int hitCount = OverlapCircleTargets(transform.position, hitRadius);
            bool appliedHit = false;

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hitCollider = _overlapBuffer[i];
                if (hitCollider == null)
                {
                    continue;
                }

                Health health = hitCollider.GetComponentInParent<Health>();
                if (health == null
                    || _hitTargets.Contains(health)
                    || IsOwnedByAttacker(health, owner)
                    || !health.CanTakeDamageThisFrame())
                {
                    continue;
                }

                _hitTargets.Add(health);
                health.Damage(
                    damage,
                    owner != null ? owner : gameObject,
                    targetInvincibilityDuration,
                    targetInvincibilityDuration,
                    new Vector3(_direction.x, _direction.y, 0f));

                appliedHit = true;
                Log("Projectile hit " + health.name + ".");

                if (destroyOnHit)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            if (appliedHit && destroyOnHit)
            {
                Destroy(gameObject);
            }
        }

        private bool ProcessObstacleCollision(float movementDistance)
        {
            if (movementDistance <= 0f || obstacleLayerMask == 0)
            {
                return false;
            }

            RaycastHit2D hit = Physics2D.CircleCast(transform.position, hitRadius, _direction, movementDistance, obstacleLayerMask);
            if (hit.collider == null)
            {
                return false;
            }

            Log("Projectile hit obstacle " + hit.collider.name + ".");
            Destroy(gameObject);
            return true;
        }

        private void UpdateAnimation(float deltaTime)
        {
            if (spriteRenderer == null || animationFrames == null || animationFrames.Length <= 1 || animationFrameRate <= 0f)
            {
                return;
            }

            _animationElapsed += deltaTime;
            int frameIndex = Mathf.FloorToInt(_animationElapsed * animationFrameRate) % animationFrames.Length;

            if (frameIndex >= 0 && frameIndex < animationFrames.Length)
            {
                spriteRenderer.sprite = animationFrames[frameIndex];
            }
        }

        private void UpdateHoming(float deltaTime)
        {
            if (homingTarget == null
                || homingTurnSpeedDegrees <= 0f
                || homingDuration <= 0f
                || _homingElapsed >= homingDuration)
            {
                return;
            }

            Vector2 targetDirection = (Vector2)(homingTarget.position - transform.position);
            if (targetDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float maxRadiansDelta = homingTurnSpeedDegrees * Mathf.Deg2Rad * deltaTime;
            Vector3 rotatedDirection = Vector3.RotateTowards(
                _direction,
                targetDirection.normalized,
                maxRadiansDelta,
                0f);

            _direction = ((Vector2)rotatedDirection).normalized;
            _homingElapsed += deltaTime;
        }

        private void ApplyVisualState()
        {
            if (spriteRenderer != null
                && animationFrames != null
                && animationFrames.Length > 0
                && spriteRenderer.sprite == null)
            {
                spriteRenderer.sprite = animationFrames[0];
            }

            if (!faceMovementDirection)
            {
                return;
            }

            transform.rotation = Quaternion.identity;

            if (spriteRenderer != null)
            {
                if (_direction.x < -0.001f)
                {
                    spriteRenderer.flipX = true;
                }
                else if (_direction.x > 0.001f)
                {
                    spriteRenderer.flipX = false;
                }
            }
        }

        private void EnsureBuffer()
        {
            if (_overlapBuffer == null || _overlapBuffer.Length != maximumHits)
            {
                _overlapBuffer = new Collider2D[Mathf.Max(1, maximumHits)];
            }
        }

        private int OverlapCircleTargets(Vector2 center, float radius)
        {
            ContactFilter2D contactFilter = new ContactFilter2D();
            contactFilter.SetLayerMask(targetLayerMask);
            contactFilter.useTriggers = Physics2D.queriesHitTriggers;
            return Physics2D.OverlapCircle(center, radius, contactFilter, _overlapBuffer);
        }

        private static bool IsOwnedByAttacker(Health health, GameObject attacker)
        {
            if (health == null || attacker == null)
            {
                return false;
            }

            return health.gameObject == attacker || health.transform.IsChildOf(attacker.transform);
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[BerthaBossProjectile] " + message, this);
            }
        }
    }
}
