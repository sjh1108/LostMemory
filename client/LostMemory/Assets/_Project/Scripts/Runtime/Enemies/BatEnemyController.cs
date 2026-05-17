using System.Collections.Generic;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Bat Enemy Controller")]
    public sealed class BatEnemyController : MonoBehaviour
    {
        private enum BatActionState
        {
            Idle,
            WanderMove,
            WanderPause,
            Approach,
            PreAttack,
            Attack,
            Recover,
            Cooldown,
            Dead
        }

        [Header("Bindings")]
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Collider2D bodyCollider;
        [SerializeField] private BoxCollider2D attackHitbox;
        [SerializeField] private Health health;

        [Header("Animation States")]
        [SerializeField] private string idleStateName = "bat1 idle";
        [SerializeField] private string flyStateName = "bat1 flying";
        [SerializeField] private string preAttackStateName = "bat1 flying to atk instance";
        [SerializeField] private string attackStateName = "bat1 atk";
        [SerializeField] private string hurtStateName = "bat1 hurt";
        [SerializeField] private string deathStateName = "bat1 death";
        [SerializeField] private bool flipSpriteOnPositiveX;

        [Header("Stats")]
        [SerializeField, Min(1f)] private float maxHealth = 25f;
        [SerializeField, Min(0f)] private float moveSpeed = 3.2f;
        [SerializeField, Min(0f)] private float detectionRadius = 7f;
        [SerializeField, Min(0f)] private float attackRange = 0.9f;
        [SerializeField, Min(0f)] private float desiredAttackDistance = 0.65f;

        [Header("Approach Movement")]
        [SerializeField] private bool approachZigzagEnabled = true;
        [SerializeField, Range(0f, 1.5f)] private float approachZigzagLateralWeight = 0.75f;
        [SerializeField, Min(0.1f)] private float approachZigzagFrequency = 2.4f;
        [SerializeField, Min(1f)] private float approachZigzagPeakSpeedMultiplier = 1.25f;
        [SerializeField, Min(0f)] private float approachZigzagMinTargetDistance = 1.25f;

        [Header("Wander")]
        [SerializeField, Min(0f)] private float wanderSpeedMultiplier = 0.55f;
        [SerializeField, Min(0.1f)] private float wanderMoveDuration = 1.4f;
        [SerializeField, Min(0f)] private float wanderPauseDuration = 0.25f;
        [SerializeField, Range(0f, 1f)] private float wanderPauseChance = 0.15f;
        [SerializeField, Min(1)] private int wanderTurnAttempts = 6;
        [SerializeField, Min(0f)] private float wanderWallProbeDistance = 0.3f;

        [Header("Flight Collision")]
        [Tooltip("Layers that block bat flight. Keep this to actual walls/doors.")]
        [SerializeField] private LayerMask wallBlockMask = (1 << 11) | (1 << 24);
        [Tooltip("Layers ignored by the bat body collider. Use this for non-wall props/holes.")]
        [SerializeField] private LayerMask ignoredCollisionLayers = (1 << 8) | (1 << 12) | (1 << 14) | (1 << 15) | (1 << 16) | (1 << 17);

        [Header("Attack")]
        [SerializeField, Min(0f)] private float preAttackDuration = 0.25f;
        [SerializeField, Min(0f)] private float attackWindup = 0.08f;
        [SerializeField, Min(0.01f)] private float attackActiveDuration = 0.2f;
        [SerializeField, Min(0f)] private float attackRecoverDuration = 0.25f;
        [SerializeField, Min(0f)] private float attackCooldown = 1.35f;
        [SerializeField, Min(0f)] private float attackDamage = 7f;
        [SerializeField, Min(0f)] private float attackInvincibilityDuration = 0.45f;
        [SerializeField] private Vector2 attackHitboxBaseOffset = new Vector2(0f, 0.05f);
        [SerializeField, Min(0f)] private float attackHitboxDistance = 0.45f;
        [SerializeField] private bool applyAttackKnockback = true;
        [SerializeField, Min(0f)] private float attackKnockbackForce = 160f;
        [SerializeField] private LayerMask targetLayerMask = 1 << 10;
        [SerializeField, Min(1)] private int attackOverlapBufferSize = 8;

        [Header("Hit Reaction")]
        [SerializeField, Min(0f)] private float hurtVisualDuration = 0.16f;
        [SerializeField, Min(0f)] private float deathDestroyDelay = 0.65f;

        private readonly HashSet<Health> _hitTargetsThisAttack = new HashSet<Health>();
        private Collider2D[] _attackOverlapBuffer;
        private Transform _target;
        private BatActionState _state = BatActionState.Idle;
        private float _stateStartedAt;
        private float _stateEndsAt;
        private float _nextTargetRefreshTime;
        private float _nextAttackAllowedAt;
        private float _hurtVisualUntil;
        private float _approachZigzagPhase;
        private Vector2 _wanderDirection = Vector2.right;
        private Vector2 _lastFacingDirection = Vector2.down;
        private Transform _visualRoot;
        private Vector3 _visualBaseScale;
        private bool _hasVisualBaseScale;
        private bool _attackHitboxActive;
        private bool _hurtAnimationActive;

        private void Reset()
        {
            ResolveBindings();
        }

        private void Awake()
        {
            ResolveBindings();
            CaptureVisualBaseScale();
            EnsureAttackOverlapBuffer();
            ConfigureFlightCollision();
            ConfigureHealth();
            SetAttackHitboxActive(false);
            EnterState(BatActionState.Idle);
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
            if (_state != BatActionState.Dead)
            {
                RefreshTargetIfNeeded();
                TickState();
            }

            UpdateAnimator();
        }

        private void LateUpdate()
        {
            if (_state != BatActionState.Dead)
            {
                ApplySpriteFlip(_lastFacingDirection);
            }
        }

        private void FixedUpdate()
        {
            if (_state == BatActionState.Approach)
            {
                MoveTowardTarget();
            }
            else if (_state == BatActionState.WanderMove)
            {
                MoveDuringWander();
            }
            else
            {
                StopBody();
            }

            if (_state == BatActionState.Attack)
            {
                ScanAttackTargets();
            }
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            moveSpeed = Mathf.Max(0f, moveSpeed);
            detectionRadius = Mathf.Max(0f, detectionRadius);
            attackRange = Mathf.Max(0f, attackRange);
            desiredAttackDistance = Mathf.Max(0f, desiredAttackDistance);
            approachZigzagFrequency = Mathf.Max(0.1f, approachZigzagFrequency);
            approachZigzagPeakSpeedMultiplier = Mathf.Max(1f, approachZigzagPeakSpeedMultiplier);
            approachZigzagMinTargetDistance = Mathf.Max(0f, approachZigzagMinTargetDistance);
            wanderSpeedMultiplier = Mathf.Max(0f, wanderSpeedMultiplier);
            wanderMoveDuration = Mathf.Max(0.1f, wanderMoveDuration);
            wanderPauseDuration = Mathf.Max(0f, wanderPauseDuration);
            wanderTurnAttempts = Mathf.Max(1, wanderTurnAttempts);
            wanderWallProbeDistance = Mathf.Max(0f, wanderWallProbeDistance);
            attackActiveDuration = Mathf.Max(0.01f, attackActiveDuration);
            attackHitboxDistance = Mathf.Max(0f, attackHitboxDistance);
            attackOverlapBufferSize = Mathf.Max(1, attackOverlapBufferSize);
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

            if (data.TryGetAttackDamage(EnemyAttackType.Melee, out float meleeDamage))
            {
                attackDamage = Mathf.Max(0f, meleeDamage);
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

        private void ConfigureFlightCollision()
        {
            if (body != null)
            {
                body.gravityScale = 0f;
                body.excludeLayers = CombineLayerMasks(body.excludeLayers, ignoredCollisionLayers);
            }

            if (bodyCollider != null)
            {
                bodyCollider.excludeLayers = CombineLayerMasks(bodyCollider.excludeLayers, ignoredCollisionLayers);
            }
        }

        private static LayerMask CombineLayerMasks(LayerMask current, LayerMask additional)
        {
            return new LayerMask { value = current.value | additional.value };
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
                case BatActionState.Idle:
                    if (HasValidTargetInDetectionRange())
                    {
                        EnterState(BatActionState.Approach);
                    }
                    else
                    {
                        EnterWanderMove();
                    }
                    break;
                case BatActionState.WanderMove:
                    if (HasValidTargetInDetectionRange())
                    {
                        EnterState(BatActionState.Approach);
                    }
                    else if (Time.time >= _stateEndsAt)
                    {
                        if (ShouldEnterWanderPause())
                        {
                            EnterState(BatActionState.WanderPause, wanderPauseDuration);
                        }
                        else
                        {
                            EnterWanderMove();
                        }
                    }
                    break;
                case BatActionState.WanderPause:
                    if (HasValidTargetInDetectionRange())
                    {
                        EnterState(BatActionState.Approach);
                    }
                    else if (Time.time >= _stateEndsAt)
                    {
                        EnterWanderMove();
                    }
                    break;
                case BatActionState.Approach:
                    FaceTarget();
                    if (CanStartAttack())
                    {
                        EnterState(BatActionState.PreAttack, preAttackDuration);
                    }
                    else if (!HasValidTargetInDetectionRange())
                    {
                        EnterWanderMove();
                    }
                    break;
                case BatActionState.PreAttack:
                    FaceTarget();
                    if (Time.time >= _stateEndsAt)
                    {
                        EnterAttack();
                    }
                    break;
                case BatActionState.Attack:
                    UpdateAttackHitboxState();
                    if (Time.time >= _stateEndsAt)
                    {
                        SetAttackHitboxActive(false);
                        EnterState(BatActionState.Recover, attackRecoverDuration);
                    }
                    break;
                case BatActionState.Recover:
                    if (Time.time >= _stateEndsAt)
                    {
                        EnterState(BatActionState.Cooldown, attackCooldown);
                    }
                    break;
                case BatActionState.Cooldown:
                    if (Time.time >= _stateEndsAt)
                    {
                        if (HasValidTargetInDetectionRange())
                        {
                            EnterState(BatActionState.Approach);
                        }
                        else
                        {
                            EnterWanderMove();
                        }
                    }
                    break;
            }
        }

        private void EnterAttack()
        {
            FaceTarget();
            PositionAttackHitbox();
            _hitTargetsThisAttack.Clear();
            _nextAttackAllowedAt = Time.time + attackCooldown;
            EnterState(BatActionState.Attack, attackWindup + attackActiveDuration);
            UpdateAttackHitboxState();
        }

        private void EnterState(BatActionState state, float duration = 0f)
        {
            _state = state;
            _stateStartedAt = Time.time;
            _stateEndsAt = duration > 0f ? Time.time + duration : 0f;

            if (state != BatActionState.Attack)
            {
                SetAttackHitboxActive(false);
            }

            if (state == BatActionState.Approach)
            {
                _approachZigzagPhase = Random.value;
            }

            if (state == BatActionState.Dead)
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

        private void MoveTowardTarget()
        {
            if (body == null || !IsTargetUsable() || moveSpeed <= 0f)
            {
                return;
            }

            Vector2 toTarget = _target.position - transform.position;
            if (toTarget.sqrMagnitude <= desiredAttackDistance * desiredAttackDistance)
            {
                StopBody();
                return;
            }

            Vector2 directDirection = toTarget.normalized;
            float zigzagIntensity;
            Vector2 direction = ResolveApproachDirection(directDirection, toTarget.magnitude, out zigzagIntensity);
            _lastFacingDirection = direction;
            float distance = moveSpeed * ResolveApproachSpeedMultiplier(zigzagIntensity) * Time.fixedDeltaTime;
            if (WouldHitWall(direction, distance))
            {
                direction = directDirection;
                distance = moveSpeed * Time.fixedDeltaTime;
                if (WouldHitWall(direction, distance))
                {
                    StopBody();
                    return;
                }
            }

            body.MovePosition(body.position + direction * distance);
        }

        private Vector2 ResolveApproachDirection(Vector2 directDirection, float targetDistance, out float zigzagIntensity)
        {
            zigzagIntensity = 0f;
            if (!approachZigzagEnabled
                || approachZigzagLateralWeight <= 0f
                || targetDistance <= approachZigzagMinTargetDistance)
            {
                return directDirection;
            }

            float elapsed = Time.time - _stateStartedAt + _approachZigzagPhase;
            float signal = Mathf.Sin(elapsed * approachZigzagFrequency * Mathf.PI * 2f);
            zigzagIntensity = Mathf.Abs(signal);

            Vector2 lateralDirection = new Vector2(-directDirection.y, directDirection.x);
            Vector2 zigzagDirection = directDirection + lateralDirection * (signal * approachZigzagLateralWeight);
            return zigzagDirection.sqrMagnitude > 0.0001f ? zigzagDirection.normalized : directDirection;
        }

        private float ResolveApproachSpeedMultiplier(float zigzagIntensity)
        {
            if (!approachZigzagEnabled || zigzagIntensity <= 0f)
            {
                return 1f;
            }

            return Mathf.Lerp(1f, approachZigzagPeakSpeedMultiplier, zigzagIntensity);
        }

        private void EnterWanderMove()
        {
            _wanderDirection = ResolveNewWanderDirection();
            _lastFacingDirection = _wanderDirection;
            ApplySpriteFlip(_lastFacingDirection);
            EnterState(BatActionState.WanderMove, wanderMoveDuration);
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

            Vector2 fallback = _wanderDirection.sqrMagnitude > 0.0001f
                ? -_wanderDirection.normalized
                : _lastFacingDirection.sqrMagnitude > 0.0001f
                    ? -_lastFacingDirection.normalized
                    : Vector2.down;

            return fallback;
        }

        private bool ShouldEnterWanderPause()
        {
            return wanderPauseDuration > 0f
                   && wanderPauseChance > 0f
                   && Random.value <= wanderPauseChance;
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

            Health[] candidates = FindObjectsOfType<Health>();
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

            float maxSqrDistance = detectionRadius * detectionRadius;
            return (_target.position - transform.position).sqrMagnitude <= maxSqrDistance;
        }

        private bool CanStartAttack()
        {
            return Time.time >= _nextAttackAllowedAt
                   && IsTargetUsable()
                   && (_target.position - transform.position).sqrMagnitude <= attackRange * attackRange;
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

        private void UpdateAttackHitboxState()
        {
            float elapsed = Time.time - _stateStartedAt;
            bool shouldBeActive = elapsed >= attackWindup && elapsed <= attackWindup + attackActiveDuration;
            if (shouldBeActive)
            {
                PositionAttackHitbox();
            }

            SetAttackHitboxActive(shouldBeActive);
        }

        private void PositionAttackHitbox()
        {
            if (attackHitbox == null)
            {
                return;
            }

            Vector2 direction = _lastFacingDirection.sqrMagnitude > 0.0001f
                ? _lastFacingDirection.normalized
                : Vector2.down;
            Vector2 localPosition = attackHitboxBaseOffset + direction * attackHitboxDistance;
            attackHitbox.transform.localPosition = localPosition;
        }

        private void SetAttackHitboxActive(bool active)
        {
            _attackHitboxActive = active;
            if (attackHitbox != null)
            {
                attackHitbox.enabled = active;
            }
        }

        private void ScanAttackTargets()
        {
            if (!_attackHitboxActive || attackHitbox == null || attackDamage <= 0f)
            {
                return;
            }

            EnsureAttackOverlapBuffer();
            Vector2 center = attackHitbox.transform.TransformPoint(attackHitbox.offset);
            Vector2 size = Vector2.Scale(attackHitbox.size, AbsScale(attackHitbox.transform.lossyScale));
            int count = Physics2D.OverlapBoxNonAlloc(
                center,
                size,
                attackHitbox.transform.eulerAngles.z,
                _attackOverlapBuffer,
                targetLayerMask);

            for (int i = 0; i < count; i++)
            {
                Collider2D targetCollider = _attackOverlapBuffer[i];
                if (targetCollider == null)
                {
                    continue;
                }

                TryDamageTarget(targetCollider);
            }
        }

        private void TryDamageTarget(Collider2D targetCollider)
        {
            Health targetHealth = ResolveHealth(targetCollider);
            if (targetHealth == null
                || targetHealth == health
                || _hitTargetsThisAttack.Contains(targetHealth)
                || targetHealth.CurrentHealth <= 0f
                || !targetHealth.CanTakeDamageThisFrame())
            {
                return;
            }

            Vector3 direction = targetHealth.transform.position - transform.position;
            direction.z = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = _lastFacingDirection;
            }

            direction.Normalize();
            _hitTargetsThisAttack.Add(targetHealth);
            targetHealth.Damage(attackDamage, gameObject, 0f, attackInvincibilityDuration, direction);
            ApplyAttackKnockback(targetHealth, direction);
        }

        private static Health ResolveHealth(Component target)
        {
            Health targetHealth = target.GetComponent<Health>();
            return targetHealth != null ? targetHealth : target.GetComponentInParent<Health>();
        }

        private void ApplyAttackKnockback(Health targetHealth, Vector3 direction)
        {
            if (!applyAttackKnockback
                || attackKnockbackForce <= 0f
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

            Vector3 knockback = direction.normalized * attackKnockbackForce;
            knockback *= targetHealth.KnockbackForceMultiplier;
            knockback = targetHealth.ComputeKnockbackForce(knockback);
            if (knockback.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            controller.Impact(knockback.normalized, knockback.magnitude);
        }

        private void UpdateAnimator()
        {
            if (_state == BatActionState.Dead)
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
                case BatActionState.WanderMove:
                case BatActionState.Approach:
                    PlayAnimatorState(flyStateName);
                    break;
                case BatActionState.PreAttack:
                    PlayAnimatorState(preAttackStateName);
                    break;
                case BatActionState.Attack:
                    PlayAnimatorState(attackStateName);
                    break;
                case BatActionState.Dead:
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
            if (_state == BatActionState.Dead)
            {
                return;
            }

            _hurtVisualUntil = Time.time + hurtVisualDuration;
            _hurtAnimationActive = true;
            PlayAnimatorState(hurtStateName);
        }

        private void HandleDeath()
        {
            if (_state == BatActionState.Dead)
            {
                return;
            }

            StopBody();
            SetAttackHitboxActive(false);
            EnterState(BatActionState.Dead);
        }

        private void EnsureAttackOverlapBuffer()
        {
            int size = Mathf.Max(1, attackOverlapBufferSize);
            if (_attackOverlapBuffer == null || _attackOverlapBuffer.Length != size)
            {
                _attackOverlapBuffer = new Collider2D[size];
            }
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
