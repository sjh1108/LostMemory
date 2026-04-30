using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Stage
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    [AddComponentMenu("Lost Memory/Stage/Boss Clear Portal Controller")]
    public sealed class BossClearPortalController : MonoBehaviour
    {
        [SerializeField] private RoomEntryRuntimeController roomController;
        [SerializeField] private GameObject portalVisualRoot;
        [SerializeField] private Collider2D portalTrigger;
        [SerializeField, Min(0.1f)] private float activationRadius = 1.5f;
        [SerializeField] private KeyCode fallbackInteractKey = KeyCode.E;
        [SerializeField] private bool hideOnStart = true;
        [SerializeField] private bool debugLogging;

        private readonly Collider2D[] overlapBuffer = new Collider2D[16];
        private bool portalAvailable;
        private bool portalUseInProgress;

        private void Reset()
        {
            RefreshReferences();
        }

        private void OnValidate()
        {
            activationRadius = Mathf.Max(0.1f, activationRadius);
            RefreshReferences();

            if (portalTrigger != null)
            {
                portalTrigger.isTrigger = true;
            }
        }

        private void Awake()
        {
            RefreshReferences();

            if (portalTrigger != null)
            {
                portalTrigger.isTrigger = true;
            }

            if (hideOnStart)
            {
                HidePortal();
            }
        }

        private void OnEnable()
        {
            RefreshReferences();

            if (roomController != null)
            {
                roomController.RoomCleared += HandleRoomCleared;
            }
        }

        private void OnDisable()
        {
            if (roomController != null)
            {
                roomController.RoomCleared -= HandleRoomCleared;
            }
        }

        private void Update()
        {
            if (!portalAvailable || portalUseInProgress)
            {
                return;
            }

            Character character = ResolvePortalCandidate();
            if (character == null || !IsInteractPressedThisFrame(character))
            {
                return;
            }

            TryUsePortal(character);
        }

        private Character ResolvePortalCandidate()
        {
            ContactFilter2D filter = new ContactFilter2D
            {
                useTriggers = true,
                useLayerMask = false
            };

            if (portalTrigger != null)
            {
                int count = portalTrigger.Overlap(filter, overlapBuffer);
                for (int i = 0; i < count; i++)
                {
                    Character character = overlapBuffer[i] != null ? overlapBuffer[i].GetComponentInParent<Character>() : null;
                    if (CanUsePortal(character))
                    {
                        return character;
                    }
                }
            }

            return ResolvePortalCandidateByDistance();
        }

        private void RefreshReferences()
        {
            if (portalTrigger == null)
            {
                portalTrigger = GetComponent<Collider2D>();
            }

            roomController ??= GetComponentInParent<RoomEntryRuntimeController>();
            if (roomController == null && transform.parent != null)
            {
                roomController = transform.parent.GetComponentInChildren<RoomEntryRuntimeController>(includeInactive: true);
            }

            if (portalVisualRoot == null && transform.childCount > 0)
            {
                portalVisualRoot = transform.GetChild(0).gameObject;
            }
        }

        private void HandleRoomCleared(RoomClearedPayload payload)
        {
            if (payload.Data == null || payload.Data.RoomType != StageRoomType.Boss)
            {
                return;
            }

            ShowPortal();
        }

        public void ShowPortal()
        {
            portalAvailable = true;
            portalUseInProgress = false;

            if (portalVisualRoot != null)
            {
                portalVisualRoot.SetActive(true);
            }

            if (portalTrigger != null)
            {
                portalTrigger.enabled = true;
            }

            Log("Portal shown.");
        }

        public void HidePortal()
        {
            portalAvailable = false;
            portalUseInProgress = false;

            if (portalVisualRoot != null)
            {
                portalVisualRoot.SetActive(false);
            }

            if (portalTrigger != null)
            {
                portalTrigger.enabled = false;
            }
        }

        private Character ResolvePortalCandidateByDistance()
        {
            Character[] characters = FindObjectsOfType<Character>();
            Vector3 portalPosition = portalTrigger != null ? portalTrigger.bounds.center : transform.position;
            float sqrRadius = activationRadius * activationRadius;

            for (int i = 0; i < characters.Length; i++)
            {
                Character character = characters[i];
                if (!CanUsePortal(character))
                {
                    continue;
                }

                Vector3 delta = character.transform.position - portalPosition;
                delta.z = 0f;
                if (delta.sqrMagnitude > sqrRadius)
                {
                    continue;
                }

                return character;
            }

            return null;
        }

        private bool TryUsePortal(Character character)
        {
            if (!portalAvailable || portalUseInProgress || !CanUsePortal(character))
            {
                return false;
            }

            RunManager runManager = RunManager.Instance;
            if (runManager == null)
            {
                Debug.LogWarning("[BossClearPortalController] RunManager.Instance is null.", this);
                return false;
            }

            Debug.Log($"[BossClearPortalController] Portal used by '{character.name}'.", this);
            portalUseInProgress = true;

            bool handled = runManager.NotifyBossClearPortalEntered();
            if (!handled)
            {
                Log("RunManager rejected portal use. Portal restored.");
                portalUseInProgress = false;
                return false;
            }

            return true;
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

            return true;
        }

        private bool IsInteractPressedThisFrame(Character character)
        {
            InputManager inputManager = character != null ? character.LinkedInputManager : null;
            if (inputManager != null && inputManager.InteractButton != null &&
                inputManager.InteractButton.State.CurrentState == MMInput.ButtonStates.ButtonDown)
            {
                return true;
            }

            return Input.GetKeyDown(fallbackInteractKey);
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[BossClearPortalController] " + message, this);
            }
        }
    }
}
