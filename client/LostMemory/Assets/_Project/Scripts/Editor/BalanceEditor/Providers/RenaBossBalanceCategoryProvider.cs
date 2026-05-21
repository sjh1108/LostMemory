using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LostMemory.Editor.BalanceEditor.Providers
{
    public sealed class RenaBossBalanceCategoryProvider : IBalanceCategoryProvider
    {
        private static readonly string[] SearchFolders =
        {
            "Assets/_Project/ScriptableObjects/Enemies"
        };

        public string CategoryName => "Rena Boss";
        public string AssetTypeFilter => "RenaBossBalanceData";

        public IEnumerable<ScriptableObject> LoadAll()
        {
            string[] guids = AssetDatabase.FindAssets($"t:{AssetTypeFilter}", SearchFolders);
            return guids
                .Select(guid => AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    AssetDatabase.GUIDToAssetPath(guid)))
                .Where(asset => asset != null);
        }
    }
}
