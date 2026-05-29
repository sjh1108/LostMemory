using System;
using System.Collections.Generic;
using LostMemory.Networking.Common;
using LostMemory.Player;
using LostMemory.Relics;
using LostMemory.TestKhi;
using MoreMountains.TopDownEngine;
using Unity.Collections;
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
        [SerializeField] private bool debugLogging = false;

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

            // [Multi Local Teleport] CustomMessagingManager 기반 ready 게이트.
            // NetworkBehaviour.OnNetworkSpawn 에 의존하지 않는 이유:
            // - 게스트가 Town_Preview 를 NGO scene sync 가 아닌 경로로 로드하면 scene-placed NetworkObject 가 spawn 안 됨.
            // - CustomMessage 는 NetworkManager.IsListening 만으로 동작 (NetworkObject spawn 불필요).
            NetworkManager nm = NetworkManager.Singleton;
            if (nm != null)
            {
                nm.OnServerStarted += HandleServerStartedForLocalTp;
                nm.OnClientConnectedCallback += HandleClientConnectedForLocalTp;
                nm.OnClientDisconnectCallback += HandleClientDisconnectForLocalTp;

                // 이미 시작된 상태라면 즉시 등록.
                if (nm.IsServer)
                {
                    RegisterServerCustomMessageHandlers();
                }
                if (nm.IsConnectedClient)
                {
                    RegisterClientCustomMessageHandlers();
                }
            }

            // KhiDownController 의 Defeated 전이 글로벌 구독. 서버에서만 처리(핸들러 내부 가드).
            // pending ready set 에 합의 충족이 'required 감소' 로 가능해질 수 있어 자동 재평가 필요.
            KhiDownController.AnyPlayerDefeated += HandleAnyPlayerDefeated;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;

            NetworkManager nm = NetworkManager.Singleton;
            if (nm != null)
            {
                nm.OnServerStarted -= HandleServerStartedForLocalTp;
                nm.OnClientConnectedCallback -= HandleClientConnectedForLocalTp;
                nm.OnClientDisconnectCallback -= HandleClientDisconnectForLocalTp;
                UnregisterCustomMessageHandlers();
            }

            KhiDownController.AnyPlayerDefeated -= HandleAnyPlayerDefeated;

            if (Instance == this)
            {
                Instance = null;
            }
        }

        // 어느 클라이언트의 player 든 Defeated 되면 서버 측에서 pending ready set 전부 재평가.
        // ResolveLocalTeleportRequiredCount 의 결과가 줄어들어 기존 set 만으로 합의 충족될 수 있음.
        private void HandleAnyPlayerDefeated(KhiDownController dc)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening || !nm.IsServer) return;
            ReevaluateAllPendingReadySets();
        }

        // 두 ready 게이트 (LocalTeleport / RouteAdvance) pending set 을 한 번에 재평가.
        // Dictionary 키 순회 중 EvaluateAndFire 가 set 을 Remove 하므로 snapshot 후 순회.
        private void ReevaluateAllPendingReadySets()
        {
            if (_localTeleportReady.Count > 0)
            {
                List<string> keys = new List<string>(_localTeleportReady.Keys);
                for (int i = 0; i < keys.Count; i++)
                {
                    if (_localTeleportReady.TryGetValue(keys[i], out HashSet<ulong> set))
                    {
                        EvaluateAndFireLocalTeleportReady(keys[i], set);
                    }
                }
            }
            if (_routeAdvanceReady.Count > 0)
            {
                List<string> keys = new List<string>(_routeAdvanceReady.Keys);
                for (int i = 0; i < keys.Count; i++)
                {
                    if (_routeAdvanceReady.TryGetValue(keys[i], out HashSet<ulong> set))
                    {
                        EvaluateAndFireRouteAdvanceReady(keys[i], set);
                    }
                }
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

        /// <summary>
        /// 멀티에서 RouteNodeExitTrigger 가 F 누름을 ready 게이트에 위임할 때 사용.
        /// 싱글 → 즉시 advance. 멀티 호스트 → 직접 서버 처리. 멀티 게스트 → CustomMessage 로 서버에 전송.
        /// 전원 ready 합의 시 서버가 TryAdvanceRouteNode 호출 → NGO SceneManager.LoadScene 으로 모든 클라 동기 로드.
        /// </summary>
        public bool RequestRouteAdvanceReady(string triggerId)
        {
            if (string.IsNullOrEmpty(triggerId)) return false;
            if (loadInProgress)
            {
                Log("Route advance ready ignored — load in progress.");
                return false;
            }

            // 싱글: 즉시 advance.
            if (!HostAuthority.IsNetworkSessionActive)
            {
                return TryAdvanceRouteNode(triggerId, null, null);
            }

            // 멀티 호스트: 서버 로직 직접 호출.
            if (HostAuthority.IsHost)
            {
                HandleRouteAdvanceReadyOnServer(triggerId, NetworkManager.Singleton != null
                    ? NetworkManager.Singleton.LocalClientId
                    : 0UL);
                return true;
            }

            // 멀티 게스트: CustomMessage 로 서버 전송.
            return SendRouteAdvReadyMessageToServer(triggerId);
        }

        /// <summary>
        /// 트리거 이탈/잠금 시 ready 취소.
        /// </summary>
        public void CancelRouteAdvanceReady(string triggerId)
        {
            if (string.IsNullOrEmpty(triggerId) || !HostAuthority.IsNetworkSessionActive) return;

            if (HostAuthority.IsHost)
            {
                RemoveRouteAdvanceReadyOnServer(triggerId, NetworkManager.Singleton != null
                    ? NetworkManager.Singleton.LocalClientId
                    : 0UL);
                return;
            }

            SendRouteAdvCancelMessageToServer(triggerId);
        }

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

        // ===========================================================================
        // [Multi Local Teleport Ready Gate]
        //
        // RouteNodeExitTrigger.useLocalTeleport=1 인 트리거(같은 씬 안 워프) 에서
        // "현재 접속한 클라이언트 N명 전원이 트리거 영역에서 F 를 눌러야" 발동되도록 하는 합의 게이트.
        // - 트리거 자체는 MonoBehaviour 라 RPC 불가 → 본 매니저가 RPC 위임.
        // - **CustomMessagingManager** 사용 (NetworkBehaviour spawn 불필요):
        //     게스트의 scene-placed NetworkObject 가 NGO scene sync 누락으로 spawn 안 되는
        //     preview/dev 시나리오에서도 ready 게이트가 동작하도록.
        // - PlayerMovementSync 가 owner-auth NetworkTransform 이라 서버가 게스트 캐릭터를 직접 못 옮긴다.
        //   → 합의 시 모든 클라이언트에 CustomMessage broadcast, 각 클라가 자기 owned Character 를 로컬 이동.
        // - 트리거 이탈/disconnect 시 ready 자동 정리.
        // ===========================================================================

        private const string LocalTpReadyMsg = "LM.LocalTp.Ready";
        private const string LocalTpCancelMsg = "LM.LocalTp.Cancel";
        private const string LocalTpFireMsg = "LM.LocalTp.Fire";

        // [Multi Route Advance Ready Gate] — useLocalTeleport=0 인 트리거(다른 씬으로 LoadScene)용.
        // Local Teleport 와 동일한 합의 패턴이지만 Fire 메시지 불필요:
        // 합의 시 서버가 TryAdvanceRouteNode → NGO SceneManager.LoadScene 으로 모든 클라 씬을 일괄 sync 로드.
        private const string RouteAdvReadyMsg = "LM.RouteAdv.Ready";
        private const string RouteAdvCancelMsg = "LM.RouteAdv.Cancel";

        private readonly Dictionary<string, HashSet<ulong>> _localTeleportReady =
            new Dictionary<string, HashSet<ulong>>(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<ulong>> _routeAdvanceReady =
            new Dictionary<string, HashSet<ulong>>(StringComparer.Ordinal);

        private bool _serverHandlersRegistered;
        private bool _clientHandlersRegistered;

        /// <summary>
        /// 트리거가 호출하는 진입점. F 누름을 서버에 통보한다.
        /// 싱글 → 즉시 fire. 멀티 호스트 → 직접 처리. 멀티 게스트 → CustomMessage 로 서버에 전송.
        /// </summary>
        /// <returns>요청이 수락됐는지 (load 진행 중이면 false). 발동 자체는 서버 합의 후.</returns>
        public bool RequestLocalTeleportReady(string triggerId)
        {
            if (string.IsNullOrEmpty(triggerId))
            {
                return false;
            }

            if (loadInProgress)
            {
                Log("Local teleport ready ignored — route load in progress.");
                return false;
            }

            // 싱글: 즉시 fire. 실제 이동은 트리거 측 ExecuteLocalTeleportForLocalCharacter 가 처리.
            if (!HostAuthority.IsNetworkSessionActive)
            {
                FireLocalTeleportLocal(triggerId);
                return true;
            }

            // 멀티 호스트: 서버 로직 직접 호출.
            if (HostAuthority.IsHost)
            {
                HandleLocalTeleportReadyOnServer(triggerId, NetworkManager.Singleton != null
                    ? NetworkManager.Singleton.LocalClientId
                    : 0UL);
                return true;
            }

            // 멀티 게스트: CustomMessage 로 서버에 전송. NetworkObject spawn 여부 무관.
            return SendReadyMessageToServer(triggerId);
        }

        /// <summary>
        /// 트리거 영역 이탈/잠금 등으로 ready 취소. 서버 readySet 에서 제거.
        /// 싱글에선 noop.
        /// </summary>
        public void CancelLocalTeleportReady(string triggerId)
        {
            if (string.IsNullOrEmpty(triggerId) || !HostAuthority.IsNetworkSessionActive)
            {
                return;
            }

            if (HostAuthority.IsHost)
            {
                RemoveLocalTeleportReadyOnServer(triggerId, NetworkManager.Singleton != null
                    ? NetworkManager.Singleton.LocalClientId
                    : 0UL);
                return;
            }

            SendCancelMessageToServer(triggerId);
        }

        private bool SendReadyMessageToServer(string triggerId)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsConnectedClient || nm.CustomMessagingManager == null)
            {
                Debug.LogWarning(
                    $"[StageRouteManager] Cannot send local teleport ready — NetworkManager not ready. triggerId='{triggerId}'.",
                    this);
                return false;
            }

            int size = FastBufferWriter.GetWriteSize(triggerId);
            using var writer = new FastBufferWriter(size, Allocator.Temp);
            writer.WriteValueSafe(triggerId);
            nm.CustomMessagingManager.SendNamedMessage(LocalTpReadyMsg, NetworkManager.ServerClientId, writer);
            return true;
        }

        private void SendCancelMessageToServer(string triggerId)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsConnectedClient || nm.CustomMessagingManager == null)
            {
                return;
            }

            int size = FastBufferWriter.GetWriteSize(triggerId);
            using var writer = new FastBufferWriter(size, Allocator.Temp);
            writer.WriteValueSafe(triggerId);
            nm.CustomMessagingManager.SendNamedMessage(LocalTpCancelMsg, NetworkManager.ServerClientId, writer);
        }

        private void BroadcastFireToAllClients(string triggerId)
        {
            NetworkManager nm = NetworkManager.Singleton;

            // 안전망: NetworkManager 가 죽었거나 CustomMessagingManager 없으면 적어도 로컬 fire.
            if (nm == null || nm.CustomMessagingManager == null)
            {
                FireLocalTeleportLocal(triggerId);
                return;
            }

            // 호스트 자신은 로컬 호출로 즉시 처리 (loopback 동작 보장이 NGO 버전마다 다를 수 있어 명시적으로 분기).
            FireLocalTeleportLocal(triggerId);

            // 나머지 클라이언트에게만 CustomMessage 전송.
            List<ulong> targets = null;
            foreach (ulong id in nm.ConnectedClientsIds)
            {
                if (id == nm.LocalClientId) continue;
                if (targets == null) targets = new List<ulong>();
                targets.Add(id);
            }

            if (targets == null || targets.Count == 0)
            {
                return;
            }

            int size = FastBufferWriter.GetWriteSize(triggerId);
            using var writer = new FastBufferWriter(size, Allocator.Temp);
            writer.WriteValueSafe(triggerId);
            nm.CustomMessagingManager.SendNamedMessage(LocalTpFireMsg, targets, writer);
        }

        // ----- CustomMessage 핸들러 등록/해제 -----

        private void HandleServerStartedForLocalTp()
        {
            RegisterServerCustomMessageHandlers();
        }

        private void HandleClientConnectedForLocalTp(ulong clientId)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null) return;
            // 본인 connect 시에만 client 측 핸들러 등록.
            if (clientId == nm.LocalClientId)
            {
                RegisterClientCustomMessageHandlers();
            }
        }

        private void HandleClientDisconnectForLocalTp(ulong clientId)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null) return;

            // 본인 disconnect — 핸들러 정리.
            if (clientId == nm.LocalClientId)
            {
                UnregisterCustomMessageHandlers();
                return;
            }

            // 다른 클라이언트 disconnect — 서버 측 readySet 정리 + 재평가.
            if (nm.IsServer)
            {
                HandleClientDisconnectForLocalTeleport(clientId);
                HandleClientDisconnectForRouteAdvance(clientId);
            }
        }

        private void HandleClientDisconnectForRouteAdvance(ulong clientId)
        {
            if (_routeAdvanceReady.Count == 0) return;

            List<string> emptyKeys = null;
            foreach (KeyValuePair<string, HashSet<ulong>> entry in _routeAdvanceReady)
            {
                if (entry.Value == null) continue;
                entry.Value.Remove(clientId);
                if (entry.Value.Count == 0)
                {
                    if (emptyKeys == null) emptyKeys = new List<string>();
                    emptyKeys.Add(entry.Key);
                }
            }
            if (emptyKeys != null)
            {
                for (int i = 0; i < emptyKeys.Count; i++) _routeAdvanceReady.Remove(emptyKeys[i]);
            }

            if (_routeAdvanceReady.Count == 0) return;

            // 남은 set 재평가 — 인원 감소로 합의 충족될 수 있음.
            List<string> keys = new List<string>(_routeAdvanceReady.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                string triggerId = keys[i];
                if (_routeAdvanceReady.TryGetValue(triggerId, out HashSet<ulong> set))
                {
                    EvaluateAndFireRouteAdvanceReady(triggerId, set, clientId);
                }
            }
        }

        private void RegisterServerCustomMessageHandlers()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || nm.CustomMessagingManager == null) return;
            if (_serverHandlersRegistered) return;

            nm.CustomMessagingManager.RegisterNamedMessageHandler(LocalTpReadyMsg, OnReadyMessageReceived);
            nm.CustomMessagingManager.RegisterNamedMessageHandler(LocalTpCancelMsg, OnCancelMessageReceived);
            nm.CustomMessagingManager.RegisterNamedMessageHandler(RouteAdvReadyMsg, OnRouteAdvReadyMessageReceived);
            nm.CustomMessagingManager.RegisterNamedMessageHandler(RouteAdvCancelMsg, OnRouteAdvCancelMessageReceived);
            _serverHandlersRegistered = true;
        }

        private void RegisterClientCustomMessageHandlers()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || nm.CustomMessagingManager == null) return;
            if (_clientHandlersRegistered) return;

            nm.CustomMessagingManager.RegisterNamedMessageHandler(LocalTpFireMsg, OnFireMessageReceived);
            _clientHandlersRegistered = true;
        }

        private void UnregisterCustomMessageHandlers()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm != null && nm.CustomMessagingManager != null)
            {
                if (_serverHandlersRegistered)
                {
                    nm.CustomMessagingManager.UnregisterNamedMessageHandler(LocalTpReadyMsg);
                    nm.CustomMessagingManager.UnregisterNamedMessageHandler(LocalTpCancelMsg);
                    nm.CustomMessagingManager.UnregisterNamedMessageHandler(RouteAdvReadyMsg);
                    nm.CustomMessagingManager.UnregisterNamedMessageHandler(RouteAdvCancelMsg);
                }
                if (_clientHandlersRegistered)
                {
                    nm.CustomMessagingManager.UnregisterNamedMessageHandler(LocalTpFireMsg);
                }
            }
            _serverHandlersRegistered = false;
            _clientHandlersRegistered = false;
        }

        private void OnReadyMessageReceived(ulong senderClientId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out string triggerId);
            HandleLocalTeleportReadyOnServer(triggerId, senderClientId);
        }

        private void OnCancelMessageReceived(ulong senderClientId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out string triggerId);
            RemoveLocalTeleportReadyOnServer(triggerId, senderClientId);
        }

        private void OnFireMessageReceived(ulong senderClientId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out string triggerId);
            FireLocalTeleportLocal(triggerId);
        }

        private void OnRouteAdvReadyMessageReceived(ulong senderClientId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out string triggerId);
            HandleRouteAdvanceReadyOnServer(triggerId, senderClientId);
        }

        private void OnRouteAdvCancelMessageReceived(ulong senderClientId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out string triggerId);
            RemoveRouteAdvanceReadyOnServer(triggerId, senderClientId);
        }

        private bool SendRouteAdvReadyMessageToServer(string triggerId)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsConnectedClient || nm.CustomMessagingManager == null)
            {
                Debug.LogWarning(
                    $"[StageRouteManager] Cannot send route advance ready — NetworkManager not ready. triggerId='{triggerId}'.",
                    this);
                return false;
            }

            int size = FastBufferWriter.GetWriteSize(triggerId);
            using var writer = new FastBufferWriter(size, Allocator.Temp);
            writer.WriteValueSafe(triggerId);
            nm.CustomMessagingManager.SendNamedMessage(RouteAdvReadyMsg, NetworkManager.ServerClientId, writer);
            return true;
        }

        private void SendRouteAdvCancelMessageToServer(string triggerId)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsConnectedClient || nm.CustomMessagingManager == null) return;

            int size = FastBufferWriter.GetWriteSize(triggerId);
            using var writer = new FastBufferWriter(size, Allocator.Temp);
            writer.WriteValueSafe(triggerId);
            nm.CustomMessagingManager.SendNamedMessage(RouteAdvCancelMsg, NetworkManager.ServerClientId, writer);
        }

        private void HandleRouteAdvanceReadyOnServer(string triggerId, ulong clientId)
        {
            if (loadInProgress)
            {
                Log($"Route advance ready dropped — load in progress. triggerId='{triggerId}', clientId={clientId}.");
                return;
            }

            if (!_routeAdvanceReady.TryGetValue(triggerId, out HashSet<ulong> set))
            {
                set = new HashSet<ulong>();
                _routeAdvanceReady[triggerId] = set;
            }

            set.Add(clientId);
            Log($"Route advance ready: triggerId='{triggerId}', clientId={clientId}, count={set.Count}.");

            EvaluateAndFireRouteAdvanceReady(triggerId, set);
        }

        private void RemoveRouteAdvanceReadyOnServer(string triggerId, ulong clientId)
        {
            if (!_routeAdvanceReady.TryGetValue(triggerId, out HashSet<ulong> set)) return;

            if (set.Remove(clientId))
            {
                Log($"Route advance ready cancelled: triggerId='{triggerId}', clientId={clientId}, count={set.Count}.");
            }

            if (set.Count == 0)
            {
                _routeAdvanceReady.Remove(triggerId);
            }
        }

        private void EvaluateAndFireRouteAdvanceReady(string triggerId, HashSet<ulong> set, ulong? excludeClientId = null)
        {
            int required = ResolveLocalTeleportRequiredCount(excludeClientId);
            if (required <= 0) return;
            if (set.Count < required) return;

            // 합의 충족 — 서버가 scene load 시작. NGO SceneManager.LoadScene 이 모든 클라 sync.
            _routeAdvanceReady.Remove(triggerId);
            Log($"Route advance CONSENSUS: triggerId='{triggerId}', required={required}.");
            TryAdvanceRouteNode(triggerId, null, null);
        }

        private void HandleLocalTeleportReadyOnServer(string triggerId, ulong clientId)
        {
            if (loadInProgress)
            {
                Log($"Local teleport ready dropped — load in progress. triggerId='{triggerId}', clientId={clientId}.");
                return;
            }

            if (!_localTeleportReady.TryGetValue(triggerId, out HashSet<ulong> set))
            {
                set = new HashSet<ulong>();
                _localTeleportReady[triggerId] = set;
            }

            set.Add(clientId);
            Log($"Local teleport ready: triggerId='{triggerId}', clientId={clientId}, count={set.Count}.");

            EvaluateAndFireLocalTeleportReady(triggerId, set);
        }

        private void RemoveLocalTeleportReadyOnServer(string triggerId, ulong clientId)
        {
            if (!_localTeleportReady.TryGetValue(triggerId, out HashSet<ulong> set))
            {
                return;
            }

            if (set.Remove(clientId))
            {
                Log($"Local teleport ready cancelled: triggerId='{triggerId}', clientId={clientId}, count={set.Count}.");
            }

            if (set.Count == 0)
            {
                _localTeleportReady.Remove(triggerId);
            }
        }

        private void EvaluateAndFireLocalTeleportReady(string triggerId, HashSet<ulong> set, ulong? excludeClientId = null)
        {
            int required = ResolveLocalTeleportRequiredCount(excludeClientId);
            if (required <= 0)
            {
                return;
            }

            if (set.Count < required)
            {
                return;
            }

            // 합의 충족 — ready 셋 비우고 broadcast (CustomMessage, NetworkObject spawn 무관).
            _localTeleportReady.Remove(triggerId);
            Log($"Local teleport CONSENSUS fire: triggerId='{triggerId}', required={required}.");
            BroadcastFireToAllClients(triggerId);
        }

        // excludeClientId: disconnect 핸들러에서 ConnectedClientsIds 가 아직 그 ID 를 포함하는 경우 제외하기 위함.
        // 일반 경로는 null 로 호출.
        // Defeated 플레이어는 required 에서 제외 — 영구 사망한 사람 때문에 게이트가 영구 stuck 되는 것 방지.
        // Down (부활 가능) 은 alive 로 취급 (required 유지) — 부활 대기 의도.
        private static int ResolveLocalTeleportRequiredCount(ulong? excludeClientId = null)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening)
            {
                return 1;
            }
            // ConnectedClientsIds 는 서버에서만 신뢰 가능. 본 호출 경로는 모두 서버 처리 후라 OK.
            int count = 0;
            foreach (ulong id in nm.ConnectedClientsIds)
            {
                if (excludeClientId.HasValue && id == excludeClientId.Value)
                {
                    continue;
                }
                if (IsClientDefeated(nm, id))
                {
                    continue;
                }
                count++;
            }
            return Mathf.Max(1, count);
        }

        // 서버 측 helper. PlayerObject 없거나 KhiDownController 없으면 false (alive 취급) 폴백.
        // PlayerHealthSync._syncedDownState 가 모든 클라 sync 라 서버 mirror 의 KhiDownController.IsDefeated 도 정확.
        private static bool IsClientDefeated(NetworkManager nm, ulong clientId)
        {
            if (!nm.ConnectedClients.TryGetValue(clientId, out NetworkClient nc)) return false;
            NetworkObject po = nc.PlayerObject;
            if (po == null) return false;
            KhiDownController dc = po.GetComponent<KhiDownController>()
                ?? po.GetComponentInChildren<KhiDownController>(includeInactive: true);
            return dc != null && dc.IsDefeated;
        }

        private void FireLocalTeleportLocal(string triggerId)
        {
            if (!RouteNodeExitTrigger.TryGetRegistered(triggerId, out RouteNodeExitTrigger trigger) ||
                trigger == null)
            {
                Debug.LogWarning(
                    $"[StageRouteManager] Local teleport fire received but no trigger registered for id='{triggerId}'.",
                    this);
                return;
            }

            trigger.ExecuteLocalTeleportForLocalCharacter();
        }

        private void HandleClientDisconnectForLocalTeleport(ulong clientId)
        {
            if (_localTeleportReady.Count == 0)
            {
                return;
            }

            // 1. 모든 readySet 에서 disconnecting clientId 제거 + 빈 set 정리.
            List<string> emptyKeys = null;
            foreach (KeyValuePair<string, HashSet<ulong>> entry in _localTeleportReady)
            {
                if (entry.Value == null) continue;
                entry.Value.Remove(clientId);
                if (entry.Value.Count == 0)
                {
                    if (emptyKeys == null) emptyKeys = new List<string>();
                    emptyKeys.Add(entry.Key);
                }
            }
            if (emptyKeys != null)
            {
                for (int i = 0; i < emptyKeys.Count; i++)
                {
                    _localTeleportReady.Remove(emptyKeys[i]);
                }
            }

            if (_localTeleportReady.Count == 0)
            {
                return;
            }

            // 2. 남은 모든 set 재평가 — 인원 수 감소로 합의 충족될 수 있음.
            //    NGO 콜백 시점에 ConnectedClientsIds 가 disconnecting clientId 를 아직 포함할 수 있어
            //    excludeClientId 로 명시 전달.
            List<string> keys = new List<string>(_localTeleportReady.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                string triggerId = keys[i];
                if (_localTeleportReady.TryGetValue(triggerId, out HashSet<ulong> set))
                {
                    EvaluateAndFireLocalTeleportReady(triggerId, set, clientId);
                }
            }
        }

        // ===========================================================================
        // [/Multi Local Teleport Ready Gate]
        // ===========================================================================

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
