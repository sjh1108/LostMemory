using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LostMemory.EditorTools
{
    /// <summary>
    /// 선택한 GameObject (Hierarchy 또는 Project 의 prefab asset) 의 자기 + 모든 자식을 스캔해
    /// missing script (Mono Script 깨짐) / null ObjectReference 필드를 콘솔에 보고한다.
    ///
    /// 사용: Tools → Lost Memory → Scan Missing Refs in Selection.
    /// Project 창에서 prefab .prefab 클릭 후 메뉴 호출 시 그 prefab asset 전체 스캔.
    /// Hierarchy 에서 GameObject 클릭 후 호출도 가능.
    /// 여러 개 동시 선택 가능.
    /// </summary>
    public static class MissingRefScanner
    {
        private const string MenuPath = "Tools/Lost Memory/Scan Missing Refs in Selection";

        [MenuItem(MenuPath, priority = 100)]
        private static void Scan()
        {
            Object[] targets = Selection.objects;
            if (targets == null || targets.Length == 0)
            {
                Debug.LogWarning("[MissingRefScanner] 선택된 객체 없음. Hierarchy 또는 Project 에서 prefab/GameObject 를 선택하고 다시 시도.");
                return;
            }

            int totalMissingScripts = 0;
            int totalMissingRefs = 0;
            int scannedObjects = 0;

            foreach (Object target in targets)
            {
                GameObject root = target as GameObject;
                if (root == null) continue;

                StringBuilder report = new StringBuilder();
                report.AppendLine($"[MissingRefScanner] === {AssetDatabase.GetAssetPath(target)} | {root.name} ===");

                int ms, mr, scanned;
                ScanGameObjectRecursive(root, report, out ms, out mr, out scanned);

                totalMissingScripts += ms;
                totalMissingRefs += mr;
                scannedObjects += scanned;

                if (ms == 0 && mr == 0)
                {
                    report.AppendLine("  (clean — no missing scripts, no null object references)");
                }
                Debug.Log(report.ToString());
            }

            Debug.Log($"[MissingRefScanner] 완료. GameObjects 스캔={scannedObjects}, missing scripts={totalMissingScripts}, null obj refs={totalMissingRefs}");
        }

        private static void ScanGameObjectRecursive(GameObject go, StringBuilder report, out int missingScripts, out int missingRefs, out int scanned)
        {
            missingScripts = 0;
            missingRefs = 0;
            scanned = 0;

            ScanSingleGameObject(go, report, ref missingScripts, ref missingRefs);
            scanned++;

            Transform t = go.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                int ms2, mr2, sc2;
                ScanGameObjectRecursive(t.GetChild(i).gameObject, report, out ms2, out mr2, out sc2);
                missingScripts += ms2;
                missingRefs += mr2;
                scanned += sc2;
            }
        }

        private static void ScanSingleGameObject(GameObject go, StringBuilder report, ref int missingScripts, ref int missingRefs)
        {
            string path = GetHierarchyPath(go);

            Component[] components = go.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component c = components[i];
                if (c == null)
                {
                    missingScripts++;
                    report.AppendLine($"  ★ MISSING SCRIPT — {path}  [component index {i}]");
                    continue;
                }

                // ObjectReference 필드 중 missing/null 검사 (None=정상 vs Missing=깨진 reference 구분).
                SerializedObject so = new SerializedObject(c);
                SerializedProperty prop = so.GetIterator();
                bool enterChildren = true;
                while (prop.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                    // instanceID != 0 인데 objectReferenceValue == null 이면 missing reference.
                    if (prop.objectReferenceInstanceIDValue != 0 && prop.objectReferenceValue == null)
                    {
                        missingRefs++;
                        report.AppendLine($"  ✗ MISSING REF — {path} :: {c.GetType().Name}.{prop.propertyPath}  (instanceID={prop.objectReferenceInstanceIDValue})");
                    }
                }
            }
        }

        private static string GetHierarchyPath(GameObject go)
        {
            List<string> stack = new List<string>();
            Transform t = go.transform;
            while (t != null)
            {
                stack.Add(t.name);
                t = t.parent;
            }
            stack.Reverse();
            return string.Join("/", stack);
        }
    }
}
