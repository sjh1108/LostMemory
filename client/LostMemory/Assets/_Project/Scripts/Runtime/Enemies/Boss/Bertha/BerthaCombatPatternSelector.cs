using System.Collections.Generic;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies.Boss.Bertha
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Boss/Bertha/Bertha Combat Pattern Selector")]
    public sealed class BerthaCombatPatternSelector : MonoBehaviour
    {
        private const float LegacyFullComboRange = 3f;
        private const float PreviousFullComboRange = 3.5f;
        private const float RequestedFullComboRange = 4f;
        private const int RequestedPhase2FullComboWeight = 2;
        private const float RequestedPhase2FullComboCooldown = 8f;
        private const int RequestedPhase3FullComboWeight = 3;
        private const float RequestedPhase3FullComboCooldown = 6f;

        [System.Serializable]
        private struct PhasePatternSettings
        {
            public BerthaBossPhase Phase;
            public bool AllowDashAttack;
            public bool AllowFullCombo;
            [Min(0f)] public float NormalDashCooldown;
            [Min(0f)] public float DashCooldown;
            [Min(0f)] public float FullComboCooldown;
            [Min(0)] public int LightAttack1Weight;
            [Min(0)] public int LightAttack2Weight;
            [Min(0)] public int HeavyAttackWeight;
            [Min(0)] public int NormalDashWeight;
            [Min(0)] public int DashAttackWeight;
            [Min(0)] public int FullComboWeight;
            [Min(0)] public int LightProjectileWeight;
            [Min(0f)] public float LightProjectileCooldown;
            [Min(0)] public int ProjectileBarrageWeight;
            [Min(0f)] public float ProjectileBarrageCooldown;
            [Min(0)] public int ProjectileStormWeight;
            [Min(0f)] public float ProjectileStormCooldown;
        }

        private enum PatternType
        {
            None = 0,
            LightAttack1 = 1,
            LightAttack2 = 2,
            HeavyAttack = 3,
            NormalDash = 4,
            DashAttack = 5,
            FullCombo = 6,
            ProjectileBarrage = 7,
            ProjectileStorm = 8,
            LightProjectile = 9
        }

        private readonly List<PatternType> _basicPatternPool = new List<PatternType>(10);

        [SerializeField] private AIBrain brain;
        [SerializeField] private Character character;
        [SerializeField] private Health health;
        [SerializeField] private CharacterDamageDash2D dashAbility;
        [SerializeField] private BerthaBossPhaseController phaseController;
        [SerializeField] private string movingStateName = "Moving";
        [SerializeField] private string lightAttack1TelegraphStateName = "LightTelegraph";
        [SerializeField] private string lightAttack2TelegraphStateName = "Light2Telegraph";
        [SerializeField] private string heavyAttackTelegraphStateName = "HeavyTelegraph";
        [SerializeField] private string normalDashTelegraphStateName = "NormalDashTelegraph";
        [SerializeField] private string dashTelegraphStateName = "DashTelegraph";
        [SerializeField] private string fullComboTelegraphStateName = "FullTelegraph";
        [SerializeField] private string lightProjectileTelegraphStateName = "LightProjectileTelegraph";
        [SerializeField] private string projectileBarrageTelegraphStateName = "ProjectileBarrageTelegraph";
        [SerializeField] private string projectileStormTelegraphStateName = "ProjectileStormTelegraph";
        [SerializeField] private float lightAttack1Range = 2.5f;
        [SerializeField] private float lightAttack2Range = 2.5f;
        [SerializeField] private float heavyAttackRange = 3.25f;
        [SerializeField] private float normalDashMinimumRange = 4.5f;
        [SerializeField] private float normalDashMaximumRange = 15f;
        [SerializeField] private float dashMinimumRange = 3f;
        [SerializeField] private float dashMaximumRange = 4.5f;
        [SerializeField] private float fullComboRange = RequestedFullComboRange;
        [SerializeField, Range(0f, 1f)] private float specialUnlockHealthThresholdNormalized = 0.9f;
        [SerializeField] private float normalDashCooldown = 10f;
        [SerializeField] private float dashCooldown = 15f;
        [SerializeField] private float fullComboCooldown = 15f;
        [SerializeField] private float lightProjectileCooldown = 10f;
        [SerializeField] private float projectileBarrageCooldown = 12f;
        [SerializeField] private float projectileStormCooldown = 14f;
        [SerializeField, Min(0f)] private float minimumTimeBetweenProjectilePatterns = 6f;
        [SerializeField, Min(0)] private int lightAttack1Weight = 3;
        [SerializeField, Min(0)] private int lightAttack2Weight = 3;
        [SerializeField, Min(0)] private int heavyAttackWeight = 2;
        [SerializeField, Min(0)] private int normalDashWeight = 1;
        [SerializeField] private PhasePatternSettings[] phasePatternSettings =
        {
            new PhasePatternSettings
            {
                Phase = BerthaBossPhase.Phase1,
                AllowDashAttack = false,
                AllowFullCombo = false,
                NormalDashCooldown = 7f,
                DashCooldown = 15f,
                FullComboCooldown = 15f,
                LightAttack1Weight = 3,
                LightAttack2Weight = 3,
                HeavyAttackWeight = 2,
                NormalDashWeight = 1,
                DashAttackWeight = 0,
                FullComboWeight = 0,
                LightProjectileWeight = 1,
                LightProjectileCooldown = 10f,
                ProjectileBarrageWeight = 0,
                ProjectileBarrageCooldown = 12f,
                ProjectileStormWeight = 0,
                ProjectileStormCooldown = 14f
            },
            new PhasePatternSettings
            {
                Phase = BerthaBossPhase.Phase2,
                AllowDashAttack = true,
                AllowFullCombo = true,
                NormalDashCooldown = 5f,
                DashCooldown = 8f,
                FullComboCooldown = RequestedPhase2FullComboCooldown,
                LightAttack1Weight = 2,
                LightAttack2Weight = 3,
                HeavyAttackWeight = 3,
                NormalDashWeight = 2,
                DashAttackWeight = 1,
                FullComboWeight = RequestedPhase2FullComboWeight,
                LightProjectileWeight = 1,
                LightProjectileCooldown = 12f,
                ProjectileBarrageWeight = 1,
                ProjectileBarrageCooldown = 16f,
                ProjectileStormWeight = 0,
                ProjectileStormCooldown = 14f
            },
            new PhasePatternSettings
            {
                Phase = BerthaBossPhase.Phase3,
                AllowDashAttack = true,
                AllowFullCombo = true,
                NormalDashCooldown = 3.5f,
                DashCooldown = 6f,
                FullComboCooldown = RequestedPhase3FullComboCooldown,
                LightAttack1Weight = 1,
                LightAttack2Weight = 2,
                HeavyAttackWeight = 4,
                NormalDashWeight = 3,
                DashAttackWeight = 2,
                FullComboWeight = RequestedPhase3FullComboWeight,
                LightProjectileWeight = 0,
                LightProjectileCooldown = 12f,
                ProjectileBarrageWeight = 2,
                ProjectileBarrageCooldown = 12f,
                ProjectileStormWeight = 2,
                ProjectileStormCooldown = 14f
            }
        };
        [SerializeField] private bool debugLogging;

        private PatternType _lastBasicPattern = PatternType.None;
        private float _nextNormalDashReadyTime;
        private float _nextDashReadyTime;
        private float _nextFullComboReadyTime;
        private float _nextLightProjectileReadyTime;
        private float _nextProjectileBarrageReadyTime;
        private float _nextProjectileStormReadyTime;
        private float _nextAnyProjectileReadyTime;

        private void Reset()
        {
            RefreshReferences();
        }

        private void OnValidate()
        {
            lightAttack1Weight = Mathf.Max(0, lightAttack1Weight);
            lightAttack2Weight = Mathf.Max(0, lightAttack2Weight);
            heavyAttackWeight = Mathf.Max(0, heavyAttackWeight);
            normalDashWeight = Mathf.Max(0, normalDashWeight);
            normalDashMinimumRange = Mathf.Max(0f, normalDashMinimumRange);
            normalDashMaximumRange = Mathf.Max(normalDashMinimumRange, normalDashMaximumRange);
            dashMinimumRange = Mathf.Max(0f, dashMinimumRange);
            dashMaximumRange = Mathf.Max(dashMinimumRange, dashMaximumRange);
            fullComboRange = NormalizeFullComboRange(fullComboRange);
            lightProjectileCooldown = Mathf.Max(0f, lightProjectileCooldown);
            projectileBarrageCooldown = Mathf.Max(0f, projectileBarrageCooldown);
            projectileStormCooldown = Mathf.Max(0f, projectileStormCooldown);
            minimumTimeBetweenProjectilePatterns = Mathf.Max(0f, minimumTimeBetweenProjectilePatterns);
            specialUnlockHealthThresholdNormalized = Mathf.Clamp01(specialUnlockHealthThresholdNormalized);
            NormalizePhaseSettings();
            RefreshReferences();
        }

        private void Awake()
        {
            RefreshReferences();
            NormalizePhaseSettings();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!IsReadyToSelectPattern())
            {
                return;
            }

            PatternType nextPattern = SelectNextPattern();
            if (nextPattern == PatternType.None)
            {
                return;
            }

            string nextStateName = ResolveTelegraphStateName(nextPattern);
            if (string.IsNullOrWhiteSpace(nextStateName))
            {
                return;
            }

            ReserveCooldown(nextPattern);
            UpdateBasicPatternHistory(nextPattern);
            Log("Transition to pattern: " + nextPattern);
            brain.TransitionToState(nextStateName);
        }

        public void RefreshReferences()
        {
            brain ??= GetComponent<AIBrain>();
            character ??= GetComponent<Character>();
            health ??= GetComponent<Health>();
            dashAbility ??= GetComponent<CharacterDamageDash2D>();
            phaseController ??= GetComponent<BerthaBossPhaseController>();
        }

        public void Configure(
            AIBrain configuredBrain,
            Character configuredCharacter,
            Health configuredHealth,
            CharacterDamageDash2D configuredDashAbility,
            BerthaBossPhaseController configuredPhaseController,
            float configuredLightAttack1Range,
            float configuredLightAttack2Range,
            float configuredHeavyAttackRange,
            float configuredNormalDashMinimumRange,
            float configuredNormalDashMaximumRange,
            float configuredDashMinimumRange,
            float configuredDashMaximumRange,
            float configuredFullComboRange,
            float configuredSpecialUnlockHealthThresholdNormalized,
            float configuredNormalDashCooldown,
            float configuredDashCooldown,
            float configuredFullComboCooldown,
            float configuredLightProjectileCooldown,
            float configuredProjectileBarrageCooldown,
            float configuredProjectileStormCooldown,
            int configuredLightAttack1Weight,
            int configuredLightAttack2Weight,
            int configuredHeavyAttackWeight,
            int configuredNormalDashWeight)
        {
            brain = configuredBrain;
            character = configuredCharacter;
            health = configuredHealth;
            dashAbility = configuredDashAbility;
            phaseController = configuredPhaseController;
            lightAttack1Range = configuredLightAttack1Range;
            lightAttack2Range = configuredLightAttack2Range;
            heavyAttackRange = configuredHeavyAttackRange;
            normalDashMinimumRange = configuredNormalDashMinimumRange;
            normalDashMaximumRange = configuredNormalDashMaximumRange;
            dashMinimumRange = configuredDashMinimumRange;
            dashMaximumRange = configuredDashMaximumRange;
            fullComboRange = NormalizeFullComboRange(configuredFullComboRange);
            specialUnlockHealthThresholdNormalized = configuredSpecialUnlockHealthThresholdNormalized;
            normalDashCooldown = configuredNormalDashCooldown;
            dashCooldown = configuredDashCooldown;
            fullComboCooldown = configuredFullComboCooldown;
            lightProjectileCooldown = configuredLightProjectileCooldown;
            projectileBarrageCooldown = configuredProjectileBarrageCooldown;
            projectileStormCooldown = configuredProjectileStormCooldown;
            lightAttack1Weight = Mathf.Max(0, configuredLightAttack1Weight);
            lightAttack2Weight = Mathf.Max(0, configuredLightAttack2Weight);
            heavyAttackWeight = Mathf.Max(0, configuredHeavyAttackWeight);
            normalDashWeight = Mathf.Max(0, configuredNormalDashWeight);
            NormalizePhaseSettings();
            RefreshReferences();
        }

        private bool IsReadyToSelectPattern()
        {
            if (brain == null
                || character == null
                || health == null
                || brain.CurrentState == null
                || brain.CurrentState.StateName != movingStateName
                || brain.Target == null)
            {
                return false;
            }

            if (IsDead() || health.CurrentHealth <= 0f)
            {
                return false;
            }

            return true;
        }

        private PatternType SelectNextPattern()
        {
            float targetDistance = Vector2.Distance(transform.position, brain.Target.position);
            PhasePatternSettings activeSettings = GetActivePhaseSettings();

            return SelectWeightedPattern(targetDistance, activeSettings);
        }

        private PatternType SelectWeightedPattern(float targetDistance, PhasePatternSettings activeSettings)
        {
            _basicPatternPool.Clear();

            AddSpecialPatternsIfReady(targetDistance, activeSettings);
            AddBasicPatternIfInRange(PatternType.LightAttack1, targetDistance, lightAttack1Range, activeSettings.LightAttack1Weight);
            AddBasicPatternIfInRange(PatternType.LightAttack2, targetDistance, lightAttack2Range, activeSettings.LightAttack2Weight);
            AddBasicPatternIfInRange(PatternType.HeavyAttack, targetDistance, heavyAttackRange, activeSettings.HeavyAttackWeight);
            AddNormalDashIfReady(targetDistance, activeSettings);

            if (_basicPatternPool.Count == 0)
            {
                return PatternType.None;
            }

            int uniquePatternCount = CountUniquePatterns(_basicPatternPool);
            if (uniquePatternCount > 1 && _lastBasicPattern != PatternType.None)
            {
                _basicPatternPool.RemoveAll(pattern => pattern == _lastBasicPattern);
            }

            if (_basicPatternPool.Count == 0)
            {
                AddSpecialPatternsIfReady(targetDistance, activeSettings);
                AddBasicPatternIfInRange(PatternType.LightAttack1, targetDistance, lightAttack1Range, activeSettings.LightAttack1Weight);
                AddBasicPatternIfInRange(PatternType.LightAttack2, targetDistance, lightAttack2Range, activeSettings.LightAttack2Weight);
                AddBasicPatternIfInRange(PatternType.HeavyAttack, targetDistance, heavyAttackRange, activeSettings.HeavyAttackWeight);
                AddNormalDashIfReady(targetDistance, activeSettings);
            }

            return _basicPatternPool[Random.Range(0, _basicPatternPool.Count)];
        }

        private void AddSpecialPatternsIfReady(float targetDistance, PhasePatternSettings activeSettings)
        {
            if (!CanUseSpecialPatterns(activeSettings))
            {
                return;
            }

            bool canSelectProjectile = CanSelectProjectilePattern();

            if (activeSettings.AllowDashAttack
                && activeSettings.DashAttackWeight > 0
                && targetDistance >= dashMinimumRange
                && targetDistance <= dashMaximumRange
                && Time.time >= _nextDashReadyTime
                && (dashAbility == null || dashAbility.Cooldown.Ready()))
            {
                AddPatternWeight(PatternType.DashAttack, activeSettings.DashAttackWeight);
            }

            if (activeSettings.AllowFullCombo
                && activeSettings.FullComboWeight > 0
                && targetDistance <= fullComboRange
                && Time.time >= _nextFullComboReadyTime)
            {
                AddPatternWeight(PatternType.FullCombo, activeSettings.FullComboWeight);
            }

            if (canSelectProjectile
                && activeSettings.LightProjectileWeight > 0
                && Time.time >= _nextLightProjectileReadyTime)
            {
                AddPatternWeight(PatternType.LightProjectile, activeSettings.LightProjectileWeight);
            }

            if (canSelectProjectile
                && activeSettings.ProjectileBarrageWeight > 0
                && Time.time >= _nextProjectileBarrageReadyTime)
            {
                AddPatternWeight(PatternType.ProjectileBarrage, activeSettings.ProjectileBarrageWeight);
            }

            if (canSelectProjectile
                && activeSettings.ProjectileStormWeight > 0
                && Time.time >= _nextProjectileStormReadyTime)
            {
                AddPatternWeight(PatternType.ProjectileStorm, activeSettings.ProjectileStormWeight);
            }
        }

        private bool CanUseSpecialPatterns(PhasePatternSettings activeSettings)
        {
            if (phaseController != null)
            {
                return activeSettings.AllowDashAttack
                    || activeSettings.AllowFullCombo
                    || activeSettings.LightProjectileWeight > 0
                    || activeSettings.ProjectileBarrageWeight > 0
                    || activeSettings.ProjectileStormWeight > 0;
            }

            if (health == null || health.MaximumHealth <= 0f)
            {
                return false;
            }

            return health.CurrentHealth <= health.MaximumHealth * specialUnlockHealthThresholdNormalized;
        }

        private bool CanSelectProjectilePattern()
        {
            return Time.time >= _nextAnyProjectileReadyTime;
        }

        private void AddBasicPatternIfInRange(PatternType patternType, float targetDistance, float attackRange, int weight)
        {
            if (weight <= 0 || targetDistance > attackRange)
            {
                return;
            }

            AddPatternWeight(patternType, weight);
        }

        private void AddPatternWeight(PatternType patternType, int weight)
        {
            for (int i = 0; i < weight; i++)
            {
                _basicPatternPool.Add(patternType);
            }
        }

        private void AddNormalDashIfReady(float targetDistance, PhasePatternSettings activeSettings)
        {
            if (activeSettings.NormalDashWeight <= 0
                || targetDistance < normalDashMinimumRange
                || targetDistance > normalDashMaximumRange
                || Time.time < _nextNormalDashReadyTime
                || (dashAbility != null && !dashAbility.Cooldown.Ready()))
            {
                return;
            }

            for (int i = 0; i < activeSettings.NormalDashWeight; i++)
            {
                _basicPatternPool.Add(PatternType.NormalDash);
            }
        }

        private void ReserveCooldown(PatternType selectedPattern)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            switch (selectedPattern)
            {
                case PatternType.NormalDash:
                    _nextNormalDashReadyTime = Time.time + GetActivePhaseSettings().NormalDashCooldown;
                    break;
                case PatternType.DashAttack:
                    _nextDashReadyTime = Time.time + GetActivePhaseSettings().DashCooldown;
                    break;
                case PatternType.FullCombo:
                    _nextFullComboReadyTime = Time.time + GetActivePhaseSettings().FullComboCooldown;
                    break;
                case PatternType.LightProjectile:
                    _nextLightProjectileReadyTime = Time.time + GetActivePhaseSettings().LightProjectileCooldown;
                    _nextAnyProjectileReadyTime = Time.time + minimumTimeBetweenProjectilePatterns;
                    break;
                case PatternType.ProjectileBarrage:
                    _nextProjectileBarrageReadyTime = Time.time + GetActivePhaseSettings().ProjectileBarrageCooldown;
                    _nextAnyProjectileReadyTime = Time.time + minimumTimeBetweenProjectilePatterns;
                    break;
                case PatternType.ProjectileStorm:
                    _nextProjectileStormReadyTime = Time.time + GetActivePhaseSettings().ProjectileStormCooldown;
                    _nextAnyProjectileReadyTime = Time.time + minimumTimeBetweenProjectilePatterns;
                    break;
            }
        }

        private PhasePatternSettings GetActivePhaseSettings()
        {
            BerthaBossPhase activePhase = ResolveActivePhase();
            if (phasePatternSettings != null)
            {
                for (int i = 0; i < phasePatternSettings.Length; i++)
                {
                    if (phasePatternSettings[i].Phase == activePhase)
                    {
                        return phasePatternSettings[i];
                    }
                }
            }

            return CreateFallbackSettings(activePhase);
        }

        private BerthaBossPhase ResolveActivePhase()
        {
            if (phaseController != null)
            {
                return phaseController.CurrentPhase;
            }

            if (health != null
                && health.MaximumHealth > 0f
                && health.CurrentHealth <= health.MaximumHealth * specialUnlockHealthThresholdNormalized)
            {
                return BerthaBossPhase.Phase2;
            }

            return BerthaBossPhase.Phase1;
        }

        private PhasePatternSettings CreateFallbackSettings(BerthaBossPhase activePhase)
        {
            bool allowSpecial = activePhase >= BerthaBossPhase.Phase2;
            bool allowFinalPhasePatterns = activePhase >= BerthaBossPhase.Phase3;
            return new PhasePatternSettings
            {
                Phase = activePhase,
                AllowDashAttack = allowSpecial,
                AllowFullCombo = allowSpecial,
                NormalDashCooldown = normalDashCooldown,
                DashCooldown = dashCooldown,
                FullComboCooldown = fullComboCooldown,
                LightAttack1Weight = lightAttack1Weight,
                LightAttack2Weight = lightAttack2Weight,
                HeavyAttackWeight = heavyAttackWeight,
                NormalDashWeight = normalDashWeight,
                DashAttackWeight = allowSpecial ? 1 : 0,
                FullComboWeight = allowSpecial ? 1 : 0,
                LightProjectileWeight = activePhase < BerthaBossPhase.Phase3 ? 1 : 0,
                LightProjectileCooldown = lightProjectileCooldown,
                ProjectileBarrageWeight = allowSpecial ? 1 : 0,
                ProjectileBarrageCooldown = projectileBarrageCooldown,
                ProjectileStormWeight = allowFinalPhasePatterns ? 1 : 0,
                ProjectileStormCooldown = projectileStormCooldown
            };
        }

        private void NormalizePhaseSettings()
        {
            if (phasePatternSettings == null)
            {
                return;
            }

            for (int i = 0; i < phasePatternSettings.Length; i++)
            {
                PhasePatternSettings settings = phasePatternSettings[i];
                settings.NormalDashCooldown = Mathf.Max(0f, settings.NormalDashCooldown);
                settings.DashCooldown = Mathf.Max(0f, settings.DashCooldown);
                settings.FullComboCooldown = Mathf.Max(0f, settings.FullComboCooldown);
                settings.LightAttack1Weight = Mathf.Max(0, settings.LightAttack1Weight);
                settings.LightAttack2Weight = Mathf.Max(0, settings.LightAttack2Weight);
                settings.HeavyAttackWeight = Mathf.Max(0, settings.HeavyAttackWeight);
                settings.NormalDashWeight = Mathf.Max(0, settings.NormalDashWeight);
                settings.DashAttackWeight = Mathf.Max(0, settings.DashAttackWeight);
                settings.FullComboWeight = Mathf.Max(0, settings.FullComboWeight);
                ApplyFullComboFrequencyDefaults(ref settings);
                ApplyLightProjectileDefaults(ref settings);
                settings.LightProjectileWeight = Mathf.Max(0, settings.LightProjectileWeight);
                settings.LightProjectileCooldown = Mathf.Max(0f, settings.LightProjectileCooldown);
                settings.ProjectileBarrageWeight = Mathf.Max(0, settings.ProjectileBarrageWeight);
                settings.ProjectileBarrageCooldown = Mathf.Max(0f, settings.ProjectileBarrageCooldown);
                settings.ProjectileStormWeight = Mathf.Max(0, settings.ProjectileStormWeight);
                settings.ProjectileStormCooldown = Mathf.Max(0f, settings.ProjectileStormCooldown);
                phasePatternSettings[i] = settings;
            }
        }

        private static float NormalizeFullComboRange(float value)
        {
            if (Mathf.Approximately(value, LegacyFullComboRange)
                || Mathf.Approximately(value, PreviousFullComboRange))
            {
                return RequestedFullComboRange;
            }

            return Mathf.Max(0f, value);
        }

        private static void ApplyFullComboFrequencyDefaults(ref PhasePatternSettings settings)
        {
            if (!settings.AllowFullCombo)
            {
                return;
            }

            switch (settings.Phase)
            {
                case BerthaBossPhase.Phase2:
                    settings.FullComboWeight = Mathf.Max(settings.FullComboWeight, RequestedPhase2FullComboWeight);
                    settings.FullComboCooldown = Mathf.Min(settings.FullComboCooldown, RequestedPhase2FullComboCooldown);
                    break;
                case BerthaBossPhase.Phase3:
                    settings.FullComboWeight = Mathf.Max(settings.FullComboWeight, RequestedPhase3FullComboWeight);
                    settings.FullComboCooldown = Mathf.Min(settings.FullComboCooldown, RequestedPhase3FullComboCooldown);
                    break;
            }
        }

        private void ApplyLightProjectileDefaults(ref PhasePatternSettings settings)
        {
            if (settings.LightProjectileWeight > 0 || settings.LightProjectileCooldown > 0f)
            {
                return;
            }

            switch (settings.Phase)
            {
                case BerthaBossPhase.Phase1:
                    settings.LightProjectileWeight = 1;
                    settings.LightProjectileCooldown = lightProjectileCooldown;
                    break;
                case BerthaBossPhase.Phase2:
                    settings.LightProjectileWeight = 1;
                    settings.LightProjectileCooldown = Mathf.Max(lightProjectileCooldown, 12f);
                    break;
                default:
                    settings.LightProjectileWeight = 0;
                    settings.LightProjectileCooldown = Mathf.Max(lightProjectileCooldown, 12f);
                    break;
            }
        }

        private void UpdateBasicPatternHistory(PatternType selectedPattern)
        {
            switch (selectedPattern)
            {
                case PatternType.LightAttack1:
                case PatternType.LightAttack2:
                case PatternType.HeavyAttack:
                case PatternType.NormalDash:
                    _lastBasicPattern = selectedPattern;
                    break;
            }
        }

        private string ResolveTelegraphStateName(PatternType selectedPattern)
        {
            return selectedPattern switch
            {
                PatternType.LightAttack1 => lightAttack1TelegraphStateName,
                PatternType.LightAttack2 => lightAttack2TelegraphStateName,
                PatternType.HeavyAttack => heavyAttackTelegraphStateName,
                PatternType.NormalDash => normalDashTelegraphStateName,
                PatternType.DashAttack => dashTelegraphStateName,
                PatternType.FullCombo => fullComboTelegraphStateName,
                PatternType.LightProjectile => lightProjectileTelegraphStateName,
                PatternType.ProjectileBarrage => projectileBarrageTelegraphStateName,
                PatternType.ProjectileStorm => projectileStormTelegraphStateName,
                _ => string.Empty
            };
        }

        private bool IsDead()
        {
            return character != null
                && character.ConditionState != null
                && character.ConditionState.CurrentState == CharacterStates.CharacterConditions.Dead;
        }

        private static int CountUniquePatterns(List<PatternType> patternPool)
        {
            bool[] seenPatterns = new bool[System.Enum.GetValues(typeof(PatternType)).Length];
            int uniqueCount = 0;

            for (int i = 0; i < patternPool.Count; i++)
            {
                int patternIndex = (int)patternPool[i];
                if (patternIndex <= 0
                    || patternIndex >= seenPatterns.Length
                    || seenPatterns[patternIndex])
                {
                    continue;
                }

                seenPatterns[patternIndex] = true;
                uniqueCount++;
            }

            return uniqueCount;
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[BerthaCombatPatternSelector] " + message, this);
            }
        }
    }
}
