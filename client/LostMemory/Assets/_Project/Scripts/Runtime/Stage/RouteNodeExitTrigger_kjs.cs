using System.Collections.Generic;
using LostMemory.TestKhi;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Stage
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    [AddComponentMenu("Lost Memory/Stage/Route Node Exit Trigger KJS")]
    public sealed class RouteNodeExitTrigger_kjs : MonoBehaviour
    {
        [SerializeField] private bool unlockedOnStart = true;
        [SerializeField] private bool requireInteractInput = true;
        [SerializeField] private KeyCode fallbackInteractKey = KeyCode.F;
        [SerializeField] private string acceptedPlayerId = "Player1";
        [SerializeField] private bool useDistanceFallback = true;
        [SerializeField, Min(0.1f)] private float activationRadius = 1.5f;
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private RouteNodeExitTriggerView triggerView;
        [SerializeField] private Transform localTeleportTarget;
        [SerializeField] private string localTeleportTargetRootName = "Map_2F_1R_2_SE_Hall";
        [SerializeField] private string localTeleportAnchorTag = "default";
        [SerializeField] private Vector3 localTeleportOffset = new Vector3(0f, -2f, 0f);
        [SerializeField] private bool debugLogging;

        private readonly HashSet<Character> candidates = new HashSet<Character>();
        private readonly Collider2D[] overlapBuffer = new Collider2D[16];
        private Collider2D trigger;
        private bool unlocked;
        private bool requestInProgress;

        private void Reset()
        {
            RefreshReferences();
            ConfigureTrigger();
        }

        private void OnValidate()
        {
            activationRadius = Mathf.Max(0.1f, activationRadius);
            RefreshReferences();
            ConfigureTrigger();
        }

        private void Awake()
        {
            RefreshReferences();
            ConfigureTrigger();
            SetUnlocked(unlockedOnStart);
        }

        private void Update()
        {
            if (!unlocked || requestInProgress)
            {
                return;
            }

            Character selected = ResolveCandidate();
            ApplyCurrentViewState(selected != null);

            if (selected == null)
            {
                return;
            }

            if (!requireInteractInput || IsInteractPressedThisFrame(selected))
            {
                RequestLocalTeleport(selected);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TrackCandidate(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TrackCandidate(other);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            Character character = other != null ? other.GetComponentInParent<Character>() : null;
            if (character != null)
            {
                candidates.Remove(character);
                ApplyCurrentViewState();
            }
        }

        private void SetUnlocked(bool value)
        {
            unlocked = value;
            requestInProgress = false;

            if (!value)
            {
                candidates.Clear();
            }

            if (trigger != null)
            {
                trigger.enabled = true;
                trigger.isTrigger = true;
            }

            if (visualRoot != null)
            {
                visualRoot.SetActive(true);
            }

            ApplyCurrentViewState();
        }

        private void TrackCandidate(Collider2D other)
        {
            if (!unlocked || other == null)
            {
                return;
            }

            Character character = other.GetComponentInParent<Character>();
            if (!CanUseTrigger(character))
            {
                return;
            }

            if (candidates.Add(character))
            {
                ApplyCurrentViewState();
            }

            if (!requireInteractInput)
            {
                RequestLocalTeleport(character);
            }
        }

        private void RefreshReferences()
        {
            if (trigger == null)
            {
                trigger = GetComponent<Collider2D>();
            }

            if (visualRoot == null && transform.childCount > 0)
            {
                visualRoot = transform.GetChild(0).gameObject;
            }

            if (triggerView == null)
            {
                triggerView = GetComponentInChildren<RouteNodeExitTriggerView>(includeInactive: true);
            }
        }

        private void ConfigureTrigger()
        {
            if (trigger != null)
            {
                trigger.isTrigger = true;
            }
        }

        private Character ResolveCandidate()
        {
            candidates.RemoveWhere(candidate => !CanUseTrigger(candidate));

            foreach (Character candidate in candidates)
            {
                if (CanUseTrigger(candidate))
                {
                    return candidate;
                }
            }

            if (!useDistanceFallback)
            {
                return null;
            }

            return ResolveCandidateFromTriggerOverlap() ?? ResolveCandidateByDistance();
        }

        private Character ResolveCandidateFromTriggerOverlap()
        {
            if (trigger == null)
            {
                return null;
            }

            ContactFilter2D filter = new ContactFilter2D
            {
                useTriggers = true,
                useLayerMask = false
            };

            int count = trigger.Overlap(filter, overlapBuffer);
            for (int i = 0; i < count; i++)
            {
                Character character = overlapBuffer[i] != null ? overlapBuffer[i].GetComponentInParent<Character>() : null;
                if (CanUseTrigger(character))
                {
                    return character;
                }
            }

            return null;
        }

        private Character ResolveCandidateByDistance()
        {
            Character[] characters = FindObjectsOfType<Character>();
            Vector3 triggerPosition = trigger != null ? trigger.bounds.center : transform.position;
            float sqrRadius = activationRadius * activationRadius;

            for (int i = 0; i < characters.Length; i++)
            {
                Character character = characters[i];
                if (!CanUseTrigger(character))
                {
                    continue;
                }

                Vector3 delta = character.transform.position - triggerPosition;
                delta.z = 0f;
                if (delta.sqrMagnitude <= sqrRadius)
                {
                    return character;
                }
            }

            return null;
        }

        private bool CanUseTrigger(Character character)
        {
            if (character == null || !character.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (character.CharacterType != Character.CharacterTypes.Player)
            {
                return false;
            }

            if (KhiPlayerActionGate.IsBlocked(character))
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

        private void RequestLocalTeleport(Character character)
        {
            if (requestInProgress)
            {
                return;
            }

            requestInProgress = true;
            ApplyCurrentViewState();

            Transform target = ResolveLocalTeleportTarget();
            if (target == null)
            {
                Debug.LogWarning("[RouteNodeExitTrigger_kjs] Local teleport target not found.", this);
                requestInProgress = false;
                ApplyCurrentViewState();
                return;
            }

            TeleportCharacter(character, target.position + localTeleportOffset);
            candidates.Clear();
            requestInProgress = false;
            ApplyCurrentViewState();
            Log($"Local teleported '{character.name}' to '{target.name}'.");
        }

        private Transform ResolveLocalTeleportTarget()
        {
            if (localTeleportTarget != null)
            {
                return localTeleportTarget;
            }

            if (string.IsNullOrWhiteSpace(localTeleportTargetRootName))
            {
                return null;
            }

            GameObject root = GameObject.Find(localTeleportTargetRootName);
            return root != null ? ResolveEntryAnchor(root.transform) : null;
        }

        private Transform ResolveEntryAnchor(Transform root)
        {
            RoomEntryAnchor[] anchors = root.GetComponentsInChildren<RoomEntryAnchor>(includeInactive: true);
            for (int i = 0; i < anchors.Length; i++)
            {
                RoomEntryAnchor anchor = anchors[i];
                if (anchor != null && anchor.Matches(localTeleportAnchorTag))
                {
                    return anchor.transform;
                }
            }

            return anchors.Length > 0 ? anchors[0].transform : root;
        }

        private static void TeleportCharacter(Character character, Vector3 targetPosition)
        {
            if (character == null)
            {
                return;
            }

            TopDownController controller = character.GetComponent<TopDownController>();
            if (controller != null)
            {
                controller.SetMovement(Vector3.zero);
                controller.MovePosition(targetPosition, true);
                return;
            }

            character.transform.position = targetPosition;
        }

        private void ApplyCurrentViewState(bool hasCandidate = false)
        {
            if (triggerView == null)
            {
                return;
            }

            if (!unlocked)
            {
                triggerView.ApplyState(RouteNodeExitTriggerViewState.Locked);
                return;
            }

            if (requestInProgress)
            {
                triggerView.ApplyState(RouteNodeExitTriggerViewState.Transitioning);
                return;
            }

            triggerView.ApplyState(hasCandidate || candidates.Count > 0
                ? RouteNodeExitTriggerViewState.CandidateInside
                : RouteNodeExitTriggerViewState.UnlockedIdle);
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[RouteNodeExitTrigger_kjs] " + message, this);
            }
        }
    }
}
