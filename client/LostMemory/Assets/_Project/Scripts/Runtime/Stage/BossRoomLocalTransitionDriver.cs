using System;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Stage
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Stage/Boss Room Local Transition Driver")]
    public sealed class BossRoomLocalTransitionDriver : MonoBehaviour
    {
        [SerializeField] private BossRoomDoorController doorController;
        [SerializeField] private BossRoomEntryPoint[] entryPoints = System.Array.Empty<BossRoomEntryPoint>();
        [SerializeField] private bool freezePlayersDuringTransition = true;
        [SerializeField] private bool autoUnfreezePlayers = true;
        [SerializeField, Min(0f)] private float unfreezeDelay = 0.1f;
        [SerializeField] private bool alignFacingDirection = true;
        [SerializeField] private bool debugLogging;

        private Coroutine _pendingUnfreezeRoutine;

        public event Action<BossRoomTransitionCompletedContext> TransitionCompleted;

        public bool AutoUnfreezePlayers
        {
            get => autoUnfreezePlayers;
            set => autoUnfreezePlayers = value;
        }

        public bool TryStartRouteEntry(string bossRoomId, string bossEntryPointId, string bossSceneName)
        {
            BossRoomEntryPoint entryPoint = ResolveEntryPoint(bossEntryPointId);
            if (entryPoint == null)
            {
                LogWarning("Boss room entry point not found for route entry: " + bossEntryPointId);
                return false;
            }

            List<Character> players = CollectScenePlayers();
            if (players.Count <= 0)
            {
                LogWarning("No eligible players were found for route boss entry.");
                return false;
            }

            string resolvedEntryPointId = string.IsNullOrWhiteSpace(bossEntryPointId)
                ? entryPoint.EntryPointId
                : bossEntryPointId;

            BossRoomEntryTransitionRequest request = new BossRoomEntryTransitionRequest(
                doorController,
                players[0],
                bossRoomId,
                resolvedEntryPointId,
                bossSceneName,
                players.Count,
                players.Count);

            TeleportPlayers(request, players, entryPoint);
            return true;
        }

        private void Reset()
        {
            doorController = GetComponent<BossRoomDoorController>();
            CacheEntryPoints(forceRefresh: true);
        }

        private void OnEnable()
        {
            if (doorController == null)
            {
                doorController = GetComponent<BossRoomDoorController>();
            }

            if (doorController != null)
            {
                doorController.BossRoomEntryStarted += HandleBossRoomEntryStarted;
            }

            CacheEntryPoints(forceRefresh: false);
        }

        private void OnDisable()
        {
            if (doorController != null)
            {
                doorController.BossRoomEntryStarted -= HandleBossRoomEntryStarted;
            }

            if (_pendingUnfreezeRoutine != null)
            {
                StopCoroutine(_pendingUnfreezeRoutine);
                _pendingUnfreezeRoutine = null;
            }
        }

        public void RefreshEntryPoints()
        {
            CacheEntryPoints(forceRefresh: true);
        }

        private void HandleBossRoomEntryStarted(BossRoomEntryTransitionRequest request)
        {
            BossRoomEntryPoint entryPoint = ResolveEntryPoint(request.BossEntryPointId);
            if (entryPoint == null)
            {
                LogWarning("Boss room entry point not found: " + request.BossEntryPointId);
                request.Controller?.ResetEntryFlow();
                return;
            }

            List<Character> players = CollectOrderedPlayers(request);
            if (players.Count <= 0)
            {
                LogWarning("No eligible players were found for boss room transition.");
                request.Controller?.ResetEntryFlow();
                return;
            }

            TeleportPlayers(request, players, entryPoint);
        }

        private List<Character> CollectOrderedPlayers(BossRoomEntryTransitionRequest request)
        {
            List<Character> players = new List<Character>();

            if (request.Controller != null)
            {
                foreach (BossRoomEntryParticipantState participant in request.Controller.Participants)
                {
                    if (participant == null || !participant.IsEligible || participant.IsUnavailable)
                    {
                        continue;
                    }

                    Character character = participant.Character;
                    if (character == null ||
                        character.CharacterType != Character.CharacterTypes.Player ||
                        !character.gameObject.activeInHierarchy ||
                        players.Contains(character))
                    {
                        continue;
                    }

                    players.Add(character);
                }
            }

            Character initiator = request.Initiator;
            if (initiator != null &&
                initiator.CharacterType == Character.CharacterTypes.Player &&
                initiator.gameObject.activeInHierarchy &&
                !players.Contains(initiator))
            {
                players.Add(initiator);
            }

            players.Sort(CompareCharacters);
            return players;
        }

        private static List<Character> CollectScenePlayers()
        {
            List<Character> players = new List<Character>();
            Character[] characters = FindObjectsByType<Character>(FindObjectsSortMode.None);
            for (int i = 0; i < characters.Length; i++)
            {
                Character character = characters[i];
                if (character == null ||
                    character.CharacterType != Character.CharacterTypes.Player ||
                    !character.gameObject.activeInHierarchy ||
                    IsCharacterUnavailable(character))
                {
                    continue;
                }

                players.Add(character);
            }

            players.Sort(CompareCharacters);
            return players;
        }

        private static bool IsCharacterUnavailable(Character character)
        {
            if (character == null || !character.gameObject.activeInHierarchy)
            {
                return true;
            }

            if (character.ConditionState != null &&
                character.ConditionState.CurrentState == CharacterStates.CharacterConditions.Dead)
            {
                return true;
            }

            Health health = character.CharacterHealth;
            return health != null && health.Initialized && health.CurrentHealth <= 0f;
        }

        private void TeleportPlayers(
            BossRoomEntryTransitionRequest request,
            IReadOnlyList<Character> players,
            BossRoomEntryPoint entryPoint)
        {
            if (_pendingUnfreezeRoutine != null)
            {
                StopCoroutine(_pendingUnfreezeRoutine);
                _pendingUnfreezeRoutine = null;
            }

            for (int i = 0; i < players.Count; i++)
            {
                Character character = players[i];
                if (character == null)
                {
                    continue;
                }

                if (freezePlayersDuringTransition)
                {
                    character.Freeze();
                }

                TeleportCharacter(character, entryPoint, i);
            }

            Character[] playerSnapshot = CopyPlayers(players);
            TransitionCompleted?.Invoke(new BossRoomTransitionCompletedContext(request, entryPoint, playerSnapshot));

            if (freezePlayersDuringTransition && autoUnfreezePlayers)
            {
                if (unfreezeDelay <= 0f)
                {
                    UnfreezePlayers(playerSnapshot);
                }
                else
                {
                    _pendingUnfreezeRoutine = StartCoroutine(UnfreezePlayersAfterDelay(playerSnapshot, unfreezeDelay));
                }
            }

            Log("Teleported " + players.Count + " player(s) to boss entry point: " + entryPoint.EntryPointId);
        }

        private void TeleportCharacter(Character character, BossRoomEntryPoint entryPoint, int participantIndex)
        {
            Transform arrivalPoint = entryPoint.GetArrivalPoint(participantIndex);
            Vector3 targetPosition = arrivalPoint != null ? arrivalPoint.position : entryPoint.transform.position;

            TopDownController controller = character.GetComponent<TopDownController>();
            if (controller != null)
            {
                controller.SetMovement(Vector3.zero);
                controller.MovePosition(targetPosition, true);
            }
            else
            {
                character.transform.position = targetPosition;
            }

            if (!alignFacingDirection)
            {
                return;
            }

            CharacterOrientation2D orientation2D = character.FindAbility<CharacterOrientation2D>();
            if (orientation2D != null)
            {
                orientation2D.InitialFacingDirection = entryPoint.FacingDirection;
                orientation2D.Face(entryPoint.FacingDirection);
                return;
            }

            CharacterOrientation3D orientation3D = character.FindAbility<CharacterOrientation3D>();
            if (orientation3D != null)
            {
                orientation3D.Face(entryPoint.FacingDirection);
            }
        }

        private IEnumerator UnfreezePlayersAfterDelay(IReadOnlyList<Character> players, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            _pendingUnfreezeRoutine = null;
            UnfreezePlayers(players);
        }

        private static Character[] CopyPlayers(IReadOnlyList<Character> players)
        {
            if (players == null || players.Count == 0)
            {
                return System.Array.Empty<Character>();
            }

            Character[] copiedPlayers = new Character[players.Count];
            for (int i = 0; i < players.Count; i++)
            {
                copiedPlayers[i] = players[i];
            }

            return copiedPlayers;
        }

        private static void UnfreezePlayers(IReadOnlyList<Character> players)
        {
            for (int i = 0; i < players.Count; i++)
            {
                Character character = players[i];
                if (character != null)
                {
                    character.UnFreeze();
                }
            }
        }

        private BossRoomEntryPoint ResolveEntryPoint(string entryPointId)
        {
            CacheEntryPoints(forceRefresh: false);

            BossRoomEntryPoint fallback = null;
            for (int i = 0; i < entryPoints.Length; i++)
            {
                BossRoomEntryPoint entryPoint = entryPoints[i];
                if (entryPoint == null)
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback = entryPoint;
                }

                if (entryPoint.Matches(entryPointId))
                {
                    return entryPoint;
                }
            }

            return string.IsNullOrWhiteSpace(entryPointId) ? fallback : null;
        }

        private void CacheEntryPoints(bool forceRefresh)
        {
            if (!forceRefresh && HasAssignedEntryPoint())
            {
                return;
            }

            entryPoints = FindObjectsByType<BossRoomEntryPoint>(FindObjectsSortMode.None);
        }

        private bool HasAssignedEntryPoint()
        {
            if (entryPoints == null || entryPoints.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < entryPoints.Length; i++)
            {
                if (entryPoints[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CompareCharacters(Character left, Character right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            string leftPlayerId = left.PlayerID ?? string.Empty;
            string rightPlayerId = right.PlayerID ?? string.Empty;
            int compareResult = string.CompareOrdinal(leftPlayerId, rightPlayerId);
            if (compareResult != 0)
            {
                return compareResult;
            }

            return left.GetInstanceID().CompareTo(right.GetInstanceID());
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[BossRoomLocalTransition] " + message, this);
            }
        }

        private void LogWarning(string message)
        {
            Debug.LogWarning("[BossRoomLocalTransition] " + message, this);
        }
    }
}
