# CL-177 Consumable 4슬롯 별도 영역 + 슬롯 지정 — 구현 기록

작성일: 2026-05-07

브랜치: `feat/S14P31C201-412/cl-177-consumable-4-슬롯-별도-영역`

기준 plan: [cl177_plan.md](cl177_plan.md), 협업 명세: [cl177_request_consumable_tryaddat.md](cl177_request_consumable_tryaddat.md), 선행 ticket 구현 기록: (CL-176 별도 implementation md 미작성)

**상태**: 🟢 **검증 완료** — Consumable 더블클릭 → 슬롯 1~4 GenericMenu, 빈/차 분기 disabled, 4슬롯 풀 시 DisplayDialog, 슬롯 번호 라벨 + 헤더 갱신 모두 정상. 사용자 "잘 됨" 확인.

---

## 목적

CL-176 의 동작 (더블클릭 자동 / 우클릭 Remove / Clear) 위에 **Consumable 슬롯 지정 UI** 추가. 디자이너가 어느 슬롯 (1~4) 에 소모품을 넣을지 명시 가능. CL-176 의 Consumable 자동 첫 빈 슬롯 동작 폐기 — Consumable 더블클릭 = 항상 메뉴 표시.

**해결되는 문제**:
- CL-176 의 Consumable 자동 배치 → 디자이너가 슬롯 위치 통제 불가 (단축키바 1번/2번/3번/4번 매핑 검증 어려움)
- 어느 슬롯이 차 있는지 한눈에 인지 어려움 → 슬롯 번호 라벨 추가
- 4슬롯 풀 상태에서 "왜 안 추가되지" 의문 → DisplayDialog 명시적 안내

---

## 설계 기준 + 사용자 결정

### Plan 단계 결정 사항 (cl177_plan.md 그대로)
- **Q1-a GenericMenu 드롭다운** — 더블클릭 시 클릭 위치에 1~4 메뉴, 항목 라벨에 현재 슬롯 내용
- **Q2-c 빈 슬롯만 활성** — 차 있는 슬롯 disabled, 4슬롯 다 차면 메뉴 자체 X + 에러 메시지
- **Q5 노소연 협업** — `PlayerConsumableInventory.TryAddAt(int, RelicData)` 추가
- **CL-176 Consumable 자동 동작 폐기** — Permanent 자동만 유지, Consumable = 항상 메뉴
- **시각 폴리시** — 슬롯 1~4 좌상단에 회색 번호 라벨 + 헤더에 `— slot 1~4` 안내
- **GenericMenu / GUIContent** — `using UnityEditor;` + `using UnityEngine;` 으로 해소 (추가 import X)

### 작업 중 사용자 결정 사항 (본 세션 Q3)
- **(a) plan 명세 그대로** — 더블클릭 = 항상 메뉴. modifier (Shift) 분기 X. CL-185 (Epic V) 에서 별도 추가.
  - 근거: plan 검토 단순 / CL-185 별도 ticket 존재 / 디자이너 피드백 받고 결정하는 게 안전 / 본 CL 범위 확장 회피

### 작업 중 발견 사항
- **차단 의존 자연 해소** — 노소연 작업 (`PlayerConsumableInventory.TryAddAt`) 은 본 CL 진입 시점에 이미 develop 머지 완료 (`21f92e41f`). cl177_plan §협업 흐름 §1-§3 자동 통과.
- **CL-176 머지 완료 직전 진행** — 사용자가 본 세션 도중 CL-176 MR (#160, `41ba59d73`) 머지 후 신규 브랜치에서 CL-177 진입. cl177_plan §체크리스트 4개 모두 통과.
- **★ Generated 폴더 IsConsumable=true asset 0개** — 본 CL 검증 시 좌측 트리 `Consumable (0)` → 더블클릭할 leaf 자체 없음 (검증 차단점). cl175_implementation §위험 1 그대로 유지된 상태였음. 임시로 기존 Permanent SO 1개의 `IsConsumable` 토글로 검증 (a 안). 검증 후 원복.
- **TestKhi prefab 와이어링은 CL-176 단계에서 해소됨** — `PlayerRelicInventory` + `PlayerConsumableInventory` 두 컴포넌트 prefab 에 부착 완료. 본 CL 추가 와이어링 X.
- **컴파일 결함 0건** — cl175 의 CS1628 같은 plan 코드 결함 없음. cl177_plan §A 의 코드 블록 그대로 적용 가능.

## 확정된 설계 결정

| 결정 | 값 | 근거 |
|---|---|---|
| Consumable 더블클릭 동작 | `ShowConsumableSlotMenu(relic)` (GenericMenu) | Q1-a / Q2-c |
| 빈 슬롯 메뉴 항목 | `AddItem("Slot N: (empty)", ...)` 활성 | 명확 라벨 |
| 차 있는 슬롯 메뉴 항목 | `AddDisabledItem("Slot N: {DisplayName}")` | Q2-c (덮어쓰기 X) |
| 4슬롯 풀 시 | 메뉴 X + `EditorUtility.DisplayDialog("Consumable Slots Full")` | Q2-c 명시 안내 |
| Permanent 더블클릭 | CL-176 그대로 (`_relicInv.TryAdd`) | Permanent = 25슬롯 unique, 슬롯 지정 의미 X |
| 슬롯 배치 호출 | `_consumeInv.TryAddAt(slot, consumable)` | 노소연 추가 메서드 |
| 배치 실패 시 | `Debug.LogWarning("[InventoryTest] Slot N 배치 실패")` | race / 외부 변경 대비 |
| 갱신 트리거 | `OnConsumableChanged()` (= `RefreshConsumableArea()`) | CL-176 패턴 그대로 (이벤트 X) |
| 슬롯 번호 라벨 | Consumable 만 좌상단 회색 1/2/3/4 (Permanent X) | 시각 비대칭 (의도) |
| 슬롯 번호 라벨 위치 | `position: absolute; top: 2px; left: 4px` | `.iv-slot` 에 `position: relative` 기준점 |
| 헤더 텍스트 | `Consumable (N/4) — slot 1~4` | 슬롯 번호 인지 보강 |
| IsInstantUse 동작 | CL-176 Console 경고 그대로 + 메뉴도 표시 | 두 동작 모두 발화 (의도) |
| 중복 소모품 | 추가 처리 X (TryAddAt 가 슬롯 비었으면 허용) | 디자이너가 같은 소모품 여러 슬롯 가능 |
| modifier 분기 | 미적용 (Q3-a) | CL-185 에서 추가 |

---

## 수정 파일

### 신규 (Claude — 0)

없음.

### 수정 (Claude — 2, CL-176 위에 추가)

```text
LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/InventoryTestWindow.cs
    - line 14 doc-comment 갱신 ("CL-177: 추가 예정" → "Consumable 슬롯 지정 메뉴 + 시각 폴리시")
    - BuildSlotElement 본문 시작에 isConsumableSlot 일 때 슬롯 번호 라벨 (.iv-slot-num) 추가
    - RefreshConsumableArea 헤더 텍스트 끝에 " — slot 1~4" 추가
    - AddRelicToCorrectInventory Consumable 분기:
        _consumeInv.TryAdd(relic) + OnConsumableChanged() 삭제
        ShowConsumableSlotMenu(relic) 로 교체
        IsInstantUse 경고는 그대로 (메뉴 표시 전 발화)
    - 신규 메서드: ShowConsumableSlotMenu(RelicData) — GenericMenu 4슬롯 + emptyCount==0 가드
    - 신규 메서드: OnConsumableSlotPicked(int, RelicData) — TryAddAt 호출 + 결과별 갱신/경고

LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/Resources/InventoryTestWindow.uss
    - .iv-slot 에 position: relative 1줄 추가 (.iv-slot-num 의 absolute 기준점)
    - .iv-slot-num 신규: position: absolute / top 2 / left 4 / font-size 9 / 회색 / bold
```

### 무수정 (참고)

- `LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/Resources/InventoryTestWindow.uxml` — 슬롯 번호는 코드에서 동적 추가
- `Runtime/Relics/PlayerConsumableInventory.cs` — `TryAddAt` 호출만 (메서드 자체는 노소연 별도 MR `21f92e41f` 에서 추가됨)
- `Runtime/Relics/PlayerRelicInventory.cs` — Permanent 호출만 (CL-176 동작 그대로)
- `Runtime/Relics/RelicData.cs` — `IsConsumable` / `IsInstantUse` / `DisplayName` 읽기만
- `_Project/Prefabs/Characters/TestKhi_MinimalCharacter2D.prefab` — CL-176 단계 와이어링 그대로 사용

---

## 발견·해소된 이슈

### 1. ★ Generated 폴더 IsConsumable=true asset 0개 (검증 차단)

**문제**: 본 CL 검증 시점에 좌측 트리 `Consumable (0)` — 더블클릭할 leaf 자체 없음. 사용자 "consumable 개수가 1개라 테스트가 불가능해" 보고 (실제는 0개, 사용자 카운트 헷갈림).

**해소**: 임시로 기존 Permanent SO 1개의 Inspector 에서 `Is Consumable` 체크 → Refresh 버튼 → 트리에 `Consumable (1)` 등장 → 더블클릭 검증 시작. PlayerConsumableInventory `TryAddAt` 가 중복 허용 (cl177_plan §위험 7) 이므로 같은 RelicData 1개로 4슬롯 다 채우는 시나리오 (5-13단계) 모두 커버됨.

**향후**: 본격 Consumable RelicData 추가 (디자이너 작업, 별도 ticket) 시 자연스럽게 트리에 분기. cl175_implementation §위험 1 그대로 유지 — 디자이너 인지 사항.

### 2. 화면 진단 false alarm (PlayerConsumableInventory 와이어링 의심)

**현상**: 사용자 첫 화면 캡처에 `Consumable (0)` 헤더만 보이고 슬롯 X → "PlayerConsumableInventory 안 잡힌 듯" 진단.

**실제**: 캡처 잘림 / 윈도우 스크롤 위치 문제. 이후 전체 화면 캡처에서 Consumable (0/4) — slot 1~4 헤더 + 슬롯 4개 + 1/2/3/4 번호 라벨 모두 정상 그려짐. PlayerConsumableInventory 컴포넌트는 CL-176 단계에서 이미 prefab 에 부착됨 (GUID `dcd05997336ce9042bfb91b72d5dafa4` 확인).

**향후**: 진단 시 "Refresh 클릭 / Console 메시지 / 전체 캡처" 3종 우선 요청. 이번 세션에서 정착된 패턴.

---

## 검증 결과

### 1. CS 빌드
- ✅ 컴파일 OK (CL-176 시점 위에 7개 편집점, plan 명세 그대로)
- `_consumeInv.TryAddAt(...)` 시그니처 매칭 ([PlayerConsumableInventory.cs:51](../../LostMemory/Assets/_Project/Scripts/Runtime/Relics/PlayerConsumableInventory.cs))
- using 추가 0건 (`UnityEditor` / `UnityEngine` 둘 다 기존 보유)

### 2. Unity Editor 동작 (사용자 "잘 됨" 확인) ✅

cl177_plan §검증 시나리오 5-13 통과:
- Permanent leaf 더블클릭 → CL-176 동작 그대로 (자동 추가)
- Consumable leaf 더블클릭 → GenericMenu 4슬롯 표시
  - 빈 슬롯: `Slot N: (empty)` 활성
  - 차 있는 슬롯: `Slot N: {DisplayName}` 회색 disabled
- 빈 슬롯 클릭 → 해당 슬롯 배치 + 자동 갱신 (slotIdx 정확)
- 같은 RelicData 4슬롯 모두 채움 (TryAddAt 중복 허용 확인)
- 4슬롯 풀 → DisplayDialog `Consumable Slots Full` + 메뉴 X
- Consumable 슬롯 시각: 좌상단 1/2/3/4 회색 번호 라벨 (Permanent 슬롯엔 X — 의도)
- ConsumableArea 헤더: `Consumable (N/4) — slot 1~4`
- Consumable slot 우클릭 Remove (CL-176 동작) → 슬롯 비워짐 + 다른 슬롯 유지
- 비운 후 다시 더블클릭 → 메뉴에 해당 Slot 만 활성으로 복귀
- Clear Consumable 버튼 (CL-176) → Confirm → 4슬롯 모두 비워짐, 번호 라벨 유지

### 미검증 (RelicData 추가 필요)
- §14 IsInstantUse=true 소모품 더블클릭 → Console 경고 + 메뉴 동시 발화
  - 본 CL 검증 시점에 IsInstantUse=true 인 Consumable RelicData asset 0개. 코드는 cl177_plan §A-1 명세 그대로 (CL-176 의 IsInstantUse 경고 분기 보존), 컴파일/로직 검증 OK.
  - 향후 IsInstantUse=true SO 추가 시 자연 검증 (별도 ticket).

---

## 위험 / 결정 미정

### 위험
1. **CL-176 의 Consumable 자동 동작 폐기**: 디자이너가 빠른 자동 추가 원할 수 있음 → CL-185 (Epic V) 에서 modifier (Shift) 분기로 보강 예정. 본 CL 미적용 (Q3-a).
2. **4슬롯 다 차면 메뉴 X**: 디자이너 흐름 끊김 가능 → DisplayDialog 안내로 해소 ("먼저 우클릭 → Remove"). 디자이너 학습 필요.
3. **TryAddAt false 반환 (race / 외부 변경)**: OnConsumableSlotPicked 에서 false 시 Console 경고만, RefreshConsumableArea 호출 X. 다음 액션 시 자동 정합 회복.
4. **중복 소모품 허용**: TryAddAt 가 슬롯만 비면 중복 허용. 디자이너 의도 (같은 소모품 여러 슬롯). 본 CL 추가 처리 X.
5. **GenericMenu 의 disabled 항목 인지**: 라벨에 슬롯 내용 표시 (`Slot 2: HealPotion`) — 회색 항목 보고 인지. UI 디자이너 사전 합의 필요한 케이스.
6. **PlayerConsumableInventory 이벤트 부재 (cl175 위험 #1 유지)**: 게임 보상 흐름 / 외부 변경 시 자동 갱신 X. CL-184 (Epic V) 에서 보강 예정.
7. **Generated 폴더 Consumable RelicData 0개**: 본격 Consumable SO 추가 시까지 검증 시 임시 토글 필요. 디자이너 인지 사항.

### 결정 미정 (본 CL 외)
- [ ] CL-178 Enemy 어댑터 보류 또는 CL-182 진입
- [ ] CL-184 (Epic V) PlayerConsumableInventory 이벤트 추가 — Runtime ticket
- [ ] CL-185 (Epic V) modifier 분기 (Shift + 더블클릭 = 자동 첫 빈 슬롯)
- [ ] CL-186 (Epic V) 드래그앤드롭 — Swap 활용
- [ ] CL-187 (Epic V) Quick Add 프리셋 — TryAddAt batch 호출
- [ ] Consumable RelicData asset 추가 (디자이너 작업, 별도 ticket)
- [ ] cl181_plan.md 분리 cleanup — CL-176 머지에 끼어들어 develop 에 들어감 (시점 지남, 후속 cleanup)

---

## 후속 인계

| Ticket | CL-177 와의 관계 |
|---|---|
| **CL-184 자동갱신 강화** | 본 CL 의 `OnConsumableChanged()` 수동 호출 → PlayerConsumableInventory 이벤트 추가 시 핸들러 자동 등록으로 단순화 (refactor 작음) |
| **CL-185 수동 슬롯 선택 불필요 (modifier)** | 본 CL 의 `ShowConsumableSlotMenu` 그대로 두고 `AddRelicToCorrectInventory` 진입 시 modifier 체크 분기 추가. modifier present → CL-176 자동 동작 부활, 일반 → 본 CL 메뉴 |
| **CL-186 드래그앤드롭** | 본 CL 의 GenericMenu 우회 — 드래그 시 슬롯 위치 직접 지정. PlayerConsumableInventory.Swap (line 65) + TryAddAt 활용 |
| **CL-187 Quick Add 프리셋** | 본 CL 의 `TryAddAt` 호출 패턴을 batch — 프리셋의 RelicData + 슬롯 인덱스 명세 |
| **별도 ticket — Consumable RelicData 본격 추가** | 본 CL 검증 시 임시 토글로 우회. 디자이너가 본격 SO 추가 시 자동 트리 분기 |

---

## 예상 vs 실제 시간

| 단계 | 예상 (plan) | 실제 |
|---|---|---|
| 노소연 협업 요청 + 머지 대기 | 1~2일 (실제 작업 ~10분) | 0일 (선행 머지 완료) |
| InventoryTestWindow.uss 갱신 (B-1/B-2) | 5분 | 약 1분 |
| InventoryTestWindow.cs 메서드 추가 + 변경 (A-1~A-6) | 25분 | 약 5분 |
| 컴파일 / 정합성 검증 (Claude) | 10분 | 약 2분 |
| Unity Editor 검증 (사용자) | 15분 | 약 10분 (Consumable 0개 차단 → 임시 토글 → 9단계 통과) |
| **본인 작업 합계** (협업 대기 제외) | **약 55분** | **약 18분** |

cl177_plan 명세도 매우 높음 + plan 코드 블록을 그대로 적용 가능했음 (cl175 의 CS1628 같은 결함 0). 노소연 협업 의존이 자연 해소된 상태에서 진입하여 lead time 추가 0. 본 CL 의 시간 대부분은 검증 차단점 해소 (Consumable 0개 → 임시 토글).
