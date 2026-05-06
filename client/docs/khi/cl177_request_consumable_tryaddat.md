# [협업 요청] PlayerConsumableInventory.TryAddAt 메서드 추가

> 수신: **노소연** (PlayerConsumableInventory.cs 작성자, f260ddde6)
> 발신: 김회인 (Epic U Inventory Test Window — CL-177)
> 작업 추정: **~10분** (메서드 1개 추가 + 본인 검증)
> 작성일: 2026-05-06

---

## 한 줄 요약

`PlayerConsumableInventory` 에 **`TryAddAt(int slot, RelicData consumable)`** 메서드 1개 추가 요청. 기존 `TryAdd` 는 무수정. 게임 보상 흐름 영향 0.

---

## 컨텍스트

김회인이 Epic U 의 **Inventory Test Window** (Tools > LostMemory > Inventory Test Window) 를 만들고 있어. 디자이너/테스터가 Play 모드에서 RelicData 를 직접 인벤토리에 추가/제거해서 효과·세트·스택을 검증하는 EditorWindow.

CL-177 단계에서 **Consumable 슬롯 지정 UI** 를 추가하려고 해. 디자이너가 "어느 슬롯 (1~4) 에 넣을지" 선택 가능. 그런데 현재 `PlayerConsumableInventory.TryAdd(RelicData)` 는 **첫 빈 슬롯 자동 배치**라 슬롯 인덱스를 명시할 방법이 없음. 그래서 슬롯 지정 메서드가 필요해.

---

## 요청 메서드 명세

### 시그니처

```csharp
public bool TryAddAt(int slot, RelicData consumable)
```

### 동작

| 조건 | 결과 |
|---|---|
| `slot < 0` 또는 `slot >= SlotCount` | `false` (범위 밖, 경고 로그) |
| `consumable == null` 또는 `!consumable.IsConsumable` | `false` (소모품 아님, 경고 로그) |
| `_slots[slot] != null` (차 있음) | **`false`** (덮어쓰기 X — 빈 슬롯에만 배치) |
| 위 모두 통과 | `_slots[slot] = consumable; Debug.Log; return true` |

→ 핵심 정책: **빈 슬롯에만 배치 (덮어쓰기 X)**. InventoryTestWindow UI 가 차 있는 슬롯을 disabled 로 표시해서 디자이너 실수 방지하는 정책과 일관.

### 구현 예시 (그대로 붙여넣기 가능)

```csharp
/// <summary>
/// CL-177: 지정 슬롯에 소모품 배치. 슬롯이 차 있으면 false (덮어쓰기 X).
/// InventoryTestWindow 의 슬롯 지정 UI 가 사용. 게임 보상 흐름은 기존 TryAdd (첫 빈 슬롯 자동) 사용.
/// </summary>
/// <returns>배치 성공이면 true, 실패 (차 있음 / 범위 밖 / 소모품 아님) 이면 false</returns>
public bool TryAddAt(int slot, RelicData consumable)
{
    if (slot < 0 || slot >= SlotCount)
    {
        Debug.LogWarning($"[PlayerConsumableInventory] 슬롯 인덱스 범위 오류: {slot}");
        return false;
    }
    if (consumable == null || !consumable.IsConsumable)
    {
        Debug.LogWarning("[PlayerConsumableInventory] 소모품이 아닌 아이템은 추가할 수 없습니다.");
        return false;
    }
    if (_slots[slot] != null)
    {
        Debug.LogWarning($"[PlayerConsumableInventory] 슬롯 {slot + 1} 이미 사용 중: {_slots[slot].DisplayName}");
        return false;
    }

    _slots[slot] = consumable;
    Debug.Log($"[PlayerConsumableInventory] 소모품 지정 배치 (슬롯 {slot + 1}): {consumable.DisplayName}");
    return true;
}
```

추가 위치: 기존 `TryAdd(RelicData)` 메서드 바로 아래 권장 (관련 메서드 인접).

---

## 비파괴 보장 (영향 0)

| 항목 | 보장 |
|---|---|
| 기존 `TryAdd(RelicData)` | **무수정** — 게임 보상 흐름 (RewardController → TryAdd) 영향 0 |
| 다른 메서드 (Remove / Slots / Clear / Swap / Get) | **무수정** |
| `_slots` 필드 | private 그대로, 외부 노출 X |
| 이벤트 발화 | 기존 정책 그대로 (이벤트 X — 본 메서드도 추가 X) |
| Inspector / ContextMenu 디버그 | 영향 0 |

→ 신규 메서드 1개 추가 외 기존 동작 0% 변경.

---

## 본인 검증 (노소연 영역)

```
1. PlayerConsumableInventory.cs 컴파일 OK
2. 게임 보상 흐름 — RewardController → PlayerConsumableInventory.TryAdd 동작 그대로 (수동 테스트 또는 기존 시나리오)
3. ContextMenu / Inspector 의 기존 디버그 메서드 동작 그대로
4. (선택) TryAddAt 단위 테스트:
   - 빈 슬롯에 배치 → true + Slots[slot] == consumable
   - 차 있는 슬롯에 배치 → false + Slots[slot] 변화 없음
   - 범위 밖 (-1, 4) → false
   - null 또는 일반 유물 (IsConsumable=false) → false
```

---

## 협업 흐름

| 순서 | 담당 | 작업 |
|---|---|---|
| 1 | **노소연** | PlayerConsumableInventory.cs 에 TryAddAt 추가 (~10분) |
| 2 | **노소연** | 위 §본인 검증 실행 |
| 3 | **노소연** | 별도 MR 생성 + 머지 |
| 4 | **노소연** | 머지 완료 시 김회인 (gimhoein@gmail.com / Slack) 에 알림 |
| 5 | 김회인 | 알림 수신 후 CL-177 의 InventoryTestWindow 코드 진입 |

---

## 참고 — 본 메서드를 호출할 InventoryTestWindow 측 코드 (예시)

노소연 작업 후 김회인이 작성할 코드 일부 (참고용 — 노소연 작업 범위 외):

```csharp
// InventoryTestWindow.cs
private void OnConsumableSlotPicked(int slot, RelicData consumable)
{
    if (_consumeInv == null) return;
    bool ok = _consumeInv.TryAddAt(slot, consumable);   // ★ 본 요청 메서드
    if (ok) RefreshConsumableArea();
    else Debug.LogWarning($"[InventoryTest] Slot {slot + 1} 배치 실패");
}
```

---

## 질문 / 변경 의견

본 명세 그대로 진행해도 되고, 시그니처/동작 의견 있으면 김회인에게 알려줘:
- gimhoein@gmail.com
- Slack DM

→ 의견 반영 시 본 md + 김회인의 cl177_plan.md 동시 갱신.

---

## 관련 문서

- 김회인의 본 작업 plan: [cl177_plan.md](cl177_plan.md) — InventoryTestWindow 측 작업 명세 (참고용)
- Epic U/V 마스터: [epic_uv_master_plan_20260506.md](epic_uv_master_plan_20260506.md) — Epic U Inventory 트랙 (CL-174~177) 전체 흐름
