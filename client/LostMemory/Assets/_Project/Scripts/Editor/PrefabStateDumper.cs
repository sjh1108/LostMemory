using System.Text;
using UnityEditor;
using UnityEngine;

namespace LostMemory.EditorTools
{
    /// <summary>
    /// 선택한 GameObject (또는 prefab) 의 자기 + 모든 자식 GameObject 의 active 상태 +
    /// 부착된 모든 Behaviour 컴포넌트의 enabled 상태를 콘솔에 dump.
    ///
    /// 사용: Project 창에서 prefab 선택 또는 Hierarchy 에서 GameObject 선택 →
    ///       Tools → Lost Memory → Dump Prefab State.
    ///
    /// missing script / 회색 None 도 표시.
    /// </summary>
    public static class PrefabStateDumper
    {
        [MenuItem("Tools/Lost Memory/Dump Prefab State", priority = 110)]
        private static void Dump()
        {
            Object[] targets = Selection.objects;
            if (targets == null || targets.Length == 0)
            {
                Debug.LogWarning("[PrefabStateDumper] 선택된 객체 없음.");
                return;
            }

            foreach (Object target in targets)
            {
                GameObject root = target as GameObject;
                if (root == null) continue;

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"[PrefabStateDumper] === {AssetDatabase.GetAssetPath(target)} | {root.name} ===");
                DumpRecursive(root, sb, "");
                Debug.Log(sb.ToString());
            }
        }

        private static void DumpRecursive(GameObject go, StringBuilder sb, string indent)
        {
            string activeMark = go.activeSelf ? "[ACTIVE]" : "[INACTIVE]";
            sb.AppendLine($"{indent}{activeMark} {go.name}");

            Component[] components = go.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component c = components[i];
                if (c == null)
                {
                    sb.AppendLine($"{indent}  ★ MISSING SCRIPT  [component index {i}]");
                    continue;
                }

                if (c is Behaviour behaviour)
                {
                    string mark = behaviour.enabled ? "✓" : "✗";
                    sb.AppendLine($"{indent}  {mark} {c.GetType().Name}");
                }
                else if (c is Renderer renderer)
                {
                    string mark = renderer.enabled ? "✓" : "✗";
                    sb.AppendLine($"{indent}  {mark} {c.GetType().Name} (Renderer)");
                }
                else if (c is Collider2D col2D)
                {
                    string mark = col2D.enabled ? "✓" : "✗";
                    sb.AppendLine($"{indent}  {mark} {c.GetType().Name} (Collider2D)");
                }
                else
                {
                    sb.AppendLine($"{indent}  · {c.GetType().Name}");
                }
            }

            Transform t = go.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                DumpRecursive(t.GetChild(i).gameObject, sb, indent + "  ");
            }
        }
    }
}
