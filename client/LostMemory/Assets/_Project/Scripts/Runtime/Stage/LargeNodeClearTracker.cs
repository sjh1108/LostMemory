using System;
using System.Collections.Generic;
using LostMemory.Stage.Data;
using LostMemory.Rendering;
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
        [SerializeField] private GameObject[] blockingRoots = Array.Empty<GameObject>();
        [SerializeField] private bool disableBlockingRootsWhenUnlocked = true;
        [SerializeField] private bool showBlockingRootVisuals = true;
        [SerializeField] private Color blockingRootVisualColor = new Color(0.25f, 0.85f, 1f, 0.55f);
        [SerializeField] private string blockingRootSortingLayerName = "Foreground";
        [SerializeField] private int blockingRootSortingOrder = 16;
        [SerializeField, Min(0f)] private float blockingRootVisualPadding = 0.08f;
        [SerializeField] private bool includeInactiveControllers = true;
        [SerializeField] private bool autoDiscoverOnEnable = true;
        [SerializeField] private bool unlockIfNoRequiredRooms;
        [SerializeField] private bool allowExitDuringActiveCombat = true;
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
        private const string BlockingVisualName = "__LargeNodeBlockingVisual";
        private static Sprite blockingVisualSprite;
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
                if (allowExitDuringActiveCombat)
                {
                    UnlockExits();
                    Log($"Combat started '{payload.RoomId}'. Exit trigger(s) kept unlocked. active={activeCombatControllers.Count}");
                }
                else
                {
                    LockExits();
                    Log($"Combat started '{payload.RoomId}'. Exit trigger(s) locked. active={activeCombatControllers.Count}");
                }
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

                if (allowExitDuringActiveCombat || activeCombatControllers.Count == 0)
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
            ApplyBlockingRoots(true);

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
            ApplyBlockingRoots(false);

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

        private void ApplyBlockingRoots(bool blocking)
        {
            if (blockingRoots == null)
            {
                return;
            }

            for (int i = 0; i < blockingRoots.Length; i++)
            {
                GameObject root = blockingRoots[i];
                if (root == null)
                {
                    continue;
                }

                bool shouldBeActive = blocking || !disableBlockingRootsWhenUnlocked;
                root.SetActive(shouldBeActive);
                if (!shouldBeActive)
                {
                    continue;
                }

                Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(includeInactive: true);
                if (blocking && showBlockingRootVisuals)
                {
                    EnsureBlockingRootVisual(root, colliders);
                }

                SetBlockingCollidersEnabled(colliders, blocking);
                SetBlockingRenderersEnabled(root, blocking);
            }
        }

        private static void SetBlockingCollidersEnabled(Collider2D[] colliders, bool enabled)
        {
            if (colliders == null)
            {
                return;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider != null && !collider.isTrigger)
                {
                    collider.enabled = enabled;
                }
            }
        }

        private static void SetBlockingRenderersEnabled(GameObject root, bool enabled)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = enabled;
                }
            }
        }

        private void EnsureBlockingRootVisual(GameObject root, Collider2D[] colliders)
        {
            BoxCollider2D boundsSource = FindVisualBoundsSource(colliders);
            if (boundsSource == null)
            {
                return;
            }

            Transform visualTransform = root.transform.Find(BlockingVisualName);
            GameObject visualObject = visualTransform != null ? visualTransform.gameObject : null;
            if (visualObject == null)
            {
                visualObject = new GameObject(BlockingVisualName);
                visualTransform = visualObject.transform;
                visualTransform.SetParent(root.transform, false);

                SpriteRenderer spriteRenderer = visualObject.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = GetOrCreateBlockingVisualSprite();
                RuntimeSpriteMaterialUtility.ApplySpriteMaterial(spriteRenderer);
                visualObject.AddComponent<BlockedPulse>();
            }

            visualTransform.SetParent(boundsSource.transform, false);
            visualTransform.localPosition = new Vector3(boundsSource.offset.x, boundsSource.offset.y, -0.01f);
            visualTransform.localRotation = Quaternion.identity;
            visualTransform.localScale = new Vector3(
                Mathf.Max(0.01f, boundsSource.size.x + blockingRootVisualPadding * 2f),
                Mathf.Max(0.01f, boundsSource.size.y + blockingRootVisualPadding * 2f),
                1f);

            SpriteRenderer renderer = visualObject.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                return;
            }

            renderer.sprite = GetOrCreateBlockingVisualSprite();
            renderer.color = blockingRootVisualColor;
            renderer.sortingLayerName = blockingRootSortingLayerName;
            renderer.sortingOrder = blockingRootSortingOrder;
            renderer.enabled = true;
        }

        private static BoxCollider2D FindVisualBoundsSource(Collider2D[] colliders)
        {
            if (colliders == null)
            {
                return null;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] is BoxCollider2D boxCollider && !boxCollider.isTrigger)
                {
                    return boxCollider;
                }
            }

            return null;
        }

        private static Sprite GetOrCreateBlockingVisualSprite()
        {
            if (blockingVisualSprite != null)
            {
                return blockingVisualSprite;
            }

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "LargeNodeBlockingVisualTexture",
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            blockingVisualSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            blockingVisualSprite.name = "LargeNodeBlockingVisualSprite";
            blockingVisualSprite.hideFlags = HideFlags.HideAndDontSave;
            return blockingVisualSprite;
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
