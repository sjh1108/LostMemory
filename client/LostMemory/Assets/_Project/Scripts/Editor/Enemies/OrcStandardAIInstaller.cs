#if UNITY_EDITOR
using LostMemory.Enemies.AI;
using UnityEditor;
using UnityEngine;

namespace LostMemory.Editor.Enemies
{
    public static class EnemyStandardAIInstaller
    {
        private const string DefaultOrcPrefabPath = "Assets/_Project/Prefabs/Enemies/Orc_CL037.prefab";

        private static readonly string[] KnownEnemyPrefabPaths =
        {
            "Assets/_Project/Prefabs/Enemies/Orc_CL037.prefab",
            "Assets/_Project/Prefabs/Enemies/Chobomb_CL212.prefab",
            "Assets/_Project/Prefabs/Enemies/Moose1_Test.prefab",
            "Assets/_Project/Prefabs/Enemies/OrcRider_CL039.prefab",
            "Assets/_Project/Prefabs/Enemies/SkeletonArcher_CL041.prefab",
            "Assets/_Project/Prefabs/Enemies/StoneGolem_Test.prefab"
        };

        [MenuItem("Lost Memory/Enemies/Install Orc Standard AI")]
        public static void InstallDefaultOrc()
        {
            InstallPrefab(DefaultOrcPrefabPath);
        }

        [MenuItem("Lost Memory/Enemies/Install Standard AI On Selected Enemy Prefab")]
        public static void InstallSelectedPrefab()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab"))
            {
                Debug.LogWarning("[EnemyStandardAIInstaller] Select an enemy prefab asset first.");
                return;
            }

            InstallPrefab(path);
        }

        [MenuItem("Lost Memory/Enemies/Install Standard AI On Known Enemy Prefabs")]
        public static void InstallKnownEnemyPrefabs()
        {
            for (int i = 0; i < KnownEnemyPrefabPaths.Length; i++)
            {
                InstallPrefab(KnownEnemyPrefabPaths[i]);
            }
        }

        private static void InstallPrefab(string prefabPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                EnemyCombatApproachProfile profile = ResolveProfile(root, prefabPath);
                if (!EnemyCombatApproachInstaller.TryInstallCombatApproach(root, profile))
                {
                    Debug.LogWarning($"[EnemyStandardAIInstaller] Could not install standard AI on '{prefabPath}'. Check the AIBrain state names.");
                    return;
                }

                MarkPrefabContentsDirty(root);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[EnemyStandardAIInstaller] Installed standard AI on '{prefabPath}' using '{profile.Label}' profile.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static EnemyCombatApproachProfile ResolveProfile(GameObject root, string prefabPath)
        {
            if (root != null && EnemyCombatApproachInstaller.TryGetProfileForPrefabName(root.name, out EnemyCombatApproachProfile profile))
            {
                return profile;
            }

            if (EnemyCombatApproachInstaller.TryGetProfileForPrefabName(prefabPath, out profile))
            {
                return profile;
            }

            return EnemyCombatApproachInstaller.CreateDefaultMeleeProfile(root != null ? root.name : prefabPath);
        }

        private static void MarkPrefabContentsDirty(GameObject root)
        {
            EditorUtility.SetDirty(root);

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                {
                    EditorUtility.SetDirty(components[i]);
                }
            }
        }
    }
}
#endif
