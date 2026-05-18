using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LostMemory.Editor.BalanceEditor.Providers
{
    public class EnemyDataCategoryProvider : IBalanceCategoryProvider
    {
        private static readonly string[] SearchFolders =
        {
            "Assets/_Project/ScriptableObjects/Enemies"
        };

        public string CategoryName => "Enemies";
        public string AssetTypeFilter => "EnemyData";

        public IEnumerable<ScriptableObject> LoadAll()
        {
            var guids = AssetDatabase.FindAssets($"t:{AssetTypeFilter}", SearchFolders);
            return guids
                .Select(g => AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    AssetDatabase.GUIDToAssetPath(g)))
                .Where(s => s != null)
                // BossData : EnemyData 상속이라 t:EnemyData 에 BossData 도 매칭됨.
                // Bosses 카테고리와 중복 표시 방지를 위해 정확 타입만 통과.
                // FullName 경로 비교로 다른 namespace 의 동명 EnemyData 충돌도 방지.
                .Where(s => s.GetType().FullName?.EndsWith(".EnemyData") == true);
        }
    }
}
