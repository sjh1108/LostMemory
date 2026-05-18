using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LostMemory.Editor.BalanceEditor
{
    /// <summary>
    /// JSON Export / Import 헬퍼.
    /// JsonUtility 기반 — RelicTag 등 enum 은 정수로 저장.
    /// 본 CL 대상 SO (RelicData / BuildSetData) 는 SO ref 거의 없어 라운드트립 안전.
    /// </summary>
    public static class JsonImportExport
    {
        private const string LastFolderKey = "BalanceEditor.LastExportFolder";

        // ---- Export ----

        public static bool ExportSingle(ScriptableObject so)
        {
            if (so == null) return false;
            string lastFolder = EditorPrefs.GetString(LastFolderKey, string.Empty);
            string path = EditorUtility.SaveFilePanel(
                "Export JSON", lastFolder, $"{so.name}.json", "json");
            if (string.IsNullOrEmpty(path)) return false;

            try
            {
                File.WriteAllText(path, JsonUtility.ToJson(so, prettyPrint: true));
                EditorPrefs.SetString(LastFolderKey, Path.GetDirectoryName(path) ?? string.Empty);
                return true;
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Export 실패",
                    $"파일 쓰기 실패:\n{e.Message}", "OK");
                return false;
            }
        }

        public static int ExportCategory(IBalanceCategoryProvider provider)
        {
            if (provider == null) return 0;
            string lastFolder = EditorPrefs.GetString(LastFolderKey, string.Empty);
            string root = EditorUtility.SaveFolderPanel(
                $"Export {provider.CategoryName} to Folder", lastFolder, provider.CategoryName);
            if (string.IsNullOrEmpty(root)) return 0;

            int count = 0;
            try
            {
                foreach (var so in provider.LoadAll())
                {
                    if (so == null) continue;
                    string p = Path.Combine(root, $"{so.name}.json");
                    File.WriteAllText(p, JsonUtility.ToJson(so, prettyPrint: true));
                    count++;
                }
                EditorPrefs.SetString(LastFolderKey, root);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Export 실패",
                    $"파일 쓰기 실패 ({count} 개 처리됨):\n{e.Message}", "OK");
            }
            return count;
        }

        public static int ExportAll(IEnumerable<IBalanceCategoryProvider> providers)
        {
            if (providers == null) return 0;
            string lastFolder = EditorPrefs.GetString(LastFolderKey, string.Empty);
            string root = EditorUtility.SaveFolderPanel(
                "Export All to Folder", lastFolder, "BalanceExport");
            if (string.IsNullOrEmpty(root)) return 0;

            int total = 0;
            try
            {
                foreach (var provider in providers)
                {
                    if (provider == null) continue;
                    var dir = Path.Combine(root, provider.CategoryName);
                    Directory.CreateDirectory(dir);
                    foreach (var so in provider.LoadAll())
                    {
                        if (so == null) continue;
                        string p = Path.Combine(dir, $"{so.name}.json");
                        File.WriteAllText(p, JsonUtility.ToJson(so, prettyPrint: true));
                        total++;
                    }
                }
                EditorPrefs.SetString(LastFolderKey, root);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Export 실패",
                    $"파일 쓰기 실패 ({total} 개 처리됨):\n{e.Message}", "OK");
            }
            return total;
        }

        // ---- Import ----

        public static bool ImportToSingle(ScriptableObject so)
        {
            if (so == null) return false;

            if (EditorUtility.IsDirty(so))
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "Import 충돌",
                    $"{so.name} 에 저장 안 한 변경이 있습니다.\nJSON 으로 덮어쓰시겠습니까?\n(JSON 에 없는 필드는 디폴트값으로 초기화됩니다)",
                    "Overwrite", "Cancel");
                if (!proceed) return false;
            }

            string lastFolder = EditorPrefs.GetString(LastFolderKey, string.Empty);
            string path = EditorUtility.OpenFilePanel("Import JSON", lastFolder, "json");
            if (string.IsNullOrEmpty(path)) return false;

            try
            {
                string json = File.ReadAllText(path);
                Undo.RecordObject(so, $"Import JSON: {Path.GetFileName(path)}");
                JsonUtility.FromJsonOverwrite(json, so);
                EditorUtility.SetDirty(so);
                EditorPrefs.SetString(LastFolderKey, Path.GetDirectoryName(path) ?? string.Empty);
                return true;
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Import 실패",
                    $"JSON 파싱 실패:\n{e.Message}", "OK");
                return false;
            }
        }

        public struct ImportFolderResult
        {
            public int Matched;
            public int Skipped;
            public int Failed;
        }

        public static ImportFolderResult ImportFolder(
            IEnumerable<IBalanceCategoryProvider> providers)
        {
            var result = new ImportFolderResult();
            if (providers == null) return result;

            string lastFolder = EditorPrefs.GetString(LastFolderKey, string.Empty);
            string root = EditorUtility.OpenFolderPanel("Import Folder", lastFolder, string.Empty);
            if (string.IsNullOrEmpty(root)) return result;

            // 이름 → SO 매핑 (모든 카테고리 통합)
            var allSo = new Dictionary<string, ScriptableObject>();
            foreach (var p in providers)
            {
                if (p == null) continue;
                foreach (var s in p.LoadAll())
                {
                    if (s != null) allSo[s.name] = s;
                }
            }

            foreach (var file in Directory.GetFiles(root, "*.json", SearchOption.AllDirectories))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (!allSo.TryGetValue(name, out var so))
                {
                    result.Skipped++;
                    continue;
                }
                try
                {
                    Undo.RecordObject(so, $"Import Folder: {name}");
                    JsonUtility.FromJsonOverwrite(File.ReadAllText(file), so);
                    EditorUtility.SetDirty(so);
                    result.Matched++;
                }
                catch
                {
                    result.Failed++;
                }
            }

            EditorPrefs.SetString(LastFolderKey, root);
            return result;
        }
    }
}
