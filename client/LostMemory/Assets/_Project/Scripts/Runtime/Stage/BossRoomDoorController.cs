using System;
using System.Collections.Generic;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace LostMemory.Stage
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Stage/Boss Room Door Controller")]
    public sealed class BossRoomDoorController : MonoBehaviour
    {
        [SerializeField] private BossRoomEntryTracker entryTracker;
        [SerializeField] private BossRoomEntryInteractionZone interactionZone;
        [SerializeField] private BossRoomDoorView doorView;
        [SerializeField] private BossRoomEntryReadyPolicy readyPolicy = BossRoomEntryReadyPolicy.Auto;
        [SerializeField] private bool cancelReadyWhenLeaveZone = true;
        [SerializeField] private float participantRefreshInterval = 0.25f;
        [SerializeField] private bool debugLogging;
        [SerializeField] private string bossRoomId = "Boss";
        [SerializeField] private string bossEntryPointId = string.Empty;
        [SerializeField] private string bossSceneName = string.Empty;

        private readonly Dictionary<string, BossRoomEntryParticipantState> _participants =
            new Dictionary<string, BossRoomEntryParticipantState>(StringComparer.Ordinal);

        private readonly HashSet<string> _presentParticipantKeys =
            new HashSet<string>(StringComparer.Ordinal);

        private BossRoomDoorState _doorState = BossRoomDoorState.Locked;
        private float _nextParticipantRefreshTime;

        public event Action<BossRoomDoorState> DoorStateChanged;
        public event Action<BossRoomEntryTransitionRequest> BossRoomEntryConfirmed;
        public event Action<BossRoomEntryTransitionRequest> BossRoomEntryStarted;

        public BossRoomDoorState DoorState => _doorState;
        public IReadOnlyCollection<BossRoomEntryParticipantState> Participants => _participants.Values;
        public int EligiblePlayerCount => CountParticipants(static participant => participant.IsEligible);
        public int ReadyPlayerCount => CountParticipants(static participant => participant.IsReady);
        public bool HasUnavailablePlayers => CountParticipants(static participant => participant.IsUnavailable) > 0;
        public bool IsDoorUnlocked => entryTracker != null && entryTracker.CanEnterBossRoom;
        public BossRoomEntryReadyPolicy ResolvedReadyPolicy => ResolveReadyPolicy();

        private void Reset()
        {
            entryTracker = GetComponent<BossRoomEntryTracker>();
            interactionZone = GetComponentInChildren<BossRoomEntryInteractionZone>();
            doorView = GetComponentInChildren<BossRoomDoorView>();
        }

        private void Awake()
        {
            if (interactionZone == null)
            {
                interactionZone = GetComponentInChildren<BossRoomEntryInteractionZone>();
            }

            interactionZone?.SetDoorController(this);
            RefreshDoorState(false);
        }

        private void OnEnable()
        {
            if (entryTracker != null)
            {
                entryTracker.BossEntryConditionChanged += HandleBossEntryConditionChanged;
                entryTracker.RefreshCondition(false);
            }

            RefreshDoorState(false);
        }

        private void OnDisable()
        {
            if (entryTracker != null)
            {
                entryTracker.BossEntryConditionChanged -= HandleBossEntryConditionChanged;
            }
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextParticipantRefreshTime)
            {
                return;
            }

            RefreshDoorState();
        }

        public void NotifyCharacterEnteredZone(Character character)
        {
            if (character == null)
            {
                return;
            }

            string participantKey = GetParticipantKey(character);
            BossRoomEntryParticipantState participant = GetOrCreateParticipant(participantKey, character);
            participant.BindCharacter(character);
            participant.IsInsideInteractionZone = true;

            Log("Participant entered zone: " + participant.PlayerId);
            RefreshDoorState();
        }

        public void NotifyCharacterExitedZone(Character character)
        {
            if (character == null)
            {
                return;
            }

            string participantKey = GetParticipantKey(character);
            BossRoomEntryParticipantState participant;
            if (!_participants.TryGetValue(participantKey, out participant))
            {
                return;
            }

            participant.IsInsideInteractionZone = false;
            if (cancelReadyWhenLeaveZone)
            {
                participant.IsReady = false;
            }

            Log("Participant exited zone: " + participant.PlayerId);
            RefreshDoorState();
        }

        public bool TryHandleInteraction(Character character)
        {
            if (character == null)
            {
                return false;
            }

            RefreshDoorState(false);

            if (!CanCharacterInteract(character))
            {
                Log("Interaction denied: " + character.PlayerID);
                return false;
            }

            if (ResolvedReadyPolicy == BossRoomEntryReadyPolicy.SoloImmediate && EligiblePlayerCount <= 1)
            {
                return ConfirmAndStartEntry(character);
            }

            BossRoomEntryParticipantState participant = GetOrCreateParticipant(GetParticipantKey(character), character);
            participant.IsReady = !participant.IsReady;
            Log("Ready toggled: " + participant.PlayerId + " -> " + participant.IsReady);

            RefreshDoorState(false);

            if (ShouldConfirmPartyEntry())
            {
                return ConfirmAndStartEntry(character);
            }

            RefreshDoorState();
            return true;
        }

        public void CancelAllReadyPlayers()
        {
            bool changed = false;

            foreach (BossRoomEntryParticipantState participant in _participants.Values)
            {
                if (!participant.IsReady)
                {
                    continue;
                }

                participant.IsReady = false;
                changed = true;
            }

            if (changed)
            {
                RefreshDoorState();
            }
        }

        public void ResetEntryFlow()
        {
            ClearReadyFlags();
            if (_doorState == BossRoomDoorState.EntryConfirmed || _doorState == BossRoomDoorState.Transitioning)
            {
                _doorState = BossRoomDoorState.Locked;
            }

            RefreshDoorState();
        }

        private void HandleBossEntryConditionChanged(BossRoomEntryConditionResult condition)
        {
            RefreshDoorState();
        }

        private void RefreshDoorState(bool notifyListeners = true)
        {
            RefreshParticipantsFromScene();
            _nextParticipantRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, participantRefreshInterval);

            BossRoomDoorState nextState = DetermineNextDoorState();
            ApplyDoorState(nextState, notifyListeners);
        }

        private void RefreshParticipantsFromScene()
        {
            _presentParticipantKeys.Clear();

            Character[] characters = FindObjectsByType<Character>(FindObjectsSortMode.None);
            for (int i = 0; i < characters.Length; i++)
            {
                Character character = characters[i];
                if (character == null || character.CharacterType != Character.CharacterTypes.Player)
                {
                    continue;
                }

                string participantKey = GetParticipantKey(character);
                _presentParticipantKeys.Add(participantKey);

                BossRoomEntryParticipantState participant = GetOrCreateParticipant(participantKey, character);
                participant.BindCharacter(character);
                participant.IsUnavailable = IsCharacterUnavailable(character);
                participant.IsEligible = !participant.IsUnavailable && character.LinkedInputManager != null;

                if (!participant.IsEligible)
                {
                    participant.IsReady = false;
                }

                if (participant.IsUnavailable)
                {
                    participant.IsInsideInteractionZone = false;
                }
            }

            if (_participants.Count == _presentParticipantKeys.Count)
            {
                return;
            }

            List<string> keysToRemove = null;
            foreach (KeyValuePair<string, BossRoomEntryParticipantState> entry in _participants)
            {
                if (_presentParticipantKeys.Contains(entry.Key))
                {
                    continue;
                }

                if (keysToRemove == null)
                {
                    keysToRemove = new List<string>();
                }

                keysToRemove.Add(entry.Key);
            }

            if (keysToRemove == null)
            {
                return;
            }

            for (int i = 0; i < keysToRemove.Count; i++)
            {
                _participants.Remove(keysToRemove[i]);
            }
        }

        private BossRoomDoorState DetermineNextDoorState()
        {
            if (_doorState == BossRoomDoorState.EntryConfirmed || _doorState == BossRoomDoorState.Transitioning)
            {
                return _doorState;
            }

            if (!IsDoorUnlocked)
            {
                ClearReadyFlags();
                return BossRoomDoorState.Locked;
            }

            if (ReadyPlayerCount <= 0)
            {
                return BossRoomDoorState.UnlockedIdle;
            }

            return BossRoomDoorState.WaitingForParty;
        }

        private void ApplyDoorState(BossRoomDoorState nextState, bool notifyListeners)
        {
            doorView?.ApplyState(nextState);

            if (_doorState == nextState)
            {
                return;
            }

            _doorState = nextState;
            Log("Door state changed: " + _doorState);

            if (notifyListeners)
            {
                DoorStateChanged?.Invoke(_doorState);
            }
        }

        private bool CanCharacterInteract(Character character)
        {
            if (character == null || !character.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (!IsDoorUnlocked || HasUnavailablePlayers)
            {
                return false;
            }

            if (_doorState == BossRoomDoorState.EntryConfirmed || _doorState == BossRoomDoorState.Transitioning)
            {
                return false;
            }

            BossRoomEntryParticipantState participant;
            if (!_participants.TryGetValue(GetParticipantKey(character), out participant))
            {
                return false;
            }

            return participant.IsEligible && participant.IsInsideInteractionZone;
        }

        private bool ShouldConfirmPartyEntry()
        {
            if (HasUnavailablePlayers)
            {
                return false;
            }

            if (ResolvedReadyPolicy == BossRoomEntryReadyPolicy.SoloImmediate)
            {
                return EligiblePlayerCount <= 1;
            }

            if (EligiblePlayerCount <= 0)
            {
                return false;
            }

            return ReadyPlayerCount == EligiblePlayerCount;
        }

        private bool ConfirmAndStartEntry(Character initiator)
        {
            if (!IsDoorUnlocked || HasUnavailablePlayers)
            {
                return false;
            }

            BossRoomEntryTransitionRequest transitionRequest = new BossRoomEntryTransitionRequest(
                this,
                initiator,
                bossRoomId,
                bossEntryPointId,
                bossSceneName,
                ReadyPlayerCount,
                EligiblePlayerCount);

            ApplyDoorState(BossRoomDoorState.EntryConfirmed, true);
            BossRoomEntryConfirmed?.Invoke(transitionRequest);

            ApplyDoorState(BossRoomDoorState.Transitioning, true);
            BossRoomEntryStarted?.Invoke(transitionRequest);
            Log("Boss room entry started.");
            return true;
        }

        private BossRoomEntryParticipantState GetOrCreateParticipant(string participantKey, Character character)
        {
            BossRoomEntryParticipantState participant;
            if (_participants.TryGetValue(participantKey, out participant))
            {
                return participant;
            }

            participant = new BossRoomEntryParticipantState(character);
            _participants.Add(participantKey, participant);
            return participant;
        }

        private BossRoomEntryReadyPolicy ResolveReadyPolicy()
        {
            if (readyPolicy != BossRoomEntryReadyPolicy.Auto)
            {
                return readyPolicy;
            }

            return EligiblePlayerCount <= 1
                ? BossRoomEntryReadyPolicy.SoloImmediate
                : BossRoomEntryReadyPolicy.AllEligiblePlayersReady;
        }

        private void ClearReadyFlags()
        {
            foreach (BossRoomEntryParticipantState participant in _participants.Values)
            {
                participant.IsReady = false;
            }
        }

        private int CountParticipants(Func<BossRoomEntryParticipantState, bool> predicate)
        {
            int count = 0;

            foreach (BossRoomEntryParticipantState participant in _participants.Values)
            {
                if (predicate(participant))
                {
                    count++;
                }
            }

            return count;
        }

        private static string GetParticipantKey(Character character)
        {
            if (character == null)
            {
                return string.Empty;
            }

            return !string.IsNullOrWhiteSpace(character.PlayerID)
                ? character.PlayerID
                : character.GetInstanceID().ToString();
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
            if (health != null && health.Initialized && health.CurrentHealth <= 0f)
            {
                return true;
            }

            return false;
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[BossRoomDoor] " + message, this);
            }
        }
    }
}
