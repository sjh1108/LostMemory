using System.Collections.Generic;
using System.IO;
using System.Linq;
using LostMemory.Relics;
using UnityEditor;
using UnityEngine;

namespace LostMemory.Editor.Relics
{
    /// <summary>
    /// RelicData._icon 일괄 자동 할당 Editor 메뉴 (icon_mapping.csv 기반).
    ///
    /// 메뉴:
    /// - LostMemory/Relics/Assign Icons from Mapping (Dry Run) — 어떤 매핑이 적용될지만 콘솔 출력
    /// - LostMemory/Relics/Assign Icons from Mapping (Apply)   — 실제 _icon 할당 + Save
    ///
    /// CSV 형식 (UTF-8): 헤더 + 행 — `so_name,icon`
    ///   so_name : RelicData_ 뒤에 붙는 부분 (예: "가죽 신발", "추적자의망토")
    ///   icon    : Art/UI/Icon/ 안의 PNG 파일명 (확장자 제외, 예: "가죽신발")
    ///
    /// 정책:
    /// - 이미 _icon 이 할당된 SO 는 skip (수동 수정 보존)
    /// - CSV 에 매핑이 없는 SO 는 NoMapping 으로 분류 (수동 처리 대상)
    /// - 매핑이 있는데 SO 또는 PNG 가 없으면 경고 분류
    /// - 한 PNG 가 여러 SO 에 매핑되는 것 허용 (작가 의도)
    /// </summary>
    public static class RelicIconAutoAssignMenu
    {
        private const string MappingCsvPath = "Assets/_Project/ScriptableObjects/Relics/_source/icon_mapping.csv";
        private const string IconFolder     = "Assets/_Project/Art/UI/Icon";
        private const string SoNamePrefix   = "RelicData_";

        [MenuItem("LostMemory/Relics/Assign Icons from Mapping (Dry Run)")]
        public static void DryRun() => Run(dryRun: true);

        [MenuItem("LostMemory/Relics/Assign Icons from Mapping (Apply)")]
        public static void Apply() => Run(dryRun: false);

        private static void Run(bool dryRun)
        {
            // ── CSV 로드 ─────────────────────────────────────────────────────
            if (!File.Exists(MappingCsvPath))
            {
                Debug.LogError($"[IconAssign] 매핑 CSV 가 없습니다: {MappingCsvPath}");
                return;
            }

            Dictionary<string, string> mapping = LoadMappingCsv(MappingCsvPath, out var csvErrors);
            if (csvErrors.Count > 0)
            {
                Debug.LogWarning($"[IconAssign] CSV 파싱 경고 {csvErrors.Count}건:\n{string.Join("\n", csvErrors)}");
            }
            if (mapping.Count == 0)
            {
                Debug.LogError($"[IconAssign] 매핑이 비어 있습니다.");
                return;
            }

            // ── 아이콘 인덱스 (파일명 → Sprite) ─────────────────────────────
            Dictionary<string, Sprite> iconByName = LoadIconIndex(IconFolder);

            // ── SO 인덱스 ────────────────────────────────────────────────────
            Dictionary<string, RelicData> soByName = LoadRelicDataIndex();

            // ── 적용 ────────────────────────────────────────────────────────
            var assigned         = new List<string>(); // SO → PNG
            int alreadyAssigned  = 0;
            var notFoundSo       = new List<string>(); // CSV 에 있는데 SO 가 없음
            var notFoundIcon     = new List<string>(); // CSV 에 있는데 PNG 가 없음

            var assignedSoNames  = new HashSet<string>();

            foreach (var kv in mapping)
            {
                string soName = SoNamePrefix + kv.Key;
                string iconName = kv.Value;

                if (!soByName.TryGetValue(soName, out RelicData so))
                {
                    notFoundSo.Add($"  CSV[{kv.Key}] → SO '{soName}' 없음");
                    continue;
                }

                assignedSoNames.Add(soName);

                if (so.Icon != null)
                {
                    alreadyAssigned++;
                    continue;
                }

                if (!iconByName.TryGetValue(iconName, out Sprite sprite))
                {
                    notFoundIcon.Add($"  {soName} → PNG '{iconName}' 없음");
                    continue;
                }

                assigned.Add($"  {soName} → {iconName}");

                if (!dryRun)
                {
                    var sObj = new SerializedObject(so);
                    sObj.FindProperty("_icon").objectReferenceValue = sprite;
                    sObj.ApplyModifiedProperties();
                    EditorUtility.SetDirty(so);
                }
            }

            // ── 매핑 없는 SO (NoMapping) — 수동 처리 대상 ────────────────────
            var noMapping = new List<string>();
            foreach (var so in soByName.Values)
            {
                if (assignedSoNames.Contains(so.name)) continue;
                if (so.Icon != null) continue; // 이미 손으로 할당된 건 제외
                noMapping.Add($"  {so.name}");
            }

            // ── 사용 안 된 PNG — 매핑되지 않은 PNG ───────────────────────────
            var usedIconNames = new HashSet<string>(mapping.Values);
            var unusedIcons = iconByName.Keys
                .Where(n => !usedIconNames.Contains(n) && !n.StartsWith("ChatGPT Image"))
                .OrderBy(n => n)
                .ToList();

            if (!dryRun)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            // ── 리포트 ──────────────────────────────────────────────────────
            string mode = dryRun ? "[DRY RUN]" : "[APPLIED]";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(
                $"[IconAssign] {mode} " +
                $"Assigned {assigned.Count}개 / " +
                $"AlreadyAssigned(skip) {alreadyAssigned}개 / " +
                $"NoMapping {noMapping.Count}개 / " +
                $"NotFoundSO {notFoundSo.Count}개 / " +
                $"NotFoundIcon {notFoundIcon.Count}개 / " +
                $"UnusedIcons {unusedIcons.Count}개");

            if (assigned.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"── Assigned ({assigned.Count}) ──");
                sb.AppendLine(string.Join("\n", assigned));
            }
            if (noMapping.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"── NoMapping — 수동 처리 필요 ({noMapping.Count}) ──");
                sb.AppendLine(string.Join("\n", noMapping));
            }
            if (notFoundSo.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"── NotFoundSO — CSV 키 오타? ({notFoundSo.Count}) ──");
                sb.AppendLine(string.Join("\n", notFoundSo));
            }
            if (notFoundIcon.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"── NotFoundIcon — PNG 파일명 오타? ({notFoundIcon.Count}) ──");
                sb.AppendLine(string.Join("\n", notFoundIcon));
            }
            if (unusedIcons.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"── UnusedIcons — 매핑에 안 쓰인 PNG ({unusedIcons.Count}) ──");
                foreach (var n in unusedIcons) sb.AppendLine($"  {n}");
            }

            Debug.Log(sb.ToString());
        }

        // ──────────────────────────────────────────────────────────────────
        // 로더
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// CSV 형식: 헤더 1줄 + 행 (`so_name,icon`). 빈 줄/주석(# 시작) 무시.
        /// 동일 so_name 중복 시 마지막 값 유지 + 경고.
        /// </summary>
        private static Dictionary<string, string> LoadMappingCsv(string path, out List<string> errors)
        {
            var dict = new Dictionary<string, string>();
            errors = new List<string>();
            string[] lines = File.ReadAllLines(path);
            bool headerSeen = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i].Trim();
                if (string.IsNullOrEmpty(raw)) continue;
                if (raw.StartsWith("#")) continue;
                if (!headerSeen) { headerSeen = true; continue; } // 첫 비어있지 않은 줄은 헤더

                string[] cols = raw.Split(',');
                if (cols.Length < 2)
                {
                    errors.Add($"  L{i + 1}: 컬럼 부족 — '{raw}'");
                    continue;
                }
                string key  = cols[0].Trim();
                string icon = cols[1].Trim();
                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(icon))
                {
                    errors.Add($"  L{i + 1}: 빈 값 — '{raw}'");
                    continue;
                }
                if (dict.ContainsKey(key))
                {
                    errors.Add($"  L{i + 1}: 중복 so_name '{key}' (이전 값 덮어씀)");
                }
                dict[key] = icon;
            }
            return dict;
        }

        private static Dictionary<string, Sprite> LoadIconIndex(string folder)
        {
            var dict = new Dictionary<string, Sprite>();
            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrEmpty(fileName)) continue;
                Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sp == null) continue;
                if (dict.ContainsKey(fileName))
                {
                    Debug.LogWarning($"[IconAssign] 동일 파일명 Sprite 충돌: '{fileName}' — 기존 유지");
                    continue;
                }
                dict[fileName] = sp;
            }
            return dict;
        }

        private static Dictionary<string, RelicData> LoadRelicDataIndex()
        {
            var dict = new Dictionary<string, RelicData>();
            string[] guids = AssetDatabase.FindAssets("t:RelicData");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                RelicData so = AssetDatabase.LoadAssetAtPath<RelicData>(path);
                if (so == null) continue;
                if (dict.ContainsKey(so.name))
                {
                    Debug.LogWarning($"[IconAssign] 동일 이름 RelicData 충돌: '{so.name}' — 기존 유지");
                    continue;
                }
                dict[so.name] = so;
            }
            return dict;
        }
    }
}
