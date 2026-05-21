using UnityEngine.SceneManagement;

namespace LostMemory.Stage
{
    public static class StageRouteDefaults
    {
        public const string DefaultEntrySpawnId = "default";

        public static StageRouteManager.RouteNode[] CreateRouteNodes()
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

        public static bool TryResolveRouteIndex(string sceneName, out int routeNodeIndex)
        {
            StageRouteManager.RouteNode[] routeNodes = CreateRouteNodes();
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
    }
}
