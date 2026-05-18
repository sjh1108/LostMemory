using System.Collections.Generic;
using LostMemory.Combat;
using LostMemory.Combat.Telegraph;
using LostMemory.Enemies;
using LostMemory.Rendering;
using LostMemory.Stage;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies.Boss.Bertha
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Boss/Bertha/Bertha Heavy Attack Bootstrap")]
    public sealed class BerthaHeavyAttackBootstrap : MonoBehaviour
    {
        private static readonly Color TelegraphColor = new Color(1f, 0.16f, 0.05f, 0.38f);

        [SerializeField] private Transform visualRoot;
        [SerializeField] private bool autoConfigureInEditMode = true;
        [SerializeField] private bool addMissingCoreCombatComponents = true;
        [SerializeField] private LayerMask targetLayerMask = 1 << 10;
        [SerializeField] private LayerMask obstacleLayerMask = (1 << 8) | (1 << 24);
        [SerializeField] private float detectionRadius = 15f;
        [SerializeField] private float minimumMoveDistance = 0.1f;
        [SerializeField] private bool enableAttackPattern = true;
        [SerializeField] private float attackRange = 3.25f;
        [SerializeField] private float telegraphDuration = 0.9f;
        [SerializeField] private float attackDuration = 1.9f;
        [SerializeField] private float recoverDuration = 0.55f;
        [SerializeField] private Vector2 attackOffset = Vector2.zero;
        [SerializeField] private Vector2 attackSize = new Vector2(6.5f, 6.5f);
        [SerializeField] private float attackDamage = 16f;
        [SerializeField] private float attackInvincibilityDuration = 0.5f;
        [SerializeField] private float initialHealth = 100f;
        [SerializeField] private bool debugLogging;

        private bool _isConfiguring;

        private void Reset()
        {
            EnsureConfigured();
        }

        private void OnEnable()
        {
            if (Application.isPlaying || autoConfigureInEditMode)
            {
                EnsureConfigured();
            }
        }

        private void OnValidate()
        {
            if (!Application.isPlaying && autoConfigureInEditMode)
            {
                EnsureConfigured();
            }
        }

        [ContextMenu("Rebuild Heavy Attack Pattern")]
        public void RebuildHeavyAttackPattern()
        {
            EnsureConfigured();
        }

        private void EnsureConfigured()
        {
            if (_isConfiguring)
            {
                return;
            }

            _isConfiguring = true;

            try
            {
                visualRoot ??= FindChildByName("Visual");
                Animator animator = ResolveAnimator();
                AIBrain brain = GetOrAdd<AIBrain>(gameObject);

                if (addMissingCoreCombatComponents)
                {
                    EnsureCoreCombatComponents(animator);
                }

                Character character = GetOrAdd<Character>(gameObject);
                CharacterOrientation2D orientation = GetOrAdd<CharacterOrientation2D>(gameObject);
                AttackTelegraph2DView telegraphView = GetOrAdd<AttackTelegraph2DView>(gameObject);
                BerthaHeavyAttackController attackController = GetOrAdd<BerthaHeavyAttackController>(gameObject);
                BossIntroSequenceController introSequenceController = GetComponent<BossIntroSequenceController>();
                BerthaBrainAnimationController brainAnimationController = GetOrAdd<BerthaBrainAnimationController>(gameObject);
                attackController.Configure(
                    brain,
                    character,
                    orientation,
                    animator,
                    transform,
                    telegraphView,
                    "HeavyTelegraph",
                    "HeavyAttack",
                    "HeavyAtk",
                    0,
                    TelegraphColor,
                    ResolveTargetLayerMask(),
                    attackOffset,
                    attackSize,
                    attackDamage,
                    attackInvincibilityDuration,
                    "BerthaHeavyAttack");
                brainAnimationController.Configure(brain, animator, introSequenceController);

                AIActionDoNothing idleAction = GetOrAdd<AIActionDoNothing>(gameObject);
                idleAction.Label = "BerthaIdle";

                AIActionMoveTowardsTarget2D moveAction = GetOrAdd<AIActionMoveTowardsTarget2D>(gameObject);
                moveAction.Label = "BerthaChase";
                moveAction.UseMinimumXDistance = true;
                moveAction.MinimumXDistance = minimumMoveDistance;

                AIActionFaceTowardsTarget2D faceAction = GetOrAdd<AIActionFaceTowardsTarget2D>(gameObject);
                faceAction.Label = "BerthaFaceTarget";
                faceAction.Mode = AIActionFaceTowardsTarget2D.Modes.LeftRight;

                AIDecisionDetectTargetRadius2D detectTarget = GetOrAdd<AIDecisionDetectTargetRadius2D>(gameObject);
                detectTarget.Label = "DetectTarget";
                detectTarget.Radius = detectionRadius;
                detectTarget.TargetLayer = ResolveTargetLayerMask();
                detectTarget.ObstacleDetection = true;
                detectTarget.ObstacleMask = ResolveObstacleLayerMask();
                detectTarget.TargetCheckFrequency = 1f;
                detectTarget.OverlapMaximum = 10;

                AIDecisionTargetIsAlive targetIsAlive = GetOrAdd<AIDecisionTargetIsAlive>(gameObject);
                targetIsAlive.Label = "TargetIsAlive";

                AIDecisionDistanceToTarget attackRangeDecision = GetOrAdd<AIDecisionDistanceToTarget>(gameObject);
                attackRangeDecision.Label = "HeavyAttackRange";
                attackRangeDecision.ComparisonMode = AIDecisionDistanceToTarget.ComparisonModes.LowerThan;
                attackRangeDecision.Distance = attackRange;

                AIDecisionTimeInState telegraphTimer = GetOrAddTimeDecision("HeavyTelegraphDuration");
                telegraphTimer.AfterTimeMin = telegraphDuration;
                telegraphTimer.AfterTimeMax = telegraphDuration;

                AIDecisionTimeInState attackTimer = GetOrAddTimeDecision("HeavyAttackDuration");
                attackTimer.AfterTimeMin = attackDuration;
                attackTimer.AfterTimeMax = attackDuration;

                AIDecisionTimeInState recoverTimer = GetOrAddTimeDecision("HeavyRecoverDuration");
                recoverTimer.AfterTimeMin = recoverDuration;
                recoverTimer.AfterTimeMax = recoverDuration;

                brain.States = BuildStates(
                    idleAction,
                    moveAction,
                    faceAction,
                    detectTarget,
                    targetIsAlive,
                    attackRangeDecision,
                    telegraphTimer,
                    attackTimer,
                    recoverTimer);

                for (int i = 0; i < brain.States.Count; i++)
                {
                    brain.States[i].SetBrain(brain);
                }

                Log("Bertha heavy attack pattern synchronized.");
            }
            finally
            {
                _isConfiguring = false;
            }
        }

        private void EnsureCoreCombatComponents(Animator animator)
        {
            Rigidbody2D body = GetOrAdd<Rigidbody2D>(gameObject);
            body.bodyType = RigidbodyType2D.Dynamic;
            body.mass = 10000f;
            body.gravityScale = 0f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.linearDamping = 1f;

            BoxCollider2D bodyCollider = GetOrAdd<BoxCollider2D>(gameObject);
            if (bodyCollider.size == Vector2.zero || bodyCollider.size == Vector2.one)
            {
                bodyCollider.size = new Vector2(0.6f, 0.5f);
                bodyCollider.offset = new Vector2(0f, 0.19f);
            }

            TopDownController2D controller = GetOrAdd<TopDownController2D>(gameObject);
            controller.ObstaclesLayerMask = ResolveObstacleLayerMask();

            Health health = GetOrAdd<Health>(gameObject);
            health.InitialHealth = initialHealth;
            health.MaximumHealth = initialHealth;
            health.TargetAnimator = animator;
            health.DestroyOnDeath = false;
            health.DelayBeforeDestruction = 0f;
            health.DisableControllerOnDeath = true;
            health.DisableModelOnDeath = false;
            health.DisableCollisionsOnDeath = true;
            if (Application.isPlaying)
            {
                EnemyDeathAnimationLock.EnsureOn(gameObject, health, animator);
                TopDownYSortOrder.EnsureOn(
                    visualRoot != null ? visualRoot.gameObject : gameObject,
                    transform,
                    animator != null ? animator.GetComponent<SpriteRenderer>() : null);
                EnemyCollisionPolicy.EnsurePlayerBodyCollisionIgnore(gameObject);
            }

            Character character = GetOrAdd<Character>(gameObject);
            character.CharacterAnimator = animator;
            character.CharacterModel = visualRoot != null ? visualRoot.gameObject : character.CharacterModel;
            character.CharacterHealth = health;

            CharacterMovement movement = GetOrAdd<CharacterMovement>(gameObject);
            movement.WalkSpeed = 3f;
            movement.Acceleration = 10f;
            movement.Deceleration = 10f;
            movement.ShouldSetMovement = true;

            CharacterOrientation2D orientation = GetOrAdd<CharacterOrientation2D>(gameObject);
            orientation.ModelShouldFlip = true;
            orientation.ModelFlipValueLeft = new Vector3(-1f, 1f, 1f);
            orientation.ModelFlipValueRight = Vector3.one;
            orientation.ModelShouldRotate = false;
        }

        private List<AIState> BuildStates(
            AIActionDoNothing idleAction,
            AIActionMoveTowardsTarget2D moveAction,
            AIActionFaceTowardsTarget2D faceAction,
            AIDecisionDetectTargetRadius2D detectTarget,
            AIDecisionTargetIsAlive targetIsAlive,
            AIDecisionDistanceToTarget attackRangeDecision,
            AIDecisionTimeInState telegraphTimer,
            AIDecisionTimeInState attackTimer,
            AIDecisionTimeInState recoverTimer)
        {
            List<AITransition> movingTransitions = new List<AITransition>
            {
                CreateTransition(detectTarget, string.Empty, "Detecting"),
                CreateTransition(targetIsAlive, string.Empty, "Detecting")
            };

            if (enableAttackPattern)
            {
                movingTransitions.Add(CreateTransition(attackRangeDecision, "HeavyTelegraph", string.Empty));
            }

            return new List<AIState>
            {
                CreateState(
                    "Detecting",
                    new AIAction[] { idleAction },
                    new[]
                    {
                        CreateTransition(detectTarget, "Moving", string.Empty)
                    }),
                CreateState(
                    "Moving",
                    new AIAction[] { moveAction, faceAction },
                    movingTransitions),
                CreateState(
                    "HeavyTelegraph",
                    new AIAction[] { idleAction },
                    new[]
                    {
                        CreateTransition(telegraphTimer, "HeavyAttack", string.Empty)
                    }),
                CreateState(
                    "HeavyAttack",
                    new AIAction[] { idleAction },
                    new[]
                    {
                        CreateTransition(attackTimer, "Recover", string.Empty)
                    }),
                CreateState(
                    "Recover",
                    new AIAction[] { idleAction },
                    new[]
                    {
                        CreateTransition(targetIsAlive, string.Empty, "Detecting"),
                        CreateTransition(recoverTimer, "Moving", string.Empty)
                    })
            };
        }

        private static AIState CreateState(string stateName, IEnumerable<AIAction> actions, IEnumerable<AITransition> transitions)
        {
            AIActionsList actionList = new AIActionsList();
            foreach (AIAction action in actions)
            {
                actionList.Add(action);
            }

            AITransitionsList transitionList = new AITransitionsList();
            foreach (AITransition transition in transitions)
            {
                transitionList.Add(transition);
            }

            return new AIState
            {
                StateName = stateName,
                Actions = actionList,
                Transitions = transitionList
            };
        }

        private static AITransition CreateTransition(AIDecision decision, string trueState, string falseState)
        {
            return new AITransition
            {
                Decision = decision,
                TrueState = trueState,
                FalseState = falseState,
                LastDecisionEvaluation = false
            };
        }

        private AIDecisionTimeInState GetOrAddTimeDecision(string label)
        {
            AIDecisionTimeInState[] decisions = GetComponents<AIDecisionTimeInState>();
            for (int i = 0; i < decisions.Length; i++)
            {
                if (decisions[i] != null && decisions[i].Label == label)
                {
                    return decisions[i];
                }
            }

            AIDecisionTimeInState created = gameObject.AddComponent<AIDecisionTimeInState>();
            created.Label = label;
            return created;
        }

        private Animator ResolveAnimator()
        {
            if (visualRoot != null)
            {
                Transform spriteChild = visualRoot.Find("BerthaSprite");
                if (spriteChild != null)
                {
                    Animator spriteAnimator = spriteChild.GetComponent<Animator>();
                    if (spriteAnimator != null)
                    {
                        return spriteAnimator;
                    }
                }

                Animator childAnimator = visualRoot.GetComponentInChildren<Animator>(true);
                if (childAnimator != null)
                {
                    return childAnimator;
                }
            }

            return GetComponentInChildren<Animator>(true);
        }

        private Transform FindChildByName(string childName)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }

        private LayerMask ResolveTargetLayerMask()
        {
            int namedMask = LayerMask.GetMask("Player");
            if (namedMask != 0)
            {
                return namedMask;
            }

            return targetLayerMask;
        }

        private LayerMask ResolveObstacleLayerMask()
        {
            int namedMask = LayerMask.GetMask("Obstacles", "DungeonWall");
            if (namedMask != 0)
            {
                return namedMask;
            }

            return obstacleLayerMask;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            if (component == null)
            {
                component = target.AddComponent<T>();
            }

            return component;
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[BerthaHeavyAttackBootstrap] " + message, this);
            }
        }
    }
}
