using System.Collections.Generic;
using LostMemory.Combat.Telegraph;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies.Boss.Bertha
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Boss/Bertha/Bertha Dash Pattern Bootstrap")]
    public sealed class BerthaDashPatternBootstrap : MonoBehaviour
    {
        private const float DashAnimationDuration = 8f / 12f;
        private const string DetectingStateName = "Detecting";
        private const string MovingStateName = "Moving";
        private const string TelegraphStateName = "Telegraph";
        private const string ChargeStateName = "Charge";
        private const string RecoverStateName = "Recover";

        [Header("Scene Roots")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform combatRoot;

        [Header("Editor Sync")]
        [SerializeField] private bool autoConfigureInEditMode = true;
        [SerializeField] private bool addMissingCoreCombatComponents = true;
        [SerializeField] private bool disableTelegraphAnimationUntilAnimatorReady = true;

        [Header("Detection")]
        [SerializeField] private LayerMask targetLayerMask = 1 << 10;
        [SerializeField] private LayerMask obstacleLayerMask = 1 << 8;
        [SerializeField] private float detectionRadius = 15f;
        [SerializeField] private float minimumMoveDistance = 0.1f;

        [Header("Dash")]
        [SerializeField] private float dashRange = 4.5f;
        [SerializeField] private float dashDuration = DashAnimationDuration;
        [SerializeField] private float dashCooldown = 1.2f;
        [SerializeField] private float telegraphDuration = 0.5f;
        [SerializeField] private float recoverDuration = 0.45f;
        [SerializeField] private bool playChargeAnimation = true;
        [SerializeField] private string chargeAnimationStateName = "Dash";

        [Header("Damage Area")]
        [SerializeField] private Vector2 chargeDamageAreaOffset = Vector2.zero;
        [SerializeField] private Vector2 chargeDamageAreaSize = new Vector2(0.8f, 0.8f);
        [SerializeField] private float dashDamage = 10f;
        [SerializeField] private float dashHitInvincibilityDuration = 0.5f;
        [SerializeField] private Vector3 dashKnockbackForce = new Vector3(10f, 10f, 10f);

        [Header("Health")]
        [SerializeField] private float initialHealth = 100f;

        [Header("Debug")]
        [SerializeField] private bool debugLogging;

        private bool _isConfiguring;

        private void Reset()
        {
            EnsureConfigured();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying && autoConfigureInEditMode)
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

        [ContextMenu("Rebuild Dash Pattern")]
        public void RebuildDashPattern()
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
                combatRoot ??= FindOrCreateChild("CombatRoot", transform);

                Animator animator = ResolveAnimator();
                AIBrain brain = GetOrAdd<AIBrain>(gameObject);

                if (addMissingCoreCombatComponents)
                {
                    EnsureCoreCombatComponents(animator);
                }

                BerthaDashHitGate dashHitGate = EnsureChargeDamageArea();
                AttackTelegraph2DView telegraphView = GetOrAdd<AttackTelegraph2DView>(gameObject);
                AIBrainDashTelegraphDriver telegraphDriver = GetOrAdd<AIBrainDashTelegraphDriver>(gameObject);

                CharacterDamageDash2D dashAbility = GetOrAdd<CharacterDamageDash2D>(gameObject);
                dashAbility.DashDistance = dashRange;
                dashAbility.DashDuration = dashDuration;
                dashAbility.Cooldown.ConsumptionDuration = dashCooldown;
                dashAbility.InvincibleWhileDashing = false;
                dashAbility.TargetDamageOnTouch = dashHitGate;

                AIActionDoNothing idleAction = GetOrAdd<AIActionDoNothing>(gameObject);
                idleAction.Label = "BerthaIdle";

                AIActionMoveTowardsTarget2D moveAction = GetOrAdd<AIActionMoveTowardsTarget2D>(gameObject);
                moveAction.Label = "BerthaChase";
                moveAction.UseMinimumXDistance = true;
                moveAction.MinimumXDistance = minimumMoveDistance;

                AIActionFaceTowardsTarget2D faceAction = GetOrAdd<AIActionFaceTowardsTarget2D>(gameObject);
                faceAction.Label = "BerthaFaceTarget";
                faceAction.Mode = AIActionFaceTowardsTarget2D.Modes.LeftRight;

                AIActionDash dashAction = GetOrAdd<AIActionDash>(gameObject);
                dashAction.Label = "BerthaChargeDash";
                dashAction.Mode = AIActionDash.Modes.None;

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

                AIDecisionDistanceToTargetAndDashReady dashReady = GetOrAdd<AIDecisionDistanceToTargetAndDashReady>(gameObject);
                dashReady.Label = "DashReady";
                dashReady.Configure(dashAbility, AIDecisionDistanceToTargetAndDashReady.ComparisonModes.LowerThan, dashRange);

                AIDecisionTimeInState telegraphTimer = GetOrAddTimeDecision("TelegraphDuration");
                telegraphTimer.AfterTimeMin = telegraphDuration;
                telegraphTimer.AfterTimeMax = telegraphDuration;

                AIDecisionTimeInState chargeTimer = GetOrAddTimeDecision("ChargeDuration");
                chargeTimer.AfterTimeMin = dashDuration;
                chargeTimer.AfterTimeMax = dashDuration;

                AIDecisionTimeInState recoverTimer = GetOrAddTimeDecision("RecoverDuration");
                recoverTimer.AfterTimeMin = recoverDuration;
                recoverTimer.AfterTimeMax = recoverDuration;

                telegraphDriver.Configure(
                    brain,
                    GetOrAdd<Character>(gameObject),
                    dashAbility,
                    GetOrAdd<CharacterOrientation2D>(gameObject),
                    animator,
                    dashAction,
                    dashHitGate.GetComponent<BoxCollider2D>(),
                    transform,
                    telegraphView,
                    TelegraphStateName,
                    ChargeStateName,
                    !disableTelegraphAnimationUntilAnimatorReady,
                    false,
                    "Idle",
                    "Idle",
                    "Walk",
                    0,
                    playChargeAnimation,
                    chargeAnimationStateName,
                    0);

                brain.States = BuildDashStates(
                    idleAction,
                    moveAction,
                    faceAction,
                    dashAction,
                    detectTarget,
                    targetIsAlive,
                    dashReady,
                    telegraphTimer,
                    chargeTimer,
                    recoverTimer);

                for (int i = 0; i < brain.States.Count; i++)
                {
                    brain.States[i].SetBrain(brain);
                }

                Log("Bertha dash pattern synchronized.");
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

        private BerthaDashHitGate EnsureChargeDamageArea()
        {
            Transform areaRoot = FindOrCreateChild("ChargeDamageArea", combatRoot);
            areaRoot.localPosition = Vector3.zero;
            areaRoot.localRotation = Quaternion.identity;
            areaRoot.localScale = Vector3.one;

            BoxCollider2D damageCollider = GetOrAdd<BoxCollider2D>(areaRoot.gameObject);
            damageCollider.isTrigger = true;
            damageCollider.offset = chargeDamageAreaOffset;
            damageCollider.size = chargeDamageAreaSize;

            BerthaDashHitGate hitGate = GetOrAdd<BerthaDashHitGate>(areaRoot.gameObject);
            hitGate.TargetLayerMask = ResolveTargetLayerMask();
            hitGate.MinDamageCaused = dashDamage;
            hitGate.MaxDamageCaused = dashDamage;
            hitGate.InvincibilityDuration = dashHitInvincibilityDuration;
            hitGate.DamageDirectionMode = DamageOnTouch.DamageDirections.BasedOnVelocity;
            hitGate.DamageCausedKnockbackType = DamageOnTouch.KnockbackStyles.AddForce;
            hitGate.DamageCausedKnockbackDirection = DamageOnTouch.KnockbackDirections.BasedOnDirection;
            hitGate.DamageCausedKnockbackForce = dashKnockbackForce;
            hitGate.TriggerFilter = DamageOnTouch.TriggerAndCollisionMask.OnTriggerEnter2D;
            hitGate.Owner = gameObject;

            return hitGate;
        }

        private List<AIState> BuildDashStates(
            AIActionDoNothing idleAction,
            AIActionMoveTowardsTarget2D moveAction,
            AIActionFaceTowardsTarget2D faceAction,
            AIActionDash dashAction,
            AIDecisionDetectTargetRadius2D detectTarget,
            AIDecisionTargetIsAlive targetIsAlive,
            AIDecisionDistanceToTargetAndDashReady dashReady,
            AIDecisionTimeInState telegraphTimer,
            AIDecisionTimeInState chargeTimer,
            AIDecisionTimeInState recoverTimer)
        {
            return new List<AIState>
            {
                CreateState(
                    DetectingStateName,
                    new AIAction[] { idleAction },
                    new[]
                    {
                        CreateTransition(detectTarget, MovingStateName, string.Empty)
                    }),
                CreateState(
                    MovingStateName,
                    new AIAction[] { moveAction, faceAction },
                    new[]
                    {
                        CreateTransition(detectTarget, string.Empty, DetectingStateName),
                        CreateTransition(targetIsAlive, string.Empty, DetectingStateName),
                        CreateTransition(dashReady, TelegraphStateName, string.Empty)
                    }),
                CreateState(
                    TelegraphStateName,
                    new AIAction[] { idleAction },
                    new[]
                    {
                        CreateTransition(telegraphTimer, ChargeStateName, string.Empty)
                    }),
                CreateState(
                    ChargeStateName,
                    new AIAction[] { dashAction },
                    new[]
                    {
                        CreateTransition(chargeTimer, RecoverStateName, string.Empty)
                    }),
                CreateState(
                    RecoverStateName,
                    new AIAction[] { idleAction },
                    new[]
                    {
                        CreateTransition(targetIsAlive, string.Empty, DetectingStateName),
                        CreateTransition(recoverTimer, MovingStateName, string.Empty)
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

            Animator ownAnimator = GetComponent<Animator>();
            if (ownAnimator != null)
            {
                return ownAnimator;
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

        private static Transform FindOrCreateChild(string childName, Transform parent)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }
            }

            GameObject childObject = new GameObject(childName);
            Transform childTransform = childObject.transform;
            childTransform.SetParent(parent, false);
            return childTransform;
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
            int namedMask = LayerMask.GetMask("Obstacles");
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
                Debug.Log("[BerthaDashPatternBootstrap] " + message, this);
            }
        }
    }
}
