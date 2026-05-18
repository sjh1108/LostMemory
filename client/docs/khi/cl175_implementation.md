# CL-175 좌측 RelicData 트리 + 우측 인벤토리 슬롯 표시 — 구현 기록

작성일: 2026-05-06

브랜치: `feat/S14P31C201-410/cl-175-좌측-relic-data-트리-우측`

기준 plan: [cl175_plan.md](cl175_plan.md), 선행 ticket 구현 기록: [cl174_implementation.md](cl174_implementation.md)

**상태**: 🟢 **검증 완료** — Play 모드 진입 시 좌측 트리 + 우측 슬롯 그리드 표시, RefreshPlayerInstances 자동 동작, Edit 모드 가드 유지. 사용자 "잘 됨" 확인.

---

## 목적

CL-174 셸 위에 **좌측 LeftPanel = RelicData 트리 (Permanent / Consumable 그룹) + 우측 RightPanel = Player 인벤토리 슬롯 시각화** 추가. 디자이너/테스터가 어떤 RelicData 가 존재하는지 + 현재 Player 가 무엇을 보유 중인지 한눈에 파악.

**해결되는 문제**:
- CL-174 의 빈 Body 영역 → 실제 시각 정보 표시
- 보상 흐름 우회 도구의 첫 번째 사용 가치 — 디자이너가 즉시 트리에서 RelicData 확인 가능 (Project 창 PingObject 도)
- CL-176 동작(추가/제거/Clear)의 진입점 마련 — _treeView / _relicInv / _consumeInv / 이벤트 구독 인프라 완성

---

## 설계 기준 + 사용자 결정

### Plan 단계 결정 사항 (cl175_plan.md 그대로)
- **TreeView 채택** — Permanent / Consumable 그룹 + RelicData leaf. ListView 단일 평면 미채택.
- **RelicData 검색 = SearchFolders 명시** (`Assets/_Project/ScriptableObjects/Relics/Generated`). RelicCategoryProvider (CL-163) 패턴 일치.
- **그룹핑 기준 = `IsConsumable`**. Permanent / Consumable 두 그룹.
- **Leaf 라벨 = DisplayName 만**. Rarity / Icon 표시는 폴리시 후속.
- **우측 분할 = PermanentArea + ConsumableArea**. 슬롯 60×60, flex-wrap.
- **이벤트 구독**:
  - `_relicInv.OnRelicAcquired / OnRelicRemoved / OnCleared / MaxSlotsChanged` → 자동 갱신
  - `_consumeInv` 이벤트 없음 → Refresh 버튼으로 수동 감지
- **Single-click Selection → `EditorGUIUtility.PingObject(relic)`** (디자이너 편의)
- **`InventoryTreeNode` 신규 클래스** — BalanceEditor 의 TreeNode 와 충돌 회피, namespace 분리.
- **모드 전환 처리** — Edit 진입 시 `UnsubscribeInventoryEvents()`, Play 진입 시 `RefreshPlayerInstances()` 가 재구독.

### 작업 중 사용자 결정 사항
- **추가 결정 없이 plan 그대로 진행** — 사용자 "그대로 진행하자" 확인. plan 의 §위험 4 "Consumable 0 = 정상" 도 그대로 수용.

### 작업 중 발견 사항
- **★ plan §A 코드의 컴파일 결함 (CS1628)** — `BuildGroup(ref int id, ...)` 메서드 안에서 `sos.Select(r => ... id++)` 람다가 ref 파라미터를 캡처하려 함. C# 에서는 `ref / out / in` 파라미터를 람다 / 익명 메서드 / 로컬 함수 안에서 직접 캡처 불가. **foreach 로 교체하여 해소** (메서드 본문이라 ref 파라미터 직접 접근 가능).
- **현재 Generated/RelicData asset 77개 모두 `IsConsumable=false`** — 본 CL 검증 시 좌측 트리에 `Permanent (77)` + `Consumable (0)` 으로 표시. plan §위험 4 와 일치, 정상 동작.
- **PlayerRelicInventory 의 Debug ContextMenu 5개 활용 가능** — 검증 시 `_debugRelicsToAdd` SerializeField 채운 후 "Debug — Add all assigned relics" 로 우측 PermanentArea 자동 갱신 검증 가능.
- **OnDisable / UpdateModeView Edit 진입 시 UnsubscribeInventoryEvents 추가** — plan §위험 7 / §설계 9 명시 사항. 이벤트 구독 leak 방지.

## 확정된 설계 결정

| 결정 | 값 | 근거 |
|---|---|---|
| 좌측 컴포넌트 | TreeView (그룹 + leaf) | 그룹핑 + BalanceEditor 일관 |
| TreeView item data | `InventoryTreeNode` 신규 클래스 | BalanceEditor TreeNode 충돌 회피 |
| RelicData 검색 | SearchFolders 명시 | RelicCategoryProvider 패턴 |
| 그룹핑 | `IsConsumable` (Permanent / Consumable) | 도메인 자연 분류 |
| Leaf 라벨 | DisplayName | 단순. Rarity/Icon 폴리시 후속 |
| 우측 분할 | PermanentArea + ConsumableArea | UXML 명시적 placeholder |
| 슬롯 크기 | 60×60 | 시각 가독성 + 다수 표시 |
| 슬롯 정렬 | flex-wrap (5×5 강제 X) | 폴리시 후속 |
| 빈 슬롯 | `.iv-slot-empty` 회색 placeholder + "·" 라벨 | 시각 명확성 |
| 채워진 슬롯 | DisplayName 라벨 (Icon 폴리시 후속) | 단순 |
| MaxSlots 표시 | PermanentArea 헤더 `(N/MaxSlots)` | 행운 보너스 반영 |
| Permanent 자동 갱신 | OnRelicAcquired/Removed/Cleared/MaxSlotsChanged 구독 | 이벤트 주도 |
| Consumable 갱신 | Refresh 버튼 | 이벤트 없음 (Runtime 한계) |
| Selection 동작 | `EditorGUIUtility.PingObject(relic)` | 디자이너 편의 |
| 모드 전환 처리 | Play 진입: 재구독 / Edit 진입: 구독 해제 | leak 방지 |
| BuildGroup ID 발급 | ref 파라미터 + foreach 루프 | CS1628 회피 (plan 의 Select 람다 결함 수정) |

---

## 수정 파일

### 신규 (Claude — 1)

| 경로 | 내용 |
|---|---|
| `LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/InventoryTreeNode.cs` | TreeView item data (DisplayLabel + Relic) |

### 수정 (Claude — 3, CL-174 위에 추가)

```text
LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/InventoryTestWindow.cs
    - using System.Collections.Generic / System.Linq 추가
    - 멤버 추가: _treeView / _permanentArea / _consumableArea / _permanentHeader / _consumableHeader / _allRelics / RelicSearchFolders
    - CreateGUI 확장: leftPanel/right area Q, RefreshButton 핸들러, BuildLeftTree 호출
    - 신규 메서드: BuildLeftTree / LoadAllRelics / BuildTreeData / BuildGroup / OnTreeSelectionChanged
                  / RefreshInventoryView / RefreshPermanentArea / RefreshConsumableArea / BuildSlotElement
                  / SubscribeInventoryEvents / UnsubscribeInventoryEvents
                  / OnRelicChanged / OnInventoryCleared / OnMaxSlotsChanged
                  / OnRefreshClicked
    - RefreshPlayerInstances 확장: Unsubscribe → Find → Subscribe → RefreshInventoryView
    - UpdateModeView 확장: Edit 진입 시 UnsubscribeInventoryEvents
    - OnDisable 확장: UnsubscribeInventoryEvents 추가 (leak 방지)

LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/Resources/InventoryTestWindow.uxml
    - Toolbar 에 .iv-toolbar-spacer + RefreshButton 추가
    - RightPanel 안에 PermanentArea (Header + PermanentGrid) + ConsumableArea (Header + ConsumableGrid)

LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/Resources/InventoryTestWindow.uss
    - 추가 셀렉터: .iv-toolbar-spacer / .iv-refresh-btn / .iv-area / .iv-area-header
                   / .iv-slot-grid / .iv-slot / .iv-slot-empty / .iv-slot Label
```

### 무수정 (참고)

- `Runtime/Relics/PlayerRelicInventory.cs` — 이벤트 구독 + 메서드 호출만
- `Runtime/Relics/PlayerConsumableInventory.cs` — Slots 읽기 + Refresh 버튼 갱신
- `Runtime/Relics/RelicData.cs` — DisplayName / IsConsumable 호출만
- BalanceEditor 전체 — TreeView 패턴 모방 대상 (코드 수정 없음)

> Runtime grep 매치 1건 (PlayerConsumableInventory.cs:48) 은 다른 작업자가 적은 cross-reference 주석 — 코드 변경 X.

---

## 발견·해소된 이슈

### 1. ★ plan §A 코드의 컴파일 결함 (CS1628)
**문제**: cl175_plan.md §A 의 `BuildGroup` 메서드:
```csharp
private TreeViewItemData<InventoryTreeNode> BuildGroup(
    ref int id, string groupName, List<RelicData> sos)
{
    var leaves = sos.Select(r => new TreeViewItemData<InventoryTreeNode>(
        id++,                    // ← CS1628: ref 파라미터를 람다 안에서 사용 불가
        new InventoryTreeNode { ... }
    )).ToList();
    ...
}
```
`ref / out / in` 파라미터는 C# 언어 사양상 람다 / 익명 메서드 / 로컬 함수 안에서 직접 캡처 불가. Unity 컴파일 시 첫 번째 시도에서 오류 발생.

**해소**: `Select` 람다를 `foreach` 로 교체. 메서드 본문이라 ref 파라미터 직접 접근 가능.
```csharp
var leaves = new List<TreeViewItemData<InventoryTreeNode>>(sos.Count);
foreach (var r in sos)
{
    leaves.Add(new TreeViewItemData<InventoryTreeNode>(
        id++,
        new InventoryTreeNode { Relic = r, DisplayLabel = r.DisplayName ?? r.name }
    ));
}
```

**향후**: cl175_plan.md 자체의 코드 갱신은 사용자 영역 (history 보존 vs 갱신 trade-off). implementation 에 결함 명시로 충분.

---

## 검증 결과

### 1. CS 빌드
- **첫 시도**: CS1628 컴파일 오류 발생 (위 §발견 이슈 1)
- **수정 후**: ✅ 컴파일 OK
- Grep 검증: `InventoryTreeNode` / `InventoryTestWindow` 매칭 = 신규/수정 2개 파일 + 다른 작업자의 주석 1건 (cross-reference, 코드 영향 X)
- git status: Modified 3 + Untracked 1 (의도와 일치)

### 2. Unity Editor 동작 (사용자 "잘 됨" 확인) ✅
- `LostMemory > Inventory Test Window` 열기 → CL-174 동작 그대로
- Edit 모드 — 안내 문구 + Body 비활성 (CL-174 동작 유지)
- Play 모드 진입:
  - 좌측 트리 — `Permanent (77)` + `Consumable (0)` 자동 펼침, RelicData leaf 들 DisplayName 정렬
  - 우측 PermanentArea — `Permanent (0/25)` 헤더 + 25개 빈 슬롯
  - 우측 ConsumableArea — `Consumable (0/4)` 헤더 + 4개 빈 슬롯
- 자동 갱신 / 이벤트 구독 동작 정상
- Play 종료 → 안내 문구 재표시, Body 비활성, 이벤트 구독 해제

### 미검증 (CL-176+ 영역)
- 트리 leaf 더블클릭 → TryAdd (CL-176)
- 우클릭 → ContextMenu Remove (CL-176)
- Clear 버튼 (CL-176)
- Consumable 슬롯 지정 (CL-177)

---

## 위험 / 결정 미정

### 위험
1. **Consumable 그룹 빈 트리 (현재 정상)**: Generated 폴더에 IsConsumable=true asset 0개 → `Consumable (0)` 표시. 일반 Consumable RelicData 추가 시 자동 분기. 디자이너가 "Consumable 안 채워져?" 라고 물을 수 있어서 인지 필요.
2. **PlayerConsumableInventory 이벤트 부재**: 게임 보상 흐름 / 외부 변경 시 우측 ConsumableArea 자동 갱신 X. Refresh 버튼 클릭 필요. Runtime 측 이벤트 추가는 별도 ticket.
3. **OwnedRelics null 슬롯 의존**: PlayerRelicInventory.Remove 가 인덱스 유지 (null 교체) 라는 가정에 의존. Runtime API 변경 시 본 도구 영향 — 컴파일 오류로 즉시 발견 가능 (`BuildSlotElement(null)` 가드).
4. **Editor 도메인 리로드 시 stale 참조**: `_treeView` / `_relicInv` / 이벤트 구독 — `OnEnable` / `CreateGUI` / `RefreshPlayerInstances` 가 재호출되며 복구.
5. **TreeView Generic 타입 명시 의존**: `GetItemDataForIndex<InventoryTreeNode>(index)` — 타입 정확 매칭 필요. 컴파일 시 보장.
6. **RelicData 클래스명 변경 시 영향**: `t:RelicData` 필터 + Runtime 직접 참조 둘 다 영향. SO rename 시 본 Provider + 본 도구 함께 갱신.

### 결정 미정 (본 CL 외)
- [ ] CL-176: 더블클릭 추가 / 우클릭 제거 / Clear 버튼
- [ ] CL-177: Consumable 4슬롯 별도 영역 + 슬롯 지정 (1~4번 클릭)
- [ ] Epic V (CL-183~187): 검색 / 드래그앤드롭 / Quick Add 프리셋
- [ ] Leaf Rarity / Icon 표시 (폴리시 ticket)
- [ ] PlayerConsumableInventory 이벤트 추가 (별도 Runtime ticket — Epic V 와 별개)

---

## 후속 인계

| Ticket | CL-175 와의 관계 |
|---|---|
| **CL-176 (더블클릭/우클릭/Clear)** | 본 CL 의 `_treeView.selectionChanged` 패턴 + Refresh 흐름 활용. 더블클릭 → `_relicInv.TryAdd(node.Relic)` (Permanent) / `_consumeInv.TryAdd` (Consumable). 우클릭 → `ContextualMenuManipulator` |
| **CL-177 (Consumable 슬롯 지정)** | 본 CL 의 ConsumableArea 가 이미 별도 영역 — CL-177 = 슬롯 번호 지정 (1~4번) UI 추가 |
| **CL-183 (Epic V 검색)** | BalanceEditor `FilterTree` 패턴 (BalanceEditorWindow.cs:506) 그대로 차용 가능 — `ToolbarSearchField` 추가 + BuildTreeData → FilterTreeData 적용 |
| **CL-184 (Epic V 자동갱신 강화)** | 본 CL 이미 Permanent 4개 이벤트 구독 — CL-184 추가 작업 = drag/drop 시점 / Consumable 부분 |
| **별도 ticket — RelicData 클래스명 변경 시** | 본 도구 + RelicCategoryProvider 함께 갱신 |

---

## 예상 vs 실제 시간

| 단계 | 예상 (plan) | 실제 |
|---|---|---|
| InventoryTreeNode.cs 작성 | 5분 | 약 2분 |
| UXML/USS 갱신 | 15분 | 약 5분 |
| InventoryTestWindow.cs 메서드 추가 | 35분 | 약 10분 (plan 코드 통합) |
| 컴파일 / Grep 검증 (Claude) | 10분 | 약 3분 (CS1628 발견) |
| 컴파일 오류 수정 (foreach 교체) | (plan 외 추가) | 약 3분 |
| Unity Editor 검증 (사용자) | 15분 | 약 5분 |
| **합계** | **약 80분** | **약 28분** |

doc 명세도가 매우 높고 BalanceEditor 패턴 재사용 효과 큼. CS1628 컴파일 오류 1건 외 큰 이슈 없었음. 2점 ticket 적정 규모이나 인프라가 cl174 / cl163 에서 잘 검증돼 있어 시간 단축.
