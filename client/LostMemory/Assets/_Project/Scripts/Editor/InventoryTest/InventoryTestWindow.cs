using System.Collections.Generic;
using System.Linq;
using LostMemory.Relics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LostMemory.Editor.InventoryTest
{
    /// <summary>
    /// Epic U 인벤토리 테스트 도구. Play 모드 전제 별도 EditorWindow.
    /// CL-174: 셸 + Play 모드 가드 + Player 자동 검색.
    /// CL-175: 좌측 RelicData 트리 + 우측 인벤토리 슬롯 표시 + 이벤트 자동 갱신.
    /// CL-176~177: 동작 / Consumable 슬롯 지정 추가 예정.
    /// </summary>
    public class InventoryTestWindow : EditorWindow
    {
        private const string UxmlPath = "InventoryTestWindow";
        private const string UssPath  = "InventoryTestWindow";

        private static readonly string[] RelicSearchFolders =
        {
            "Assets/_Project/ScriptableObjects/Relics/Generated"
        };

        private PlayerRelicInventory _relicInv;
        private PlayerConsumableInventory _consumeInv;

        private VisualElement _bodyContainer;
        private Label _modeHint;

        private TreeView _treeView;
        private VisualElement _permanentArea;
        private VisualElement _consumableArea;
        private Label _permanentHeader;
        private Label _consumableHeader;
        private List<RelicData> _allRelics = new();

        [MenuItem("LostMemory/Inventory Test Window")]
        public static void Open()
        {
            var window = GetWindow<InventoryTestWindow>();
            window.titleContent = new GUIContent("Inventory Test");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            UnsubscribeInventoryEvents();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;

            var uxml = Resources.Load<VisualTreeAsset>(UxmlPath);
            if (uxml == null)
            {
                Debug.LogError($"[CL-174] UXML 미발견: Resources/{UxmlPath}");
                return;
            }
            uxml.CloneTree(root);

            var uss = Resources.Load<StyleSheet>(UssPath);
            if (uss != null) root.styleSheets.Add(uss);

            _bodyContainer = root.Q<VisualElement>("BodyContainer");
            _modeHint      = root.Q<Label>("ModeHint");

            var leftPanel = root.Q<VisualElement>("LeftPanel");
            _permanentArea  = root.Q<VisualElement>("PermanentArea");
            _consumableArea = root.Q<VisualElement>("ConsumableArea");
            _permanentHeader  = root.Q<Label>("PermanentHeader");
            _consumableHeader = root.Q<Label>("ConsumableHeader");

            var refreshBtn = root.Q<Button>("RefreshButton");
            if (refreshBtn != null) refreshBtn.clicked += OnRefreshClicked;

            BuildLeftTree(leftPanel);
            UpdateModeView();
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode ||
                change == PlayModeStateChange.EnteredEditMode)
            {
                UpdateModeView();
            }
        }

        private void UpdateModeView()
        {
            bool playMode = EditorApplication.isPlaying;
            if (_modeHint != null)
                _modeHint.style.display = playMode ? DisplayStyle.None : DisplayStyle.Flex;
            if (_bodyContainer != null)
                _bodyContainer.SetEnabled(playMode);

            if (playMode) RefreshPlayerInstances();
            else UnsubscribeInventoryEvents();
        }

        private void RefreshPlayerInstances()
        {
            UnsubscribeInventoryEvents();
            _relicInv   = Object.FindAnyObjectByType<PlayerRelicInventory>();
            _consumeInv = Object.FindAnyObjectByType<PlayerConsumableInventory>();
            SubscribeInventoryEvents();
            RefreshInventoryView();
        }

        // ── 좌측 트리 ──────────────────────────────────────

        private void BuildLeftTree(VisualElement leftPanel)
        {
            _allRelics = LoadAllRelics();

            _treeView = new TreeView
            {
                makeItem = () => new Label(),
                bindItem = (element, index) =>
                {
                    var node = _treeView.GetItemDataForIndex<InventoryTreeNode>(index);
                    var label = (Label)element;
                    label.text = node?.DisplayLabel ?? string.Empty;
                },
                fixedItemHeight = 18,
                style = { flexGrow = 1 }
            };
            _treeView.selectionChanged += OnTreeSelectionChanged;
            _treeView.SetRootItems(BuildTreeData());
            _treeView.Rebuild();
            _treeView.ExpandAll();

            leftPanel.Clear();
            leftPanel.Add(_treeView);
        }

        private List<RelicData> LoadAllRelics()
        {
            var guids = AssetDatabase.FindAssets("t:RelicData", RelicSearchFolders);
            return guids
                .Select(g => AssetDatabase.LoadAssetAtPath<RelicData>(
                    AssetDatabase.GUIDToAssetPath(g)))
                .Where(r => r != null)
                .OrderBy(r => r.DisplayName ?? r.name)
                .ToList();
        }

        private List<TreeViewItemData<InventoryTreeNode>> BuildTreeData()
        {
            var permanents  = _allRelics.Where(r => !r.IsConsumable).ToList();
            var consumables = _allRelics.Where(r =>  r.IsConsumable).ToList();

            int id = 0;
            var roots = new List<TreeViewItemData<InventoryTreeNode>>();
            roots.Add(BuildGroup(ref id, "Permanent", permanents));
            roots.Add(BuildGroup(ref id, "Consumable", consumables));
            return roots;
        }

        private TreeViewItemData<InventoryTreeNode> BuildGroup(
            ref int id, string groupName, List<RelicData> sos)
        {
            var leaves = new List<TreeViewItemData<InventoryTreeNode>>(sos.Count);
            foreach (var r in sos)
            {
                leaves.Add(new TreeViewItemData<InventoryTreeNode>(
                    id++,
                    new InventoryTreeNode { Relic = r, DisplayLabel = r.DisplayName ?? r.name }
                ));
            }

            var group = new InventoryTreeNode
            {
                DisplayLabel = $"{groupName} ({sos.Count})"
            };
            return new TreeViewItemData<InventoryTreeNode>(id++, group, leaves);
        }

        private void OnTreeSelectionChanged(IEnumerable<object> selected)
        {
            var node = selected.OfType<InventoryTreeNode>().FirstOrDefault();
            if (node?.Relic == null) return;
            EditorGUIUtility.PingObject(node.Relic);
        }

        // ── 우측 슬롯 ──────────────────────────────────────

        private void RefreshInventoryView()
        {
            if (_relicInv == null || _consumeInv == null) return;
            RefreshPermanentArea();
            RefreshConsumableArea();
        }

        private void RefreshPermanentArea()
        {
            if (_relicInv == null || _permanentArea == null) return;
            int max = _relicInv.MaxSlots;
            var owned = _relicInv.OwnedRelics;
            int filled = owned.Count(r => r != null);

            _permanentHeader.text = $"Permanent ({filled}/{max})";

            var grid = _permanentArea.Q<VisualElement>("PermanentGrid");
            grid.Clear();
            for (int i = 0; i < max; i++)
            {
                var slot = i < owned.Count ? owned[i] : null;
                grid.Add(BuildSlotElement(slot));
            }
        }

        private void RefreshConsumableArea()
        {
            if (_consumeInv == null || _consumableArea == null) return;
            var slots = _consumeInv.Slots;
            int filled = slots.Count(s => s != null);
            _consumableHeader.text = $"Consumable ({filled}/{PlayerConsumableInventory.SlotCount})";

            var grid = _consumableArea.Q<VisualElement>("ConsumableGrid");
            grid.Clear();
            for (int i = 0; i < PlayerConsumableInventory.SlotCount; i++)
                grid.Add(BuildSlotElement(slots[i]));
        }

        private VisualElement BuildSlotElement(RelicData relic)
        {
            var slot = new VisualElement();
            slot.AddToClassList("iv-slot");
            if (relic == null)
            {
                slot.AddToClassList("iv-slot-empty");
                slot.Add(new Label("·"));
            }
            else
            {
                slot.Add(new Label(relic.DisplayName ?? relic.name));
            }
            return slot;
        }

        // ── 이벤트 구독 ─────────────────────────────────────

        private void SubscribeInventoryEvents()
        {
            if (_relicInv == null) return;
            _relicInv.OnRelicAcquired += OnRelicChanged;
            _relicInv.OnRelicRemoved  += OnRelicChanged;
            _relicInv.OnCleared       += OnInventoryCleared;
            _relicInv.MaxSlotsChanged += OnMaxSlotsChanged;
        }

        private void UnsubscribeInventoryEvents()
        {
            if (_relicInv == null) return;
            _relicInv.OnRelicAcquired -= OnRelicChanged;
            _relicInv.OnRelicRemoved  -= OnRelicChanged;
            _relicInv.OnCleared       -= OnInventoryCleared;
            _relicInv.MaxSlotsChanged -= OnMaxSlotsChanged;
        }

        private void OnRelicChanged(RelicData _) => RefreshPermanentArea();
        private void OnInventoryCleared()        => RefreshPermanentArea();
        private void OnMaxSlotsChanged(int _)    => RefreshPermanentArea();

        // ── Refresh 버튼 ───────────────────────────────────

        private void OnRefreshClicked()
        {
            _allRelics = LoadAllRelics();
            _treeView.SetRootItems(BuildTreeData());
            _treeView.Rebuild();
            _treeView.ExpandAll();
            RefreshInventoryView();
        }
    }
}
