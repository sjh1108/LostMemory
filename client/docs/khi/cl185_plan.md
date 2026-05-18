# CL-185 — Consumable 4슬롯 별도 영역 + 더블클릭 시 빈 슬롯 자동 배치

## Context

Epic V (인벤토리 테스트 도구) ticket. master plan epic_uv §2 ticket 시트 line 71:

> CL-185 — Consumable 4슬롯 별도 영역 + **더블클릭 시 빈 슬롯 자동 배치** — 소모품 영역 분리. 더블클릭 시 첫 빈 슬롯 자동 (수동 슬롯 선택 불필요) — 의존 CL-176 — 1점 P1

**CL-177 (Epic U) 와 정책 충돌**:
- CL-177 (`db82245f0` 머지) = 더블클릭 → `ShowConsumableSlotMenu` (1~4번 슬롯 GenericMenu) → 사용자 **수동 선택** → `TryAddAt(slot, relic)`
- CL-185 = 더블클릭 → 첫 빈 슬롯 **자동 배치** → `TryAdd(relic)`

[epic_uv_master_plan_20260506.md §9 사용자 결정 #3](epic_uv_master_plan_20260506.md): "Consumable 더블클릭 시 빈 슬롯 자동 배치 → CL-185 단순화" — 자동 배치 방향이 사용자 결정. CL-177 머지 시점에 메뉴 패턴이 들어갔으나 본 CL 에서 **자동 배치 + CL-186 drag 로 슬롯 지정 분리** 정책으로 통합.

### ★ 사용자 결정 (2026-05-07, 본 conversation)

| 항목 | 결정 |
|---|---|
| **정책 채택** | **옵션 C** — 더블클릭 = 자동 배치 (CL-185) + 드래그앤드롭 = 수동 슬롯 지정 (CL-186) — 두 UX 분리 |
| **CL-177 의 `ShowConsumableSlotMenu` 처리** | **폐기** (메뉴 진입점 자체 제거) — CL-186 drag 가 수동 슬롯 지정을 담당 |
| **CL-185 진입 시점** | CL-186 머지 여부와 무관 (drag 없어도 자동 배치 + 더블클릭만으로 디자이너 워크플로 충분) |

### 본 CL 책임 범위

| 포함 | 제외 |
|---|---|
| `AddRelicToCorrectInventory` 의 Consumable 분기 단순화 — `ShowConsumableSlotMenu` 호출 → `_consumeInv.TryAdd(relic)` 직접 호출 | 4슬롯 영역 자체 (CL-175 / CL-177 머지로 이미 구현) |
| `ShowConsumableSlotMenu` / `OnConsumableSlotPicked` 메서드 폐기 | Consumable 슬롯 시각 변경 (라벨 / 슬롯 번호 — CL-177 머지 결과 그대로) |
| `BuildSlotElement` 의 Consumable 분기 그대로 (우클릭 Remove 메뉴 유지 — CL-176 머지) | 자동 배치 시 슬롯 우선순위 로직 변경 (`PlayerConsumableInventory.TryAdd` 가 첫 빈 결정) |
| `IsInstantUse=true` 디자이너 경고 로그 유지 (CL-176 패턴) | CL-186 drag 흐름 (별도 ticket) |
| 슬롯이 모두 차 있을 때 Console 경고 + 디자이너에게 사유 표시 | CL-187 preset Load 흐름 (TryAdd 사용 — 본 CL 변경에 일관) |

---

## 현황 (탐색)

### 1.1 CL-177 머지 결과 — 폐기 대상 코드

[InventoryTestWindow.cs:285-308](../../client/LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/InventoryTestWindow.cs) 의 `AddRelicToCorrectInventory`:

```csharp
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
            Debug.LogWarning(...);
        }
        _consumeInv.TryAdd(relic);  // ★ CL-176 시점엔 TryAdd 직접 호출
        OnConsumableChanged();
    }
    else
    {
        _relicInv.TryAdd(relic);
    }
}
```

> **참고**: CL-177 머지 (`db82245f0`) 의 정확한 변경 — `AddRelicToCorrectInventory` 의 Consumable 분기를 `_consumeInv.TryAdd(relic)` 직접 호출에서 `ShowConsumableSlotMenu(relic)` 호출로 교체. 본 CL 은 그 변경을 **되돌림** (TryAdd 직접 호출로 복귀).

[InventoryTestWindow.cs:344+](../../client/LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/InventoryTestWindow.cs) 의 CL-177 신규 메서드:

```csharp
private void ShowConsumableSlotMenu(RelicData consumable)
{
    if (_consumeInv == null) return;
    var slots = _consumeInv.Slots;
    int emptyCount = slots.Count(s => s == null);

    if (emptyCount == 0)
    {
        EditorUtility.DisplayDialog("Consumable Slots Full", ..., "OK");
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
            // ... 차 있는 슬롯은 disabled
        }
    }
    menu.ShowAsContext();
}

private void OnConsumableSlotPicked(int slotIndex, RelicData consumable)
{
    if (_consumeInv == null) return;
    _consumeInv.TryAddAt(slotIndex, consumable);
    OnConsumableChanged();
}
```

→ 본 CL 에서 두 메서드 모두 **삭제**. `AddRelicToCorrectInventory` 분기를 `TryAdd` 직접 호출로 복귀.

### 1.2 PlayerConsumableInventory.TryAdd vs TryAddAt

| API | 동작 | 본 CL 사용 |
|---|---|---|
| `TryAdd(RelicData)` | 첫 빈 슬롯 자동 결정 | ✅ **자동 배치** — 본 CL 채택 |
| `TryAddAt(int slot, RelicData)` | 지정 슬롯 (빈 슬롯만) | CL-186 drag 영역 (본 CL 무관) |

`TryAdd` 가 모두 차 있으면 false 반환 + 변화 X — 본 CL 에서 false 반환 시 Console 경고 추가.

### 1.3 CL-186 (drag) plan 과의 정합

[cl186_plan.md §설계 결정 5](cl186_plan.md): "더블클릭 흐름과의 직교 — CL-176 의 `OnTreeItemsChosen` → `AddRelicToCorrectInventory(relic)` 그대로 무수정. drag 는 별도 콜백 그룹".

→ 본 CL 의 `AddRelicToCorrectInventory` 단순화는 CL-186 drag 콜백과 무관. 두 ticket 직교 유지.

### 1.4 BuildSlotElement Consumable 분기 — 무수정 (CL-176/177 결과 유지)

[InventoryTestWindow.cs:244-274](../../client/LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/InventoryTestWindow.cs):

```csharp
private VisualElement BuildSlotElement(RelicData relic, int slotIndex, bool isConsumableSlot)
{
    var slot = new VisualElement();
    slot.AddToClassList("iv-slot");
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
```

→ **무수정**. 우클릭 Remove (CL-176) 그대로 유지. CL-177 의 슬롯 번호 라벨 (`.iv-slot-num`) 도 그대로 유지 — 시각 정보로 의미 있음.

### 1.5 본 CL 에서 손대는 코드의 작성자

| 파일 | 작성자 |
|---|---|
| `InventoryTestWindow.cs` | **김회인 본인** (CL-174~177 모두 본인) — `AddRelicToCorrectInventory` 분기 단순화 + 메서드 2개 삭제 |
| `InventoryTestWindow.uxml` / `.uss` | 본인 — 무수정 |

→ 협업 0 / 침범 0. CL-177 머지 결과를 본 CL 이 부분 되돌림 — 정상 (본인 작업의 정책 변경).

---

## 설계 결정

### 1. 자동 배치 채택 (epic_uv §9 #3 일관)

```csharp
if (relic.IsConsumable)
{
    // CL-176 의 IsInstantUse 경고 유지
    if (relic.IsInstantUse)
    {
        Debug.LogWarning(...);
    }

    bool added = _consumeInv.TryAdd(relic);
    if (!added)
    {
        Debug.LogWarning($"[InventoryTest] Consumable 슬롯 4개 모두 차 있음. " +
                         $"'{relic.DisplayName ?? relic.name}' 추가 실패. 우클릭으로 슬롯 비운 후 재시도");
    }
    OnConsumableChanged();
}
```

→ TryAdd 가 첫 빈 슬롯 자동 결정. 모두 차 있으면 false → Console 경고. 디자이너에게 명확한 fail-safe.

### 2. CL-177 의 슬롯 메뉴 폐기

`ShowConsumableSlotMenu` / `OnConsumableSlotPicked` 메서드 자체 삭제. 그 자리에 주석:

```csharp
// CL-185: 더블클릭 = 자동 배치로 정책 통합. 슬롯 직접 지정은 CL-186 drag/drop 으로 대체.
// CL-177 의 ShowConsumableSlotMenu / OnConsumableSlotPicked 폐기.
```

→ 코드 readers 가 history 추적 가능.

### 3. EditorUtility.DisplayDialog 모두 차 있을 때

CL-177 의 다이얼로그 (`"Consumable Slots Full"`) 도 폐기. 대신 Console 경고만 — modal 다이얼로그는 빠른 입력 흐름 (디자이너 더블클릭 반복) 을 끊음.

근거:
- CL-186 drag 머지 후엔 시각 피드백 (빨간 border) 으로 자연스러운 UI
- 본 CL 시점 (drag 미머지) 에도 우클릭 Remove (CL-176) 가 있어 디자이너가 슬롯 정리 가능

### 4. CL-177 의 슬롯 번호 라벨 (`.iv-slot-num`) 유지

[InventoryTestWindow.uss:85-92](../../client/LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/Resources/InventoryTestWindow.uss):

```css
.iv-slot-num {
    position: absolute;
    top: 2px;
    left: 4px;
    font-size: 9px;
    color: rgb(140, 140, 140);
    -unity-font-style: bold;
}
```

→ 무수정. 슬롯 번호 (1/2/3/4) 시각 정보는 디자이너 workflow 에 유용 (drag 시 어느 슬롯에 떨어뜨릴지 가늠).

CL-177 의 `BuildSlotElement` 의 `.iv-slot-num` Label 추가 코드도 무수정 — Consumable 슬롯의 번호 표시.

### 5. CL-186 / CL-187 와의 정합

| Ticket | 본 CL 정책과의 관계 |
|---|---|
| **CL-186 drag** | 정상 — drag 의 Consumable 슬롯 drop receiver 가 `TryAddAt(N, relic)` 호출. 본 CL 의 더블클릭 (TryAdd 자동) 과 직교 |
| **CL-187 preset Load** | 정상 — preset 의 consumable[i] 가 비어있지 않으면 `TryAddAt(i, relic)` 호출. 본 CL 변경 영향 X |

→ 더블클릭 = 자동 / drag = 수동 / preset = 슬롯 지정 — 3가지 입력 모두 일관.

### 6. namespace / 파일 위치

기존 그대로. 신규 파일 X.

---

## 신규 / 수정 파일 — 코드

### A. `InventoryTestWindow.cs` — 변경

```csharp
// AddRelicToCorrectInventory 변경
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

        // CL-185: 자동 배치 (epic_uv §9 #3) — 첫 빈 슬롯에 추가
        bool added = _consumeInv.TryAdd(relic);
        if (!added)
        {
            Debug.LogWarning(
                $"[InventoryTest] Consumable 슬롯 4개 모두 차 있음. " +
                $"'{relic.DisplayName ?? relic.name}' 추가 실패. " +
                "우클릭으로 슬롯 비운 후 재시도하거나 (CL-186 머지 후) drag 로 직접 지정.");
        }
        OnConsumableChanged();
    }
    else
    {
        _relicInv.TryAdd(relic);
    }
}

// 폐기 — ShowConsumableSlotMenu / OnConsumableSlotPicked 메서드 자체 삭제
// CL-185: 더블클릭 = 자동 배치로 정책 통합. 슬롯 직접 지정은 CL-186 drag/drop 으로 대체.
// CL-177 의 ShowConsumableSlotMenu / OnConsumableSlotPicked 폐기.
```

> 두 메서드 삭제 + `AddRelicToCorrectInventory` 의 Consumable 분기 변경. 다른 메서드 / `BuildSlotElement` / 우클릭 Remove (CL-176) 모두 무수정.

### B. `InventoryTestWindow.uxml` — 무수정

CL-177 머지 결과 그대로. Toolbar / RightPanel / ConsumableArea / ConsumableGrid 변경 X.

### C. `InventoryTestWindow.uss` — 무수정

CL-177 의 `.iv-slot-num` 그대로. 본 CL 추가 셀렉터 X.

---

## 위험 / 알려진 상황

| # | 위험 | 대응 |
|---|---|---|
| 1 | **CL-177 머지 코드의 부분 되돌림 — git history 일관성** | 본 CL 의 commit message 에 "CL-185: CL-177 의 슬롯 메뉴 폐기 + 자동 배치로 단순화" 명시. 머지 history 자체는 유지 (revert 아님), 부분 변경 |
| 2 | **디자이너 학습 — 기존 메뉴 흐름에서 자동 배치로 변경** | epic_uv §9 #3 의 결정 따름. 디자이너 안내: "더블클릭 = 자동 첫 빈 슬롯, 우클릭으로 정리. 슬롯 직접 지정은 drag 로 (CL-186 머지 후)" |
| 3 | **모두 차 있을 때 Console 경고만 — 디자이너가 못 볼 수 있음** | Console 메시지에 명시적 fail-safe 안내 ("우클릭으로 슬롯 비운 후 재시도"). UI 시각 피드백 (예: 트리 leaf 빨강) 은 폴리시 후속 |
| 4 | **CL-177 의 슬롯 번호 라벨 (.iv-slot-num) 유지** | 본 CL 무관. 시각 정보로 가치 있음 (drag 머지 후 슬롯 식별에 도움) |
| 5 | **TryAdd 가 false 반환 시 OnConsumableChanged 호출** | added 여부 무관 호출 — 변화 없으면 RefreshConsumableArea 가 동일 슬롯 재생성. 약간의 비효율이지만 안전 |
| 6 | **GenericMenu using 문 cleanup** | `using UnityEditor;` 가 다른 코드 (`EditorUtility.DisplayDialog` 등) 에서도 사용 중. 유지 필요. `GenericMenu` 만 사용한 코드면 import cleanup 가능하나 본 CL 은 다른 사용처 영향 X |
| 7 | **CL-186 drag 머지 전 사용자 워크플로** | drag 없는 동안 디자이너가 정확한 슬롯 지정 불가. `_consumeInv.TryAdd` 자동 + 우클릭 Remove 로 슬롯 정리 후 재추가 — 디자이너 안내. CL-186 머지 후 정확 지정 가능 |
| 8 | **CL-187 preset Load 와의 일관** | preset Load 의 `TryAddAt(i, relic)` 호출은 본 CL 의 `TryAdd` 자동 배치와 다른 path. 일관성 OK — preset 은 슬롯 위치 보존이 핵심 |
| 9 | **`PlayerConsumableInventory.TryAdd` 시그니처** | bool 반환 가정 (TryAddAt 와 일관). 사전 검증 필요 — Read 로 확인 |

---

## 검증 시나리오

### CS 빌드 검증 (Claude)

```
1. PlayerConsumableInventory.TryAdd(RelicData) 시그니처 확인 (bool 반환)
2. InventoryTestWindow.cs 의 ShowConsumableSlotMenu / OnConsumableSlotPicked 삭제 후 컴파일 OK
3. AddRelicToCorrectInventory 의 Consumable 분기 변경 후 컴파일 OK
4. Grep "ShowConsumableSlotMenu" / "OnConsumableSlotPicked" 매치 0 (모두 폐기 확인)
5. Grep "_consumeInv.TryAdd(" 매치 = AddRelicToCorrectInventory 1곳 (본 CL 변경 결과)
6. GenericMenu / EditorUtility.DisplayDialog 사용처 — 본 CL 에서 더 이상 사용 X 확인 (다른 코드 영향 X)
```

### Unity Editor 검증 (사용자)

```
7. Inventory Test Window 열기 → Play 모드 진입
8. 좌측 트리에서 Consumable RelicData (예: 회복약) 더블클릭
   → 첫 빈 슬롯 (Slot 1) 에 자동 배치 — 메뉴 표시 X
   → Console 메시지 X (정상 추가)
9. 같은 Consumable 더블클릭 반복 → Slot 2, Slot 3, Slot 4 순으로 자동 배치
10. 5번째 더블클릭 → Console 경고 "Consumable 슬롯 4개 모두 차 있음. ... 우클릭으로 슬롯 비운 후 재시도"
    → 슬롯 변화 X
11. Slot 2 우클릭 → "Remove" → Slot 2 비움
12. 다시 Consumable 더블클릭 → Slot 2 (첫 빈) 에 자동 배치
13. Permanent RelicData 더블클릭 → PermanentArea 자동 추가 (CL-176 동작 — 변경 없음 확인)
14. IsInstantUse=true RelicData (있다면) 더블클릭 → Console 경고 + 자동 배치 (CL-176 경고 유지 확인)
15. (CL-186 머지 후) drag 로 Consumable Slot 2 직접 지정 → 정확 슬롯 추가 (본 CL 과 직교)
16. (CL-187 머지 후) preset Save → Load → Consumable 슬롯 위치 정확 복원 (본 CL 과 직교)
```

---

## 핵심 파일

### 수정 (Claude — 1)

| 경로 | 변경 |
|---|---|
| `LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/InventoryTestWindow.cs` | `AddRelicToCorrectInventory` 의 Consumable 분기 단순화 + `ShowConsumableSlotMenu` / `OnConsumableSlotPicked` 메서드 삭제 (~30줄 제거) + 폐기 사유 주석 추가 |

### 무수정

- `InventoryTestWindow.uxml` — Toolbar / ConsumableArea 변경 X (CL-177 머지 결과 그대로)
- `InventoryTestWindow.uss` — `.iv-slot-num` 그대로 유지 (시각 정보로 가치)
- `BuildSlotElement` 의 우클릭 Remove (CL-176) — 무수정
- `Runtime/Relics/PlayerConsumableInventory.cs` — TryAdd 호출만, API 무수정

---

## 후속 ticket 영향

| Ticket | 본 CL 와의 관계 |
|---|---|
| **CL-186** drag/drop | 본 CL 무관 — drag 의 Consumable drop receiver 는 `TryAddAt(N, relic)` 호출. 본 CL 의 더블클릭 (TryAdd 자동) 과 직교. UX 보완 관계 |
| **CL-187** preset Save/Load | 본 CL 무관 — preset Load 의 `TryAddAt(i, relic)` 호출은 슬롯 위치 보존 핵심. 본 CL 변경 영향 0 |
| **PlayerConsumableInventory 이벤트 추가** (별도 Runtime ticket) | 본 CL 의 `OnConsumableChanged()` 명시 호출은 이벤트 부재의 우회. 이벤트 추가 시 자동 갱신으로 단순화 |
| **Consumable 슬롯 시각 폴리시** (예: 자동 배치 시 슬롯 번호 강조) | 본 CL 외 — 자동 배치된 슬롯에 잠깐 하이라이트 effect 등 UX 강화 |

---

## 작업 순서

| 순서 | 담당 | 작업 |
|---|---|---|
| 1 | (선결) | CL-176 (`41ba59d73`) + CL-177 (`db82245f0`) 머지 확인 ✅ |
| 2 | Claude | `PlayerConsumableInventory.TryAdd` 시그니처 확인 (bool 반환 검증) |
| 3 | Claude | `InventoryTestWindow.cs` 의 `ShowConsumableSlotMenu` / `OnConsumableSlotPicked` 메서드 삭제 |
| 4 | Claude | `AddRelicToCorrectInventory` 의 Consumable 분기 변경 (TryAdd 직접 + 실패 경고) |
| 5 | Claude | 폐기 사유 주석 추가 |
| 6 | Claude | 컴파일 / Grep 검증 |
| 7 | 사용자 | Unity Editor 검증 (§검증 7-14) |
| 8 | 사용자 | MR 생성 (커밋 메시지에 "CL-185 + CL-177 부분 폐기" 명시) |
| 9 | — | Epic V 다음 진입 후보: CL-183 (검색) / CL-186 (drag) / CL-187 (preset) |

---

## 예상 시간

| 단계 | 담당 | 시간 |
|---|---|---|
| TryAdd 시그니처 확인 | Claude | 2분 |
| InventoryTestWindow.cs 메서드 삭제 + 분기 변경 | Claude | 8분 |
| 컴파일 / Grep 검증 (Claude) | Claude | 5분 |
| Unity Editor 검증 (사용자) — 8개 시나리오 | 사용자 | 15분 |
| **합계** | | **약 30분** |

→ 1점 P1 ticket. 코드 단순화 (~30줄 제거) + 분기 1개 변경. CL-184 (코드 0) 와 CL-186 (~55분) 사이 분량.

---

## 메모

- **C 안 채택 사유** — UX 양쪽 보존:
  - 더블클릭 = 빠른 입력 (자동 배치, 1점 ticket 가치)
  - drag (CL-186) = 정확한 슬롯 지정
  - 두 입력 직교 — 디자이너 워크플로 풍부
- **CL-177 머지 코드 부분 되돌림** — `db82245f0` 이 `ShowConsumableSlotMenu` 추가, 본 CL 이 폐기. 머지 history 는 유지 (revert 아님), 코드만 단순화. 본인 단독 작업이라 history 일관성 부담 적음
- **epic_uv §9 #3 의 사용자 결정 (자동 배치)** — 본 CL 이 정식 반영. CL-177 머지 시점에 메뉴 패턴이 잠깐 들어갔으나 본 CL 로 통합
- **CL-186 / CL-187 plan 과 정합** — drag 의 슬롯 지정 / preset 의 슬롯 보존 모두 본 CL 의 자동 배치와 직교
- **slim 1점 ticket** — 코드 제거 위주, 신규 클래스 / UI 추가 없음. CL-178 (코드 0, 흡수 기록) / CL-184 (코드 0, 흡수 기록) 와 다르게 **실제 코드 변경 있음**
- master plan epic_uv ticket 시트 line 71 / §5-B line 175 갱신은 사용자 영역 — "CL-185 (자동 배치) — CL-177 의 슬롯 메뉴 폐기" 메모 권장
- **Epic V 정리 마무리** — CL-185 머지 후 Epic V 정책 충돌 0:
  - CL-182 ~~취소~~ (CL-174 흡수)
  - CL-183 (검색) — plan 작성됨
  - CL-184 ~~취소~~ (CL-175/176 흡수)
  - **CL-185 (자동 배치) — 본 plan**
  - CL-186 (drag) — plan 작성됨
  - CL-187 (preset) — plan 작성됨
- 디자이너 안내 (CL-186 머지 전):
  - "Consumable 더블클릭 = 빈 슬롯 자동 배치. 슬롯 모두 차면 우클릭 Remove 로 정리"
  - "정확한 슬롯 지정은 CL-186 drag 머지 후 가능"
