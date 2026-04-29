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
        }

        private enum PatternType
        {
            None = 0,
            LightAttack1 = 1,
            LightAttack2 = 2,
            HeavyAttack = 3,
            NormalDash = 4,
            DashAttack = 5,
            FullCombo = 6
        }

        private readonly List<PatternType> _basicPatternPool = new List<PatternType>(8);

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
        [SerializeField] private float lightAttack1Range = 2.5f;
        [SerializeField] private float lightAttack2Range = 2.5f;
        [SerializeField] private float heavyAttackRange = 3.25f;
        [SerializeField] private float normalDashMinimumRange = 4.5f;
        [SerializeField] private float normalDashMaximumRange = 15f;
        [SerializeField] private float dashMinimumRange = 3f;
        [SerializeField] private float dashMaximumRange = 4.5f;
        [SerializeField] private float fullComboRange = 3f;
        [SerializeField, Range(0f, 1f)] private float specialUnlockHealthThresholdNormalized = 0.9f;
        [SerializeField] private float normalDashCooldown = 10f;
        [SerializeField] private float dashCooldown = 15f;
        [SerializeField] private float fullComboCooldown = 15f;
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
                NormalDashCooldown = 10f,
                DashCooldown = 15f,
                FullComboCooldown = 15f,
                LightAttack1Weight = 3,
                LightAttack2Weight = 3,
                HeavyAttackWeight = 2,
                NormalDashWeight = 1
            },
            new PhasePatternSettings
            {
                Phase = BerthaBossPhase.Phase2,
                AllowDashAttack = true,
                AllowFullCombo = true,
                NormalDashCooldown = 8f,
                DashCooldown = 12f,
                FullComboCooldown = 12f,
                LightAttack1Weight = 2,
                LightAttack2Weight = 3,
                HeavyAttackWeight = 3,
                NormalDashWeight = 2
            },
            new PhasePatternSettings
            {
                Phase = BerthaBossPhase.Phase3,
                AllowDashAttack = true,
                AllowFullCombo = true,
                NormalDashCooldown = 6f,
                DashCooldown = 9f,
                FullComboCooldown = 9f,
                LightAttack1Weight = 1,
                LightAttack2Weight = 2,
                HeavyAttackWeight = 3,
                NormalDashWeight = 2
            }
        };
        [SerializeField] private bool debugLogging;

        private PatternType _lastBasicPattern = PatternType.None;
        private float _nextNormalDashReadyTime;
        private float _nextDashReadyTime;
        private float _nextFullComboReadyTime;

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
            fullComboRange = configuredFullComboRange;
            specialUnlockHealthThresholdNormalized = configuredSpecialUnlockHealthThresholdNormalized;
            normalDashCooldown = configuredNormalDashCooldown;
            dashCooldown = configuredDashCooldown;
            fullComboCooldown = configuredFullComboCooldown;
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

            if (CanUseSpecialPatterns(activeSettings))
            {
                if (activeSettings.AllowFullCombo
                    && targetDistance <= fullComboRange
                    && Time.time >= _nextFullComboReadyTime)
                {
                    return PatternType.FullCombo;
                }

                if (activeSettings.AllowDashAttack
                    && targetDistance >= dashMinimumRange
                    && targetDistance <= dashMaximumRange
                    && Time.time >= _nextDashReadyTime
                    && (dashAbility == null || dashAbility.Cooldown.Ready()))
                {
                    return PatternType.DashAttack;
                }
            }

            return SelectRandomBasicPattern(targetDistance, activeSettings);
        }

        private PatternType SelectRandomBasicPattern(float targetDistance, PhasePatternSettings activeSettings)
        {
            _basicPatternPool.Clear();

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
                AddBasicPatternIfInRange(PatternType.LightAttack1, targetDistance, lightAttack1Range, activeSettings.LightAttack1Weight);
                AddBasicPatternIfInRange(PatternType.LightAttack2, targetDistance, lightAttack2Range, activeSettings.LightAttack2Weight);
                AddBasicPatternIfInRange(PatternType.HeavyAttack, targetDistance, heavyAttackRange, activeSettings.HeavyAttackWeight);
                AddNormalDashIfReady(targetDistance, activeSettings);
            }

            return _basicPatternPool[Random.Range(0, _basicPatternPool.Count)];
        }

        private bool CanUseSpecialPatterns(PhasePatternSettings activeSettings)
        {
            if (phaseController != null)
            {
                return activeSettings.AllowDashAttack || activeSettings.AllowFullCombo;
            }

            if (health == null || health.MaximumHealth <= 0f)
            {
                return false;
            }

            return health.CurrentHealth <= health.MaximumHealth * specialUnlockHealthThresholdNormalized;
        }

        private void AddBasicPatternIfInRange(PatternType patternType, float targetDistance, float attackRange, int weight)
        {
            if (weight <= 0 || targetDistance > attackRange)
            {
                return;
            }

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
                NormalDashWeight = normalDashWeight
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
                phasePatternSettings[i] = settings;
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
            int uniqueCount = 0;
            bool hasLight1 = false;
            bool hasLight2 = false;
            bool hasHeavy = false;
            bool hasNormalDash = false;

            for (int i = 0; i < patternPool.Count; i++)
            {
                switch (patternPool[i])
                {
                    case PatternType.LightAttack1 when !hasLight1:
                        hasLight1 = true;
                        uniqueCount++;
                        break;
                    case PatternType.LightAttack2 when !hasLight2:
                        hasLight2 = true;
                        uniqueCount++;
                        break;
                    case PatternType.HeavyAttack when !hasHeavy:
                        hasHeavy = true;
                        uniqueCount++;
                        break;
                    case PatternType.NormalDash when !hasNormalDash:
                        hasNormalDash = true;
                        uniqueCount++;
                        break;
                }
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
