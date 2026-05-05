using System.Linq;
using UnityEditor;

namespace LostMemory.Editor.BalanceEditor
{
    public class BalanceEditorAssetWatcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (!BalanceEditorWindow.IsOpen) return;

            bool relevant = importedAssets
                .Concat(deletedAssets)
                .Concat(movedAssets)
                .Concat(movedFromAssetPaths)
                .Any(IsTrackedAsset);

            if (relevant) BalanceEditorWindow.RefreshTree();
        }

        private static bool IsTrackedAsset(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (!path.EndsWith(".asset")) return false;
            return path.Contains("/Relics/Generated/")
                || path.Contains("/BuildSets/");
        }
    }
}
