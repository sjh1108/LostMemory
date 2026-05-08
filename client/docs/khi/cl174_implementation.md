# CL-174 InventoryTestWindow 셸 + Play 모드 가드 — 구현 기록

작성일: 2026-05-06

브랜치: `feat/S14P31C201-409/cl-174-inventory-test-window-셸`

기준 plan: [cl174_plan.md](cl174_plan.md), 선행 ticket 구현 기록: [cl173_implementation.md](cl173_implementation.md)

**상태**: 🟢 **검증 완료** — 메뉴 등록 / 빈 EditorWindow 열림 / Play 모드 가드 정상 (Edit 모드 안내 문구 표시 + Body 비활성, Play 모드 진입 시 안내 사라지고 Body 활성, Play 종료 시 복원). 도메인 리로드 회복도 OK.

---

## 목적

Epic U 인벤토리 테스트 도구 트랙 첫 ticket. 디자이너/테스터가 던전 클리어 → 보상 추첨 흐름을 우회해 즉시 RelicData 추가/제거하고 효과/세트/스택 검증할 별도 EditorWindow 신설. 본 CL = 셸 + 메뉴 등록 + Play 모드 가드 + Player 자동 검색 hook 까지.

**해결되는 문제**:
- 디자이너가 보상 흐름 거치지 않고 즉시 인벤토리 상태 조작이 필요한데, 그런 도구가 없어 매 검증마다 보스 클리어 반복
- BalanceEditor 와 별도 도구가 필요한 이유: 인벤토리는 Play 모드 전제(인스턴스 조작) 라 BalanceEditor 의 Edit 모드 SO 편집 흐름과 책임 분리가 자연스러움

---

## 설계 기준 + 사용자 결정

### Plan 단계 결정 사항
- **asmdef 정책 = asmdef 없음 ((a))** — Runtime asmdef 미존재 환경에서 Editor 가 PlayerRelicInventory 직접 호출하려면 같은 어셈블리에 있어야 함. Editor 폴더(어떤 깊이든) 안의 스크립트는 Unity 가 자동으로 Editor-only 컴파일.
- **CL-174 가 CL-182 정의(Player 자동 검색) 흡수** — 셸 단계에서 hook 까지 마련, CL-175 가 트리/슬롯 시작 시 즉시 사용 가능.
- **UI 클래스 접두 = `.iv-`** — BalanceEditor 의 `.be-` 와 충돌 회피.
- **namespace = `LostMemory.Editor.InventoryTest`** — 기존 Editor 도구 컨벤션(`LostMemory.Editor.[FolderName]`) 일치. 추후 asmdef 도입 시 이전 비용 작음.

### 작업 중 사용자 결정 사항
- **MenuItem 문자열 = `LostMemory/Inventory Test Window`** (Tools/ prefix 없음)
  - 근거: cl174_plan.md 는 `Tools/LostMemory/...` 로 적혀 있었으나, 코드베이스 검증 결과 기존 모든 메뉴 항목(BalanceEditor / Relics / Shop / UI) 이 `LostMemory/...` 컨벤션 사용 중. `Tools/` prefix 둔 메뉴 0개.
  - 사용자 결정으로 코드베이스 컨벤션 따름.

### 작업 중 발견 사항
- **cl174_plan.md 의 MenuItem 문자열이 코드베이스 컨벤션과 불일치** — 검증 단계에서 발견하고 사용자 결정 후 수정.
- **PlayerRelicInventory 에 `MaxSlotsChanged` 이벤트도 존재** — plan 명세에 없는 추가 이벤트. CL-175+ 에서 슬롯 갱신 시 사용 가능. 본 CL 셸 단계에서는 무관.
- **BalanceEditor asmdef references=[] 와 모순 X** — Editor asmdef 가 Runtime SO 클래스를 직접 참조하지 않는 정책. 본 CL 의 (a) 안 채택은 별개 도구라 정책 충돌 없음 (BalanceEditor 와 InventoryTest 는 책임 분리).

## 확정된 설계 결정

| 결정 | 값 | 근거 |
|---|---|---|
| asmdef 정책 | asmdef 없음 (Editor 폴더 자동 분리) | Runtime asmdef 미존재 + 직접 참조 필요 |
| MenuItem 문자열 | `LostMemory/Inventory Test Window` | 코드베이스 컨벤션 (사용자 결정) |
| Player 자동 검색 | 셸 단계에 포함 (CL-182 흡수) | CL-175 즉시 사용 / 단일 Player 가정 |
| Play 모드 가드 | `EditorApplication.playModeStateChanged` 구독 | EnteredPlayMode/EnteredEditMode 시 UpdateModeView |
| UI 클래스 접두 | `.iv-` | BalanceEditor `.be-` 충돌 회피 |
| 폴더 | `Editor/InventoryTest/` + `Resources/` | 기존 BalanceEditor 패턴 동일 |
| namespace | `LostMemory.Editor.InventoryTest` | Editor 도구 컨벤션 |
| UXML element name 컨벤션 | `BodyContainer / LeftPanel / RightPanel / ModeHint` | CL-175+ 진입점 안정성 |
| Window minSize | (600, 400) | BalanceEditor 의 (800, 500) 대비 컴팩트 |

---

## 수정 파일

### 신규 (Claude — 3)

| 경로 | 내용 |
|---|---|
| `LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/InventoryTestWindow.cs` | EditorWindow 셸. `[MenuItem]` / `Open()` / `OnEnable / OnDisable / CreateGUI` / `OnPlayModeChanged` / `UpdateModeView` / `RefreshPlayerInstances` |
| `LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/Resources/InventoryTestWindow.uxml` | UI 구조 (Toolbar + ModeHint Label + BodyContainer[Left/Right]) |
| `LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/Resources/InventoryTestWindow.uss` | `.iv-*` 스타일 셀렉터 |

### 무수정 (참고)

- `Runtime/Relics/PlayerRelicInventory.cs` — Editor 가 Find + 호출만, 본 CL 셸 단계에선 변수 보유만
- `Runtime/Relics/PlayerConsumableInventory.cs` — 동일
- `Runtime/Relics/RelicData.cs` — IsConsumable 플래그는 CL-175+ 에서 사용
- BalanceEditor 전체 — 별개 Window, 영향 0

### 재사용 (참고)

- BalanceEditorWindow 셸 패턴 (`Open` / `CreateGUI` / `Resources.Load` / `CloneTree` / `styleSheets.Add`)
- BalanceEditorWindow.uxml / .uss 의 Toolbar + Body[Left/Right] 구조

---

## 발견·해소된 이슈

### 1. MenuItem 컨벤션 불일치 (사용자 결정으로 해소)
**문제**: cl174_plan.md 명세는 `[MenuItem("Tools/LostMemory/Inventory Test Window")]` (Tools/ prefix 포함). 코드베이스 검증 결과 기존 모든 메뉴 항목(BalanceEditor / Relics 4개 / Shop 6개 / UI 2개 / Scene 2개) 가 `LostMemory/...` 또는 `Lost Memory/...` 만 사용. `Tools/` prefix 사용처 0.

**해소**: 사용자 결정으로 plan 명세 대신 코드베이스 컨벤션 따름. `[MenuItem("LostMemory/Inventory Test Window")]` 로 확정.

**향후 영향**: master plan / cl174_plan.md 의 MenuItem 문자열 표기는 갱신 권장 (이번 implementation 도 본 결정 기록으로 충분).

---

## 검증 결과

### 1. CS 빌드 ✅
- `InventoryTestWindow.cs` 컴파일 OK (`PlayerRelicInventory` / `PlayerConsumableInventory` 직접 참조 — Editor 폴더 규칙으로 Assembly-CSharp-Editor 자동 분리 확인)
- UXML/USS Resources 경로 일치 (`Editor/InventoryTest/Resources/InventoryTestWindow.uxml` / `.uss`)
- Grep 검증: `InventoryTestWindow` 토큰 매칭 = CS 파일 1곳 (다른 코드 영향 없음)
- git status: 신규 폴더 `?? Editor/InventoryTest/` 만, Runtime 무수정 확인

### 2. 메뉴 등록 ✅
- `LostMemory > Inventory Test Window` 메뉴 표시 (사용자 보고)

### 3. Window 열림 ✅
- 메뉴 클릭 → 빈 EditorWindow 열림
- Toolbar / ModeHint Label / BodyContainer[Left/Right] 구조 정상

### 4. ★ Play 모드 가드 (사용자 보고: "시작하면 노란 글씨 없어지고 끄면 다시 켜지고") ✅
- Edit 모드 → 안내 문구 (노란 이탤릭) 표시 + Body 비활성
- Play 모드 진입 → 안내 문구 숨김 + Body 활성 + `RefreshPlayerInstances()` 호출
- Play 종료 → 안내 문구 재표시 + Body 비활성
- Window 닫고 Play 진입 후 메뉴 재오픈 → 정상 동작 (도메인 리로드 회복)

### 미검증 (CL-175+ 영역)
- `_relicInv` / `_consumeInv` 가 실제로 Player 인스턴스를 잡는지 — 트리/슬롯 표시가 없는 셸 단계라 시각 검증 불가. CL-175 진입 시 첫 동작으로 자연스레 검증됨.

---

## 위험 / 결정 미정

### 위험
1. **Runtime 클래스 직접 참조**: `PlayerRelicInventory` / `PlayerConsumableInventory` Runtime API 변경 시 본 도구도 영향. asmdef 분리 X 결정의 trade-off, 컴파일 오류로 즉시 발견 가능.
2. **단일 Player 가정**: `FindAnyObjectByType` 1개만 반환. 멀티플레이/2명 디버깅 필요 시 Player 드롭다운 후속 ticket.
3. **Play 모드 도메인 리로드**: `_relicInv` 참조가 Play 진입 시 stale. `OnPlayModeChanged` → `RefreshPlayerInstances()` 매번 재검색으로 대응.
4. **Window 닫힌 채 Play 진입**: `OnEnable` 미구독 → 다음 Open 시 `CreateGUI` / `UpdateModeView` 가 현 모드 반영. 정상 동작 검증됨.
5. **UXML element name 안정성 (CL-175+ 의존)**: `BodyContainer` / `LeftPanel` / `RightPanel` / `ModeHint` 이름 변경 시 후속 ticket 영향. 본 ticket 컨벤션 확정.
6. **CL-174 ↔ CL-182 정의 통합**: master plan 갱신으로 CL-182 = "셸 + Player 자동 검색" 흡수 완료. Epic V (CL-183~187) 는 검색/드래그앤드롭/Quick Add 등 확장 기능만 별도 ticket.

### 결정 미정 (본 CL 외)
- [ ] CL-175: 좌측 RelicData 트리 + 우측 인벤토리 슬롯 표시
- [ ] CL-176: 더블클릭 추가 / 우클릭 제거 / Clear 버튼
- [ ] CL-177: Consumable 4슬롯 별도 영역 + 슬롯 지정
- [ ] Epic V (CL-183~187): 검색 / 드래그앤드롭 / Quick Add 프리셋
- [ ] Runtime asmdef 신설 (별도 큰 ticket) — 본 도구도 그때 별도 asmdef 로 이전 가능

---

## 후속 인계

| Ticket | CL-174 와의 관계 |
|---|---|
| **CL-175 (좌측 트리 + 우측 슬롯)** | LeftPanel 에 RelicData 트리, RightPanel 에 슬롯 시각화. 본 CL 의 `_relicInv` / `_consumeInv` 즉시 사용 |
| **CL-176 (동작 — 추가/제거/Clear)** | `_relicInv.TryAdd / Remove / Clear` 호출. `OnRelicAcquired` 등 이벤트 구독으로 자동 갱신 |
| **CL-177 (Consumable 4슬롯)** | `_consumeInv.TryAdd / Remove(int slot)` 호출. SlotCount=4 const 활용 |
| **Epic V (CL-183~187)** | 검색 / 드래그앤드롭 / Quick Add 프리셋 — 본 CL 셸 위에 기능 확장 |

---

## 예상 vs 실제 시간

| 단계 | 예상 (plan) | 실제 |
|---|---|---|
| UXML 작성 | 10분 | 약 3분 (plan 코드 그대로 복제) |
| USS 작성 | 10분 | 약 3분 (동일) |
| InventoryTestWindow.cs 작성 | 25분 | 약 5분 (plan 코드 그대로 + MenuItem 컨벤션 결정 반영) |
| 컴파일 / Grep 검증 (Claude) | 10분 | 약 3분 |
| 사용자 결정 (MenuItem 컨벤션) | (plan 단계) | 추가 약 3분 |
| Unity Editor 검증 (사용자) | 10분 | 약 5분 |
| **합계** | **약 65분** | **약 20분** |

doc 명세도가 매우 높고 코드 패턴이 BalanceEditor 와 거의 동일해서 작성 시간 단축. 사용자 결정 (MenuItem 컨벤션) 만 추가 발생. 2점 ticket 적정 규모이나 인프라 신설 작업이 BalanceEditor (CL-162) 에서 이미 잘 검증돼 있어 재활용 효과 컸음.
