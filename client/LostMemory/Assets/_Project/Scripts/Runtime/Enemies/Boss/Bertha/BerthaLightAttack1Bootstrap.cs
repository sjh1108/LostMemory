using System.Collections.Generic;
using LostMemory.Combat.Telegraph;
using LostMemory.Enemies;
using LostMemory.Stage;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LostMemory.Enemies.Boss.Bertha
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Boss/Bertha/Bertha Light Attack 1 Bootstrap")]
    public sealed class BerthaLightAttack1Bootstrap : MonoBehaviour
    {
        private static readonly HashSet<BerthaLightAttack1Bootstrap> ActiveBootstraps =
            new HashSet<BerthaLightAttack1Bootstrap>();

        private static readonly Vector2 LegacyAttackOffset = new Vector2(1.3f, 0f);
        private static readonly Vector2 LegacyAttackSize = new Vector2(2.4f, 1.3f);
        private static readonly Vector2 RequestedAttackOffset = Vector2.zero;
        private static readonly Vector2 RequestedAttackSize = new Vector2(5f, 5f);
        private static readonly Color LightAttack1TelegraphColor = new Color(1f, 0.34f, 0.08f, 0.32f);
        private static readonly Color LightAttack2TelegraphColor = new Color(1f, 0.44f, 0.12f, 0.32f);
        private static readonly Color HeavyTelegraphColor = new Color(1f, 0.16f, 0.05f, 0.38f);
        private static readonly Color FullComboTelegraphColor = new Color(1f, 0.12f, 0.05f, 0.42f);
        private static readonly Color LegacyHitFlashColor = new Color(1f, 0.45f, 0.45f, 1f);
        private static readonly Color RequestedHitFlashColor = new Color(1f, 0.25f, 0.25f, 1f);

        private const float LegacyAttackRange = 2f;
        private const float RequestedAttackRange = 2.5f;
        private const float LegacyHeavyAttackDuration = 1.9f;
        private const float RequestedHeavyAttackDuration = 22f / 12f;
        private const float LightAttack1DefaultImpactTime = 0.85f;
        private const float LightAttack2DefaultImpactTime = 0.55f;
        private const float HeavyAttackDefaultImpactTime = RequestedHeavyAttackDuration;
        private const float NormalDashAnimationDuration = 8f / 12f;
        private const float DashAttackAnimationDuration = 27f / 12f;
        private const float DashAttackImpactTime = 26f / 12f;
        private const float FullComboDefaultImpactTime = 3.2f;
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
        private const string NormalDashTelegraphStateName = "NormalDashTelegraph";
        private const string NormalDashChargeStateName = "NormalDashCharge";
        private const string NormalDashRecoverStateName = "NormalDashRecover";
        private const string DashTelegraphStateName = "DashTelegraph";
        private const string DashChargeStateName = "DashCharge";
        private const string DashRecoverStateName = "DashRecover";
        private const string FullTelegraphStateName = "FullTelegraph";
        private const string FullComboAttackStateName = "FullComboAttack";
        private const string FullRecoverStateName = "FullRecover";
        private const string ComboProjectileFxFolder = "Assets/_Project/Art/Enemies/Boss/1_Bertha/Attacks/ComboAtk/FX";

        [Header("Scene Roots")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform combatRoot;

        [Header("Editor Sync")]
        [SerializeField] private bool autoConfigureInEditMode = true;
        [SerializeField] private bool addMissingCoreCombatComponents = true;

        [Header("Balance Data")]
        [SerializeField] private BossData bossData;

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
        [SerializeField] private float attackImpactTime = LightAttack1DefaultImpactTime;
        [SerializeField] private float recoverDuration = 0.35f;
        [SerializeField] private Vector2 attackOffset = Vector2.zero;
        [SerializeField] private Vector2 attackSize = new Vector2(5f, 5f);
        [SerializeField] private float attackDamage = 8f;
        [SerializeField] private float attackInvincibilityDuration = 0.5f;

        [Header("Light Attack 2")]
        [SerializeField] private float lightAttack2Range = 2.5f;
        [SerializeField] private float lightAttack2TelegraphDuration = 0.5f;
        [SerializeField] private float lightAttack2Duration = 0.55f;
        [SerializeField] private float lightAttack2ImpactTime = LightAttack2DefaultImpactTime;
        [SerializeField] private float lightAttack2RecoverDuration = 0.3f;
        [SerializeField] private Vector2 lightAttack2Offset = Vector2.zero;
        [SerializeField] private Vector2 lightAttack2Size = new Vector2(5f, 5f);
        [SerializeField] private float lightAttack2Damage = 10f;
        [SerializeField] private float lightAttack2InvincibilityDuration = 0.5f;

        [Header("Heavy Attack")]
        [SerializeField] private float heavyAttackRange = 3.25f;
        [SerializeField] private float heavyTelegraphDuration = 0.9f;
        [SerializeField] private float heavyAttackDuration = RequestedHeavyAttackDuration;
        [SerializeField] private float heavyAttackImpactTime = HeavyAttackDefaultImpactTime;
        [SerializeField] private float heavyRecoverDuration = 0.55f;
        [SerializeField] private Vector2 heavyAttackOffset = Vector2.zero;
        [SerializeField] private Vector2 heavyAttackSize = new Vector2(6.5f, 6.5f);
        [SerializeField] private float heavyAttackDamage = 16f;
        [SerializeField] private float heavyAttackInvincibilityDuration = 0.5f;

        [Header("Normal Dash")]
        [SerializeField] private float normalDashMinimumRange = 4.5f;
        [SerializeField] private float normalDashRange = 15f;
        [SerializeField] private float normalDashDuration = NormalDashAnimationDuration;
        [SerializeField] private float normalDashTelegraphDuration = 0.35f;
        [SerializeField] private float normalDashRecoverDuration = 0.35f;
        [SerializeField] private float normalDashCooldown = 10f;

        [Header("Dash Attack")]
        [SerializeField] private float dashMinimumRange = 3f;
        [SerializeField] private float dashRange = 4.5f;
        [SerializeField] private float dashDuration = 0.32f;
        [SerializeField] private float dashAttackDuration = DashAttackAnimationDuration;
        [SerializeField] private float dashImpactTime = DashAttackImpactTime;
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
        [SerializeField] private float fullComboImpactTime = FullComboDefaultImpactTime;
        [SerializeField] private float fullComboRecoverDuration = 0.65f;
        [SerializeField] private Vector2 fullComboOffset = Vector2.zero;
        [SerializeField] private Vector2 fullComboSize = new Vector2(7f, 7f);
        [SerializeField] private float fullComboDamage = 24f;
        [SerializeField] private float fullComboInvincibilityDuration = 0.5f;

        [Header("Projectile Burst")]
        [SerializeField] private bool autoAssignProjectileFxFramesInEditMode = true;
        [SerializeField] private Sprite[] comboFxProjectileFrames = System.Array.Empty<Sprite>();
        [SerializeField, Min(0f)] private float projectileAnimationFrameRate = 12f;
        [SerializeField, Min(1)] private int projectileMaximumHits = 8;
        [SerializeField] private bool destroyProjectileOnHit = true;
        [SerializeField] private bool faceProjectileDirection = true;
        [SerializeField] private int projectileSortingOrderOffset = 1;
        [SerializeField] private BerthaProjectilePatternDriver.BurstInstruction[] heavyProjectileBursts =
        {
            new BerthaProjectilePatternDriver.BurstInstruction
            {
                Mode = BerthaProjectilePatternDriver.BurstPatternMode.Radial,
                Delay = 0.82f,
                ProjectileCount = 8,
                SpreadAngle = 0f,
                Speed = 6.25f,
                Lifetime = 1.15f,
                Damage = 8f,
                TargetInvincibilityDuration = 0.5f,
                HitRadius = 0.35f,
                AngleOffsetDegrees = 0f,
                SpawnDistance = 0.45f
            }
        };
        [SerializeField] private BerthaProjectilePatternDriver.BurstInstruction[] fullComboProjectileBursts =
        {
            new BerthaProjectilePatternDriver.BurstInstruction
            {
                Mode = BerthaProjectilePatternDriver.BurstPatternMode.Fan,
                Delay = 0.58f,
                ProjectileCount = 5,
                SpreadAngle = 70f,
                Speed = 6f,
                Lifetime = 1.1f,
                Damage = 7f,
                TargetInvincibilityDuration = 0.5f,
                HitRadius = 0.35f,
                AngleOffsetDegrees = 0f,
                SpawnDistance = 0.45f
            },
            new BerthaProjectilePatternDriver.BurstInstruction
            {
                Mode = BerthaProjectilePatternDriver.BurstPatternMode.Fan,
                Delay = 0.72f,
                ProjectileCount = 6,
                SpreadAngle = 95f,
                Speed = 6.2f,
                Lifetime = 1.15f,
                Damage = 7f,
                TargetInvincibilityDuration = 0.5f,
                HitRadius = 0.35f,
                AngleOffsetDegrees = 0f,
                SpawnDistance = 0.45f
            },
            new BerthaProjectilePatternDriver.BurstInstruction
            {
                Mode = BerthaProjectilePatternDriver.BurstPatternMode.Radial,
                Delay = 0.78f,
                ProjectileCount = 8,
                SpreadAngle = 0f,
                Speed = 6.5f,
                Lifetime = 1.2f,
                Damage = 9f,
                TargetInvincibilityDuration = 0.5f,
                HitRadius = 0.35f,
                AngleOffsetDegrees = 0f,
                SpawnDistance = 0.45f
            }
        };

        [Header("Basic Pattern Weights")]
        [SerializeField, Min(0)] private int lightAttack1Weight = 3;
        [SerializeField, Min(0)] private int lightAttack2Weight = 3;
        [SerializeField, Min(0)] private int heavyAttackWeight = 2;
        [SerializeField, Min(0)] private int normalDashWeight = 1;

        [Header("Health")]
        [SerializeField] private float initialHealth = 100f;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float movementSpeed = 3f;

        [Header("Health Threshold Reactions")]
        [SerializeField, Range(0f, 1f)] private float stunThresholdNormalized = 0.7f;
        [SerializeField, Range(0f, 1f)] private float tiredThresholdNormalized = 0.3f;
        [SerializeField, Min(0f)] private float stunReactionDuration = 1.1f;
        [SerializeField, Min(0f)] private float stunShakeHeadDuration = 5f;
        [SerializeField, Min(0f)] private float tiredReactionDuration = 1.35f;

        [Header("Hit Feedback")]
        [SerializeField] private Color hitFlashColor = new Color(1f, 0.25f, 0.25f, 1f);
        [SerializeField, Range(0f, 1f)] private float hitFlashStrength = 0.75f;
        [SerializeField, Min(0f)] private float hitFlashDuration = 0.14f;
        [SerializeField, Min(0f)] private float hitShakeDuration = 0.12f;
        [SerializeField, Min(0f)] private float hitShakeMagnitude = 0.12f;
        [SerializeField, Min(1f)] private float hitScaleMultiplier = 1.1f;
        [SerializeField, Min(0f)] private float hitScaleDuration = 0.12f;
        [SerializeField, Min(1f)] private float dashHitReactionMultiplier = 1.6f;

        [Header("Debug")]
        [SerializeField] private bool debugLogging;

        private bool _isConfiguring;

        private void Reset()
        {
            EnsureConfigured();
        }

        private void OnEnable()
        {
            ActiveBootstraps.Add(this);

            if (Application.isPlaying || autoConfigureInEditMode)
            {
                EnsureConfigured();
            }
        }

        private void OnDisable()
        {
            ActiveBootstraps.Remove(this);
        }

        public static void ApplySavedBossData(BossData savedAsset)
        {
            if (savedAsset == null || ActiveBootstraps.Count == 0)
            {
                return;
            }

            List<BerthaLightAttack1Bootstrap> bootstraps = new List<BerthaLightAttack1Bootstrap>(ActiveBootstraps);
            for (int i = 0; i < bootstraps.Count; i++)
            {
                BerthaLightAttack1Bootstrap bootstrap = bootstraps[i];
                if (bootstrap == null || bootstrap.bossData != savedAsset)
                {
                    continue;
                }

                bootstrap.ApplyRuntimeTuning();
            }
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            TryAutoAssignProjectileFramesInEditor();
#endif
            NormalizeCombatProfile();

            if (Application.isPlaying)
            {
                ApplyRuntimeTuning();
            }
            else if (autoConfigureInEditMode)
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
#if UNITY_EDITOR
                TryAutoAssignProjectileFramesInEditor();
#endif
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
                BerthaBossPhaseController phaseController = GetOrAdd<BerthaBossPhaseController>(gameObject);
                BerthaHitReactionPresenter hitReactionPresenter = GetOrAdd<BerthaHitReactionPresenter>(gameObject);
                BerthaHealthThresholdReactionController thresholdReactionController = GetOrAdd<BerthaHealthThresholdReactionController>(gameObject);
                AttackTelegraph2DView telegraphView = GetOrAdd<AttackTelegraph2DView>(gameObject);
                BossIntroSequenceController introSequenceController = GetComponent<BossIntroSequenceController>();
                BerthaBrainAnimationController brainAnimationController = GetOrAdd<BerthaBrainAnimationController>(gameObject);
                Transform projectileRoot = FindOrCreateChild("ProjectileRoot", combatRoot);
                Transform projectileSpawnOrigin = FindOrCreateChild("ProjectileSpawnOrigin", combatRoot);
                projectileRoot.localPosition = Vector3.zero;
                projectileRoot.localRotation = Quaternion.identity;
                projectileRoot.localScale = Vector3.one;
                projectileSpawnOrigin.localPosition = Vector3.zero;
                projectileSpawnOrigin.localRotation = Quaternion.identity;
                projectileSpawnOrigin.localScale = Vector3.one;
                brainAnimationController.Configure(brain, animator, introSequenceController);
                hitReactionPresenter.Configure(
                    health,
                    character,
                    ResolveHitReactionShakeTarget(),
                    ResolveHitReactionRenderers(),
                    hitFlashColor,
                    hitFlashStrength,
                    hitFlashDuration,
                    hitShakeDuration,
                    hitShakeMagnitude,
                    hitScaleMultiplier,
                    hitScaleDuration,
                    dashHitReactionMultiplier,
                    debugLogging);

                BerthaProjectileBurstEmitter projectileBurstEmitter = GetOrAdd<BerthaProjectileBurstEmitter>(gameObject);
                projectileBurstEmitter.Configure(
                    projectileRoot,
                    projectileSpawnOrigin,
                    ResolveProjectileSortingReference(),
                    ResolveTargetLayerMask(),
                    ResolveObstacleLayerMask(),
                    comboFxProjectileFrames,
                    projectileAnimationFrameRate,
                    projectileMaximumHits,
                    destroyProjectileOnHit,
                    faceProjectileDirection,
                    projectileSortingOrderOffset);

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
                    attackImpactTime,
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
                    lightAttack2ImpactTime,
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
                    heavyAttackImpactTime,
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
                    fullComboImpactTime,
                    "BerthaFullCombo");

                BerthaProjectilePatternDriver heavyProjectileDriver = GetOrCreateProjectilePatternDriver("HeavyProjectiles");
                heavyProjectileDriver.Configure(
                    "HeavyProjectiles",
                    brain,
                    character,
                    orientation,
                    projectileSpawnOrigin,
                    projectileBurstEmitter,
                    HeavyAttackStateName,
                    heavyProjectileBursts,
                    debugLogging);

                BerthaProjectilePatternDriver fullComboProjectileDriver = GetOrCreateProjectilePatternDriver("FullComboProjectiles");
                fullComboProjectileDriver.Configure(
                    "FullComboProjectiles",
                    brain,
                    character,
                    orientation,
                    projectileSpawnOrigin,
                    projectileBurstEmitter,
                    FullComboAttackStateName,
                    fullComboProjectileBursts,
                    debugLogging);

                BerthaDashAttackController dashAttackController = GetOrAdd<BerthaDashAttackController>(gameObject);

                CharacterDamageDash2D dashAbility = GetOrAdd<CharacterDamageDash2D>(gameObject);
                dashAbility.DashDistance = dashRange;
                dashAbility.DashDuration = dashDuration;
                dashAbility.Cooldown.ConsumptionDuration = dashDuration;
                dashAbility.Cooldown.PauseOnEmptyDuration = 0f;
                dashAbility.Cooldown.RefillDuration = 0.01f;
                dashAbility.Cooldown.CanInterruptRefill = true;
                dashAbility.InvincibleWhileDashing = false;

                ResolvePhaseThresholds(
                    out float resolvedPhase2ThresholdNormalized,
                    out float resolvedPhase3ThresholdNormalized);

                phaseController.Configure(
                    health,
                    resolvedPhase2ThresholdNormalized,
                    resolvedPhase3ThresholdNormalized,
                    debugLogging);
                thresholdReactionController.Configure(
                    brain,
                    character,
                    health,
                    dashAbility,
                    animator,
                    introSequenceController,
                    resolvedPhase2ThresholdNormalized,
                    resolvedPhase3ThresholdNormalized,
                    stunReactionDuration,
                    stunShakeHeadDuration,
                    tiredReactionDuration,
                    debugLogging);
                dashAttackController.Configure(
                    brain,
                    character,
                    orientation,
                    dashAbility,
                    animator,
                    DashChargeStateName,
                    "DashAtk",
                    0,
                    dashImpactTime,
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

                BerthaDashStartAction normalDashStartAction = GetOrAddDashStartAction("BerthaNormalDashOnce");
                normalDashStartAction.Label = "BerthaNormalDashOnce";
                normalDashStartAction.Configure(dashAbility, true, normalDashDuration);

                BerthaDashStartAction dashAttackStartAction = GetOrAddDashStartAction("BerthaChargeDashOnce");
                dashAttackStartAction.Label = "BerthaChargeDashOnce";
                dashAttackStartAction.Configure(dashAbility, true, dashDuration);

                AIActionDash dashAction = GetComponent<AIActionDash>();
                if (dashAction != null)
                {
                    dashAction.Label = "BerthaChargeDashLegacy";
                    dashAction.Mode = AIActionDash.Modes.None;
                }

                BerthaDashHitGate dashHitGate = EnsureChargeDamageArea();
                dashAbility.TargetDamageOnTouch = dashHitGate;

                AIBrainDashTelegraphDriver normalDashTelegraphDriver = GetOrAddDashTelegraphDriver("NormalDash");
                normalDashTelegraphDriver.Configure(
                    brain,
                    character,
                    dashAbility,
                    orientation,
                    animator,
                    dashAction,
                    dashHitGate.GetComponent<BoxCollider2D>(),
                    transform,
                    telegraphView,
                    NormalDashTelegraphStateName,
                    NormalDashChargeStateName,
                    false,
                    false,
                    "Idle",
                    "Idle",
                    "Walk",
                    0,
                    true,
                    "Dash",
                    0,
                    "NormalDash");

                AIBrainDashTelegraphDriver dashTelegraphDriver = GetOrAddDashTelegraphDriver("DashAttack");
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
                    "Walk",
                    0,
                    false,
                    "Dash",
                    0,
                    "DashAttack");

                BerthaCombatPatternSelector patternSelector = GetOrAdd<BerthaCombatPatternSelector>(gameObject);
                patternSelector.Configure(
                    brain,
                    character,
                    health,
                    dashAbility,
                    phaseController,
                    attackRange,
                    lightAttack2Range,
                    heavyAttackRange,
                    normalDashMinimumRange,
                    normalDashRange,
                    dashMinimumRange,
                    dashRange,
                    fullComboRange,
                    specialPatternHealthThresholdNormalized,
                    normalDashCooldown,
                    specialPatternCooldown,
                    specialPatternCooldown,
                    lightAttack1Weight,
                    lightAttack2Weight,
                    heavyAttackWeight,
                    normalDashWeight);
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

                AIDecisionTimeInState normalDashTelegraphTimer = GetOrAddTimeDecision("NormalDashTelegraphDuration");
                normalDashTelegraphTimer.AfterTimeMin = normalDashTelegraphDuration;
                normalDashTelegraphTimer.AfterTimeMax = normalDashTelegraphDuration;

                AIDecisionTimeInState normalDashChargeTimer = GetOrAddTimeDecision("NormalDashChargeDuration");
                normalDashChargeTimer.AfterTimeMin = normalDashDuration;
                normalDashChargeTimer.AfterTimeMax = normalDashDuration;

                AIDecisionTimeInState normalDashRecoverTimer = GetOrAddTimeDecision("NormalDashRecoverDuration");
                normalDashRecoverTimer.AfterTimeMin = normalDashRecoverDuration;
                normalDashRecoverTimer.AfterTimeMax = normalDashRecoverDuration;

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
                    normalDashStartAction,
                    dashAttackStartAction,
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
                    normalDashTelegraphTimer,
                    normalDashChargeTimer,
                    normalDashRecoverTimer,
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

            if (Approximately(hitFlashColor, LegacyHitFlashColor)
                && Mathf.Approximately(hitFlashStrength, 0.4f)
                && Mathf.Approximately(hitFlashDuration, 0.1f)
                && Mathf.Approximately(hitShakeDuration, 0.12f)
                && Mathf.Approximately(hitShakeMagnitude, 0.06f))
            {
                hitFlashColor = RequestedHitFlashColor;
                hitFlashStrength = 0.75f;
                hitFlashDuration = 0.14f;
                hitShakeDuration = 0.12f;
                hitShakeMagnitude = 0.12f;
            }

            lightAttack1Weight = Mathf.Max(0, lightAttack1Weight);
            lightAttack2Weight = Mathf.Max(0, lightAttack2Weight);
            heavyAttackWeight = Mathf.Max(0, heavyAttackWeight);
            normalDashWeight = Mathf.Max(0, normalDashWeight);
            attackImpactTime = Mathf.Clamp(attackImpactTime, 0f, attackDuration);
            lightAttack2ImpactTime = Mathf.Clamp(lightAttack2ImpactTime, 0f, lightAttack2Duration);
            heavyAttackImpactTime = Mathf.Clamp(heavyAttackImpactTime, 0f, heavyAttackDuration);
            normalDashMinimumRange = Mathf.Max(0f, normalDashMinimumRange);
            normalDashRange = Mathf.Max(normalDashMinimumRange, normalDashRange);
            normalDashDuration = Mathf.Max(0.01f, normalDashDuration);
            normalDashTelegraphDuration = Mathf.Max(0f, normalDashTelegraphDuration);
            normalDashRecoverDuration = Mathf.Max(0f, normalDashRecoverDuration);
            normalDashCooldown = Mathf.Max(0f, normalDashCooldown);
            dashMinimumRange = Mathf.Max(0f, dashMinimumRange);
            dashRange = Mathf.Max(dashMinimumRange, dashRange);
            dashDuration = Mathf.Max(0.01f, dashDuration);
            dashAttackDuration = Mathf.Max(dashDuration, dashAttackDuration);
            dashImpactTime = Mathf.Clamp(dashImpactTime, 0f, dashAttackDuration);
            fullComboImpactTime = Mathf.Clamp(fullComboImpactTime, 0f, fullComboDuration);
            specialPatternHealthThresholdNormalized = Mathf.Clamp01(specialPatternHealthThresholdNormalized);
            initialHealth = Mathf.Max(0f, initialHealth);
            movementSpeed = Mathf.Max(0f, movementSpeed);
            stunThresholdNormalized = Mathf.Clamp01(stunThresholdNormalized);
            tiredThresholdNormalized = Mathf.Clamp01(tiredThresholdNormalized);
            tiredThresholdNormalized = Mathf.Min(tiredThresholdNormalized, stunThresholdNormalized);
            stunReactionDuration = Mathf.Max(0f, stunReactionDuration);
            stunShakeHeadDuration = Mathf.Max(0f, stunShakeHeadDuration);
            tiredReactionDuration = Mathf.Max(0f, tiredReactionDuration);
            projectileAnimationFrameRate = Mathf.Max(0f, projectileAnimationFrameRate);
            projectileMaximumHits = Mathf.Max(1, projectileMaximumHits);
            hitFlashStrength = Mathf.Clamp01(hitFlashStrength);
            hitFlashDuration = Mathf.Max(0f, hitFlashDuration);
            hitShakeDuration = Mathf.Max(0f, hitShakeDuration);
            hitShakeMagnitude = Mathf.Max(0f, hitShakeMagnitude);
            hitScaleMultiplier = Mathf.Max(1f, hitScaleMultiplier);
            hitScaleDuration = Mathf.Max(0f, hitScaleDuration);
            dashHitReactionMultiplier = Mathf.Max(1f, dashHitReactionMultiplier);
            NormalizeBurstSequence(heavyProjectileBursts);
            NormalizeBurstSequence(fullComboProjectileBursts);
        }

        private void ApplyRuntimeTuning()
        {
            BerthaDashAttackController dashAttackController = GetComponent<BerthaDashAttackController>();
            if (dashAttackController != null)
            {
                dashAttackController.SetImpactTime(dashImpactTime);
            }

            GetComponent<BerthaLightAttack1Controller>()?.SetImpactTime(attackImpactTime);
            GetComponent<BerthaLightAttack2Controller>()?.SetImpactTime(lightAttack2ImpactTime);
            GetComponent<BerthaHeavyAttackController>()?.SetImpactTime(heavyAttackImpactTime);
            GetComponent<BerthaFullComboController>()?.SetImpactTime(fullComboImpactTime);

            Health health = GetComponent<Health>();
            ApplyResolvedHealth(health);

            CharacterMovement movement = GetComponent<CharacterMovement>();
            ApplyResolvedMovement(movement);

            ResolvePhaseThresholds(
                out float resolvedPhase2ThresholdNormalized,
                out float resolvedPhase3ThresholdNormalized);

            BerthaBossPhaseController phaseController = GetComponent<BerthaBossPhaseController>();
            if (phaseController != null)
            {
                phaseController.Configure(
                    health,
                    resolvedPhase2ThresholdNormalized,
                    resolvedPhase3ThresholdNormalized,
                    debugLogging);
            }

            BerthaHealthThresholdReactionController thresholdReactionController =
                GetComponent<BerthaHealthThresholdReactionController>();
            if (thresholdReactionController != null)
            {
                thresholdReactionController.Configure(
                    GetComponent<AIBrain>(),
                    GetComponent<Character>(),
                    health,
                    GetComponent<CharacterDash2D>(),
                    ResolveAnimator(),
                    GetComponent<BossIntroSequenceController>(),
                    resolvedPhase2ThresholdNormalized,
                    resolvedPhase3ThresholdNormalized,
                    stunReactionDuration,
                    stunShakeHeadDuration,
                    tiredReactionDuration,
                    debugLogging);
            }
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
            float configuredImpactTime,
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
                configuredImpactTime,
                debugName);
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
            ApplyResolvedHealth(health);
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
            ApplyResolvedMovement(movement);
            movement.Acceleration = 10f;
            movement.Deceleration = 10f;
            movement.ShouldSetMovement = true;

            CharacterOrientation2D orientation = GetOrAdd<CharacterOrientation2D>(gameObject);
            orientation.ModelShouldFlip = true;
            orientation.ModelFlipValueLeft = new Vector3(-1f, 1f, 1f);
            orientation.ModelFlipValueRight = Vector3.one;
            orientation.ModelShouldRotate = false;
        }

        private void ApplyResolvedHealth(Health health)
        {
            if (health == null)
            {
                return;
            }

            float resolvedHealth = ResolveMaxHealth();
            health.InitialHealth = resolvedHealth;
            health.MaximumHealth = resolvedHealth;

            if (Application.isPlaying)
            {
                health.SetHealth(resolvedHealth);
            }
        }

        private void ApplyResolvedMovement(CharacterMovement movement)
        {
            if (movement == null)
            {
                return;
            }

            float resolvedMoveSpeed = ResolveMoveSpeed();
            movement.WalkSpeed = resolvedMoveSpeed;
            movement.MovementSpeed = resolvedMoveSpeed;
        }

        private float ResolveMaxHealth()
        {
            return Mathf.Max(0f, bossData != null ? bossData.MaxHealth : initialHealth);
        }

        private float ResolveMoveSpeed()
        {
            return Mathf.Max(0f, bossData != null ? bossData.MoveSpeed : movementSpeed);
        }

        private void ResolvePhaseThresholds(out float phase2ThresholdNormalized, out float phase3ThresholdNormalized)
        {
            if (bossData != null)
            {
                phase2ThresholdNormalized = Mathf.Clamp01(bossData.Phase2ThresholdNormalized);
                phase3ThresholdNormalized = Mathf.Clamp01(bossData.Phase3ThresholdNormalized);
            }
            else
            {
                phase2ThresholdNormalized = Mathf.Clamp01(stunThresholdNormalized);
                phase3ThresholdNormalized = Mathf.Clamp01(tiredThresholdNormalized);
            }

            phase3ThresholdNormalized = Mathf.Min(phase3ThresholdNormalized, phase2ThresholdNormalized);
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
            AIAction normalDashStartAction,
            AIAction dashAttackStartAction,
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
            AIDecisionTimeInState normalDashTelegraphTimer,
            AIDecisionTimeInState normalDashChargeTimer,
            AIDecisionTimeInState normalDashRecoverTimer,
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
                    NormalDashTelegraphStateName,
                    new AIAction[] { idleAction },
                    new[]
                    {
                        CreateTransition(normalDashTelegraphTimer, NormalDashChargeStateName, string.Empty)
                    }),
                CreateState(
                    NormalDashChargeStateName,
                    new[] { normalDashStartAction },
                    new[]
                    {
                        CreateTransition(normalDashChargeTimer, NormalDashRecoverStateName, string.Empty)
                    }),
                CreateRecoverState(NormalDashRecoverStateName, idleAction, targetIsAlive, normalDashRecoverTimer),
                CreateState(
                    DashTelegraphStateName,
                    new AIAction[] { idleAction },
                    new[]
                    {
                        CreateTransition(dashTelegraphTimer, DashChargeStateName, string.Empty)
                    }),
                CreateState(
                    DashChargeStateName,
                    new[] { dashAttackStartAction },
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

        private BerthaDashStartAction GetOrAddDashStartAction(string actionLabel)
        {
            BerthaDashStartAction[] existingActions = GetComponents<BerthaDashStartAction>();
            BerthaDashStartAction unassignedAction = null;

            for (int i = 0; i < existingActions.Length; i++)
            {
                BerthaDashStartAction action = existingActions[i];
                if (action == null)
                {
                    continue;
                }

                if (action.Label == actionLabel)
                {
                    return action;
                }

                if (string.IsNullOrWhiteSpace(action.Label) && unassignedAction == null)
                {
                    unassignedAction = action;
                }
            }

            return unassignedAction != null ? unassignedAction : gameObject.AddComponent<BerthaDashStartAction>();
        }

        private AIBrainDashTelegraphDriver GetOrAddDashTelegraphDriver(string driverKey)
        {
            AIBrainDashTelegraphDriver[] existingDrivers = GetComponents<AIBrainDashTelegraphDriver>();
            AIBrainDashTelegraphDriver reusableDriver = null;

            for (int i = 0; i < existingDrivers.Length; i++)
            {
                AIBrainDashTelegraphDriver driver = existingDrivers[i];
                if (driver == null)
                {
                    continue;
                }

                if (driver.DriverKey == driverKey)
                {
                    return driver;
                }

                if (reusableDriver == null
                    && (string.IsNullOrWhiteSpace(driver.DriverKey) || driver.DriverKey == "DashTelegraph"))
                {
                    reusableDriver = driver;
                }
            }

            AIBrainDashTelegraphDriver resolvedDriver = reusableDriver != null
                ? reusableDriver
                : gameObject.AddComponent<AIBrainDashTelegraphDriver>();
            resolvedDriver.SetDriverKey(driverKey);
            return resolvedDriver;
        }

        private BerthaProjectilePatternDriver GetOrCreateProjectilePatternDriver(string driverKey)
        {
            BerthaProjectilePatternDriver[] existingDrivers = GetComponents<BerthaProjectilePatternDriver>();
            BerthaProjectilePatternDriver unassignedDriver = null;

            for (int i = 0; i < existingDrivers.Length; i++)
            {
                BerthaProjectilePatternDriver driver = existingDrivers[i];
                if (driver == null)
                {
                    continue;
                }

                if (driver.DriverKey == driverKey)
                {
                    return driver;
                }

                if (string.IsNullOrWhiteSpace(driver.DriverKey) && unassignedDriver == null)
                {
                    unassignedDriver = driver;
                }
            }

            return unassignedDriver != null ? unassignedDriver : gameObject.AddComponent<BerthaProjectilePatternDriver>();
        }

        private SpriteRenderer ResolveProjectileSortingReference()
        {
            if (visualRoot != null)
            {
                SpriteRenderer visualRenderer = visualRoot.GetComponentInChildren<SpriteRenderer>(true);
                if (visualRenderer != null)
                {
                    return visualRenderer;
                }
            }

            return GetComponentInChildren<SpriteRenderer>(true);
        }

        private Transform ResolveHitReactionShakeTarget()
        {
            if (visualRoot != null)
            {
                Transform spriteChild = visualRoot.Find("BerthaSprite");
                if (spriteChild != null)
                {
                    return spriteChild;
                }

                return visualRoot;
            }

            return transform;
        }

        private SpriteRenderer[] ResolveHitReactionRenderers()
        {
            if (visualRoot != null)
            {
                SpriteRenderer[] visualRenderers = visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
                if (visualRenderers != null && visualRenderers.Length > 0)
                {
                    return visualRenderers;
                }
            }

            return GetComponentsInChildren<SpriteRenderer>(true);
        }

        private static void NormalizeBurstSequence(BerthaProjectilePatternDriver.BurstInstruction[] burstSequence)
        {
            if (burstSequence == null)
            {
                return;
            }

            for (int i = 0; i < burstSequence.Length; i++)
            {
                BerthaProjectilePatternDriver.BurstInstruction burst = burstSequence[i];
                burst.Delay = Mathf.Max(0f, burst.Delay);
                burst.ProjectileCount = Mathf.Max(1, burst.ProjectileCount);
                burst.SpreadAngle = Mathf.Max(0f, burst.SpreadAngle);
                burst.Speed = Mathf.Max(0.01f, burst.Speed);
                burst.Lifetime = Mathf.Max(0.01f, burst.Lifetime);
                burst.Damage = Mathf.Max(0f, burst.Damage);
                burst.TargetInvincibilityDuration = Mathf.Max(0f, burst.TargetInvincibilityDuration);
                burst.HitRadius = Mathf.Max(0.05f, burst.HitRadius);
                burst.SpawnDistance = Mathf.Max(0f, burst.SpawnDistance);
                burstSequence[i] = burst;
            }
        }

#if UNITY_EDITOR
        private void TryAutoAssignProjectileFramesInEditor()
        {
            if (!autoAssignProjectileFxFramesInEditMode)
            {
                return;
            }

            string[] spriteGuids = AssetDatabase.FindAssets("t:Sprite", new[] { ComboProjectileFxFolder });
            if (spriteGuids == null || spriteGuids.Length == 0)
            {
                return;
            }

            List<string> spritePaths = new List<string>(spriteGuids.Length);
            for (int i = 0; i < spriteGuids.Length; i++)
            {
                spritePaths.Add(AssetDatabase.GUIDToAssetPath(spriteGuids[i]));
            }

            spritePaths.Sort(CompareProjectileSpritePaths);

            List<Sprite> loadedSprites = new List<Sprite>(spritePaths.Count);
            for (int i = 0; i < spritePaths.Count; i++)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePaths[i]);
                if (sprite != null)
                {
                    loadedSprites.Add(sprite);
                }
            }

            if (loadedSprites.Count == 0)
            {
                return;
            }

            if (HasSameProjectileSpriteSequence(comboFxProjectileFrames, loadedSprites))
            {
                return;
            }

            comboFxProjectileFrames = loadedSprites.ToArray();
            EditorUtility.SetDirty(this);
        }
#endif

        private static bool HasSameProjectileSpriteSequence(Sprite[] existingFrames, List<Sprite> loadedSprites)
        {
            if (existingFrames == null || loadedSprites == null || existingFrames.Length != loadedSprites.Count)
            {
                return false;
            }

            for (int i = 0; i < existingFrames.Length; i++)
            {
                if (existingFrames[i] != loadedSprites[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static int CompareProjectileSpritePaths(string leftPath, string rightPath)
        {
            int leftNumber = ExtractTrailingNumber(System.IO.Path.GetFileNameWithoutExtension(leftPath));
            int rightNumber = ExtractTrailingNumber(System.IO.Path.GetFileNameWithoutExtension(rightPath));
            int numberComparison = leftNumber.CompareTo(rightNumber);

            if (numberComparison != 0)
            {
                return numberComparison;
            }

            return string.CompareOrdinal(leftPath, rightPath);
        }

        private static int ExtractTrailingNumber(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return int.MaxValue;
            }

            int digitStartIndex = value.Length;
            while (digitStartIndex > 0 && char.IsDigit(value[digitStartIndex - 1]))
            {
                digitStartIndex--;
            }

            if (digitStartIndex >= value.Length)
            {
                return int.MaxValue;
            }

            return int.TryParse(value.Substring(digitStartIndex), out int parsedNumber)
                ? parsedNumber
                : int.MaxValue;
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

        private static bool Approximately(Color left, Color right)
        {
            return Mathf.Approximately(left.r, right.r)
                && Mathf.Approximately(left.g, right.g)
                && Mathf.Approximately(left.b, right.b)
                && Mathf.Approximately(left.a, right.a);
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
