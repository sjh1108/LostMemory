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

        private Health subscribedHealth;

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
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
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

            if (animator == null && health != null && health.TargetAnimator != null)
            {
                animator = health.TargetAnimator;
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(includeInactive: true);
            }
        }

        private void HandleDeath()
        {
            ResolveReferences();

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
