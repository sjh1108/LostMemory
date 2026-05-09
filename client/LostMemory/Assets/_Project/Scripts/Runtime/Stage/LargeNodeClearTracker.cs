using System;
using System.Collections.Generic;
using LostMemory.Stage.Data;
using UnityEngine;

namespace LostMemory.Stage
{
    public enum LargeNodeClearPolicy
    {
        ExplicitRequiredRoomIds,
        AllDiscoveredRooms,
        AllCombatRooms,
        NoActiveCombat
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Stage/Large Node Clear Tracker")]
    public sealed class LargeNodeClearTracker : MonoBehaviour
    {
        [SerializeField] private LargeNodeClearPolicy clearPolicy = LargeNodeClearPolicy.NoActiveCombat;
        [SerializeField] private string[] requiredRoomIds = Array.Empty<string>();
        [SerializeField] private RouteNodeExitTrigger[] exitTriggers = Array.Empty<RouteNodeExitTrigger>();
        [SerializeField] private bool includeInactiveControllers = true;
        [SerializeField] private bool autoDiscoverOnEnable = true;
        [SerializeField] private bool unlockIfNoRequiredRooms;
        [SerializeField] private bool debugLogging;

        private readonly Dictionary<RoomEntryRuntimeController, Action<RoomClearedPayload>> roomClearedHandlers =
            new Dictionary<RoomEntryRuntimeController, Action<RoomClearedPayload>>();

        private readonly Dictionary<RoomEntryRuntimeController, Action<RoomCombatStartedPayload>> combatStartedHandlers =
            new Dictionary<RoomEntryRuntimeController, Action<RoomCombatStartedPayload>>();

        private readonly HashSet<RoomEntryRuntimeController> requiredControllers =
            new HashSet<RoomEntryRuntimeController>();

        private readonly HashSet<RoomEntryRuntimeController> completedControllers =
            new HashSet<RoomEntryRuntimeController>();

        private readonly HashSet<RoomEntryRuntimeController> activeCombatControllers =
            new HashSet<RoomEntryRuntimeController>();

        private readonly HashSet<string> requiredIds = new HashSet<string>();
        private readonly HashSet<string> completedIds = new HashSet<string>();
        private bool nodeCleared;

        public bool IsCleared => clearPolicy == LargeNodeClearPolicy.NoActiveCombat
            ? activeCombatControllers.Count == 0
            : nodeCleared;

        private void Reset()
        {
            exitTriggers = GetComponentsInChildren<RouteNodeExitTrigger>(includeInactive: true);
        }

        private void OnEnable()
        {
            if (autoDiscoverOnEnable)
            {
                RebuildSubscriptions();
            }
        }

        private void Start()
        {
            if (autoDiscoverOnEnable)
            {
                RebuildSubscriptions();
            }
        }

        private void OnDisable()
        {
            UnsubscribeAll();
        }

        public void RebuildSubscriptions()
        {
            UnsubscribeAll();
            nodeCleared = false;
            completedControllers.Clear();
            completedIds.Clear();
            requiredControllers.Clear();
            requiredIds.Clear();
            activeCombatControllers.Clear();

            if (exitTriggers == null || exitTriggers.Length == 0)
            {
                exitTriggers = GetComponentsInChildren<RouteNodeExitTrigger>(includeInactive: true);
            }

            if (clearPolicy == LargeNodeClearPolicy.NoActiveCombat)
            {
                UnlockExits();
            }
            else
            {
                LockExits();
            }

            BuildRequiredIds();

            RoomEntryRuntimeController[] controllers = FindObjectsOfType<RoomEntryRuntimeController>(includeInactiveControllers);
            for (int i = 0; i < controllers.Length; i++)
            {
                RoomEntryRuntimeController controller = controllers[i];
                if (controller == null || controller.gameObject.scene != gameObject.scene)
                {
                    continue;
                }

                if (!ShouldTrack(controller))
                {
                    continue;
                }

                requiredControllers.Add(controller);
                Action<RoomClearedPayload> roomClearedHandler = payload => HandleRoomCleared(controller, payload);
                controller.RoomCleared += roomClearedHandler;
                roomClearedHandlers.Add(controller, roomClearedHandler);

                if (clearPolicy == LargeNodeClearPolicy.NoActiveCombat)
                {
                    Action<RoomCombatStartedPayload> combatStartedHandler = payload => HandleRoomCombatStarted(controller, payload);
                    controller.RoomCombatStarted += combatStartedHandler;
                    combatStartedHandlers.Add(controller, combatStartedHandler);
                }
            }

            Log($"Tracking {requiredControllers.Count} room controller(s), policy={clearPolicy}.");

            if (requiredControllers.Count == 0 && requiredIds.Count == 0 && unlockIfNoRequiredRooms)
            {
                MarkNodeCleared();
            }
        }

        private void BuildRequiredIds()
        {
            if (requiredRoomIds == null)
            {
                return;
            }

            for (int i = 0; i < requiredRoomIds.Length; i++)
            {
                string id = requiredRoomIds[i];
                if (!string.IsNullOrWhiteSpace(id))
                {
                    requiredIds.Add(id);
                }
            }
        }

        private bool ShouldTrack(RoomEntryRuntimeController controller)
        {
            RoomData roomData = controller.RoomData;
            string roomId = controller.RoomId;

            switch (clearPolicy)
            {
                case LargeNodeClearPolicy.ExplicitRequiredRoomIds:
                    return !string.IsNullOrEmpty(roomId) && requiredIds.Contains(roomId);
                case LargeNodeClearPolicy.AllDiscoveredRooms:
                    return roomData != null;
                case LargeNodeClearPolicy.AllCombatRooms:
                    return roomData != null && roomData.RoomType == StageRoomType.Combat;
                case LargeNodeClearPolicy.NoActiveCombat:
                    return roomData != null &&
                           (roomData.RoomType == StageRoomType.Combat ||
                            roomData.RoomType == StageRoomType.Boss);
                default:
                    return false;
            }
        }

        private void HandleRoomCombatStarted(RoomEntryRuntimeController controller, RoomCombatStartedPayload payload)
        {
            if (clearPolicy != LargeNodeClearPolicy.NoActiveCombat || controller == null)
            {
                return;
            }

            if (activeCombatControllers.Add(controller))
            {
                LockExits();
                Log($"Combat started '{payload.RoomId}'. Exit trigger(s) locked. active={activeCombatControllers.Count}");
            }
        }

        private void HandleRoomCleared(RoomEntryRuntimeController controller, RoomClearedPayload payload)
        {
            if (nodeCleared || controller == null)
            {
                return;
            }

            if (clearPolicy == LargeNodeClearPolicy.NoActiveCombat)
            {
                activeCombatControllers.Remove(controller);
                Log($"Combat ended '{payload.RoomId}'. active={activeCombatControllers.Count}");

                if (activeCombatControllers.Count == 0)
                {
                    UnlockExits();
                }

                return;
            }

            completedControllers.Add(controller);
            if (!string.IsNullOrEmpty(payload.RoomId))
            {
                completedIds.Add(payload.RoomId);
            }

            Log($"Room cleared '{payload.RoomId}'. {completedControllers.Count}/{requiredControllers.Count}");

            if (IsClearConditionMet())
            {
                MarkNodeCleared();
            }
        }

        private bool IsClearConditionMet()
        {
            if (clearPolicy == LargeNodeClearPolicy.ExplicitRequiredRoomIds && requiredIds.Count > 0)
            {
                return completedIds.IsSupersetOf(requiredIds);
            }

            return requiredControllers.Count > 0 && completedControllers.IsSupersetOf(requiredControllers);
        }

        private void MarkNodeCleared()
        {
            if (nodeCleared)
            {
                return;
            }

            nodeCleared = true;
            UnlockExits();
            Log("Large node cleared. Exit trigger(s) unlocked.");
        }

        private void LockExits()
        {
            if (exitTriggers == null)
            {
                return;
            }

            for (int i = 0; i < exitTriggers.Length; i++)
            {
                if (exitTriggers[i] != null)
                {
                    exitTriggers[i].Lock();
                }
            }
        }

        private void UnlockExits()
        {
            if (exitTriggers == null)
            {
                return;
            }

            for (int i = 0; i < exitTriggers.Length; i++)
            {
                if (exitTriggers[i] != null)
                {
                    exitTriggers[i].Unlock();
                }
            }
        }

        private void UnsubscribeAll()
        {
            foreach (KeyValuePair<RoomEntryRuntimeController, Action<RoomClearedPayload>> entry in roomClearedHandlers)
            {
                if (entry.Key != null)
                {
                    entry.Key.RoomCleared -= entry.Value;
                }
            }

            roomClearedHandlers.Clear();

            foreach (KeyValuePair<RoomEntryRuntimeController, Action<RoomCombatStartedPayload>> entry in combatStartedHandlers)
            {
                if (entry.Key != null)
                {
                    entry.Key.RoomCombatStarted -= entry.Value;
                }
            }

            combatStartedHandlers.Clear();
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[LargeNodeClearTracker] " + message, this);
            }
        }
    }
}
