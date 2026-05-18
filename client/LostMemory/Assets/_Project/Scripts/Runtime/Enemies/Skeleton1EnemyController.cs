using System.Collections.Generic;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Skeleton1 Enemy Controller")]
    public sealed class Skeleton1EnemyController : MonoBehaviour
    {
        private enum SkeletonActionState
        {
            Idle,
            WanderMove,
            WanderPause,
            Approach,
            Attack,
            Recover,
            Cooldown,
            Dead
        }

        private static readonly Color AttackHitboxGizmoColor = new Color(1f, 0.15f, 0.1f, 0.9f);

        [Header("Bindings")]
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Collider2D bodyCollider;
        [SerializeField] private BoxCollider2D attackHitbox;
        [SerializeField] private Health health;

        [Header("Animation States")]
        [SerializeField] private string idleStateName = "skeleton 1 idle";
        [SerializeField] private string walkStateName = "skeleton 1 walk";
        [SerializeField] private string attackStateName = "skeleton 1 atk";
        [SerializeField] private string hurtStateName = "skeleton 1 hurt";
        [SerializeField] private string deathStateName = "skeleton 1 death";
        [SerializeField] private bool flipSpriteOnPositiveX;

        [Header("Stats")]
        [SerializeField, Min(1f)] private float maxHealth = 30f;
        [SerializeField, Min(0f)] private float moveSpeed = 2.35f;
        [SerializeField, Min(0f)] private float detectionRadius = 6f;
        [SerializeField, Min(0f)] private float desiredAttackDistance = 0.9f;
        [SerializeField] private LayerMask wallBlockMask = (1 << 8) | (1 << 11) | (1 << 24);
        [SerializeField] private LayerMask targetLayerMask = 1 << 10;

        [Header("Wander")]
        [SerializeField, Min(0f)] private float wanderSpeedMultiplier = 0.45f;
        [SerializeField, Min(0.1f)] private float wanderMoveDuration = 1.25f;
        [SerializeField, Min(0f)] private float wanderPauseDuration = 0.55f;
        [SerializeField, Range(0f, 1f)] private float wanderPauseChance = 0.35f;
        [SerializeField, Min(1)] private int wanderTurnAttempts = 8;
        [SerializeField, Min(0f)] private float wanderWallProbeDistance = 0.2f;

        [Header("Attack")]
        [SerializeField, Min(0.01f)] private float attackDuration = 0.9f;
        [SerializeField, Min(0f)] private float attackWindup = 0.5f;
        [SerializeField, Min(0.01f)] private float attackActiveDuration = 0.2f;
        [SerializeField, Min(0f)] private float attackLungeStart = 0.32f;
        [SerializeField, Min(0.01f)] private float attackLungeDuration = 0.24f;
        [SerializeField, Min(0f)] private float attackLungeDistance = 0.58f;
        [SerializeField, Range(0f, 1f)] private float attackKnockbackResistanceMultiplier = 0.35f;
        [SerializeField] private bool suppressHurtAnimationDuringAttack = true;
        [SerializeField, Min(0f)] private float attackRecoverDuration = 0.12f;
        [SerializeField, Min(0f)] private float attackCooldown = 0.75f;
        [SerializeField, Min(0f)] private float attackDamage = 7f;
        [SerializeField, Min(0f)] private float attackInvincibilityDuration = 0.45f;
        [SerializeField] private Vector2 attackHitboxBaseOffset = new Vector2(0f, 0.08f);
        [SerializeField, Min(0f)] private float attackHitboxDistance = 0.65f;
        [SerializeField] private bool applyAttackKnockback = true;
        [SerializeField, Min(0f)] private float attackKnockbackForce = 170f;
        [SerializeField, Min(1)] private int attackOverlapBufferSize = 8;

        [Header("Hit Reaction")]
        [SerializeField, Min(0f)] private float hurtVisualDuration = 0.16f;
        [SerializeField, Min(0f)] private float deathDestroyDelay = 0.8f;

        private readonly HashSet<Health> _hitTargetsThisAttack = new HashSet<Health>();
        private Collider2D[] _attackOverlapBuffer;
        private Transform _target;
        private SkeletonActionState _state = SkeletonActionState.Idle;
        private float _stateStartedAt;
        private float _stateEndsAt;
        private float _nextTargetRefreshTime;
        private float _nextAttackAllowedAt;
        private float _hurtVisualUntil;
        private Vector2 _wanderDirection = Vector2.right;
        private Vector2 _lastFacingDirection = Vector2.right;
        private Transform _visualRoot;
        private Vector3 _visualBaseScale;
        private bool _hasVisualBaseScale;
        private bool _attackHitboxActive;
        private bool _hurtAnimationActive;
        private Vector2 _attackLungeDirection = Vector2.right;
        private float _attackLungeDistanceMoved;
        private float _knockbackForceMultiplierBeforeAttack = 1f;
        private bool _attackKnockbackResistanceActive;

        private void Reset()
        {
            ResolveBindings();
        }

        private void Awake()
        {
            ResolveBindings();
            CaptureVisualBaseScale();
            EnsureAttackOverlapBuffer();
            ConfigureHealth();
            SetAttackHitboxActive(false);
            EnterState(SkeletonActionState.Idle);
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
            DeactivateAttackKnockbackResistance();
            if (health != null)
            {
                health.OnHit -= HandleHit;
                health.OnDeath -= HandleDeath;
            }
        }

        private void Update()
        {
            if (_state != SkeletonActionState.Dead)
            {
                RefreshTargetIfNeeded();
                TickState();
            }

            UpdateAnimator();
        }

        private void LateUpdate()
        {
            if (_state != SkeletonActionState.Dead)
            {
                ApplySpriteFlip(_lastFacingDirection);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Color previousColor = Gizmos.color;
            Matrix4x4 previousMatrix = Gizmos.matrix;

            Gizmos.matrix = Matrix4x4.identity;
            DrawAttackHitboxGizmo();

            Gizmos.color = previousColor;
            Gizmos.matrix = previousMatrix;
        }

        private void FixedUpdate()
        {
            if (_state == SkeletonActionState.Attack)
            {
                MoveDuringAttackLunge();
                ScanAttackTargets();
                return;
            }

            if (_state == SkeletonActionState.WanderMove)
            {
                MoveDuringWander();
            }
            else if (_state == SkeletonActionState.Approach)
            {
                MoveTowardTarget();
            }
            else
            {
                StopBody();
            }
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            moveSpeed = Mathf.Max(0f, moveSpeed);
            detectionRadius = Mathf.Max(0f, detectionRadius);
            desiredAttackDistance = Mathf.Max(0f, desiredAttackDistance);
            wanderSpeedMultiplier = Mathf.Max(0f, wanderSpeedMultiplier);
            wanderMoveDuration = Mathf.Max(0.1f, wanderMoveDuration);
            wanderPauseDuration = Mathf.Max(0f, wanderPauseDuration);
            wanderTurnAttempts = Mathf.Max(1, wanderTurnAttempts);
            wanderWallProbeDistance = Mathf.Max(0f, wanderWallProbeDistance);
            attackDuration = Mathf.Max(0.01f, attackDuration);
            attackWindup = Mathf.Max(0f, attackWindup);
            attackActiveDuration = Mathf.Max(0.01f, attackActiveDuration);
            attackLungeStart = Mathf.Max(0f, attackLungeStart);
            attackLungeDuration = Mathf.Max(0.01f, attackLungeDuration);
            attackLungeDistance = Mathf.Max(0f, attackLungeDistance);
            attackKnockbackResistanceMultiplier = Mathf.Clamp01(attackKnockbackResistanceMultiplier);
            attackRecoverDuration = Mathf.Max(0f, attackRecoverDuration);
            attackCooldown = Mathf.Max(0f, attackCooldown);
            attackDamage = Mathf.Max(0f, attackDamage);
            attackInvincibilityDuration = Mathf.Max(0f, attackInvincibilityDuration);
            attackHitboxDistance = Mathf.Max(0f, attackHitboxDistance);
            attackKnockbackForce = Mathf.Max(0f, attackKnockbackForce);
            attackOverlapBufferSize = Mathf.Max(1, attackOverlapBufferSize);
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

        private void ActivateAttackKnockbackResistance()
        {
            if (health == null || _attackKnockbackResistanceActive)
            {
                return;
            }

            _knockbackForceMultiplierBeforeAttack = health.KnockbackForceMultiplier;
            health.KnockbackForceMultiplier = _knockbackForceMultiplierBeforeAttack * attackKnockbackResistanceMultiplier;
            _attackKnockbackResistanceActive = true;
        }

        private void DeactivateAttackKnockbackResistance()
        {
            if (health == null || !_attackKnockbackResistanceActive)
            {
                return;
            }

            health.KnockbackForceMultiplier = _knockbackForceMultiplierBeforeAttack;
            _attackKnockbackResistanceActive = false;
        }

        private void TickState()
        {
            switch (_state)
            {
                case SkeletonActionState.Idle:
                    if (HasValidTargetInDetectionRange())
                    {
                        EnterState(SkeletonActionState.Approach);
                    }
                    else
                    {
                        EnterWanderMove();
                    }
                    break;
                case SkeletonActionState.WanderMove:
                    if (HasValidTargetInDetectionRange())
                    {
                        EnterState(SkeletonActionState.Approach);
                    }
                    else if (Time.time >= _stateEndsAt)
                    {
                        if (ShouldEnterWanderPause())
                        {
                            EnterState(SkeletonActionState.WanderPause, wanderPauseDuration);
                        }
                        else
                        {
                            EnterWanderMove();
                        }
                    }
                    break;
                case SkeletonActionState.WanderPause:
                    if (HasValidTargetInDetectionRange())
                    {
                        EnterState(SkeletonActionState.Approach);
                    }
                    else if (Time.time >= _stateEndsAt)
                    {
                        EnterWanderMove();
                    }
                    break;
                case SkeletonActionState.Approach:
                    FaceTarget();
                    if (CanStartAttack())
                    {
                        EnterAttack();
                    }
                    else if (!HasValidTargetInDetectionRange())
                    {
                        EnterWanderMove();
                    }
                    break;
                case SkeletonActionState.Attack:
                    UpdateAttackHitboxState();
                    if (Time.time >= _stateEndsAt)
                    {
                        SetAttackHitboxActive(false);
                        EnterState(SkeletonActionState.Recover, attackRecoverDuration);
                    }
                    break;
                case SkeletonActionState.Recover:
                    if (Time.time >= _stateEndsAt)
                    {
                        EnterState(SkeletonActionState.Cooldown, attackCooldown);
                    }
                    break;
                case SkeletonActionState.Cooldown:
                    if (Time.time >= _stateEndsAt)
                    {
                        if (HasValidTargetInDetectionRange())
                        {
                            EnterState(SkeletonActionState.Approach);
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
            _attackLungeDirection = _lastFacingDirection.sqrMagnitude > 0.0001f
                ? _lastFacingDirection.normalized
                : Vector2.right;
            _attackLungeDistanceMoved = 0f;
            PositionAttackHitbox();
            _hitTargetsThisAttack.Clear();
            _nextAttackAllowedAt = Time.time + attackCooldown;
            EnterState(SkeletonActionState.Attack, attackDuration);
            UpdateAttackHitboxState();
        }

        private void EnterState(SkeletonActionState state, float duration = 0f)
        {
            _state = state;
            _stateStartedAt = Time.time;
            _stateEndsAt = duration > 0f ? Time.time + duration : 0f;

            if (state != SkeletonActionState.Attack)
            {
                DeactivateAttackKnockbackResistance();
                SetAttackHitboxActive(false);
            }
            else
            {
                ActivateAttackKnockbackResistance();
            }

            if (state == SkeletonActionState.Dead)
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
            EnterState(SkeletonActionState.WanderMove, wanderMoveDuration);
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

        private void MoveTowardTarget()
        {
            if (body == null || !IsTargetUsable() || moveSpeed <= 0f)
            {
                return;
            }

            Vector2 toTarget = _target.position - transform.position;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                _lastFacingDirection = toTarget.normalized;
                ApplySpriteFlip(_lastFacingDirection);
            }

            if (IsTargetInsideAttackStartArea())
            {
                StopBody();
                return;
            }

            Vector2 direction = toTarget.normalized;
            _lastFacingDirection = direction;
            float distance = moveSpeed * Time.fixedDeltaTime;
            if (WouldHitWall(direction, distance))
            {
                StopBody();
                return;
            }

            body.MovePosition(body.position + direction * distance);
        }

        private void MoveDuringAttackLunge()
        {
            if (body == null
                || attackLungeDistance <= 0f
                || attackLungeDuration <= 0f
                || _attackLungeDistanceMoved >= attackLungeDistance)
            {
                StopBody();
                return;
            }

            float elapsed = Time.time - _stateStartedAt;
            if (elapsed < attackLungeStart || elapsed > attackLungeStart + attackLungeDuration)
            {
                StopBody();
                return;
            }

            Vector2 direction = _attackLungeDirection.sqrMagnitude > 0.0001f
                ? _attackLungeDirection.normalized
                : Vector2.right;
            float lungeSpeed = attackLungeDistance / attackLungeDuration;
            float remainingDistance = attackLungeDistance - _attackLungeDistanceMoved;
            float distance = Mathf.Min(lungeSpeed * Time.fixedDeltaTime, remainingDistance);
            if (distance <= 0f)
            {
                StopBody();
                return;
            }

            if (WouldHitWall(direction, distance))
            {
                _attackLungeDistanceMoved = attackLungeDistance;
                StopBody();
                return;
            }

            _lastFacingDirection = direction;
            ApplySpriteFlip(_lastFacingDirection);
            body.MovePosition(body.position + direction * distance);
            _attackLungeDistanceMoved += distance;
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
                   && IsTargetInsideAttackStartArea();
        }

        private bool IsTargetInsideAttackStartArea()
        {
            if (!IsTargetUsable())
            {
                return false;
            }

            if (IsTargetOverlappingPredictedAttackHitbox())
            {
                return true;
            }

            if (attackHitbox != null)
            {
                return false;
            }

            return (_target.position - transform.position).sqrMagnitude <= desiredAttackDistance * desiredAttackDistance;
        }

        private bool IsTargetOverlappingPredictedAttackHitbox()
        {
            if (!TryResolveAttackHitboxPreview(out Vector2 center, out Vector2 size, out float angle))
            {
                return false;
            }

            EnsureAttackOverlapBuffer();
            int count = Physics2D.OverlapBoxNonAlloc(
                center,
                size,
                angle,
                _attackOverlapBuffer,
                targetLayerMask);

            for (int i = 0; i < count; i++)
            {
                Collider2D targetCollider = _attackOverlapBuffer[i];
                if (IsTargetCollider(targetCollider))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsTargetCollider(Collider2D targetCollider)
        {
            if (targetCollider == null || _target == null)
            {
                return false;
            }

            Transform targetColliderTransform = targetCollider.transform;
            return targetColliderTransform == _target
                   || targetColliderTransform.IsChildOf(_target)
                   || _target.IsChildOf(targetColliderTransform);
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
                : Vector2.right;
            Vector2 localPosition = attackHitboxBaseOffset + direction * attackHitboxDistance;
            attackHitbox.transform.localPosition = localPosition;
        }

        private void DrawAttackHitboxGizmo()
        {
            if (!TryResolveAttackHitboxPreview(out Vector2 center, out Vector2 size, out float angle))
            {
                return;
            }

            Gizmos.color = AttackHitboxGizmoColor;
            Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.Euler(0f, 0f, angle), Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, size.y, 0.01f));
        }

        private bool TryResolveAttackHitboxPreview(out Vector2 center, out Vector2 size, out float angle)
        {
            center = Vector2.zero;
            size = Vector2.zero;
            angle = 0f;
            if (attackHitbox == null)
            {
                return false;
            }

            Transform hitboxTransform = attackHitbox.transform;
            Transform parent = hitboxTransform.parent != null ? hitboxTransform.parent : transform;
            Vector2 direction = ResolveGizmoFacingDirection();
            Vector2 localPosition = attackHitboxBaseOffset + direction * attackHitboxDistance;
            Vector3 previewLocalPosition = new Vector3(localPosition.x, localPosition.y, hitboxTransform.localPosition.z);
            Matrix4x4 previewMatrix = parent.localToWorldMatrix
                                      * Matrix4x4.TRS(previewLocalPosition, hitboxTransform.localRotation, hitboxTransform.localScale);
            Vector2 scale = AbsScale(hitboxTransform.lossyScale);
            Quaternion rotation = parent.rotation * hitboxTransform.localRotation;

            center = previewMatrix.MultiplyPoint3x4(attackHitbox.offset);
            size = Vector2.Scale(attackHitbox.size, scale);
            angle = rotation.eulerAngles.z;
            return true;
        }

        private Vector2 ResolveGizmoFacingDirection()
        {
            if (Application.isPlaying && _lastFacingDirection.sqrMagnitude > 0.0001f)
            {
                return _lastFacingDirection.normalized;
            }

            return Vector2.right;
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
            if (_state == SkeletonActionState.Dead)
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
                case SkeletonActionState.WanderMove:
                case SkeletonActionState.Approach:
                    PlayAnimatorState(walkStateName);
                    break;
                case SkeletonActionState.Attack:
                    PlayAnimatorState(attackStateName);
                    break;
                case SkeletonActionState.Dead:
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
            if (_state == SkeletonActionState.Dead)
            {
                return;
            }

            if (_state == SkeletonActionState.Attack && suppressHurtAnimationDuringAttack)
            {
                return;
            }

            _hurtVisualUntil = Time.time + hurtVisualDuration;
            _hurtAnimationActive = true;
            PlayAnimatorState(hurtStateName);
        }

        private void HandleDeath()
        {
            if (_state == SkeletonActionState.Dead)
            {
                return;
            }

            StopBody();
            SetAttackHitboxActive(false);
            EnterState(SkeletonActionState.Dead);
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
