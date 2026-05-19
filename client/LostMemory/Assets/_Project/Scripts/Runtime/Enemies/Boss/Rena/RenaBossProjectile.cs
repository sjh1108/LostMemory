using System;
using System.Collections.Generic;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies.Boss.Rena
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Boss/Rena/Rena Boss Projectile")]
    public sealed class RenaBossProjectile : MonoBehaviour
    {
        [SerializeField] private GameObject owner;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private SpriteRenderer glowSpriteRenderer;
        [SerializeField] private LayerMask targetLayerMask = 1 << 10;
        [SerializeField] private LayerMask obstacleLayerMask = (1 << 8) | (1 << 11) | (1 << 24);
        [SerializeField, Min(0.01f)] private float speed = 7f;
        [SerializeField, Min(0.01f)] private float lifetime = 3f;
        [SerializeField, Min(0f)] private float damage = 10f;
        [SerializeField, Min(0f)] private float targetInvincibilityDuration = 0.35f;
        [SerializeField, Min(0.05f)] private float hitRadius = 0.35f;
        [SerializeField, Min(1)] private int maximumHits = 1;
        [SerializeField] private bool destroyOnHit = true;
        [SerializeField] private bool rotateVisualToDirection = true;
        [SerializeField, Min(0.01f)] private float visualScale = 1f;
        [SerializeField, Min(0.01f)] private float glowVisualScale = 1f;
        [SerializeField, Min(0f)] private float animationFrameRate = 12f;
        [SerializeField] private bool moveDuringCastedAnimation = true;
        [SerializeField] private bool homingEnabled;
        [SerializeField] private Transform homingTarget;
        [SerializeField] private Vector2 homingTargetOffset;
        [SerializeField, Min(0f)] private float homingTurnRateDegrees = 120f;
        [SerializeField, Min(0f)] private float homingStartDelay = 0.12f;
        [SerializeField, Min(0f)] private float homingEndDistance = 0.65f;
        [SerializeField] private Sprite[] castedAnimationFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] animationFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] hitAnimationFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] castedGlowAnimationFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] glowAnimationFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] hitGlowAnimationFrames = Array.Empty<Sprite>();
        [SerializeField] private bool debugLogging;

        private enum VisualPhase
        {
            Casted,
            Loop,
            Hit
        }

        private readonly HashSet<Health> _hitTargets = new HashSet<Health>();
        private Collider2D[] _overlapBuffer;
        private Vector2 _direction = Vector2.right;
        private float _elapsedLifetime;
        private float _animationElapsed;
        private VisualPhase _visualPhase = VisualPhase.Loop;
        private bool _isResolvingHit;
        private bool _homingEnded;

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
            visualScale = Mathf.Max(0.01f, visualScale);
            glowVisualScale = Mathf.Max(0.01f, glowVisualScale);
            animationFrameRate = Mathf.Max(0f, animationFrameRate);
            homingTurnRateDegrees = Mathf.Max(0f, homingTurnRateDegrees);
            homingStartDelay = Mathf.Max(0f, homingStartDelay);
            homingEndDistance = Mathf.Max(0f, homingEndDistance);
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
            bool configuredRotateVisualToDirection,
            bool configuredMoveDuringCastedAnimation,
            Sprite[] configuredCastedAnimationFrames,
            Sprite[] configuredAnimationFrames,
            Sprite[] configuredHitAnimationFrames,
            float configuredAnimationFrameRate,
            float configuredVisualScale,
            SpriteRenderer configuredGlowSpriteRenderer,
            Sprite[] configuredCastedGlowAnimationFrames,
            Sprite[] configuredGlowAnimationFrames,
            Sprite[] configuredHitGlowAnimationFrames,
            float configuredGlowVisualScale,
            bool configuredDebugLogging = false)
        {
            owner = configuredOwner;
            spriteRenderer = configuredSpriteRenderer;
            glowSpriteRenderer = configuredGlowSpriteRenderer;
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
            rotateVisualToDirection = configuredRotateVisualToDirection;
            moveDuringCastedAnimation = configuredMoveDuringCastedAnimation;
            castedAnimationFrames = configuredCastedAnimationFrames ?? Array.Empty<Sprite>();
            animationFrames = configuredAnimationFrames ?? Array.Empty<Sprite>();
            hitAnimationFrames = configuredHitAnimationFrames ?? Array.Empty<Sprite>();
            castedGlowAnimationFrames = configuredCastedGlowAnimationFrames ?? Array.Empty<Sprite>();
            glowAnimationFrames = configuredGlowAnimationFrames ?? Array.Empty<Sprite>();
            hitGlowAnimationFrames = configuredHitGlowAnimationFrames ?? Array.Empty<Sprite>();
            animationFrameRate = Mathf.Max(0f, configuredAnimationFrameRate);
            visualScale = Mathf.Max(0.01f, configuredVisualScale);
            glowVisualScale = Mathf.Max(0.01f, configuredGlowVisualScale);
            debugLogging = configuredDebugLogging;
            _elapsedLifetime = 0f;
            _animationElapsed = 0f;
            _isResolvingHit = false;
            homingEnabled = false;
            homingTarget = null;
            homingTargetOffset = Vector2.zero;
            _homingEnded = false;
            _hitTargets.Clear();
            _visualPhase = HasFrames(castedAnimationFrames) || HasFrames(castedGlowAnimationFrames)
                ? VisualPhase.Casted
                : VisualPhase.Loop;
            EnsureBuffer();
            ApplyVisualState();
            ApplyCurrentFrame();
        }

        public void ConfigureHoming(
            Transform configuredTarget,
            Vector2 configuredTargetOffset,
            float configuredTurnRateDegrees,
            float configuredStartDelay,
            float configuredEndDistance)
        {
            homingTarget = configuredTarget;
            homingTargetOffset = configuredTargetOffset;
            homingTurnRateDegrees = Mathf.Max(0f, configuredTurnRateDegrees);
            homingStartDelay = Mathf.Max(0f, configuredStartDelay);
            homingEndDistance = Mathf.Max(0f, configuredEndDistance);
            homingEnabled = homingTarget != null && homingTurnRateDegrees > 0f;
            _homingEnded = false;
        }

        private void Update()
        {
            if (_isResolvingHit)
            {
                UpdateAnimation(Time.deltaTime);
                return;
            }

            float deltaTime = Time.deltaTime;
            if (_visualPhase == VisualPhase.Casted && !moveDuringCastedAnimation)
            {
                UpdateAnimation(deltaTime);
                return;
            }

            UpdateHomingDirection(deltaTime);

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
                StartHitResolution();
            }
        }

        private void UpdateHomingDirection(float deltaTime)
        {
            if (!CanUpdateHomingDirection(deltaTime))
            {
                return;
            }

            Vector2 targetPosition = (Vector2)homingTarget.position + homingTargetOffset;
            Vector2 toTarget = targetPosition - (Vector2)transform.position;
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            if (homingEndDistance > 0f && toTarget.sqrMagnitude <= homingEndDistance * homingEndDistance)
            {
                _homingEnded = true;
                return;
            }

            float currentAngle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
            float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
            float nextAngle = Mathf.MoveTowardsAngle(
                currentAngle,
                targetAngle,
                homingTurnRateDegrees * deltaTime);
            _direction = DirectionFromAngle(nextAngle);
        }

        private bool CanUpdateHomingDirection(float deltaTime)
        {
            return deltaTime > 0f
                   && homingEnabled
                   && !_homingEnded
                   && homingTarget != null
                   && homingTarget.gameObject.activeInHierarchy
                   && _elapsedLifetime >= homingStartDelay
                   && homingTurnRateDegrees > 0f;
        }

        private void ProcessHits()
        {
            EnsureBuffer();

            int hitCount = OverlapCircleTargets(transform.position, hitRadius);
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

                Log("Projectile hit " + health.name + ".");

                if (destroyOnHit)
                {
                    StartHitResolution();
                    return;
                }
            }
        }

        private bool ProcessObstacleCollision(float movementDistance)
        {
            if (movementDistance <= 0f || obstacleLayerMask.value == 0)
            {
                return false;
            }

            RaycastHit2D hit = Physics2D.CircleCast(transform.position, hitRadius, _direction, movementDistance, obstacleLayerMask);
            if (hit.collider == null)
            {
                return false;
            }

            Log("Projectile hit obstacle " + hit.collider.name + ".");
            transform.position += (Vector3)(_direction * Mathf.Max(0f, hit.distance));
            StartHitResolution();
            return true;
        }

        private void UpdateAnimation(float deltaTime)
        {
            if (animationFrameRate <= 0f)
            {
                return;
            }

            _animationElapsed += deltaTime;
            if (_visualPhase == VisualPhase.Casted)
            {
                bool castedComplete = ApplyAnimationFrame(spriteRenderer, castedAnimationFrames, false);
                bool glowCastedComplete = ApplyAnimationFrame(glowSpriteRenderer, castedGlowAnimationFrames, false);
                if (castedComplete && glowCastedComplete)
                {
                    SwitchVisualPhase(VisualPhase.Loop);
                }

                return;
            }

            if (_visualPhase == VisualPhase.Hit)
            {
                bool hitComplete = ApplyAnimationFrame(spriteRenderer, hitAnimationFrames, false);
                bool glowHitComplete = ApplyAnimationFrame(glowSpriteRenderer, hitGlowAnimationFrames, false);
                if (hitComplete && glowHitComplete)
                {
                    Destroy(gameObject);
                }

                return;
            }

            ApplyAnimationFrame(spriteRenderer, animationFrames, true);
            ApplyAnimationFrame(glowSpriteRenderer, glowAnimationFrames, true);
        }

        private void ApplyVisualState()
        {
            Sprite[] currentFrames = GetCurrentFrames();
            if (spriteRenderer != null && spriteRenderer.sprite == null && HasFrames(currentFrames))
            {
                spriteRenderer.sprite = currentFrames[0];
            }

            if (glowSpriteRenderer != null)
            {
                Sprite[] currentGlowFrames = GetCurrentGlowFrames();
                if (glowSpriteRenderer.sprite == null && HasFrames(currentGlowFrames))
                {
                    glowSpriteRenderer.sprite = currentGlowFrames[0];
                }

                glowSpriteRenderer.transform.localScale = Vector3.one * glowVisualScale;
            }

            transform.localScale = Vector3.one * visualScale;

            if (!rotateVisualToDirection)
            {
                return;
            }

            float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }

        private bool ApplyAnimationFrame(SpriteRenderer renderer, Sprite[] frames, bool loop)
        {
            if (renderer == null || !HasFrames(frames))
            {
                return true;
            }

            int frameIndex = Mathf.FloorToInt(_animationElapsed * animationFrameRate);
            if (loop)
            {
                frameIndex %= frames.Length;
            }
            else if (frameIndex >= frames.Length)
            {
                renderer.sprite = frames[frames.Length - 1];
                return true;
            }

            if (frameIndex >= 0)
            {
                renderer.sprite = frames[frameIndex];
            }

            return false;
        }

        private void ApplyCurrentFrame()
        {
            Sprite[] currentFrames = GetCurrentFrames();
            if (spriteRenderer != null && HasFrames(currentFrames))
            {
                spriteRenderer.sprite = currentFrames[0];
            }

            Sprite[] currentGlowFrames = GetCurrentGlowFrames();
            if (glowSpriteRenderer != null && HasFrames(currentGlowFrames))
            {
                glowSpriteRenderer.sprite = currentGlowFrames[0];
            }
        }

        private void StartHitResolution()
        {
            if (_isResolvingHit)
            {
                return;
            }

            if (!HasFrames(hitAnimationFrames) && !HasFrames(hitGlowAnimationFrames))
            {
                Destroy(gameObject);
                return;
            }

            _isResolvingHit = true;
            SwitchVisualPhase(VisualPhase.Hit);
        }

        private void SwitchVisualPhase(VisualPhase visualPhase)
        {
            _visualPhase = visualPhase;
            _animationElapsed = 0f;
            ApplyCurrentFrame();
        }

        private Sprite[] GetCurrentFrames()
        {
            if (_visualPhase == VisualPhase.Casted && HasFrames(castedAnimationFrames))
            {
                return castedAnimationFrames;
            }

            if (_visualPhase == VisualPhase.Hit && HasFrames(hitAnimationFrames))
            {
                return hitAnimationFrames;
            }

            return HasFrames(animationFrames) ? animationFrames : Array.Empty<Sprite>();
        }

        private Sprite[] GetCurrentGlowFrames()
        {
            if (_visualPhase == VisualPhase.Casted && HasFrames(castedGlowAnimationFrames))
            {
                return castedGlowAnimationFrames;
            }

            if (_visualPhase == VisualPhase.Hit && HasFrames(hitGlowAnimationFrames))
            {
                return hitGlowAnimationFrames;
            }

            return HasFrames(glowAnimationFrames) ? glowAnimationFrames : Array.Empty<Sprite>();
        }

        private static bool HasFrames(Sprite[] frames)
        {
            return frames != null && frames.Length > 0;
        }

        private static Vector2 DirectionFromAngle(float angleDegrees)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
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
                Debug.Log("[RenaBossProjectile] " + message, this);
            }
        }
    }
}
