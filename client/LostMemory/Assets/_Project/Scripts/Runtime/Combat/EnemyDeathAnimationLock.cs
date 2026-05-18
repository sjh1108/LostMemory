using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Combat
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Combat/Enemy Death Animation Lock")]
    [DefaultExecutionOrder(320)]
    public sealed class EnemyDeathAnimationLock : MonoBehaviour
    {
        private const string DefaultAnimatorLayerName = "Base Layer";
        private const string DeathParameterName = "Death";

        [SerializeField] private Health health;
        [SerializeField] private Animator animator;
        [SerializeField] private string deathStateName = string.Empty;
        [SerializeField] private string[] triggerParametersToReset = { "Damage" };
        [SerializeField] private string[] boolParametersToDisable = { "Attack", "Walking" };
        [SerializeField] private bool inferDeathStateFromControllerName = true;
        [SerializeField] private bool forceImmediateStatePlay = true;
        [SerializeField] private bool forceAnimatorUpdate = true;
        [SerializeField] private bool keepDeathStateLocked = true;

        private Health subscribedHealth;
        private bool deathStateLocked;

        public static EnemyDeathAnimationLock EnsureOn(
            GameObject host,
            Health configuredHealth = null,
            Animator configuredAnimator = null)
        {
            if (host == null)
            {
                return null;
            }

            Health resolvedHealth = configuredHealth != null
                ? configuredHealth
                : host.GetComponent<Health>();
            Animator resolvedAnimator = ResolveAnimator(host, resolvedHealth, configuredAnimator);

            if (resolvedHealth == null || resolvedAnimator == null)
            {
                return null;
            }

            EnemyDeathAnimationLock animationLock = host.GetComponent<EnemyDeathAnimationLock>();
            if (animationLock == null)
            {
                animationLock = host.AddComponent<EnemyDeathAnimationLock>();
            }

            animationLock.Configure(resolvedHealth, resolvedAnimator);
            return animationLock;
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void Reset()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (health != null && health.CurrentHealth > 0f)
            {
                deathStateLocked = false;
            }

            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void LateUpdate()
        {
            if (!deathStateLocked
                || !keepDeathStateLocked
                || animator == null
                || !animator.isActiveAndEnabled
                || !TryGetDeathStateHash(out int deathStateHash)
                || IsStatePlaying(deathStateHash))
            {
                return;
            }

            animator.Play(deathStateHash, 0, 0f);

            if (forceAnimatorUpdate)
            {
                animator.Update(0f);
            }
        }

        public void Configure(Health configuredHealth, Animator configuredAnimator)
        {
            health = configuredHealth;
            animator = configuredAnimator;

            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        private void Subscribe()
        {
            if (subscribedHealth == health)
            {
                return;
            }

            Unsubscribe();

            if (health == null)
            {
                return;
            }

            subscribedHealth = health;
            subscribedHealth.OnDeath += HandleDeath;
        }

        private void Unsubscribe()
        {
            if (subscribedHealth == null)
            {
                return;
            }

            subscribedHealth.OnDeath -= HandleDeath;
            subscribedHealth = null;
        }

        private void ResolveReferences()
        {
            if (health == null)
            {
                health = GetComponent<Health>();
            }

            animator = ResolveAnimator(gameObject, health, animator);
        }

        private void HandleDeath()
        {
            ResolveReferences();
            deathStateLocked = true;

            if (animator == null || !animator.isActiveAndEnabled)
            {
                return;
            }

            ResetTriggers();
            DisableBools();
            SetTriggerIfPresent(DeathParameterName);

            if (forceImmediateStatePlay && TryGetDeathStateHash(out int deathStateHash))
            {
                animator.Play(deathStateHash, 0, 0f);
            }

            if (forceAnimatorUpdate)
            {
                animator.Update(0f);
            }
        }

        private void ResetTriggers()
        {
            if (triggerParametersToReset == null)
            {
                return;
            }

            for (int i = 0; i < triggerParametersToReset.Length; i++)
            {
                string parameterName = triggerParametersToReset[i];
                if (HasParameter(parameterName, AnimatorControllerParameterType.Trigger))
                {
                    animator.ResetTrigger(parameterName);
                }
            }
        }

        private void DisableBools()
        {
            if (boolParametersToDisable == null)
            {
                return;
            }

            for (int i = 0; i < boolParametersToDisable.Length; i++)
            {
                string parameterName = boolParametersToDisable[i];
                if (HasParameter(parameterName, AnimatorControllerParameterType.Bool))
                {
                    animator.SetBool(parameterName, false);
                }
            }
        }

        private void SetTriggerIfPresent(string parameterName)
        {
            if (HasParameter(parameterName, AnimatorControllerParameterType.Trigger))
            {
                animator.SetTrigger(parameterName);
            }
        }

        private bool TryGetDeathStateHash(out int stateHash)
        {
            if (TryGetStateHash(deathStateName, out stateHash))
            {
                return true;
            }

            if (inferDeathStateFromControllerName
                && animator.runtimeAnimatorController != null
                && TryGetStateHash(animator.runtimeAnimatorController.name + "_Death", out stateHash))
            {
                return true;
            }

            return TryGetStateHash(DeathParameterName, out stateHash);
        }

        private bool TryGetStateHash(string stateName, out int stateHash)
        {
            stateHash = 0;
            if (string.IsNullOrWhiteSpace(stateName))
            {
                return false;
            }

            stateHash = Animator.StringToHash(DefaultAnimatorLayerName + "." + stateName);
            if (animator.HasState(0, stateHash))
            {
                return true;
            }

            stateHash = Animator.StringToHash(stateName);
            return animator.HasState(0, stateHash);
        }

        private bool IsStatePlaying(int stateHash)
        {
            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
            if (currentState.fullPathHash == stateHash || currentState.shortNameHash == stateHash)
            {
                return true;
            }

            if (!animator.IsInTransition(0))
            {
                return false;
            }

            AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(0);
            return nextState.fullPathHash == stateHash || nextState.shortNameHash == stateHash;
        }

        private static Animator ResolveAnimator(
            GameObject host,
            Health resolvedHealth,
            Animator preferredAnimator)
        {
            if (preferredAnimator != null)
            {
                return preferredAnimator;
            }

            if (resolvedHealth != null && resolvedHealth.TargetAnimator != null)
            {
                return resolvedHealth.TargetAnimator;
            }

            return host != null
                ? host.GetComponentInChildren<Animator>(includeInactive: true)
                : null;
        }

        private bool HasParameter(string parameterName, AnimatorControllerParameterType parameterType)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                return false;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (parameter.type == parameterType && parameter.name == parameterName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
