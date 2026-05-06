# CL-175 — 좌측 RelicData 트리 + 우측 인벤토리 슬롯 표시

## Context

CL-174 셸 위에 **좌측 LeftPanel 에 RelicData 트리 + 우측 RightPanel 에 Player 인벤토리 슬롯 시각화** 추가. 디자이너/테스터가 어떤 RelicData 가 존재하는지 + 현재 Player 가 무엇을 보유 중인지 한눈에 파악.

**2점 P1, CL-174 의존.**

### 본 CL 책임 범위

| 포함 | 제외 |
|---|---|
| 좌측 TreeView — RelicData 그룹핑 (Permanent / Consumable) | 더블클릭 추가 / 우클릭 제거 / Clear (→ CL-176) |
| 우측 PermanentArea — OwnedRelics 슬롯 그리드 | Consumable 4슬롯 **별도 영역** + 슬롯 지정 (→ CL-177 — 본 CL 은 단순 wrap) |
| 우측 ConsumableArea — Slots[4] 표시 | 검색 (→ CL-183 Epic V) |
| 이벤트 구독 (OnRelicAcquired/Removed/Cleared/MaxSlotsChanged) → 자동 갱신 | 드래그앤드롭 (→ CL-186 Epic V) |
| Refresh 버튼 (PlayerConsumableInventory 변경 수동 감지용) | Quick Add 프리셋 (→ CL-187 Epic V) |
| 좌측 leaf 선택 시 EditorGUIUtility.PingObject (Inspector 노출) | |

---

## 현황 (탐색 결과)

### 1.1 CL-174 셸 (plan 기준 — 미머지)

[cl174_plan.md](cl174_plan.md) 정의:
- `InventoryTestWindow` (namespace `LostMemory.Editor.InventoryTest`)
- 멤버: `_relicInv` (PlayerRelicInventory) / `_consumeInv` (PlayerConsumableInventory) / `_bodyContainer` / `_modeHint`
- UXML element: `BodyContainer` / `LeftPanel` / `RightPanel` / `ModeHint`
- `RefreshPlayerInstances()` — Play 모드 진입 시 자동 호출
- USS prefix: `.iv-`

→ **CL-174 머지 선결**. 본 plan 은 CL-174 의 셸 위에 코드/UXML/USS 추가.

### 1.2 RelicData SO ([RelicData.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Relics/RelicData.cs))

```csharp
public class RelicData : ScriptableObject
{
    public string      DisplayName       => _displayName;
    public bool        IsConsumable      => _isConsumable;   // ★ 그룹핑 기준
    public bool        IsInstantUse      => _isInstantUse;
    public RelicRarity Rarity            => _rarity;
    public Sprite      Icon              => _icon;
    public RelicTag    TagPrimary        => _tagPrimary;
    public RelicTag    TagSecondary      => _tagSecondary;
    public Vector2Int  Size              => _size;
    public string      EffectDescription => _effectDescription;
    public IReadOnlyList<EffectEntry> Effects => _effects;
}
```

> CreateAssetMenu: `LostMemory/Relic Data`. Generated asset 폴더 = `Assets/_Project/ScriptableObjects/Relics/Generated` ([RelicCategoryProvider.cs:12](../../LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/Providers/RelicCategoryProvider.cs:12) 참조).

### 1.3 PlayerRelicInventory ([PlayerRelicInventory.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Relics/PlayerRelicInventory.cs))

| 멤버 | 시그니처 | 비고 |
|---|---|---|
| `OwnedRelics` | `IReadOnlyList<RelicData>` | 슬롯 위치 유지 (제거 시 null 로 교체, line 107) |
| `MaxSlots` | int (기본 25 + 보너스) | line 28. CL-146 |
| `MaxSlotsChanged` | event Action<int> | UI sync 용. line 31 |
| `OnRelicAcquired` | event Action<RelicData> | line 34 |
| `OnRelicRemoved` | event Action<RelicData> | line 37 |
| `OnCleared` | event Action | line 40 |
| `TryAdd(RelicData)` | bool | **소모품은 false 반환 → PlayerConsumableInventory 로 라우팅** (line 68-73) |

### 1.4 PlayerConsumableInventory ([PlayerConsumableInventory.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Relics/PlayerConsumableInventory.cs))

| 멤버 | 비고 |
|---|---|
| `SlotCount` const = 4 | line 13 |
| `Slots` IReadOnlyList<RelicData> | null = 빈 칸 |
| `TryAdd(RelicData)` | 첫 빈 칸에 추가 (line 24) |
| `Remove(int slotIndex)` | |
| **이벤트 없음** | ★ 자동 갱신 불가 — Refresh 버튼 필요 |

### 1.5 BalanceEditor 의 TreeView 사용 패턴 (모방 대상)

[BalanceEditorWindow.cs:351-394](../../LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/BalanceEditorWindow.cs:351):
- `TreeView` + `makeItem = () => new Label()` + `bindItem` 으로 동적 라벨
- `TreeViewItemData<TreeNode>` 로 그룹/leaf 구성
- `selectionChanged` 이벤트 → `EditorGUIUtility.PingObject(so)` 패턴

→ 동일 패턴 적용. `TreeNode` 자체 정의는 InventoryTest 안에 별도 (BalanceEditor 의 TreeNode 와 충돌 회피 + asmdef 분리 X 정책상 같은 어셈블리지만 namespace 분리 OK).

---

## 설계 결정

### 1. TreeView vs ListView

| 안 | 장점 | 단점 |
|---|---|---|
| (a) TreeView (그룹 + leaf) | 그룹핑 (Permanent/Consumable), BalanceEditor 일관 | 코드 약간 더 |
| (b) ListView (단일 평면) | 단순 | 그룹핑 X |

→ **(a) TreeView 채택**. Permanent/Consumable 그룹 노드 + Leaf RelicData 노드. CL-183 (검색 추가) 시 트리 필터 로직 BalanceEditor 의 FilterTree 패턴 재사용 가능.

### 2. RelicData 검색 경로

| 안 | 결정 |
|---|---|
| 전체 검색 (`AssetDatabase.FindAssets("t:RelicData")`) | ❌ |
| **SearchFolders 명시** (`Assets/_Project/ScriptableObjects/Relics/Generated`) | ✅ |

→ RelicCategoryProvider (CL-163) 패턴 따름. 디자이너가 Generated 폴더 안에 작성하는 컨벤션과 일관.

### 3. RelicData 그룹핑 기준

```csharp
groups:
  Permanent  := relics where IsConsumable == false
  Consumable := relics where IsConsumable == true
```

→ Rarity / TagPrimary 별 추가 그룹핑은 본 CL 범위 외. (CL-183 검색 / Rarity 필터로 대체 가능)

### 4. Leaf 라벨 포맷

| 옵션 | 예 |
|---|---|
| (a) DisplayName 만 | `Bertha's Crown` |
| (b) DisplayName + Rarity | `Bertha's Crown (Legendary)` |
| (c) Icon + DisplayName | (sprite 미리보기 + 라벨) |

→ **(a) DisplayName 만**. BalanceEditor 와 일관. 폴리시 ticket (선택) 에서 (b)/(c) 추가 여지.

### 5. 우측 RightPanel 분할

```
RightPanel
├─ PermanentArea  (header "Permanent (N/MaxSlots)" + 슬롯 그리드)
└─ ConsumableArea (header "Consumable (N/4)" + 슬롯 그리드)
```

UXML 에 명시적 placeholder 추가 (CL-174 셸 의 RightPanel 안에 두 자식). 라벨 + 슬롯 영역.

### 6. 슬롯 시각화

| 항목 | 결정 |
|---|---|
| 그리드 정렬 | flex-wrap 으로 자연 줄바꿈 (5×5 강제 X). 폴리시는 후속 |
| 빈 슬롯 | 회색 placeholder VisualElement (`.iv-slot-empty` 클래스) |
| 채워진 슬롯 | DisplayName 라벨 (Icon 폴리시 후속) |
| 슬롯 크기 | 60×60 px (시각 가독성 + 다수 슬롯 표시) |
| MaxSlots 표시 | PermanentArea 헤더에 `(N/{MaxSlots})` |

### 7. 이벤트 구독 — 자동 갱신

| 출처 | 이벤트 | 본 CL 동작 |
|---|---|---|
| `_relicInv.OnRelicAcquired` | RelicData 획득 시 | PermanentArea 재그림 |
| `_relicInv.OnRelicRemoved` | RelicData 제거 시 | PermanentArea 재그림 |
| `_relicInv.OnCleared` | Clear() 호출 시 | PermanentArea 재그림 |
| `_relicInv.MaxSlotsChanged` | MaxSlots 변경 시 | PermanentArea 헤더 + 빈 슬롯 갱신 |
| `_consumeInv` | (이벤트 없음) | **Refresh 버튼**으로 수동 갱신 |

★ 본 CL 시점 = 동작 (TryAdd/Remove/Clear) 호출 X — Editor 외부 (디버그 ContextMenu, 게임 보상 흐름) 에서만 변경됨. 그래도 미리 구독해 두면 CL-176 추가 작업 0.

### 8. Refresh 버튼

PlayerConsumableInventory 가 이벤트 없으므로 Toolbar 에 Refresh 버튼 추가. 좌측 트리 / 우측 슬롯 모두 다시 그림. CL-174 셸의 Toolbar 활용.

### 9. 모드 전환 시 처리

```
EnteredEditMode → UnsubscribeInventoryEvents() (이미 구독 해제됨일 수도, 안전 호출)
EnteredPlayMode → RefreshPlayerInstances() → 슬롯 재그림 + 이벤트 재구독
```

CL-174 의 `UpdateModeView()` / `RefreshPlayerInstances()` 확장.

### 10. 좌측 트리 selection 동작

본 CL 책임 외 (더블클릭 = CL-176). 다만 **단일 클릭 시 `EditorGUIUtility.PingObject(relicData)`** — Project 창에서 RelicData asset 위치 표시. 디자이너 편의.

### 11. namespace / 파일 위치

| 파일 | 변경 |
|---|---|
| `InventoryTestWindow.cs` | 멤버 + 메서드 추가 (CL-174 베이스 위에) |
| `InventoryTestWindow.uxml` | RightPanel 안에 PermanentArea / ConsumableArea + Toolbar Refresh 버튼 |
| `InventoryTestWindow.uss` | `.iv-slot` / `.iv-slot-empty` / `.iv-slot-grid` / `.iv-area-header` 추가 |
| `InventoryTreeNode.cs` (신규) | TreeView 노드 데이터 클래스 (BalanceEditor 의 TreeNode 모방) |

---

## 수정/신규 파일 — 코드

### A. `InventoryTestWindow.cs` 추가 멤버 / 메서드

```csharp
using System.Collections.Generic;
using System.Linq;
using LostMemory.Relics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LostMemory.Editor.InventoryTest
{
    public class InventoryTestWindow : EditorWindow
    {
        // CL-174 (기존)
        private PlayerRelicInventory _relicInv;
        private PlayerConsumableInventory _consumeInv;
        private VisualElement _bodyContainer;
        private Label _modeHint;

        // CL-175 (신규)
        private static readonly string[] RelicSearchFolders =
        {
            "Assets/_Project/ScriptableObjects/Relics/Generated"
        };
        private TreeView _treeView;
        private VisualElement _permanentArea;
        private VisualElement _consumableArea;
        private Label _permanentHeader;
        private Label _consumableHeader;
        private List<RelicData> _allRelics = new();

        // ... (CL-174 OnEnable/OnDisable 그대로)

        public void CreateGUI()
        {
            // ... (CL-174 셸 로드)

            _bodyContainer = root.Q<VisualElement>("BodyContainer");
            _modeHint      = root.Q<Label>("ModeHint");

            var leftPanel  = root.Q<VisualElement>("LeftPanel");
            var rightPanel = root.Q<VisualElement>("RightPanel");
            _permanentArea  = root.Q<VisualElement>("PermanentArea");
            _consumableArea = root.Q<VisualElement>("ConsumableArea");
            _permanentHeader  = root.Q<Label>("PermanentHeader");
            _consumableHeader = root.Q<Label>("ConsumableHeader");

            var refreshBtn = root.Q<Button>("RefreshButton");
            if (refreshBtn != null) refreshBtn.clicked += OnRefreshClicked;

            BuildLeftTree(leftPanel);
            UpdateModeView();
        }

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
            var leaves = sos.Select(r => new TreeViewItemData<InventoryTreeNode>(
                id++,
                new InventoryTreeNode { Relic = r, DisplayLabel = r.DisplayName ?? r.name }
            )).ToList();

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

        // ── 우측 슬롯 시각화 ────────────────────────────

        private void RefreshInventoryView()
        {
            if (_relicInv == null || _consumeInv == null) return;
            RefreshPermanentArea();
            RefreshConsumableArea();
        }

        private void RefreshPermanentArea()
        {
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

        // ── 이벤트 구독 ─────────────────────────────────

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

        // ── CL-174 RefreshPlayerInstances 확장 ─────────

        private void RefreshPlayerInstances()
        {
            UnsubscribeInventoryEvents();              // 이전 구독 해제

            _relicInv   = Object.FindAnyObjectByType<PlayerRelicInventory>();
            _consumeInv = Object.FindAnyObjectByType<PlayerConsumableInventory>();

            SubscribeInventoryEvents();                // 새 인스턴스에 구독
            RefreshInventoryView();
        }

        private void OnRefreshClicked()
        {
            // 좌측 트리 + 우측 슬롯 둘 다 강제 재로드
            _allRelics = LoadAllRelics();
            _treeView.SetRootItems(BuildTreeData());
            _treeView.Rebuild();
            _treeView.ExpandAll();
            RefreshInventoryView();
        }
    }
}
```

### B. `InventoryTreeNode.cs` (신규)

```csharp
using LostMemory.Relics;

namespace LostMemory.Editor.InventoryTest
{
    /// <summary>TreeView item data. Group 노드는 Relic == null.</summary>
    public class InventoryTreeNode
    {
        public string DisplayLabel;
        public RelicData Relic;   // null = group 노드
    }
}
```

### C. `InventoryTestWindow.uxml` (CL-174 위에 추가)

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements">
    <ui:VisualElement class="iv-root">
        <uie:Toolbar class="iv-toolbar">
            <ui:Label text="Inventory Test" class="iv-title" />
            <ui:VisualElement class="iv-toolbar-spacer" />
            <uie:ToolbarButton name="RefreshButton" text="Refresh" class="iv-refresh-btn" />
        </uie:Toolbar>

        <ui:Label name="ModeHint" text="Play 모드 진입 시 인벤토리 테스트가 활성화됩니다." class="iv-mode-hint" />

        <ui:VisualElement name="BodyContainer" class="iv-body">
            <ui:VisualElement name="LeftPanel" class="iv-left-panel" />

            <ui:VisualElement name="RightPanel" class="iv-right-panel">
                <ui:VisualElement name="PermanentArea" class="iv-area">
                    <ui:Label name="PermanentHeader" text="Permanent (0/0)" class="iv-area-header" />
                    <ui:VisualElement name="PermanentGrid" class="iv-slot-grid" />
                </ui:VisualElement>
                <ui:VisualElement name="ConsumableArea" class="iv-area">
                    <ui:Label name="ConsumableHeader" text="Consumable (0/4)" class="iv-area-header" />
                    <ui:VisualElement name="ConsumableGrid" class="iv-slot-grid" />
                </ui:VisualElement>
            </ui:VisualElement>
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

### D. `InventoryTestWindow.uss` 추가

```css
.iv-toolbar-spacer { flex-grow: 1; flex-shrink: 1; }
.iv-refresh-btn { flex-shrink: 0; flex-grow: 0; width: 80px; }

.iv-area {
    flex-shrink: 0;
    margin-bottom: 12px;
}
.iv-area-header {
    color: rgb(220, 220, 220);
    font-size: 12px;
    -unity-font-style: bold;
    margin-bottom: 4px;
}
.iv-slot-grid {
    flex-direction: row;
    flex-wrap: wrap;
}
.iv-slot {
    width: 60px;
    height: 60px;
    margin: 2px;
    border-width: 1px;
    border-color: rgb(80, 80, 80);
    background-color: rgb(64, 64, 64);
    align-items: center;
    justify-content: center;
    overflow: hidden;
}
.iv-slot-empty {
    background-color: rgb(48, 48, 48);
    border-color: rgb(60, 60, 60);
}
.iv-slot Label {
    font-size: 9px;
    -unity-text-align: middle-center;
    white-space: normal;
}
```

---

## 위험 / 알려진 상황

| # | 위험 | 대응 |
|---|---|---|
| 1 | **PlayerConsumableInventory 이벤트 없음** | Refresh 버튼 (§설계 8). 게임 보상 흐름이 추가 시 자동 미반영 → 디자이너 안내 |
| 2 | **OwnedRelics 의 null 슬롯** — Remove 시 null 로 교체 (PlayerRelicInventory.cs:107) | `BuildSlotElement(null)` = 빈 슬롯 처리 (§설계 6) |
| 3 | **MaxSlots 변경 (CL-146 행운 보너스)** | MaxSlotsChanged 구독 → 헤더 + 빈 슬롯 수 자동 갱신 |
| 4 | **트리에 RelicData 0개** (Generated 폴더 비었을 경우) | 빈 그룹 표시 ("Permanent (0)" / "Consumable (0)"). 정상 |
| 5 | **Player 인스턴스 미존재** | _relicInv/_consumeInv null. RefreshInventoryView 첫 줄 가드. UI 빈 상태로 표시 (선택 — 안내 라벨은 폴리시 후속) |
| 6 | **TreeView 의 Generic 타입 명시** — `GetItemDataForIndex<InventoryTreeNode>(int)` 가 InventoryTreeNode 타입 정확 매칭 필요 | 런타임 타입 안전. 컴파일 시 보장 |
| 7 | **이벤트 구독 leak** — Window 닫기 / Play 종료 / 새 Player 등장 시 | OnDisable / RefreshPlayerInstances 첫 줄 / OnPlayModeChanged 시 Unsubscribe 호출. 안전 |
| 8 | **Editor 도메인 리로드** (CS 파일 변경 → 컴파일) — _treeView/_relicInv 등 stale | OnEnable/CreateGUI 가 재호출. RefreshPlayerInstances 가 인스턴스 재검색 |

---

## 검증 시나리오

### CS 빌드 검증 (Claude)

```
1. InventoryTestWindow.cs / InventoryTreeNode.cs 컴파일 오류 없음
2. UXML/USS Resources.Load 경로 그대로 (CL-174 와 동일)
3. PlayerRelicInventory / RelicData / PlayerConsumableInventory 무수정 확인
```

### Unity Editor 검증 (사용자)

```
4. Tools > LostMemory > Inventory Test Window 열기 (CL-174 동작)
5. Edit 모드 — 안내 문구 표시 + BodyContainer 비활성 (CL-174 동작 그대로)
6. Play 모드 진입 — 좌측 트리 표시:
   - "Permanent (N)" 그룹 — IsConsumable=false 인 RelicData 들
   - "Consumable (M)" 그룹 — IsConsumable=true 인 RelicData 들
   - 자동 펼침 (ExpandAll)
   - leaf = DisplayName
7. Play 모드 — 우측 슬롯 표시:
   - "Permanent (filled/MaxSlots)" 헤더 + 25 슬롯 그리드 (전부 빈 칸)
   - "Consumable (filled/4)" 헤더 + 4 슬롯
8. PlayerRelicInventory Inspector 의 ContextMenu "Debug — Add all assigned relics" 실행
   → 우측 PermanentArea 자동 갱신 (이벤트 구독 동작 검증)
9. PlayerRelicInventory Inspector 의 ContextMenu "Debug — Clear inventory"
   → PermanentArea 전부 빈 슬롯으로 갱신
10. PlayerConsumableInventory 에 외부에서 TryAdd 호출 (예: ContextMenu 추가 / 보상 흐름)
    → 자동 갱신 X (이벤트 없음). Refresh 버튼 클릭 → ConsumableArea 갱신 확인
11. 좌측 트리 leaf 클릭 → Project 창에서 RelicData asset 강조 (PingObject)
12. Play 종료 → 안내 문구 재표시, BodyContainer 비활성. 이벤트 구독 해제 (메모리 leak 없음)
13. 새 Generated/RelicData 추가 → Refresh 버튼 클릭 → 트리에 즉시 반영
```

---

## 핵심 파일

### 신규 (Claude 작성)

| 경로 | 내용 |
|---|---|
| `LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/InventoryTreeNode.cs` | TreeView 노드 데이터 클래스 |

### 수정 (Claude 작성, CL-174 위에)

| 경로 | 변경 |
|---|---|
| `LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/InventoryTestWindow.cs` | 트리 / 슬롯 / 이벤트 구독 메서드 추가 + RefreshPlayerInstances 확장 |
| `.../Resources/InventoryTestWindow.uxml` | RightPanel 안에 PermanentArea/ConsumableArea + Toolbar Refresh 버튼 |
| `.../Resources/InventoryTestWindow.uss` | `.iv-slot` / `.iv-slot-empty` / `.iv-slot-grid` / `.iv-area` / `.iv-area-header` / `.iv-toolbar-spacer` / `.iv-refresh-btn` 추가 |

### 무수정 (참고)

| 파일 | 이유 |
|---|---|
| `Runtime/Relics/RelicData.cs` | 호출만 (DisplayName/IsConsumable/...) |
| `Runtime/Relics/PlayerRelicInventory.cs` | 호출 + 이벤트 구독만 |
| `Runtime/Relics/PlayerConsumableInventory.cs` | 호출만 |

> 다른 사람 코드 침범 없음. 모든 변경은 CL-174 (본인 작성) 위에 추가 + 신규 파일 1개.

---

## 후속 ticket 영향

| Ticket | 관계 |
|---|---|
| **CL-176** 더블클릭 / 우클릭 / Clear | 본 CL 의 _treeView selectionChanged 패턴 + Refresh 흐름 그대로 활용. 더블클릭 시 `_relicInv.TryAdd(node.Relic)` (Permanent) / `_consumeInv.TryAdd` (Consumable). 우클릭 메뉴 — VisualElement.AddManipulator(ContextualMenuManipulator) |
| **CL-177** Consumable 4슬롯 별도 영역 + 슬롯 지정 | 본 CL 의 ConsumableArea 가 이미 별도 영역. CL-177 = 슬롯 지정 (1~4 번 클릭으로 지정) 추가 |
| **CL-183** Epic V 검색 (이름/태그/효과) | BalanceEditor 의 FilterTree 패턴 (BalanceEditorWindow.cs:506) 참고. ToolbarSearchField 추가 + BuildTreeData → FilterTreeData 적용 |
| **CL-184** Epic V 자동갱신 강화 | 본 CL 이미 OnRelicAcquired/Removed/Cleared/MaxSlotsChanged 구독. CL-184 추가 작업 = OnRelicRemoved 의 미세 처리, drag/drop 시점 갱신 등 |

---

## 작업 순서

| 순서 | 담당 | 내용 |
|---|---|---|
| 1 | (선결) | **CL-174 머지 완료** 확인 (셸 + UXML/USS) |
| 2 | Claude | `InventoryTreeNode.cs` 작성 |
| 3 | Claude | `InventoryTestWindow.uxml` 갱신 (Toolbar Refresh + RightPanel placeholder) |
| 4 | Claude | `InventoryTestWindow.uss` 갱신 (.iv-slot 계열 추가) |
| 5 | Claude | `InventoryTestWindow.cs` 갱신 (BuildLeftTree / RefreshInventoryView / 이벤트 구독 / OnRefreshClicked) |
| 6 | Claude | 컴파일 오류 없음 확인 (Grep / Read) |
| 7 | 사용자 | Unity Editor 에서 §검증 시나리오 4-13 실행 |
| 8 | 사용자 | MR 생성 |
| 9 | — | CL-176 (더블클릭/우클릭/Clear) 진입 |

---

## 예상 시간

| 단계 | 시간 |
|---|---|
| InventoryTreeNode.cs 작성 | 5분 |
| UXML/USS 갱신 | 15분 |
| InventoryTestWindow.cs 메서드 추가 (트리/슬롯/이벤트) | 35분 |
| 컴파일 / 검증 (Claude) | 10분 |
| Unity Editor 검증 (사용자) | 15분 |
| **합계** | **약 80분** |

→ 2점 ticket (CL-174 와 동일 점수). UXML/USS + CS 모두 손대고 이벤트 구독 까지라 CL-174 셸 (~65분) 보다 약간 더 소요. cl172_plan 의 1점 ticket 패턴 (~35분) 의 2배.

---

## 메모

- 본 CL 완료 후 디자이너에게 **PlayerConsumableInventory 변경 시 Refresh 버튼 클릭 필요** 안내 (이벤트 없음 — 게임 흐름 외 변경은 자동 미반영)
- master plan epic_uv §11 의 "(CL-175~177) InventoryTestWindow 트리/슬롯/동작 코드 추가" 일관 (CL-175 = 표시, CL-176 = 동작, CL-177 = Consumable 슬롯 지정)
- 본 plan 의 우측 ConsumableArea 가 CL-177 의 "별도 영역" 일부 미리 구현됨. CL-177 의 추가 작업은 슬롯 지정 (몇 번 슬롯에 넣을지) UI
