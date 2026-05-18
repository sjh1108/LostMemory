using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;
using LostMemory.Stage;

namespace LostMemory.Enemies.Boss.Bertha
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Enemies/Boss/Bertha Boss Encounter Controller")]
    public sealed class BerthaBossEncounterController : MonoBehaviour, IBossEncounterController
    {
        [SerializeField] private AIBrain brain;
        [SerializeField] private Behaviour[] combatBehaviours = System.Array.Empty<Behaviour>();
        [SerializeField] private GameObject[] combatObjects = System.Array.Empty<GameObject>();
        [SerializeField] private bool disableCombatOnAwake = true;
        [SerializeField] private bool debugLogging;

        public bool IsEncounterStarted { get; private set; }

        private void Reset()
        {
            brain = GetComponent<AIBrain>();
        }

        private void Awake()
        {
            brain ??= GetComponent<AIBrain>();

            if (disableCombatOnAwake)
            {
                SetCombatActive(false);
            }
        }

        public void BeginEncounter()
        {
            if (IsEncounterStarted)
            {
                return;
            }

            IsEncounterStarted = true;
            SetCombatActive(true);
            Log("Bertha encounter started.");
        }

        public void ResetEncounter()
        {
            IsEncounterStarted = false;
            SetCombatActive(false);
            Log("Bertha encounter reset.");
        }

        private void SetCombatActive(bool isActive)
        {
            if (brain != null)
            {
                brain.enabled = isActive;
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
                Debug.Log("[BerthaBossEncounter] " + message, this);
            }
        }
    }
}
