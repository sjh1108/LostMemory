using System.Collections.Generic;
using LostMemory.Relics;
using UnityEditor;
using UnityEngine;

namespace LostMemory.Editor.Relics
{
    /// <summary>
    /// CL-150: 등급 → 사이즈 매핑 일괄 적용 Editor 메뉴.
    ///
    /// 메뉴:
    /// - LostMemory/Relics/Apply Rarity-based Sizes (Dry Run) — 변경 예정 콘솔 출력만, SO 변경 X
    /// - LostMemory/Relics/Apply Rarity-based Sizes — 실제 적용 (Lock 건너뜀, AssetDatabase.SaveAssets)
    ///
    /// Dry Run 흐름:
    /// 1. AssetDatabase.FindAssets("t:RelicData") 로 모든 RelicData SO 검색
    /// 2. 각 SO 의 Size 와 RelicSizeMapping.GetDefaultSize(Rarity) 비교
    /// 3. Lock 된 것 / 이미 일치하는 것 / 변경 예정인 것 카운트
    /// 4. 변경 예정 목록을 콘솔에 표시 — 사용자가 의도된 변형 사이즈 SO 에 Lock 추가 가능
    ///
    /// Apply 흐름: Dry Run 과 동일하되 SerializedObject 로 _size 갱신 + EditorUtility.SetDirty + SaveAssets.
    /// </summary>
    public static class RelicSizeApplyMenu
    {
        [MenuItem("LostMemory/Relics/Apply Rarity-based Sizes (Dry Run)")]
        public static void DryRun() => Run(dryRun: true);

        [MenuItem("LostMemory/Relics/Apply Rarity-based Sizes")]
        public static void Apply() => Run(dryRun: false);

        // ── 1×1 강제 리셋 — IsSizeLocked 무시하고 전부 (1,1) ─────────────────────────
        [MenuItem("LostMemory/Relics/Reset All Sizes to 1×1 (Dry Run)")]
        public static void Reset1x1DryRun() => RunReset1x1(dryRun: true);

        [MenuItem("LostMemory/Relics/Reset All Sizes to 1×1")]
        public static void Reset1x1Apply() => RunReset1x1(dryRun: false);

        private static void RunReset1x1(bool dryRun)
        {
            string[] guids = AssetDatabase.FindAssets("t:RelicData");
            int changeCount = 0, sameCount = 0;
            var changeList = new List<string>();
            Vector2Int target = new Vector2Int(1, 1);

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                RelicData so = AssetDatabase.LoadAssetAtPath<RelicData>(path);
                if (so == null) continue;

                Vector2Int current = so.Size;
                if (current == target) { sameCount++; continue; }

                changeList.Add($"  {so.name}: ({current.x},{current.y}) → (1,1)" + (so.IsSizeLocked ? " [Locked 무시]" : ""));
                changeCount++;

                if (!dryRun)
                {
                    var sObj = new SerializedObject(so);
                    sObj.FindProperty("_size").vector2IntValue = target;
                    sObj.ApplyModifiedProperties();
                    EditorUtility.SetDirty(so);
                }
            }

            if (!dryRun)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            string mode = dryRun ? "[DRY RUN]" : "[APPLIED]";
            string changeBlock = changeList.Count > 0
                ? $"\n변경 예정 목록:\n{string.Join("\n", changeList)}"
                : "";
            Debug.Log($"[Reset1x1] {mode} 변경 {changeCount}개 / 이미 (1,1) {sameCount}개{changeBlock}");
        }

        private static void Run(bool dryRun)
        {
            string[] guids = AssetDatabase.FindAssets("t:RelicData");
            int changeCount = 0, sameCount = 0, lockedCount = 0;
            var changeList = new List<string>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                RelicData so = AssetDatabase.LoadAssetAtPath<RelicData>(path);
                if (so == null) continue;
                if (so.IsSizeLocked) { lockedCount++; continue; }

                Vector2Int current = so.Size;
                Vector2Int target  = RelicSizeMapping.GetDefaultSize(so.Rarity);
                if (current == target) { sameCount++; continue; }

                changeList.Add($"  {so.name}: ({current.x},{current.y}) → ({target.x},{target.y}) [{so.Rarity}]");
                changeCount++;

                if (!dryRun)
                {
                    var sObj = new SerializedObject(so);
                    sObj.FindProperty("_size").vector2IntValue = target;
                    sObj.ApplyModifiedProperties();
                    EditorUtility.SetDirty(so);
                }
            }

            if (!dryRun)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            string mode = dryRun ? "[DRY RUN]" : "[APPLIED]";
            string changeBlock = changeList.Count > 0
                ? $"\n변경 예정 목록:\n{string.Join("\n", changeList)}"
                : "";
            Debug.Log($"[CL-150] {mode} 변경 예정 {changeCount}개 / 변경 없음 {sameCount}개 / Lock 건너뜀 {lockedCount}개{changeBlock}");
        }
    }
}
