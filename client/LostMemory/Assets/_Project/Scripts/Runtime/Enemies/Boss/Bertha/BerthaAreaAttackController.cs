using System.Collections.Generic;
using LostMemory.Combat.Telegraph;
using LostMemory.Stage;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies.Boss.Bertha
{
    [AddComponentMenu("Lost Memory/Enemies/Boss/Bertha/Bertha Area Attack Controller")]
    public class BerthaAreaAttackController : MonoBehaviour, MMEventListener<AIStateEvent>
    {
        [SerializeField] private AIBrain brain;
        [SerializeField] private Character character;
        [SerializeField] private CharacterOrientation2D orientationAbility;
        [SerializeField] private Animator animator;
        [SerializeField] private BossIntroSequenceController introSequenceController;
        [SerializeField] private Transform telegraphOrigin;
        [SerializeField] private AttackTelegraph2DView telegraphView;
        [SerializeField] private string telegraphStateName = "LightTelegraph";
        [SerializeField] private string attackStateName = "LightAttack1";
        [SerializeField] private string attackAnimationStateName = "LightAtk1";
        [SerializeField, Min(0)] private int attackAnimationLayer;
        [SerializeField] private float impactTime = -1f;
        [SerializeField] private Color telegraphColor = new Color(1f, 0.34f, 0.08f, 0.32f);
        [SerializeField] private Vector2 attackOffset = Vector2.zero;
        [SerializeField] private Vector2 attackSize = new Vector2(5f, 5f);
        [SerializeField] private LayerMask targetLayerMask = 1 << 10;
        [SerializeField, Min(1)] private int maximumHits = 16;
        [SerializeField] private float damage = 8f;
        [SerializeField] private float targetInvincibilityDuration = 0.5f;
        [SerializeField] private float horizontalFacingThreshold = 0.05f;
        [SerializeField] private string debugName = "BerthaAreaAttack";
        [SerializeField] private bool debugLogging;

        private readonly HashSet<Health> _hitTargetsThisAttack = new HashSet<Health>();
        private Collider2D[] _overlapBuffer;
        private Vector2 _lockedDirection = Vector2.right;
        private Vector2 _lockedCenter;
        private float _attackStateElapsed;
        private bool _hasLockedAttack;
        private bool _hasExecutedImpact;
        private bool _hasPendingAttackDamage;

        public string TelegraphStateName => telegraphStateName;
        public string AttackStateName => attackStateName;

        private void Reset()
        {
            RefreshReferences();
        }

        private void OnValidate()
        {
            maximumHits = Mathf.Max(1, maximumHits);
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

            if (_hasPendingAttackDamage && !_hasExecutedImpact && impactTime >= 0f)
            {
                _attackStateElapsed += Time.deltaTime;
                if (_attackStateElapsed >= impactTime)
                {
                    ExecuteImpactNow();
                }
            }

            if (!IsInState(telegraphStateName) || telegraphView == null || !_hasLockedAttack)
            {
                return;
            }

            telegraphView.Refresh(BuildTelegraphRequest());
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

            if (enteringState == telegraphStateName)
            {
                LockAttackFromFacing();
                ApplyLockedFacing();
                telegraphView?.Show(BuildTelegraphRequest());
                return;
            }

            if (enteringState == attackStateName)
            {
                telegraphView?.Hide();
                ApplyLockedFacing();
                PlayAttackAnimation();
                QueueAttackDamage();
                _attackStateElapsed = 0f;
                _hasExecutedImpact = false;

                if (impactTime <= 0f && impactTime >= 0f)
                {
                    ExecuteImpactNow();
                }
                return;
            }

            if (exitingState == telegraphStateName)
            {
                telegraphView?.Hide();
            }

            if (exitingState == attackStateName)
            {
                ExecuteQueuedAttackDamage();
                ClearLockedAttack();
            }
        }

        public void RefreshReferences()
        {
            brain ??= GetComponent<AIBrain>();
            character ??= GetComponent<Character>();
            orientationAbility ??= GetComponent<CharacterOrientation2D>();
            introSequenceController ??= GetComponent<BossIntroSequenceController>();
            telegraphView ??= GetComponent<AttackTelegraph2DView>();
            telegraphOrigin ??= transform;

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
            CharacterOrientation2D configuredOrientation,
            Animator configuredAnimator,
            Transform configuredTelegraphOrigin,
            AttackTelegraph2DView configuredTelegraphView,
            string configuredTelegraphStateName,
            string configuredAttackStateName,
            string configuredAttackAnimationStateName,
            int configuredAttackAnimationLayer,
            Color configuredTelegraphColor,
            LayerMask configuredTargetLayerMask,
            Vector2 configuredAttackOffset,
            Vector2 configuredAttackSize,
            float configuredDamage,
            float configuredTargetInvincibilityDuration,
            string configuredDebugName = null)
        {
            Configure(
                configuredBrain,
                configuredCharacter,
                configuredOrientation,
                configuredAnimator,
                configuredTelegraphOrigin,
                configuredTelegraphView,
                configuredTelegraphStateName,
                configuredAttackStateName,
                configuredAttackAnimationStateName,
                configuredAttackAnimationLayer,
                configuredTelegraphColor,
                configuredTargetLayerMask,
                configuredAttackOffset,
                configuredAttackSize,
                configuredDamage,
                configuredTargetInvincibilityDuration,
                -1f,
                configuredDebugName);
        }

        public void Configure(
            AIBrain configuredBrain,
            Character configuredCharacter,
            CharacterOrientation2D configuredOrientation,
            Animator configuredAnimator,
            Transform configuredTelegraphOrigin,
            AttackTelegraph2DView configuredTelegraphView,
            string configuredTelegraphStateName,
            string configuredAttackStateName,
            string configuredAttackAnimationStateName,
            int configuredAttackAnimationLayer,
            Color configuredTelegraphColor,
            LayerMask configuredTargetLayerMask,
            Vector2 configuredAttackOffset,
            Vector2 configuredAttackSize,
            float configuredDamage,
            float configuredTargetInvincibilityDuration,
            float configuredImpactTime,
            string configuredDebugName = null)
        {
            brain = configuredBrain;
            character = configuredCharacter;
            orientationAbility = configuredOrientation;
            animator = configuredAnimator;
            telegraphOrigin = configuredTelegraphOrigin;
            telegraphView = configuredTelegraphView;
            telegraphStateName = configuredTelegraphStateName;
            attackStateName = configuredAttackStateName;
            attackAnimationStateName = configuredAttackAnimationStateName;
            attackAnimationLayer = configuredAttackAnimationLayer;
            telegraphColor = configuredTelegraphColor;
            targetLayerMask = configuredTargetLayerMask;
            attackOffset = configuredAttackOffset;
            attackSize = configuredAttackSize;
            damage = configuredDamage;
            targetInvincibilityDuration = configuredTargetInvincibilityDuration;
            SetImpactTime(configuredImpactTime);
            if (!string.IsNullOrWhiteSpace(configuredDebugName))
            {
                debugName = configuredDebugName;
            }

            RefreshReferences();
            EnsureBuffer();
        }

        public void SetImpactTime(float configuredImpactTime)
        {
            impactTime = configuredImpactTime;
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

        private void LockAttackFromFacing()
        {
            Vector2 origin = ResolveOriginPosition();
            _lockedDirection = ResolveFacingDirection();
            float angle = Mathf.Atan2(_lockedDirection.y, _lockedDirection.x) * Mathf.Rad2Deg;
            _lockedCenter = origin + (Vector2)(Quaternion.Euler(0f, 0f, angle) * (Vector3)attackOffset);
            _hasLockedAttack = true;
        }

        private void ApplyLockedFacing()
        {
            if (orientationAbility == null || Mathf.Abs(_lockedDirection.x) < horizontalFacingThreshold)
            {
                return;
            }

            orientationAbility.FaceDirection(_lockedDirection.x >= 0f ? 1 : -1);
        }

        private void PlayAttackAnimation()
        {
            if (animator == null || string.IsNullOrWhiteSpace(attackAnimationStateName))
            {
                return;
            }

            animator.Play(attackAnimationStateName, attackAnimationLayer, 0f);
        }

        private void ExecuteAttack()
        {
            EnsureBuffer();
            _hitTargetsThisAttack.Clear();

            if (!_hasLockedAttack)
            {
                LockAttackFromFacing();
            }

            int hitCount = OverlapCircleTargets(_lockedCenter, ResolveCircleRadius(attackSize));

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
                    new Vector3(_lockedDirection.x, _lockedDirection.y, 0f));
            }

            Log("Executed " + attackStateName + ".");
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

            ExecuteAttack();
        }

        private AttackTelegraphRequest2D BuildTelegraphRequest()
        {
            Vector2 direction = _lockedDirection.sqrMagnitude > 0.0001f ? _lockedDirection.normalized : Vector2.right;
            return new AttackTelegraphRequest2D
            {
                Shape = AttackTelegraphShape2D.Circle,
                Center = _lockedCenter,
                Direction = direction,
                Size = attackSize,
                Color = telegraphColor,
                Duration = 0f
            };
        }

        private Vector2 ResolveOriginPosition()
        {
            return telegraphOrigin != null ? telegraphOrigin.position : transform.position;
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

        private Vector2 ResolveFacingDirection()
        {
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

            return ResolveFallbackDirection();
        }

        private Vector2 ResolveFallbackDirection()
        {
            Vector2 transformRight = transform.right;
            return transformRight.sqrMagnitude > 0.0001f ? transformRight.normalized : Vector2.right;
        }

        private bool IsDead()
        {
            return character != null
                && character.ConditionState != null
                && character.ConditionState.CurrentState == CharacterStates.CharacterConditions.Dead;
        }

        private bool IsInState(string stateName)
        {
            return brain != null
                && brain.CurrentState != null
                && brain.CurrentState.StateName == stateName;
        }

        private void ClearLockedAttack()
        {
            _attackStateElapsed = 0f;
            _hasLockedAttack = false;
            _hasExecutedImpact = false;
            _hasPendingAttackDamage = false;
            _hitTargetsThisAttack.Clear();
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
