# CL-186 — 좌측→우측 드래그 앤 드롭 추가 (UI Toolkit Manipulator)

## Context

Epic V (인벤토리 테스트 도구) 의 **UX 강화 ticket** — 좌측 RelicData 트리 leaf 를 우측 슬롯으로 드래그해서 추가. 더블클릭 (CL-176 머지) 과 **동등 효과** 지만 디자이너가 빠르게 슬롯 위치 지정 (Consumable 의 경우 어떤 슬롯에 넣을지 직접 선택) 가능.

master plan epic_uv §2 ticket 시트 line 72: "**좌측 → 우측 드래그 앤 드롭** — UI Toolkit Manipulator 로 좌측 트리 → 우측 슬롯 드래그. 더블클릭과 동등 효과 — 의존 CL-176 — 2점 P2".

CL-176 (`41ba59d73`) + CL-177 (`db82245f0`) 머지 완료 → 진입 가능. CL-184 와 달리 **신규 가치 명확** (드래그 자체가 새 UX, CL-176 의 더블클릭은 슬롯 자동 배정만 지원).

### 본 CL 책임 범위

| 포함 | 제외 |
|---|---|
| TreeView leaf 에서 drag 시작 (`PointerDownEvent` + `DragAndDrop.PrepareStartDrag`) | TreeView 의 children reorder (트리 내부 드래그) |
| PermanentArea 슬롯 / 그리드 drop receiver (`TryAdd`) | 우측 → 좌측 (슬롯 → 트리) 드래그 — 제거는 우클릭 메뉴 (CL-176) 그대로 |
| Consumable 개별 슬롯 drop receiver (`TryAddAt(N, consumable)`) — 슬롯 직접 지정 | Consumable 슬롯 간 swap / 이동 (별도 ticket 후보) |
| 드래그 시각 피드백 — USS 클래스 토글 (`.iv-slot-drop-target` / `.iv-slot-drop-invalid`) | OS 드래그 (외부 파일 / 다른 창 → 본 창) |
| Permanent ↔ Consumable 분기 검증 (잘못된 슬롯 drop 거부) | 드래그 중 ghost 이미지 (Editor 기본 hover 표시만) |
| 더블클릭 (`OnTreeItemsChosen`) 흐름 무수정 — drag 와 직교 | CL-185 자동 배치 정책 (별도 결정 ticket) |

---

## 현황 (탐색)

### 1.1 기존 InventoryTestWindow 구조 (CL-174~177 위)

[InventoryTestWindow.cs](../../client/LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/InventoryTestWindow.cs) 의 핵심 진입점:

| 멤버 / 메서드 | line | 역할 |
|---|---|---|
| `BuildLeftTree(leftPanel)` | 129-153 | TreeView 생성 — `makeItem = () => new Label()` / `bindItem` 으로 `InventoryTreeNode.DisplayLabel` 표시 |
| `_treeView.itemsChosen += OnTreeItemsChosen` | 146 | 더블클릭 → `AddRelicToCorrectInventory` (CL-176 머지) |
| `OnTreeSelectionChanged` | 197-202 | 선택 변경 → `EditorGUIUtility.PingObject` |
| `BuildSlotElement(relic, slotIndex, isConsumableSlot)` | 244-274 | 우측 슬롯 1개 생성 — 빈 슬롯 (`.iv-slot-empty`) / 채워진 슬롯 (Label + ContextualMenuManipulator) |
| `RefreshPermanentArea` | 213-229 | PermanentGrid 25칸 재생성 |
| `RefreshConsumableArea` | 231-242 | ConsumableGrid 4칸 재생성 |
| `AddRelicToCorrectInventory(relic)` | 285-308 | Permanent → `TryAdd(relic)` / Consumable → `TryAdd(relic)` (자동 첫 빈) — 본 CL drag 가 이 흐름의 일부 재사용 |

→ **drag 시작 진입점** = `BuildLeftTree` 의 `makeItem` / `bindItem` 에 `PointerDownEvent` callback 추가.
→ **drop receiver 진입점** = `BuildSlotElement` (개별 슬롯 단위) 또는 `PermanentGrid` / `ConsumableGrid` (영역 단위).

### 1.2 PlayerRelicInventory / PlayerConsumableInventory API (drop 동작)

| API | 위치 | 본 CL 사용 |
|---|---|---|
| `PlayerRelicInventory.TryAdd(RelicData)` | `Runtime/Relics/PlayerRelicInventory.cs` | Permanent 영역 drop → 자동 첫 빈 슬롯 |
| `PlayerConsumableInventory.TryAdd(RelicData)` | `Runtime/Relics/PlayerConsumableInventory.cs` | (사용 X — drag 는 슬롯 지정이 가치) |
| `PlayerConsumableInventory.TryAddAt(int slot, RelicData)` | `Runtime/Relics/PlayerConsumableInventory.cs` | **Consumable 개별 슬롯 N drop** — drag 의 핵심 가치 (수동 슬롯 지정) |

→ Permanent 는 슬롯 인덱스 자유 (인벤토리가 자동 배치) → 그리드 영역 단위 drop 로 충분.
→ Consumable 은 슬롯 인덱스 의미 있음 (1~4번) → 개별 슬롯 단위 drop.

### 1.3 UI Toolkit 드래그앤드롭 표준 패턴

본 프로젝트의 EditorWindow 코드베이스 검색 결과 — **드래그앤드롭 코드 0** (BalanceEditor / InventoryTestWindow / 기타 Editor 도구). UI Toolkit 표준 채택 필요.

표준 패턴 (UnityEditor + UIElements):

```csharp
// (a) Drag initiator (트리 leaf)
element.RegisterCallback<PointerDownEvent>(evt =>
{
    DragAndDrop.PrepareStartDrag();
    DragAndDrop.SetGenericData("InventoryTest_RelicData", relic);
    DragAndDrop.objectReferences = new Object[] { relic };  // PingObject 호환
    DragAndDrop.StartDrag("Drag RelicData");
});

// (b) Drop receiver (슬롯 또는 그리드)
target.RegisterCallback<DragEnterEvent>(evt => {
    var relic = DragAndDrop.GetGenericData("InventoryTest_RelicData") as RelicData;
    if (CanAccept(relic)) target.AddToClassList("iv-slot-drop-target");
    else                  target.AddToClassList("iv-slot-drop-invalid");
});
target.RegisterCallback<DragUpdatedEvent>(evt => {
    var relic = DragAndDrop.GetGenericData(...);
    DragAndDrop.visualMode = CanAccept(relic) ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
});
target.RegisterCallback<DragLeaveEvent>(evt => {
    target.RemoveFromClassList("iv-slot-drop-target");
    target.RemoveFromClassList("iv-slot-drop-invalid");
});
target.RegisterCallback<DragPerformEvent>(evt => {
    var relic = DragAndDrop.GetGenericData(...);
    if (CanAccept(relic)) {
        DragAndDrop.AcceptDrag();
        ApplyDrop(relic, slotIndex, isConsumableSlot);
    }
    target.RemoveFromClassList("iv-slot-drop-target");
    target.RemoveFromClassList("iv-slot-drop-invalid");
});
```

→ Editor 의 `UnityEditor.DragAndDrop` API + UI Toolkit `DragEnter / Updated / Leave / Perform` 이벤트 결합.

### 1.4 본 CL 에서 손대는 코드의 작성자

| 파일 | 작성자 |
|---|---|
| `InventoryTestWindow.cs` | **김회인 본인** (CL-174 / CL-175 / CL-176 / CL-177 모두 본인) — 메서드 추가 + makeItem/BuildSlotElement 시그니처 무변경 |
| `InventoryTestWindow.uxml` | 본인 — 무수정 |
| `InventoryTestWindow.uss` | 본인 — `.iv-slot-drop-*` 셀렉터 추가 |
| `RelicData.cs` / `PlayerRelicInventory.cs` / `PlayerConsumableInventory.cs` | 본인 — 무수정, 호출만 |

→ 협업 0 / 침범 0.

---

## 설계 결정

### 1. 드래그 데이터 키 = `"InventoryTest_RelicData"`

`DragAndDrop.SetGenericData` 의 key 가 globally 유일해야 함. 본 CL 고유 키. 다른 Editor 창 / 외부 패키지와 충돌 회피.

### 2. Drop receiver 단위 — Permanent: 그리드, Consumable: 개별 슬롯

| 영역 | drop receiver | 동작 |
|---|---|---|
| **PermanentGrid** | `_permanentArea.Q<VisualElement>("PermanentGrid")` 1개 | `_relicInv.TryAdd(relic)` (인벤토리 자동 첫 빈) |
| **Consumable 슬롯 i (i=0~3)** | `BuildSlotElement(slot, i, isConsumableSlot=true)` 의 결과 element 4개 | `_consumeInv.TryAddAt(i, relic)` (해당 슬롯 지정) |

근거:
- Permanent 는 슬롯 인덱스 자유 → 그리드 1개 drop receiver 로 충분 (시각 피드백도 그리드 전체 하이라이트)
- Consumable 은 슬롯 인덱스 가치 → 슬롯 4개 각각 drop receiver (디자이너가 정확한 슬롯 선택)

### 3. CanAccept 정책 (잘못된 drop 거부)

| Drop receiver | RelicData.IsConsumable=false (Permanent) | RelicData.IsConsumable=true (Consumable) |
|---|---|---|
| **PermanentGrid** | ✅ accept | ❌ reject (`Rejected` visualMode + `.iv-slot-drop-invalid` 시각) |
| **Consumable 슬롯** | ❌ reject | ✅ accept (단 빈 슬롯만 — `_consumeInv.TryAddAt` 가 차 있는 슬롯엔 false 반환) |

→ 드래그 시작 시 `RelicData.IsConsumable` 캡처 + drop receiver 가 type 체크.

### 4. 시각 피드백 — USS 클래스 토글

신규 셀렉터:

```css
.iv-slot-drop-target {
    border-color: rgb(120, 200, 120);  /* 초록 — accept */
    border-width: 2px;
}

.iv-slot-drop-invalid {
    border-color: rgb(200, 80, 80);  /* 빨강 — reject */
    border-width: 2px;
}

.iv-grid-drop-target {
    background-color: rgba(120, 200, 120, 0.15);  /* 영역 단위 */
}
```

→ DragEnter 시 추가 / DragLeave + DragPerform 시 제거. Unity Editor 의 기본 hover effect 위에 강조.

### 5. 더블클릭 흐름과의 직교

CL-176 의 `OnTreeItemsChosen` → `AddRelicToCorrectInventory(relic)` 그대로 무수정. drag 는 별도 콜백 그룹.

→ 디자이너가 더블클릭 / drag 둘 다 사용 가능. 동등 효과 (단 drag 는 Consumable 슬롯 지정 추가 가치).

### 6. ContextualMenuManipulator 충돌 방지

`BuildSlotElement` 의 `ContextualMenuManipulator` (우클릭) 는 `PointerDown` 의 right button 만 처리. 본 CL 의 drag 는 left button 만 → 충돌 X.

명시적 가드:

```csharp
element.RegisterCallback<PointerDownEvent>(evt =>
{
    if (evt.button != 0) return;  // left button 만
    // ... drag start
});
```

### 7. TreeView makeItem element pool 재사용 가드

UI Toolkit 의 TreeView 는 makeItem 으로 만든 element 를 재사용 (pool). bindItem 에서 매번 `RegisterCallback` 추가하면 중복 등록 → drag 다발.

해결:
- `makeItem` 에서 한 번만 `RegisterCallback` 등록
- `bindItem` 에서 element 의 userData 에 현재 InventoryTreeNode 저장 → `PointerDownEvent` 콜백 안에서 userData 조회

```csharp
makeItem = () =>
{
    var label = new Label();
    label.RegisterCallback<PointerDownEvent>(OnLeafPointerDown);
    return label;
},
bindItem = (element, index) =>
{
    var node = _treeView.GetItemDataForIndex<InventoryTreeNode>(index);
    var label = (Label)element;
    label.text = node?.DisplayLabel ?? string.Empty;
    label.userData = node;  // ★ 콜백에서 사용
}
```

### 8. 그룹 노드 drag 방지

InventoryTreeNode 의 그룹 (Permanent / Consumable 카테고리 노드) 은 `Relic == null`. drag 시작 시 가드:

```csharp
private void OnLeafPointerDown(PointerDownEvent evt)
{
    if (evt.button != 0) return;
    var element = (VisualElement)evt.currentTarget;
    if (element.userData is not InventoryTreeNode node) return;
    if (node.Relic == null) return;  // ★ 그룹 노드 drag 방지

    DragAndDrop.PrepareStartDrag();
    DragAndDrop.SetGenericData("InventoryTest_RelicData", node.Relic);
    DragAndDrop.objectReferences = new Object[] { node.Relic };
    DragAndDrop.StartDrag(node.Relic.DisplayName ?? node.Relic.name);
}
```

### 9. drop 후 자동 갱신

`_relicInv.TryAdd` / `_consumeInv.TryAddAt` 호출 → CL-175 의 이벤트 구독 (`OnRelicAcquired` / `MaxSlotsChanged`) 자동 발화 → `RefreshPermanentArea` 자동 호출. **추가 갱신 코드 X**.

Consumable 의 경우 이벤트 부재 ([cl175_implementation.md §위험 #2](cl175_implementation.md)) → drop 후 명시적 `OnConsumableChanged()` 호출 (CL-176 패턴 일관).

### 10. namespace / 파일 위치

기존 그대로 (`LostMemory.Editor.InventoryTest`, `Editor/InventoryTest/`). 신규 클래스 X — 모두 `InventoryTestWindow` 멤버 / 메서드 추가.

---

## 신규 / 수정 파일 — 코드

### A. `InventoryTestWindow.cs` — 추가/변경

```csharp
// 신규 const (멤버 영역)
private const string DragDataKey = "InventoryTest_RelicData";
private const string DragTargetClass = "iv-slot-drop-target";
private const string DragInvalidClass = "iv-slot-drop-invalid";
private const string GridTargetClass = "iv-grid-drop-target";

// BuildLeftTree 의 TreeView 변경 — makeItem / bindItem 시그니처 갱신
_treeView = new TreeView
{
    makeItem = () =>
    {
        var label = new Label();
        label.RegisterCallback<PointerDownEvent>(OnLeafPointerDown);
        return label;
    },
    bindItem = (element, index) =>
    {
        var node = _treeView.GetItemDataForIndex<InventoryTreeNode>(index);
        var label = (Label)element;
        label.text = node?.DisplayLabel ?? string.Empty;
        label.userData = node;
    },
    // ... 나머지 동일
};

// 신규 메서드 — drag start
private void OnLeafPointerDown(PointerDownEvent evt)
{
    if (evt.button != 0) return;
    var element = (VisualElement)evt.currentTarget;
    if (element.userData is not InventoryTreeNode node) return;
    if (node.Relic == null) return;

    DragAndDrop.PrepareStartDrag();
    DragAndDrop.SetGenericData(DragDataKey, node.Relic);
    DragAndDrop.objectReferences = new Object[] { node.Relic };
    DragAndDrop.StartDrag(node.Relic.DisplayName ?? node.Relic.name);
}

// CreateGUI 에서 PermanentGrid drop receiver 등록 — RefreshPermanentArea 직후
private void RegisterPermanentGridDropTarget()
{
    var grid = _permanentArea.Q<VisualElement>("PermanentGrid");
    if (grid == null) return;

    grid.RegisterCallback<DragEnterEvent>(evt =>
    {
        var relic = DragAndDrop.GetGenericData(DragDataKey) as RelicData;
        if (relic == null) return;
        if (relic.IsConsumable) grid.AddToClassList(DragInvalidClass);
        else                    grid.AddToClassList(GridTargetClass);
    });
    grid.RegisterCallback<DragUpdatedEvent>(evt =>
    {
        var relic = DragAndDrop.GetGenericData(DragDataKey) as RelicData;
        DragAndDrop.visualMode = (relic != null && !relic.IsConsumable)
            ? DragAndDropVisualMode.Copy
            : DragAndDropVisualMode.Rejected;
    });
    grid.RegisterCallback<DragLeaveEvent>(evt =>
    {
        grid.RemoveFromClassList(GridTargetClass);
        grid.RemoveFromClassList(DragInvalidClass);
    });
    grid.RegisterCallback<DragPerformEvent>(evt =>
    {
        var relic = DragAndDrop.GetGenericData(DragDataKey) as RelicData;
        grid.RemoveFromClassList(GridTargetClass);
        grid.RemoveFromClassList(DragInvalidClass);
        if (relic == null || relic.IsConsumable) return;
        if (_relicInv == null) return;
        DragAndDrop.AcceptDrag();
        _relicInv.TryAdd(relic);
        // 자동 갱신은 OnRelicAcquired 이벤트 (CL-175 구독)
    });
}

// BuildSlotElement 변경 — Consumable 슬롯에 drop receiver 추가
private VisualElement BuildSlotElement(RelicData relic, int slotIndex, bool isConsumableSlot)
{
    var slot = new VisualElement();
    slot.AddToClassList("iv-slot");
    // ... 기존 빈/채워진 슬롯 표시 + ContextualMenuManipulator (CL-176/177 머지) 그대로

    if (isConsumableSlot)
    {
        RegisterConsumableSlotDropTarget(slot, slotIndex);
    }
    return slot;
}

// 신규 메서드 — Consumable 슬롯 drop receiver
private void RegisterConsumableSlotDropTarget(VisualElement slot, int slotIndex)
{
    slot.RegisterCallback<DragEnterEvent>(evt =>
    {
        var relic = DragAndDrop.GetGenericData(DragDataKey) as RelicData;
        if (relic == null) return;
        bool canAccept = relic.IsConsumable
                         && _consumeInv != null
                         && _consumeInv.Slots[slotIndex] == null;
        slot.AddToClassList(canAccept ? DragTargetClass : DragInvalidClass);
    });
    slot.RegisterCallback<DragUpdatedEvent>(evt =>
    {
        var relic = DragAndDrop.GetGenericData(DragDataKey) as RelicData;
        bool canAccept = relic != null && relic.IsConsumable
                         && _consumeInv != null
                         && _consumeInv.Slots[slotIndex] == null;
        DragAndDrop.visualMode = canAccept
            ? DragAndDropVisualMode.Copy
            : DragAndDropVisualMode.Rejected;
    });
    slot.RegisterCallback<DragLeaveEvent>(evt =>
    {
        slot.RemoveFromClassList(DragTargetClass);
        slot.RemoveFromClassList(DragInvalidClass);
    });
    slot.RegisterCallback<DragPerformEvent>(evt =>
    {
        var relic = DragAndDrop.GetGenericData(DragDataKey) as RelicData;
        slot.RemoveFromClassList(DragTargetClass);
        slot.RemoveFromClassList(DragInvalidClass);
        if (relic == null || !relic.IsConsumable) return;
        if (_consumeInv == null) return;
        if (_consumeInv.Slots[slotIndex] != null) return;
        DragAndDrop.AcceptDrag();
        _consumeInv.TryAddAt(slotIndex, relic);
        OnConsumableChanged();  // 이벤트 부재 → 명시적 갱신
    });
}
```

> `using UnityEditor;` 는 이미 존재 — 별도 import 불요. `Object` 는 `UnityEngine.Object` (이미 in scope).

### B. `InventoryTestWindow.uxml` — 무수정

UXML 변경 X. 기존 `PermanentGrid` / `ConsumableGrid` / 슬롯 element 그대로 사용.

### C. `InventoryTestWindow.uss` — 추가 (3 셀렉터)

```css
.iv-slot-drop-target {
    border-color: rgb(120, 200, 120);
    border-width: 2px;
}

.iv-slot-drop-invalid {
    border-color: rgb(200, 80, 80);
    border-width: 2px;
}

.iv-grid-drop-target {
    background-color: rgba(120, 200, 120, 0.15);
}
```

→ 너비 2px / 색상 초록(accept) · 빨강(reject) · 영역 초록 반투명. 기존 `.iv-slot` 의 border-width 1px 위에 덧씀.

---

## 위험 / 알려진 상황

| # | 위험 | 대응 |
|---|---|---|
| 1 | **TreeView element pool 재사용 → 콜백 중복 등록** | `makeItem` 에서 1회 등록 + `bindItem` 에서 userData 갱신 (§7) |
| 2 | **DragLeave 미발화 (드래그 중 윈도우 밖 / 도메인 리로드)** | DragPerform 에서도 RemoveFromClassList 호출. 도메인 리로드 시 `OnEnable` 재호출되며 USS 클래스 자동 정리 (rootVisualElement 재구축) |
| 3 | **PointerDown 후 drag 시작 안 함 (단순 클릭)** | `DragAndDrop.StartDrag` 는 PointerDown 시점에 즉시 호출. Unity 가 마우스 이동 임계값 자동 처리 (단순 클릭이면 drag event 미발화). selection / itemsChosen (`OnTreeSelectionChanged` / `OnTreeItemsChosen`) 도 정상 동작 |
| 4 | **그룹 노드 (Permanent / Consumable 카테고리) drag** | `node.Relic == null` 가드 (§8). 그룹 라벨 텍스트 끌기 방지 |
| 5 | **잘못된 영역 drop** | `IsConsumable` type 체크 + visualMode `Rejected` (§3). 디자이너가 빨간 border 시각으로 즉시 인지 |
| 6 | **차 있는 Consumable 슬롯 drop** | DragEnter / Updated 의 `_consumeInv.Slots[slotIndex] == null` 체크 → `Rejected`. DragPerform 에서도 가드 |
| 7 | **`_consumeInv.Slots` 가 IReadOnlyList 인지 List 인지** | [PlayerConsumableInventory.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Relics/PlayerConsumableInventory.cs) 의 `Slots` 프로퍼티 확인 — `IReadOnlyList<RelicData>` 라면 인덱서 OK |
| 8 | **Permanent 그리드 의 빈 슬롯 visual feedback** | 본 CL 은 그리드 영역 단위 receiver — 개별 빈 슬롯 하이라이트 X. 향후 폴리시 ticket 검토 (Permanent 도 슬롯 단위 drop 시 더 정확한 피드백) |
| 9 | **드래그 중 우클릭 메뉴 발화** | `ContextualMenuManipulator` 은 right button (`evt.button == 1`) 처리. drag 는 left (`evt.button == 0`). 충돌 X (§6) |
| 10 | **OS 드래그 (외부 파일 / Project 창 → 본 창)** | 본 CL 무관. `DragAndDrop.GetGenericData(DragDataKey)` 가 외부 drag 시 null → 모든 receiver 가 자동 Rejected |
| 11 | **`DragAndDrop.objectReferences` 의 부수효과** | `objectReferences` 설정 시 Unity 가 Project 창에서 PingObject 가능. 디자이너 보너스 — 의도된 동작 |
| 12 | **CL-185 (자동 배치) 결정 영향** | 본 CL 은 슬롯 직접 지정 (수동) — CL-177 (수동 메뉴) 와 일관. CL-185 자동 배치 채택 시에도 drag 는 별도 가치 (정확한 슬롯 선택). CL-185 결정과 직교 |
| 13 | **CL-183 (검색) 적용 후 drag** | 검색 필터링된 트리 leaf 에서도 drag 시작 가능 — `_treeView.GetItemDataForIndex` 가 필터 결과 기준. 본 CL 코드 영향 X |
| 14 | **DragAndDrop API 의 EditorWindow 한정** | `UnityEditor.DragAndDrop` 는 Editor 전용. 본 CL 은 EditorWindow 라 OK. Runtime 은 `IBeginDragHandler` 등 별도 — 본 CL 무관 |

---

## 검증 시나리오

### CS 빌드 검증 (Claude)

```
1. InventoryTestWindow.cs 컴파일 OK (UnityEditor / UIElements 이미 import)
2. InventoryTestWindow.uss 추가 셀렉터 파싱 OK
3. Grep "DragAndDrop.GetGenericData" 매치 = InventoryTestWindow.cs 신규 콜백들
4. PointerDownEvent / DragEnterEvent / DragUpdatedEvent / DragLeaveEvent / DragPerformEvent 사용처 확인
5. _treeView.makeItem / bindItem 변경 후 시그니처 정합 확인
```

### Unity Editor 검증 (사용자)

선결: CL-176 (`41ba59d73`) + CL-177 (`db82245f0`) 머지 확인 ✅

```
6. Tools > LostMemory > Inventory Test Window 열기 → Play 모드 진입
7. 좌측 트리에서 Permanent RelicData (예: 'Khi 검') 선택 → 마우스 버튼 누른 채 우측 PermanentGrid 영역으로 드래그
   → DragEnter 시 PermanentGrid 영역에 초록 반투명 배경 (.iv-grid-drop-target)
   → DragLeave 시 배경 사라짐
   → DragPerform (마우스 떼기) 시 슬롯에 RelicData 추가, OnRelicAcquired → 자동 갱신
8. 같은 Permanent RelicData 를 Consumable 슬롯으로 드래그
   → 슬롯에 빨간 border (.iv-slot-drop-invalid)
   → 마우스 떼도 추가 X (visualMode Rejected)
9. Consumable RelicData (예: 회복약) 를 Permanent 그리드로 드래그
   → 그리드에 빨간 border-like 시각 (영역 단위는 단순 거부)
   → 추가 X
10. Consumable RelicData 를 Consumable 슬롯 0 (1번 슬롯) 으로 드래그
    → 슬롯에 초록 border (.iv-slot-drop-target)
    → 마우스 떼면 슬롯 0 에 추가, OnConsumableChanged → RefreshConsumableArea
11. 차 있는 Consumable 슬롯에 다른 Consumable 드래그
    → 빨간 border, 마우스 떼도 추가 X
12. 트리의 그룹 노드 (Permanent / Consumable 카테고리 라벨) 드래그 시도
    → drag 시작 X (node.Relic == null 가드)
13. 단순 클릭 (drag 없이 떼기) 시 selection 정상 동작 (PingObject)
14. 더블클릭 시 기존 itemsChosen 흐름 (TryAdd 자동) 정상 동작 — drag 와 직교
15. 우클릭 시 ContextualMenu (Remove) 정상 동작 — left button drag 와 충돌 X
16. 검색 (CL-183 머지 후) 적용 상태에서 drag 동작 확인 — 필터링된 leaf 도 drag 가능
```

---

## 핵심 파일

### 수정 (Claude — 2)

| 경로 | 변경 |
|---|---|
| `LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/InventoryTestWindow.cs` | const 4개 + makeItem/bindItem 시그니처 변경 + `OnLeafPointerDown` 신규 + `RegisterPermanentGridDropTarget` 신규 + `RegisterConsumableSlotDropTarget` 신규 + `BuildSlotElement` 변경 (Consumable drop receiver 호출) + CreateGUI 에서 RegisterPermanentGridDropTarget 호출 |
| `.../Resources/InventoryTestWindow.uss` | `.iv-slot-drop-target` / `.iv-slot-drop-invalid` / `.iv-grid-drop-target` 3 셀렉터 추가 |

### 무수정

- `InventoryTestWindow.uxml` — 구조 변경 없음
- `Runtime/Relics/RelicData.cs` / `PlayerRelicInventory.cs` / `PlayerConsumableInventory.cs` — 호출만
- `InventoryTreeNode.cs` — userData 에 저장만, 구조 무변경
- 기존 ContextualMenuManipulator (우클릭 Remove, CL-176 머지) — 무수정, drag 와 직교

---

## 후속 ticket 영향

| Ticket | 본 CL 와의 관계 |
|---|---|
| **CL-187** (Quick Add 프리셋) | 본 CL 무관 — 프리셋 저장/로드는 별도 영역 (EditorPrefs / PresetSO 직렬화 + 메뉴) |
| **CL-185** (Consumable 자동 배치, 결정 미정) | 본 CL 의 drag (수동 슬롯 지정) 가 CL-185 (자동 배치) 와 직교. 둘 다 채택 가능 (drag 는 정확한 슬롯, 더블클릭은 자동 배치). 또는 CL-185 흡수 결정 시에도 본 CL 영향 0 |
| **Permanent 슬롯 단위 drop receiver** (폴리시 후속) | 본 CL 은 그리드 영역 단위 — 개별 빈 슬롯 하이라이트 X. 향후 폴리시 ticket 에서 슬롯 단위 receiver 로 전환 가능 (`BuildSlotElement` 의 Permanent 분기 추가) |
| **드래그 중 ghost 이미지** (폴리시 후속) | Unity Editor 의 기본 hover effect 만. 향후 사용자 정의 ghost (RelicData icon 표시) 폴리시 |
| **PlayerConsumableInventory 이벤트 추가** (별도 Runtime ticket) | 본 CL 의 `OnConsumableChanged()` 명시 호출은 이벤트 부재의 우회. 이벤트 추가 시 자동 갱신으로 단순화 |
| **Consumable 슬롯 간 swap / 이동** (별도 ticket 후보) | 본 CL 무관 — 슬롯 → 슬롯 drag 는 본 CL 범위 외 |

---

## 작업 순서

| 순서 | 담당 | 작업 |
|---|---|---|
| 1 | (선결) | CL-176 (`41ba59d73`) + CL-177 (`db82245f0`) 머지 확인 ✅ |
| 2 | Claude | `InventoryTestWindow.uss` 에 3 셀렉터 추가 |
| 3 | Claude | `InventoryTestWindow.cs` 수정 — const 4개 + makeItem/bindItem + drag/drop 콜백 메서드 + CreateGUI / BuildSlotElement 호출 |
| 4 | Claude | 컴파일 / Grep 검증 |
| 5 | 사용자 | Unity Editor 검증 (§검증 6-16) |
| 6 | 사용자 | MR 생성 (커밋 / push 사용자 직접) |
| 7 | — | Epic V 다음 진입 후보: **CL-187** (Quick Add 프리셋, 신규) / CL-185 정책 결정 (CL-177 충돌) |

---

## 예상 시간

| 단계 | 담당 | 시간 |
|---|---|---|
| USS 추가 | Claude | 5분 |
| `InventoryTestWindow.cs` drag/drop 로직 추가 | Claude | 25분 |
| 컴파일 / Grep 검증 (Claude) | Claude | 5분 |
| Unity Editor 검증 (사용자) — 11개 시나리오 | 사용자 | 20분 |
| **합계** | | **약 55분** |

→ 2점 P2 ticket 적정 분량. CL-183 (3점, 40분 예상) 보다 약간 김 — drag/drop 의 4개 이벤트 콜백 + Permanent/Consumable 분기 + visual feedback 등 코드량 ↑.

---

## 메모

- **드래그앤드롭은 EditorWindow 표준 패턴 채택** — `UnityEditor.DragAndDrop` API + UI Toolkit `DragEnter/Updated/Leave/Perform` 결합. Runtime EventSystem (uGUI `IBeginDragHandler`) 와 별개 체계
- **더블클릭 (CL-176) 흐름 무수정** — drag 는 별도 콜백. 디자이너가 둘 다 사용 가능. drag 는 Consumable 슬롯 직접 지정이 추가 가치
- **Permanent 그리드 영역 단위 receiver** — 슬롯 단위 receiver 는 폴리시 후속. 본 CL 은 인벤토리 자동 배치 정책 일관 (TryAdd 가 첫 빈 슬롯 자동 결정)
- **Consumable 슬롯 단위 receiver** — TryAddAt(N) 호출로 디자이너가 정확한 슬롯 지정. CL-177 의 수동 메뉴 (ShowConsumableSlotMenu) 와 동등 가치 — drag 는 시각적으로 더 직관적
- **CL-183 (검색) 머지 여부와 무관** — drag 는 트리 leaf element 단위 콜백 → 검색 필터링 후에도 drag 가능
- **CL-185 결정과 직교** — drag (수동 슬롯) 가 채택 후에도 더블클릭 자동 배치 (CL-185) 동시 채택 가능. UX 보완 관계
- master plan epic_uv §2 ticket 시트 line 72 의 우선순위 P2 — CL-187 (Quick Add) 도 P2 — Epic V 폴리시 단계
- 본 CL 완료 후 [client1_tasks_master_plan.md](client1_tasks_master_plan.md) / [epic_uv_master_plan_20260506.md](epic_uv_master_plan_20260506.md) §0 진행 상태 갱신은 사용자 영역
