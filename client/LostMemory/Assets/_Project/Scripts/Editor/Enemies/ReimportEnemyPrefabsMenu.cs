using UnityEditor;
using UnityEngine;

namespace LostMemory.EditorTools.Enemies
{
    /// <summary>
    /// `Assets/_Project/Prefabs/Enemies` 의 모든 .prefab 강제 reimport.
    ///
    /// 문제 배경:
    ///   AddMonsterSyncMenu 가 NetworkObject 를 AddComponent 만 하고 ForceUpdate import 를 안 하면
    ///   prefab 의 NetworkObject GlobalObjectIdHash 가 0 으로 남음.
    ///   런타임에 NGO 가 "NetworkPrefab (Orc_CL037) has a duplicate GlobalObjectIdHash source entry value of: 0!" 에러 후
    ///   "Removing invalid prefabs from Network Prefab registration" 으로 enemy prefab 들 전부 제거 → spawn / sync 불가.
    ///
    /// 사용:
    ///   메뉴: Lost Memory > Sync > Reimport Enemy Prefabs (Fix GlobalObjectIdHash)
    ///   한 번 실행 후 DefaultNetworkPrefabs.asset 의 hash 가 0 이 아닌지 inspector 로 확인.
    /// </summary>
    public static class ReimportEnemyPrefabsMenu
    {
        private const string MenuPath = "Lost Memory/Sync/Reimport Enemy Prefabs (Fix GlobalObjectIdHash)";
        private const string EnemiesFolder = "Assets/_Project/Prefabs/Enemies";

        [MenuItem(MenuPath)]
        private static void Run()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { EnemiesFolder });
            if (guids == null || guids.Length == 0)
            {
                EditorUtility.DisplayDialog("Reimport Enemy Prefabs",
                    $"{EnemiesFolder} 에 prefab 없음.", "OK");
                return;
            }

            int count = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path)) continue;
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    Debug.Log($"[ReimportEnemyPrefabs] ForceUpdate: {path}");
                    count++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            string msg =
                $"reimport 완료: {count}개\n\n" +
                $"확인:\n" +
                $"1. DefaultNetworkPrefabs.asset 열어서 enemy prefab 각 entry 의 SourceHash 가 0 이 아닌지\n" +
                $"2. 다시 빌드 → Player.log 에 'duplicate GlobalObjectIdHash source entry value of: 0' 에러 사라졌는지";
            Debug.Log("[ReimportEnemyPrefabs] " + msg);
            EditorUtility.DisplayDialog("Reimport Enemy Prefabs", msg, "OK");
        }
    }
}
