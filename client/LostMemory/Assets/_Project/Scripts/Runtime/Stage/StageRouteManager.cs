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
        [SerializeField] private bool completeDefaultRouteIfIncomplete = true;
        [SerializeField] private Vector3 playerSpawnOffset = new Vector3(0.75f, 0f, 0f);
        [SerializeField] private bool debugLogging = true;

        private int currentNodeIndex;
        private bool loadInProgress;
        private string pendingSpawnId = string.Empty;

        public int CurrentNodeIndex => currentNodeIndex;
        public int RouteNodeCount => routeNodes?.Length ?? 0;
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
            CompleteDefaultRouteIfIncompleteForActiveScene();
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

        private void CompleteDefaultRouteIfIncompleteForActiveScene()
        {
            if (!completeDefaultRouteIfIncomplete)
            {
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() ||
                !StageRouteDefaults.TryResolveRouteIndex(activeScene.name, out int routeNodeIndex))
            {
                return;
            }

            StageRouteManager.RouteNode[] defaultNodes = StageRouteDefaults.CreateRouteNodes();
            if (HasRouteCoverage(defaultNodes))
            {
                return;
            }

            routeNodes = defaultNodes;
            initialNodeIndex = routeNodeIndex;
            Log($"Completed default dungeon route for scene '{activeScene.name}' at node index {routeNodeIndex}.");
        }

        private bool HasRouteCoverage(RouteNode[] expectedNodes)
        {
            if (routeNodes == null || expectedNodes == null || routeNodes.Length < expectedNodes.Length)
            {
                return false;
            }

            for (int i = 0; i < expectedNodes.Length; i++)
            {
                RouteNode current = routeNodes[i];
                RouteNode expected = expectedNodes[i];
                if (current == null ||
                    expected == null ||
                    current.SceneName != expected.SceneName ||
                    current.ExitTriggerId != expected.ExitTriggerId)
                {
                    return false;
                }
            }

            return true;
        }

        // [DiagRoute] 라우트 advance 가 안 넘어가는 원인 추적용 throttle 로그.
        // 1초에 한 번만 찍어서 Update-loop 스팸 방지. 사용처: route 가 또 안 넘어갈 때만 true 로.
        [Header("Diagnostics (route advance trace)")]
        [SerializeField] private bool _diagRouteLogging = false;
        private float _diagNextLogTimeAdvance;
        private float _diagNextLogTimeRpc;

        public bool RequestAdvanceRouteNode(string triggerId, Character requester)
        {
            // [DiagRoute] entry — 진입 시점의 권위/스폰/loadInProgress 스냅샷.
            if (_diagRouteLogging && Time.unscaledTime >= _diagNextLogTimeAdvance)
            {
                _diagNextLogTimeAdvance = Time.unscaledTime + 1f;
                var nm = NetworkManager.Singleton;
                Debug.Log(
                    $"[DiagRoute] RequestAdvanceRouteNode ENTER triggerId='{triggerId}' " +
                    $"sessionActive={HostAuthority.IsNetworkSessionActive} " +
                    $"IsServer={IsServer} IsSpawned={IsSpawned} loadInProgress={loadInProgress} " +
                    $"currentIndex={currentNodeIndex}/{(routeNodes != null ? routeNodes.Length : 0)} " +
                    $"InstanceIsThis={(Instance == this)} " +
                    $"NM(host={nm?.IsHost},listening={nm?.IsListening},localId={nm?.LocalClientId}) " +
                    $"activeScene='{SceneManager.GetActiveScene().name}'",
                    this);
            }

            if (loadInProgress)
            {
                Log("Advance request ignored because a route load is already in progress.");
                return false;
            }

            // [Fix-Route-DDOL] StageRouteManager 가 이전 씬에서 scene-placed → DDOL 된 케이스 대응.
            //
            // 증상: 진단 로그에서 host 임에도 `IsServer=False`, `IsSpawned=False` 로 잡혔다.
            // 원인: manager 가 NGO 네트워크 씬 sync 대상이 아닌 곳에서 한 번도 spawn 안 된 채 DDOL.
            //       → NetworkBehaviour.IsServer/IsSpawned 가 둘 다 false. ServerRpc 도 못 보냄.
            //
            // 호스트의 advance 는 manager 의 NetworkObject 가 아니라
            // `NetworkManager.Singleton.SceneManager.LoadScene` 로 진행되므로,
            // host 권위만 NetworkManager 레벨로 판정하면 spawn 여부 무관하게 동작한다.
            // (NetworkSceneManager.LoadScene 이 자체적으로 모든 클라에 씬 sync 를 broadcast 함.)
            bool isHostByNm = HostAuthority.IsHost; // NetworkManager.Singleton.IsHost (또는 싱글 fallback)
            if (isHostByNm)
            {
                return TryAdvanceRouteNode(triggerId, requester, null);
            }

            if (HostAuthority.IsNetworkSessionActive)
            {
                // 게스트: ServerRpc 필요. manager NetworkObject 가 spawn 되어 있어야 보낼 수 있다.
                if (!IsSpawned)
                {
                    // 게스트는 호스트가 advance 할 때까지 조용히 대기. 진단 모드일 때만 로그.
                    if (_diagRouteLogging && Time.unscaledTime >= _diagNextLogTimeRpc)
                    {
                        _diagNextLogTimeRpc = Time.unscaledTime + 1f;
                        Debug.LogWarning(
                            $"[StageRouteManager] Guest cannot send route advance RPC — manager not network-spawned. " +
                            $"Host's portal touch will drive scene change. " +
                            $"go='{gameObject.name}' scene='{gameObject.scene.name}' " +
                            $"InstanceIsThis={(Instance == this)} " +
                            $"Instance.IsSpawned={(Instance != null ? Instance.IsSpawned.ToString() : "no-instance")}",
                            this);
                    }
                    return false;
                }

                RequestAdvanceRouteNodeServerRpc(triggerId);
                return true;
            }

            // 싱글
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

        public bool CanAdvanceCurrentRouteNode()
        {
            return TryGetCurrentRouteExitTriggerId(out string triggerId) &&
                   CanAdvanceRouteNode(triggerId);
        }

        public bool RequestAdvanceCurrentRouteNode(Character requester)
        {
            if (!TryGetCurrentRouteExitTriggerId(out string triggerId))
            {
                return false;
            }

            return RequestAdvanceRouteNode(triggerId, requester);
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

        private bool TryGetCurrentRouteExitTriggerId(out string triggerId)
        {
            triggerId = string.Empty;

            if (routeNodes == null || currentNodeIndex < 0 || currentNodeIndex >= routeNodes.Length)
            {
                return false;
            }

            RouteNode current = routeNodes[currentNodeIndex];
            if (current == null)
            {
                return false;
            }

            triggerId = current.ExitTriggerId;
            return true;
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

            // 멀티: 자기 NGO LocalClient.PlayerObject 의 스냅샷만 캡처.
            // 솔로/미할당이면 씬 안 첫 Player Character fallback.
            Character player = ResolveLocalPlayerCharacter();

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

            // CL-230: 무기 모드 (Sword/Dagger/Bow/Staff/Flamethrower) 도 씬 전환 시 보존.
            // WeaponModeController.ApplyMode 가 WeaponUpgradeService SO 교체까지 자동 호출하므로
            // 이 한 값만 복구하면 Dagger SO 까지 자동 전파됨.
            LostMemory.TestKhi.WeaponModeController weaponMode = player.GetComponentInChildren<LostMemory.TestKhi.WeaponModeController>(true);
            if (weaponMode != null)
            {
                weaponMode.CaptureInto(ref snapshot);
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

            NetworkManager nm = NetworkManager.Singleton;
            bool multi = nm != null && nm.IsListening;
            if (multi)
            {
                // 멀티: 각 클라가 자기 local PlayerObject 만 spawn 으로 이동.
                // owner-authoritative PlayerMovementSync 와 부합 — non-owner 측에서 이동시키면 다음 frame sync 로 race.
                // offset 은 OwnerClientId 기반 deterministic — 모든 클라에서 동일 layout (4명이 살짝 stagger).
                Character local = ResolveLocalPlayerCharacter();
                if (local == null)
                {
                    Log($"PlacePlayersAtSpawn: local PlayerObject 미할당 — skip. spawnId='{spawnId}'");
                    return;
                }
                int index = ResolveLocalPlayerSpawnIndex();
                spawnPoint.Place(local, playerSpawnOffset * index);
                Log($"Placed local player at spawnId '{spawnId}' index={index}.");
                return;
            }

            // 솔로: 기존 동작 — 씬 안 모든 Player Character 를 stagger 배치.
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

        /// <summary>
        /// 멀티: NGO LocalClient.PlayerObject 의 Character 컴포넌트 반환.
        /// 솔로 또는 미할당 시 씬 안 첫 Player Character fallback.
        /// </summary>
        private static Character ResolveLocalPlayerCharacter()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening)
            {
                NetworkClient local = nm.LocalClient;
                if (local != null && local.PlayerObject != null)
                {
                    Character c = local.PlayerObject.GetComponentInChildren<Character>(true);
                    if (c == null) c = local.PlayerObject.GetComponent<Character>();
                    if (c != null) return c;
                }
            }

            Character[] characters = FindObjectsOfType<Character>();
            for (int i = 0; i < characters.Length; i++)
            {
                Character c = characters[i];
                if (c != null && c.CharacterType == Character.CharacterTypes.Player) return c;
            }
            return null;
        }

        /// <summary>
        /// OwnerClientId 를 ConnectedClientsIds 비교 순서에 매핑해 0-based index 반환.
        /// 모든 클라에서 동일 결과 → spawn offset stagger 일관.
        /// </summary>
        private static int ResolveLocalPlayerSpawnIndex()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return 0;
            NetworkClient local = nm.LocalClient;
            if (local == null) return 0;
            ulong myId = local.ClientId;
            int countBelow = 0;
            foreach (ulong id in nm.ConnectedClientsIds)
            {
                if (id < myId) countBelow++;
            }
            return countBelow;
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
