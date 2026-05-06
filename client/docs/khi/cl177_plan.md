# CL-177 — Consumable 4슬롯 별도 영역 + 슬롯 지정

> ## ⚠️ 차단 의존 — 본 plan 진입 전 필독
>
> 본 CL 의 InventoryTestWindow 코드는 **노소연의 `PlayerConsumableInventory.TryAddAt` 메서드 추가**가 머지된 후에만 컴파일 가능. **미머지 상태에서 코드 작성 시도 시 즉시 컴파일 오류** (호출부의 메서드 미발견).
>
> ### 진입 전 체크리스트
>
> - [ ] [cl177_request_consumable_tryaddat.md](cl177_request_consumable_tryaddat.md) 노소연에게 공유 완료
> - [ ] 노소연이 PlayerConsumableInventory.cs 에 `TryAddAt(int, RelicData)` 추가 + MR 머지 완료
> - [ ] 노소연이 김회인에게 머지 완료 알림 수신
> - [ ] CL-176 머지 완료
>
> **위 4개 모두 ✅ 후에만 §작업 순서 §5 (코드 작성) 진입**. ✅ 전 진입 시 시간 낭비 (컴파일 오류 → 노소연 대기).

## Context

CL-176 의 동작 (더블클릭 / 우클릭 / Clear) 위에 **Consumable 슬롯 지정 UI** 추가. 디자이너가 어느 슬롯 (1~4) 에 소모품을 넣을지 명시 가능. CL-175 의 "슬롯 영역 별도 표시" 도 시각 폴리시로 강화.

**2점 P2, CL-176 의존.**

### 사용자 결정 (확정)

| Q | 결정 |
|---|---|
| Q1 | **(a) GenericMenu 드롭다운** — 더블클릭 시 클릭 위치에 1~4 메뉴, 항목 라벨에 현재 슬롯 내용 |
| Q2 | **(c) 빈 슬롯만 활성** — 차 있는 슬롯 disabled, 4슬롯 다 차면 메뉴 자체 + 에러 메시지 |
| Q5 (Runtime 정책) | **노소연 협업 — 본 plan 에 명세, 노소연이 PlayerConsumableInventory.TryAddAt 추가 후 본 코드 진입** |

### 본 CL 책임 범위

| 포함 | 제외 |
|---|---|
| Consumable 트리 leaf 더블클릭 → GenericMenu (4 슬롯 항목, 빈 슬롯만 활성) | Permanent 트리 leaf 동작 (CL-176 그대로 — 25슬롯 자동) |
| 4슬롯에 1/2/3/4 번호 라벨 (시각 폴리시) | 드래그앤드롭 (→ CL-186 Epic V) |
| 4슬롯 다 차면 메뉴 disabled + 에러 메시지 | Quick Add 프리셋 (→ CL-187 Epic V) |
| `_consumeInv.TryAddAt(slot, relic)` 호출 (★ 노소연 Runtime 추가 의존) | 검색 (→ CL-183 Epic V) |
| CL-176 의 Consumable 자동 추가 동작 **폐기** (Consumable 더블클릭 = 항상 메뉴) | Permanent 의 슬롯 지정 (의미 X — 25슬롯 unique) |

---

## 현황 (탐색)

### 1.1 CL-176 시점

[cl176_plan.md](cl176_plan.md) 정의:
- Consumable 더블클릭 → `_consumeInv.TryAdd(relic)` (첫 빈 슬롯 자동) + IsInstantUse 경고
- BuildSlotElement(RelicData, slotIndex, isConsumableSlot) — 우클릭 ContextMenu

본 CL = Consumable 부분 변경 (자동 → 슬롯 지정).

### 1.2 PlayerConsumableInventory 작성자 (★ 협업 영역)

```bash
git log --pretty=format:"%h %an" -- PlayerConsumableInventory.cs
→ f260ddde6 노소연 (CL-100번대 "런 임시 인벤토리 UI 구현")
```

**노소연 작성 — 김회인(본인) 코드 X**. 메서드 추가 시 노소연 영역 침범 → 사전 협업 필요.

현재 메서드 (cl175_plan §1.4):
- `TryAdd(RelicData)` — 첫 빈 슬롯 자동 (슬롯 인덱스 지정 불가)
- `Remove(int slotIndex)` — 슬롯 인덱스 기반
- `Slots` IReadOnlyList<RelicData> — 읽기 전용
- `Clear()`
- `Swap(int a, int b)`
- `_slots = new RelicData[SlotCount]` — private (직접 접근 불가)

**핵심 결손**: 슬롯 인덱스 직접 지정해서 추가하는 메서드.

### 1.3 UI Toolkit GenericMenu

```csharp
var menu = new GenericMenu();
menu.AddItem(new GUIContent("Slot 1: (empty)"), false, () => ...);          // 활성
menu.AddDisabledItem(new GUIContent("Slot 2: HealPotion"), false);           // disabled
menu.ShowAsContext();
```

→ disabled 항목 지원. Q2-c 직접 매핑.

---

## 설계 결정

### 1. Consumable 더블클릭 = 슬롯 지정 메뉴 (CL-176 자동 동작 폐기)

CL-176 의 `AddRelicToCorrectInventory` 의 Consumable 분기 변경:

```csharp
private void AddRelicToCorrectInventory(RelicData relic)
{
    // ... 가드
    if (relic.IsConsumable)
    {
        if (relic.IsInstantUse)
        {
            Debug.LogWarning(/* CL-176 IsInstantUse 경고 그대로 */);
        }
        ShowConsumableSlotMenu(relic);   // ★ CL-177 변경 — TryAdd 직접 호출 X
    }
    else
    {
        _relicInv.TryAdd(relic);   // Permanent 그대로
    }
}
```

### 2. ShowConsumableSlotMenu — Q1-a + Q2-c

```csharp
private void ShowConsumableSlotMenu(RelicData consumable)
{
    var slots = _consumeInv.Slots;
    int emptyCount = slots.Count(s => s == null);

    if (emptyCount == 0)
    {
        // Q2-c: 4슬롯 다 차면 메뉴 X + 에러 메시지
        EditorUtility.DisplayDialog(
            "Consumable Slots Full",
            "Consumable 슬롯 4개가 모두 차 있습니다.\n먼저 우클릭 → Remove 로 비워주세요.",
            "OK");
        return;
    }

    var menu = new GenericMenu();
    for (int i = 0; i < PlayerConsumableInventory.SlotCount; i++)
    {
        int slotIdx = i;   // 클로저 캡처
        var existing = slots[i];
        if (existing == null)
        {
            // 빈 슬롯 — 활성
            menu.AddItem(
                new GUIContent($"Slot {i + 1}: (empty)"),
                false,
                () => OnConsumableSlotPicked(slotIdx, consumable));
        }
        else
        {
            // 차 있는 슬롯 — disabled (Q2-c)
            menu.AddDisabledItem(
                new GUIContent($"Slot {i + 1}: {existing.DisplayName ?? existing.name}"));
        }
    }
    menu.ShowAsContext();
}

private void OnConsumableSlotPicked(int slot, RelicData consumable)
{
    if (_consumeInv == null) return;

    // ★ 노소연 추가 예정 메서드 — 미구현 시 컴파일 오류 (의도)
    bool ok = _consumeInv.TryAddAt(slot, consumable);

    if (ok)
    {
        OnConsumableChanged();   // 수동 갱신 (이벤트 X)
    }
    else
    {
        Debug.LogWarning($"[InventoryTest] Slot {slot + 1} 배치 실패 — 슬롯이 차 있거나 인덱스 오류");
    }
}
```

### 3. 시각 폴리시 — 슬롯 1~4 번호 라벨

기존 BuildSlotElement (CL-176) 에 슬롯 번호 라벨 추가 (Consumable 만):

```csharp
private VisualElement BuildSlotElement(RelicData relic, int slotIndex, bool isConsumableSlot)
{
    var slot = new VisualElement();
    slot.AddToClassList("iv-slot");

    // CL-177 시각 폴리시 — Consumable 슬롯에 번호
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

### 4. ConsumableArea 헤더 안내 추가

CL-175/176 의 헤더 `Consumable (filled/4)` 에 "(slot 1-4)" 안내:

```csharp
_consumableHeader.text = $"Consumable ({filled}/{PlayerConsumableInventory.SlotCount}) — slot 1~4";
```

### 5. Permanent 트리 leaf 동작 — CL-176 그대로

Permanent 는 25슬롯 unique. 슬롯 지정 의미 X. CL-176 의 `_relicInv.TryAdd(relic)` 그대로.

### 6. 4슬롯 다 차면 — 메뉴 X + 에러 (Q2-c)

ShowConsumableSlotMenu 진입 시 emptyCount==0 체크 → EditorUtility.DisplayDialog. 메뉴 자체 표시 X.

### 7. 같은 RelicData 중복 검증

PlayerConsumableInventory.TryAdd 는 중복 체크 X (PlayerConsumableInventory.cs 참조 — IsConsumable 만 확인). TryAddAt 도 동일 정책 (디자이너가 같은 소모품 여러 슬롯에 넣고 싶을 수도). 본 CL 추가 처리 X.

### 8. ★ 노소연 협업 — TryAddAt 메서드 추가 명세

본 CL 의 InventoryTestWindow 코드는 `_consumeInv.TryAddAt(slot, relic)` 호출. PlayerConsumableInventory 에 해당 메서드 미존재 → 컴파일 오류. 노소연 작업 후 본 코드 진입.

**노소연에게 요청할 메서드 명세** (별도 §협업 요청 섹션 참조).

### 9. namespace / 파일 위치

| 파일 | 변경 |
|---|---|
| `InventoryTestWindow.cs` | ShowConsumableSlotMenu / OnConsumableSlotPicked / AddRelicToCorrectInventory 변경 / BuildSlotElement (슬롯 번호 라벨) / RefreshConsumableArea 헤더 |
| `InventoryTestWindow.uxml` | 변경 X (슬롯 번호는 코드에서 동적 추가) |
| `InventoryTestWindow.uss` | `.iv-slot-num` 추가 |
| **`PlayerConsumableInventory.cs`** (★ 노소연 영역) | `TryAddAt(int slot, RelicData consumable)` 메서드 추가 (협업 요청) |

신규 파일 0.

---

## ★ 협업 요청 섹션 — PlayerConsumableInventory.TryAddAt 추가

> ⚠️ **차단 의존**: 본 plan 의 InventoryTestWindow 측 코드는 노소연의 메서드 추가 + 머지 완료 후 진행. 미머지 상태에서 코드 작성 시 컴파일 오류 (호출부 메서드 미발견).

| 항목 | 내용 |
|---|---|
| **대상** | 노소연 (PlayerConsumableInventory.cs 작성자, f260ddde6) |
| **요청** | 슬롯 인덱스 지정 메서드 1개 추가: `public bool TryAddAt(int slot, RelicData consumable)` |
| **정책** | 빈 슬롯에만 배치 (차 있으면 false — 덮어쓰기 X). 기존 TryAdd 무수정. 게임 보상 흐름 영향 0 |
| **추정** | ~10분 (메서드 추가 + 본인 검증) |
| **상세 명세 (노소연 전달용)** | ★ **[cl177_request_consumable_tryaddat.md](cl177_request_consumable_tryaddat.md)** |

### 협업 흐름

| 순서 | 담당 | 작업 |
|---|---|---|
| 1 | 김회인 | [cl177_request_consumable_tryaddat.md](cl177_request_consumable_tryaddat.md) 노소연에게 공유 (Slack/MR 코멘트) |
| 2 | 노소연 | TryAddAt 추가 + 본인 검증 + MR 머지 (~10분) |
| 3 | 노소연 | 머지 완료 시 김회인에게 알림 (gimhoein@gmail.com / Slack) |
| 4 | 김회인 | 알림 수신 + CL-176 머지 확인 후 본 plan 의 §작업 순서 §5 진입 |

→ Step 2 미완 시 Step 4 진입 X (상단 ⚠️ 체크리스트 참조).

---

## 신규/수정 파일 — 코드

### A. `InventoryTestWindow.cs` 변경

#### A-1. AddRelicToCorrectInventory 의 Consumable 분기 변경

```csharp
// CL-176: _consumeInv.TryAdd(relic); + OnConsumableChanged();
// CL-177: ShowConsumableSlotMenu(relic);
```

#### A-2. ShowConsumableSlotMenu / OnConsumableSlotPicked 신규

```csharp
private void ShowConsumableSlotMenu(RelicData consumable)
{
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
    bool ok = _consumeInv.TryAddAt(slot, consumable);   // ★ 노소연 추가 메서드
    if (ok) OnConsumableChanged();
    else Debug.LogWarning($"[InventoryTest] Slot {slot + 1} 배치 실패");
}
```

#### A-3. BuildSlotElement — Consumable 슬롯에 번호 라벨

```csharp
if (isConsumableSlot)
{
    var slotNumLabel = new Label((slotIndex + 1).ToString());
    slotNumLabel.AddToClassList("iv-slot-num");
    slot.Add(slotNumLabel);
}
```

#### A-4. RefreshConsumableArea 헤더

```csharp
_consumableHeader.text = $"Consumable ({filled}/{PlayerConsumableInventory.SlotCount}) — slot 1~4";
```

### B. `InventoryTestWindow.uss` 추가

```css
.iv-slot-num {
    position: absolute;
    top: 2px;
    left: 4px;
    font-size: 9px;
    color: rgb(140, 140, 140);
    -unity-font-style: bold;
}

.iv-slot {
    /* CL-175 의 .iv-slot 에 position: relative 추가 (slot-num 의 absolute 기준) */
    position: relative;
}
```

(CL-175 의 .iv-slot 에 `position: relative` 만 추가하면 됨)

### C. `InventoryTestWindow.uxml`

변경 X (슬롯 번호는 코드에서 동적 추가).

---

## 위험 / 알려진 상황

| # | 위험 | 대응 |
|---|---|---|
| 1 | **★ 노소연 Runtime 작업 의존** — TryAddAt 미추가 시 컴파일 오류 | 본 plan 의 §협업 요청 섹션을 노소연에게 공유. Step 2 완료 후 Step 4 진입 (협업 흐름) |
| 2 | **CL-176 의 Consumable 자동 동작 폐기** — 디자이너가 빠른 자동 추가를 원할 수도 | CL-185 (Epic V) 가 다시 자동화 — "수동 슬롯 선택 불필요" 명시. 본 CL 은 MVP 의 정직한 슬롯 지정 단계 |
| 3 | **4슬롯 다 차면 메뉴 X** — 디자이너 흐름 끊김 | EditorUtility.DisplayDialog 안내 ("먼저 우클릭 → Remove"). 디자이너 액션 명확 |
| 4 | **GenericMenu 의 disabled 항목 인지** | 라벨에 슬롯 내용 표시 ("Slot 2: HealPotion") — 디자이너가 회색 항목 보고 인지 |
| 5 | **TryAddAt false 반환 (race condition / 외부 변경)** | OnConsumableSlotPicked 에서 false 시 Console 경고. RefreshConsumableArea 자동 호출 X (이미 OnConsumableChanged 가 if-ok 안에) |
| 6 | **Permanent 동작 기존 그대로 — 사용자 혼동** | Permanent 는 25슬롯 unique. 더블클릭 = 자동 (CL-176). Consumable 만 슬롯 지정 — 의도된 비대칭. 디자이너 안내 |
| 7 | **이미 보유 중인 Consumable 메뉴에서도 보임** | PlayerConsumableInventory.TryAdd 가 중복 허용 (IsConsumable 만 체크). 본 CL 추가 처리 X (디자이너가 의도적으로 같은 소모품 여러 슬롯에 넣을 수 있음) |
| 8 | **노소연 작업 지연 시 본 코드 차단** | 임시 우회: TryAddAt 호출부를 `#if false ... #endif` 블록 처리하거나, OnConsumableSlotPicked 본문에 NotImplementedException + 명확한 메시지. 사용자 결정 |

---

## 검증 시나리오

### CS 빌드 검증 (Claude)

```
1. InventoryTestWindow.cs 컴파일 — 노소연의 TryAddAt 머지 후 OK
2. PlayerConsumableInventory.cs 무수정 (본 CL 측에서) 확인
3. PlayerRelicInventory / RelicData 무수정 확인
```

### Unity Editor 검증 (사용자 — 노소연 머지 후)

```
4. Play 모드 진입
5. Permanent 트리 leaf 더블클릭 → CL-176 동작 그대로 (자동 추가)
6. Consumable 트리 leaf 더블클릭 → GenericMenu 표시:
   - 빈 슬롯: "Slot 1: (empty)" 활성
   - 차 있는 슬롯: "Slot 2: HealPotion" 회색 disabled
7. 빈 슬롯 클릭 → 해당 슬롯에 소모품 배치 + 자동 갱신
8. 4 슬롯 다 차면 더블클릭 → EditorUtility.DisplayDialog ("Consumable Slots Full") 표시 + 메뉴 X
9. Consumable 슬롯 시각: 좌상단에 1/2/3/4 번호 라벨 표시
10. ConsumableArea 헤더: "Consumable (N/4) — slot 1~4"
11. Permanent 슬롯엔 번호 라벨 X (의도)
12. 우클릭 Remove (CL-176 동작) 그대로 — Consumable slot 2 우클릭 → Remove → 슬롯 비워짐
13. 빈 상태에서 다시 더블클릭 → 메뉴에 Slot 2 활성
14. IsInstantUse=true 소모품 더블클릭 → Console 경고 (CL-176) + 슬롯 메뉴 표시 (CL-177 동작 그대로)
```

---

## 핵심 파일

### 신규 (Claude 작성)

없음.

### 수정 (Claude 작성, CL-176 위에 — InventoryTestWindow 측)

| 경로 | 변경 |
|---|---|
| `LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/InventoryTestWindow.cs` | AddRelicToCorrectInventory Consumable 분기 변경 + ShowConsumableSlotMenu / OnConsumableSlotPicked 신규 + BuildSlotElement 슬롯 번호 + RefreshConsumableArea 헤더 |
| `.../Resources/InventoryTestWindow.uss` | `.iv-slot-num` 추가 + `.iv-slot` 에 `position: relative` |

### ★ 협업 (노소연 작성)

| 경로 | 변경 |
|---|---|
| `LostMemory/Assets/_Project/Scripts/Runtime/Relics/PlayerConsumableInventory.cs` | `TryAddAt(int slot, RelicData)` 메서드 1개 추가 (§협업 요청 섹션 명세) |

### 무수정 (참고)

| 파일 | 이유 |
|---|---|
| `Runtime/Relics/PlayerRelicInventory.cs` | 호출만 |
| `Runtime/Relics/RelicData.cs` | 호출만 |
| `InventoryTestWindow.uxml` | 슬롯 번호는 코드에서 동적 (UXML 변경 X) |

> 노소연 영역 (PlayerConsumableInventory) 에 메서드 1개 추가 외 본인 코드 (CL-174~176 산출물) 만 손댐. **사전 협업 합의 + 별도 MR**로 진행.

---

## 후속 ticket 영향

| Ticket | 관계 |
|---|---|
| **CL-184** Epic V 자동 갱신 강화 | 본 CL 의 OnConsumableChanged 수동 호출이 PlayerConsumableInventory 이벤트 추가 시 자동화. 노소연 협업 추가 가능 (이벤트 추가) |
| **CL-185** Epic V "수동 슬롯 선택 불필요" | 본 CL 의 슬롯 지정 메뉴를 우회 — Shift 키 modifier 또는 자동 분기. 본 CL 의 ShowConsumableSlotMenu 그대로 두고 modifier 분기만 추가 |
| **CL-186** Epic V 드래그앤드롭 | 본 CL 의 GenericMenu 우회 — 드래그 시 슬롯 위치 직접 지정. PlayerConsumableInventory.Swap (line 65) 활용 |
| **CL-187** Epic V Quick Add 프리셋 | 본 CL 의 TryAddAt 호출 패턴을 batch — 프리셋의 RelicData + 슬롯 인덱스 명세 |

---

## 작업 순서

| 순서 | 담당 | 작업 |
|---|---|---|
| 1 | 김회인 | **[cl177_request_consumable_tryaddat.md](cl177_request_consumable_tryaddat.md) 를 노소연에게 공유** (Slack/MR 코멘트) |
| 2 | 노소연 | PlayerConsumableInventory.TryAddAt 추가 + 본인 검증 + MR 머지 |
| 3 | 노소연 | 김회인에게 머지 완료 알림 |
| 4 | (선결) | **CL-176 머지 + 노소연 MR 머지 둘 다 확인** ⚠️ |
| — | — | ━━━ 🚧 **§5 진입 차단선** ━━━ 위 §1~§4 모두 완료 전 진입 시 컴파일 오류 |
| 5 | Claude | `InventoryTestWindow.uss` 갱신 (.iv-slot-num + position: relative) |
| 6 | Claude | `InventoryTestWindow.cs` 갱신 (AddRelicToCorrectInventory / ShowConsumableSlotMenu / OnConsumableSlotPicked / BuildSlotElement / RefreshConsumableArea) |
| 7 | Claude | 컴파일 오류 없음 확인 |
| 8 | 사용자 | Unity Editor 에서 §검증 4-14 실행 |
| 9 | 사용자 | MR 생성 |
| 10 | — | CL-178 (Enemy 어댑터 보류) 또는 CL-182 (Epic V) 진입 |

---

## 예상 시간

| 단계 | 담당 | 시간 |
|---|---|---|
| 노소연 협업 요청 + 머지 대기 | 김회인 + 노소연 | 1~2일 (실제 작업 ~10분) |
| InventoryTestWindow.uss 갱신 | Claude | 5분 |
| InventoryTestWindow.cs 메서드 추가 + 변경 | Claude | 25분 |
| 컴파일 / 검증 (Claude) | Claude | 10분 |
| Unity Editor 검증 (사용자) | 사용자 | 15분 |
| **본인 작업 합계** | | **약 55분** (협업 대기 제외) |

→ 2점 ticket. 코드 양은 적지만 **노소연 협업 차단 의존** 으로 실제 lead time 길어질 수 있음.

---

## 메모

- 본 CL 은 **사용자 결정 (Q5) 에 따라 노소연 협업** 으로 진행. 본 plan 의 §협업 요청 섹션이 노소연 작업 명세
- 노소연 작업 지연 시 우회 옵션 (위험 #8): TryAddAt 호출부를 임시 NotImplementedException 처리. 단, 본 CL Editor 동작 X
- master plan epic_uv §11 의 "(CL-175~177) InventoryTestWindow 트리/슬롯/동작 코드 추가" 일관 (CL-175 = 표시, CL-176 = 동작, CL-177 = Consumable 슬롯 지정 + 시각 폴리시)
- CL-185 (Epic V) 가 본 CL 의 슬롯 지정을 다시 자동화 — Shift 키 등 modifier 로 분기 권장
- 디자이너 안내:
  - Consumable 더블클릭 = 슬롯 지정 메뉴 (CL-176 의 자동 동작 폐기)
  - 4 슬롯 다 차면 먼저 Remove
  - Permanent 는 그대로 (CL-176 동작)
