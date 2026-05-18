using System.Collections.Generic;
using LostMemory.Combat.Telegraph;
using LostMemory.Stage;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies.Boss.Bertha
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Boss/Bertha/Bertha Dash Attack Controller")]
    public sealed class BerthaDashAttackController : MonoBehaviour, MMEventListener<AIStateEvent>
    {
        [SerializeField] private AIBrain brain;
        [SerializeField] private Character character;
        [SerializeField] private CharacterOrientation2D orientationAbility;
        [SerializeField] private CharacterDash2D dashAbility;
        [SerializeField] private Animator animator;
        [SerializeField] private BossIntroSequenceController introSequenceController;
        [SerializeField] private Transform telegraphOrigin;
        [SerializeField] private AttackTelegraph2DView telegraphView;
        [SerializeField] private string attackStateName = "DashCharge";
        [SerializeField] private string attackAnimationStateName = "DashAtk";
        [SerializeField, Min(0)] private int attackAnimationLayer;
        [SerializeField, Min(0f)] private float impactTime = 26f / 12f;
        [SerializeField] private Color telegraphColor = new Color(1f, 0.34f, 0.08f, 0.32f);
        [SerializeField] private Vector2 attackOffset = Vector2.zero;
        [SerializeField] private Vector2 attackSize = new Vector2(5f, 5f);
        [SerializeField] private LayerMask targetLayerMask = 1 << 10;
        [SerializeField, Min(1)] private int maximumHits = 16;
        [SerializeField] private float damage = 10f;
        [SerializeField] private float targetInvincibilityDuration = 0.5f;
        [SerializeField] private float horizontalFacingThreshold = 0.05f;
        [SerializeField] private AudioClip impactSfx;
        [SerializeField, Range(0f, 1f)] private float impactSfxVolume = 1f;
        [SerializeField, Range(0.1f, 3f)] private float impactSfxMinPitch = 1f;
        [SerializeField, Range(0.1f, 3f)] private float impactSfxMaxPitch = 1f;
        [SerializeField] private bool fallbackWithoutSoundManager = true;
        [SerializeField] private string debugName = "BerthaDashAttack";
        [SerializeField] private bool debugLogging;

        private readonly HashSet<Health> _hitTargetsThisAttack = new HashSet<Health>();
        private Collider2D[] _overlapBuffer;
        private Vector2 _lockedImpactDirection = Vector2.right;
        private Vector2 _lockedImpactCenter;
        private float _attackStateElapsed;
        private bool _hasLockedAttack;
        private bool _hasExecutedImpact;
        private bool _hasPendingAttackDamage;

        private void Reset()
        {
            RefreshReferences();
        }

        private void OnValidate()
        {
            maximumHits = Mathf.Max(1, maximumHits);
            impactSfxVolume = Mathf.Clamp01(impactSfxVolume);
            impactSfxMinPitch = Mathf.Clamp(impactSfxMinPitch, 0.1f, 3f);
            impactSfxMaxPitch = Mathf.Clamp(impactSfxMaxPitch, impactSfxMinPitch, 3f);
            RefreshReferences();
            EnsureBuffer();
        }

        private void Awake()
        {
            RefreshReferences();
            EnsureBuffer();
        }

        private void OnEnable()
        {
            this.MMEventStartListening<AIStateEvent>();
        }

        private void OnDisable()
        {
            this.MMEventStopListening<AIStateEvent>();
            telegraphView?.Hide();
            ClearLockedAttack();
        }

        private void Update()
        {
            if (IsDead())
            {
                telegraphView?.Hide();
                ClearLockedAttack();
                return;
            }

            if (!_hasLockedAttack)
            {
                return;
            }

            if (_hasPendingAttackDamage && !_hasExecutedImpact)
            {
                if (telegraphView != null)
                {
                    telegraphView.Refresh(BuildImpactTelegraphRequest());
                }

                _attackStateElapsed += Time.deltaTime;
                if (_attackStateElapsed >= impactTime)
                {
                    ExecuteImpactNow();
                }
            }
        }

        public void OnMMEvent(AIStateEvent stateEvent)
        {
            if (stateEvent.Brain != brain)
            {
                return;
            }

            if (IsDead())
            {
                telegraphView?.Hide();
                ClearLockedAttack();
                return;
            }

            string enteringState = stateEvent.EnterState != null ? stateEvent.EnterState.StateName : string.Empty;
            string exitingState = stateEvent.ExitState != null ? stateEvent.ExitState.StateName : string.Empty;

            if (enteringState == attackStateName)
            {
                LockAttackPlan();
                ApplyLockedFacing();
                PlayAttackAnimation();
                telegraphView?.Show(BuildImpactTelegraphRequest());
                QueueAttackDamage();
                _attackStateElapsed = 0f;
                _hasExecutedImpact = false;

                if (impactTime <= 0f)
                {
                    ExecuteImpactNow();
                }
                return;
            }

            if (exitingState == attackStateName)
            {
                ExecuteQueuedAttackDamage();
                telegraphView?.Hide();
                ClearLockedAttack();
            }
        }

        public void RefreshReferences()
        {
            brain ??= GetComponent<AIBrain>();
            character ??= GetComponent<Character>();
            orientationAbility ??= GetComponent<CharacterOrientation2D>();
            dashAbility ??= GetComponent<CharacterDash2D>();
            introSequenceController ??= GetComponent<BossIntroSequenceController>();
            telegraphOrigin ??= transform;
            telegraphView ??= GetComponent<AttackTelegraph2DView>();

            if (animator == null)
            {
                animator = introSequenceController != null
                    ? introSequenceController.BossAnimator
                    : ResolveAnimator();
            }
        }

        public void Configure(
            AIBrain configuredBrain,
            Character configuredCharacter,
            CharacterOrientation2D configuredOrientationAbility,
            CharacterDash2D configuredDashAbility,
            Animator configuredAnimator,
            string configuredAttackStateName,
            string configuredAttackAnimationStateName,
            int configuredAttackAnimationLayer,
            float configuredImpactTime,
            LayerMask configuredTargetLayerMask,
            Vector2 configuredAttackOffset,
            Vector2 configuredAttackSize,
            float configuredDamage,
            float configuredTargetInvincibilityDuration,
            string configuredDebugName = null)
        {
            brain = configuredBrain;
            character = configuredCharacter;
            orientationAbility = configuredOrientationAbility;
            dashAbility = configuredDashAbility;
            animator = configuredAnimator;
            attackStateName = configuredAttackStateName;
            attackAnimationStateName = configuredAttackAnimationStateName;
            attackAnimationLayer = configuredAttackAnimationLayer;
            SetImpactTime(configuredImpactTime);
            targetLayerMask = configuredTargetLayerMask;
            attackOffset = configuredAttackOffset;
            attackSize = configuredAttackSize;
            damage = configuredDamage;
            targetInvincibilityDuration = configuredTargetInvincibilityDuration;
            if (!string.IsNullOrWhiteSpace(configuredDebugName))
            {
                debugName = configuredDebugName;
            }

            RefreshReferences();
            EnsureBuffer();
        }

        public void SetImpactTime(float configuredImpactTime)
        {
            impactTime = Mathf.Max(0f, configuredImpactTime);
        }

        private void PlayAttackAnimation()
        {
            if (animator == null || string.IsNullOrWhiteSpace(attackAnimationStateName))
            {
                return;
            }

            animator.Play(attackAnimationStateName, attackAnimationLayer, 0f);
        }

        private void QueueAttackDamage()
        {
            _hasPendingAttackDamage = true;
        }

        private void ExecuteImpactNow()
        {
            if (!_hasPendingAttackDamage || _hasExecutedImpact)
            {
                return;
            }

            _hasExecutedImpact = true;
            ExecuteQueuedAttackDamage();
            telegraphView?.Hide();
        }

        private void ExecuteQueuedAttackDamage()
        {
            if (!_hasPendingAttackDamage)
            {
                return;
            }

            _hasPendingAttackDamage = false;

            if (IsDead())
            {
                return;
            }

            PlayImpactSfx();
            ExecuteAttack();
        }

        private void PlayImpactSfx()
        {
            if (impactSfx == null)
            {
                return;
            }

            float pitch = ResolveImpactSfxPitch();

            if (MMSoundManager.HasInstance && MMSoundManager.Current != null)
            {
                MMSoundManagerPlayOptions options = MMSoundManagerPlayOptions.Default;
                options.MmSoundManagerTrack = MMSoundManager.MMSoundManagerTracks.Sfx;
                options.Location = transform.position;
                options.Volume = impactSfxVolume;
                options.Pitch = pitch;
                options.Loop = false;

                MMSoundManagerSoundPlayEvent.Trigger(impactSfx, options);
                return;
            }

            if (fallbackWithoutSoundManager)
            {
                AudioSource.PlayClipAtPoint(impactSfx, transform.position, impactSfxVolume);
            }
        }

        private float ResolveImpactSfxPitch()
        {
            float minPitch = Mathf.Clamp(impactSfxMinPitch, 0.1f, 3f);
            float maxPitch = Mathf.Clamp(impactSfxMaxPitch, minPitch, 3f);
            return Mathf.Approximately(minPitch, maxPitch)
                ? minPitch
                : Random.Range(minPitch, maxPitch);
        }

        private void ExecuteAttack()
        {
            EnsureBuffer();
            _hitTargetsThisAttack.Clear();

            Vector2 direction = _hasLockedAttack ? _lockedImpactDirection : ResolveAttackDirection();
            Vector2 center = _hasLockedAttack ? _lockedImpactCenter : ResolveImpactCenter(ResolveOriginPosition(), direction);

            int hitCount = OverlapCircleTargets(center, ResolveCircleRadius(attackSize));

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hitCollider = _overlapBuffer[i];
                if (hitCollider == null)
                {
                    continue;
                }

                Health health = hitCollider.GetComponentInParent<Health>();
                if (health == null
                    || _hitTargetsThisAttack.Contains(health)
                    || IsOwnedByAttacker(health, gameObject)
                    || !health.CanTakeDamageThisFrame())
                {
                    continue;
                }

                _hitTargetsThisAttack.Add(health);
                health.Damage(
                    damage,
                    gameObject,
                    targetInvincibilityDuration,
                    targetInvincibilityDuration,
                    new Vector3(direction.x, direction.y, 0f));
            }

            Log("Executed " + attackStateName + " impact.");
        }

        private void LockAttackPlan()
        {
            Vector2 direction = ResolveAttackDirection();
            float dashDistance = ResolveDashDistance();
            Vector2 origin = ResolveOriginPosition() + direction * dashDistance;

            _lockedImpactDirection = direction;
            _lockedImpactCenter = ResolveImpactCenter(origin, direction);
            _hasLockedAttack = true;
        }

        private void ApplyLockedFacing()
        {
            if (orientationAbility == null)
            {
                return;
            }

            if (Mathf.Abs(_lockedImpactDirection.x) < horizontalFacingThreshold)
            {
                return;
            }

            orientationAbility.FaceDirection(_lockedImpactDirection.x >= 0f ? 1 : -1);
        }

        private Vector2 ResolveAttackDirection()
        {
            if (dashAbility != null)
            {
                Vector2 dashDirection = new Vector2(dashAbility.DashDirection.x, dashAbility.DashDirection.y);
                if (dashDirection.sqrMagnitude > 0.0001f)
                {
                    return dashDirection.normalized;
                }
            }

            if (orientationAbility != null)
            {
                switch (orientationAbility.CurrentFacingDirection)
                {
                    case Character.FacingDirections.West:
                        return Vector2.left;
                    case Character.FacingDirections.North:
                        return Vector2.up;
                    case Character.FacingDirections.South:
                        return Vector2.down;
                    default:
                        return Vector2.right;
                }
            }

            Vector2 transformRight = transform.right;
            return transformRight.sqrMagnitude > 0.0001f ? transformRight.normalized : Vector2.right;
        }

        private AttackTelegraphRequest2D BuildImpactTelegraphRequest()
        {
            Vector2 normalizedDirection = _lockedImpactDirection.sqrMagnitude > 0.0001f
                ? _lockedImpactDirection.normalized
                : Vector2.right;

            return new AttackTelegraphRequest2D
            {
                Shape = AttackTelegraphShape2D.Circle,
                Center = _lockedImpactCenter,
                Direction = normalizedDirection,
                Size = attackSize,
                Color = telegraphColor,
                Duration = 0f
            };
        }

        private Vector2 ResolveOriginPosition()
        {
            return telegraphOrigin != null ? (Vector2)telegraphOrigin.position : (Vector2)transform.position;
        }

        private float ResolveDashDistance()
        {
            if (dashAbility == null)
            {
                return 0f;
            }

            return Mathf.Max(0f, dashAbility.DashDistance);
        }

        private Vector2 ResolveImpactCenter(Vector2 origin, Vector2 direction)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            return origin + (Vector2)(Quaternion.Euler(0f, 0f, angle) * (Vector3)attackOffset);
        }

        private static float ResolveCircleRadius(Vector2 size)
        {
            return Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.y)) * 0.5f;
        }

        private int OverlapCircleTargets(Vector2 center, float radius)
        {
            ContactFilter2D contactFilter = new ContactFilter2D();
            contactFilter.SetLayerMask(targetLayerMask);
            contactFilter.useTriggers = Physics2D.queriesHitTriggers;
            return Physics2D.OverlapCircle(center, radius, contactFilter, _overlapBuffer);
        }

        private void ClearLockedAttack()
        {
            _attackStateElapsed = 0f;
            _hasLockedAttack = false;
            _hasExecutedImpact = false;
            _hasPendingAttackDamage = false;
            _hitTargetsThisAttack.Clear();
        }

        private Animator ResolveAnimator()
        {
            Transform visualChild = transform.Find("Visual");
            if (visualChild != null)
            {
                Transform spriteChild = visualChild.Find("BerthaSprite");
                if (spriteChild != null)
                {
                    Animator spriteAnimator = spriteChild.GetComponent<Animator>();
                    if (spriteAnimator != null)
                    {
                        return spriteAnimator;
                    }
                }

                Animator visualAnimator = visualChild.GetComponentInChildren<Animator>(true);
                if (visualAnimator != null)
                {
                    return visualAnimator;
                }
            }

            return GetComponentInChildren<Animator>(true);
        }

        private void EnsureBuffer()
        {
            if (_overlapBuffer == null || _overlapBuffer.Length != maximumHits)
            {
                _overlapBuffer = new Collider2D[Mathf.Max(1, maximumHits)];
            }
        }

        private bool IsDead()
        {
            return character != null
                && character.ConditionState != null
                && character.ConditionState.CurrentState == CharacterStates.CharacterConditions.Dead;
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
                Debug.Log("[" + debugName + "] " + message, this);
            }
        }
    }
}
