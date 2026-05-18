using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Skeleton Mage Enemy Controller")]
    public sealed class SkeletonMageEnemyController : MonoBehaviour
    {
        private enum MageActionState
        {
            Idle,
            WanderMove,
            WanderPause,
            Approach,
            ProjectileAttack,
            AreaAttack,
            Recover,
            Cooldown,
            Dead
        }

        private enum MageAttackKind
        {
            Projectile,
            Area
        }

        [Header("Bindings")]
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Collider2D bodyCollider;
        [SerializeField] private Health health;
        [SerializeField] private SkeletonMageProjectile projectilePrefab;
        [SerializeField] private SkeletonMageAreaAttack areaAttackPrefab;

        [Header("Animation States")]
        [SerializeField] private string idleStateName = "New Animation";
        [SerializeField] private string walkStateName = "MageSkeleton_walk";
        [SerializeField] private string projectileWindupStateName = "MageSkeleton_atk1_prep stage";
        [SerializeField] private string projectileFireStateName = "MageSkeleton_atk1_final stage";
        [SerializeField] private string areaAttackStateName = "MageSkeleton_atk2";
        [SerializeField] private string hurtStateName = "MageSkeleton_hurt";
        [SerializeField] private string deathStateName = "MageSkeleton_death";
        [SerializeField] private bool flipSpriteOnPositiveX;

        [Header("Stats")]
        [SerializeField, Min(1f)] private float maxHealth = 38f;
        [SerializeField, Min(0f)] private float moveSpeed = 2.15f;
        [SerializeField, Min(0f)] private float detectionRadius = 16f;
        [SerializeField, Min(0f)] private float preferredRange = 4.25f;
        [SerializeField, Min(0f)] private float minimumRange = 2.1f;
        [SerializeField] private LayerMask wallBlockMask = (1 << 8) | (1 << 11) | (1 << 24);
        [SerializeField] private LayerMask targetLayerMask = 1 << 10;

        [Header("Combat Movement")]
        [SerializeField, Min(0f)] private float combatStrafeSpeedMultiplier = 0.6f;
        [SerializeField, Min(0.1f)] private float combatStrafeTurnInterval = 1.25f;

        [Header("Wander")]
        [SerializeField, Min(0f)] private float wanderSpeedMultiplier = 0.45f;
        [SerializeField, Min(0.1f)] private float wanderMoveDuration = 1.25f;
        [SerializeField, Min(0f)] private float wanderPauseDuration = 0.55f;
        [SerializeField, Range(0f, 1f)] private float wanderPauseChance = 0.35f;
        [SerializeField, Min(1)] private int wanderTurnAttempts = 8;
        [SerializeField, Min(0f)] private float wanderWallProbeDistance = 0.2f;

        [Header("Projectile Attack")]
        [SerializeField, Min(0f)] private float projectileAttackRange = 14f;
        [SerializeField, Min(0f)] private float projectileAttackMinRange = 1.25f;
        [SerializeField, Min(0.01f)] private float projectileAttackDuration = 0.82f;
        [SerializeField, Min(0f)] private float projectileFireTime = 0.42f;
        [SerializeField, Min(0f)] private float projectileRecoverDuration = 0.2f;
        [SerializeField, Min(0f)] private float projectileAttackCooldown = 1.45f;
        [SerializeField, Min(0f)] private float projectileDamage = 9f;
        [SerializeField, Min(0f)] private float projectileSpeed = 6f;
        [SerializeField, Min(0f)] private float projectileHitRadius = 0.28f;
        [SerializeField, Min(0f)] private float projectileLifetime = 3f;
        [SerializeField, Min(0.01f)] private float projectileVisualScale = 1.4f;
        [SerializeField] private Vector2 projectileSpawnOffset = new Vector2(0.15f, 0.35f);
        [SerializeField, Min(0f)] private float projectileInvincibilityDuration = 0.35f;
        [SerializeField] private bool applyProjectileKnockback = true;
        [SerializeField, Min(0f)] private float projectileKnockbackForce = 130f;
        [SerializeField] private bool projectileHomingEnabled = true;
        [SerializeField, Min(0f)] private float projectileHomingTurnSpeed = 160f;
        [SerializeField, Min(0f)] private float projectileHomingDuration = 1.4f;
        [SerializeField, Min(0f)] private float projectileHomingScanRadius = 14f;

        [Header("Area Attack")]
        [SerializeField, Min(0f)] private float areaAttackRange = 5.75f;
        [SerializeField, Min(0f)] private float areaAttackMinRange = 0.5f;
        [SerializeField, Min(0.01f)] private float areaAttackDuration = 1.05f;
        [SerializeField, Min(0f)] private float areaImpactTime = 0.62f;
        [SerializeField, Min(0f)] private float areaRecoverDuration = 0.35f;
        [SerializeField, Min(0f)] private float areaAttackCooldown = 4.2f;
        [SerializeField, Min(0f)] private float areaDamage = 13f;
        [SerializeField, Min(0f)] private float areaRadius = 1.15f;
        [SerializeField, Min(0f)] private float areaLifetime = 1.1f;
        [SerializeField, Min(0f)] private float areaVisualScale = 1.2f;
        [SerializeField, Min(0f)] private float areaInvincibilityDuration = 0.45f;
        [SerializeField] private bool applyAreaKnockback = true;
        [SerializeField, Min(0f)] private float areaKnockbackForce = 180f;

        [Header("Hit Reaction")]
        [SerializeField, Min(0f)] private float hurtVisualDuration = 0.16f;
        [SerializeField, Min(0f)] private float deathDestroyDelay = 0.85f;

        private Transform _target;
        private MageActionState _state = MageActionState.Idle;
        private MageAttackKind _currentAttackKind = MageAttackKind.Projectile;
        private float _stateStartedAt;
        private float _stateEndsAt;
        private float _nextTargetRefreshTime;
        private float _nextProjectileAttackAllowedAt;
        private float _nextAreaAttackAllowedAt;
        private float _hurtVisualUntil;
        private Vector2 _wanderDirection = Vector2.right;
        private Vector2 _lastFacingDirection = Vector2.right;
        private Vector2 _areaTargetPosition;
        private Transform _visualRoot;
        private Vector3 _visualBaseScale;
        private bool _hasVisualBaseScale;
        private bool _attackTriggered;
        private bool _hurtAnimationActive;
        private int _combatStrafeSign = 1;
        private float _nextCombatStrafeTurnAt;

        private void Reset()
        {
            ResolveBindings();
        }

        private void Awake()
        {
            ResolveBindings();
            CaptureVisualBaseScale();
            ConfigureHealth();
            EnterWanderMove();
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.OnHit += HandleHit;
                health.OnDeath += HandleDeath;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.OnHit -= HandleHit;
                health.OnDeath -= HandleDeath;
            }
        }

        private void Update()
        {
            if (_state != MageActionState.Dead)
            {
                RefreshTargetIfNeeded();
                TickState();
            }

            UpdateAnimator();
        }

        private void FixedUpdate()
        {
            if (_state == MageActionState.WanderMove)
            {
                MoveDuringWander();
            }
            else if (_state == MageActionState.Approach)
            {
                MoveForCombatRange();
            }
            else
            {
                StopBody();
            }
        }

        private void LateUpdate()
        {
            if (_state != MageActionState.Dead)
            {
                ApplySpriteFlip(_lastFacingDirection);
            }
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            moveSpeed = Mathf.Max(0f, moveSpeed);
            detectionRadius = Mathf.Max(0f, detectionRadius);
            preferredRange = Mathf.Max(0f, preferredRange);
            minimumRange = Mathf.Max(0f, minimumRange);
            combatStrafeSpeedMultiplier = Mathf.Max(0f, combatStrafeSpeedMultiplier);
            combatStrafeTurnInterval = Mathf.Max(0.1f, combatStrafeTurnInterval);
            wanderSpeedMultiplier = Mathf.Max(0f, wanderSpeedMultiplier);
            wanderMoveDuration = Mathf.Max(0.1f, wanderMoveDuration);
            wanderPauseDuration = Mathf.Max(0f, wanderPauseDuration);
            wanderTurnAttempts = Mathf.Max(1, wanderTurnAttempts);
            wanderWallProbeDistance = Mathf.Max(0f, wanderWallProbeDistance);
            projectileAttackRange = Mathf.Max(0f, projectileAttackRange);
            projectileAttackMinRange = Mathf.Max(0f, projectileAttackMinRange);
            projectileAttackDuration = Mathf.Max(0.01f, projectileAttackDuration);
            projectileFireTime = Mathf.Max(0f, projectileFireTime);
            projectileRecoverDuration = Mathf.Max(0f, projectileRecoverDuration);
            projectileAttackCooldown = Mathf.Max(0f, projectileAttackCooldown);
            projectileDamage = Mathf.Max(0f, projectileDamage);
            projectileSpeed = Mathf.Max(0f, projectileSpeed);
            projectileHitRadius = Mathf.Max(0f, projectileHitRadius);
            projectileLifetime = Mathf.Max(0f, projectileLifetime);
            projectileVisualScale = Mathf.Max(0.01f, projectileVisualScale);
            projectileInvincibilityDuration = Mathf.Max(0f, projectileInvincibilityDuration);
            projectileKnockbackForce = Mathf.Max(0f, projectileKnockbackForce);
            projectileHomingTurnSpeed = Mathf.Max(0f, projectileHomingTurnSpeed);
            projectileHomingDuration = Mathf.Max(0f, projectileHomingDuration);
            projectileHomingScanRadius = Mathf.Max(0f, projectileHomingScanRadius);
            areaAttackRange = Mathf.Max(0f, areaAttackRange);
            areaAttackMinRange = Mathf.Max(0f, areaAttackMinRange);
            areaAttackDuration = Mathf.Max(0.01f, areaAttackDuration);
            areaImpactTime = Mathf.Max(0f, areaImpactTime);
            areaRecoverDuration = Mathf.Max(0f, areaRecoverDuration);
            areaAttackCooldown = Mathf.Max(0f, areaAttackCooldown);
            areaDamage = Mathf.Max(0f, areaDamage);
            areaRadius = Mathf.Max(0f, areaRadius);
            areaLifetime = Mathf.Max(0f, areaLifetime);
            areaVisualScale = Mathf.Max(0f, areaVisualScale);
            areaInvincibilityDuration = Mathf.Max(0f, areaInvincibilityDuration);
            areaKnockbackForce = Mathf.Max(0f, areaKnockbackForce);
            hurtVisualDuration = Mathf.Max(0f, hurtVisualDuration);
            deathDestroyDelay = Mathf.Max(0f, deathDestroyDelay);
        }

        public void ApplyRuntimeData(EnemyData data)
        {
            if (data == null)
            {
                return;
            }

            maxHealth = Mathf.Max(1f, data.MaxHealth);
            moveSpeed = Mathf.Max(0f, data.MoveSpeed);

            if (data.TryGetAttackDamage(EnemyAttackType.Projectile, out float configuredProjectileDamage))
            {
                projectileDamage = Mathf.Max(0f, configuredProjectileDamage);
            }

            if (data.TryGetAttackDamage(EnemyAttackType.Slam, out float configuredAreaDamage))
            {
                areaDamage = Mathf.Max(0f, configuredAreaDamage);
            }

            ConfigureHealth();
            if (health != null)
            {
                health.SetHealth(maxHealth);
            }
        }

        private void ResolveBindings()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(includeInactive: true);
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>(includeInactive: true);
            }

            if (spriteRenderer != null && _visualRoot == null)
            {
                _visualRoot = spriteRenderer.transform;
            }

            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            if (bodyCollider == null)
            {
                bodyCollider = GetComponent<Collider2D>();
            }

            if (health == null)
            {
                health = GetComponent<Health>();
            }
        }

        private void CaptureVisualBaseScale()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            Transform currentVisualRoot = spriteRenderer.transform;
            if (_hasVisualBaseScale && _visualRoot == currentVisualRoot)
            {
                return;
            }

            _visualRoot = currentVisualRoot;
            _visualBaseScale = currentVisualRoot.localScale;
            _visualBaseScale.x = Mathf.Abs(_visualBaseScale.x);
            _hasVisualBaseScale = true;
        }

        private void ConfigureHealth()
        {
            if (health == null)
            {
                return;
            }

            health.InitialHealth = maxHealth;
            health.MaximumHealth = maxHealth;
            health.DestroyOnDeath = true;
            health.DelayBeforeDestruction = Mathf.Max(health.DelayBeforeDestruction, deathDestroyDelay);
            health.DisableModelOnDeath = false;

            if (health.CurrentHealth <= 0f)
            {
                health.SetHealth(maxHealth);
            }
        }

        private void TickState()
        {
            switch (_state)
            {
                case MageActionState.Idle:
                    if (HasValidTargetInDetectionRange())
                    {
                        EnterState(MageActionState.Approach);
                    }
                    else
                    {
                        EnterWanderMove();
                    }
                    break;
                case MageActionState.WanderMove:
                    if (HasValidTargetInDetectionRange())
                    {
                        EnterState(MageActionState.Approach);
                    }
                    else if (Time.time >= _stateEndsAt)
                    {
                        if (ShouldEnterWanderPause())
                        {
                            EnterState(MageActionState.WanderPause, wanderPauseDuration);
                        }
                        else
                        {
                            EnterWanderMove();
                        }
                    }
                    break;
                case MageActionState.WanderPause:
                    if (HasValidTargetInDetectionRange())
                    {
                        EnterState(MageActionState.Approach);
                    }
                    else if (Time.time >= _stateEndsAt)
                    {
                        EnterWanderMove();
                    }
                    break;
                case MageActionState.Approach:
                    FaceTarget();
                    if (CanStartAreaAttack())
                    {
                        EnterAreaAttack();
                    }
                    else if (CanStartProjectileAttack())
                    {
                        EnterProjectileAttack();
                    }
                    else if (!HasValidTargetInDetectionRange())
                    {
                        EnterWanderMove();
                    }
                    break;
                case MageActionState.ProjectileAttack:
                    TickProjectileAttack();
                    if (Time.time >= _stateEndsAt)
                    {
                        EnterState(MageActionState.Recover, projectileRecoverDuration);
                    }
                    break;
                case MageActionState.AreaAttack:
                    TickAreaAttack();
                    if (Time.time >= _stateEndsAt)
                    {
                        EnterState(MageActionState.Recover, areaRecoverDuration);
                    }
                    break;
                case MageActionState.Recover:
                    if (Time.time >= _stateEndsAt)
                    {
                        if (HasValidTargetInDetectionRange())
                        {
                            EnterState(MageActionState.Approach);
                        }
                        else
                        {
                            EnterWanderMove();
                        }
                    }
                    break;
                case MageActionState.Cooldown:
                    if (Time.time >= _stateEndsAt)
                    {
                        if (HasValidTargetInDetectionRange())
                        {
                            EnterState(MageActionState.Approach);
                        }
                        else
                        {
                            EnterWanderMove();
                        }
                    }
                    break;
            }
        }

        private void EnterProjectileAttack()
        {
            _currentAttackKind = MageAttackKind.Projectile;
            _attackTriggered = false;
            FaceTarget();
            _nextProjectileAttackAllowedAt = Time.time + projectileAttackCooldown;
            EnterState(MageActionState.ProjectileAttack, projectileAttackDuration);
        }

        private void EnterAreaAttack()
        {
            _currentAttackKind = MageAttackKind.Area;
            _attackTriggered = false;
            FaceTarget();
            _areaTargetPosition = IsTargetUsable() ? (Vector2)_target.position : (Vector2)transform.position + _lastFacingDirection;
            _nextAreaAttackAllowedAt = Time.time + areaAttackCooldown;
            EnterState(MageActionState.AreaAttack, areaAttackDuration);
            SpawnAreaAttack(_areaTargetPosition);
            _attackTriggered = true;
        }

        private void EnterState(MageActionState state, float duration = 0f)
        {
            _state = state;
            _stateStartedAt = Time.time;
            _stateEndsAt = duration > 0f ? Time.time + duration : 0f;

            if (state == MageActionState.Dead)
            {
                _hurtAnimationActive = false;
                PlayAnimatorState(deathStateName);
                return;
            }

            if (!_hurtAnimationActive)
            {
                PlayAnimatorStateForCurrentState();
            }
        }

        private void EnterWanderMove()
        {
            _wanderDirection = ResolveNewWanderDirection();
            _lastFacingDirection = _wanderDirection;
            ApplySpriteFlip(_lastFacingDirection);
            EnterState(MageActionState.WanderMove, wanderMoveDuration);
        }

        private void TickProjectileAttack()
        {
            FaceTarget();
            float elapsed = Time.time - _stateStartedAt;
            if (!_attackTriggered && elapsed >= projectileFireTime)
            {
                _attackTriggered = true;
                SpawnProjectile();
                PlayAnimatorState(projectileFireStateName);
            }
        }

        private void TickAreaAttack()
        {
            FaceTarget();
        }

        private void SpawnProjectile()
        {
            if (projectilePrefab == null)
            {
                return;
            }

            Vector2 direction = ResolveAttackDirection();
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            Vector2 spawnPosition = (Vector2)transform.position
                                    + direction * projectileSpawnOffset.x
                                    + perpendicular * projectileSpawnOffset.y;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            SkeletonMageProjectile projectile = Instantiate(
                projectilePrefab,
                new Vector3(spawnPosition.x, spawnPosition.y, transform.position.z),
                Quaternion.AngleAxis(angle, Vector3.forward));

            projectile.Initialize(
                gameObject,
                health,
                targetLayerMask,
                wallBlockMask,
                projectileHomingEnabled && IsTargetUsable() ? _target : null,
                direction,
                projectileSpeed,
                projectileDamage,
                projectileHitRadius,
                projectileLifetime,
                projectileVisualScale,
                projectileInvincibilityDuration,
                applyProjectileKnockback,
                projectileKnockbackForce,
                projectileHomingEnabled,
                projectileHomingTurnSpeed,
                projectileHomingDuration,
                projectileHomingScanRadius);
        }

        private void SpawnAreaAttack(Vector2 targetPosition)
        {
            if (areaAttackPrefab == null)
            {
                return;
            }

            SkeletonMageAreaAttack areaAttack = Instantiate(
                areaAttackPrefab,
                new Vector3(targetPosition.x, targetPosition.y, transform.position.z),
                Quaternion.identity);

            areaAttack.Initialize(
                gameObject,
                health,
                targetLayerMask,
                areaRadius,
                areaDamage,
                areaImpactTime,
                areaLifetime,
                areaVisualScale,
                areaInvincibilityDuration,
                applyAreaKnockback,
                areaKnockbackForce);
        }

        private bool CanStartProjectileAttack()
        {
            if (Time.time < _nextProjectileAttackAllowedAt || !IsTargetUsable())
            {
                return false;
            }

            float sqrDistance = (_target.position - transform.position).sqrMagnitude;
            return sqrDistance <= projectileAttackRange * projectileAttackRange
                   && sqrDistance >= projectileAttackMinRange * projectileAttackMinRange;
        }

        private bool CanStartAreaAttack()
        {
            if (Time.time < _nextAreaAttackAllowedAt || !IsTargetUsable())
            {
                return false;
            }

            float sqrDistance = (_target.position - transform.position).sqrMagnitude;
            return sqrDistance <= areaAttackRange * areaAttackRange
                   && sqrDistance >= areaAttackMinRange * areaAttackMinRange;
        }

        private float ResolveCooldownDuration()
        {
            return _currentAttackKind == MageAttackKind.Area ? areaAttackCooldown : projectileAttackCooldown;
        }

        private Vector2 ResolveAttackDirection()
        {
            if (IsTargetUsable())
            {
                Vector2 direction = _target.position - transform.position;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    _lastFacingDirection = direction.normalized;
                    return _lastFacingDirection;
                }
            }

            return _lastFacingDirection.sqrMagnitude > 0.0001f ? _lastFacingDirection.normalized : Vector2.right;
        }

        private void MoveDuringWander()
        {
            if (body == null || _wanderDirection.sqrMagnitude <= 0.0001f || moveSpeed <= 0f)
            {
                return;
            }

            Vector2 direction = _wanderDirection.normalized;
            float distance = moveSpeed * wanderSpeedMultiplier * Time.fixedDeltaTime;
            if (WouldHitWall(direction, distance + wanderWallProbeDistance))
            {
                EnterWanderMove();
                return;
            }

            _lastFacingDirection = direction;
            ApplySpriteFlip(_lastFacingDirection);
            body.MovePosition(body.position + direction * distance);
        }

        private void MoveForCombatRange()
        {
            if (body == null || !IsTargetUsable() || moveSpeed <= 0f)
            {
                return;
            }

            Vector2 toTarget = _target.position - transform.position;
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                StopBody();
                return;
            }

            float distanceToTarget = toTarget.magnitude;
            Vector2 direction = toTarget / distanceToTarget;
            _lastFacingDirection = direction;
            ApplySpriteFlip(_lastFacingDirection);

            if (distanceToTarget < minimumRange)
            {
                MoveInDirection(-direction);
                return;
            }

            if (distanceToTarget > preferredRange)
            {
                MoveInDirection(direction);
                return;
            }

            StrafeAroundTarget(direction);
        }

        private void StrafeAroundTarget(Vector2 directionToTarget)
        {
            if (body == null || combatStrafeSpeedMultiplier <= 0f || moveSpeed <= 0f)
            {
                StopBody();
                return;
            }

            if (Time.time >= _nextCombatStrafeTurnAt)
            {
                _nextCombatStrafeTurnAt = Time.time + combatStrafeTurnInterval;
                if (Random.value < 0.35f)
                {
                    _combatStrafeSign *= -1;
                }
            }

            Vector2 strafeDirection = new Vector2(-directionToTarget.y, directionToTarget.x) * _combatStrafeSign;
            if (strafeDirection.sqrMagnitude <= 0.0001f)
            {
                StopBody();
                return;
            }

            strafeDirection.Normalize();
            float distance = moveSpeed * combatStrafeSpeedMultiplier * Time.fixedDeltaTime;
            if (WouldHitWall(strafeDirection, distance))
            {
                _combatStrafeSign *= -1;
                strafeDirection = -strafeDirection;
                if (WouldHitWall(strafeDirection, distance))
                {
                    StopBody();
                    return;
                }
            }

            body.MovePosition(body.position + strafeDirection * distance);
        }

        private void MoveInDirection(Vector2 direction)
        {
            if (body == null || direction.sqrMagnitude <= 0.0001f)
            {
                StopBody();
                return;
            }

            direction.Normalize();
            float distance = moveSpeed * Time.fixedDeltaTime;
            if (WouldHitWall(direction, distance))
            {
                StopBody();
                return;
            }

            body.MovePosition(body.position + direction * distance);
        }

        private bool WouldHitWall(Vector2 direction, float distance)
        {
            if (bodyCollider == null
                || direction.sqrMagnitude <= 0.0001f
                || distance <= 0f
                || wallBlockMask.value == 0)
            {
                return false;
            }

            if (bodyCollider is BoxCollider2D boxCollider)
            {
                Vector2 center = boxCollider.transform.TransformPoint(boxCollider.offset);
                Vector2 size = Vector2.Scale(boxCollider.size, AbsScale(boxCollider.transform.lossyScale));
                return Physics2D.BoxCast(center, size, boxCollider.transform.eulerAngles.z, direction, distance, wallBlockMask).collider != null;
            }

            if (bodyCollider is CircleCollider2D circleCollider)
            {
                Vector2 center = circleCollider.transform.TransformPoint(circleCollider.offset);
                Vector2 scale = AbsScale(circleCollider.transform.lossyScale);
                float radius = circleCollider.radius * Mathf.Max(scale.x, scale.y);
                return Physics2D.CircleCast(center, radius, direction, distance, wallBlockMask).collider != null;
            }

            Bounds bounds = bodyCollider.bounds;
            return Physics2D.BoxCast(bounds.center, bounds.size, 0f, direction, distance, wallBlockMask).collider != null;
        }

        private void StopBody()
        {
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
        }

        private Vector2 ResolveNewWanderDirection()
        {
            for (int attempt = 0; attempt < wanderTurnAttempts; attempt++)
            {
                Vector2 candidate = Random.insideUnitCircle;
                if (candidate.sqrMagnitude <= 0.0001f)
                {
                    continue;
                }

                candidate.Normalize();
                if (!WouldHitWall(candidate, wanderWallProbeDistance))
                {
                    return candidate;
                }
            }

            return _wanderDirection.sqrMagnitude > 0.0001f ? -_wanderDirection.normalized : Vector2.right;
        }

        private bool ShouldEnterWanderPause()
        {
            return wanderPauseDuration > 0f
                   && wanderPauseChance > 0f
                   && Random.value <= wanderPauseChance;
        }

        private void RefreshTargetIfNeeded()
        {
            if (Time.time < _nextTargetRefreshTime && IsTargetUsable())
            {
                return;
            }

            _nextTargetRefreshTime = Time.time + 0.25f;
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null && IsLayerInMask(player.layer, targetLayerMask))
            {
                _target = player.transform;
                return;
            }

            Health[] candidates = FindObjectsByType<Health>(FindObjectsSortMode.None);
            float bestSqrDistance = float.PositiveInfinity;
            Transform bestTarget = null;
            Vector3 origin = transform.position;
            for (int i = 0; i < candidates.Length; i++)
            {
                Health candidate = candidates[i];
                if (candidate == null
                    || candidate == health
                    || candidate.CurrentHealth <= 0f
                    || !IsLayerInMask(candidate.gameObject.layer, targetLayerMask))
                {
                    continue;
                }

                float sqrDistance = (candidate.transform.position - origin).sqrMagnitude;
                if (sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    bestTarget = candidate.transform;
                }
            }

            _target = bestTarget;
        }

        private bool HasValidTargetInDetectionRange()
        {
            if (!IsTargetUsable())
            {
                return false;
            }

            return (_target.position - transform.position).sqrMagnitude <= detectionRadius * detectionRadius;
        }

        private bool IsTargetUsable()
        {
            return _target != null && _target.gameObject.activeInHierarchy;
        }

        private void FaceTarget()
        {
            if (!IsTargetUsable())
            {
                return;
            }

            Vector2 direction = _target.position - transform.position;
            if (direction.sqrMagnitude > 0.0001f)
            {
                _lastFacingDirection = direction.normalized;
            }

            ApplySpriteFlip(_lastFacingDirection);
        }

        private void UpdateAnimator()
        {
            if (_state == MageActionState.Dead)
            {
                return;
            }

            if (_hurtAnimationActive && Time.time >= _hurtVisualUntil)
            {
                _hurtAnimationActive = false;
                PlayAnimatorStateForCurrentState();
            }
        }

        private void PlayAnimatorStateForCurrentState()
        {
            switch (_state)
            {
                case MageActionState.WanderMove:
                case MageActionState.Approach:
                    PlayAnimatorState(walkStateName);
                    break;
                case MageActionState.ProjectileAttack:
                    PlayAnimatorState(projectileWindupStateName);
                    break;
                case MageActionState.AreaAttack:
                    PlayAnimatorState(areaAttackStateName);
                    break;
                case MageActionState.Dead:
                    PlayAnimatorState(deathStateName);
                    break;
                default:
                    PlayAnimatorState(idleStateName);
                    break;
            }
        }

        private void PlayAnimatorState(string stateName)
        {
            if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(stateName))
            {
                return;
            }

            int stateHash = Animator.StringToHash("Base Layer." + stateName);
            if (!animator.HasState(0, stateHash))
            {
                stateHash = Animator.StringToHash(stateName);
                if (!animator.HasState(0, stateHash))
                {
                    return;
                }
            }

            ApplySpriteFlip(_lastFacingDirection);
            animator.Play(stateHash, 0, 0f);
            animator.Update(0f);
        }

        private void ApplySpriteFlip(Vector2 direction)
        {
            if (Mathf.Abs(direction.x) <= 0.0001f)
            {
                return;
            }

            bool shouldFlip = (flipSpriteOnPositiveX && direction.x > 0f)
                              || (!flipSpriteOnPositiveX && direction.x < 0f);

            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = false;
            }

            if (!_hasVisualBaseScale)
            {
                CaptureVisualBaseScale();
            }

            if (_visualRoot == null)
            {
                return;
            }

            Vector3 scale = _visualBaseScale;
            scale.x = Mathf.Abs(_visualBaseScale.x) * (shouldFlip ? -1f : 1f);
            _visualRoot.localScale = scale;
        }

        private void HandleHit()
        {
            if (_state == MageActionState.Dead)
            {
                return;
            }

            _hurtVisualUntil = Time.time + hurtVisualDuration;
            _hurtAnimationActive = true;
            PlayAnimatorState(hurtStateName);
        }

        private void HandleDeath()
        {
            if (_state == MageActionState.Dead)
            {
                return;
            }

            StopBody();
            EnterState(MageActionState.Dead);
        }

        private static Vector2 AbsScale(Vector3 scale)
        {
            return new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        }

        private static bool IsLayerInMask(int layer, LayerMask mask)
        {
            return (mask.value & (1 << layer)) != 0;
        }
    }
}
