using System.Collections.Generic;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LostMemory.SceneFlow
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    [AddComponentMenu("Lost Memory/Scene Flow/Scene Load Portal Controller")]
    public sealed class SceneLoadPortalController : MonoBehaviour
    {
        [SerializeField] private string targetSceneName = "BossClear";
        [SerializeField] private LoadSceneMode loadSceneMode = LoadSceneMode.Single;
        [SerializeField] private bool requireInteractInput = true;
        [SerializeField] private KeyCode fallbackInteractKey = KeyCode.E;
        [SerializeField] private string acceptedPlayerId = "Player1";
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private bool debugLogging;

        private readonly HashSet<Character> _candidates = new HashSet<Character>();
        private Collider2D _trigger;
        private bool _loadInProgress;

        public void Configure(string sceneName, bool requireInput, KeyCode interactKey, string playerId)
        {
            targetSceneName = sceneName;
            requireInteractInput = requireInput;
            fallbackInteractKey = interactKey;
            acceptedPlayerId = playerId;
            RefreshReferences();
            ConfigureTrigger();
        }

        private void Reset()
        {
            RefreshReferences();
            ConfigureTrigger();
        }

        private void OnValidate()
        {
            RefreshReferences();
            ConfigureTrigger();
        }

        private void Awake()
        {
            RefreshReferences();
            ConfigureTrigger();
        }

        private void Update()
        {
            if (!requireInteractInput || _loadInProgress)
            {
                return;
            }

            Character selected = null;
            foreach (Character candidate in _candidates)
            {
                if (!CanUsePortal(candidate))
                {
                    continue;
                }

                selected = candidate;
                break;
            }

            if (selected != null && IsInteractPressedThisFrame(selected))
            {
                LoadTargetScene(selected);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Character character = other != null ? other.GetComponentInParent<Character>() : null;
            if (!CanUsePortal(character))
            {
                return;
            }

            _candidates.Add(character);

            if (!requireInteractInput)
            {
                LoadTargetScene(character);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            Character character = other != null ? other.GetComponentInParent<Character>() : null;
            if (character != null)
            {
                _candidates.Remove(character);
            }
        }

        private void RefreshReferences()
        {
            if (_trigger == null)
            {
                _trigger = GetComponent<Collider2D>();
            }

            if (visualRoot == null && transform.childCount > 0)
            {
                visualRoot = transform.GetChild(0).gameObject;
            }
        }

        private void ConfigureTrigger()
        {
            if (_trigger != null)
            {
                _trigger.isTrigger = true;
            }
        }

        private bool CanUsePortal(Character character)
        {
            if (character == null || !character.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (character.CharacterType != Character.CharacterTypes.Player)
            {
                return false;
            }

            return string.IsNullOrEmpty(acceptedPlayerId) || character.PlayerID == acceptedPlayerId;
        }

        private bool IsInteractPressedThisFrame(Character character)
        {
            InputManager inputManager = character != null ? character.LinkedInputManager : null;
            if (inputManager != null &&
                inputManager.InteractButton != null &&
                inputManager.InteractButton.State.CurrentState == MMInput.ButtonStates.ButtonDown)
            {
                return true;
            }

            return Input.GetKeyDown(fallbackInteractKey);
        }

        private void LoadTargetScene(Character character)
        {
            if (_loadInProgress)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(targetSceneName))
            {
                Debug.LogWarning("[SceneLoadPortalController] Target scene name is empty.", this);
                return;
            }

            _loadInProgress = true;
            Log($"Loading scene '{targetSceneName}' from portal '{name}' used by '{character.name}'.");
            SceneManager.LoadScene(targetSceneName, loadSceneMode);
        }

        private void OnDrawGizmosSelected()
        {
            RefreshReferences();
            if (_trigger == null)
            {
                return;
            }

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;

            if (_trigger is BoxCollider2D box)
            {
                Gizmos.DrawCube(box.offset, box.size);
            }
            else if (_trigger is CircleCollider2D circle)
            {
                Gizmos.DrawSphere(circle.offset, circle.radius);
            }
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[SceneLoadPortalController] " + message, this);
            }
        }
    }
}
