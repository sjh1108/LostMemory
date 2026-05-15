using System;
using LostMemory.Networking.Common;
using LostMemory.Player;
using LostMemory.Relics;
using MoreMountains.TopDownEngine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace LostMemory.Stage
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [AddComponentMenu("Lost Memory/Stage/Stage Route Manager")]
    public sealed class StageRouteManager : NetworkBehaviour
    {
        [Serializable]
        public sealed class RouteNode
        {
            [SerializeField] private string nodeId = string.Empty;
            [SerializeField] private string sceneName = string.Empty;
            [SerializeField] private string editorScenePath = string.Empty;
            [SerializeField] private string entrySpawnId = "default";
            [SerializeField] private string exitTriggerId = "default";
            [SerializeField] private LoadSceneMode loadSceneMode = LoadSceneMode.Single;

            public RouteNode()
            {
            }

            public RouteNode(
                string nodeId,
                string sceneName,
                string editorScenePath,
                string entrySpawnId,
                string exitTriggerId,
                LoadSceneMode loadSceneMode)
            {
                this.nodeId = nodeId;
                this.sceneName = sceneName;
                this.editorScenePath = editorScenePath;
                this.entrySpawnId = entrySpawnId;
                this.exitTriggerId = exitTriggerId;
                this.loadSceneMode = loadSceneMode;
            }

            public string NodeId => nodeId;
            public string SceneName => sceneName;
            public string EditorScenePath => editorScenePath;
            public string EntrySpawnId => entrySpawnId;
            public string ExitTriggerId => exitTriggerId;
            public LoadSceneMode LoadSceneMode => loadSceneMode;
        }

        public static StageRouteManager Instance { get; private set; }

        [Header("Route")]
        [SerializeField] private RouteNode[] routeNodes = Array.Empty<RouteNode>();
        [SerializeField, Min(0)] private int initialNodeIndex;

        [Header("Behavior")]
        [SerializeField] private bool dontDestroyOnLoad = true;
        [SerializeField] private bool placePlayersAfterSceneLoad = true;
        [SerializeField] private bool destroyWhenSceneOutsideRoute = true;
        [SerializeField] private Vector3 playerSpawnOffset = new Vector3(0.75f, 0f, 0f);
        [SerializeField] private bool debugLogging = true;

        private int currentNodeIndex;
        private bool loadInProgress;
        private string pendingSpawnId = string.Empty;

        public int CurrentNodeIndex => currentNodeIndex;
        public bool LoadInProgress => loadInProgress;

        public void ConfigureRouteNodes(RouteNode[] nodes, int nodeIndex, string overrideEntrySpawnId, bool placePlayers)
        {
            routeNodes = nodes ?? Array.Empty<RouteNode>();
            initialNodeIndex = routeNodes.Length > 0
                ? Mathf.Clamp(nodeIndex, 0, routeNodes.Length - 1)
                : 0;

            InitializeRouteNode(initialNodeIndex, overrideEntrySpawnId, placePlayers);
        }

        private void OnValidate()
        {
            if (routeNodes == null || routeNodes.Length == 0)
            {
                initialNodeIndex = 0;
                return;
            }

            initialNodeIndex = Mathf.Clamp(initialNodeIndex, 0, routeNodes.Length - 1);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[StageRouteManager] Duplicate instance detected. Destroying.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            currentNodeIndex = Mathf.Clamp(initialNodeIndex, 0, Mathf.Max(0, routeNodes.Length - 1));

            if (dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;

            if (Instance == this)
            {
                Instance = null;
            }
        }

        public bool RequestAdvanceRouteNode(string triggerId, Character requester)
        {
            if (loadInProgress)
            {
                Log("Advance request ignored because a route load is already in progress.");
                return false;
            }

            if (HostAuthority.IsNetworkSessionActive && !IsServer)
            {
                if (!IsSpawned)
                {
                    Debug.LogWarning("[StageRouteManager] Client cannot send route advance RPC because this manager is not spawned.", this);
                    return false;
                }

                RequestAdvanceRouteNodeServerRpc(triggerId);
                return true;
            }

            return TryAdvanceRouteNode(triggerId, requester, null);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestAdvanceRouteNodeServerRpc(string triggerId, ServerRpcParams rpcParams = default)
        {
            TryAdvanceRouteNode(triggerId, null, rpcParams.Receive.SenderClientId);
        }

        public bool CanAdvanceRouteNode(string triggerId)
        {
            if (loadInProgress || routeNodes == null || routeNodes.Length == 0)
            {
                return false;
            }

            if (currentNodeIndex < 0 || currentNodeIndex >= routeNodes.Length)
            {
                return false;
            }

            if (currentNodeIndex + 1 >= routeNodes.Length)
            {
                return false;
            }

            RouteNode current = routeNodes[currentNodeIndex];
            return current == null ||
                   string.IsNullOrEmpty(current.ExitTriggerId) ||
                   current.ExitTriggerId == triggerId;
        }

        public bool LoadRouteNode(int nodeIndex)
        {
            if (!IsAuthorityToLoad())
            {
                Debug.LogWarning("[StageRouteManager] LoadRouteNode ignored on non-server client.", this);
                return false;
            }

            return TryLoadRouteNode(nodeIndex);
        }

        public bool InitializeRouteNode(int nodeIndex, string overrideEntrySpawnId, bool placePlayers)
        {
            if (routeNodes == null || nodeIndex < 0 || nodeIndex >= routeNodes.Length)
            {
                Debug.LogWarning($"[StageRouteManager] Invalid initial route node index {nodeIndex}.", this);
                return false;
            }

            RouteNode node = routeNodes[nodeIndex];
            if (node == null)
            {
                Debug.LogWarning($"[StageRouteManager] Route node {nodeIndex} is null.", this);
                return false;
            }

            currentNodeIndex = nodeIndex;
            pendingSpawnId = string.Empty;
            loadInProgress = false;

            string spawnId = string.IsNullOrWhiteSpace(overrideEntrySpawnId)
                ? node.EntrySpawnId
                : overrideEntrySpawnId;

            if (placePlayersAfterSceneLoad && placePlayers)
            {
                PlacePlayersAtSpawn(spawnId);
            }

            Log($"Initialized route node {nodeIndex}: nodeId='{node.NodeId}', scene='{node.SceneName}', spawn='{spawnId}'.");
            return true;
        }

        private bool TryAdvanceRouteNode(string triggerId, Character requester, ulong? requesterClientId)
        {
            if (!CanAdvanceRouteNode(triggerId))
            {
                Debug.LogWarning(
                    $"[StageRouteManager] Route advance rejected. triggerId='{triggerId}', currentIndex={currentNodeIndex}, requesterClientId={requesterClientId?.ToString() ?? "offline"}.",
                    this);
                return false;
            }

            return TryLoadRouteNode(currentNodeIndex + 1);
        }

        private bool TryLoadRouteNode(int nodeIndex)
        {
            if (routeNodes == null || nodeIndex < 0 || nodeIndex >= routeNodes.Length)
            {
                Debug.LogWarning($"[StageRouteManager] Invalid route node index {nodeIndex}.", this);
                return false;
            }

            RouteNode node = routeNodes[nodeIndex];
            if (node == null || string.IsNullOrWhiteSpace(node.SceneName))
            {
                Debug.LogWarning($"[StageRouteManager] Route node {nodeIndex} has no scene name.", this);
                return false;
            }

            if (!CanLoadNode(node))
            {
                Debug.LogWarning($"[StageRouteManager] Scene '{node.SceneName}' is not available for route node '{node.NodeId}'.", this);
                return false;
            }

            int previousIndex = currentNodeIndex;
            currentNodeIndex = nodeIndex;
            pendingSpawnId = node.EntrySpawnId;
            loadInProgress = true;

            // 씬 전환 직전 Player 의 인벤토리/HP 를 PlayerRunState 로 스냅샷. 새 씬에서
            // Player 가 다시 스폰될 때 인벤토리/Health 컴포넌트들이 자체 복구한다.
            CapturePlayerSnapshot();

            bool started = StartSceneLoad(node);
            if (!started)
            {
                currentNodeIndex = previousIndex;
                pendingSpawnId = string.Empty;
                loadInProgress = false;
                return false;
            }

            Log($"Loading route node {nodeIndex}: nodeId='{node.NodeId}', scene='{node.SceneName}', spawn='{node.EntrySpawnId}'.");
            return true;
        }

        private bool IsAuthorityToLoad()
        {
            return !HostAuthority.IsNetworkSessionActive || IsServer;
        }

        private bool CanLoadNode(RouteNode node)
        {
            if (HostAuthority.IsNetworkSessionActive)
            {
                return Application.CanStreamedLevelBeLoaded(node.SceneName);
            }

            if (Application.CanStreamedLevelBeLoaded(node.SceneName))
            {
                return true;
            }

#if UNITY_EDITOR
            return !string.IsNullOrWhiteSpace(node.EditorScenePath);
#else
            return false;
#endif
        }

        private bool StartSceneLoad(RouteNode node)
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager != null && networkManager.IsListening)
            {
                if (!networkManager.IsServer)
                {
                    Debug.LogWarning("[StageRouteManager] Network scene load must be started by the server.", this);
                    return false;
                }

                networkManager.SceneManager.LoadScene(node.SceneName, node.LoadSceneMode);
                return true;
            }

            if (Application.CanStreamedLevelBeLoaded(node.SceneName))
            {
                SceneManager.LoadScene(node.SceneName, node.LoadSceneMode);
                return true;
            }

            return LoadNodeInEditor(node);
        }

        private bool LoadNodeInEditor(RouteNode node)
        {
#if UNITY_EDITOR
            if (string.IsNullOrWhiteSpace(node.EditorScenePath))
            {
                return false;
            }

            EditorSceneManager.LoadSceneInPlayMode(
                node.EditorScenePath,
                new LoadSceneParameters(node.LoadSceneMode));
            return true;
#else
            return false;
#endif
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (destroyWhenSceneOutsideRoute && scene.IsValid() && !IsRouteScene(scene.name))
            {
                Log($"Destroying route manager after leaving route scene '{scene.name}'.");
                Destroy(gameObject);
                return;
            }

            if (!loadInProgress)
            {
                return;
            }

            loadInProgress = false;

            if (placePlayersAfterSceneLoad)
            {
                PlacePlayersAtSpawn(pendingSpawnId);
            }

            pendingSpawnId = string.Empty;
        }

        private bool IsRouteScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName) || routeNodes == null)
            {
                return false;
            }

            for (int i = 0; i < routeNodes.Length; i++)
            {
                RouteNode node = routeNodes[i];
                if (node != null && node.SceneName == sceneName)
                {
                    return true;
                }
            }

            return false;
        }

        private void CapturePlayerSnapshot()
        {
            PlayerRunState runState = PlayerRunState.Instance;
            if (runState == null)
            {
                return;
            }

            Character[] characters = FindObjectsOfType<Character>();
            Character player = null;
            for (int i = 0; i < characters.Length; i++)
            {
                Character c = characters[i];
                if (c != null && c.CharacterType == Character.CharacterTypes.Player)
                {
                    player = c;
                    break;
                }
            }

            if (player == null)
            {
                return;
            }

            PlayerSnapshot snapshot = default;

            PlayerRelicInventory relicInventory = player.GetComponentInChildren<PlayerRelicInventory>(true);
            if (relicInventory != null)
            {
                relicInventory.CaptureSnapshotInto(ref snapshot);
            }

            PlayerConsumableInventory consumableInventory = player.GetComponentInChildren<PlayerConsumableInventory>(true);
            if (consumableInventory != null)
            {
                snapshot.ConsumableSlots = consumableInventory.CaptureSnapshot();
            }

            PlayerHealthSnapshotter healthSnapshotter = player.GetComponentInChildren<PlayerHealthSnapshotter>(true);
            if (healthSnapshotter != null)
            {
                healthSnapshotter.CaptureInto(ref snapshot);
            }

            runState.Capture(snapshot);
        }

        private void PlacePlayersAtSpawn(string spawnId)
        {
            RouteNodeSpawnPoint spawnPoint = ResolveSpawnPoint(spawnId);
            if (spawnPoint == null)
            {
                Debug.LogWarning($"[StageRouteManager] No RouteNodeSpawnPoint found for spawnId '{spawnId}'.", this);
                return;
            }

            Character[] characters = FindObjectsOfType<Character>();
            int playerIndex = 0;
            for (int i = 0; i < characters.Length; i++)
            {
                Character character = characters[i];
                if (character == null || character.CharacterType != Character.CharacterTypes.Player)
                {
                    continue;
                }

                spawnPoint.Place(character, playerSpawnOffset * playerIndex);
                playerIndex++;
            }

            Log($"Placed {playerIndex} player(s) at spawnId '{spawnId}'.");
        }

        private static RouteNodeSpawnPoint ResolveSpawnPoint(string spawnId)
        {
            RouteNodeSpawnPoint[] spawnPoints = FindObjectsOfType<RouteNodeSpawnPoint>();
            RouteNodeSpawnPoint fallback = null;
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                RouteNodeSpawnPoint point = spawnPoints[i];
                if (point == null)
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback = point;
                }

                if (point.Matches(spawnId))
                {
                    return point;
                }
            }

            return fallback;
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[StageRouteManager] " + message, this);
            }
        }
    }
}
