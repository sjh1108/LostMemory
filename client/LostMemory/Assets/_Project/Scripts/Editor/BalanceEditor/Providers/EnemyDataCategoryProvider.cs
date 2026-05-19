using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LostMemory.Editor.BalanceEditor.Providers
{
    public class EnemyDataCategoryProvider : IBalanceCategoryProvider
    {
        private const string EnemyDataTypeName = "LostMemory.Enemies.EnemyData";
        private const string BossDataTypeName = "LostMemory.Enemies.BossData";

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
                // Bosses 카테고리와 중복되지 않게 BossData 계열만 제외하고,
                // SkeletonEliteData 같은 일반 적 전용 파생 데이터는 Enemies에 포함한다.
                .Where(IsEnemyBalanceData);
        }

        private static bool IsEnemyBalanceData(ScriptableObject asset)
        {
            Type type = asset.GetType();
            return IsAssignableTo(type, EnemyDataTypeName)
                && !IsAssignableTo(type, BossDataTypeName);
        }

        private static bool IsAssignableTo(Type type, string baseTypeFullName)
        {
            while (type != null)
            {
                if (type.FullName == baseTypeFullName)
                {
                    return true;
                }

                type = type.BaseType;
            }

            return false;
        }
    }
}
