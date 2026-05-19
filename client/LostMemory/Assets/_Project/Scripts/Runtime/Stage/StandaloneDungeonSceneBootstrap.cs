using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LostMemory.Stage
{
    /// <summary>
    /// Editor/development convenience: when a route scene is played directly, recreate the route context
    /// so exits still use StageRouteManager instead of one-off scene-loading shortcuts.
    /// </summary>
    public static class StandaloneDungeonSceneBootstrap
    {
        private const string DefaultEntrySpawnId = "default";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapActiveScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            TryBootstrap(activeScene);
        }

        private static void TryBootstrap(Scene scene)
        {
            if (!Application.isEditor && !Debug.isDebugBuild)
            {
                return;
            }

            if (!scene.IsValid() || !TryResolveRouteIndex(scene.name, out int routeNodeIndex))
            {
                return;
            }

            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager != null && networkManager.IsListening && !networkManager.IsServer)
            {
                return;
            }

            StageRouteManager routeManager = StageRouteManager.Instance;
            if (routeManager == null)
            {
                GameObject routeManagerObject = new GameObject("StageRouteManager (Standalone Test)");
                routeManagerObject.AddComponent<NetworkObject>();
                routeManager = routeManagerObject.AddComponent<StageRouteManager>();
            }
            routeManager.ConfigureRouteNodes(CreateDefaultRouteNodes(), routeNodeIndex, DefaultEntrySpawnId, false);

            if (scene.name != "Dungeon_1F_Boss")
            {
                UnlockRouteExitTriggers(routeManager);
            }

            Debug.Log($"[StandaloneDungeonSceneBootstrap] Bootstrapped route context for '{scene.name}' at node index {routeNodeIndex}.");
        }

        private static StageRouteManager.RouteNode[] CreateDefaultRouteNodes()
        {
            return new[]
            {
                CreateNode("stage1_1r", "Dungeon_1F_1R", "Dungeon_1F_1R", "to_1f_2r"),
                CreateNode("stage1_2r", "Dungeon_1F_2R", "Dungeon_1F_2R", "to_1f_shop"),
                CreateNode("stage1_shop", "Dungeon_1F_Shop", "Dungeon_1F_Shop", "to_1f_3r"),
                CreateNode("stage1_3r", "Dungeon_1F_3R", "Dungeon_1F_3R", "to_1f_4r"),
                CreateNode("stage1_4r", "Dungeon_1F_4R", "Dungeon_1F_4R", "to_1f_boss"),
                CreateNode("stage1_boss", "Dungeon_1F_Boss", "Dungeon_1F_Boss", "to_2f_1r"),
                CreateNode("stage2_1r", "Dungeon_2F_1R", "Dungeon_2F_1R", "to_2f_2r"),
                CreateNode("stage2_2r", "Dungeon_2F_2R", "Dungeon_2F_2R", "to_2f_3r"),
                CreateNode("stage2_3r", "Dungeon_2F_3R", "Dungeon_2F_3R", "to_2f_4r"),
                CreateNode("stage2_4r", "Dungeon_2F_4R", "Dungeon_2F_4R", "to_2f_boss"),
                CreateNode("stage2_boss", "Dungeon_2F_Boss", "Dungeon_2F_Boss", string.Empty),
            };
        }

        private static StageRouteManager.RouteNode CreateNode(
            string nodeId,
            string sceneName,
            string sceneFileName,
            string exitTriggerId)
        {
            return new StageRouteManager.RouteNode(
                nodeId,
                sceneName,
                $"Assets/_Project/Scenes/Dungeon/{sceneFileName}.unity",
                DefaultEntrySpawnId,
                exitTriggerId,
                LoadSceneMode.Single);
        }

        private static bool TryResolveRouteIndex(string sceneName, out int routeNodeIndex)
        {
            StageRouteManager.RouteNode[] routeNodes = CreateDefaultRouteNodes();
            for (int i = 0; i < routeNodes.Length; i++)
            {
                if (routeNodes[i].SceneName == sceneName)
                {
                    routeNodeIndex = i;
                    return true;
                }
            }

            routeNodeIndex = 0;
            return false;
        }

        private static void UnlockRouteExitTriggers(StageRouteManager routeManager)
        {
            RouteNodeExitTrigger[] triggers = UnityEngine.Object.FindObjectsByType<RouteNodeExitTrigger>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < triggers.Length; i++)
            {
                if (triggers[i] != null)
                {
                    if (routeManager == null ||
                        triggers[i].UsesLocalTeleport ||
                        routeManager.CanAdvanceRouteNode(triggers[i].TriggerId))
                    {
                        triggers[i].Unlock();
                    }
                }
            }
        }
    }
}
