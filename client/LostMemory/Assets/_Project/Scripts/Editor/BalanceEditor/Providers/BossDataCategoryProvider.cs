using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LostMemory.Editor.BalanceEditor.Providers
{
    public class BossDataCategoryProvider : IBalanceCategoryProvider
    {
        private static readonly string[] SearchFolders =
        {
            "Assets/_Project/ScriptableObjects/Enemies"
        };

        public string CategoryName => "Bosses";
        public string AssetTypeFilter => "BossData";

        public IEnumerable<ScriptableObject> LoadAll()
        {
            var guids = AssetDatabase.FindAssets($"t:{AssetTypeFilter}", SearchFolders);
            return guids
                .Select(g => AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    AssetDatabase.GUIDToAssetPath(g)))
                .Where(s => s != null);
        }
    }
}
