using System.Collections;
using System.Collections.Generic;
using LostMemory.Stage;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies.Boss.Bertha
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Boss/Bertha/Bertha Health Threshold Reaction Controller")]
    [DefaultExecutionOrder(320)]
    public sealed class BerthaHealthThresholdReactionController : MonoBehaviour
    {
        private enum ReactionType
        {
            None = 0,
            Stun = 1,
            Tired = 2
        }

        [SerializeField] private AIBrain brain;
        [SerializeField] private Character character;
        [SerializeField] private Health health;
        [SerializeField] private CharacterDash2D dashAbility;
        [SerializeField] private CharacterMovement movementAbility;
        [SerializeField] private TopDownController2D controller;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Animator animator;
        [SerializeField] private BossIntroSequenceController introSequenceController;
        [SerializeField] private string neutralBrainStateName = "Detecting";
        [SerializeField, Min(0)] private int animationLayer;

        [Header("Thresholds")]
        [SerializeField, Range(0f, 1f)] private float stunThresholdNormalized = 0.7f;
        [SerializeField, Range(0f, 1f)] private float tiredThresholdNormalized = 0.3f;

        [Header("Stun")]
        [SerializeField] private string toStunStateName = "ToStun";
        [SerializeField] private string stunedStateName = "Stuned";
        [SerializeField] private string shakeHeadStateName = "ShakeHead";
        [SerializeField] private string outStunStateName = "OutStun";
        [SerializeField, Min(0f)] private float stunLoopDuration = 1.1f;
        [SerializeField, Min(0f)] private float shakeHeadDuration = 5f;

        [Header("Tired")]
        [SerializeField] private string tiredStateName = "Tired";
        [SerializeField, Min(0f)] private float tiredDuration = 1.35f;

        [Header("Reaction Lock")]
        [SerializeField] private bool invulnerableDuringReaction = true;
        [SerializeField, Min(0f)] private float reactionExitSettleDuration = 0.12f;

        [Header("Debug")]
        [SerializeField] private bool debugLogging;

        private readonly Dictionary<string, float> _animationStateDurations = new Dictionary<string, float>(8);
        private readonly Queue<ReactionType> _pendingReactions = new Queue<ReactionType>(2);

        private Coroutine _reactionRoutine;
        private bool _stunQueuedOrPlayed;
        private bool _tiredQueuedOrPlayed;
        private bool _reactionLocked;
        private bool _frozenByReaction;
        private bool _brainWasActive;
        private bool _healthWasInvulnerable;
        private bool _appliedReactionInvulnerability;

        public bool IsReactionInvulnerabilityActive => _reactionLocked && _appliedReactionInvulnerability;

        private void Reset()
        {
            RefreshReferences();
            NormalizeThresholds();
        }

        private void OnValidate()
        {
            RefreshReferences();
            NormalizeThresholds();
            CacheAnimationStateDurations();
        }

        private void Awake()
        {
            RefreshReferences();
            NormalizeThresholds();
            CacheAnimationStateDurations();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            RefreshReferences();
            CacheAnimationStateDurations();

            if (health != null)
            {
                health.OnHit += HandleHit;
                health.OnDeath += HandleDeath;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.OnHit -= HandleHit;
                health.OnDeath -= HandleDeath;
            }

            ClearPendingReactions();
            StopActiveReaction(restoreState: health == null || health.CurrentHealth > 0f);
        }

        public void Configure(
            AIBrain configuredBrain,
            Character configuredCharacter,
            Health configuredHealth,
            CharacterDash2D configuredDashAbility,
            Animator configuredAnimator,
            BossIntroSequenceController configuredIntroSequenceController,
            float configuredStunThresholdNormalized,
            float configuredTiredThresholdNormalized,
            float configuredStunLoopDuration,
            float configuredShakeHeadDuration,
            float configuredTiredDuration,
            bool configuredDebugLogging,
            bool configuredInvulnerableDuringReaction = true,
            float configuredReactionExitSettleDuration = 0.12f)
        {
            brain = configuredBrain;
            character = configuredCharacter;
            health = configuredHealth;
            dashAbility = configuredDashAbility;
            animator = configuredAnimator;
            introSequenceController = configuredIntroSequenceController;
            stunThresholdNormalized = Mathf.Clamp01(configuredStunThresholdNormalized);
            tiredThresholdNormalized = Mathf.Clamp01(configuredTiredThresholdNormalized);
            stunLoopDuration = Mathf.Max(0f, configuredStunLoopDuration);
            shakeHeadDuration = Mathf.Max(0f, configuredShakeHeadDuration);
            tiredDuration = Mathf.Max(0f, configuredTiredDuration);
            invulnerableDuringReaction = configuredInvulnerableDuringReaction;
            reactionExitSettleDuration = Mathf.Max(0f, configuredReactionExitSettleDuration);
            debugLogging = configuredDebugLogging;

            RefreshReferences();
            NormalizeThresholds();
            CacheAnimationStateDurations();
        }

        private void HandleHit()
        {
            if (health == null || health.CurrentHealth <= 0f)
            {
                return;
            }

            float normalizedHealth = GetNormalizedHealth();
            bool queuedReaction = false;

            if (!_stunQueuedOrPlayed && normalizedHealth <= stunThresholdNormalized)
            {
                EnqueueReaction(ReactionType.Stun);
                _stunQueuedOrPlayed = true;
                queuedReaction = true;
            }

            if (!_tiredQueuedOrPlayed && normalizedHealth <= tiredThresholdNormalized)
            {
                EnqueueReaction(ReactionType.Tired);
                _tiredQueuedOrPlayed = true;
                queuedReaction = true;
            }

            if (!queuedReaction)
            {
                return;
            }

            Log("Queued threshold reaction at normalized health " + normalizedHealth.ToString("0.00") + ".");
            TryStartReactionSequence();
        }

        private void HandleDeath()
        {
            ClearPendingReactions();
            StopActiveReaction(restoreState: false);

            if (brain != null)
            {
                brain.BrainActive = false;
            }

            if (dashAbility != null)
            {
                dashAbility.DashStop();
            }

            if (_frozenByReaction && character != null)
            {
                character.UnFreeze();
                _frozenByReaction = false;
            }

            Log("Stopped threshold reactions on death.");
        }

        private void TryStartReactionSequence()
        {
            if (!isActiveAndEnabled || _reactionRoutine != null || _pendingReactions.Count == 0)
            {
                return;
            }

            _reactionRoutine = StartCoroutine(RunReactionSequence());
        }

        private IEnumerator RunReactionSequence()
        {
            while (_pendingReactions.Count > 0)
            {
                if (health == null || health.CurrentHealth <= 0f)
                {
                    break;
                }

                if (introSequenceController != null && introSequenceController.IsIntroRunning)
                {
                    yield return null;
                    continue;
                }

                ReactionType reaction = _pendingReactions.Dequeue();
                BeginReaction();
                yield return PlayReaction(reaction);

                if (health == null || health.CurrentHealth <= 0f)
                {
                    break;
                }

                yield return EndReactionRoutine();
                yield return null;
            }

            _reactionRoutine = null;
        }

        private void BeginReaction()
        {
            RefreshReferences();
            CacheAnimationStateDurations();
            _reactionLocked = true;
            ApplyReactionInvulnerability();

            if (dashAbility != null)
            {
                dashAbility.DashStop();
            }

            StopReactionMotion();

            if (brain != null)
            {
                _brainWasActive = brain.BrainActive;
                if (!string.IsNullOrWhiteSpace(neutralBrainStateName))
                {
                    brain.TransitionToState(neutralBrainStateName);
                }

                brain.BrainActive = false;
            }
            else
            {
                _brainWasActive = false;
            }

            _frozenByReaction = false;
            if (character != null
                && character.ConditionState.CurrentState != CharacterStates.CharacterConditions.Frozen)
            {
                character.Freeze();
                _frozenByReaction = true;
            }
        }

        private IEnumerator EndReactionRoutine()
        {
            if (!_reactionLocked)
            {
                yield break;
            }

            if (_frozenByReaction && character != null)
            {
                character.UnFreeze();
                _frozenByReaction = false;
            }

            yield return WaitForExitSettle();

            if (brain != null)
            {
                brain.BrainActive = _brainWasActive;
                if (brain.BrainActive && !string.IsNullOrWhiteSpace(neutralBrainStateName))
                {
                    brain.TransitionToState(neutralBrainStateName);
                }
            }

            RestoreReactionInvulnerability();
            _reactionLocked = false;
        }

        private void EndReactionImmediately()
        {
            if (!_reactionLocked)
            {
                return;
            }

            if (_frozenByReaction && character != null)
            {
                character.UnFreeze();
                _frozenByReaction = false;
            }

            if (brain != null)
            {
                brain.BrainActive = _brainWasActive;
                if (brain.BrainActive && !string.IsNullOrWhiteSpace(neutralBrainStateName))
                {
                    brain.TransitionToState(neutralBrainStateName);
                }
            }

            RestoreReactionInvulnerability();
            _reactionLocked = false;
        }

        private IEnumerator PlayReaction(ReactionType reaction)
        {
            switch (reaction)
            {
                case ReactionType.Stun:
                    Log("Play Stun threshold reaction.");
                    yield return PlayAnimationStateAndWait(toStunStateName, ResolveAnimationStateDuration(toStunStateName));
                    yield return PlayAnimationStateAndWait(stunedStateName, stunLoopDuration);
                    yield return PlayAnimationStateAndWait(shakeHeadStateName, shakeHeadDuration);
                    yield return PlayAnimationStateAndWait(outStunStateName, ResolveAnimationStateDuration(outStunStateName));
                    break;

                case ReactionType.Tired:
                    Log("Play Tired threshold reaction.");
                    yield return PlayAnimationStateAndWait(tiredStateName, tiredDuration);
                    break;
            }
        }

        private IEnumerator PlayAnimationStateAndWait(string stateName, float duration)
        {
            if (animator != null && !string.IsNullOrWhiteSpace(stateName))
            {
                animator.Play(stateName, animationLayer, 0f);
            }

            if (duration <= 0f)
            {
                yield return null;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (health == null || health.CurrentHealth <= 0f)
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                MaintainReactionInvulnerability();
                StopReactionMotion();
                yield return null;
            }
        }

        private IEnumerator WaitForExitSettle()
        {
            if (reactionExitSettleDuration <= 0f)
            {
                StopReactionMotion();
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < reactionExitSettleDuration)
            {
                if (health == null || health.CurrentHealth <= 0f)
                {
                    yield break;
                }

                StopReactionMotion();
                MaintainReactionInvulnerability();
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private float ResolveAnimationStateDuration(string stateName)
        {
            if (string.IsNullOrWhiteSpace(stateName))
            {
                return 0f;
            }

            return _animationStateDurations.TryGetValue(stateName, out float duration)
                ? duration
                : 0f;
        }

        private void CacheAnimationStateDurations()
        {
            _animationStateDurations.Clear();

            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip clip = clips[i];
                if (clip == null || string.IsNullOrWhiteSpace(clip.name))
                {
                    continue;
                }

                _animationStateDurations[clip.name] = clip.length;
            }
        }

        private void RefreshReferences()
        {
            brain ??= GetComponent<AIBrain>();
            character ??= GetComponent<Character>();
            health ??= GetComponent<Health>();
            dashAbility ??= GetComponent<CharacterDash2D>();
            movementAbility ??= GetComponent<CharacterMovement>();
            controller ??= GetComponent<TopDownController2D>();
            body ??= GetComponent<Rigidbody2D>();
            introSequenceController ??= GetComponent<BossIntroSequenceController>();

            if (animator == null)
            {
                animator = introSequenceController != null
                    ? introSequenceController.BossAnimator
                    : GetComponentInChildren<Animator>(true);
            }
        }

        private void NormalizeThresholds()
        {
            stunThresholdNormalized = Mathf.Clamp01(stunThresholdNormalized);
            tiredThresholdNormalized = Mathf.Clamp01(tiredThresholdNormalized);
            tiredThresholdNormalized = Mathf.Min(tiredThresholdNormalized, stunThresholdNormalized);
            stunLoopDuration = Mathf.Max(0f, stunLoopDuration);
            shakeHeadDuration = Mathf.Max(0f, shakeHeadDuration);
            tiredDuration = Mathf.Max(0f, tiredDuration);
            reactionExitSettleDuration = Mathf.Max(0f, reactionExitSettleDuration);
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

        private void EnqueueReaction(ReactionType reaction)
        {
            if (reaction == ReactionType.None)
            {
                return;
            }

            _pendingReactions.Enqueue(reaction);
        }

        private void ClearPendingReactions()
        {
            _pendingReactions.Clear();
        }

        private void StopActiveReaction(bool restoreState)
        {
            if (_reactionRoutine != null)
            {
                StopCoroutine(_reactionRoutine);
                _reactionRoutine = null;
            }

            if (!restoreState)
            {
                RestoreReactionInvulnerability();
                _reactionLocked = false;
                return;
            }

            EndReactionImmediately();
        }

        private void ApplyReactionInvulnerability()
        {
            if (!invulnerableDuringReaction || health == null || _appliedReactionInvulnerability)
            {
                return;
            }

            _healthWasInvulnerable = health.Invulnerable;
            health.DamageDisabled();
            _appliedReactionInvulnerability = true;
        }

        private void MaintainReactionInvulnerability()
        {
            if (!_appliedReactionInvulnerability || health == null)
            {
                return;
            }

            health.Invulnerable = true;
        }

        private void RestoreReactionInvulnerability()
        {
            if (!_appliedReactionInvulnerability || health == null)
            {
                _appliedReactionInvulnerability = false;
                return;
            }

            health.Invulnerable = _healthWasInvulnerable;
            _appliedReactionInvulnerability = false;
        }

        private void StopReactionMotion()
        {
            if (dashAbility != null)
            {
                dashAbility.DashStop();
            }

            movementAbility?.SetMovement(Vector2.zero);

            if (controller != null)
            {
                controller.SetMovement(Vector3.zero);
                controller.CurrentMovement = Vector3.zero;
                controller.Speed = Vector3.zero;
                controller.Velocity = Vector3.zero;
                controller.VelocityLastFrame = Vector3.zero;
                controller.Acceleration = Vector3.zero;
                controller.AddedForce = Vector3.zero;
            }

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[BerthaThresholdReaction] " + message, this);
            }
        }
    }
}
