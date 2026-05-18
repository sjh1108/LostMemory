using System.Collections.Generic;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Assassin Enemy Controller")]
    public sealed class AssassinEnemyController : MonoBehaviour
    {
        private enum AssassinActionState
        {
            Idle,
            WanderRun,
            Approach,
            NormalAttack,
            TeleportTelegraph,
            TeleportReady,
            TeleportAttack,
            TeleportRetreat,
            Recover,
            Cooldown,
            Dead
        }

        private enum AssassinAttackKind
        {
            Normal,
            Teleport
        }

        [Header("Bindings")]
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Collider2D bodyCollider;
        [SerializeField] private BoxCollider2D attackHitbox;
        [SerializeField] private Health health;

        [Header("Animation States")]
        [SerializeField] private string idleStateName = "Assassin_idle";
        [SerializeField] private string idle2StateName = "Assassin_idle2";
        [SerializeField] private string runStateName = "Assassin_run";
        [SerializeField] private string normalAttackStateName = "Assassin_atk1";
        [SerializeField] private string teleportAttackStateName = "Assassin_atk2";
        [SerializeField] private string hurtStateName = "Assassin_hurt";
        [SerializeField] private string deathStateName = "Assassin_death";
        [SerializeField] private bool flipSpriteOnPositiveX;

        [Header("Stats")]
        [SerializeField, Min(1f)] private float maxHealth = 32f;
        [SerializeField, Min(0f)] private float moveSpeed = 3.4f;
        [SerializeField, Min(0f)] private float detectionRadius = 7f;
        [SerializeField, Min(0f)] private float normalAttackRange = 1f;
        [SerializeField, Min(0f)] private float desiredAttackDistance = 0.75f;
        [SerializeField] private LayerMask wallBlockMask = (1 << 8) | (1 << 11) | (1 << 24);
        [SerializeField] private LayerMask targetLayerMask = 1 << 10;

        [Header("Wander")]
        [SerializeField, Min(0f)] private float wanderSpeedMultiplier = 0.85f;
        [SerializeField, Min(0.1f)] private float wanderRunDuration = 1.1f;
        [SerializeField, Min(1)] private int wanderTurnAttempts = 8;
        [SerializeField, Min(0f)] private float wanderWallProbeDistance = 0.2f;

        [Header("Normal Attack")]
        [SerializeField, Min(0.01f)] private float normalAttackDuration = 0.48f;
        [SerializeField, Min(0f)] private float normalAttackWindup = 0.14f;
        [SerializeField, Min(0.01f)] private float normalAttackActiveDuration = 0.18f;
        [SerializeField, Min(0f)] private float normalAttackRecoverDuration = 0.25f;
        [SerializeField, Min(0f)] private float normalAttackCooldown = 1.1f;
        [SerializeField, Min(0f)] private float normalAttackDamage = 8f;

        [Header("Teleport Attack")]
        [SerializeField, Min(0f)] private float teleportAttackRange = 5.5f;
        [SerializeField, Min(0f)] private float teleportAttackMinRange = 1.4f;
        [SerializeField, Min(0f)] private float teleportInitialEngageDelay = 1.35f;
        [SerializeField, Min(0f)] private float teleportTelegraphDuration = 0.45f;
        [SerializeField, Min(0f)] private float teleportPostDelay = 0.2f;
        [SerializeField, Min(0.01f)] private float teleportAttackDuration = 0.58f;
        [SerializeField, Min(0f)] private float teleportAttackWindup = 0.08f;
        [SerializeField, Min(0.01f)] private float teleportAttackActiveDuration = 0.24f;
        [SerializeField, Min(0f)] private float teleportAttackRecoverDuration = 0.35f;
        [SerializeField, Min(0f)] private float teleportAttackCooldown = 3.6f;
        [SerializeField, Min(0f)] private float teleportAttackDamage = 12f;
        [SerializeField, Min(0f)] private float teleportBehindDistance = 0.85f;
        [SerializeField, Min(0f)] private float teleportPositionProbeRadius = 0.35f;
        [SerializeField, Min(0f)] private float teleportRetreatDuration = 0.75f;
        [SerializeField, Min(0f)] private float teleportRetreatSpeedMultiplier = 1.25f;
        [SerializeField, Min(0f)] private float teleportRetreatWallProbeDistance = 0.25f;

        [Header("Teleport Visual")]
        [SerializeField, Min(0f)] private float teleportVanishDuration = 0.16f;
        [SerializeField, Range(0f, 1f)] private float teleportVanishMinAlpha = 0f;
        [SerializeField, Min(0f)] private float teleportAppearDuration = 0.12f;

        [Header("Attack Hitbox")]
        [SerializeField, Min(0f)] private float attackInvincibilityDuration = 0.45f;
        [SerializeField] private Vector2 attackHitboxBaseOffset = new Vector2(0f, 0.05f);
        [SerializeField, Min(0f)] private float attackHitboxDistance = 0.55f;
        [SerializeField] private bool applyAttackKnockback = true;
        [SerializeField, Min(0f)] private float normalAttackKnockbackForce = 180f;
        [SerializeField, Min(0f)] private float teleportAttackKnockbackForce = 260f;
        [SerializeField, Min(1)] private int attackOverlapBufferSize = 8;

        [Header("Hit Reaction")]
        [SerializeField, Min(0f)] private float hurtVisualDuration = 0.16f;
        [SerializeField, Min(0f)] private float deathDestroyDelay = 0.65f;

        private readonly HashSet<Health> _hitTargetsThisAttack = new HashSet<Health>();
        private Collider2D[] _attackOverlapBuffer;
        private Transform _target;
        private AssassinActionState _state = AssassinActionState.Idle;
        private AssassinAttackKind _attackKind = AssassinAttackKind.Normal;
        private float _stateStartedAt;
        private float _stateEndsAt;
        private float _nextTargetRefreshTime;
        private float _nextNormalAttackAllowedAt;
        private float _nextTeleportAttackAllowedAt;
        private float _hurtVisualUntil;
        private Vector2 _wanderDirection = Vector2.right;
        private Vector2 _retreatDirection = Vector2.right;
        private Vector2 _lastFacingDirection = Vector2.right;
        private Transform _visualRoot;
        private Vector3 _visualBaseScale;
        private Color _spriteBaseColor = Color.white;
        private bool _hasVisualBaseScale;
        private bool _hasSpriteBaseColor;
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
            CaptureSpriteBaseColor();
            EnsureAttackOverlapBuffer();
            ConfigureHealth();
            SetAttackHitboxActive(false);
            EnterState(AssassinActionState.Idle);
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
            if (_state != AssassinActionState.Dead)
            {
                RefreshTargetIfNeeded();
                TickState();
            }

            UpdateAnimator();
        }

        private void LateUpdate()
        {
            if (_state != AssassinActionState.Dead)
            {
                ApplySpriteFlip(_lastFacingDirection);
                UpdateTeleportVanishVisual();
            }
        }

        private void FixedUpdate()
        {
            if (_state == AssassinActionState.WanderRun)
            {
                MoveDuringWander();
            }
            else if (_state == AssassinActionState.TeleportRetreat)
            {
                MoveDuringTeleportRetreat();
            }
            else if (_state == AssassinActionState.Approach)
            {
                MoveTowardTarget();
            }
            else
            {
                StopBody();
            }

            if (_state == AssassinActionState.NormalAttack || _state == AssassinActionState.TeleportAttack)
            {
                ScanAttackTargets();
            }
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            moveSpeed = Mathf.Max(0f, moveSpeed);
            detectionRadius = Mathf.Max(0f, detectionRadius);
            normalAttackRange = Mathf.Max(0f, normalAttackRange);
            desiredAttackDistance = Mathf.Max(0f, desiredAttackDistance);
            wanderSpeedMultiplier = Mathf.Max(0f, wanderSpeedMultiplier);
            wanderRunDuration = Mathf.Max(0.1f, wanderRunDuration);
            wanderTurnAttempts = Mathf.Max(1, wanderTurnAttempts);
            wanderWallProbeDistance = Mathf.Max(0f, wanderWallProbeDistance);
            normalAttackDuration = Mathf.Max(0.01f, normalAttackDuration);
            normalAttackActiveDuration = Mathf.Max(0.01f, normalAttackActiveDuration);
            teleportAttackRange = Mathf.Max(0f, teleportAttackRange);
            teleportAttackMinRange = Mathf.Max(0f, teleportAttackMinRange);
            teleportInitialEngageDelay = Mathf.Max(0f, teleportInitialEngageDelay);
            teleportPostDelay = Mathf.Max(0f, teleportPostDelay);
            teleportAttackDuration = Mathf.Max(0.01f, teleportAttackDuration);
            teleportAttackActiveDuration = Mathf.Max(0.01f, teleportAttackActiveDuration);
            teleportBehindDistance = Mathf.Max(0f, teleportBehindDistance);
            teleportPositionProbeRadius = Mathf.Max(0f, teleportPositionProbeRadius);
            teleportRetreatDuration = Mathf.Max(0f, teleportRetreatDuration);
            teleportRetreatSpeedMultiplier = Mathf.Max(0f, teleportRetreatSpeedMultiplier);
            teleportRetreatWallProbeDistance = Mathf.Max(0f, teleportRetreatWallProbeDistance);
            teleportVanishDuration = Mathf.Max(0f, teleportVanishDuration);
            teleportAppearDuration = Mathf.Max(0f, teleportAppearDuration);
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
                normalAttackDamage = Mathf.Max(0f, meleeDamage);
            }

            if (data.TryGetAttackDamage(EnemyAttackType.Charge, out float chargeDamage))
            {
                teleportAttackDamage = Mathf.Max(0f, chargeDamage);
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

        private void CaptureSpriteBaseColor()
        {
            if (spriteRenderer == null || _hasSpriteBaseColor)
            {
                return;
            }

            _spriteBaseColor = spriteRenderer.color;
            _hasSpriteBaseColor = true;
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
                case AssassinActionState.Idle:
                    if (HasValidTargetInDetectionRange())
                    {
                        EnterEngagedApproach();
                    }
                    else
                    {
                        EnterWanderRun();
                    }
                    break;
                case AssassinActionState.WanderRun:
                    if (HasValidTargetInDetectionRange())
                    {
                        EnterEngagedApproach();
                    }
                    else if (Time.time >= _stateEndsAt)
                    {
                        EnterWanderRun();
                    }
                    break;
                case AssassinActionState.Approach:
                    FaceTarget();
                    if (CanStartTeleportAttack())
                    {
                        _attackKind = AssassinAttackKind.Teleport;
                        EnterState(AssassinActionState.TeleportTelegraph, teleportTelegraphDuration);
                    }
                    else if (CanStartNormalAttack())
                    {
                        EnterAttack(AssassinAttackKind.Normal);
                    }
                    else if (!HasValidTargetInDetectionRange())
                    {
                        EnterWanderRun();
                    }
                    break;
                case AssassinActionState.TeleportTelegraph:
                    FaceTarget();
                    if (Time.time >= _stateEndsAt)
                    {
                        TeleportBehindTarget();
                        EnterState(AssassinActionState.TeleportReady, teleportPostDelay);
                    }
                    break;
                case AssassinActionState.TeleportReady:
                    FaceTarget();
                    if (Time.time >= _stateEndsAt)
                    {
                        EnterAttack(AssassinAttackKind.Teleport);
                    }
                    break;
                case AssassinActionState.NormalAttack:
                case AssassinActionState.TeleportAttack:
                    UpdateAttackHitboxState();
                    if (Time.time >= _stateEndsAt)
                    {
                        SetAttackHitboxActive(false);
                        if (_state == AssassinActionState.TeleportAttack)
                        {
                            EnterTeleportRetreat();
                        }
                        else
                        {
                            EnterState(AssassinActionState.Recover, ResolveRecoverDuration());
                        }
                    }
                    break;
                case AssassinActionState.TeleportRetreat:
                    if (Time.time >= _stateEndsAt)
                    {
                        EnterState(AssassinActionState.Cooldown, ResolveRemainingTeleportCooldown());
                    }
                    break;
                case AssassinActionState.Recover:
                    if (Time.time >= _stateEndsAt)
                    {
                        EnterState(AssassinActionState.Cooldown, ResolveCooldownDuration());
                    }
                    break;
                case AssassinActionState.Cooldown:
                    if (Time.time >= _stateEndsAt)
                    {
                        if (HasValidTargetInDetectionRange())
                        {
                            EnterState(AssassinActionState.Approach);
                        }
                        else
                        {
                            EnterWanderRun();
                        }
                    }
                    break;
            }
        }

        private void EnterAttack(AssassinAttackKind attackKind)
        {
            _attackKind = attackKind;
            FaceTarget();
            PositionAttackHitbox();
            _hitTargetsThisAttack.Clear();

            if (attackKind == AssassinAttackKind.Teleport)
            {
                _nextTeleportAttackAllowedAt = Time.time + teleportAttackCooldown;
                EnterState(AssassinActionState.TeleportAttack, teleportAttackDuration);
            }
            else
            {
                _nextNormalAttackAllowedAt = Time.time + normalAttackCooldown;
                EnterState(AssassinActionState.NormalAttack, normalAttackDuration);
            }

            UpdateAttackHitboxState();
        }

        private void EnterEngagedApproach()
        {
            DelayTeleportAttack(teleportInitialEngageDelay);
            EnterState(AssassinActionState.Approach);
        }

        private void DelayTeleportAttack(float delay)
        {
            if (delay <= 0f)
            {
                return;
            }

            _nextTeleportAttackAllowedAt = Mathf.Max(_nextTeleportAttackAllowedAt, Time.time + delay);
        }

        private void EnterState(AssassinActionState state, float duration = 0f)
        {
            _state = state;
            _stateStartedAt = Time.time;
            _stateEndsAt = duration > 0f ? Time.time + duration : 0f;

            if (state != AssassinActionState.NormalAttack && state != AssassinActionState.TeleportAttack)
            {
                SetAttackHitboxActive(false);
            }

            if (state == AssassinActionState.Recover || state == AssassinActionState.Cooldown)
            {
                RestoreSpriteAlpha();
            }

            if (state == AssassinActionState.Dead)
            {
                _hurtAnimationActive = false;
                RestoreSpriteAlpha();
                PlayAnimatorState(deathStateName);
                return;
            }

            if (!_hurtAnimationActive)
            {
                PlayAnimatorStateForCurrentState();
            }
        }

        private void EnterWanderRun()
        {
            _wanderDirection = ResolveNewWanderDirection();
            _lastFacingDirection = _wanderDirection;
            ApplySpriteFlip(_lastFacingDirection);
            EnterState(AssassinActionState.WanderRun, wanderRunDuration);
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
                EnterWanderRun();
                return;
            }

            _lastFacingDirection = direction;
            ApplySpriteFlip(_lastFacingDirection);
            body.MovePosition(body.position + direction * distance);
        }

        private void EnterTeleportRetreat()
        {
            _retreatDirection = ResolveRetreatDirection();
            _lastFacingDirection = _retreatDirection;
            ApplySpriteFlip(_lastFacingDirection);
            EnterState(AssassinActionState.TeleportRetreat, teleportRetreatDuration);
        }

        private Vector2 ResolveRetreatDirection()
        {
            Vector2 awayFromTarget = Vector2.zero;
            if (IsTargetUsable())
            {
                awayFromTarget = transform.position - _target.position;
            }

            if (awayFromTarget.sqrMagnitude <= 0.0001f)
            {
                awayFromTarget = _lastFacingDirection.sqrMagnitude > 0.0001f ? -_lastFacingDirection : Vector2.right;
            }

            awayFromTarget.Normalize();
            Vector2 sideDirection = Rotate90(awayFromTarget);
            Vector2[] candidates =
            {
                awayFromTarget,
                (awayFromTarget + sideDirection * 0.75f).normalized,
                (awayFromTarget - sideDirection * 0.75f).normalized,
                sideDirection,
                -sideDirection
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                Vector2 candidate = candidates[i];
                if (candidate.sqrMagnitude > 0.0001f && !WouldHitWall(candidate, teleportRetreatWallProbeDistance))
                {
                    return candidate.normalized;
                }
            }

            return awayFromTarget;
        }

        private void MoveDuringTeleportRetreat()
        {
            if (body == null || _retreatDirection.sqrMagnitude <= 0.0001f || moveSpeed <= 0f)
            {
                return;
            }

            Vector2 direction = _retreatDirection.normalized;
            float distance = moveSpeed * teleportRetreatSpeedMultiplier * Time.fixedDeltaTime;
            if (WouldHitWall(direction, distance + teleportRetreatWallProbeDistance))
            {
                _retreatDirection = ResolveRetreatDirection();
                direction = _retreatDirection.normalized;
                if (direction.sqrMagnitude <= 0.0001f || WouldHitWall(direction, distance + teleportRetreatWallProbeDistance))
                {
                    StopBody();
                    return;
                }
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
            if (toTarget.sqrMagnitude <= desiredAttackDistance * desiredAttackDistance)
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

        private void TeleportBehindTarget()
        {
            if (body == null || !IsTargetUsable())
            {
                return;
            }

            SetSpriteAlpha(teleportVanishMinAlpha);
            Vector2 toTarget = _target.position - transform.position;
            Vector2 behindDirection = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : _lastFacingDirection.normalized;
            Vector2 targetPosition = _target.position;
            Vector2 teleportPosition = ResolveTeleportPosition(targetPosition, behindDirection, body.position);
            body.position = teleportPosition;
            transform.position = new Vector3(teleportPosition.x, teleportPosition.y, transform.position.z);

            Vector2 faceDirection = targetPosition - teleportPosition;
            if (faceDirection.sqrMagnitude > 0.0001f)
            {
                _lastFacingDirection = faceDirection.normalized;
                ApplySpriteFlip(_lastFacingDirection);
            }
        }

        private Vector2 ResolveTeleportPosition(Vector2 targetPosition, Vector2 behindDirection, Vector2 fallbackPosition)
        {
            Vector2[] candidates =
            {
                targetPosition + behindDirection * teleportBehindDistance,
                targetPosition + Rotate90(behindDirection) * teleportBehindDistance,
                targetPosition - Rotate90(behindDirection) * teleportBehindDistance,
                targetPosition - behindDirection * teleportBehindDistance
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                if (!WouldOverlapWall(candidates[i]))
                {
                    return candidates[i];
                }
            }

            return fallbackPosition;
        }

        private bool WouldOverlapWall(Vector2 position)
        {
            if (bodyCollider == null || wallBlockMask.value == 0)
            {
                return false;
            }

            if (bodyCollider is BoxCollider2D boxCollider)
            {
                Vector2 center = position + (Vector2)boxCollider.transform.TransformVector(boxCollider.offset);
                Vector2 size = Vector2.Scale(boxCollider.size, AbsScale(boxCollider.transform.lossyScale));
                return Physics2D.OverlapBox(center, size, boxCollider.transform.eulerAngles.z, wallBlockMask) != null;
            }

            if (bodyCollider is CircleCollider2D circleCollider)
            {
                Vector2 center = position + (Vector2)circleCollider.transform.TransformVector(circleCollider.offset);
                Vector2 scale = AbsScale(circleCollider.transform.lossyScale);
                float radius = Mathf.Max(teleportPositionProbeRadius, circleCollider.radius * Mathf.Max(scale.x, scale.y));
                return Physics2D.OverlapCircle(center, radius, wallBlockMask) != null;
            }

            Bounds bounds = bodyCollider.bounds;
            Vector2 boundsOffset = (Vector2)bounds.center - (Vector2)transform.position;
            return Physics2D.OverlapBox(position + boundsOffset, bounds.size, 0f, wallBlockMask) != null;
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

        private bool CanStartNormalAttack()
        {
            return Time.time >= _nextNormalAttackAllowedAt
                   && IsTargetUsable()
                   && (_target.position - transform.position).sqrMagnitude <= normalAttackRange * normalAttackRange;
        }

        private bool CanStartTeleportAttack()
        {
            if (Time.time < _nextTeleportAttackAllowedAt || !IsTargetUsable())
            {
                return false;
            }

            float sqrDistance = (_target.position - transform.position).sqrMagnitude;
            return sqrDistance <= teleportAttackRange * teleportAttackRange
                   && sqrDistance >= teleportAttackMinRange * teleportAttackMinRange;
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
            bool shouldBeActive = elapsed >= ResolveAttackWindup()
                                  && elapsed <= ResolveAttackWindup() + ResolveAttackActiveDuration();
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
            if (!_attackHitboxActive || attackHitbox == null)
            {
                return;
            }

            float damage = ResolveAttackDamage();
            if (damage <= 0f)
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

                TryDamageTarget(targetCollider, damage);
            }
        }

        private void TryDamageTarget(Collider2D targetCollider, float damage)
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
            targetHealth.Damage(damage, gameObject, 0f, attackInvincibilityDuration, direction);
            ApplyAttackKnockback(targetHealth, direction);
        }

        private static Health ResolveHealth(Component target)
        {
            Health targetHealth = target.GetComponent<Health>();
            return targetHealth != null ? targetHealth : target.GetComponentInParent<Health>();
        }

        private void ApplyAttackKnockback(Health targetHealth, Vector3 direction)
        {
            float force = ResolveAttackKnockbackForce();
            if (!applyAttackKnockback
                || force <= 0f
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

            Vector3 knockback = direction.normalized * force;
            knockback *= targetHealth.KnockbackForceMultiplier;
            knockback = targetHealth.ComputeKnockbackForce(knockback);
            if (knockback.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            controller.Impact(knockback.normalized, knockback.magnitude);
        }

        private float ResolveAttackDamage()
        {
            return _attackKind == AssassinAttackKind.Teleport ? teleportAttackDamage : normalAttackDamage;
        }

        private float ResolveAttackWindup()
        {
            return _attackKind == AssassinAttackKind.Teleport ? teleportAttackWindup : normalAttackWindup;
        }

        private float ResolveAttackActiveDuration()
        {
            return _attackKind == AssassinAttackKind.Teleport ? teleportAttackActiveDuration : normalAttackActiveDuration;
        }

        private float ResolveRecoverDuration()
        {
            return _attackKind == AssassinAttackKind.Teleport ? teleportAttackRecoverDuration : normalAttackRecoverDuration;
        }

        private float ResolveCooldownDuration()
        {
            return _attackKind == AssassinAttackKind.Teleport ? teleportAttackCooldown : normalAttackCooldown;
        }

        private float ResolveRemainingTeleportCooldown()
        {
            return Mathf.Max(0f, _nextTeleportAttackAllowedAt - Time.time);
        }

        private float ResolveAttackKnockbackForce()
        {
            return _attackKind == AssassinAttackKind.Teleport ? teleportAttackKnockbackForce : normalAttackKnockbackForce;
        }

        private void UpdateAnimator()
        {
            if (_state == AssassinActionState.Dead)
            {
                return;
            }

            if (_hurtAnimationActive && Time.time >= _hurtVisualUntil)
            {
                _hurtAnimationActive = false;
                PlayAnimatorStateForCurrentState();
            }

            SuppressUnexpectedTelegraphAnimation();
        }

        private void SuppressUnexpectedTelegraphAnimation()
        {
            if (_state == AssassinActionState.TeleportTelegraph
                || animator == null
                || animator.runtimeAnimatorController == null
                || string.IsNullOrEmpty(idle2StateName))
            {
                return;
            }

            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
            if (currentState.IsName(idle2StateName) || currentState.IsName("Base Layer." + idle2StateName))
            {
                PlayAnimatorStateForCurrentState();
            }
        }

        private void PlayAnimatorStateForCurrentState()
        {
            switch (_state)
            {
                case AssassinActionState.WanderRun:
                case AssassinActionState.TeleportRetreat:
                case AssassinActionState.Approach:
                    PlayAnimatorState(runStateName);
                    break;
                case AssassinActionState.TeleportTelegraph:
                    PlayAnimatorState(idle2StateName);
                    break;
                case AssassinActionState.TeleportReady:
                    PlayAnimatorState(idleStateName);
                    break;
                case AssassinActionState.NormalAttack:
                    PlayAnimatorState(normalAttackStateName);
                    break;
                case AssassinActionState.TeleportAttack:
                    PlayAnimatorState(teleportAttackStateName);
                    break;
                case AssassinActionState.Dead:
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

        private void UpdateTeleportVanishVisual()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            CaptureSpriteBaseColor();

            if (_state == AssassinActionState.TeleportTelegraph)
            {
                float remaining = Mathf.Max(0f, _stateEndsAt - Time.time);
                if (teleportVanishDuration <= 0f || remaining > teleportVanishDuration)
                {
                    RestoreSpriteAlpha();
                    return;
                }

                float progress = 1f - remaining / teleportVanishDuration;
                SetSpriteAlpha(Mathf.Lerp(_spriteBaseColor.a, _spriteBaseColor.a * teleportVanishMinAlpha, progress));
                return;
            }

            if (_state == AssassinActionState.TeleportAttack)
            {
                float elapsed = Time.time - _stateStartedAt;
                if (teleportAppearDuration > 0f && elapsed <= teleportAppearDuration)
                {
                    float progress = elapsed / teleportAppearDuration;
                    SetSpriteAlpha(Mathf.Lerp(_spriteBaseColor.a * teleportVanishMinAlpha, _spriteBaseColor.a, progress));
                    return;
                }
            }

            if (_state == AssassinActionState.TeleportReady)
            {
                RestoreSpriteAlpha();
                return;
            }

            RestoreSpriteAlpha();
        }

        private void RestoreSpriteAlpha()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            CaptureSpriteBaseColor();
            SetSpriteAlpha(_spriteBaseColor.a);
        }

        private void SetSpriteAlpha(float alpha)
        {
            if (spriteRenderer == null)
            {
                return;
            }

            Color color = spriteRenderer.color;
            color.a = Mathf.Clamp01(alpha);
            spriteRenderer.color = color;
        }

        private void HandleHit()
        {
            if (_state == AssassinActionState.Dead)
            {
                return;
            }

            _hurtVisualUntil = Time.time + hurtVisualDuration;
            _hurtAnimationActive = true;
            PlayAnimatorState(hurtStateName);
        }

        private void HandleDeath()
        {
            if (_state == AssassinActionState.Dead)
            {
                return;
            }

            StopBody();
            SetAttackHitboxActive(false);
            EnterState(AssassinActionState.Dead);
        }

        private void EnsureAttackOverlapBuffer()
        {
            int size = Mathf.Max(1, attackOverlapBufferSize);
            if (_attackOverlapBuffer == null || _attackOverlapBuffer.Length != size)
            {
                _attackOverlapBuffer = new Collider2D[size];
            }
        }

        private static Vector2 Rotate90(Vector2 direction)
        {
            return new Vector2(-direction.y, direction.x);
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
