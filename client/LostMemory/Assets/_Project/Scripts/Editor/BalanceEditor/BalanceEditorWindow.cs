using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LostMemory.Editor.BalanceEditor
{
    /// <summary>
    /// CL-162: Epic U(밸런스 에디터) 셸. UI Toolkit 기반 단일 EditorWindow.
    /// 본 CL 은 인프라만 — 실제 데이터 표시/편집은 CL-163 부터.
    /// </summary>
    /// test
    public class BalanceEditorWindow : EditorWindow
    {
        private const string UxmlPath = "BalanceEditorWindow";
        private const string UssPath  = "BalanceEditorWindow";



        [MenuItem("LostMemory/Balance Editor")]
        public static void Open()
        {
            var window = GetWindow<BalanceEditorWindow>();
            window.titleContent = new GUIContent("Balance Editor");
            window.minSize = new Vector2(800, 500);
            window.Show();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;

            var uxml = Resources.Load<VisualTreeAsset>(UxmlPath);
            if (uxml == null)
            {
                Debug.LogError($"[CL-162] UXML 미발견: Resources/{UxmlPath}");
                return;
            }
            uxml.CloneTree(root);

            var uss = Resources.Load<StyleSheet>(UssPath);
            if (uss != null)
            {
                root.styleSheets.Add(uss);
            }
            else
            {
                Debug.LogWarning($"[CL-162] USS 미발견: Resources/{UssPath} (스타일 미적용)");
            }

            PopulateLeftPanel(root.Q<VisualElement>("LeftPanel"));
            PopulateRightPanel(root.Q<VisualElement>("RightPanel"));
        }

        // CL-163 override 진입점.
        protected virtual void PopulateLeftPanel(VisualElement panel) { }
        protected virtual void PopulateRightPanel(VisualElement panel) { }
    }
}
