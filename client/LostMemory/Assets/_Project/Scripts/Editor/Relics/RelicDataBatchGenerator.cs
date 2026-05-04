using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LostMemory.Relics;
using UnityEditor;
using UnityEngine;

namespace LostMemory.Editor.Relics
{
    /// <summary>
    /// CL-141: items.csv → RelicData SO 일괄 생성 Editor 스크립트.
    ///
    /// 메뉴:
    /// - LostMemory/Relics/Validate items.csv (Dry Run) — 파싱·검증만, SO 생성 X
    /// - LostMemory/Relics/Generate RelicData from CSV — 실제 SO 일괄 생성
    ///
    /// 입력: Assets/_Project/ScriptableObjects/Relics/_source/items.csv (RFC 4180 준수)
    /// 출력: Assets/_Project/ScriptableObjects/Relics/Generated/RelicData_*.asset
    ///
    /// 명명 충돌 정책: 기존 SO (Relics/ 직하 18개) 와 동명이면 스킵 + 경고.
    /// </summary>
    public static class RelicDataBatchGenerator
    {
        private const string CsvPath      = "Assets/_Project/ScriptableObjects/Relics/_source/items.csv";
        private const string OutputFolder = "Assets/_Project/ScriptableObjects/Relics/Generated";

        // CSV 헤더 (있는 그대로 — items.csv 작성자가 결정. 변경 시 여기와 동기화 필요)
        private static readonly string[] ExpectedHeaders =
        {
            "ID", "Name", "Theme", "Rarity", "TagPrimary", "TagSecondary",
            "SizeX", "SizeY", "Effect1Type", "Effect1Mag", "Effect2Type", "Effect2Mag", "Description",
        };

        [MenuItem("LostMemory/Relics/Validate items.csv (Dry Run)")]
        public static void ValidateCsv() => Run(dryRun: true);

        [MenuItem("LostMemory/Relics/Generate RelicData from CSV")]
        public static void GenerateFromCsv() => Run(dryRun: false);

        private static void Run(bool dryRun)
        {
            TextAsset csvAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(CsvPath);
            if (csvAsset == null)
            {
                Debug.LogError($"[RelicDataBatchGenerator] CSV not found: {CsvPath}");
                return;
            }

            var errors = new List<string>();
            List<Row> rows = ParseCsv(csvAsset.text, errors);

            if (errors.Count > 0)
            {
                foreach (string e in errors) Debug.LogError($"[RelicDataBatchGenerator] {e}");
                Debug.LogError($"[RelicDataBatchGenerator] {errors.Count} error(s). Aborting (no SOs created).");
                return;
            }

            // 기존 RelicData asset 이름 수집 (충돌 회피)
            HashSet<string> existing = AssetDatabase.FindAssets("t:RelicData")
                .Select(g => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)))
                .ToHashSet();

            EnsureFolder(OutputFolder);

            int created = 0, skipped = 0;
            foreach (Row row in rows)
            {
                string fileName = SanitizeFileName($"RelicData_{row.Name}");
                if (existing.Contains(fileName))
                {
                    Debug.LogWarning($"[RelicDataBatchGenerator] Skip (name conflict with existing SO): {fileName} (id={row.Id})");
                    skipped++;
                    continue;
                }

                if (dryRun)
                {
                    Debug.Log($"[Dry] Would create {fileName} (rarity={row.Rarity}, tags={row.TagPrimary}+{row.TagSecondary}, size={row.SizeX}x{row.SizeY}, effects=[{row.Effect1Type}/{row.Effect1Mag}, {row.Effect2Type}/{row.Effect2Mag}])");
                    created++;
                    continue;
                }

                CreateRelicDataAsset(row, fileName);
                created++;
            }

            if (!dryRun)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            string verb = dryRun ? "Validated" : "Created";
            Debug.Log($"[RelicDataBatchGenerator] {verb} {created}, Skipped {skipped} (name conflict).");
        }

        private static void CreateRelicDataAsset(Row row, string fileName)
        {
            RelicData data = ScriptableObject.CreateInstance<RelicData>();
            var so = new SerializedObject(data);

            so.FindProperty("_displayName").stringValue        = row.Name;
            so.FindProperty("_isConsumable").boolValue         = false;
            so.FindProperty("_isInstantUse").boolValue         = false;
            so.FindProperty("_rarity").enumValueIndex          = (int)row.Rarity;
            so.FindProperty("_tagPrimary").enumValueIndex      = (int)row.TagPrimary;
            so.FindProperty("_tagSecondary").enumValueIndex    = (int)row.TagSecondary;
            so.FindProperty("_size").vector2IntValue           = new Vector2Int(row.SizeX, row.SizeY);
            so.FindProperty("_effectDescription").stringValue  = row.Description;

            // Effects[] 배열: Effect2 가 None/0 이면 1개, 둘 다 None/0 이면 0개
            int effectCount = (row.Effect2Type != RelicEffectType.None || row.Effect2Mag != 0f) ? 2
                            : (row.Effect1Type != RelicEffectType.None || row.Effect1Mag != 0f) ? 1
                            : 0;
            SerializedProperty effectsProp = so.FindProperty("_effects");
            effectsProp.arraySize = effectCount;
            if (effectCount >= 1) WriteEffectEntry(effectsProp.GetArrayElementAtIndex(0), row.Effect1Type, row.Effect1Mag);
            if (effectCount >= 2) WriteEffectEntry(effectsProp.GetArrayElementAtIndex(1), row.Effect2Type, row.Effect2Mag);

            so.ApplyModifiedPropertiesWithoutUndo();

            string path = $"{OutputFolder}/{fileName}.asset";
            AssetDatabase.CreateAsset(data, path);
        }

        private static void WriteEffectEntry(SerializedProperty entry, RelicEffectType type, float magnitude)
        {
            entry.FindPropertyRelative("Type").enumValueIndex      = (int)type;
            entry.FindPropertyRelative("Magnitude").floatValue     = magnitude;
            entry.FindPropertyRelative("Duration").floatValue      = 0f;
            entry.FindPropertyRelative("Threshold").floatValue     = 0f;
        }

        // ── CSV 파싱 ──────────────────────────────────────────

        private static List<Row> ParseCsv(string text, List<string> errors)
        {
            var rows = new List<Row>();
            string[] lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            if (lines.Length < 2)
            {
                errors.Add("CSV is empty or header-only.");
                return rows;
            }

            // 헤더 검증
            List<string> header = SplitCsvLine(lines[0]);
            if (header.Count != ExpectedHeaders.Length)
            {
                errors.Add($"Header column count mismatch: expected {ExpectedHeaders.Length}, got {header.Count}");
                return rows;
            }
            for (int i = 0; i < ExpectedHeaders.Length; i++)
            {
                if (!string.Equals(header[i].Trim(), ExpectedHeaders[i], StringComparison.Ordinal))
                {
                    errors.Add($"Header[{i}] mismatch: expected '{ExpectedHeaders[i]}', got '{header[i]}'");
                }
            }
            if (errors.Count > 0) return rows;

            // 데이터 행
            for (int lineNo = 1; lineNo < lines.Length; lineNo++)
            {
                string line = lines[lineNo];
                if (string.IsNullOrWhiteSpace(line)) continue;

                List<string> fields = SplitCsvLine(line);
                if (fields.Count != ExpectedHeaders.Length)
                {
                    errors.Add($"Line {lineNo + 1}: column count mismatch (got {fields.Count}, expected {ExpectedHeaders.Length}). Line: {line}");
                    continue;
                }

                if (!TryParseRow(fields, lineNo + 1, errors, out Row row))
                    continue;

                rows.Add(row);
            }

            return rows;
        }

        private static bool TryParseRow(List<string> f, int lineNo, List<string> errors, out Row row)
        {
            row = default;
            try
            {
                row = new Row
                {
                    Id            = int.Parse(f[0]),
                    Name          = f[1].Trim(),
                    Theme         = f[2].Trim(),
                    Rarity        = ParseEnum<RelicRarity>(f[3], lineNo, "Rarity", errors),
                    TagPrimary    = ParseEnum<RelicTag>(f[4], lineNo, "TagPrimary", errors),
                    TagSecondary  = ParseEnum<RelicTag>(f[5], lineNo, "TagSecondary", errors),
                    SizeX         = int.Parse(f[6]),
                    SizeY         = int.Parse(f[7]),
                    Effect1Type   = ParseEnum<RelicEffectType>(f[8], lineNo, "Effect1Type", errors),
                    Effect1Mag    = float.Parse(f[9], System.Globalization.CultureInfo.InvariantCulture),
                    Effect2Type   = ParseEnum<RelicEffectType>(f[10], lineNo, "Effect2Type", errors),
                    Effect2Mag    = float.Parse(f[11], System.Globalization.CultureInfo.InvariantCulture),
                    Description   = f[12].Trim(),
                };
                return true;
            }
            catch (FormatException ex)
            {
                errors.Add($"Line {lineNo}: parse error — {ex.Message}");
                return false;
            }
            catch (OverflowException ex)
            {
                errors.Add($"Line {lineNo}: overflow — {ex.Message}");
                return false;
            }
        }

        private static T ParseEnum<T>(string value, int lineNo, string column, List<string> errors) where T : struct
        {
            if (Enum.TryParse(value.Trim(), ignoreCase: false, out T result))
                return result;
            errors.Add($"Line {lineNo}: invalid {column} '{value}' (must be one of {string.Join(", ", Enum.GetNames(typeof(T)))})");
            return default;
        }

        // RFC 4180 준수 — 따옴표 안의 콤마는 필드 구분자로 취급 안 함.
        private static List<string> SplitCsvLine(string line)
        {
            var fields = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"');   // escaped quote
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                else
                {
                    if (c == ',')
                    {
                        fields.Add(sb.ToString());
                        sb.Clear();
                    }
                    else if (c == '"' && sb.Length == 0)
                    {
                        inQuotes = true;
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
            }
            fields.Add(sb.ToString());
            return fields;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            string parent = Path.GetDirectoryName(folderPath).Replace('\\', '/');
            string leaf   = Path.GetFileName(folderPath);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                Debug.LogError($"[RelicDataBatchGenerator] Parent folder missing: {parent}");
                return;
            }
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static string SanitizeFileName(string raw)
        {
            // Windows 파일명 금지 문자 제거 + 공백 보존 (기존 18 SO 와 일관성)
            var sb = new StringBuilder(raw.Length);
            foreach (char c in raw)
            {
                if (c == '/' || c == '\\' || c == ':' || c == '*' || c == '?' ||
                    c == '"' || c == '<' || c == '>' || c == '|') continue;
                sb.Append(c);
            }
            return sb.ToString();
        }

        private struct Row
        {
            public int             Id;
            public string          Name;
            public string          Theme;
            public RelicRarity     Rarity;
            public RelicTag        TagPrimary;
            public RelicTag        TagSecondary;
            public int             SizeX;
            public int             SizeY;
            public RelicEffectType Effect1Type;
            public float           Effect1Mag;
            public RelicEffectType Effect2Type;
            public float           Effect2Mag;
            public string          Description;
        }
    }
}
