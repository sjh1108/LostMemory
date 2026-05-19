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

            if (!scene.IsValid() || !StageRouteDefaults.TryResolveRouteIndex(scene.name, out int routeNodeIndex))
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
            routeManager.ConfigureRouteNodes(
                StageRouteDefaults.CreateRouteNodes(),
                routeNodeIndex,
                StageRouteDefaults.DefaultEntrySpawnId,
                false);

            if (scene.name != "Dungeon_1F_Boss")
            {
                UnlockRouteExitTriggers(routeManager);
            }

            Debug.Log($"[StandaloneDungeonSceneBootstrap] Bootstrapped route context for '{scene.name}' at node index {routeNodeIndex}.");
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
