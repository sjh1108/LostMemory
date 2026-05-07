using System;
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
    /// CL-162: 셸. CL-163: 트리/디테일/검색/자동 새로고침. CL-164: Dirty/Save/Undo.
    /// </summary>
    public class BalanceEditorWindow : EditorWindow
    {
        private const string UxmlPath = "BalanceEditorWindow";
        private const string UssPath  = "BalanceEditorWindow";
        private const string DirtyClass = "be-tree-item-dirty";

        private static BalanceEditorWindow _instance;
        public static bool IsOpen => _instance != null;
        public static event Action<ScriptableObject> ScriptableObjectChanged;

        // SaveAll 동안 AssetPostprocessor 의 자동 새로고침 억제 (선택/디테일 보존)
        internal static bool SuppressAssetWatcher { get; private set; }

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
        private bool _isQuitting;
        private string _lastAutoSaveTimestamp = string.Empty;

        // 디테일 패널이 현재 표시 중인 SO. 같은 SO 재선택 시 InspectorElement 재생성 방지.
        private ScriptableObject _currentlyShownSo;
        // RebuildTree 진행 중 발화하는 빈 selectionChanged 가 ClearDetail 호출 못 하게 차단.
        private bool _inRebuild;

        // CL-165: Auto-save debounce 컨트롤러
        private AutoSaveController _autoSave;

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
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.quitting += OnEditorQuitting;
            EditorApplication.update += OnEditorUpdate;

            _autoSave = new AutoSaveController(CountAllDirty, SaveAll);
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.quitting -= OnEditorQuitting;
            EditorApplication.update -= OnEditorUpdate;
            if (_instance == this) _instance = null;
        }

        private void OnEditorUpdate()
        {
            _autoSave?.Tick();
        }

        private void OnEditorQuitting()
        {
            _isQuitting = true;
        }

        private void OnDestroy()
        {
            if (_isQuitting) return;

            int dirtyCount = CountAllDirty();
            if (dirtyCount <= 0) return;

            bool save = EditorUtility.DisplayDialog(
                "Balance Editor",
                $"{dirtyCount} 개의 SO 가 dirty 상태입니다.\n" +
                "지금 저장하지 않으면 Unity 가 다음 SaveAssets 까지 메모리에 보관합니다.",
                "Save Now",
                "Later");
            if (save) AssetDatabase.SaveAssets();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;

            var uxml = Resources.Load<VisualTreeAsset>(UxmlPath);
            if (uxml == null)
            {
                Debug.LogError($"[CL-164] UXML 미발견: Resources/{UxmlPath}");
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
                Debug.LogWarning($"[CL-164] USS 미발견: Resources/{UssPath} (스타일 미적용)");
            }

            _providers = new List<IBalanceCategoryProvider>
            {
                new RelicCategoryProvider(),
                new BuildSetCategoryProvider(),
                new WeaponCategoryProvider(),
                new SkillCategoryProvider(),
                new ShopConfigCategoryProvider(),
                new EnemyDataCategoryProvider(),
                new BossDataCategoryProvider(),
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

            var saveBtn = root.Q<Button>("SaveButton");
            if (saveBtn != null)
            {
                saveBtn.clicked += SaveAll;
            }

            // CL-165: Export / Import / Auto-save toolbar
            var exportMenu = root.Q<ToolbarMenu>("ExportMenu");
            if (exportMenu != null)
            {
                exportMenu.menu.AppendAction("Selected SO",
                    _ => ExportSelected(),
                    _ => _currentlyShownSo != null
                        ? DropdownMenuAction.Status.Normal
                        : DropdownMenuAction.Status.Disabled);
                exportMenu.menu.AppendAction("Current Category",
                    _ => ExportCurrentCategory(),
                    _ => GetCurrentCategoryProvider() != null
                        ? DropdownMenuAction.Status.Normal
                        : DropdownMenuAction.Status.Disabled);
                exportMenu.menu.AppendAction("All Categories", _ => ExportAll());
            }

            var importMenu = root.Q<ToolbarMenu>("ImportMenu");
            if (importMenu != null)
            {
                importMenu.menu.AppendAction("To Selected SO",
                    _ => ImportToSelected(),
                    _ => _currentlyShownSo != null
                        ? DropdownMenuAction.Status.Normal
                        : DropdownMenuAction.Status.Disabled);
                importMenu.menu.AppendAction("Folder...", _ => ImportFolder());
            }

            var autoSaveToggle = root.Q<ToolbarToggle>("AutoSaveToggle");
            if (autoSaveToggle != null && _autoSave != null)
            {
                autoSaveToggle.value = _autoSave.Enabled;
                autoSaveToggle.RegisterValueChangedCallback(evt =>
                {
                    _autoSave.SetEnabled(evt.newValue);
                    UpdateStatus(evt.newValue
                        ? $"Auto-save ON ({(int)_autoSave.DelaySeconds}s debounce)"
                        : "Auto-save OFF");
                    UpdateTitle();
                });
            }

            var delayField = root.Q<IntegerField>("AutoSaveDelay");
            if (delayField != null && _autoSave != null)
            {
                delayField.value = (int)_autoSave.DelaySeconds;
                delayField.RegisterValueChangedCallback(evt =>
                {
                    int clamped = (int)Math.Clamp(
                        evt.newValue, AutoSaveController.MinDelay, AutoSaveController.MaxDelay);
                    if (clamped != evt.newValue) delayField.SetValueWithoutNotify(clamped);
                    _autoSave.SetDelay(clamped);
                    UpdateTitle();
                });
            }

            root.RegisterCallback<KeyDownEvent>(OnKeyDown);

            PopulateLeftPanel(leftPanel);
            PopulateRightPanel(rightPanel);

            RebuildTree();
            UpdateTitle();
        }

        private void OnKeyDown(KeyDownEvent e)
        {
            if (e.ctrlKey && e.keyCode == KeyCode.S)
            {
                SaveAll();
                e.StopPropagation();
            }
        }

        private void SaveAll()
        {
            NotifyDirtyScriptableObjectsChanged();

            // SaveAssets 가 AssetPostprocessor 트리거 → RebuildTree 로 선택/디테일 잃을 위험.
            // SaveAll 동안만 watcher 억제. RefreshItems 로 라벨만 직접 갱신.
            SuppressAssetWatcher = true;
            try
            {
                AssetDatabase.SaveAssets();
            }
            finally
            {
                SuppressAssetWatcher = false;
            }
            _autoSave?.NotifySaved();
            _lastAutoSaveTimestamp = DateTime.Now.ToString("HH:mm:ss");
            _treeView?.RefreshItems();
            UpdateTitle();
            UpdateStatus($"Saved at {_lastAutoSaveTimestamp}");
        }

        private void OnUndoRedo()
        {
            _treeView?.RefreshItems();
            _autoSave?.NotifyChange();
            NotifyScriptableObjectChanged(_currentlyShownSo);
            UpdateTitle();
            UpdateStatus("Undo/Redo applied");
        }

        private void OnInspectorChanged()
        {
            _treeView?.RefreshItems();
            _autoSave?.NotifyChange();
            NotifyScriptableObjectChanged(_currentlyShownSo);
            UpdateTitle();
        }

        private static void NotifyScriptableObjectChanged(ScriptableObject so)
        {
            if (so == null || !EditorApplication.isPlaying)
            {
                return;
            }

            ScriptableObjectChanged?.Invoke(so);
        }

        private void NotifyDirtyScriptableObjectsChanged()
        {
            if (!EditorApplication.isPlaying || _providers == null)
            {
                return;
            }

            foreach (var provider in _providers)
            {
                foreach (var so in provider.LoadAll().Where(DirtyTracker.IsDirty))
                {
                    NotifyScriptableObjectChanged(so);
                }
            }
        }

        // ---- CL-165: Export / Import handlers ----

        private IBalanceCategoryProvider GetCurrentCategoryProvider()
        {
            if (_currentlyShownSo == null || _providers == null) return null;
            foreach (var p in _providers)
            {
                if (p.LoadAll().Contains(_currentlyShownSo)) return p;
            }
            return null;
        }

        private void ExportSelected()
        {
            if (_currentlyShownSo == null) return;
            if (JsonImportExport.ExportSingle(_currentlyShownSo))
            {
                UpdateStatus($"Exported: {_currentlyShownSo.name}.json");
            }
        }

        private void ExportCurrentCategory()
        {
            var provider = GetCurrentCategoryProvider();
            if (provider == null) return;
            int count = JsonImportExport.ExportCategory(provider);
            if (count > 0) UpdateStatus($"Exported {count} {provider.CategoryName} files");
        }

        private void ExportAll()
        {
            int total = JsonImportExport.ExportAll(_providers);
            if (total > 0) UpdateStatus($"Exported {total} files (all categories)");
        }

        private void ImportToSelected()
        {
            if (_currentlyShownSo == null) return;
            SuppressAssetWatcher = true;
            try
            {
                if (JsonImportExport.ImportToSingle(_currentlyShownSo))
                {
                    UpdateTitle();
                    _treeView?.RefreshItems();
                    _autoSave?.NotifyChange();
                    NotifyScriptableObjectChanged(_currentlyShownSo);
                    UpdateStatus($"Imported to {_currentlyShownSo.name}");
                }
            }
            finally
            {
                SuppressAssetWatcher = false;
            }
        }

        private void ImportFolder()
        {
            SuppressAssetWatcher = true;
            try
            {
                var result = JsonImportExport.ImportFolder(_providers);
                if (result.Matched + result.Skipped + result.Failed == 0) return;
                UpdateTitle();
                _treeView?.RefreshItems();
                _autoSave?.NotifyChange();
                NotifyDirtyScriptableObjectsChanged();
                string msg = $"Imported {result.Matched} matched, {result.Skipped} skipped";
                if (result.Failed > 0) msg += $", {result.Failed} failed";
                UpdateStatus(msg);
            }
            finally
            {
                SuppressAssetWatcher = false;
            }
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
                    var label = (Label)element;
                    if (node == null)
                    {
                        label.text = string.Empty;
                        return;
                    }

                    if (node.So != null)
                    {
                        // Leaf 노드 — dirty marker
                        bool dirty = DirtyTracker.IsDirty(node.So);
                        label.text = dirty ? "* " + node.DisplayName : node.DisplayName;
                        if (dirty && !label.ClassListContains(DirtyClass))
                            label.AddToClassList(DirtyClass);
                        else if (!dirty && label.ClassListContains(DirtyClass))
                            label.RemoveFromClassList(DirtyClass);
                    }
                    else if (node.CachedSos != null)
                    {
                        // Category 노드 — 동적 dirty count
                        int dirtyCount = node.CachedSos.Count(DirtyTracker.IsDirty);
                        label.text = dirtyCount > 0
                            ? $"{node.CategoryName} ({node.CachedSos.Count}) [{dirtyCount} dirty]"
                            : $"{node.CategoryName} ({node.CachedSos.Count})";
                        if (label.ClassListContains(DirtyClass))
                            label.RemoveFromClassList(DirtyClass);
                    }
                    else
                    {
                        label.text = node.DisplayName ?? string.Empty;
                    }
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

            var prevSelectedSo = _currentlyShownSo;

            _inRebuild = true;
            try
            {
                var data = BuildTreeData();
                int totalLeaves = CountLeaves(data);
                int totalDirty = CountAllDirty();
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

                if (prevSelectedSo != null)
                {
                    int? newId = FindIdForSo(prevSelectedSo, data);
                    if (newId.HasValue)
                    {
                        _treeView.SetSelectionById(newId.Value);
                        _treeView.ScrollToItemById(newId.Value);
                    }
                    // 트리에서 사라진 경우 (검색 필터로 가려짐 / 외부 삭제) — 디테일은 그대로 유지.
                }

                string dirtyMsg = totalDirty > 0 ? $", {totalDirty} dirty" : "";
                UpdateStatus($"Loaded {_providers.Count} categories, {totalLeaves} items{dirtyMsg}");
            }
            finally
            {
                _inRebuild = false;
            }
        }

        private static int? FindIdForSo(
            ScriptableObject so, List<TreeViewItemData<TreeNode>> data)
        {
            if (so == null) return null;
            foreach (var category in data)
            {
                if (category.children == null) continue;
                foreach (var leaf in category.children)
                {
                    if (leaf.data?.So == so) return leaf.id;
                }
            }
            return null;
        }

        private List<TreeViewItemData<TreeNode>> BuildTreeData()
        {
            var roots = new List<TreeViewItemData<TreeNode>>();
            int id = 0;
            foreach (var provider in _providers.OrderBy(p => p.CategoryName))
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

                // 카테고리 라벨은 bindItem 에서 CachedSos 기반 동적 계산.
                var category = new TreeNode
                {
                    DisplayName = provider.CategoryName,
                    CategoryName = provider.CategoryName,
                    CachedSos = sos
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

                var matchedSos = matched
                    .Select(c => c.data?.So)
                    .Where(s => s != null)
                    .ToList();
                var newCat = new TreeNode
                {
                    DisplayName = category.data.CategoryName,
                    CategoryName = category.data.CategoryName,
                    CachedSos = matchedSos
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

        private int CountAllDirty()
        {
            if (_providers == null) return 0;
            int count = 0;
            foreach (var p in _providers)
            {
                count += p.LoadAll().Count(DirtyTracker.IsDirty);
            }
            return count;
        }

        private void UpdateTitle()
        {
            int dirtyCount = CountAllDirty();
            string parts = "Balance Editor";
            if (dirtyCount > 0) parts += $" ({dirtyCount} unsaved)";
            if (_autoSave != null && _autoSave.Enabled)
            {
                parts += $" • Auto {(int)_autoSave.DelaySeconds}s";
            }
            titleContent = new GUIContent(parts);
        }

        private void OnTreeSelectionChanged(IEnumerable<object> selected)
        {
            var node = selected.OfType<TreeNode>().FirstOrDefault();
            if (node?.So == null)
            {
                // RebuildTree 진행 중 발화하는 빈 selectionChanged 는 무시 (디테일 보존)
                if (_inRebuild) return;
                ClearDetail();
                _currentlyShownSo = null;
                return;
            }
            if (node.So == _currentlyShownSo)
            {
                // 같은 SO 재선택 — 인스펙터 재생성하지 않고 그대로 유지
                UpdateStatus($"Selected: {node.So.name}");
                return;
            }
            _currentlyShownSo = node.So;
            ShowDetail(node.So);
            EditorGUIUtility.PingObject(node.So);
            UpdateStatus($"Selected: {node.So.name}");
        }

        private void ShowDetail(ScriptableObject so)
        {
            if (_detailContainer == null) return;
            _detailContainer.Clear();
            var inspector = new InspectorElement(so);
            inspector.RegisterCallback<SerializedPropertyChangeEvent>(_ => OnInspectorChanged());
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
