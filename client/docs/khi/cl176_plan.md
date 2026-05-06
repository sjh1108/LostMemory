# CL-176 — 더블클릭 추가 / 우클릭 제거 / Clear 버튼

## Context

CL-175 의 트리/슬롯 위에 **동작** 추가. 디자이너가 RelicData 를 인벤토리에 추가/제거/일괄 비우기 가능 — 효과 검증 흐름 완성.

**2점 P1, CL-175 의존.**

### 사용자 결정 (확정)

| Q | 결정 |
|---|---|
| Q1 | **(b) Clear 버튼 분리** — "Clear Permanent" / "Clear Consumable" 2개 |
| Q2 | **(a) Confirm 다이얼로그** — `EditorUtility.DisplayDialog` |
| Q3 | **(a) IsInstantUse 일반 TryAdd + Console 경고 (hybrid)** — 동작은 단순 (슬롯 추가), 안내는 Debug.LogWarning |

### 본 CL 책임 범위

| 포함 | 제외 |
|---|---|
| TreeView leaf 더블클릭 → IsConsumable 분기 → 적절 inventory.TryAdd | Consumable 슬롯 지정 (어느 슬롯에 — → CL-177) |
| Permanent slot 우클릭 → `_relicInv.Remove(relic)` | 드래그앤드롭 (→ CL-186 Epic V) |
| Consumable slot 우클릭 → `_consumeInv.Remove(int slotIndex)` | Quick Add 프리셋 (→ CL-187 Epic V) |
| "Clear Permanent" / "Clear Consumable" 버튼 + Confirm 다이얼로그 | 검색 (→ CL-183 Epic V) |
| IsInstantUse Console 경고 (디자이너 안내) | |

---

## 현황 (탐색 — CL-175 시점에서 파악, 신규 read 0)

### 1.1 CL-175 셸 + 트리/슬롯 (plan 기준 — 미머지)

[cl175_plan.md](cl175_plan.md) 정의:
- `_treeView` (TreeView) — leaf 단일 클릭 = PingObject (CL-175)
- `_relicInv` / `_consumeInv` (Player 인스턴스)
- `BuildSlotElement(RelicData)` — 단일 인자
- `RefreshPermanentArea()` / `RefreshConsumableArea()` — 슬롯 재그림
- 이벤트 구독: OnRelicAcquired/Removed/Cleared/MaxSlotsChanged → 자동 갱신
- Toolbar: Refresh 버튼

→ **CL-175 머지 선결**. 본 plan 은 CL-175 위에 동작 추가.

### 1.2 PlayerRelicInventory 동작 메서드 (cl175_plan §1.3)

| 메서드 | 동작 | 이벤트 발화 |
|---|---|---|
| `TryAdd(RelicData)` | 일반 유물 추가, 소모품은 false 반환 (라우팅 신호) | `OnRelicAcquired` |
| `Remove(RelicData)` | 첫 매칭 슬롯 null 교체 (위치 유지) | `OnRelicRemoved` |
| `Clear()` | 전체 List Clear | `OnCleared` |
| `Has(RelicData)` | name 비교 unique 체크 | — |

### 1.3 PlayerConsumableInventory 동작 메서드 (cl175_plan §1.4)

| 메서드 | 동작 | 이벤트 발화 |
|---|---|---|
| `TryAdd(RelicData)` | 첫 빈 슬롯에 추가, 소모품 아니면 false | **없음** |
| `Remove(int slotIndex)` | 해당 슬롯 null 교체 | **없음** |
| `Clear()` | 4슬롯 전부 null | **없음** |

→ ★ 이벤트 X. 본 CL 이 직접 변경한 시점마다 `OnConsumableChanged()` 수동 호출.

### 1.4 RelicData 분기 필드

- `IsConsumable` (bool) — TryAdd 라우팅 기준
- `IsInstantUse` (bool) — 게임 보상 흐름에선 즉시 효과 후 사라짐. 본 도구는 일반 TryAdd (Q3-a) + Console 경고

### 1.5 UI Toolkit API (표준, 추가 read 불필요)

| API | 용도 |
|---|---|
| `TreeView.itemsChosen` | IEnumerable<object> — 더블클릭 / Enter 키 |
| `VisualElement.AddManipulator(new ContextualMenuManipulator(evt => ...))` | 우클릭 컨텍스트 메뉴 |
| `evt.menu.AppendAction("Remove", _ => ...)` | 메뉴 항목 추가 |
| `EditorUtility.DisplayDialog(title, message, ok, cancel)` | bool — Confirm 다이얼로그 |

---

## 설계 결정

### 1. 더블클릭 라우팅 — `IsConsumable` 분기

PlayerRelicInventory.TryAdd 의 false 신호 (소모품) 에 의존 X. 본 CL 이 미리 판별 후 적절한 inventory 호출.

```csharp
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
        // Q3-a: 일반 TryAdd + Console 경고 (IsInstantUse)
        if (relic.IsInstantUse)
        {
            Debug.LogWarning(
                $"[InventoryTest] '{relic.DisplayName}' 은 IsInstantUse=true. " +
                "게임 보상 흐름에선 즉시 효과 후 사라지지만, 본 도구는 슬롯 추가만 합니다.");
        }
        _consumeInv.TryAdd(relic);   // 이벤트 X — 수동 갱신
        OnConsumableChanged();
    }
    else
    {
        _relicInv.TryAdd(relic);     // OnRelicAcquired → CL-175 자동 갱신
    }
}
```

### 2. 우클릭 ContextualMenuManipulator — slot 별

CL-175 의 `BuildSlotElement(RelicData)` 를 확장. 시그니처 변경:

```csharp
// 기존 (CL-175): BuildSlotElement(RelicData relic)
// 변경 (CL-176): BuildSlotElement(RelicData relic, int slotIndex, bool isConsumableSlot)

private VisualElement BuildSlotElement(RelicData relic, int slotIndex, bool isConsumableSlot)
{
    var slot = new VisualElement();
    slot.AddToClassList("iv-slot");

    if (relic == null)
    {
        slot.AddToClassList("iv-slot-empty");
        slot.Add(new Label("·"));
        // 빈 슬롯 — 우클릭 메뉴 표시 X
        return slot;
    }

    slot.Add(new Label(relic.DisplayName ?? relic.name));
    slot.AddManipulator(new ContextualMenuManipulator(evt =>
    {
        evt.menu.AppendAction("Remove", _ =>
        {
            if (isConsumableSlot)
            {
                _consumeInv.Remove(slotIndex);
                OnConsumableChanged();
            }
            else
            {
                _relicInv.Remove(relic);   // OnRelicRemoved → 자동 갱신
            }
        });
    }));
    return slot;
}
```

CL-175 의 호출부 갱신:
- `RefreshPermanentArea()`: `grid.Add(BuildSlotElement(slot, i, isConsumableSlot: false))`
- `RefreshConsumableArea()`: `grid.Add(BuildSlotElement(slots[i], i, isConsumableSlot: true))`

### 3. Clear 버튼 분리 (Q1-b) + Confirm (Q2-a)

Toolbar 에 2개 버튼:

```csharp
private void OnClearPermanentClicked()
{
    if (_relicInv == null) return;
    if (!EditorUtility.DisplayDialog(
            title: "Clear Permanent Inventory",
            message: $"Permanent 인벤토리 {_relicInv.OwnedRelics.Count(r => r != null)}개를 모두 비웁니다.\n계속할까요?",
            ok: "Clear",
            cancel: "Cancel"))
        return;

    _relicInv.Clear();   // OnCleared → CL-175 자동 갱신
}

private void OnClearConsumableClicked()
{
    if (_consumeInv == null) return;
    int filled = _consumeInv.Slots.Count(s => s != null);
    if (!EditorUtility.DisplayDialog(
            title: "Clear Consumable Slots",
            message: $"Consumable 슬롯 {filled}개를 모두 비웁니다.\n계속할까요?",
            ok: "Clear",
            cancel: "Cancel"))
        return;

    _consumeInv.Clear();
    OnConsumableChanged();   // 이벤트 X — 수동 갱신
}
```

### 4. `OnConsumableChanged()` 헬퍼

PlayerConsumableInventory 이벤트 X. 본 CL 변경 시점 (TryAdd/Remove/Clear) 마다 호출.

```csharp
private void OnConsumableChanged() => RefreshConsumableArea();
```

→ CL-184 (Epic V) 에서 PlayerConsumableInventory 에 이벤트 추가 시 본 메서드를 이벤트 핸들러로 직접 등록 가능 (refactor 작음).

### 5. selection vs itemsChosen — 충돌 X

UI Toolkit TreeView 는 두 이벤트 별개:
- `selectionChanged` (단일 클릭) → CL-175 PingObject
- `itemsChosen` (더블클릭/Enter) → 본 CL TryAdd

동시 등록 가능. 사용자 의도 명확히 분리됨.

### 6. Permanent Remove — 슬롯 인덱스 vs RelicData

PlayerRelicInventory.Remove(RelicData) 가 첫 매칭 슬롯 null 교체 (PlayerRelicInventory.cs:105). Has 가 unique 보장 → 한 RelicData 가 한 슬롯에만 → Remove(relic) 안전.

→ **RelicData 기반** 채택 (코드 단순). 슬롯 인덱스 기반은 후속 (drag/drop 시 필요).

### 7. 빈 슬롯 우클릭

빈 슬롯 (relic == null) 은 ContextualMenuManipulator 추가 X. 우클릭 시 메뉴 표시 안 됨. 디자이너 혼동 회피.

### 8. UXML 추가 — Toolbar 2개 버튼

CL-175 의 Refresh 버튼 옆에:
```xml
<uie:ToolbarButton name="ClearPermanentButton" text="Clear Permanent" class="iv-clear-btn" />
<uie:ToolbarButton name="ClearConsumableButton" text="Clear Consumable" class="iv-clear-btn" />
```

### 9. namespace / 파일 위치

| 파일 | 변경 |
|---|---|
| `InventoryTestWindow.cs` | 메서드 추가 + BuildSlotElement 시그니처 변경 |
| `InventoryTestWindow.uxml` | Toolbar 에 Clear 버튼 2개 |
| `InventoryTestWindow.uss` | `.iv-clear-btn` 추가 |

신규 파일 0.

---

## 신규/수정 파일 — 코드

### A. `InventoryTestWindow.cs` 추가/변경

```csharp
// CreateGUI() 끝부분에 추가
var clearPermanentBtn = root.Q<Button>("ClearPermanentButton");
if (clearPermanentBtn != null) clearPermanentBtn.clicked += OnClearPermanentClicked;

var clearConsumableBtn = root.Q<Button>("ClearConsumableButton");
if (clearConsumableBtn != null) clearConsumableBtn.clicked += OnClearConsumableClicked;

// BuildLeftTree 안 — _treeView 생성 후
_treeView.itemsChosen += OnTreeItemsChosen;

// ── 신규 메서드들 ──

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
        _consumeInv.TryAdd(relic);
        OnConsumableChanged();
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
```

### B. `BuildSlotElement` 시그니처 변경 + Manipulator 추가

```csharp
private VisualElement BuildSlotElement(RelicData relic, int slotIndex, bool isConsumableSlot)
{
    var slot = new VisualElement();
    slot.AddToClassList("iv-slot");

    if (relic == null)
    {
        slot.AddToClassList("iv-slot-empty");
        slot.Add(new Label("·"));
        return slot;   // 빈 슬롯 우클릭 메뉴 X
    }

    slot.Add(new Label(relic.DisplayName ?? relic.name));
    slot.AddManipulator(new ContextualMenuManipulator(evt =>
    {
        evt.menu.AppendAction("Remove", _ =>
        {
            if (isConsumableSlot)
            {
                _consumeInv.Remove(slotIndex);
                OnConsumableChanged();
            }
            else
            {
                _relicInv.Remove(relic);
            }
        });
    }));
    return slot;
}
```

CL-175 의 호출부 갱신:
- `RefreshPermanentArea()`: `grid.Add(BuildSlotElement(slot, i, isConsumableSlot: false));`
- `RefreshConsumableArea()`: `grid.Add(BuildSlotElement(slots[i], i, isConsumableSlot: true));`

### C. `InventoryTestWindow.uxml` 추가

CL-175 의 Toolbar 안에 Refresh 버튼 옆에 2개 추가:

```xml
<uie:Toolbar class="iv-toolbar">
    <ui:Label text="Inventory Test" class="iv-title" />
    <ui:VisualElement class="iv-toolbar-spacer" />
    <uie:ToolbarButton name="ClearPermanentButton"  text="Clear Permanent"  class="iv-clear-btn" />
    <uie:ToolbarButton name="ClearConsumableButton" text="Clear Consumable" class="iv-clear-btn" />
    <uie:ToolbarButton name="RefreshButton"         text="Refresh"          class="iv-refresh-btn" />
</uie:Toolbar>
```

### D. `InventoryTestWindow.uss` 추가

```css
.iv-clear-btn {
    flex-shrink: 0;
    flex-grow: 0;
    width: 110px;
    margin-right: 4px;
}
```

---

## 위험 / 알려진 상황

| # | 위험 | 대응 |
|---|---|---|
| 1 | **PlayerRelicInventory.TryAdd 의 false 신호** — 소모품 라우팅 신호. 본 CL 이 IsConsumable 미리 판별해 우회 | TryAdd 의 라우팅 로직과 본 CL 의 판별이 중복. PlayerRelicInventory 동작 변경 시 본 CL 도 갱신 필요. 단, 안전성을 위해 미리 판별 채택 |
| 2 | **PlayerConsumableInventory 이벤트 X** | OnConsumableChanged 수동 호출 (TryAdd / Remove / Clear 시점). CL-184 에서 이벤트 추가 시 핸들러 자동 등록으로 단순화 |
| 3 | **IsInstantUse 게임 흐름과 다른 동작** | Console 경고로 디자이너 안내 (Q3-a hybrid). 게임에서 즉시 효과 발동 후 사라지는 것이 본 도구에선 슬롯 추가만 됨 |
| 4 | **Confirm 다이얼로그 modal — 작업 흐름 끊김** | Q2-a 결정. 디자이너가 매번 확인 귀찮을 수도 있지만 안전 우선. 후속에서 "Don't ask again" 옵션 추가 가능 |
| 5 | **빈 슬롯 우클릭 시 메뉴 안 뜸** | BuildSlotElement 의 if-null 분기에서 manipulator 추가 X. 디자이너 혼동 회피 (의도된 동작) |
| 6 | **같은 RelicData 더블클릭 두 번** | `_relicInv.TryAdd` 가 Has 중복 체크 → false 반환 + Debug.LogWarning. 본 CL 추가 처리 X (기존 동작 그대로) |
| 7 | **트리에 RelicData 0개 / Player 인스턴스 없음** | OnTreeItemsChosen 의 가드 (`node?.Relic == null`) + AddRelicToCorrectInventory 의 가드 (`_relicInv == null || _consumeInv == null`) — 안전 |
| 8 | **slotIndex 가 RefreshConsumableArea 의 i 와 일치** | for 루프 인덱스 그대로 전달. 슬롯 위치 유지 (Consumable 슬롯도 SlotCount=4 고정) |

---

## 검증 시나리오

### CS 빌드 검증 (Claude)

```
1. InventoryTestWindow.cs 컴파일 오류 없음 (BuildSlotElement 시그니처 변경 + 호출부 일치)
2. UXML element name (ClearPermanentButton / ClearConsumableButton) 정확
3. PlayerRelicInventory / PlayerConsumableInventory / RelicData 무수정 확인
```

### Unity Editor 검증 (사용자)

```
4. Play 모드 진입
5. 좌측 트리 leaf (Permanent) 더블클릭 → 우측 PermanentArea 슬롯에 추가 (자동 갱신)
6. 좌측 트리 leaf (Consumable, IsInstantUse=false) 더블클릭 → 우측 ConsumableArea 첫 빈 슬롯에 추가
7. 좌측 트리 leaf (Consumable, IsInstantUse=true 인 랜덤박스 류) 더블클릭 → 슬롯 추가 + Console 에 경고 메시지
8. Permanent slot 우클릭 → "Remove" 메뉴 표시 → 클릭 → 슬롯 비워짐
9. Consumable slot 우클릭 → "Remove" → 슬롯 비워짐
10. 빈 슬롯 우클릭 → 메뉴 표시 안 됨 (의도)
11. "Clear Permanent" 버튼 → Confirm 다이얼로그 ("Permanent 인벤토리 N개를 모두 비웁니다") → "Clear" → 전체 빈 슬롯
12. "Clear Permanent" 버튼 → 다이얼로그 → "Cancel" → 무동작
13. "Clear Consumable" 버튼 → 동일 흐름
14. 같은 RelicData 더블클릭 두 번 → 두 번째 Console 에 "[PlayerRelicInventory] 이미 보유 중" 경고 (PlayerRelicInventory 기존 동작)
15. CL-175 단일 클릭 PingObject 동작 그대로 — 더블클릭과 충돌 X
16. CL-175 의 OnRelicAcquired/Removed/Cleared 자동 갱신 정상 (이벤트 구독 그대로)
```

---

## 핵심 파일

### 신규 (Claude 작성)

없음.

### 수정 (Claude 작성, CL-175 위에)

| 경로 | 변경 |
|---|---|
| `LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/InventoryTestWindow.cs` | itemsChosen 등록 / AddRelicToCorrectInventory / BuildSlotElement 시그니처 변경 + Manipulator / Clear 핸들러 / OnConsumableChanged |
| `.../Resources/InventoryTestWindow.uxml` | Toolbar 에 ClearPermanentButton / ClearConsumableButton 2개 추가 |
| `.../Resources/InventoryTestWindow.uss` | `.iv-clear-btn` 추가 |

### 무수정 (참고)

| 파일 | 이유 |
|---|---|
| `Runtime/Relics/PlayerRelicInventory.cs` | TryAdd / Remove / Clear 호출만 |
| `Runtime/Relics/PlayerConsumableInventory.cs` | TryAdd / Remove(int) / Clear 호출만 |
| `Runtime/Relics/RelicData.cs` | IsConsumable / IsInstantUse 읽기만 |

> 다른 사람 코드 침범 없음. 모든 변경은 CL-175 (본인 작성) 위에 메서드 추가 + UXML/USS 추가.

---

## 후속 ticket 영향

| Ticket | 관계 |
|---|---|
| **CL-177** Consumable 슬롯 지정 | 본 CL = 첫 빈 슬롯 자동 (PlayerConsumableInventory.TryAdd 동작 그대로). CL-177 = 슬롯 1~4 번 지정 UI 추가 (예: 더블클릭 시 모달 또는 우측 영역 클릭) |
| **CL-184** Epic V 자동갱신 강화 | 본 CL 의 OnConsumableChanged 수동 호출 → PlayerConsumableInventory 에 이벤트 추가 (Runtime 수정) → 본 CL 의 메서드를 이벤트 핸들러로 등록 (refactor) |
| **CL-186** Epic V 드래그앤드롭 | 본 CL 의 BuildSlotElement Manipulator 패턴 위에 DragAndDropManipulator 추가. PlayerRelicInventory.Swap (line 113) 활용 |
| **CL-187** Epic V Quick Add 프리셋 | 본 CL 의 AddRelicToCorrectInventory 를 batch 호출 (프리셋의 RelicData 목록 순회) |

---

## 작업 순서

| 순서 | 담당 | 내용 |
|---|---|---|
| 1 | (선결) | **CL-175 머지 완료** 확인 |
| 2 | Claude | `InventoryTestWindow.uxml` 갱신 (Toolbar 2개 버튼) |
| 3 | Claude | `InventoryTestWindow.uss` 갱신 (.iv-clear-btn) |
| 4 | Claude | `InventoryTestWindow.cs` 갱신 (itemsChosen / AddRelicToCorrectInventory / BuildSlotElement 시그니처 / Clear 핸들러 / OnConsumableChanged) + 호출부 일치 |
| 5 | Claude | 컴파일 오류 없음 확인 |
| 6 | 사용자 | Unity Editor 에서 §검증 4-16 실행 |
| 7 | 사용자 | MR 생성 |
| 8 | — | CL-177 (Consumable 슬롯 지정) 진입 |

---

## 예상 시간

| 단계 | 시간 |
|---|---|
| UXML/USS 갱신 | 10분 |
| InventoryTestWindow.cs 메서드 추가 + BuildSlotElement 시그니처 변경 + 호출부 일치 | 25분 |
| 컴파일 / 검증 (Claude) | 10분 |
| Unity Editor 검증 (사용자) | 15분 |
| **합계** | **약 60분** |

→ 2점 ticket. CL-175 (~80분) 보다 약간 적음 (신규 파일 0, 코드 양 적음).

---

## 메모

- 본 CL 완료 후 디자이너에게 안내:
  - **IsInstantUse** 소모품은 게임과 다른 동작 (즉시 효과 X, 슬롯 추가만)
  - **Clear 버튼**은 Confirm 다이얼로그 매번 표시. 디자이너 피드백 따라 "Don't ask again" 옵션 추가 가능
- master plan epic_uv §11 의 "(CL-175~177) InventoryTestWindow 트리/슬롯/동작 코드 추가" 일관 (CL-175 = 표시, CL-176 = 동작, CL-177 = Consumable 슬롯 지정)
- CL-184 (Epic V) 가 PlayerConsumableInventory 에 이벤트 추가하면 본 CL 의 OnConsumableChanged 수동 호출이 자동화 — refactor 작음
