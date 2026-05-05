using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LostMemory.Editor.BalanceEditor
{
    public static class DirtyTracker
    {
        public static bool IsDirty(ScriptableObject so)
            => so != null && EditorUtility.IsDirty(so);

        public static int CountDirty(IEnumerable<ScriptableObject> universe)
            => universe?.Count(IsDirty) ?? 0;

        public static IEnumerable<ScriptableObject> GetAllDirty(
            IEnumerable<ScriptableObject> universe)
            => universe?.Where(IsDirty) ?? System.Linq.Enumerable.Empty<ScriptableObject>();
    }
}
