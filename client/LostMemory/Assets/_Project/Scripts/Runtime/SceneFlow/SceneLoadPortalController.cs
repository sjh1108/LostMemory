using System.Collections.Generic;
using LostMemory.Stage;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace LostMemory.SceneFlow
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    [AddComponentMenu("Lost Memory/Scene Flow/Scene Load Portal Controller")]
    public sealed class SceneLoadPortalController : MonoBehaviour
    {
        private const string DefaultDungeonStartSceneName = "Dungeon_1F_1R";

        [SerializeField] private string targetSceneName = "BossClear";
        [SerializeField] private string editorScenePath = string.Empty;
        [SerializeField] private LoadSceneMode loadSceneMode = LoadSceneMode.Single;
        [SerializeField] private bool requireInteractInput = true;
        [SerializeField] private KeyCode fallbackInteractKey = KeyCode.E;
        [SerializeField] private string acceptedPlayerId = "Player1";
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private bool debugLogging;

        [Header("Dungeon Run Entry")]
        [SerializeField] private bool requestDungeonRunStart;
        [SerializeField, Min(0)] private int dungeonRouteNodeIndex;
        [SerializeField] private string dungeonEntrySpawnId = "default";
        [SerializeField] private bool startRunAfterLoad = true;

        private readonly HashSet<Character> _candidates = new HashSet<Character>();
        private Collider2D _trigger;
        private bool _loadInProgress;

        public void Configure(string sceneName, bool requireInput, KeyCode interactKey, string playerId)
        {
            targetSceneName = sceneName;
            requireInteractInput = requireInput;
            fallbackInteractKey = interactKey;
            acceptedPlayerId = playerId;
            RefreshReferences();
            ConfigureTrigger();
        }

        public void ConfigureEditorScenePath(string scenePath)
        {
            editorScenePath = scenePath;
        }

        public void ConfigureDungeonRunEntry(bool enabled, int routeNodeIndex, string entrySpawnId, bool startRun)
        {
            requestDungeonRunStart = enabled;
            dungeonRouteNodeIndex = Mathf.Max(0, routeNodeIndex);
            dungeonEntrySpawnId = entrySpawnId;
            startRunAfterLoad = startRun;
        }

        private void Reset()
        {
            RefreshReferences();
            ConfigureTrigger();
        }

        private void OnValidate()
        {
            RefreshReferences();
            ConfigureTrigger();
        }

        private void Awake()
        {
            RefreshReferences();
            ConfigureTrigger();
        }

        private void Update()
        {
            if (!requireInteractInput || _loadInProgress)
            {
                return;
            }

            Character selected = null;
            foreach (Character candidate in _candidates)
            {
                if (!CanUsePortal(candidate))
                {
                    continue;
                }

                selected = candidate;
                break;
            }

            if (selected != null && IsInteractPressedThisFrame(selected))
            {
                LoadTargetScene(selected);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Character character = other != null ? other.GetComponentInParent<Character>() : null;
            if (!CanUsePortal(character))
            {
                return;
            }

            _candidates.Add(character);

            if (!requireInteractInput)
            {
                LoadTargetScene(character);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            Character character = other != null ? other.GetComponentInParent<Character>() : null;
            if (character != null)
            {
                _candidates.Remove(character);
            }
        }

        private void RefreshReferences()
        {
            if (_trigger == null)
            {
                _trigger = GetComponent<Collider2D>();
            }

            if (visualRoot == null && transform.childCount > 0)
            {
                visualRoot = transform.GetChild(0).gameObject;
            }
        }

        private void ConfigureTrigger()
        {
            if (_trigger != null)
            {
                _trigger.isTrigger = true;
            }
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

        private void LoadTargetScene(Character character)
        {
            if (_loadInProgress)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(targetSceneName))
            {
                Debug.LogWarning("[SceneLoadPortalController] Target scene name is empty.", this);
                return;
            }

            _loadInProgress = true;
            Log($"Loading scene '{targetSceneName}' from portal '{name}' used by '{character.name}'.");

            if (ShouldRequestDungeonRunStart())
            {
                DungeonRunSceneEntryRequest.Request(
                    targetSceneName,
                    dungeonRouteNodeIndex,
                    dungeonEntrySpawnId,
                    startRunAfterLoad);
            }

            if (!TryLoadTargetScene())
            {
                DungeonRunSceneEntryRequest.ClearPendingRequest(targetSceneName);
                _loadInProgress = false;
            }
        }

        private bool TryLoadTargetScene()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            bool networkSessionActive = networkManager != null && networkManager.IsListening;
            if (networkSessionActive)
            {
                if (!networkManager.IsServer)
                {
                    Debug.LogWarning("[SceneLoadPortalController] Network scene load must be requested on the server/host.", this);
                    return false;
                }

                if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
                {
                    Debug.LogWarning($"[SceneLoadPortalController] Scene '{targetSceneName}' is not in Build Settings.", this);
                    return false;
                }

                networkManager.SceneManager.LoadScene(targetSceneName, loadSceneMode);
                return true;
            }

            if (Application.CanStreamedLevelBeLoaded(targetSceneName))
            {
                SceneManager.LoadScene(targetSceneName, loadSceneMode);
                return true;
            }

#if UNITY_EDITOR
            if (!string.IsNullOrWhiteSpace(editorScenePath))
            {
                EditorSceneManager.LoadSceneInPlayMode(editorScenePath, new LoadSceneParameters(loadSceneMode));
                return true;
            }
#endif

            Debug.LogWarning($"[SceneLoadPortalController] Scene '{targetSceneName}' is not available. Add it to Build Settings or set an editor scene path.", this);
            return false;
        }

        private bool ShouldRequestDungeonRunStart()
        {
            return requestDungeonRunStart || targetSceneName == DefaultDungeonStartSceneName;
        }

        private void OnDrawGizmosSelected()
        {
            RefreshReferences();
            if (_trigger == null)
            {
                return;
            }

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;

            if (_trigger is BoxCollider2D box)
            {
                Gizmos.DrawCube(box.offset, box.size);
            }
            else if (_trigger is CircleCollider2D circle)
            {
                Gizmos.DrawSphere(circle.offset, circle.radius);
            }
        }

        private void Log(string message)
        {
            if (debugLogging)
            {
                Debug.Log("[SceneLoadPortalController] " + message, this);
            }
        }
    }

    internal static class DungeonRunSceneEntryRequest
    {
        private static string pendingSceneName;
        private static string pendingSpawnId;
        private static int pendingRouteNodeIndex;
        private static bool pendingStartRun;
        private static bool registered;

        public static void Request(string sceneName, int routeNodeIndex, string spawnId, bool startRun)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

            EnsureRegistered();
            pendingSceneName = sceneName;
            pendingRouteNodeIndex = Mathf.Max(0, routeNodeIndex);
            pendingSpawnId = spawnId;
            pendingStartRun = startRun;
        }

        public static void ClearPendingRequest(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName) || pendingSceneName == sceneName)
            {
                ClearPendingRequest();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            ClearPendingRequest();
            registered = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            EnsureRegistered();
        }

        private static void EnsureRegistered()
        {
            if (registered)
            {
                return;
            }

            SceneManager.sceneLoaded += HandleSceneLoaded;
            registered = true;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (string.IsNullOrEmpty(pendingSceneName) || !scene.IsValid() || scene.name != pendingSceneName)
            {
                return;
            }

            ApplyPendingEntry();
            ClearPendingRequest();
        }

        private static void ApplyPendingEntry()
        {
            StageRouteManager routeManager = Object.FindObjectOfType<StageRouteManager>();
            if (routeManager != null)
            {
                routeManager.InitializeRouteNode(pendingRouteNodeIndex, pendingSpawnId, true);
            }
            else
            {
                Debug.LogWarning("[DungeonRunSceneEntryRequest] No StageRouteManager found in loaded dungeon scene.");
            }

            if (!pendingStartRun)
            {
                return;
            }

            RunManager runManager = Object.FindObjectOfType<RunManager>();
            if (runManager == null)
            {
                Debug.LogWarning("[DungeonRunSceneEntryRequest] No RunManager found in loaded dungeon scene.");
                return;
            }

            runManager.StartRun();
        }

        private static void ClearPendingRequest()
        {
            pendingSceneName = null;
            pendingSpawnId = null;
            pendingRouteNodeIndex = 0;
            pendingStartRun = false;
        }
    }
}
