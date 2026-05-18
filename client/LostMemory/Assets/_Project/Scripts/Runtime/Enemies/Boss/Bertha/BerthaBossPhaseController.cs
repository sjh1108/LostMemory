using System;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies.Boss.Bertha
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Boss/Bertha/Bertha Boss Phase Controller")]
    [DefaultExecutionOrder(300)]
    public sealed class BerthaBossPhaseController : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField, Range(0f, 1f)] private float phase2ThresholdNormalized = 0.7f;
        [SerializeField, Range(0f, 1f)] private float phase3ThresholdNormalized = 0.3f;
        [SerializeField] private bool debugLogging;

        private BerthaBossPhase _currentPhase = BerthaBossPhase.Phase1;

        public event Action<BerthaBossPhase, BerthaBossPhase> PhaseChanged;

        public BerthaBossPhase CurrentPhase => _currentPhase;

        public float NormalizedHealth => GetNormalizedHealth();

        public float Phase2ThresholdNormalized => phase2ThresholdNormalized;

        public float Phase3ThresholdNormalized => phase3ThresholdNormalized;

        private void Reset()
        {
            RefreshReferences();
            NormalizeThresholds();
        }

        private void OnValidate()
        {
            RefreshReferences();
            NormalizeThresholds();
        }

        private void Awake()
        {
            RefreshReferences();
            NormalizeThresholds();
            _currentPhase = ResolvePhase(GetNormalizedHealth());
        }

        private void OnEnable()
        {
            RefreshReferences();
            NormalizeThresholds();

            if (health == null)
            {
                return;
            }

            health.OnHit += HandleHealthChanged;
            health.OnDeath += HandleDeath;
            EvaluatePhase(raiseEvent: false);
        }

        private void OnDisable()
        {
            if (health == null)
            {
                return;
            }

            health.OnHit -= HandleHealthChanged;
            health.OnDeath -= HandleDeath;
        }

        public void Configure(
            Health configuredHealth,
            float configuredPhase2ThresholdNormalized,
            float configuredPhase3ThresholdNormalized,
            bool configuredDebugLogging)
        {
            health = configuredHealth;
            phase2ThresholdNormalized = configuredPhase2ThresholdNormalized;
            phase3ThresholdNormalized = configuredPhase3ThresholdNormalized;
            debugLogging = configuredDebugLogging;

            NormalizeThresholds();
            _currentPhase = ResolvePhase(GetNormalizedHealth());
        }

        public void RefreshReferences()
        {
            health ??= GetComponent<Health>();
        }

        public bool IsAtLeast(BerthaBossPhase phase)
        {
            return _currentPhase >= phase;
        }

        private void HandleHealthChanged()
        {
            EvaluatePhase(raiseEvent: true);
        }

        private void HandleDeath()
        {
            Log("Stopped phase evaluation on death.");
        }

        private void EvaluatePhase(bool raiseEvent)
        {
            if (health == null || health.CurrentHealth <= 0f)
            {
                return;
            }

            BerthaBossPhase nextPhase = ResolvePhase(GetNormalizedHealth());
            if (nextPhase <= _currentPhase)
            {
                return;
            }

            BerthaBossPhase previousPhase = _currentPhase;
            _currentPhase = nextPhase;
            Log("Phase changed: " + previousPhase + " -> " + _currentPhase + ".");

            if (raiseEvent)
            {
                PhaseChanged?.Invoke(previousPhase, _currentPhase);
            }
        }

        private BerthaBossPhase ResolvePhase(float normalizedHealth)
        {
            if (normalizedHealth <= phase3ThresholdNormalized)
            {
                return BerthaBossPhase.Phase3;
            }

            if (normalizedHealth <= phase2ThresholdNormalized)
            {
                return BerthaBossPhase.Phase2;
            }

            return BerthaBossPhase.Phase1;
        }

        private void NormalizeThresholds()
        {
            phase2ThresholdNormalized = Mathf.Clamp01(phase2ThresholdNormalized);
            phase3ThresholdNormalized = Mathf.Clamp01(phase3ThresholdNormalized);
            phase3ThresholdNormalized = Mathf.Min(phase3ThresholdNormalized, phase2ThresholdNormalized);
        }

        private float GetNormalizedHealth()
        {
            if (health == null)
            {
                return 1f;
            }

            float maxHealth = Mathf.Max(health.MaximumHealth, health.InitialHealth, 0.0001f);
            return Mathf.Clamp01(health.CurrentHealth / maxHealth);
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[BerthaBossPhaseController] " + message, this);
            }
        }
    }
}
