using System.Collections.Generic;
using System.Linq;
using LostMemory.Relics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace LostMemory.Editor.InventoryTest
{
    /// <summary>
    /// Epic U 인벤토리 테스트 도구. Play 모드 전제 별도 EditorWindow.
    /// CL-174: 셸 + Play 모드 가드 + Player 자동 검색.
    /// CL-175: 좌측 RelicData 트리 + 우측 인벤토리 슬롯 표시 + 이벤트 자동 갱신.
    /// CL-176: 더블클릭 추가 / 우클릭 제거 / Clear 버튼.
    /// CL-177: Consumable 슬롯 지정 메뉴 + 시각 폴리시.
    /// </summary>
    public class InventoryTestWindow : EditorWindow
    {
        private const string UxmlPath = "InventoryTestWindow";
        private const string UssPath  = "InventoryTestWindow";

        // 부모 폴더 하나만 지정 — AssetDatabase.FindAssets 는 하위 폴더(Generated)까지 재귀 검색.
        // Generated 외부의 수동 자산(잔상의목걸이, 수호의파편 등 18개)도 같이 노출.
        private static readonly string[] RelicSearchFolders =
        {
            "Assets/_Project/ScriptableObjects/Relics"
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

        // CL-183: 검색 필드 + 마지막 필터 (Refresh / 자동갱신 시 보존)
        private ToolbarSearchField _searchField;
        private string _lastFilter = string.Empty;

        // 미소녀 소환 유물(듀얼 태그 [미소녀]+[속성]) 만 표시하는 토글.
        private ToolbarToggle _magicalGirlOnlyToggle;
        private bool _magicalGirlOnly;

        // CL-221: RelicTag → BuildSetData.DisplayName (한글 세트명) 매핑. 좌측 트리 그룹 헤더용.
        private Dictionary<RelicTag, string> _setDisplayNames = new();

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
            // 씬 전환 시 Player 가 새로 스폰되므로 기존 _relicInv 참조가 stale 이 됨.
            // sceneLoaded 구독 → PlayMode 중 씬 전환마다 인스턴스 재검색.
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnsubscribeInventoryEvents();
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (!EditorApplication.isPlaying) return;
            // 새 씬에 Player 가 spawn 된 직후 인스턴스 재검색 — 1프레임 정도 늦춰주면 더 안전하나
            // FindAnyObjectByType 가 이미 활성 객체를 찾으므로 즉시 호출도 동작.
            RefreshPlayerInstances();
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

            // CL-183: SearchField — 4채널 OR 매칭 (DisplayName / EffectDescription / Tags / Effects[].Type)
            _searchField = root.Q<ToolbarSearchField>("SearchField");
            if (_searchField != null)
            {
                _searchField.RegisterValueChangedCallback(evt =>
                {
                    _lastFilter = evt.newValue ?? string.Empty;
                    RebuildTreeWithFilter();
                });
            }

            _magicalGirlOnlyToggle = root.Q<ToolbarToggle>("MagicalGirlOnlyToggle");
            if (_magicalGirlOnlyToggle != null)
            {
                _magicalGirlOnlyToggle.RegisterValueChangedCallback(evt =>
                {
                    _magicalGirlOnly = evt.newValue;
                    RebuildTreeWithFilter();
                });
            }

            var refreshBtn = root.Q<Button>("RefreshButton");
            if (refreshBtn != null) refreshBtn.clicked += OnRefreshClicked;

            var clearPermanentBtn = root.Q<Button>("ClearPermanentButton");
            if (clearPermanentBtn != null) clearPermanentBtn.clicked += OnClearPermanentClicked;

            var clearConsumableBtn = root.Q<Button>("ClearConsumableButton");
            if (clearConsumableBtn != null) clearConsumableBtn.clicked += OnClearConsumableClicked;

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
            _setDisplayNames = LoadSetDisplayNames();

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
            _treeView.itemsChosen += OnTreeItemsChosen;
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

        // CL-221: 16개 BuildSetData 자산을 스캔해 RelicTag → 한글 DisplayName 매핑 빌드.
        // 자산 누락 / DisplayName 미설정이면 enum 이름 fallback 으로 그룹 헤더 표기.
        private static Dictionary<RelicTag, string> LoadSetDisplayNames()
        {
            var map = new Dictionary<RelicTag, string>();
            var guids = AssetDatabase.FindAssets("t:BuildSetData");
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var bsd = AssetDatabase.LoadAssetAtPath<BuildSetData>(path);
                if (bsd == null) continue;
                if (!map.ContainsKey(bsd.SetTag))
                    map[bsd.SetTag] = string.IsNullOrEmpty(bsd.DisplayName)
                        ? bsd.SetTag.ToString()
                        : bsd.DisplayName;
            }
            return map;
        }

        private List<TreeViewItemData<InventoryTreeNode>> BuildTreeData()
        {
            return BuildTreeData(_lastFilter);
        }

        // CL-183: 필터 적용 트리 데이터. 빈 필터 = 전체 표시. 빈 카테고리 (매칭 0) 자동 숨김.
        private List<TreeViewItemData<InventoryTreeNode>> BuildTreeData(string filter)
        {
            var permanents  = _allRelics.Where(r => !r.IsConsumable).ToList();
            var consumables = _allRelics.Where(r =>  r.IsConsumable).ToList();

            if (_magicalGirlOnly)
            {
                permanents = permanents.Where(IsMagicalGirlSpawnRelic).ToList();
                consumables = new List<RelicData>();   // 미소녀 모드에서는 소모품 숨김
            }

            if (!string.IsNullOrEmpty(filter))
            {
                var lower = filter.ToLowerInvariant();
                permanents  = permanents.Where(r => MatchesFilter(r, lower)).ToList();
                consumables = consumables.Where(r => MatchesFilter(r, lower)).ToList();
            }

            int id = 0;
            var roots = new List<TreeViewItemData<InventoryTreeNode>>();

            // CL-221: Permanent 를 RelicTag 별 16개 세트 그룹으로 분리. 듀얼 태그(Primary + Secondary)
            // 인 RelicData 는 양쪽 그룹에 중복 leaf 로 노출 (옵션 A — 세트 빌드 검증성 우선).
            // 그룹 표시 순서는 enum 정의 순서. 빈 그룹은 자동 숨김.
            foreach (RelicTag tag in System.Enum.GetValues(typeof(RelicTag)))
            {
                if (tag == RelicTag.None) continue;
                var members = permanents
                    .Where(r => r.TagPrimary == tag || r.TagSecondary == tag)
                    .ToList();
                if (members.Count == 0) continue;
                string label = _setDisplayNames.TryGetValue(tag, out var dn) ? dn : tag.ToString();
                roots.Add(BuildGroup(ref id, label, members));
            }

            if (consumables.Count > 0) roots.Add(BuildGroup(ref id, "Consumable", consumables));
            return roots;
        }

        /// <summary>
        /// 미소녀 소환/강화 유물 판정. 듀얼 태그에 [미소녀] 포함 + Effects 에 Summon/Elemental/Enhanced 보유.
        /// MagicalGirlSpawner.HandleRelicAcquired 의 spawn 조건과 동일 로직.
        /// </summary>
        private static bool IsMagicalGirlSpawnRelic(RelicData r)
        {
            if (r == null) return false;
            bool primaryIsGirl   = r.TagPrimary == RelicTag.MagicalGirl;
            bool secondaryIsGirl = r.TagSecondary == RelicTag.MagicalGirl;
            if (!primaryIsGirl && !secondaryIsGirl) return false;

            if (r.Effects == null) return false;
            foreach (var e in r.Effects)
            {
                if (e.Type == RelicEffectType.MagicalGirlSummon ||
                    e.Type == RelicEffectType.MagicalGirlElementalAttack ||
                    e.Type == RelicEffectType.MagicalGirlElementalEnhanced)
                    return true;
            }
            return false;
        }

        // CL-183: 4채널 OR 매칭 — 이름 / 효과설명 / 태그 / 효과타입.
        // legacy _effectTypeLegacy 검색 X (Effects[] 만). RelicData.Effects null/0 가드.
        private static bool MatchesFilter(RelicData r, string lower)
        {
            var name = r.DisplayName ?? r.name;
            if (!string.IsNullOrEmpty(name)
                && name.ToLowerInvariant().Contains(lower)) return true;

            if (!string.IsNullOrEmpty(r.EffectDescription)
                && r.EffectDescription.ToLowerInvariant().Contains(lower)) return true;

            if (r.TagPrimary.ToString().ToLowerInvariant().Contains(lower))   return true;
            if (r.TagSecondary.ToString().ToLowerInvariant().Contains(lower)) return true;

            if (r.Effects != null && r.Effects.Count > 0)
            {
                foreach (var e in r.Effects)
                    if (e.Type.ToString().ToLowerInvariant().Contains(lower)) return true;
            }
            return false;
        }

        // CL-183: 필터 적용 트리 재구축. SearchField 콜백 / Refresh / 후속 자동갱신 진입점.
        private void RebuildTreeWithFilter()
        {
            if (_treeView == null) return;
            _treeView.SetRootItems(BuildTreeData(_lastFilter));
            _treeView.Rebuild();
            _treeView.ExpandAll();
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
                grid.Add(BuildSlotElement(slot, i, isConsumableSlot: false));
            }
        }

        private void RefreshConsumableArea()
        {
            if (_consumeInv == null || _consumableArea == null) return;
            var slots = _consumeInv.Slots;
            int filled = slots.Count(s => s != null);
            _consumableHeader.text = $"Consumable ({filled}/{PlayerConsumableInventory.SlotCount}) — slot 1~4";

            var grid = _consumableArea.Q<VisualElement>("ConsumableGrid");
            grid.Clear();
            for (int i = 0; i < PlayerConsumableInventory.SlotCount; i++)
                grid.Add(BuildSlotElement(slots[i], i, isConsumableSlot: true));
        }

        private VisualElement BuildSlotElement(RelicData relic, int slotIndex, bool isConsumableSlot)
        {
            var slot = new VisualElement();
            slot.AddToClassList("iv-slot");

            if (isConsumableSlot)
            {
                var slotNumLabel = new Label((slotIndex + 1).ToString());
                slotNumLabel.AddToClassList("iv-slot-num");
                slot.Add(slotNumLabel);
            }

            if (relic == null)
            {
                slot.AddToClassList("iv-slot-empty");
                slot.Add(new Label("·"));
                return slot;
            }

            slot.Add(new Label(relic.DisplayName ?? relic.name));
            slot.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("Remove", _ =>
                {
                    if (isConsumableSlot)
                    {
                        if (_consumeInv == null) return;
                        _consumeInv.Remove(slotIndex);
                        OnConsumableChanged();
                    }
                    else
                    {
                        if (_relicInv == null) return;
                        _relicInv.Remove(relic);
                    }
                });
            }));
            return slot;
        }

        // ── CL-176 동작: 더블클릭 / Clear ─────────────────

        private void OnTreeItemsChosen(IEnumerable<object> items)
        {
            var node = items.OfType<InventoryTreeNode>().FirstOrDefault();
            if (node?.Relic == null) return;
            AddRelicToCorrectInventory(node.Relic);
        }

        private void AddRelicToCorrectInventory(RelicData relic)
        {
            if (_relicInv == null || _consumeInv == null)
            {
                Debug.LogWarning("[CL-176] Player 인스턴스 없음 — Refresh 후 재시도");
                return;
            }

            if (relic.IsConsumable)
            {
                if (relic.IsInstantUse)
                {
                    Debug.LogWarning(
                        $"[InventoryTest] '{relic.DisplayName}' 은 IsInstantUse=true. " +
                        "게임 보상 흐름에선 즉시 효과 후 사라지지만, 본 도구는 슬롯 추가만 합니다.");
                }
                ShowConsumableSlotMenu(relic);
            }
            else
            {
                _relicInv.TryAdd(relic);
            }
        }

        private void OnClearPermanentClicked()
        {
            if (_relicInv == null) return;
            int filled = _relicInv.OwnedRelics.Count(r => r != null);
            if (!EditorUtility.DisplayDialog(
                    "Clear Permanent Inventory",
                    $"Permanent 인벤토리 {filled}개를 모두 비웁니다.\n계속할까요?",
                    "Clear", "Cancel"))
                return;
            _relicInv.Clear();
        }

        private void OnClearConsumableClicked()
        {
            if (_consumeInv == null) return;
            int filled = _consumeInv.Slots.Count(s => s != null);
            if (!EditorUtility.DisplayDialog(
                    "Clear Consumable Slots",
                    $"Consumable 슬롯 {filled}개를 모두 비웁니다.\n계속할까요?",
                    "Clear", "Cancel"))
                return;
            _consumeInv.Clear();
            OnConsumableChanged();
        }

        private void OnConsumableChanged() => RefreshConsumableArea();

        // ── CL-177 Consumable 슬롯 지정 메뉴 ───────────────

        private void ShowConsumableSlotMenu(RelicData consumable)
        {
            if (_consumeInv == null) return;

            var slots = _consumeInv.Slots;
            int emptyCount = slots.Count(s => s == null);

            if (emptyCount == 0)
            {
                EditorUtility.DisplayDialog(
                    "Consumable Slots Full",
                    "Consumable 슬롯 4개가 모두 차 있습니다.\n먼저 우클릭 → Remove 로 비워주세요.",
                    "OK");
                return;
            }

            var menu = new GenericMenu();
            for (int i = 0; i < PlayerConsumableInventory.SlotCount; i++)
            {
                int slotIdx = i;
                var existing = slots[i];
                if (existing == null)
                {
                    menu.AddItem(
                        new GUIContent($"Slot {i + 1}: (empty)"),
                        false,
                        () => OnConsumableSlotPicked(slotIdx, consumable));
                }
                else
                {
                    menu.AddDisabledItem(
                        new GUIContent($"Slot {i + 1}: {existing.DisplayName ?? existing.name}"));
                }
            }
            menu.ShowAsContext();
        }

        private void OnConsumableSlotPicked(int slot, RelicData consumable)
        {
            if (_consumeInv == null) return;
            bool ok = _consumeInv.TryAddAt(slot, consumable);
            if (ok) OnConsumableChanged();
            else Debug.LogWarning($"[InventoryTest] Slot {slot + 1} 배치 실패");
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
            // Refresh 버튼은 좌측 RelicData 트리 + 우측 인벤토리 뷰 모두 재구축한다.
            // 씬 전환 직후 stale 참조 복구 케이스도 같은 버튼으로 처리하도록 Player 인스턴스 재검색 포함.
            if (EditorApplication.isPlaying) RefreshPlayerInstances();
            _allRelics = LoadAllRelics();
            _setDisplayNames = LoadSetDisplayNames();
            // CL-183: _lastFilter 보존 — Refresh 후에도 검색 결과 유지
            RebuildTreeWithFilter();
            RefreshInventoryView();
        }
    }
}
