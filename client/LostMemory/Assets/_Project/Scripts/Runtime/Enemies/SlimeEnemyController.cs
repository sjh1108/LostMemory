using System.Collections.Generic;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Slime Enemy Controller")]
    public sealed class SlimeEnemyController : MonoBehaviour
    {
        private enum SlimeActionState
        {
            Idle,
            WanderMove,
            WanderPause,
            HopMove,
            Pause,
            PreAttack,
            Attack,
            Recover,
            Cooldown,
            Dead
        }

        private enum SlimeFacingDirection
        {
            Front,
            Side,
            Back
        }

        [Header("Bindings")]
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Collider2D bodyCollider;
        [SerializeField] private BoxCollider2D attackHitbox;
        [SerializeField] private Health health;

        [Header("Animation")]
        [SerializeField] private bool flipSideSpriteOnPositiveX = true;

        [Header("Stats")]
        [SerializeField, Min(1f)] private float maxHealth = 35f;
        [SerializeField, Min(0f)] private float moveSpeed = 2f;
        [SerializeField, Min(0f)] private float detectionRadius = 6f;
        [SerializeField, Min(0f)] private float attackRange = 1.05f;

        [Header("Wander")]
        [SerializeField, Min(0f)] private float wanderSpeedMultiplier = 0.55f;
        [SerializeField, Min(0.1f)] private float wanderMoveDuration = 1.2f;
        [SerializeField, Min(0f)] private float wanderPauseDuration = 0.6f;
        [SerializeField, Range(0f, 1f)] private float wanderIdleChance = 0.3f;

        [Header("Hop Pattern")]
        [SerializeField, Min(1)] private int hopsBeforePause = 2;
        [SerializeField, Min(0.05f)] private float hopDuration = 0.32f;
        [SerializeField, Min(0f)] private float pauseDuration = 0.45f;

        [Header("Attack")]
        [SerializeField, Min(0f)] private float preAttackPause = 0.45f;
        [SerializeField, Min(0f)] private float attackWindup = 0.1f;
        [SerializeField, Min(0.01f)] private float attackActiveDuration = 0.22f;
        [SerializeField, Min(0f)] private float attackRecoverDuration = 0.35f;
        [SerializeField, Min(0f)] private float attackCooldown = 3f;
        [SerializeField, Min(0f)] private float attackDamage = 8f;
        [SerializeField, Min(0f)] private float attackInvincibilityDuration = 0.5f;
        [SerializeField] private Vector2 attackHitboxBaseOffset = new Vector2(0f, 0.12f);
        [SerializeField, Min(0f)] private float attackHitboxDistance = 0.46f;
        [SerializeField] private bool applyAttackKnockback = true;
        [SerializeField, Min(0f)] private float attackKnockbackForce = 250f;
        [SerializeField] private LayerMask targetLayerMask = 1 << 10;
        [SerializeField, Min(1)] private int attackOverlapBufferSize = 8;

        [Header("Hit Reaction")]
        [SerializeField, Min(0f)] private float hurtVisualDuration = 0.18f;
        [SerializeField, Min(0f)] private float deathDestroyDelay = 0.65f;

        private static readonly int FacingDirection2DHash = Animator.StringToHash("FacingDirection2D");
        private static readonly int IdleStateHash = Animator.StringToHash("Base Layer.Slime_Idle");
        private static readonly int MoveStateHash = Animator.StringToHash("Base Layer.Slime_Move");
        private static readonly int LegacyReadyStateHash = Animator.StringToHash("Base Layer.Slime_Ready");
        private static readonly int JumpStateHash = Animator.StringToHash("Base Layer.Slime_Jump");
        private static readonly int AttackStateHash = Animator.StringToHash("Base Layer.Slime_Attack");
        private static readonly int HurtStateHash = Animator.StringToHash("Base Layer.Slime_Hurt");
        private static readonly int DeathStateHash = Animator.StringToHash("Base Layer.Slime_Death");

        private readonly HashSet<Health> _hitTargetsThisAttack = new HashSet<Health>();
        private Collider2D[] _attackOverlapBuffer;
        private Transform _target;
        private SlimeActionState _state = SlimeActionState.Idle;
        private float _stateStartedAt;
        private float _stateEndsAt;
        private float _nextTargetRefreshTime;
        private float _nextAttackAllowedAt;
        private float _hurtVisualUntil;
        private int _hopCount;
        private Vector2 _wanderDirection = Vector2.right;
        private Vector2 _hopDirection = Vector2.down;
        private Vector2 _lastFacingDirection = Vector2.down;
        private int _heldAnimatorStateHash;
        private float _heldAnimatorNormalizedTime;
        private bool _attackHitboxActive;
        private bool _hurtAnimationActive;

        private void Reset()
        {
            ResolveBindings();
        }

        private void Awake()
        {
            ResolveBindings();
            EnsureAttackOverlapBuffer();
            ConfigureHealth();
            SetAttackHitboxActive(false);
            EnterState(SlimeActionState.Idle);
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
            if (_state != SlimeActionState.Dead)
            {
                RefreshTargetIfNeeded();
                TickState();
            }

            UpdateAnimator();
        }

        private void FixedUpdate()
        {
            if (_state == SlimeActionState.HopMove)
            {
                MoveDuringHop();
            }
            else if (_state == SlimeActionState.WanderMove)
            {
                MoveDuringWander();
            }
            else
            {
                StopBody();
            }

            if (_state == SlimeActionState.Attack)
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
            wanderSpeedMultiplier = Mathf.Max(0f, wanderSpeedMultiplier);
            wanderMoveDuration = Mathf.Max(0.1f, wanderMoveDuration);
            wanderPauseDuration = Mathf.Max(0f, wanderPauseDuration);
            wanderIdleChance = Mathf.Clamp01(wanderIdleChance);
            hopsBeforePause = Mathf.Max(1, hopsBeforePause);
            hopDuration = Mathf.Max(0.05f, hopDuration);
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
                case SlimeActionState.Idle:
                    if (HasValidTargetInDetectionRange())
                    {
                        _hopCount = 0;
                        EnterHop();
                    }
                    else
                    {
                        EnterWanderMove();
                    }
                    break;
                case SlimeActionState.WanderMove:
                    if (HasValidTargetInDetectionRange())
                    {
                        _hopCount = 0;
                        EnterHop();
                    }
                    else if (Time.time >= _stateEndsAt)
                    {
                        if (ShouldEnterWanderIdle())
                        {
                            EnterState(SlimeActionState.WanderPause, wanderPauseDuration);
                        }
                        else
                        {
                            EnterWanderMove();
                        }
                    }
                    break;
                case SlimeActionState.WanderPause:
                    if (HasValidTargetInDetectionRange())
                    {
                        _hopCount = 0;
                        EnterHop();
                    }
                    else if (Time.time >= _stateEndsAt)
                    {
                        EnterWanderMove();
                    }
                    break;
                case SlimeActionState.HopMove:
                    if (Time.time < _stateEndsAt)
                    {
                        break;
                    }

                    if (CanStartAttack())
                    {
                        EnterState(SlimeActionState.PreAttack, preAttackPause);
                    }
                    else if (HasValidTargetInDetectionRange() && _hopCount < hopsBeforePause)
                    {
                        EnterHop();
                    }
                    else
                    {
                        EnterState(SlimeActionState.Pause, pauseDuration);
                    }
                    break;
                case SlimeActionState.Pause:
                    if (Time.time < _stateEndsAt)
                    {
                        break;
                    }

                    if (CanStartAttack())
                    {
                        EnterState(SlimeActionState.PreAttack, preAttackPause);
                    }
                    else if (HasValidTargetInDetectionRange())
                    {
                        _hopCount = 0;
                        EnterHop();
                    }
                    else
                    {
                        EnterState(SlimeActionState.Idle);
                    }
                    break;
                case SlimeActionState.PreAttack:
                    FaceTarget();
                    if (Time.time >= _stateEndsAt)
                    {
                        EnterAttack();
                    }
                    break;
                case SlimeActionState.Attack:
                    UpdateAttackHitboxState();
                    if (Time.time >= _stateEndsAt)
                    {
                        SetAttackHitboxActive(false);
                        EnterState(SlimeActionState.Recover, attackRecoverDuration);
                    }
                    break;
                case SlimeActionState.Recover:
                    if (Time.time >= _stateEndsAt)
                    {
                        EnterState(SlimeActionState.Cooldown, attackCooldown);
                    }
                    break;
                case SlimeActionState.Cooldown:
                    if (Time.time >= _stateEndsAt)
                    {
                        if (HasValidTargetInDetectionRange())
                        {
                            _hopCount = 0;
                            EnterHop();
                        }
                        else
                        {
                            EnterState(SlimeActionState.Idle);
                        }
                    }
                    break;
            }
        }

        private void EnterHop()
        {
            FaceTarget();
            _hopDirection = ResolveDirectionToTarget();
            if (_hopDirection.sqrMagnitude <= 0.0001f)
            {
                _hopDirection = _lastFacingDirection;
            }

            _hopCount++;
            EnterState(SlimeActionState.HopMove, hopDuration);
        }

        private void EnterAttack()
        {
            FaceTarget();
            PositionAttackHitbox();
            _hitTargetsThisAttack.Clear();
            _nextAttackAllowedAt = Time.time + attackCooldown;
            EnterState(SlimeActionState.Attack, attackWindup + attackActiveDuration);
            UpdateAttackHitboxState();
        }

        private void EnterState(SlimeActionState state, float duration = 0f)
        {
            if (ShouldHoldCurrentAnimatorState(state) && !_hurtAnimationActive)
            {
                CaptureHeldAnimatorState();
            }

            _state = state;
            _stateStartedAt = Time.time;
            _stateEndsAt = duration > 0f ? Time.time + duration : 0f;

            if (state != SlimeActionState.Attack)
            {
                SetAttackHitboxActive(false);
            }

            if (state == SlimeActionState.Dead)
            {
                _hurtAnimationActive = false;
                PlayAnimatorState(DeathStateHash);
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
            EnterState(SlimeActionState.WanderMove, wanderMoveDuration);
        }

        private Vector2 ResolveNewWanderDirection()
        {
            Vector2 direction = Random.insideUnitCircle;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = _lastFacingDirection.sqrMagnitude > 0.0001f ? _lastFacingDirection : Vector2.down;
            }

            return direction.normalized;
        }

        private bool ShouldEnterWanderIdle()
        {
            return wanderPauseDuration > 0f
                   && wanderIdleChance > 0f
                   && Random.value <= wanderIdleChance;
        }

        private void MoveDuringHop()
        {
            if (body == null || _hopDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector2 nextPosition = body.position + (_hopDirection.normalized * moveSpeed * Time.fixedDeltaTime);
            body.MovePosition(nextPosition);
        }

        private void MoveDuringWander()
        {
            if (body == null || _wanderDirection.sqrMagnitude <= 0.0001f || moveSpeed <= 0f)
            {
                return;
            }

            Vector2 nextPosition = body.position + (_wanderDirection.normalized * moveSpeed * wanderSpeedMultiplier * Time.fixedDeltaTime);
            body.MovePosition(nextPosition);
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

        private Vector2 ResolveDirectionToTarget()
        {
            if (!IsTargetUsable())
            {
                return _lastFacingDirection;
            }

            Vector2 direction = _target.position - transform.position;
            if (direction.sqrMagnitude > 0.0001f)
            {
                _lastFacingDirection = direction.normalized;
            }

            return _lastFacingDirection;
        }

        private void FaceTarget()
        {
            Vector2 direction = ResolveDirectionToTarget();
            ApplySpriteFlip(direction);
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
            Vector2 localPosition = attackHitboxBaseOffset + (direction * attackHitboxDistance);
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
            UpdateAnimatorDirection();

            if (_state == SlimeActionState.Dead)
            {
                return;
            }

            if (_hurtAnimationActive && Time.time >= _hurtVisualUntil)
            {
                _hurtAnimationActive = false;
                PlayAnimatorStateForCurrentState();
            }
        }

        private void UpdateAnimatorDirection()
        {
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetFloat(FacingDirection2DHash, ResolveFacingDirectionParameter(_lastFacingDirection));
            }

            ApplySpriteFlip(_lastFacingDirection);
        }

        private void PlayAnimatorStateForCurrentState()
        {
            switch (_state)
            {
                case SlimeActionState.Idle:
                case SlimeActionState.WanderPause:
                    PlayAnimatorState(IdleStateHash);
                    break;
                case SlimeActionState.WanderMove:
                    PlayAnimatorState(MoveStateHash, LegacyReadyStateHash);
                    break;
                case SlimeActionState.HopMove:
                    PlayAnimatorState(JumpStateHash);
                    break;
                case SlimeActionState.Pause:
                case SlimeActionState.PreAttack:
                    PlayHeldAnimatorState();
                    break;
                case SlimeActionState.Attack:
                    PlayAnimatorState(AttackStateHash);
                    break;
                case SlimeActionState.Dead:
                    PlayAnimatorState(DeathStateHash);
                    break;
                default:
                    PlayAnimatorState(IdleStateHash);
                    break;
            }
        }

        private static bool ShouldHoldCurrentAnimatorState(SlimeActionState state)
        {
            return state == SlimeActionState.Pause || state == SlimeActionState.PreAttack;
        }

        private void CaptureHeldAnimatorState()
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            _heldAnimatorStateHash = stateInfo.fullPathHash;
            _heldAnimatorNormalizedTime = Mathf.Clamp01(stateInfo.normalizedTime);
        }

        private void PlayHeldAnimatorState()
        {
            if (animator == null || animator.runtimeAnimatorController == null || _heldAnimatorStateHash == 0)
            {
                return;
            }

            UpdateAnimatorDirection();
            animator.Play(_heldAnimatorStateHash, 0, _heldAnimatorNormalizedTime);
            animator.Update(0f);
        }

        private void PlayAnimatorState(int stateHash)
        {
            PlayAnimatorState(stateHash, 0);
        }

        private void PlayAnimatorState(int stateHash, int fallbackStateHash)
        {
            if (animator == null || animator.runtimeAnimatorController == null || stateHash == 0)
            {
                return;
            }

            if (!animator.HasState(0, stateHash))
            {
                if (fallbackStateHash == 0 || !animator.HasState(0, fallbackStateHash))
                {
                    return;
                }

                stateHash = fallbackStateHash;
            }

            UpdateAnimatorDirection();
            animator.Play(stateHash, 0, 0f);
            animator.Update(0f);
        }

        private SlimeFacingDirection ResolveFacingDirection(Vector2 direction)
        {
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            {
                return SlimeFacingDirection.Side;
            }

            return direction.y > 0f ? SlimeFacingDirection.Back : SlimeFacingDirection.Front;
        }

        private float ResolveFacingDirectionParameter(Vector2 direction)
        {
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            {
                return direction.x < 0f ? 0f : 2f;
            }

            return direction.y > 0f ? 1f : 3f;
        }

        private void ApplySpriteFlip(Vector2 direction)
        {
            if (spriteRenderer == null)
            {
                return;
            }

            spriteRenderer.flipX = ResolveFacingDirection(direction) == SlimeFacingDirection.Side
                                   && ((flipSideSpriteOnPositiveX && direction.x > 0f)
                                       || (!flipSideSpriteOnPositiveX && direction.x < 0f));
        }

        private void HandleHit()
        {
            if (_state == SlimeActionState.Dead)
            {
                return;
            }

            _hurtVisualUntil = Time.time + hurtVisualDuration;
            _hurtAnimationActive = true;
            PlayAnimatorState(HurtStateHash);
        }

        private void HandleDeath()
        {
            if (_state == SlimeActionState.Dead)
            {
                return;
            }

            StopBody();
            SetAttackHitboxActive(false);
            EnterState(SlimeActionState.Dead);
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
