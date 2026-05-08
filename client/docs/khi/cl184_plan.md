# CL-184 — 더블클릭 추가 / 우클릭 제거 / Clear 버튼 (이벤트 구독 자동 갱신)

## 결론

**CL-184 = CL-175 + CL-176 흡수로 신규 코드 0**. plan 자체가 형식 doc — Jira / master plan 시트의 ticket 식별자 보존 + 흡수 사유 영속 기록.

CL-182 (CL-174 흡수로 패스) 와 동일 패턴. 본 영역 사용자 단독 작업 + Jira 미등록 → 별도 코드 작업 / MR 불필요.

---

## Context

master plan epic_uv §2 ticket 시트 line 70 의 정의:

> CL-184 — Epic V. 인벤토리 테스트 도구 — 더블클릭 추가 / 우클릭 제거 / Clear 버튼 — `TryAdd / Remove / Clear` 호출. **`OnRelicAcquired` / `OnRelicRemoved` 이벤트 구독으로 자동 갱신** — 의존 CL-175 — 2점

차별점 (CL-176 대비) = **"이벤트 구독으로 자동 갱신"**. 동작 자체 (더블클릭 / 우클릭 / Clear) 는 CL-176 와 동일.

→ 그러나 **CL-175 가 이미 `SubscribeInventoryEvents` 구현 + CL-176 가 동작 호출 구현** → CL-184 의 시트 acceptance 가 두 머지로 완전 충족.

---

## 흡수 구조 (이중)

| 흡수 ticket | Jira | 머지 커밋 | 흡수 범위 |
|---|---|---|---|
| **CL-175** (좌측 트리 + 우측 슬롯) | S14P31C201-410 | `b0c40ee57` (merge) / `b636dc84f` (소스) | **이벤트 구독 자동 갱신** — `SubscribeInventoryEvents()` 가 `OnRelicAcquired` / `OnRelicRemoved` / `OnCleared` / `MaxSlotsChanged` 모두 구독. 인벤토리 변경 시 트리/슬롯 자동 갱신 |
| **CL-176** (더블클릭/우클릭/Clear) | S14P31C201-411 | `41ba59d73` (merge) / `5a1f8b436` (소스) | **동작 호출** — `_treeView.itemsChosen` (더블클릭 → `TryAdd`) + `ContextualMenuManipulator` (우클릭 → `Remove`) + `ClearPermanentButton` / `ClearConsumableButton` (`Clear()`) |

→ CL-184 시트 acceptance 모두 충족.

### 코드 증거

**이벤트 구독** ([InventoryTestWindow.cs:339-355](../../client/LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/InventoryTestWindow.cs)):

```csharp
private void SubscribeInventoryEvents()
{
    if (_relicInv == null) return;
    _relicInv.OnRelicAcquired += OnRelicChanged;
    _relicInv.OnRelicRemoved  += OnRelicChanged;
    _relicInv.OnCleared       += OnInventoryCleared;
    _relicInv.MaxSlotsChanged += OnMaxSlotsChanged;
}
```

**동작 호출** (CL-176 머지 후):
- `OnTreeItemsChosen` → `AddRelicToCorrectInventory` → `_relicInv.TryAdd(relic)` / `_consumeInv.TryAdd(relic)`
- `BuildSlotElement` 의 `ContextualMenuManipulator` → "Remove" → `_consumeInv.Remove(slotIndex)` / `_relicInv.Remove(relic)`
- `OnClearPermanentClicked` → `_relicInv.Clear()` / `OnClearConsumableClicked` → `_consumeInv.Clear()`

→ `_relicInv.TryAdd / Remove / Clear` 호출 시 `OnRelicAcquired / OnRelicRemoved / OnCleared` 발화 → CL-175 의 구독 핸들러 (`OnRelicChanged` / `OnInventoryCleared`) 발화 → `RefreshPermanentArea()` 자동 호출. **CL-184 의 "이벤트 구독으로 자동 갱신" 그대로 동작**.

---

## 본 CL 책임 범위

| 포함 | 제외 |
|---|---|
| 본 plan doc 자체 (흡수 사유 영속 기록) | 신규 .cs / .uxml / .uss 코드 (모두 CL-175/176 머지로 충족) |
| 시트 정정 권장 (사용자 영역) | Khi_Stats / Bertha_Boss 등 asset 작업 (본 ticket 무관) |
| Jira ticket 상태 정리 (사용자 영역) | 검색 / 드래그앤드롭 / Quick Add (CL-183 / CL-186 / CL-187) |

---

## 사용자 결정

- 본 영역 (Inventory Test Window) 작업자는 사용자 단독
- Jira 에 CL-184 ticket 미등록
- 사용자가 master plan / ticket 시트 doc 에 직접 ~~CL-184~~ 취소 표기로 마무리

→ Claude 측 코드 작업 0. 별도 implementation doc 미작성. cl184_plan.md 단독으로 결론 영속화.

---

## 핵심 파일

수정 / 신규 / 삭제 — **본 plan doc 1개만** (`client/docs/khi/cl184_plan.md`).

---

## 위험 / 알려진 상황

| # | 위험 | 대응 |
|---|---|---|
| 1 | **후속 Claude / 협업자가 시트 line 70 만 보고 CL-184 진입 시도** | 본 plan doc 으로 흡수 사유 영속화 → 시트 cold read 위험 해소. ~~취소선~~ 표기 권장 |
| 2 | **점수 회계 왜곡** (Epic V 미진행 2점이 미반영 통계) | 사용자 영역. 시트 갱신 시 점수 0 처리 또는 "Duplicate" 상태값으로 통계 제외 |
| 3 | **CL-183 (검색) 진입 시 본 CL 의 자동 갱신 흐름과 충돌 가능성** | 무관 — CL-183 의 `_lastFilter` 보존 정책 ([cl183_plan.md §위험 #7](cl183_plan.md)) 이 CL-184 (CL-175 머지) 의 `RefreshPermanentArea` 와 호환 |
| 4 | **CL-186 (드래그앤드롭) 진입 시 본 CL 흐름과의 의존** | CL-186 도 `TryAdd` 호출이 동일 path → CL-175 의 이벤트 구독으로 자동 갱신 동일 동작. CL-184 흡수와 일관 |
| 5 | **PlayerConsumableInventory 이벤트 부재** | CL-175 implementation §위험 #2 — 게임 보상 흐름 / 외부 변경 시 우측 ConsumableArea 자동 갱신 X. 본 CL 무관. Runtime 측 이벤트 추가는 별도 ticket |

---

## 검증 시나리오

### Claude (read-only 확인)

```
1. Read InventoryTestWindow.cs:339-355 → SubscribeInventoryEvents 의 OnRelicAcquired/Removed/Cleared/MaxSlotsChanged 구독 확인 (CL-175 머지 결과)
2. Read InventoryTestWindow.cs:278-308 → OnTreeItemsChosen / AddRelicToCorrectInventory 의 TryAdd 호출 확인 (CL-176 머지 결과)
3. Read InventoryTestWindow.cs:310-334 → OnClearPermanentClicked / OnClearConsumableClicked 의 Clear() 호출 확인
4. git log --oneline 으로 머지 커밋 확인 — b0c40ee57 (CL-175) / 41ba59d73 (CL-176)
```

### 사용자 (Unity Editor)

cl175 / cl176 검증으로 이미 통과. 본 CL 별도 검증 X. 추가 검증 필요 시:

```
5. Tools > LostMemory > Inventory Test Window 열기 → Play 모드 진입
6. 좌측 트리에서 RelicData 더블클릭 → 우측 PermanentArea 슬롯에 즉시 표시 (TryAdd → OnRelicAcquired → 자동 갱신)
7. 우측 슬롯 우클릭 → "Remove" → 즉시 사라짐 (Remove → OnRelicRemoved → 자동 갱신)
8. Toolbar "Clear Permanent" 버튼 → 모두 사라짐 (Clear → OnCleared → 자동 갱신)
9. Toolbar "Clear Consumable" 버튼 → Consumable 영역 모두 사라짐 (Refresh 버튼으로 수동 갱신 — Consumable 이벤트 부재)
```

→ 위 9 가 cl175 / cl176 검증과 100% 동일. CL-184 는 동일 acceptance 의 별도 진입점.

---

## 작업 순서

| 순서 | 담당 | 작업 |
|---|---|---|
| 1 | Claude | 본 plan doc 작성 (위 §"흡수 구조" / §"코드 증거") — 완료 |
| 2 | 사용자 | (선택) `epic_uv_master_plan_20260506.md` 의 §2 ticket 시트 line 70 + §5-B 표 line 174 + §6 의존 그래프 + §7 우선순위 의 CL-184 라인에 ~~취소선~~ + "(CL-175/176 흡수)" 메모 |
| 3 | 사용자 | (선택) Jira CL-184 ticket = "Closed as Duplicate of CL-175 + CL-176" (등록되지 않았으면 skip) |
| 4 | — | Epic V 다음 진입 후보: CL-183 (검색, plan 작성됨) / CL-186 (드래그앤드롭, plan 미작성) / CL-187 (Quick Add 프리셋, plan 미작성) |

---

## 후속 ticket 영향

| Ticket | 본 CL 와의 관계 |
|---|---|
| **CL-183** (검색) | 무관 — 검색은 좌측 트리 layer, 본 CL 은 동작 + 자동갱신 layer. 직교. CL-183 의 `_lastFilter` 보존 정책이 CL-175 의 `RefreshPermanentArea` 와 호환 ([cl183_plan §위험 #7](cl183_plan.md)) |
| **CL-185** (Consumable 자동 배치) | CL-177 와 충돌 (epic_uv §9 #3 자동 배치 결정 vs CL-177 의 ShowConsumableSlotMenu 수동 메뉴). 본 conversation 분석에서 CL-185 도 흡수 또는 정책 결정 필요로 결론. 별도 plan/decision doc |
| **CL-186** (드래그앤드롭) | 본 CL 무관 — `TryAdd` 호출 path 동일 → 자동 갱신 동작 일관. CL-186 진입 시 별도 plan 작성 |
| **CL-187** (Quick Add 프리셋) | 본 CL 무관 — 신규 영역 (EditorPrefs / PresetSO 직렬화 + 메뉴) |
| **PlayerConsumableInventory 이벤트 추가** (별도 Runtime ticket) | 본 CL 흡수의 한계 보완 — 외부 변경 시 우측 ConsumableArea 자동 갱신 가능해짐 |

---

## 메모

- **CL-184 doc 위치**: `client/docs/khi/cl184_plan.md` 단독 — implementation doc 미작성 (코드 작업 0). cl178_implementation.md 와 유사 패턴이지만 본 CL 은 plan 단계의 "흡수 결정" 이라 plan doc 으로 분류
- **CL-182 와의 차이**: CL-182 는 plan doc 없이 사용자 시트 ~~취소선~~ 만으로 처리 (사용자 결정 2026-05-07). 본 CL 은 사용자 명시 요청 ("plan md 작성") 으로 doc 작성. 두 ticket 모두 코드 작업 0 / Jira 미등록 / 사용자 단독 영역 — 처리 일관
- **CL-184 / CL-185 동시 정책 결정** — 본 conversation 분석에서 CL-184 흡수 + CL-185 충돌 결론. CL-185 는 별도 결정 필요 (CL-177 의 수동 슬롯 메뉴 ↔ CL-185 의 자동 배치 — epic_uv §9 #3 가 자동 결정이지만 CL-177 가 이미 수동으로 머지됨)
- **이벤트 구독 자동 갱신 패턴 영속 기록** — CL-175 implementation §확정 결정 line 63 ("Permanent 자동 갱신 | OnRelicAcquired/Removed/Cleared/MaxSlotsChanged 구독") + 본 CL plan = 이중 영속 기록. 후속 Claude 가 CL-186 / CL-187 진입 시 이벤트 흐름 재확인 가능
- master plan epic_uv ticket 시트 line 70 / §5-B line 174 갱신은 사용자 영역 (cl173 / cl181 / cl182 §메모 정책 동일). 권장: ~~CL-184~~ + "(CL-175/176 흡수)"
- Epic V 의 실질 진입점은 **CL-183** (검색 — plan 작성됨) 또는 **CL-186** (드래그앤드롭 — plan 미작성). CL-184 / CL-185 결정 후 진입
