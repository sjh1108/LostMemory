using System.Collections.Generic;
using LostMemory.Combat.Telegraph;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;
using UnityEngine.Serialization;

namespace LostMemory.Enemies
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Skeleton Elite Enemy Controller")]
    public sealed class SkeletonEliteEnemyController : MonoBehaviour
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

        private enum EliteAttackType
        {
            Light,
            Heavy
        }

        private static readonly Color LightAttackGizmoColor = new Color(1f, 0.15f, 0.1f, 0.9f);
        private static readonly Color HeavyAttackGizmoColor = new Color(1f, 0.55f, 0.05f, 0.9f);

        [Header("Bindings")]
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Collider2D bodyCollider;
        [SerializeField] private BoxCollider2D attackHitbox;
        [SerializeField] private AttackTelegraph2DView heavyAttackTelegraphView;
        [SerializeField] private Health health;
        [SerializeField] private MMHealthBar healthBar;

        [Header("Animation States")]
        [SerializeField] private string idleStateName = "SkeletonWithShield-idle";
        [SerializeField] private string walkStateName = "SkeletonWithShield-walk";
        [SerializeField] private string lightAttackStateName = "SkeletonWithShield-lightAttack";
        [SerializeField] private string heavyAttackStateName = "SkeletonWithShield-heavyAttack";
        [SerializeField] private string hurtStateName = "SkeletonWithShield-hurt";
        [SerializeField] private string deathStateName = "SkeletonWithShield-death";
        [SerializeField] private string blockStateName = "SkeletonWithShield-block";
        [SerializeField] private string shieldBreakStateName = "SkeletonWithShield-hurt2";
        [SerializeField] private bool flipSpriteOnPositiveX;

        [Header("Shield Block")]
        [SerializeField] private RuntimeAnimatorController shieldlessAnimatorController;
        [SerializeField] private Sprite shieldlessIdleSprite;
        [SerializeField] private string shieldlessIdleStateName = "skeleton-idle";
        [SerializeField] private string shieldlessWalkStateName = "skeleton-walk";
        [SerializeField] private string shieldlessLightAttackStateName = "SkeletonEliteNoShield-lightAttack";
        [SerializeField] private string shieldlessHeavyAttackStateName = "SkeletonEliteNoShield-heavyAttack";
        [SerializeField] private string shieldlessHurtStateName = "skeleton-hurt";
        [SerializeField] private string shieldlessDeathStateName = "skeleton-death";
        [SerializeField, Min(0.01f)] private float shieldHealth = 200f;
        [SerializeField, Min(0f)] private float shieldDamageInterval = 0.12f;
        [SerializeField, Min(0f)] private float shieldBlockDuration = 0.2f;
        [SerializeField, Min(0f)] private float shieldBreakDuration = 0.8f;
        [SerializeField] private bool hideHealthBarUntilShieldBreak = true;
        [SerializeField] private bool showHealthBarOnShieldBreak = true;

        [Header("Stats")]
        [SerializeField, Min(1f)] private float maxHealth = 65f;
        [SerializeField, Min(0f)] private float moveSpeed = 2.05f;
        [SerializeField, Min(0f)] private float detectionRadius = 6.5f;
        [SerializeField, Min(0f)] private float desiredAttackDistance = 1f;
        [SerializeField] private bool ignorePlayerBodyCollision = true;
        [SerializeField] private LayerMask wallBlockMask = (1 << 8) | (1 << 11) | (1 << 24);
        [SerializeField] private LayerMask targetLayerMask = 1 << 10;

        [Header("Wander")]
        [SerializeField, Min(0f)] private float wanderSpeedMultiplier = 0.45f;
        [SerializeField, Min(0.1f)] private float wanderMoveDuration = 1.25f;
        [SerializeField, Min(0f)] private float wanderPauseDuration = 0.55f;
        [SerializeField, Range(0f, 1f)] private float wanderPauseChance = 0.35f;
        [SerializeField, Min(1)] private int wanderTurnAttempts = 8;
        [SerializeField, Min(0f)] private float wanderWallProbeDistance = 0.2f;

        [Header("Light Attack")]
        [SerializeField, Min(0.01f)] private float attackDuration = 0.9f;
        [SerializeField, Min(0f)] private float attackWindup = 0.42f;
        [SerializeField, Min(0.01f)] private float attackActiveDuration = 0.22f;
        [SerializeField, Min(0f)] private float attackLungeStart = 0.28f;
        [SerializeField, Min(0.01f)] private float attackLungeDuration = 0.22f;
        [SerializeField, Min(0f)] private float attackLungeDistance = 0.5f;
        [SerializeField] private Vector2 attackHitboxSize = new Vector2(0.7f, 0.58f);
        [SerializeField] private Vector2 attackHitboxBaseOffset = new Vector2(0f, 0.08f);
        [SerializeField, Min(0f)] private float attackHitboxDistance = 0.68f;
        [SerializeField, Min(0f)] private float attackDamage = 9f;
        [SerializeField, Min(0f)] private float attackRecoverDuration = 0.14f;
        [SerializeField, Min(0f)] private float attackCooldown = 0.85f;

        [Header("Heavy Attack")]
        [SerializeField, Min(0f)] private float heavyAttackInterval = 3.2f;
        [SerializeField, Min(0.01f)] private float heavyAttackDuration = 1.54f;
        [SerializeField, Min(0f)] private float heavyAttackWindup = 0.6f;
        [SerializeField, Min(0.01f)] private float heavyAttackActiveDuration = 0.94f;
        [SerializeField, Min(0f)] private float heavyAttackLungeStart = 0.6f;
        [SerializeField, Min(0.01f)] private float heavyAttackLungeDuration = 0.94f;
        [SerializeField, Min(0f)] private float heavyAttackLungeDistance = 12f;
        [SerializeField, Min(0f)] private float heavyAttackChargeRange = 5.4f;
        [SerializeField] private bool heavyAttackChargeUsesTargetDistance;
        [SerializeField] private bool heavyAttackChargeStopsAtWall = true;
        [FormerlySerializedAs("heavyAttackChargeStopDistance")]
        [SerializeField, Min(0f)] private float heavyAttackChargeOvershootDistance = 1.2f;
        [SerializeField, Min(0f)] private float heavyAttackWallStopPadding = 0.08f;
        [SerializeField] private Vector2 heavyAttackHitboxSize = new Vector2(1.2f, 1.05f);
        [SerializeField] private Vector2 heavyAttackHitboxBaseOffset = new Vector2(0f, 0.08f);
        [SerializeField, Min(0f)] private float heavyAttackHitboxDistance = 0.9f;
        [SerializeField, Min(0f)] private float heavyAttackDamage = 14f;
        [SerializeField, Min(0f)] private float heavyAttackRecoverDuration;
        [SerializeField, Min(0f)] private float heavyAttackCooldown = 1.2f;

        [Header("Heavy Attack Telegraph")]
        [SerializeField] private bool showHeavyAttackTelegraph = true;
        [SerializeField] private Color heavyAttackTelegraphColor = new Color(1f, 0.34f, 0.08f, 0.3f);
        [SerializeField, Min(0.01f)] private float heavyAttackTelegraphLength = 12f;
        [SerializeField, Min(0f)] private float heavyAttackTelegraphLengthPadding;
        [SerializeField, Min(0f)] private float heavyAttackTelegraphWidthPadding = 0.12f;
        [FormerlySerializedAs("heavyAttackTelegraphOutlineColor")]
        [SerializeField] private Color heavyAttackTelegraphMarkerColor = new Color(1f, 0.92f, 0.55f, 0.75f);
        [FormerlySerializedAs("heavyAttackTelegraphOutlineMinScale")]
        [SerializeField, Range(0.01f, 0.25f)] private float heavyAttackTelegraphMarkerThickness = 0.055f;

        [Header("Attack Common")]
        [SerializeField, Range(0f, 1f)] private float attackKnockbackResistanceMultiplier = 0.35f;
        [SerializeField] private bool suppressHurtAnimationDuringAttack = true;
        [SerializeField, Min(0f)] private float attackInvincibilityDuration = 0.45f;
        [SerializeField] private bool applyAttackKnockback = true;
        [SerializeField, Min(0f)] private float attackKnockbackForce = 190f;
        [SerializeField, Min(0f)] private float heavyAttackKnockbackForce = 280f;
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
        private Vector2 _heavyAttackChargeDirection = Vector2.right;
        private float _heavyAttackChargeDistance;
        private bool _heavyAttackChargeLocked;
        private Quaternion _attackHitboxDefaultLocalRotation = Quaternion.identity;
        private bool _attackHitboxDefaultLocalRotationCached;
        private float _knockbackForceMultiplierBeforeAttack = 1f;
        private bool _attackKnockbackResistanceActive;
        private EliteAttackType _currentAttackType = EliteAttackType.Light;
        private EliteAttackType _pendingAttackType = EliteAttackType.Light;
        private bool _hasPendingAttackType;
        private float _nextHeavyAttackAllowedAt;
        private bool _shieldBroken;
        private bool _shieldlessVisualsApplied;
        private bool _blockAnimationActive;
        private bool _shieldBreakAnimationActive;
        private float _currentShieldHealth;
        private float _nextShieldDamageAllowedAt;
        private float _shieldBlockUntil;
        private float _shieldBreakUntil;
        private bool _prefabShieldHealthCaptured;
        private float _prefabShieldHealth;

        private void Reset()
        {
            ResolveBindings();
        }

        private void Awake()
        {
            ResolveBindings();
            ApplyPlayerBodyCollisionPolicy();
            CaptureVisualBaseScale();
            CacheAttackHitboxDefaultLocalRotation();
            EnsureAttackOverlapBuffer();
            CapturePrefabShieldHealth();
            ResetShieldHealth();
            ConfigureHealth();
            SetAttackHitboxActive(false);
            EnterState(SkeletonActionState.Idle);
        }

        private void Start()
        {
            UpdateHealthBarVisibility();
        }

        private void OnEnable()
        {
            ApplyPlayerBodyCollisionPolicy();

            if (health != null)
            {
                health.OnHit += HandleHit;
                health.OnDeath += HandleDeath;
            }

            if (_shieldBroken)
            {
                UpdateHealthBarVisibility();
            }
        }

        private void OnDisable()
        {
            DeactivateAttackKnockbackResistance();
            HideHeavyAttackTelegraph();
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
                if (!_shieldBreakAnimationActive)
                {
                    TickState();
                }
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
            if (_shieldBreakAnimationActive)
            {
                StopBody();
                return;
            }

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

        private void ApplyPlayerBodyCollisionPolicy()
        {
            if (!ignorePlayerBodyCollision)
            {
                return;
            }

            EnemyCollisionPolicy.EnsurePlayerBodyCollisionIgnore(gameObject);
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
            attackHitboxSize = MaxVector2(attackHitboxSize, 0.01f);
            attackHitboxDistance = Mathf.Max(0f, attackHitboxDistance);
            attackDamage = Mathf.Max(0f, attackDamage);
            attackRecoverDuration = Mathf.Max(0f, attackRecoverDuration);
            attackCooldown = Mathf.Max(0f, attackCooldown);
            heavyAttackInterval = Mathf.Max(0f, heavyAttackInterval);
            heavyAttackDuration = Mathf.Max(0.01f, heavyAttackDuration);
            heavyAttackWindup = Mathf.Max(0f, heavyAttackWindup);
            heavyAttackActiveDuration = Mathf.Max(0.01f, heavyAttackActiveDuration);
            heavyAttackLungeStart = Mathf.Max(0f, heavyAttackLungeStart);
            heavyAttackLungeDuration = Mathf.Max(0.01f, heavyAttackLungeDuration);
            heavyAttackLungeDistance = Mathf.Max(0f, heavyAttackLungeDistance);
            heavyAttackChargeRange = Mathf.Max(0f, heavyAttackChargeRange);
            heavyAttackChargeOvershootDistance = Mathf.Max(0f, heavyAttackChargeOvershootDistance);
            heavyAttackWallStopPadding = Mathf.Max(0f, heavyAttackWallStopPadding);
            heavyAttackHitboxSize = MaxVector2(heavyAttackHitboxSize, 0.01f);
            heavyAttackHitboxDistance = Mathf.Max(0f, heavyAttackHitboxDistance);
            heavyAttackDamage = Mathf.Max(0f, heavyAttackDamage);
            heavyAttackRecoverDuration = Mathf.Max(0f, heavyAttackRecoverDuration);
            heavyAttackCooldown = Mathf.Max(0f, heavyAttackCooldown);
            heavyAttackTelegraphLength = Mathf.Max(0.01f, heavyAttackTelegraphLength);
            heavyAttackTelegraphLengthPadding = Mathf.Max(0f, heavyAttackTelegraphLengthPadding);
            heavyAttackTelegraphWidthPadding = Mathf.Max(0f, heavyAttackTelegraphWidthPadding);
            heavyAttackTelegraphMarkerThickness = Mathf.Clamp(heavyAttackTelegraphMarkerThickness, 0.01f, 0.25f);
            attackKnockbackResistanceMultiplier = Mathf.Clamp01(attackKnockbackResistanceMultiplier);
            attackInvincibilityDuration = Mathf.Max(0f, attackInvincibilityDuration);
            attackKnockbackForce = Mathf.Max(0f, attackKnockbackForce);
            heavyAttackKnockbackForce = Mathf.Max(0f, heavyAttackKnockbackForce);
            attackOverlapBufferSize = Mathf.Max(1, attackOverlapBufferSize);
            hurtVisualDuration = Mathf.Max(0f, hurtVisualDuration);
            deathDestroyDelay = Mathf.Max(0f, deathDestroyDelay);
            shieldHealth = Mathf.Max(0.01f, shieldHealth);
            shieldDamageInterval = Mathf.Max(0f, shieldDamageInterval);
            shieldBlockDuration = Mathf.Max(0f, shieldBlockDuration);
            shieldBreakDuration = Mathf.Max(0f, shieldBreakDuration);
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
                heavyAttackDamage = Mathf.Max(0f, meleeDamage * 1.55f);
            }

            ApplyShieldHealthFromData(data);

            ConfigureHealth();
            if (health != null)
            {
                health.SetHealth(maxHealth);
            }
        }

        private void ApplyShieldHealthFromData(EnemyData data)
        {
            CapturePrefabShieldHealth();
            shieldHealth = data is SkeletonEliteData skeletonEliteData
                ? skeletonEliteData.ShieldHealth
                : _prefabShieldHealth;

            if (!_shieldBroken)
            {
                ResetShieldHealth();
            }
        }

        private void CapturePrefabShieldHealth()
        {
            if (_prefabShieldHealthCaptured)
            {
                return;
            }

            _prefabShieldHealth = Mathf.Max(0.01f, shieldHealth);
            _prefabShieldHealthCaptured = true;
        }

        public bool CanAbsorbShieldDamage
        {
            get
            {
                return _state != SkeletonActionState.Dead
                       && (!_shieldBroken || _shieldBreakAnimationActive);
            }
        }

        public bool TryAbsorbShieldDamage(float damage, GameObject instigator, Vector3 damageDirection)
        {
            if (_state == SkeletonActionState.Dead)
            {
                return false;
            }

            if (_shieldBreakAnimationActive)
            {
                return true;
            }

            if (_shieldBroken)
            {
                return false;
            }

            Vector2 incomingDirection = ResolveIncomingDamageDirection(instigator, damageDirection);
            return AbsorbShieldDamage(damage, incomingDirection);
        }

        private bool AbsorbShieldDamage(float damage, Vector2 incomingDirection)
        {
            if (_state == SkeletonActionState.Dead)
            {
                return false;
            }

            if (_shieldBreakAnimationActive)
            {
                return true;
            }

            if (_shieldBroken)
            {
                return false;
            }

            FaceIncomingDamage(incomingDirection);
            if (damage > 0f && Time.time >= _nextShieldDamageAllowedAt)
            {
                _currentShieldHealth -= damage;
                _nextShieldDamageAllowedAt = Time.time + shieldDamageInterval;
            }

            if (_currentShieldHealth <= 0f)
            {
                TriggerShieldBreak();
            }
            else if (_state == SkeletonActionState.Attack)
            {
                return true;
            }
            else if (_blockAnimationActive)
            {
                _shieldBlockUntil = Mathf.Max(_shieldBlockUntil, Time.time + shieldBlockDuration);
            }
            else
            {
                PlayShieldBlock();
            }

            return true;
        }

        private Vector2 ResolveIncomingDamageDirection(GameObject instigator, Vector3 damageDirection)
        {
            Vector2 direction = damageDirection;
            if (direction.sqrMagnitude > 0.0001f)
            {
                return direction.normalized;
            }

            if (instigator != null)
            {
                Vector2 fromInstigatorToSelf = transform.position - instigator.transform.position;
                if (fromInstigatorToSelf.sqrMagnitude > 0.0001f)
                {
                    return fromInstigatorToSelf.normalized;
                }
            }

            return Vector2.zero;
        }

        private void FaceIncomingDamage(Vector2 incomingDirection)
        {
            if (incomingDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            _lastFacingDirection = -incomingDirection.normalized;
            ApplySpriteFlip(_lastFacingDirection);
        }

        private void PlayShieldBlock()
        {
            _hurtAnimationActive = false;
            _blockAnimationActive = true;
            _shieldBlockUntil = Time.time + shieldBlockDuration;
            PlayAnimatorState(blockStateName);
        }

        private void TriggerShieldBreak()
        {
            _shieldBroken = true;
            StopBody();
            DeactivateAttackKnockbackResistance();
            SetAttackHitboxActive(false);
            InterruptActionForShield(shieldBreakDuration);
            _hurtAnimationActive = false;
            _blockAnimationActive = false;
            _shieldBreakAnimationActive = true;
            _shieldBreakUntil = Time.time + shieldBreakDuration;
            bool playedBreakAnimation = PlayAnimatorState(shieldBreakStateName);

            if (shieldBreakDuration <= 0f || !playedBreakAnimation)
            {
                _shieldBreakAnimationActive = false;
                ApplyShieldlessVisuals();
                PlayAnimatorStateForCurrentState();
            }
        }

        private void InterruptActionForShield(float duration)
        {
            _state = SkeletonActionState.Cooldown;
            _stateStartedAt = Time.time;
            _stateEndsAt = Time.time + Mathf.Max(0f, duration);
            _hasPendingAttackType = false;
            _hitTargetsThisAttack.Clear();
            HideHeavyAttackTelegraph();
        }

        private void ApplyShieldlessVisuals()
        {
            if (_shieldlessVisualsApplied)
            {
                return;
            }

            _shieldBroken = true;
            _shieldlessVisualsApplied = true;
            idleStateName = shieldlessIdleStateName;
            walkStateName = shieldlessWalkStateName;
            lightAttackStateName = shieldlessLightAttackStateName;
            heavyAttackStateName = shieldlessHeavyAttackStateName;
            hurtStateName = shieldlessHurtStateName;
            deathStateName = shieldlessDeathStateName;

            if (animator != null && shieldlessAnimatorController != null)
            {
                animator.runtimeAnimatorController = shieldlessAnimatorController;
                animator.Rebind();
                animator.Update(0f);
            }
            else if (animator != null && shieldlessIdleSprite != null)
            {
                animator.enabled = false;
            }

            if (spriteRenderer != null && shieldlessIdleSprite != null)
            {
                spriteRenderer.sprite = shieldlessIdleSprite;
            }

            ApplySpriteFlip(_lastFacingDirection);
            UpdateHealthBarVisibility();
        }

        private void ResetShieldHealth()
        {
            _currentShieldHealth = Mathf.Max(0.01f, shieldHealth);
        }

        private void UpdateHealthBarVisibility()
        {
            if (!hideHealthBarUntilShieldBreak || healthBar == null)
            {
                return;
            }

            healthBar.AlwaysVisible = false;
            if (!_shieldBroken)
            {
                if (healthBar.enabled)
                {
                    healthBar.ShowBar(false);
                }
                return;
            }

            if (showHealthBarOnShieldBreak && health != null)
            {
                health.UpdateHealthBar(true);
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

            if (heavyAttackTelegraphView == null)
            {
                heavyAttackTelegraphView = GetComponent<AttackTelegraph2DView>();
            }

            if (health == null)
            {
                health = GetComponent<Health>();
            }

            if (healthBar == null)
            {
                healthBar = GetComponent<MMHealthBar>();
            }
        }

        private void CacheAttackHitboxDefaultLocalRotation()
        {
            if (attackHitbox == null || _attackHitboxDefaultLocalRotationCached)
            {
                return;
            }

            _attackHitboxDefaultLocalRotation = attackHitbox.transform.localRotation;
            _attackHitboxDefaultLocalRotationCached = true;
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
                    UpdateHeavyAttackChargeState();
                    UpdateAttackHitboxState();
                    if (Time.time >= _stateEndsAt)
                    {
                        SetAttackHitboxActive(false);
                        HideHeavyAttackTelegraph();
                        float recoverDuration = ResolveCurrentAttackRecoverDuration();
                        if (recoverDuration > 0f)
                        {
                            EnterState(SkeletonActionState.Recover, recoverDuration);
                        }
                        else
                        {
                            EnterState(SkeletonActionState.Cooldown, ResolveCurrentAttackCooldown());
                        }
                    }
                    break;
                case SkeletonActionState.Recover:
                    if (Time.time >= _stateEndsAt)
                    {
                        EnterState(SkeletonActionState.Cooldown, ResolveCurrentAttackCooldown());
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
            if (_hasPendingAttackType)
            {
                _currentAttackType = _pendingAttackType;
                _hasPendingAttackType = false;
            }
            else if (!TrySelectAttackType(out _currentAttackType))
            {
                _currentAttackType = EliteAttackType.Light;
            }

            _attackLungeDirection = _lastFacingDirection.sqrMagnitude > 0.0001f
                ? _lastFacingDirection.normalized
                : Vector2.right;
            _attackLungeDistanceMoved = 0f;
            _heavyAttackChargeLocked = false;
            _heavyAttackChargeDistance = heavyAttackLungeDistance;
            if (_currentAttackType == EliteAttackType.Heavy)
            {
                UpdateHeavyAttackChargePreview();
            }
            else
            {
                HideHeavyAttackTelegraph();
            }

            PositionAttackHitbox();
            _hitTargetsThisAttack.Clear();
            _nextAttackAllowedAt = Time.time + ResolveCurrentAttackCooldown();
            if (_currentAttackType == EliteAttackType.Heavy)
            {
                _nextHeavyAttackAllowedAt = Time.time + heavyAttackInterval;
            }

            EnterState(SkeletonActionState.Attack, ResolveCurrentAttackDuration());
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
                HideHeavyAttackTelegraph();
            }
            else
            {
                _blockAnimationActive = false;
                ActivateAttackKnockbackResistance();
            }

            if (state == SkeletonActionState.Dead)
            {
                _hurtAnimationActive = false;
                HideHeavyAttackTelegraph();
                PlayAnimatorState(deathStateName);
                return;
            }

            if (!_hurtAnimationActive && !_blockAnimationActive && !_shieldBreakAnimationActive)
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
            EnsureHeavyAttackChargeLockedIfNeeded();

            float lungeDistance = ResolveCurrentAttackLungeDistance();
            float lungeDuration = ResolveCurrentAttackLungeDuration();
            if (body == null
                || lungeDistance <= 0f
                || lungeDuration <= 0f
                || _attackLungeDistanceMoved >= lungeDistance)
            {
                StopBody();
                return;
            }

            float elapsed = Time.time - _stateStartedAt;
            float lungeStart = ResolveCurrentAttackLungeStart();
            if (elapsed < lungeStart || elapsed > lungeStart + lungeDuration)
            {
                StopBody();
                return;
            }

            Vector2 direction = _attackLungeDirection.sqrMagnitude > 0.0001f
                ? _attackLungeDirection.normalized
                : Vector2.right;
            float lungeSpeed = lungeDistance / lungeDuration;
            float remainingDistance = lungeDistance - _attackLungeDistanceMoved;
            float distance = Mathf.Min(lungeSpeed * Time.fixedDeltaTime, remainingDistance);
            if (distance <= 0f)
            {
                StopBody();
                return;
            }

            if (TryGetWallHitDistance(direction, distance, out float wallHitDistance))
            {
                float allowedDistance = Mathf.Max(0f, wallHitDistance - heavyAttackWallStopPadding);
                if (allowedDistance > 0f)
                {
                    _lastFacingDirection = direction;
                    ApplySpriteFlip(_lastFacingDirection);
                    body.MovePosition(body.position + direction * allowedDistance);
                    _attackLungeDistanceMoved += allowedDistance;
                }

                _attackLungeDistanceMoved = lungeDistance;
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
            return TryGetWallHitDistance(direction, distance, out _);
        }

        private float ResolveWallLimitedDistance(Vector2 direction, float distance)
        {
            if (TryGetWallHitDistance(direction, distance, out float wallHitDistance))
            {
                return Mathf.Max(0f, wallHitDistance - heavyAttackWallStopPadding);
            }

            return Mathf.Max(0f, distance);
        }

        private bool TryGetWallHitDistance(Vector2 direction, float distance, out float hitDistance)
        {
            if (bodyCollider == null
                || direction.sqrMagnitude <= 0.0001f
                || distance <= 0f
                || wallBlockMask.value == 0)
            {
                hitDistance = 0f;
                return false;
            }

            RaycastHit2D hit;
            if (bodyCollider is BoxCollider2D boxCollider)
            {
                Vector2 center = boxCollider.transform.TransformPoint(boxCollider.offset);
                Vector2 size = Vector2.Scale(boxCollider.size, AbsScale(boxCollider.transform.lossyScale));
                hit = Physics2D.BoxCast(center, size, boxCollider.transform.eulerAngles.z, direction, distance, wallBlockMask);
                hitDistance = Mathf.Max(0f, hit.distance);
                return hit.collider != null;
            }

            if (bodyCollider is CircleCollider2D circleCollider)
            {
                Vector2 center = circleCollider.transform.TransformPoint(circleCollider.offset);
                Vector2 scale = AbsScale(circleCollider.transform.lossyScale);
                float radius = circleCollider.radius * Mathf.Max(scale.x, scale.y);
                hit = Physics2D.CircleCast(center, radius, direction, distance, wallBlockMask);
                hitDistance = Mathf.Max(0f, hit.distance);
                return hit.collider != null;
            }

            Bounds bounds = bodyCollider.bounds;
            hit = Physics2D.BoxCast(bounds.center, bounds.size, 0f, direction, distance, wallBlockMask);
            hitDistance = Mathf.Max(0f, hit.distance);
            return hit.collider != null;
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
            if (Time.time < _nextAttackAllowedAt || !IsTargetUsable())
            {
                return false;
            }

            if (!TrySelectAttackType(out EliteAttackType selectedAttackType))
            {
                return false;
            }

            _pendingAttackType = selectedAttackType;
            _hasPendingAttackType = true;
            return true;
        }

        private bool IsTargetInsideAttackStartArea()
        {
            if (!IsTargetUsable())
            {
                return false;
            }

            return IsTargetOverlappingPredictedAttackHitbox(EliteAttackType.Light)
                   || (Time.time >= _nextHeavyAttackAllowedAt
                       && IsTargetInsideHeavyAttackChargeRange());
        }

        private bool TrySelectAttackType(out EliteAttackType selectedAttackType)
        {
            bool heavyCanReach = Time.time >= _nextHeavyAttackAllowedAt
                                 && IsTargetInsideHeavyAttackChargeRange();
            if (heavyCanReach)
            {
                selectedAttackType = EliteAttackType.Heavy;
                return true;
            }

            if (IsTargetOverlappingPredictedAttackHitbox(EliteAttackType.Light))
            {
                selectedAttackType = EliteAttackType.Light;
                return true;
            }

            selectedAttackType = EliteAttackType.Light;
            return attackHitbox == null
                   && (_target.position - transform.position).sqrMagnitude <= desiredAttackDistance * desiredAttackDistance;
        }

        private bool IsTargetInsideHeavyAttackChargeRange()
        {
            if (!IsTargetUsable())
            {
                return false;
            }

            Vector2 origin = ResolveHeavyAttackChargeOrigin();
            Vector2 toTarget = (Vector2)_target.position - origin;
            float sqrDistance = toTarget.sqrMagnitude;
            float range = Mathf.Max(heavyAttackChargeRange, heavyAttackHitboxDistance);
            if (sqrDistance > range * range)
            {
                return false;
            }

            if (sqrDistance <= 0.0001f)
            {
                return true;
            }

            Vector2 direction = toTarget.normalized;
            float targetDistance = Mathf.Sqrt(sqrDistance);
            float chargeDistance = ResolveHeavyAttackChargeDistance(direction, targetDistance);
            return targetDistance <= chargeDistance + heavyAttackWallStopPadding + 0.01f;
        }

        private void UpdateHeavyAttackChargeState()
        {
            if (_currentAttackType != EliteAttackType.Heavy)
            {
                HideHeavyAttackTelegraph();
                return;
            }

            float elapsed = Time.time - _stateStartedAt;
            if (elapsed < heavyAttackLungeStart)
            {
                UpdateHeavyAttackChargePreview();
                ShowOrRefreshHeavyAttackTelegraph();
                return;
            }

            EnsureHeavyAttackChargeLockedIfNeeded();
            HideHeavyAttackTelegraph();
        }

        private void UpdateHeavyAttackChargePreview()
        {
            ResolveHeavyAttackChargePlan(out _heavyAttackChargeDirection, out _heavyAttackChargeDistance);
            _attackLungeDirection = _heavyAttackChargeDirection;
            _lastFacingDirection = _heavyAttackChargeDirection;
            ApplySpriteFlip(_lastFacingDirection);
        }

        private void EnsureHeavyAttackChargeLockedIfNeeded()
        {
            if (_currentAttackType != EliteAttackType.Heavy || _heavyAttackChargeLocked)
            {
                return;
            }

            float elapsed = Time.time - _stateStartedAt;
            if (elapsed < heavyAttackLungeStart)
            {
                return;
            }

            UpdateHeavyAttackChargePreview();
            _heavyAttackChargeLocked = true;
            _attackLungeDistanceMoved = 0f;
            PositionAttackHitbox();
        }

        private void ResolveHeavyAttackChargePlan(out Vector2 direction, out float distance)
        {
            Vector2 origin = ResolveHeavyAttackChargeOrigin();
            Vector2 fallbackDirection = _lastFacingDirection.sqrMagnitude > 0.0001f
                ? _lastFacingDirection.normalized
                : Vector2.right;

            if (!IsTargetUsable())
            {
                direction = fallbackDirection;
                distance = ResolveHeavyAttackChargeDistance(direction, Mathf.Max(0f, heavyAttackLungeDistance));
                return;
            }

            Vector2 toTarget = (Vector2)_target.position - origin;
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                direction = fallbackDirection;
                distance = ResolveHeavyAttackChargeDistance(direction, Mathf.Max(0f, heavyAttackLungeDistance));
                return;
            }

            float targetDistance = toTarget.magnitude;
            direction = toTarget / targetDistance;
            distance = ResolveHeavyAttackChargeDistance(direction, targetDistance);
        }

        private Vector2 ResolveHeavyAttackChargeOrigin()
        {
            return body != null ? body.position : (Vector2)transform.position;
        }

        private float ResolveHeavyAttackChargeDistance(Vector2 direction, float targetDistance)
        {
            float maxDistance = Mathf.Max(0f, heavyAttackLungeDistance);
            float intendedDistance = maxDistance;
            if (!heavyAttackChargeStopsAtWall && heavyAttackChargeUsesTargetDistance)
            {
                intendedDistance = Mathf.Min(
                    Mathf.Max(0f, targetDistance + heavyAttackChargeOvershootDistance),
                    maxDistance);
            }

            return heavyAttackChargeStopsAtWall
                ? ResolveWallLimitedDistance(direction, intendedDistance)
                : intendedDistance;
        }

        private void ShowOrRefreshHeavyAttackTelegraph()
        {
            AttackTelegraph2DView view = ResolveHeavyAttackTelegraphView();
            if (view == null)
            {
                return;
            }

            view.Refresh(BuildHeavyAttackTelegraphRequest());
        }

        private AttackTelegraph2DView ResolveHeavyAttackTelegraphView()
        {
            if (!showHeavyAttackTelegraph)
            {
                return null;
            }

            if (heavyAttackTelegraphView == null)
            {
                heavyAttackTelegraphView = GetComponent<AttackTelegraph2DView>();
            }

            if (heavyAttackTelegraphView == null)
            {
                heavyAttackTelegraphView = gameObject.AddComponent<AttackTelegraph2DView>();
            }

            return heavyAttackTelegraphView;
        }

        private AttackTelegraphRequest2D BuildHeavyAttackTelegraphRequest()
        {
            Vector2 direction = _heavyAttackChargeDirection.sqrMagnitude > 0.0001f
                ? _heavyAttackChargeDirection.normalized
                : Vector2.right;
            Vector2 hitboxSize = ResolveAttackHitboxWorldSize(EliteAttackType.Heavy);
            Vector2 startCenter = ResolveHeavyAttackTelegraphStartCenter(direction);
            float telegraphLength = Mathf.Min(
                Mathf.Max(0.01f, _heavyAttackChargeDistance),
                heavyAttackTelegraphLength);
            float telegraphProgress = Mathf.Clamp01((Time.time - _stateStartedAt) / Mathf.Max(0.01f, heavyAttackLungeStart));

            return new AttackTelegraphRequest2D
            {
                Shape = AttackTelegraphShape2D.Box,
                Center = startCenter + direction * (telegraphLength * 0.5f),
                Direction = direction,
                Size = new Vector2(
                    telegraphLength + heavyAttackTelegraphLengthPadding,
                    hitboxSize.y + heavyAttackTelegraphWidthPadding),
                Color = heavyAttackTelegraphColor,
                Duration = 0f,
                ShowTimingMarker = true,
                TimingProgress = telegraphProgress,
                TimingMarkerColor = heavyAttackTelegraphMarkerColor,
                TimingMarkerThickness = heavyAttackTelegraphMarkerThickness
            };
        }

        private Vector2 ResolveHeavyAttackTelegraphStartCenter(Vector2 direction)
        {
            Vector2 origin = ResolveHeavyAttackChargeOrigin();
            Vector2 localOffset = heavyAttackHitboxBaseOffset + direction * heavyAttackHitboxDistance;
            return origin + localOffset;
        }

        private Vector2 ResolveAttackHitboxWorldSize(EliteAttackType attackType)
        {
            Vector2 size = ResolveAttackHitboxSize(attackType);
            if (attackHitbox == null)
            {
                return size;
            }

            return Vector2.Scale(size, AbsScale(attackHitbox.transform.lossyScale));
        }

        private void HideHeavyAttackTelegraph()
        {
            if (heavyAttackTelegraphView != null)
            {
                heavyAttackTelegraphView.Hide();
            }
        }

        private bool IsTargetOverlappingPredictedAttackHitbox(EliteAttackType attackType)
        {
            if (!TryResolveAttackHitboxPreview(attackType, out Vector2 center, out Vector2 size, out float angle))
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
            float windup = ResolveCurrentAttackWindup();
            bool shouldBeActive = elapsed >= windup && elapsed <= windup + ResolveCurrentAttackActiveDuration();
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
            Vector2 localPosition = ResolveAttackHitboxBaseOffset(_currentAttackType)
                                    + direction * ResolveAttackHitboxDistance(_currentAttackType);
            attackHitbox.transform.localPosition = localPosition;
            attackHitbox.transform.localRotation = ResolveAttackHitboxRotation(direction);
            attackHitbox.size = ResolveAttackHitboxSize(_currentAttackType);
        }

        private Quaternion ResolveAttackHitboxRotation(Vector2 direction)
        {
            return ResolveAttackHitboxRotation(_currentAttackType, direction);
        }

        private Quaternion ResolveAttackHitboxRotation(EliteAttackType attackType, Vector2 direction)
        {
            CacheAttackHitboxDefaultLocalRotation();
            if (attackType != EliteAttackType.Heavy || direction.sqrMagnitude <= 0.0001f)
            {
                return _attackHitboxDefaultLocalRotation;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            return Quaternion.AngleAxis(angle, Vector3.forward);
        }

        private void DrawAttackHitboxGizmo()
        {
            DrawAttackHitboxGizmo(EliteAttackType.Light, LightAttackGizmoColor);
            DrawAttackHitboxGizmo(EliteAttackType.Heavy, HeavyAttackGizmoColor);
        }

        private void DrawAttackHitboxGizmo(EliteAttackType attackType, Color color)
        {
            if (!TryResolveAttackHitboxPreview(attackType, out Vector2 center, out Vector2 size, out float angle))
            {
                return;
            }

            Gizmos.color = color;
            Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.Euler(0f, 0f, angle), Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, size.y, 0.01f));
        }

        private bool TryResolveAttackHitboxPreview(EliteAttackType attackType, out Vector2 center, out Vector2 size, out float angle)
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
            Vector2 localPosition = ResolveAttackHitboxBaseOffset(attackType)
                                    + direction * ResolveAttackHitboxDistance(attackType);
            Vector3 previewLocalPosition = new Vector3(localPosition.x, localPosition.y, hitboxTransform.localPosition.z);
            Quaternion localRotation = ResolveAttackHitboxRotation(attackType, direction);
            Matrix4x4 previewMatrix = parent.localToWorldMatrix
                                      * Matrix4x4.TRS(previewLocalPosition, localRotation, hitboxTransform.localScale);
            Vector2 scale = AbsScale(hitboxTransform.lossyScale);
            Quaternion rotation = parent.rotation * localRotation;

            center = previewMatrix.MultiplyPoint3x4(attackHitbox.offset);
            size = Vector2.Scale(ResolveAttackHitboxSize(attackType), scale);
            angle = rotation.eulerAngles.z;
            return true;
        }

        private float ResolveCurrentAttackDuration()
        {
            if (_currentAttackType != EliteAttackType.Heavy)
            {
                return attackDuration;
            }

            return Mathf.Max(0.01f, heavyAttackWindup + ResolveCurrentAttackLungeDuration());
        }

        private float ResolveCurrentAttackWindup()
        {
            return _currentAttackType == EliteAttackType.Heavy ? heavyAttackWindup : attackWindup;
        }

        private float ResolveCurrentAttackActiveDuration()
        {
            return _currentAttackType == EliteAttackType.Heavy ? ResolveCurrentAttackLungeDuration() : attackActiveDuration;
        }

        private float ResolveCurrentAttackLungeStart()
        {
            return _currentAttackType == EliteAttackType.Heavy ? heavyAttackLungeStart : attackLungeStart;
        }

        private float ResolveCurrentAttackLungeDuration()
        {
            if (_currentAttackType != EliteAttackType.Heavy)
            {
                return attackLungeDuration;
            }

            float maxDistance = Mathf.Max(0.01f, heavyAttackLungeDistance);
            float baseDuration = Mathf.Max(0.01f, heavyAttackLungeDuration);
            float lungeSpeed = maxDistance / baseDuration;
            return Mathf.Max(0.01f, _heavyAttackChargeDistance / lungeSpeed);
        }

        private float ResolveCurrentAttackLungeDistance()
        {
            if (_currentAttackType != EliteAttackType.Heavy)
            {
                return attackLungeDistance;
            }

            return _heavyAttackChargeDistance;
        }

        private float ResolveCurrentAttackRecoverDuration()
        {
            return _currentAttackType == EliteAttackType.Heavy ? heavyAttackRecoverDuration : attackRecoverDuration;
        }

        private float ResolveCurrentAttackCooldown()
        {
            return _currentAttackType == EliteAttackType.Heavy ? heavyAttackCooldown : attackCooldown;
        }

        private float ResolveCurrentAttackDamage()
        {
            return _currentAttackType == EliteAttackType.Heavy ? heavyAttackDamage : attackDamage;
        }

        private float ResolveCurrentAttackKnockbackForce()
        {
            return _currentAttackType == EliteAttackType.Heavy ? heavyAttackKnockbackForce : attackKnockbackForce;
        }

        private Vector2 ResolveAttackHitboxSize(EliteAttackType attackType)
        {
            return attackType == EliteAttackType.Heavy ? heavyAttackHitboxSize : attackHitboxSize;
        }

        private Vector2 ResolveAttackHitboxBaseOffset(EliteAttackType attackType)
        {
            return attackType == EliteAttackType.Heavy ? heavyAttackHitboxBaseOffset : attackHitboxBaseOffset;
        }

        private float ResolveAttackHitboxDistance(EliteAttackType attackType)
        {
            return attackType == EliteAttackType.Heavy ? heavyAttackHitboxDistance : attackHitboxDistance;
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
            if (!_attackHitboxActive || attackHitbox == null || ResolveCurrentAttackDamage() <= 0f)
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
            targetHealth.Damage(ResolveCurrentAttackDamage(), gameObject, 0f, attackInvincibilityDuration, direction);
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
                || ResolveCurrentAttackKnockbackForce() <= 0f
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

            Vector3 knockback = direction.normalized * ResolveCurrentAttackKnockbackForce();
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

            if (_shieldBreakAnimationActive && Time.time >= _shieldBreakUntil)
            {
                _shieldBreakAnimationActive = false;
                ApplyShieldlessVisuals();
                PlayAnimatorStateForCurrentState();
            }

            if (_shieldBreakAnimationActive)
            {
                return;
            }

            if (_blockAnimationActive && Time.time >= _shieldBlockUntil)
            {
                _blockAnimationActive = false;
                PlayAnimatorStateForCurrentState();
            }

            if (_blockAnimationActive)
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
            if (_blockAnimationActive || _shieldBreakAnimationActive)
            {
                return;
            }

            switch (_state)
            {
                case SkeletonActionState.WanderMove:
                case SkeletonActionState.Approach:
                    PlayAnimatorState(walkStateName);
                    break;
                case SkeletonActionState.Attack:
                    PlayAnimatorState(_currentAttackType == EliteAttackType.Heavy ? heavyAttackStateName : lightAttackStateName);
                    break;
                case SkeletonActionState.Dead:
                    PlayAnimatorState(deathStateName);
                    break;
                default:
                    PlayAnimatorState(idleStateName);
                    break;
            }
        }

        private bool PlayAnimatorState(string stateName)
        {
            if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(stateName))
            {
                return false;
            }

            int stateHash = Animator.StringToHash("Base Layer." + stateName);
            if (!animator.HasState(0, stateHash))
            {
                stateHash = Animator.StringToHash(stateName);
                if (!animator.HasState(0, stateHash))
                {
                    return false;
                }
            }

            ApplySpriteFlip(_lastFacingDirection);
            animator.Play(stateHash, 0, 0f);
            animator.Update(0f);
            return true;
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

            if (_blockAnimationActive || _shieldBreakAnimationActive)
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
            _blockAnimationActive = false;
            _shieldBreakAnimationActive = false;
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

        private static Vector2 MaxVector2(Vector2 value, float minValue)
        {
            return new Vector2(Mathf.Max(value.x, minValue), Mathf.Max(value.y, minValue));
        }

        private static bool IsLayerInMask(int layer, LayerMask mask)
        {
            return (mask.value & (1 << layer)) != 0;
        }
    }
}
