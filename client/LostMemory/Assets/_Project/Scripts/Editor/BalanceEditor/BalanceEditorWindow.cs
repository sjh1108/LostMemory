using System.Collections.Generic;
using System.Linq;
using LostMemory.Editor.BalanceEditor.Providers;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace LostMemory.Editor.BalanceEditor
{
    /// <summary>
    /// Epic U 밸런스 에디터. UI Toolkit 단일 EditorWindow.
    /// CL-162: 셸. CL-163: 카테고리 트리 + 디테일 + 검색 + 자동 새로고침.
    /// </summary>
    public class BalanceEditorWindow : EditorWindow
    {
        private const string UxmlPath = "BalanceEditorWindow";
        private const string UssPath  = "BalanceEditorWindow";

        private static BalanceEditorWindow _instance;
        public static bool IsOpen => _instance != null;
        public static void RefreshTree()
        {
            if (_instance != null) _instance.RebuildTree(_instance._lastFilter);
        }

        private List<IBalanceCategoryProvider> _providers;
        private TreeView _treeView;
        private VisualElement _detailContainer;
        private Label _statusLabel;
        private ToolbarSearchField _searchField;
        private string _lastFilter = string.Empty;

        [MenuItem("LostMemory/Balance Editor")]
        public static void Open()
        {
            var window = GetWindow<BalanceEditorWindow>();
            window.titleContent = new GUIContent("Balance Editor");
            window.minSize = new Vector2(800, 500);
            window.Show();
        }

        private void OnEnable()
        {
            _instance = this;
        }

        private void OnDisable()
        {
            if (_instance == this) _instance = null;
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;

            var uxml = Resources.Load<VisualTreeAsset>(UxmlPath);
            if (uxml == null)
            {
                Debug.LogError($"[CL-163] UXML 미발견: Resources/{UxmlPath}");
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
                Debug.LogWarning($"[CL-163] USS 미발견: Resources/{UssPath} (스타일 미적용)");
            }

            _providers = new List<IBalanceCategoryProvider>
            {
                new RelicCategoryProvider(),
                new BuildSetCategoryProvider(),
            };

            var leftPanel = root.Q<VisualElement>("LeftPanel");
            var rightPanel = root.Q<VisualElement>("RightPanel");
            var body = leftPanel.parent;

            body.Clear();
            var split = new TwoPaneSplitView(0, 250, TwoPaneSplitViewOrientation.Horizontal);
            split.Add(leftPanel);
            split.Add(rightPanel);
            body.Add(split);

            _statusLabel = root.Q<Label>("StatusLabel");

            _searchField = root.Q<ToolbarSearchField>("SearchField");
            if (_searchField != null)
            {
                _searchField.RegisterValueChangedCallback(evt => RebuildTree(evt.newValue));
            }

            var refreshBtn = root.Q<Button>("RefreshButton");
            if (refreshBtn != null)
            {
                refreshBtn.clicked += () => RebuildTree(_lastFilter);
            }

            PopulateLeftPanel(leftPanel);
            PopulateRightPanel(rightPanel);

            RebuildTree();
        }

        protected virtual void PopulateLeftPanel(VisualElement panel)
        {
            panel.Clear();
            _treeView = new TreeView
            {
                makeItem = () => new Label(),
                bindItem = (element, index) =>
                {
                    var node = _treeView.GetItemDataForIndex<TreeNode>(index);
                    ((Label)element).text = node?.DisplayName ?? string.Empty;
                },
                fixedItemHeight = 18,
                style = { flexGrow = 1 }
            };
            _treeView.selectionChanged += OnTreeSelectionChanged;
            panel.Add(_treeView);
        }

        protected virtual void PopulateRightPanel(VisualElement panel)
        {
            panel.Clear();
            _detailContainer = new VisualElement
            {
                style = { flexGrow = 1 }
            };
            var emptyHint = new Label("(좌측 트리에서 항목을 선택하세요)")
            {
                name = "DetailEmptyHint"
            };
            emptyHint.AddToClassList("be-detail-empty");
            _detailContainer.Add(emptyHint);
            panel.Add(_detailContainer);
        }

        private void RebuildTree(string filter = null)
        {
            _lastFilter = filter ?? string.Empty;
            if (_treeView == null) return;

            var data = BuildTreeData();
            int totalLeaves = CountLeaves(data);
            if (!string.IsNullOrEmpty(_lastFilter))
            {
                data = FilterTree(data, _lastFilter);
            }
            _treeView.SetRootItems(data);
            _treeView.Rebuild();
            if (!string.IsNullOrEmpty(_lastFilter))
            {
                _treeView.ExpandAll();
            }

            UpdateStatus($"Loaded {_providers.Count} categories, {totalLeaves} items");
        }

        private List<TreeViewItemData<TreeNode>> BuildTreeData()
        {
            var roots = new List<TreeViewItemData<TreeNode>>();
            int id = 0;
            foreach (var provider in _providers)
            {
                var sos = provider.LoadAll()
                    .Where(s => s != null)
                    .OrderBy(s => s.name)
                    .ToList();

                var leaves = new List<TreeViewItemData<TreeNode>>();
                foreach (var so in sos)
                {
                    var leaf = new TreeNode
                    {
                        DisplayName = so.name,
                        So = so,
                        CategoryName = provider.CategoryName
                    };
                    leaves.Add(new TreeViewItemData<TreeNode>(id++, leaf));
                }

                var category = new TreeNode
                {
                    DisplayName = $"{provider.CategoryName} ({sos.Count})",
                    CategoryName = provider.CategoryName
                };
                roots.Add(new TreeViewItemData<TreeNode>(id++, category, leaves));
            }
            return roots;
        }

        private List<TreeViewItemData<TreeNode>> FilterTree(
            List<TreeViewItemData<TreeNode>> source, string filter)
        {
            var lower = filter.ToLowerInvariant();
            int newId = 100000;
            var result = new List<TreeViewItemData<TreeNode>>();
            foreach (var category in source)
            {
                var children = category.children?.ToList() ?? new List<TreeViewItemData<TreeNode>>();
                var matched = children
                    .Where(c => c.data?.So != null &&
                                c.data.DisplayName.ToLowerInvariant().Contains(lower))
                    .Select(c => new TreeViewItemData<TreeNode>(newId++, c.data))
                    .ToList();
                if (matched.Count == 0) continue;

                var newCat = new TreeNode
                {
                    DisplayName = $"{category.data.CategoryName} ({matched.Count})",
                    CategoryName = category.data.CategoryName
                };
                result.Add(new TreeViewItemData<TreeNode>(newId++, newCat, matched));
            }
            return result;
        }

        private static int CountLeaves(List<TreeViewItemData<TreeNode>> data)
        {
            int total = 0;
            foreach (var node in data)
            {
                if (node.children != null)
                {
                    foreach (var c in node.children)
                    {
                        if (c.data?.So != null) total++;
                    }
                }
            }
            return total;
        }

        private void OnTreeSelectionChanged(IEnumerable<object> selected)
        {
            var node = selected.OfType<TreeNode>().FirstOrDefault();
            if (node?.So == null)
            {
                ClearDetail();
                return;
            }
            ShowDetail(node.So);
            EditorGUIUtility.PingObject(node.So);
            UpdateStatus($"Selected: {node.So.name}");
        }

        private void ShowDetail(ScriptableObject so)
        {
            if (_detailContainer == null) return;
            _detailContainer.Clear();
            var inspector = new InspectorElement(so);
            _detailContainer.Add(inspector);
        }

        private void ClearDetail()
        {
            if (_detailContainer == null) return;
            _detailContainer.Clear();
            var emptyHint = new Label("(좌측 트리에서 항목을 선택하세요)")
            {
                name = "DetailEmptyHint"
            };
            emptyHint.AddToClassList("be-detail-empty");
            _detailContainer.Add(emptyHint);
        }

        private void UpdateStatus(string msg)
        {
            if (_statusLabel != null) _statusLabel.text = msg;
        }
    }
}
