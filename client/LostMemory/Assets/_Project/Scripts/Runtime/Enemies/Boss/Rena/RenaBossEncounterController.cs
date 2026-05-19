using LostMemory.Stage;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Enemies.Boss.Rena
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Boss/Rena/Rena Boss Encounter Controller")]
    public sealed class RenaBossEncounterController : MonoBehaviour, IBossEncounterController
    {
        [SerializeField] private Health health;
        [SerializeField] private Animator animator;
        [SerializeField] private Behaviour[] combatBehaviours = System.Array.Empty<Behaviour>();
        [SerializeField] private GameObject[] combatObjects = System.Array.Empty<GameObject>();
        [SerializeField] private bool disableCombatOnAwake = true;
        [SerializeField] private string deathStateName = "Death";
        [SerializeField] private bool debugLogging;

        public bool IsEncounterStarted { get; private set; }

        private Health _subscribedHealth;
        private bool _isDead;

        private void Awake()
        {
            RefreshReferences();

            if (disableCombatOnAwake)
            {
                SetCombatActive(false);
            }
        }

        private void OnEnable()
        {
            RefreshReferences();
            SubscribeHealth();
        }

        private void OnDisable()
        {
            UnsubscribeHealth();
        }

        public void BeginEncounter()
        {
            if (IsEncounterStarted || _isDead)
            {
                return;
            }

            IsEncounterStarted = true;
            SetCombatActive(true);
            Log("Rena encounter started.");
        }

        public void ResetEncounter()
        {
            _isDead = false;
            IsEncounterStarted = false;
            SetCombatActive(false);
            Log("Rena encounter reset.");
        }

        private void RefreshReferences()
        {
            health ??= GetComponent<Health>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(includeInactive: true);
            }
        }

        private void SubscribeHealth()
        {
            if (_subscribedHealth == health)
            {
                return;
            }

            UnsubscribeHealth();

            if (health == null)
            {
                return;
            }

            _subscribedHealth = health;
            _subscribedHealth.OnDeath += HandleDeath;
        }

        private void UnsubscribeHealth()
        {
            if (_subscribedHealth == null)
            {
                return;
            }

            _subscribedHealth.OnDeath -= HandleDeath;
            _subscribedHealth = null;
        }

        private void HandleDeath()
        {
            _isDead = true;
            IsEncounterStarted = false;
            SetCombatActive(false);

            if (animator != null && !string.IsNullOrWhiteSpace(deathStateName))
            {
                animator.Play(deathStateName, 0, 0f);
            }

            Log("Rena encounter ended by death.");
        }

        private void SetCombatActive(bool isActive)
        {
            if (_isDead && isActive)
            {
                return;
            }

            for (int i = 0; i < combatBehaviours.Length; i++)
            {
                Behaviour behaviour = combatBehaviours[i];
                if (behaviour != null)
                {
                    behaviour.enabled = isActive;
                }
            }

            for (int i = 0; i < combatObjects.Length; i++)
            {
                GameObject combatObject = combatObjects[i];
                if (combatObject != null)
                {
                    combatObject.SetActive(isActive);
                }
            }
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[RenaBossEncounter] " + message, this);
            }
        }
    }
}
