using System.Collections.Generic;
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
        [SerializeField] private string attackStateName = "DashCharge";
        [SerializeField] private string attackAnimationStateName = "DashAtk";
        [SerializeField, Min(0)] private int attackAnimationLayer;
        [SerializeField] private Vector2 attackOffset = Vector2.zero;
        [SerializeField] private Vector2 attackSize = new Vector2(5f, 5f);
        [SerializeField] private LayerMask targetLayerMask = 1 << 10;
        [SerializeField, Min(1)] private int maximumHits = 16;
        [SerializeField] private float damage = 10f;
        [SerializeField] private float targetInvincibilityDuration = 0.5f;
        [SerializeField] private float horizontalFacingThreshold = 0.05f;
        [SerializeField] private string debugName = "BerthaDashAttack";
        [SerializeField] private bool debugLogging;

        private readonly HashSet<Health> _hitTargetsThisAttack = new HashSet<Health>();
        private Collider2D[] _overlapBuffer;
        private bool _hasPendingAttackDamage;

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
            _hasPendingAttackDamage = false;
            _hitTargetsThisAttack.Clear();
        }

        public void OnMMEvent(AIStateEvent stateEvent)
        {
            if (stateEvent.Brain != brain)
            {
                return;
            }

            if (IsDead())
            {
                _hasPendingAttackDamage = false;
                _hitTargetsThisAttack.Clear();
                return;
            }

            string enteringState = stateEvent.EnterState != null ? stateEvent.EnterState.StateName : string.Empty;
            string exitingState = stateEvent.ExitState != null ? stateEvent.ExitState.StateName : string.Empty;

            if (enteringState == attackStateName)
            {
                ApplyAttackFacing();
                PlayAttackAnimation();
                QueueAttackDamage();
                return;
            }

            if (exitingState == attackStateName)
            {
                ExecuteQueuedAttackDamage();
            }
        }

        public void RefreshReferences()
        {
            brain ??= GetComponent<AIBrain>();
            character ??= GetComponent<Character>();
            orientationAbility ??= GetComponent<CharacterOrientation2D>();
            dashAbility ??= GetComponent<CharacterDash2D>();
            introSequenceController ??= GetComponent<BossIntroSequenceController>();

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

        private void ExecuteAttack()
        {
            EnsureBuffer();
            _hitTargetsThisAttack.Clear();

            Vector2 direction = ResolveAttackDirection();
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Vector2 center = (Vector2)transform.position + (Vector2)(Quaternion.Euler(0f, 0f, angle) * (Vector3)attackOffset);

            int hitCount = Physics2D.OverlapBoxNonAlloc(center, attackSize, angle, _overlapBuffer, targetLayerMask);

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

        private void ApplyAttackFacing()
        {
            if (orientationAbility == null)
            {
                return;
            }

            Vector2 direction = ResolveAttackDirection();
            if (Mathf.Abs(direction.x) < horizontalFacingThreshold)
            {
                return;
            }

            orientationAbility.FaceDirection(direction.x >= 0f ? 1 : -1);
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
