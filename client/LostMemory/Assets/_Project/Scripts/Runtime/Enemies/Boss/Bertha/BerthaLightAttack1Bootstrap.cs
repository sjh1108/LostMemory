using System.Collections.Generic;
using LostMemory.Combat.Telegraph;
using LostMemory.Stage;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies.Boss.Bertha
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Boss/Bertha/Bertha Light Attack 1 Bootstrap")]
    public sealed class BerthaLightAttack1Bootstrap : MonoBehaviour
    {
        private static readonly Vector2 LegacyAttackOffset = new Vector2(1.3f, 0f);
        private static readonly Vector2 LegacyAttackSize = new Vector2(2.4f, 1.3f);
        private static readonly Vector2 RequestedAttackOffset = Vector2.zero;
        private static readonly Vector2 RequestedAttackSize = new Vector2(5f, 5f);
        private static readonly Color LightAttack1TelegraphColor = new Color(1f, 0.34f, 0.08f, 0.32f);
        private static readonly Color LightAttack2TelegraphColor = new Color(1f, 0.44f, 0.12f, 0.32f);
        private static readonly Color HeavyTelegraphColor = new Color(1f, 0.16f, 0.05f, 0.38f);
        private static readonly Color FullComboTelegraphColor = new Color(1f, 0.12f, 0.05f, 0.42f);

        private const float LegacyAttackRange = 2f;
        private const float RequestedAttackRange = 2.5f;
        private const float LegacyHeavyAttackDuration = 1.9f;
        private const float RequestedHeavyAttackDuration = 22f / 12f;
        private const float DashAttackAnimationDuration = 27f / 12f;
        private const string DetectingStateName = "Detecting";
        private const string MovingStateName = "Moving";
        private const string LightTelegraphStateName = "LightTelegraph";
        private const string LightAttack1StateName = "LightAttack1";
        private const string LightRecoverStateName = "LightRecover";
        private const string Light2TelegraphStateName = "Light2Telegraph";
        private const string LightAttack2StateName = "LightAttack2";
        private const string Light2RecoverStateName = "Light2Recover";
        private const string HeavyTelegraphStateName = "HeavyTelegraph";
        private const string HeavyAttackStateName = "HeavyAttack";
        private const string HeavyRecoverStateName = "HeavyRecover";
        private const string DashTelegraphStateName = "DashTelegraph";
        private const string DashChargeStateName = "DashCharge";
        private const string DashRecoverStateName = "DashRecover";
        private const string FullTelegraphStateName = "FullTelegraph";
        private const string FullComboAttackStateName = "FullComboAttack";
        private const string FullRecoverStateName = "FullRecover";

        [Header("Scene Roots")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform combatRoot;

        [Header("Editor Sync")]
        [SerializeField] private bool autoConfigureInEditMode = true;
        [SerializeField] private bool addMissingCoreCombatComponents = true;

        [Header("Detection")]
        [SerializeField] private LayerMask targetLayerMask = 1 << 10;
        [SerializeField] private LayerMask obstacleLayerMask = 1 << 8;
        [SerializeField] private float detectionRadius = 15f;
        [SerializeField] private float minimumMoveDistance = 0.1f;
        [SerializeField] private bool enableAttackPattern = true;

        [Header("Light Attack 1")]
        [SerializeField] private float attackRange = RequestedAttackRange;
        [SerializeField] private float telegraphDuration = 0.7f;
        [SerializeField] private float attackDuration = 0.85f;
        [SerializeField] private float recoverDuration = 0.35f;
        [SerializeField] private Vector2 attackOffset = Vector2.zero;
        [SerializeField] private Vector2 attackSize = new Vector2(5f, 5f);
        [SerializeField] private float attackDamage = 8f;
        [SerializeField] private float attackInvincibilityDuration = 0.5f;

        [Header("Light Attack 2")]
        [SerializeField] private float lightAttack2Range = 2.5f;
        [SerializeField] private float lightAttack2TelegraphDuration = 0.5f;
        [SerializeField] private float lightAttack2Duration = 0.55f;
        [SerializeField] private float lightAttack2RecoverDuration = 0.3f;
        [SerializeField] private Vector2 lightAttack2Offset = Vector2.zero;
        [SerializeField] private Vector2 lightAttack2Size = new Vector2(5f, 5f);
        [SerializeField] private float lightAttack2Damage = 10f;
        [SerializeField] private float lightAttack2InvincibilityDuration = 0.5f;

        [Header("Heavy Attack")]
        [SerializeField] private float heavyAttackRange = 3.25f;
        [SerializeField] private float heavyTelegraphDuration = 0.9f;
        [SerializeField] private float heavyAttackDuration = RequestedHeavyAttackDuration;
        [SerializeField] private float heavyRecoverDuration = 0.55f;
        [SerializeField] private Vector2 heavyAttackOffset = Vector2.zero;
        [SerializeField] private Vector2 heavyAttackSize = new Vector2(6.5f, 6.5f);
        [SerializeField] private float heavyAttackDamage = 16f;
        [SerializeField] private float heavyAttackInvincibilityDuration = 0.5f;

        [Header("Dash Attack")]
        [SerializeField] private float dashMinimumRange = 3f;
        [SerializeField] private float dashRange = 4.5f;
        [SerializeField] private float dashDuration = 0.32f;
        [SerializeField] private float dashAttackDuration = DashAttackAnimationDuration;
        [SerializeField] private float dashTelegraphDuration = 0.5f;
        [SerializeField] private float dashRecoverDuration = 0.45f;
        [SerializeField] private Vector2 chargeDamageAreaOffset = Vector2.zero;
        [SerializeField] private Vector2 chargeDamageAreaSize = new Vector2(0.8f, 0.8f);
        [SerializeField] private Vector2 dashAttackOffset = Vector2.zero;
        [SerializeField] private Vector2 dashAttackSize = new Vector2(5f, 5f);
        [SerializeField] private float dashDamage = 10f;
        [SerializeField] private float dashHitInvincibilityDuration = 0.5f;
        [SerializeField] private Vector3 dashKnockbackForce = new Vector3(10f, 10f, 10f);

        [Header("Full Combo")]
        [SerializeField, Range(0f, 1f)] private float specialPatternHealthThresholdNormalized = 0.9f;
        [SerializeField] private float specialPatternCooldown = 15f;
        [SerializeField] private float fullComboRange = 3f;
        [SerializeField] private float fullComboTelegraphDuration = 0.75f;
        [SerializeField] private float fullComboDuration = 3.2f;
        [SerializeField] private float fullComboRecoverDuration = 0.65f;
        [SerializeField] private Vector2 fullComboOffset = Vector2.zero;
        [SerializeField] private Vector2 fullComboSize = new Vector2(7f, 7f);
        [SerializeField] private float fullComboDamage = 24f;
        [SerializeField] private float fullComboInvincibilityDuration = 0.5f;

        [Header("Basic Pattern Weights")]
        [SerializeField, Min(0)] private int lightAttack1Weight = 3;
        [SerializeField, Min(0)] private int lightAttack2Weight = 3;
        [SerializeField, Min(0)] private int heavyAttackWeight = 2;

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
            if (Application.isPlaying || autoConfigureInEditMode)
            {
                EnsureConfigured();
            }
        }

        private void OnValidate()
        {
            NormalizeCombatProfile();

            if (!Application.isPlaying && autoConfigureInEditMode)
            {
                EnsureConfigured();
            }
        }

        [ContextMenu("Rebuild Bertha Combat Pattern")]
        public void RebuildLightAttackPattern()
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
                NormalizeCombatProfile();
                visualRoot ??= FindChildByName("Visual");
                combatRoot ??= FindOrCreateChild("CombatRoot", transform);

                Animator animator = ResolveAnimator();
                AIBrain brain = GetOrAdd<AIBrain>(gameObject);

                if (addMissingCoreCombatComponents)
                {
                    EnsureCoreCombatComponents(animator);
                }

                Character character = GetOrAdd<Character>(gameObject);
                CharacterOrientation2D orientation = GetOrAdd<CharacterOrientation2D>(gameObject);
                Health health = GetOrAdd<Health>(gameObject);
                AttackTelegraph2DView telegraphView = GetOrAdd<AttackTelegraph2DView>(gameObject);
                BossIntroSequenceController introSequenceController = GetComponent<BossIntroSequenceController>();
                BerthaBrainAnimationController brainAnimationController = GetOrAdd<BerthaBrainAnimationController>(gameObject);
                brainAnimationController.Configure(brain, animator, introSequenceController);

                ConfigureAreaAttackController(
                    GetOrAdd<BerthaLightAttack1Controller>(gameObject),
                    brain,
                    character,
                    orientation,
                    animator,
                    telegraphView,
                    LightTelegraphStateName,
                    LightAttack1StateName,
                    "LightAtk1",
                    LightAttack1TelegraphColor,
                    attackOffset,
                    attackSize,
                    attackDamage,
                    attackInvincibilityDuration,
                    "BerthaLightAttack1");

                ConfigureAreaAttackController(
                    GetOrAdd<BerthaLightAttack2Controller>(gameObject),
                    brain,
                    character,
                    orientation,
                    animator,
                    telegraphView,
                    Light2TelegraphStateName,
                    LightAttack2StateName,
                    "LightAtk2",
                    LightAttack2TelegraphColor,
                    lightAttack2Offset,
                    lightAttack2Size,
                    lightAttack2Damage,
                    lightAttack2InvincibilityDuration,
                    "BerthaLightAttack2");

                ConfigureAreaAttackController(
                    GetOrAdd<BerthaHeavyAttackController>(gameObject),
                    brain,
                    character,
                    orientation,
                    animator,
                    telegraphView,
                    HeavyTelegraphStateName,
                    HeavyAttackStateName,
                    "HeavyAtk",
                    HeavyTelegraphColor,
                    heavyAttackOffset,
                    heavyAttackSize,
                    heavyAttackDamage,
                    heavyAttackInvincibilityDuration,
                    "BerthaHeavyAttack");

                ConfigureAreaAttackController(
                    GetOrAdd<BerthaFullComboController>(gameObject),
                    brain,
                    character,
                    orientation,
                    animator,
                    telegraphView,
                    FullTelegraphStateName,
                    FullComboAttackStateName,
                    "FullCombo",
                    FullComboTelegraphColor,
                    fullComboOffset,
                    fullComboSize,
                    fullComboDamage,
                    fullComboInvincibilityDuration,
                    "BerthaFullCombo");

                BerthaDashAttackController dashAttackController = GetOrAdd<BerthaDashAttackController>(gameObject);

                CharacterDamageDash2D dashAbility = GetOrAdd<CharacterDamageDash2D>(gameObject);
                dashAbility.DashDistance = dashRange;
                dashAbility.DashDuration = dashDuration;
                dashAbility.Cooldown.ConsumptionDuration = dashDuration;
                dashAbility.Cooldown.PauseOnEmptyDuration = 0f;
                dashAbility.Cooldown.RefillDuration = 0.01f;
                dashAbility.Cooldown.CanInterruptRefill = true;
                dashAbility.InvincibleWhileDashing = false;
                dashAttackController.Configure(
                    brain,
                    character,
                    orientation,
                    dashAbility,
                    animator,
                    DashChargeStateName,
                    "DashAtk",
                    0,
                    ResolveTargetLayerMask(),
                    dashAttackOffset,
                    dashAttackSize,
                    dashDamage,
                    dashHitInvincibilityDuration,
                    "BerthaDashAttack");

                AIActionDoNothing idleAction = GetOrAdd<AIActionDoNothing>(gameObject);
                idleAction.Label = "BerthaIdle";

                AIActionMoveTowardsTarget2D moveAction = GetOrAdd<AIActionMoveTowardsTarget2D>(gameObject);
                moveAction.Label = "BerthaChase";
                moveAction.UseMinimumXDistance = true;
                moveAction.MinimumXDistance = minimumMoveDistance;

                AIActionFaceTowardsTarget2D faceAction = GetOrAdd<AIActionFaceTowardsTarget2D>(gameObject);
                faceAction.Label = "BerthaFaceTarget";
                faceAction.Mode = AIActionFaceTowardsTarget2D.Modes.LeftRight;

                BerthaDashStartAction dashStartAction = GetOrAdd<BerthaDashStartAction>(gameObject);
                dashStartAction.Label = "BerthaChargeDashOnce";
                dashStartAction.Configure(dashAbility);

                AIActionDash dashAction = GetComponent<AIActionDash>();
                if (dashAction != null)
                {
                    dashAction.Label = "BerthaChargeDashLegacy";
                    dashAction.Mode = AIActionDash.Modes.None;
                }

                BerthaDashHitGate dashHitGate = EnsureChargeDamageArea();
                dashAbility.TargetDamageOnTouch = dashHitGate;

                AIBrainDashTelegraphDriver dashTelegraphDriver = GetOrAdd<AIBrainDashTelegraphDriver>(gameObject);
                dashTelegraphDriver.Configure(
                    brain,
                    character,
                    dashAbility,
                    orientation,
                    animator,
                    dashAction,
                    dashHitGate.GetComponent<BoxCollider2D>(),
                    transform,
                    telegraphView,
                    DashTelegraphStateName,
                    DashChargeStateName,
                    false,
                    false,
                    "Idle",
                    "Idle",
                    "Walk");

                BerthaCombatPatternSelector patternSelector = GetOrAdd<BerthaCombatPatternSelector>(gameObject);
                patternSelector.Configure(
                    brain,
                    character,
                    health,
                    dashAbility,
                    attackRange,
                    lightAttack2Range,
                    heavyAttackRange,
                    dashMinimumRange,
                    dashRange,
                    fullComboRange,
                    specialPatternHealthThresholdNormalized,
                    specialPatternCooldown,
                    specialPatternCooldown,
                    lightAttack1Weight,
                    lightAttack2Weight,
                    heavyAttackWeight);
                patternSelector.enabled = enableAttackPattern;

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

                AIDecisionTimeInState lightTelegraphTimer = GetOrAddTimeDecision("LightTelegraphDuration");
                lightTelegraphTimer.AfterTimeMin = telegraphDuration;
                lightTelegraphTimer.AfterTimeMax = telegraphDuration;

                AIDecisionTimeInState lightAttackTimer = GetOrAddTimeDecision("LightAttack1Duration");
                lightAttackTimer.AfterTimeMin = attackDuration;
                lightAttackTimer.AfterTimeMax = attackDuration;

                AIDecisionTimeInState lightRecoverTimer = GetOrAddTimeDecision("LightRecoverDuration");
                lightRecoverTimer.AfterTimeMin = recoverDuration;
                lightRecoverTimer.AfterTimeMax = recoverDuration;

                AIDecisionTimeInState light2TelegraphTimer = GetOrAddTimeDecision("Light2TelegraphDuration");
                light2TelegraphTimer.AfterTimeMin = lightAttack2TelegraphDuration;
                light2TelegraphTimer.AfterTimeMax = lightAttack2TelegraphDuration;

                AIDecisionTimeInState light2AttackTimer = GetOrAddTimeDecision("LightAttack2Duration");
                light2AttackTimer.AfterTimeMin = lightAttack2Duration;
                light2AttackTimer.AfterTimeMax = lightAttack2Duration;

                AIDecisionTimeInState light2RecoverTimer = GetOrAddTimeDecision("Light2RecoverDuration");
                light2RecoverTimer.AfterTimeMin = lightAttack2RecoverDuration;
                light2RecoverTimer.AfterTimeMax = lightAttack2RecoverDuration;

                AIDecisionTimeInState heavyTelegraphTimer = GetOrAddTimeDecision("HeavyTelegraphDuration");
                heavyTelegraphTimer.AfterTimeMin = heavyTelegraphDuration;
                heavyTelegraphTimer.AfterTimeMax = heavyTelegraphDuration;

                AIDecisionTimeInState heavyAttackTimer = GetOrAddTimeDecision("HeavyAttackDuration");
                heavyAttackTimer.AfterTimeMin = heavyAttackDuration;
                heavyAttackTimer.AfterTimeMax = heavyAttackDuration;

                AIDecisionTimeInState heavyRecoverTimer = GetOrAddTimeDecision("HeavyRecoverDuration");
                heavyRecoverTimer.AfterTimeMin = heavyRecoverDuration;
                heavyRecoverTimer.AfterTimeMax = heavyRecoverDuration;

                AIDecisionTimeInState dashTelegraphTimer = GetOrAddTimeDecision("DashTelegraphDuration");
                dashTelegraphTimer.AfterTimeMin = dashTelegraphDuration;
                dashTelegraphTimer.AfterTimeMax = dashTelegraphDuration;

                AIDecisionTimeInState dashChargeTimer = GetOrAddTimeDecision("DashChargeDuration");
                dashChargeTimer.AfterTimeMin = dashAttackDuration;
                dashChargeTimer.AfterTimeMax = dashAttackDuration;

                AIDecisionTimeInState dashRecoverTimer = GetOrAddTimeDecision("DashRecoverDuration");
                dashRecoverTimer.AfterTimeMin = dashRecoverDuration;
                dashRecoverTimer.AfterTimeMax = dashRecoverDuration;

                AIDecisionTimeInState fullTelegraphTimer = GetOrAddTimeDecision("FullTelegraphDuration");
                fullTelegraphTimer.AfterTimeMin = fullComboTelegraphDuration;
                fullTelegraphTimer.AfterTimeMax = fullComboTelegraphDuration;

                AIDecisionTimeInState fullAttackTimer = GetOrAddTimeDecision("FullComboDuration");
                fullAttackTimer.AfterTimeMin = fullComboDuration;
                fullAttackTimer.AfterTimeMax = fullComboDuration;

                AIDecisionTimeInState fullRecoverTimer = GetOrAddTimeDecision("FullRecoverDuration");
                fullRecoverTimer.AfterTimeMin = fullComboRecoverDuration;
                fullRecoverTimer.AfterTimeMax = fullComboRecoverDuration;

                brain.States = BuildStates(
                    idleAction,
                    moveAction,
                    faceAction,
                    dashStartAction,
                    detectTarget,
                    targetIsAlive,
                    lightTelegraphTimer,
                    lightAttackTimer,
                    lightRecoverTimer,
                    light2TelegraphTimer,
                    light2AttackTimer,
                    light2RecoverTimer,
                    heavyTelegraphTimer,
                    heavyAttackTimer,
                    heavyRecoverTimer,
                    dashTelegraphTimer,
                    dashChargeTimer,
                    dashRecoverTimer,
                    fullTelegraphTimer,
                    fullAttackTimer,
                    fullRecoverTimer);

                for (int i = 0; i < brain.States.Count; i++)
                {
                    brain.States[i].SetBrain(brain);
                }

                Log("Bertha combat pattern synchronized.");
            }
            finally
            {
                _isConfiguring = false;
            }
        }

        private void NormalizeCombatProfile()
        {
            if (Approximately(attackOffset, LegacyAttackOffset) && Approximately(attackSize, LegacyAttackSize))
            {
                attackOffset = RequestedAttackOffset;
                attackSize = RequestedAttackSize;
            }

            if (Mathf.Approximately(attackRange, LegacyAttackRange))
            {
                attackRange = RequestedAttackRange;
            }

            if (Mathf.Approximately(heavyAttackDuration, LegacyHeavyAttackDuration))
            {
                heavyAttackDuration = RequestedHeavyAttackDuration;
            }

            lightAttack1Weight = Mathf.Max(0, lightAttack1Weight);
            lightAttack2Weight = Mathf.Max(0, lightAttack2Weight);
            heavyAttackWeight = Mathf.Max(0, heavyAttackWeight);
            dashMinimumRange = Mathf.Max(0f, dashMinimumRange);
            dashRange = Mathf.Max(dashMinimumRange, dashRange);
            dashAttackDuration = Mathf.Max(dashDuration, dashAttackDuration);
            specialPatternHealthThresholdNormalized = Mathf.Clamp01(specialPatternHealthThresholdNormalized);
        }

        private void ConfigureAreaAttackController(
            BerthaAreaAttackController controller,
            AIBrain brain,
            Character character,
            CharacterOrientation2D orientation,
            Animator animator,
            AttackTelegraph2DView telegraphView,
            string telegraphStateName,
            string attackStateName,
            string attackAnimationStateName,
            Color telegraphColor,
            Vector2 configuredAttackOffset,
            Vector2 configuredAttackSize,
            float configuredDamage,
            float configuredInvincibilityDuration,
            string debugName)
        {
            controller.Configure(
                brain,
                character,
                orientation,
                animator,
                transform,
                telegraphView,
                telegraphStateName,
                attackStateName,
                attackAnimationStateName,
                0,
                telegraphColor,
                ResolveTargetLayerMask(),
                configuredAttackOffset,
                configuredAttackSize,
                configuredDamage,
                configuredInvincibilityDuration,
                debugName);
        }

        private void EnsureCoreCombatComponents(Animator animator)
        {
            Rigidbody2D body = GetOrAdd<Rigidbody2D>(gameObject);
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
            health.DisableControllerOnDeath = true;
            health.DisableModelOnDeath = true;
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
            hitGate.TargetLayerMask = 0;
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

        private List<AIState> BuildStates(
            AIActionDoNothing idleAction,
            AIActionMoveTowardsTarget2D moveAction,
            AIActionFaceTowardsTarget2D faceAction,
            AIAction dashStartAction,
            AIDecisionDetectTargetRadius2D detectTarget,
            AIDecisionTargetIsAlive targetIsAlive,
            AIDecisionTimeInState lightTelegraphTimer,
            AIDecisionTimeInState lightAttackTimer,
            AIDecisionTimeInState lightRecoverTimer,
            AIDecisionTimeInState light2TelegraphTimer,
            AIDecisionTimeInState light2AttackTimer,
            AIDecisionTimeInState light2RecoverTimer,
            AIDecisionTimeInState heavyTelegraphTimer,
            AIDecisionTimeInState heavyAttackTimer,
            AIDecisionTimeInState heavyRecoverTimer,
            AIDecisionTimeInState dashTelegraphTimer,
            AIDecisionTimeInState dashChargeTimer,
            AIDecisionTimeInState dashRecoverTimer,
            AIDecisionTimeInState fullTelegraphTimer,
            AIDecisionTimeInState fullAttackTimer,
            AIDecisionTimeInState fullRecoverTimer)
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
                        CreateTransition(targetIsAlive, string.Empty, DetectingStateName)
                    }),
                CreateState(
                    LightTelegraphStateName,
                    new AIAction[] { idleAction },
                    new[]
                    {
                        CreateTransition(lightTelegraphTimer, LightAttack1StateName, string.Empty)
                    }),
                CreateState(
                    LightAttack1StateName,
                    new AIAction[] { idleAction },
                    new[]
                    {
                        CreateTransition(lightAttackTimer, LightRecoverStateName, string.Empty)
                    }),
                CreateRecoverState(LightRecoverStateName, idleAction, targetIsAlive, lightRecoverTimer),
                CreateState(
                    Light2TelegraphStateName,
                    new AIAction[] { idleAction },
                    new[]
                    {
                        CreateTransition(light2TelegraphTimer, LightAttack2StateName, string.Empty)
                    }),
                CreateState(
                    LightAttack2StateName,
                    new AIAction[] { idleAction },
                    new[]
                    {
                        CreateTransition(light2AttackTimer, Light2RecoverStateName, string.Empty)
                    }),
                CreateRecoverState(Light2RecoverStateName, idleAction, targetIsAlive, light2RecoverTimer),
                CreateState(
                    HeavyTelegraphStateName,
                    new AIAction[] { idleAction },
                    new[]
                    {
                        CreateTransition(heavyTelegraphTimer, HeavyAttackStateName, string.Empty)
                    }),
                CreateState(
                    HeavyAttackStateName,
                    new AIAction[] { idleAction },
                    new[]
                    {
                        CreateTransition(heavyAttackTimer, HeavyRecoverStateName, string.Empty)
                    }),
                CreateRecoverState(HeavyRecoverStateName, idleAction, targetIsAlive, heavyRecoverTimer),
                CreateState(
                    DashTelegraphStateName,
                    new AIAction[] { idleAction },
                    new[]
                    {
                        CreateTransition(dashTelegraphTimer, DashChargeStateName, string.Empty)
                    }),
                CreateState(
                    DashChargeStateName,
                    new[] { dashStartAction },
                    new[]
                    {
                        CreateTransition(dashChargeTimer, DashRecoverStateName, string.Empty)
                    }),
                CreateRecoverState(DashRecoverStateName, idleAction, targetIsAlive, dashRecoverTimer),
                CreateState(
                    FullTelegraphStateName,
                    new AIAction[] { idleAction },
                    new[]
                    {
                        CreateTransition(fullTelegraphTimer, FullComboAttackStateName, string.Empty)
                    }),
                CreateState(
                    FullComboAttackStateName,
                    new AIAction[] { idleAction },
                    new[]
                    {
                        CreateTransition(fullAttackTimer, FullRecoverStateName, string.Empty)
                    }),
                CreateRecoverState(FullRecoverStateName, idleAction, targetIsAlive, fullRecoverTimer)
            };
        }

        private AIState CreateRecoverState(
            string stateName,
            AIActionDoNothing idleAction,
            AIDecisionTargetIsAlive targetIsAlive,
            AIDecisionTimeInState recoverTimer)
        {
            return CreateState(
                stateName,
                new AIAction[] { idleAction },
                new[]
                {
                    CreateTransition(targetIsAlive, string.Empty, DetectingStateName),
                    CreateTransition(recoverTimer, MovingStateName, string.Empty)
                });
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

        private static Transform FindOrCreateChild(string childName, Transform parent)
        {
            if (parent == null)
            {
                return null;
            }

            Transform child = parent.Find(childName);
            if (child != null)
            {
                return child;
            }

            GameObject childObject = new GameObject(childName);
            child = childObject.transform;
            child.SetParent(parent, false);
            return child;
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

        private static bool Approximately(Vector2 left, Vector2 right)
        {
            return Mathf.Approximately(left.x, right.x)
                && Mathf.Approximately(left.y, right.y);
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[BerthaCombatBootstrap] " + message, this);
            }
        }
    }
}
