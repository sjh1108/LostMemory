using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LostMemory.Editor.BalanceEditor.Providers
{
    public class PlayerStatsCategoryProvider : IBalanceCategoryProvider
    {
        private static readonly string[] SearchFolders =
        {
            "Assets/_Project/ScriptableObjects/Player"
        };

        public string CategoryName => "PlayerStats";
        public string AssetTypeFilter => "PlayerStatsData";

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
