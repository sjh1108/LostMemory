using System.Collections.Generic;
using System.Linq;
using System.Text;
using LostMemory.Relics;
using LostMemory.Rewards;
using UnityEditor;
using UnityEngine;

namespace LostMemory.Editor.Rewards
{
    /// <summary>
    /// CL-152: RewardPool 의 _allRewards 배열에 RelicData SO 일괄 등록 + 분포 audit.
    ///
    /// 메뉴 (CL-150 RelicSizeApplyMenu 패턴 일관):
    /// - LostMemory/Relics/Populate RewardPool (Dry Run) — 추가 예정 SO 목록 콘솔 출력만
    /// - LostMemory/Relics/Populate RewardPool — 실제 일괄 추가 (idempotent: 중복 제외)
    /// - LostMemory/Relics/Audit RewardPool Distribution — 등급별 분포 콘솔 출력
    ///
    /// Apply 흐름:
    /// 1. AssetDatabase.FindAssets("t:RelicData", { RelicsFolder }) 로 모든 RelicData SO 검색
    /// 2. 현재 _allRewards 의 SO names 집합 (existing) 계산
    /// 3. existing 에 없는 SO 만 toAdd 에 수집
    /// 4. SerializedObject 로 _allRewards.arraySize 확장 + 끝부분에 toAdd append
    /// 5. SetDirty + SaveAssets
    ///
    /// 메뉴 다시 돌려도 안전 (existing 가드로 중복 추가 방지).
    /// </summary>
    public static class RewardPoolPopulateMenu
    {
        private const string PoolPath     = "Assets/_Project/ScriptableObjects/Reward/RewardPool.asset";
        private const string RelicsFolder = "Assets/_Project/ScriptableObjects/Relics";

        [MenuItem("LostMemory/Relics/Populate RewardPool (Dry Run)")]
        public static void DryRun() => Run(dryRun: true);

        [MenuItem("LostMemory/Relics/Populate RewardPool")]
        public static void Apply() => Run(dryRun: false);

        [MenuItem("LostMemory/Relics/Audit RewardPool Distribution")]
        public static void Audit()
        {
            var pool = AssetDatabase.LoadAssetAtPath<RewardPool>(PoolPath);
            if (pool == null)
            {
                Debug.LogError($"[CL-152] RewardPool 미발견: {PoolPath}");
                return;
            }

            var sObj = new SerializedObject(pool);
            var arr = sObj.FindProperty("_allRewards");
            var counts = new Dictionary<RelicRarity, int>();
            int consumables = 0;
            int total = arr.arraySize;
            int nullCount = 0;

            for (int i = 0; i < total; i++)
            {
                var r = arr.GetArrayElementAtIndex(i).objectReferenceValue as RelicData;
                if (r == null) { nullCount++; continue; }
                if (r.IsConsumable) { consumables++; continue; }
                counts.TryGetValue(r.Rarity, out int c);
                counts[r.Rarity] = c + 1;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[CL-152] RewardPool 분포 (총 {total} 슬롯):");
            foreach (var kv in counts.OrderByDescending(x => (int)x.Key))
                sb.AppendLine($"  {kv.Key}: {kv.Value}");
            sb.AppendLine($"  Consumable: {consumables}");
            if (nullCount > 0) sb.AppendLine($"  ⚠ Null refs: {nullCount}");
            Debug.Log(sb.ToString());
        }

        private static void Run(bool dryRun)
        {
            var pool = AssetDatabase.LoadAssetAtPath<RewardPool>(PoolPath);
            if (pool == null)
            {
                Debug.LogError($"[CL-152] RewardPool 미발견: {PoolPath}");
                return;
            }

            // 1) 풀 내 RelicData SO 검색
            string[] guids = AssetDatabase.FindAssets("t:RelicData", new[] { RelicsFolder });
            var allRelics = guids
                .Select(g => AssetDatabase.LoadAssetAtPath<RelicData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(r => r != null)
                .ToList();

            // 2) 현재 등록된 SO names (중복 추가 방지)
            var sObj = new SerializedObject(pool);
            var arr = sObj.FindProperty("_allRewards");
            var existing = new HashSet<string>();
            for (int i = 0; i < arr.arraySize; i++)
            {
                var r = arr.GetArrayElementAtIndex(i).objectReferenceValue as RelicData;
                if (r != null) existing.Add(r.name);
            }

            // 3) toAdd 수집
            var toAdd = allRelics.Where(r => !existing.Contains(r.name)).ToList();

            string mode = dryRun ? "[DRY RUN]" : "[APPLIED]";
            string list = toAdd.Count > 0
                ? "\n추가 예정 목록:\n  " + string.Join("\n  ", toAdd.Select(r => $"{r.name} [{r.Rarity}]"))
                : "";
            Debug.Log($"[CL-152] {mode} 기존 {existing.Count}개 / 추가 예정 {toAdd.Count}개 / 풀 검색 {allRelics.Count}개{list}");

            if (dryRun || toAdd.Count == 0) return;

            // 4) Apply: 끝부분에 toAdd append (기존 ref 그대로 유지)
            int oldSize = arr.arraySize;
            arr.arraySize = oldSize + toAdd.Count;
            for (int i = 0; i < toAdd.Count; i++)
                arr.GetArrayElementAtIndex(oldSize + i).objectReferenceValue = toAdd[i];
            sObj.ApplyModifiedProperties();
            EditorUtility.SetDirty(pool);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
