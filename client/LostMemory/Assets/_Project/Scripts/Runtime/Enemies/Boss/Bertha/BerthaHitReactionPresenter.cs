using UnityEngine;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;

namespace LostMemory.Enemies.Boss.Bertha
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Boss/Bertha/Bertha Hit Reaction Presenter")]
    [DefaultExecutionOrder(310)]
    public sealed class BerthaHitReactionPresenter : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private Character character;
        [SerializeField] private AIBrain brain;
        [SerializeField] private Transform shakeTarget;
        [SerializeField] private SpriteRenderer[] targetRenderers = System.Array.Empty<SpriteRenderer>();
        [SerializeField] private Color hitTintColor = new Color(1f, 0.45f, 0.45f, 1f);
        [SerializeField, Range(0f, 1f)] private float hitTintStrength = 0.4f;
        [SerializeField, Min(0f)] private float hitTintDuration = 0.1f;
        [SerializeField, Min(0f)] private float hitShakeDuration = 0.12f;
        [SerializeField, Min(0f)] private float hitShakeMagnitude = 0.06f;
        [SerializeField, Min(1f)] private float hitScaleMultiplier = 1.08f;
        [SerializeField, Min(0f)] private float hitScaleDuration = 0.1f;
        [SerializeField, Min(1f)] private float dashReactionMultiplier = 1.6f;
        [SerializeField] private string[] enhancedReactionStateNameKeywords = { "Dash" };
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private bool debugLogging;

        private Color[] _baseColors = System.Array.Empty<Color>();
        private Vector3 _restLocalPosition;
        private Vector3 _restLocalScale = Vector3.one;
        private float _effectStartTime;
        private float _stateReactionMultiplier = 1f;
        private bool _isActive;

        private void Awake()
        {
            ResolveReferences();
            CacheBaseState();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            ResolveReferences();
            CacheBaseState();

            if (health != null)
            {
                health.OnHit += HandleHit;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.OnHit -= HandleHit;
            }

            RestoreState();
        }

        public void Configure(
            Health configuredHealth,
            Character configuredCharacter,
            Transform configuredShakeTarget,
            SpriteRenderer[] configuredTargetRenderers,
            Color configuredHitTintColor,
            float configuredHitTintStrength,
            float configuredHitTintDuration,
            float configuredHitShakeDuration,
            float configuredHitShakeMagnitude,
            float configuredHitScaleMultiplier,
            float configuredHitScaleDuration,
            float configuredDashReactionMultiplier,
            bool configuredDebugLogging)
        {
            health = configuredHealth;
            character = configuredCharacter;
            shakeTarget = configuredShakeTarget;
            targetRenderers = configuredTargetRenderers ?? System.Array.Empty<SpriteRenderer>();
            hitTintColor = configuredHitTintColor;
            hitTintStrength = Mathf.Clamp01(configuredHitTintStrength);
            hitTintDuration = Mathf.Max(0f, configuredHitTintDuration);
            hitShakeDuration = Mathf.Max(0f, configuredHitShakeDuration);
            hitShakeMagnitude = Mathf.Max(0f, configuredHitShakeMagnitude);
            hitScaleMultiplier = Mathf.Max(1f, configuredHitScaleMultiplier);
            hitScaleDuration = Mathf.Max(0f, configuredHitScaleDuration);
            dashReactionMultiplier = Mathf.Max(1f, configuredDashReactionMultiplier);
            debugLogging = configuredDebugLogging;

            ResolveReferences();
            CacheBaseState();
        }

        private void LateUpdate()
        {
            if (!_isActive)
            {
                return;
            }

            float now = GetCurrentTime();
            bool tintActive = UpdateTint(now);
            bool shakeActive = UpdateShake(now);
            bool scaleActive = UpdateScale(now);

            if (!tintActive && !shakeActive && !scaleActive)
            {
                RestoreState();
                _isActive = false;
            }
        }

        private void HandleHit()
        {
            ResolveReferences();
            RestoreState();
            CacheBaseState();

            _effectStartTime = GetCurrentTime();
            _stateReactionMultiplier = ResolveStateReactionMultiplier();
            _isActive = true;
            Log("Hit reaction triggered.");
        }

        private bool UpdateTint(float now)
        {
            if (hitTintDuration <= 0f || targetRenderers == null || targetRenderers.Length == 0)
            {
                RestoreColors();
                return false;
            }

            float elapsed = now - _effectStartTime;
            if (elapsed >= hitTintDuration)
            {
                RestoreColors();
                return false;
            }

            float normalized = 1f - (elapsed / hitTintDuration);
            float appliedTintStrength = Mathf.Clamp01(hitTintStrength * _stateReactionMultiplier);
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                SpriteRenderer renderer = targetRenderers[i];
                if (renderer == null || i >= _baseColors.Length)
                {
                    continue;
                }

                Color baseColor = _baseColors[i];
                Color tintedTarget = Color.Lerp(
                    baseColor,
                    new Color(hitTintColor.r, hitTintColor.g, hitTintColor.b, baseColor.a),
                    appliedTintStrength);
                renderer.color = Color.Lerp(baseColor, tintedTarget, normalized);
            }

            return true;
        }

        private bool UpdateShake(float now)
        {
            if (hitShakeDuration <= 0f || hitShakeMagnitude <= 0f || shakeTarget == null)
            {
                RestoreShake();
                return false;
            }

            float elapsed = now - _effectStartTime;
            if (elapsed >= hitShakeDuration)
            {
                RestoreShake();
                return false;
            }

            float normalized = 1f - (elapsed / hitShakeDuration);
            float appliedShakeMagnitude = hitShakeMagnitude * _stateReactionMultiplier;
            Vector2 offset = Random.insideUnitCircle * (appliedShakeMagnitude * normalized);
            shakeTarget.localPosition = _restLocalPosition + new Vector3(offset.x, offset.y, 0f);
            return true;
        }

        private bool UpdateScale(float now)
        {
            if (hitScaleDuration <= 0f || hitScaleMultiplier <= 1f || shakeTarget == null)
            {
                RestoreScale();
                return false;
            }

            float elapsed = now - _effectStartTime;
            if (elapsed >= hitScaleDuration)
            {
                RestoreScale();
                return false;
            }

            float normalized = 1f - (elapsed / hitScaleDuration);
            float appliedScaleMultiplier = 1f + ((hitScaleMultiplier - 1f) * _stateReactionMultiplier);
            shakeTarget.localScale = _restLocalScale * (1f + ((appliedScaleMultiplier - 1f) * normalized));
            return true;
        }

        private void ResolveReferences()
        {
            health ??= GetComponent<Health>();
            character ??= GetComponent<Character>();
            brain ??= GetComponent<AIBrain>();

            if (shakeTarget == null)
            {
                shakeTarget = transform;
            }

            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                targetRenderers = shakeTarget != null
                    ? shakeTarget.GetComponentsInChildren<SpriteRenderer>(true)
                    : GetComponentsInChildren<SpriteRenderer>(true);
            }
        }

        private void CacheBaseState()
        {
            if (shakeTarget != null)
            {
                _restLocalPosition = shakeTarget.localPosition;
                _restLocalScale = shakeTarget.localScale;
            }

            if (targetRenderers == null)
            {
                _baseColors = System.Array.Empty<Color>();
                return;
            }

            _baseColors = new Color[targetRenderers.Length];
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                SpriteRenderer renderer = targetRenderers[i];
                _baseColors[i] = renderer != null ? renderer.color : Color.white;
            }
        }

        private void RestoreState()
        {
            RestoreColors();
            RestoreShake();
            RestoreScale();
        }

        private void RestoreColors()
        {
            if (targetRenderers == null || _baseColors == null)
            {
                return;
            }

            int colorCount = Mathf.Min(targetRenderers.Length, _baseColors.Length);
            for (int i = 0; i < colorCount; i++)
            {
                SpriteRenderer renderer = targetRenderers[i];
                if (renderer != null)
                {
                    renderer.color = _baseColors[i];
                }
            }
        }

        private void RestoreShake()
        {
            if (shakeTarget != null)
            {
                shakeTarget.localPosition = _restLocalPosition;
            }
        }

        private void RestoreScale()
        {
            if (shakeTarget != null)
            {
                shakeTarget.localScale = _restLocalScale;
            }
        }

        private float ResolveStateReactionMultiplier()
        {
            if (IsMovementDashActive())
            {
                return dashReactionMultiplier;
            }

            if (IsEnhancedAiStateActive())
            {
                return dashReactionMultiplier;
            }

            return 1f;
        }

        private bool IsMovementDashActive()
        {
            if (character == null || character.MovementState == null)
            {
                return false;
            }

            return character.MovementState.CurrentState == CharacterStates.MovementStates.Dashing;
        }

        private bool IsEnhancedAiStateActive()
        {
            if (brain == null || brain.CurrentState == null || enhancedReactionStateNameKeywords == null)
            {
                return false;
            }

            string currentStateName = brain.CurrentState.StateName;
            if (string.IsNullOrWhiteSpace(currentStateName))
            {
                return false;
            }

            for (int i = 0; i < enhancedReactionStateNameKeywords.Length; i++)
            {
                string keyword = enhancedReactionStateNameKeywords[i];
                if (string.IsNullOrWhiteSpace(keyword))
                {
                    continue;
                }

                if (currentStateName.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private float GetCurrentTime()
        {
            return useUnscaledTime ? Time.unscaledTime : Time.time;
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[BerthaHitReactionPresenter] " + message, this);
            }
        }
    }
}
