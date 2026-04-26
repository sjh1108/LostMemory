using System.Collections.Generic;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Stage
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    [AddComponentMenu("Lost Memory/Stage/Boss Room Entry Interaction Zone")]
    public sealed class BossRoomEntryInteractionZone : MonoBehaviour
    {
        [SerializeField] private BossRoomDoorController doorController;
        [SerializeField] private LayerMask targetLayerMask = ~0;
        [SerializeField] private bool debugLogging;

        private readonly Dictionary<Character, int> _characterOverlapCounts = new Dictionary<Character, int>();
        private readonly List<Character> _iterationBuffer = new List<Character>();

        private void Awake()
        {
            if (doorController == null)
            {
                doorController = GetComponentInParent<BossRoomDoorController>();
            }
        }

        private void Reset()
        {
            Collider2D triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }

            int playerLayer = LayerMask.NameToLayer("Player");
            targetLayerMask = playerLayer >= 0 ? 1 << playerLayer : ~0;
            doorController = GetComponentInParent<BossRoomDoorController>();
        }

        private void Update()
        {
            if (_characterOverlapCounts.Count == 0)
            {
                return;
            }

            _iterationBuffer.Clear();
            foreach (KeyValuePair<Character, int> entry in _characterOverlapCounts)
            {
                _iterationBuffer.Add(entry.Key);
            }

            for (int i = 0; i < _iterationBuffer.Count; i++)
            {
                Character character = _iterationBuffer[i];
                if (!IsCharacterStillValid(character))
                {
                    ForceExit(character);
                    continue;
                }

                if (IsInteractPressedThisFrame(character))
                {
                    doorController?.TryHandleInteraction(character);
                }
            }
        }

        public void SetDoorController(BossRoomDoorController controller)
        {
            doorController = controller;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Character character = ResolveCharacter(other);
            if (!CanTrack(other, character))
            {
                return;
            }

            int overlapCount;
            _characterOverlapCounts.TryGetValue(character, out overlapCount);
            _characterOverlapCounts[character] = overlapCount + 1;

            if (overlapCount == 0)
            {
                doorController?.NotifyCharacterEnteredZone(character);
                Log($"Entered zone: {character.PlayerID}");
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            Character character = ResolveCharacter(other);
            if (character == null)
            {
                return;
            }

            int overlapCount;
            if (!_characterOverlapCounts.TryGetValue(character, out overlapCount))
            {
                return;
            }

            overlapCount--;
            if (overlapCount > 0)
            {
                _characterOverlapCounts[character] = overlapCount;
                return;
            }

            _characterOverlapCounts.Remove(character);
            doorController?.NotifyCharacterExitedZone(character);
            Log($"Exited zone: {character.PlayerID}");
        }

        private void ForceExit(Character character)
        {
            if (character == null)
            {
                return;
            }

            if (_characterOverlapCounts.Remove(character))
            {
                doorController?.NotifyCharacterExitedZone(character);
                Log($"Force exit: {character.PlayerID}");
            }
        }

        private bool CanTrack(Collider2D source, Character character)
        {
            if (character == null || !character.gameObject.activeInHierarchy)
            {
                return false;
            }

            bool sourceLayerAllowed = source != null && MMLayers.LayerInLayerMask(source.gameObject.layer, targetLayerMask);
            bool characterLayerAllowed = MMLayers.LayerInLayerMask(character.gameObject.layer, targetLayerMask);
            if (!sourceLayerAllowed && !characterLayerAllowed)
            {
                return false;
            }

            if (character.CharacterType != Character.CharacterTypes.Player)
            {
                return false;
            }

            return true;
        }

        private bool IsCharacterStillValid(Character character)
        {
            return character != null && character.gameObject.activeInHierarchy;
        }

        private static bool IsInteractPressedThisFrame(Character character)
        {
            InputManager inputManager = character != null ? character.LinkedInputManager : null;
            if (inputManager == null || inputManager.InteractButton == null)
            {
                return false;
            }

            return inputManager.InteractButton.State.CurrentState == MMInput.ButtonStates.ButtonDown;
        }

        private static Character ResolveCharacter(Component source)
        {
            return source != null ? source.GetComponentInParent<Character>() : null;
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[BossRoomEntryZone] " + message, this);
            }
        }
    }
}
