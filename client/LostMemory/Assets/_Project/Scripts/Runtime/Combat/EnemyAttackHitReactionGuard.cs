using System;
using System.Collections;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Combat
{
    public interface ICancelableEnemyAttack
    {
        bool CanCancelAttack { get; }

        void CancelAttack();
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Combat/Enemy Super Armor Guard")]
    public sealed class EnemyAttackHitReactionGuard : MonoBehaviour,
        MMEventListener<MMDamageTakenEvent>,
        MMEventListener<AIStateEvent>
    {
        private const string DamageAnimatorParameterName = "Damage";

        [SerializeField] private Health health;
        [SerializeField] private AIBrain brain;
        [SerializeField] private Animator animator;
        [SerializeField] private CharacterMovement movementAbility;
        [SerializeField] private TopDownController controller;
        [SerializeField] private Rigidbody2D body2D;
        [SerializeField] private string[] protectedBrainStateNames = { "AttackTelegraph", "Attack", "RangedAttack", "Recover" };
        [SerializeField] private string[] uninterruptibleBrainStateNames = { "AttackTelegraph", "Attack", "RangedAttack", "Recover" };
        [SerializeField] private string damageTriggerName = DamageAnimatorParameterName;
        [SerializeField] private bool lockMovementDuringHitReaction;
        [SerializeField, Min(0f)] private float hitReactionMovementLockDuration = 0.6f;
        [SerializeField] private bool cancelAttackOnHitReaction = true;
        [SerializeField] private bool pauseBrainDuringHitReaction = true;
        [SerializeField] private bool freezeMovementSpeedDuringHitReaction = true;
        [SerializeField] private bool showSuperArmorVisualEffect;
        [SerializeField] private EnemySuperArmorOutlineEffect superArmorVisualEffect;
        [SerializeField] private bool useTimedSuperArmor;
        [SerializeField, Min(0f)] private float superArmorDuration = 5f;
        [SerializeField, Min(0f)] private float superArmorCooldown = 10f;

        private Health subscribedHealth;
        private Animator suppressedTargetAnimator;
        private Coroutine restoreRoutine;
        private Coroutine hitReactionMovementLockRoutine;
        private bool suppressingTargetAnimator;
        private bool movementLockedForHitReaction;
        private bool movementForbiddenBeforeHitReaction;
        private bool movementSpeedFrozenForHitReaction;
        private float movementSpeedMultiplierBeforeHitReaction;
        private bool brainPausedForHitReaction;
        private bool brainActiveBeforeHitReaction;
        private bool superArmorVisualActive;
        private bool timedSuperArmorActive;
        private float superArmorExpiresAt;
        private float superArmorCooldownEndsAt;

        public bool IsSuperArmorActive => IsSuperArmorActiveNow();

        private void Reset()
        {
            RefreshReferences();
        }

        private void Awake()
        {
            RefreshReferences();
        }

        private void OnEnable()
        {
            RefreshReferences();
            ResetTimedSuperArmor();
            this.MMEventStartListening<MMDamageTakenEvent>();
            this.MMEventStartListening<AIStateEvent>();
            SubscribeDeath();
            RefreshSuperArmorState();
        }

        private void OnDisable()
        {
            this.MMEventStopListening<MMDamageTakenEvent>();
            this.MMEventStopListening<AIStateEvent>();
            UnsubscribeDeath();
            StopRestoreRoutine();
            RestoreTargetAnimator();
            StopHitReactionMovementLockRoutine();
            RestoreMovementAfterHitReaction();
            ResetTimedSuperArmor();
            SetSuperArmorVisualActive(false);
        }

        private void Update()
        {
            if (movementLockedForHitReaction)
            {
                ForceStopHitReactionMovement();
            }

            RefreshSuperArmorState();
        }

        private void FixedUpdate()
        {
            if (movementLockedForHitReaction)
            {
                ForceStopHitReactionMovement();
            }
        }

        private void LateUpdate()
        {
            if (movementLockedForHitReaction)
            {
                ForceStopHitReactionMovement();
            }
        }

        public void Configure(
            Health configuredHealth,
            AIBrain configuredBrain,
            Animator configuredAnimator,
            EnemySuperArmorOutlineEffect configuredSuperArmorVisualEffect,
            params string[] configuredProtectedBrainStateNames)
        {
            health = configuredHealth;
            brain = configuredBrain;
            animator = configuredAnimator;
            superArmorVisualEffect = configuredSuperArmorVisualEffect;

            if (configuredProtectedBrainStateNames != null && configuredProtectedBrainStateNames.Length > 0)
            {
                protectedBrainStateNames = configuredProtectedBrainStateNames;
            }

            RefreshReferences();
            SubscribeDeath();
            RefreshSuperArmorState();
        }

        public void Configure(
            Health configuredHealth,
            AIBrain configuredBrain,
            Animator configuredAnimator,
            params string[] configuredProtectedBrainStateNames)
        {
            Configure(configuredHealth, configuredBrain, configuredAnimator, null, configuredProtectedBrainStateNames);
        }

        public void OnMMEvent(MMDamageTakenEvent damageEvent)
        {
            if (health == null || damageEvent.AffectedHealth != health)
            {
                return;
            }

            if (damageEvent.CurrentHealth <= 0f)
            {
                StopRestoreRoutine();
                RestoreTargetAnimator();
                StopHitReactionMovementLockRoutine();
                RestoreMovementAfterHitReaction(restoreBrainActive: false);
                ClearDamageTrigger();
                ResetTimedSuperArmor();
                SetSuperArmorVisualActive(false);
                return;
            }

            RefreshSuperArmorState();

            if (IsInUninterruptibleBrainState())
            {
                SuppressTargetAnimatorUntilNextFrame();
                ClearDamageTrigger();
                return;
            }

            if (!IsSuperArmorActiveNow())
            {
                CancelAttacksForHitReaction();

                if (movementLockedForHitReaction)
                {
                    // Health sets the Damage trigger after this event; prevent queued Hurt replays.
                    SuppressTargetAnimatorUntilNextFrame();
                    ForceStopHitReactionMovement();
                    return;
                }

                LockMovementForHitReaction();
                return;
            }

            SetSuperArmorVisualActive(true);
            SuppressTargetAnimatorUntilNextFrame();
            ClearDamageTrigger();
        }

        public void OnMMEvent(AIStateEvent stateEvent)
        {
            if (brain == null || stateEvent.Brain != brain)
            {
                return;
            }

            string enteringState = stateEvent.EnterState != null ? stateEvent.EnterState.StateName : string.Empty;
            string exitingState = stateEvent.ExitState != null ? stateEvent.ExitState.StateName : string.Empty;

            if (IsUninterruptibleStateName(enteringState) && (health == null || health.CurrentHealth > 0f))
            {
                ClearDamageTrigger();
            }

            if (IsProtectedStateName(enteringState) && (health == null || health.CurrentHealth > 0f))
            {
                bool isSuperArmorActive = ResolveSuperArmorActive(isEligibleForSuperArmor: true);
                SetSuperArmorVisualActive(isSuperArmorActive);

                if (isSuperArmorActive)
                {
                    ClearDamageTrigger();
                }

                return;
            }

            if (IsProtectedStateName(exitingState))
            {
                RestoreTargetAnimator();
                EndTimedSuperArmor(startCooldown: true);
                SetSuperArmorVisualActive(false);
            }
        }

        private void RefreshReferences()
        {
            health ??= GetComponent<Health>();
            brain ??= GetComponent<AIBrain>();
            movementAbility ??= GetComponent<CharacterMovement>();
            controller ??= GetComponent<TopDownController>();
            body2D ??= GetComponent<Rigidbody2D>();
            superArmorVisualEffect ??= GetComponent<EnemySuperArmorOutlineEffect>();

            if (animator != null)
            {
                return;
            }

            if (health != null && health.TargetAnimator != null)
            {
                animator = health.TargetAnimator;
                return;
            }

            Character character = GetComponent<Character>();
            if (character != null && character.CharacterAnimator != null)
            {
                animator = character.CharacterAnimator;
                return;
            }

            animator = GetComponentInChildren<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(includeInactive: true);
            }
        }

        private void SubscribeDeath()
        {
            if (!isActiveAndEnabled || subscribedHealth == health)
            {
                return;
            }

            UnsubscribeDeath();

            if (health == null)
            {
                return;
            }

            subscribedHealth = health;
            subscribedHealth.OnDeath += HandleDeath;
        }

        private void UnsubscribeDeath()
        {
            if (subscribedHealth == null)
            {
                return;
            }

            subscribedHealth.OnDeath -= HandleDeath;
            subscribedHealth = null;
        }

        private void HandleDeath()
        {
            StopRestoreRoutine();
            RestoreTargetAnimator();
            StopHitReactionMovementLockRoutine();
            RestoreMovementAfterHitReaction(restoreBrainActive: false);
            ClearDamageTrigger();
            ResetTimedSuperArmor();
            SetSuperArmorVisualActive(false);
        }

        private bool IsInProtectedBrainState()
        {
            string stateName = brain != null && brain.CurrentState != null
                ? brain.CurrentState.StateName
                : string.Empty;
            return IsProtectedStateName(stateName);
        }

        private bool IsInUninterruptibleBrainState()
        {
            string stateName = brain != null && brain.CurrentState != null
                ? brain.CurrentState.StateName
                : string.Empty;
            return IsUninterruptibleStateName(stateName);
        }

        private bool IsProtectedStateName(string stateName)
        {
            return ContainsStateName(stateName, protectedBrainStateNames);
        }

        private bool IsUninterruptibleStateName(string stateName)
        {
            return ContainsStateName(stateName, uninterruptibleBrainStateNames);
        }

        private static bool ContainsStateName(string stateName, string[] stateNames)
        {
            if (string.IsNullOrEmpty(stateName) || stateNames == null)
            {
                return false;
            }

            for (int i = 0; i < stateNames.Length; i++)
            {
                if (string.Equals(stateName, stateNames[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshSuperArmorState()
        {
            if (movementLockedForHitReaction)
            {
                SetSuperArmorVisualActive(false);
                return;
            }

            bool isSuperArmorActive = ResolveSuperArmorActive(IsEligibleForSuperArmorNow());
            SetSuperArmorVisualActive(isSuperArmorActive);

            if (isSuperArmorActive)
            {
                ClearDamageTrigger();
            }

            if (IsInUninterruptibleBrainState())
            {
                ClearDamageTrigger();
            }
        }

        private bool IsSuperArmorActiveNow()
        {
            if (!IsEligibleForSuperArmorNow())
            {
                return false;
            }

            if (!useTimedSuperArmor)
            {
                return true;
            }

            return timedSuperArmorActive && Time.time < superArmorExpiresAt;
        }

        private bool IsEligibleForSuperArmorNow()
        {
            return IsInProtectedBrainState() && (health == null || health.CurrentHealth > 0f);
        }

        private bool ResolveSuperArmorActive(bool isEligibleForSuperArmor)
        {
            if (!isEligibleForSuperArmor)
            {
                EndTimedSuperArmor(startCooldown: true);
                return false;
            }

            if (!useTimedSuperArmor)
            {
                return true;
            }

            return RefreshTimedSuperArmor();
        }

        private bool RefreshTimedSuperArmor()
        {
            float now = Time.time;

            if (timedSuperArmorActive)
            {
                if (now < superArmorExpiresAt)
                {
                    return true;
                }

                EndTimedSuperArmor(startCooldown: true);
                return false;
            }

            if (now < superArmorCooldownEndsAt)
            {
                return false;
            }

            StartTimedSuperArmor();
            return timedSuperArmorActive;
        }

        private void StartTimedSuperArmor()
        {
            float duration = Mathf.Max(0f, superArmorDuration);
            if (duration <= 0f)
            {
                timedSuperArmorActive = false;
                superArmorExpiresAt = 0f;
                superArmorCooldownEndsAt = Time.time + Mathf.Max(0f, superArmorCooldown);
                return;
            }

            timedSuperArmorActive = true;
            superArmorExpiresAt = Time.time + duration;
        }

        private void EndTimedSuperArmor(bool startCooldown)
        {
            if (!timedSuperArmorActive)
            {
                return;
            }

            timedSuperArmorActive = false;
            superArmorExpiresAt = 0f;

            if (startCooldown)
            {
                superArmorCooldownEndsAt = Time.time + Mathf.Max(0f, superArmorCooldown);
            }
        }

        private void ResetTimedSuperArmor()
        {
            timedSuperArmorActive = false;
            superArmorExpiresAt = 0f;
            superArmorCooldownEndsAt = 0f;
        }

        private void SetSuperArmorVisualActive(bool active)
        {
            if (superArmorVisualActive == active && !active)
            {
                return;
            }

            superArmorVisualActive = active;

            if (!showSuperArmorVisualEffect || superArmorVisualEffect == null)
            {
                return;
            }

            superArmorVisualEffect.SetVisible(active);
        }

        private void ClearDamageTrigger()
        {
            if (animator != null && HasTrigger(animator, damageTriggerName))
            {
                animator.ResetTrigger(damageTriggerName);
            }
        }

        private void SuppressTargetAnimatorUntilNextFrame()
        {
            if (health == null || health.TargetAnimator == null)
            {
                return;
            }

            suppressedTargetAnimator = health.TargetAnimator;
            health.TargetAnimator = null;
            suppressingTargetAnimator = true;

            StopRestoreRoutine();
            restoreRoutine = StartCoroutine(RestoreTargetAnimatorAfterFrame());
        }

        private IEnumerator RestoreTargetAnimatorAfterFrame()
        {
            yield return null;
            restoreRoutine = null;
            RestoreTargetAnimator();
        }

        private void RestoreTargetAnimator()
        {
            if (!suppressingTargetAnimator)
            {
                return;
            }

            if (health != null && health.TargetAnimator == null && suppressedTargetAnimator != null)
            {
                health.TargetAnimator = suppressedTargetAnimator;
            }

            suppressedTargetAnimator = null;
            suppressingTargetAnimator = false;
        }

        private void StopRestoreRoutine()
        {
            if (restoreRoutine == null)
            {
                return;
            }

            StopCoroutine(restoreRoutine);
            restoreRoutine = null;
        }

        private void CancelAttacksForHitReaction()
        {
            if (!cancelAttackOnHitReaction)
            {
                return;
            }

            MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || behaviour == this)
                {
                    continue;
                }

                if (behaviour is ICancelableEnemyAttack cancelableAttack
                    && cancelableAttack.CanCancelAttack)
                {
                    cancelableAttack.CancelAttack();
                }
            }
        }

        private void LockMovementForHitReaction()
        {
            if (!lockMovementDuringHitReaction
                || hitReactionMovementLockDuration <= 0f)
            {
                return;
            }

            if (movementAbility == null && controller == null && body2D == null)
            {
                return;
            }

            if (!movementLockedForHitReaction)
            {
                if (movementAbility != null)
                {
                    movementForbiddenBeforeHitReaction = movementAbility.MovementForbidden;
                    movementSpeedMultiplierBeforeHitReaction = movementAbility.MovementSpeedMultiplier;
                    movementSpeedFrozenForHitReaction = freezeMovementSpeedDuringHitReaction;
                }

                if (pauseBrainDuringHitReaction && brain != null)
                {
                    brainActiveBeforeHitReaction = brain.BrainActive;
                    brain.BrainActive = false;
                    brainPausedForHitReaction = true;
                }
            }

            movementLockedForHitReaction = true;
            ForceStopHitReactionMovement();

            StopHitReactionMovementLockRoutine();
            hitReactionMovementLockRoutine = StartCoroutine(RestoreMovementAfterHitReactionDelay());
        }

        private IEnumerator RestoreMovementAfterHitReactionDelay()
        {
            yield return new WaitForSeconds(hitReactionMovementLockDuration);
            hitReactionMovementLockRoutine = null;
            RestoreMovementAfterHitReaction();
        }

        private void RestoreMovementAfterHitReaction(bool restoreBrainActive = true)
        {
            if (!movementLockedForHitReaction)
            {
                RestoreBrainAfterHitReaction(restoreBrainActive);
                return;
            }

            ForceStopHitReactionMovement();

            if (movementAbility != null)
            {
                movementAbility.MovementForbidden = movementForbiddenBeforeHitReaction;

                if (movementSpeedFrozenForHitReaction)
                {
                    movementAbility.MovementSpeedMultiplier = movementSpeedMultiplierBeforeHitReaction;
                }
            }

            movementLockedForHitReaction = false;
            movementForbiddenBeforeHitReaction = false;
            movementSpeedFrozenForHitReaction = false;
            movementSpeedMultiplierBeforeHitReaction = 0f;

            RestoreBrainAfterHitReaction(restoreBrainActive);
        }

        private void ForceStopHitReactionMovement()
        {
            if (movementAbility != null)
            {
                movementAbility.SetMovement(Vector2.zero);
                movementAbility.MovementForbidden = true;

                if (freezeMovementSpeedDuringHitReaction)
                {
                    movementAbility.MovementSpeedMultiplier = 0f;
                }
            }

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

            if (body2D != null)
            {
                body2D.linearVelocity = Vector2.zero;
                body2D.angularVelocity = 0f;
            }
        }

        private void RestoreBrainAfterHitReaction(bool restoreBrainActive)
        {
            if (!brainPausedForHitReaction)
            {
                return;
            }

            if (restoreBrainActive && brain != null)
            {
                brain.BrainActive = brainActiveBeforeHitReaction;
            }

            brainPausedForHitReaction = false;
            brainActiveBeforeHitReaction = false;
        }

        private void StopHitReactionMovementLockRoutine()
        {
            if (hitReactionMovementLockRoutine == null)
            {
                return;
            }

            StopCoroutine(hitReactionMovementLockRoutine);
            hitReactionMovementLockRoutine = null;
        }

        private static bool HasTrigger(Animator targetAnimator, string parameterName)
        {
            if (targetAnimator == null || string.IsNullOrEmpty(parameterName))
            {
                return false;
            }

            AnimatorControllerParameter[] parameters = targetAnimator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (parameter.type == AnimatorControllerParameterType.Trigger
                    && parameter.name == parameterName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
