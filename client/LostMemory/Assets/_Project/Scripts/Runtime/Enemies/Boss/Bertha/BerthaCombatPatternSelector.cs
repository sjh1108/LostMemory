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
        private enum PatternType
        {
            None = 0,
            LightAttack1 = 1,
            LightAttack2 = 2,
            HeavyAttack = 3,
            Dash = 4,
            FullCombo = 5
        }

        private readonly List<PatternType> _basicPatternPool = new List<PatternType>(8);

        [SerializeField] private AIBrain brain;
        [SerializeField] private Character character;
        [SerializeField] private Health health;
        [SerializeField] private CharacterDamageDash2D dashAbility;
        [SerializeField] private string movingStateName = "Moving";
        [SerializeField] private string lightAttack1TelegraphStateName = "LightTelegraph";
        [SerializeField] private string lightAttack2TelegraphStateName = "Light2Telegraph";
        [SerializeField] private string heavyAttackTelegraphStateName = "HeavyTelegraph";
        [SerializeField] private string dashTelegraphStateName = "DashTelegraph";
        [SerializeField] private string fullComboTelegraphStateName = "FullTelegraph";
        [SerializeField] private float lightAttack1Range = 2.5f;
        [SerializeField] private float lightAttack2Range = 2.5f;
        [SerializeField] private float heavyAttackRange = 3.25f;
        [SerializeField] private float dashMinimumRange = 3f;
        [SerializeField] private float dashMaximumRange = 4.5f;
        [SerializeField] private float fullComboRange = 3f;
        [SerializeField, Range(0f, 1f)] private float specialUnlockHealthThresholdNormalized = 0.9f;
        [SerializeField] private float dashCooldown = 15f;
        [SerializeField] private float fullComboCooldown = 15f;
        [SerializeField, Min(0)] private int lightAttack1Weight = 3;
        [SerializeField, Min(0)] private int lightAttack2Weight = 3;
        [SerializeField, Min(0)] private int heavyAttackWeight = 2;
        [SerializeField] private bool debugLogging;

        private PatternType _lastBasicPattern = PatternType.None;
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
            dashMinimumRange = Mathf.Max(0f, dashMinimumRange);
            dashMaximumRange = Mathf.Max(dashMinimumRange, dashMaximumRange);
            specialUnlockHealthThresholdNormalized = Mathf.Clamp01(specialUnlockHealthThresholdNormalized);
            RefreshReferences();
        }

        private void Awake()
        {
            RefreshReferences();
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
        }

        public void Configure(
            AIBrain configuredBrain,
            Character configuredCharacter,
            Health configuredHealth,
            CharacterDamageDash2D configuredDashAbility,
            float configuredLightAttack1Range,
            float configuredLightAttack2Range,
            float configuredHeavyAttackRange,
            float configuredDashMinimumRange,
            float configuredDashMaximumRange,
            float configuredFullComboRange,
            float configuredSpecialUnlockHealthThresholdNormalized,
            float configuredDashCooldown,
            float configuredFullComboCooldown,
            int configuredLightAttack1Weight,
            int configuredLightAttack2Weight,
            int configuredHeavyAttackWeight)
        {
            brain = configuredBrain;
            character = configuredCharacter;
            health = configuredHealth;
            dashAbility = configuredDashAbility;
            lightAttack1Range = configuredLightAttack1Range;
            lightAttack2Range = configuredLightAttack2Range;
            heavyAttackRange = configuredHeavyAttackRange;
            dashMinimumRange = configuredDashMinimumRange;
            dashMaximumRange = configuredDashMaximumRange;
            fullComboRange = configuredFullComboRange;
            specialUnlockHealthThresholdNormalized = configuredSpecialUnlockHealthThresholdNormalized;
            dashCooldown = configuredDashCooldown;
            fullComboCooldown = configuredFullComboCooldown;
            lightAttack1Weight = Mathf.Max(0, configuredLightAttack1Weight);
            lightAttack2Weight = Mathf.Max(0, configuredLightAttack2Weight);
            heavyAttackWeight = Mathf.Max(0, configuredHeavyAttackWeight);
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

            if (CanUseSpecialPatterns())
            {
                if (targetDistance <= fullComboRange && Time.time >= _nextFullComboReadyTime)
                {
                    return PatternType.FullCombo;
                }

                if (targetDistance >= dashMinimumRange
                    && targetDistance <= dashMaximumRange
                    && Time.time >= _nextDashReadyTime
                    && (dashAbility == null || dashAbility.Cooldown.Ready()))
                {
                    return PatternType.Dash;
                }
            }

            return SelectRandomBasicPattern(targetDistance);
        }

        private PatternType SelectRandomBasicPattern(float targetDistance)
        {
            _basicPatternPool.Clear();

            AddBasicPatternIfInRange(PatternType.LightAttack1, targetDistance, lightAttack1Range, lightAttack1Weight);
            AddBasicPatternIfInRange(PatternType.LightAttack2, targetDistance, lightAttack2Range, lightAttack2Weight);
            AddBasicPatternIfInRange(PatternType.HeavyAttack, targetDistance, heavyAttackRange, heavyAttackWeight);

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
                AddBasicPatternIfInRange(PatternType.LightAttack1, targetDistance, lightAttack1Range, lightAttack1Weight);
                AddBasicPatternIfInRange(PatternType.LightAttack2, targetDistance, lightAttack2Range, lightAttack2Weight);
                AddBasicPatternIfInRange(PatternType.HeavyAttack, targetDistance, heavyAttackRange, heavyAttackWeight);
            }

            return _basicPatternPool[Random.Range(0, _basicPatternPool.Count)];
        }

        private bool CanUseSpecialPatterns()
        {
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

        private void ReserveCooldown(PatternType selectedPattern)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            switch (selectedPattern)
            {
                case PatternType.Dash:
                    _nextDashReadyTime = Time.time + dashCooldown;
                    break;
                case PatternType.FullCombo:
                    _nextFullComboReadyTime = Time.time + fullComboCooldown;
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
                PatternType.Dash => dashTelegraphStateName,
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
