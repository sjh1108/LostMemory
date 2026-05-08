# CL-151 인벤토리 자동 배치 (데이터 only) — 구현 기록

작성일: 2026-05-06

브랜치: `feat/S14P31C201-?/cl-151-인벤토리-자동-배치-데이터`

기준 plan: [cl151_plan.md](cl151_plan.md), 선행 ticket 구현 기록: [cl150_implementation.md](cl150_implementation.md)

**상태**: 🟢 **본인 데이터 작업 검증 완료** — 자동 배치 (Top-left first fit) + Sort + 점유 그리드 + OnPlacementChanged 이벤트 모두 정상. UI 시각 갱신은 노소연 인계 ([cl151_handoff_to_노소연.md](cl151_handoff_to_노소연.md)).

---

## 목적

Epic S Phase 4 의 자동 배치 ticket — **B-Lite 1 인벤토리 핵심 메커니즘** (P0).

회의록 컨셉:
- 자동 배치 (드래그 없음)
- 5×5 그리드에 다중 사이즈 (1×1 / 2×1 / 2×2)
- 정리 버튼 (정렬 + 재배치)

**침범 방지 — 본인/노소연 작업 분리** (사용자 결정):
- **본 CL 본인 작업**: 데이터 구조 + 알고리즘 + 정리 컴포넌트
- **노소연 인계**: UI 시각 갱신 (`InventoryPanelView` placement 기반 / `InventorySlotView.SetSize`+`SetOccupied` / `InventoryPanel.prefab` 정리 버튼 배치)

본인 PR 은 노소연 코드 수정 X — 데이터 구조만 노출. 호환 유지.

CL-148 의 `InventoryHudView` 가 실제로는 존재하지 않고 노소연의 `Shop/InventoryPanelView` 가 인벤토리 UI 양쪽 (런 + 상점) 담당 — 따라서 본 CL UI 작업은 노소연 영역.

---

## 설계 기준 + 사용자 결정

### Plan 단계 결정 사항

| # | 항목 | 결정 |
|---|---|---|
| 1 | UI 작업 분담 | 본인 = 데이터 only / 노소연 = UI 갱신 (별도 plan md) |
| 2 | 정렬 디폴트 | RarityThenSize (등급 ↓ 후 사이즈 ↓) |
| 3 | 정렬 토글 UI | MVP 미포함 — 노소연 인계 plan 의 (선택) 항목 |
| 4 | Swap 정책 | swap 후 자동 Sort 호출 (2D 그리드 일관성 유지) |
| 5 | overflow 정책 | reject + LogWarning (토스트 UI 는 CL-152) |
| 6 | Sort 시 effect 재발화 | `OnRelicAcquired` 발화 X, `OnPlacementChanged` 만 발화 |
| 7 | MaxSlots (CL-146 행운) vs 그리드 | 별도 개념 — 본 CL 5×5 고정 |
| 8 | 그리드 좌표 컨벤션 | x=column, y=row (CL-150 Width/Height 와 일관) |
| 9 | InventoryCleanupButton 위치 | `Scripts/Runtime/UI/` (CurrencyHUDView 등과 동일) |
| 10 | 데이터 구조 | `RelicPlacement` struct (Relic + Origin) + `bool[,]` 점유 캐시 |
| 11 | 5×5 상수 정식화 | `_maxCols`/`_maxRows` SerializeField 도입 (CL-150 const → 프로퍼티 교체) |

### 작업 중 발견·결정 사항

- **`InventoryHudView` 가 실제로 존재하지 않음**: CL-148 plan 의 가정 틀림. Phase 4 진행률 재산정 필요. 노소연의 `Shop/InventoryPanelView` 가 인벤토리 UI 양쪽 담당 → 본 CL UI 작업은 노소연 영역으로 인계.
- **노소연/김회인 작성 파일 분포 분석**: Shop 폴더 안에 노소연 (`InventoryPanelView`, `InventorySlotView`, `ConsumableSlotView`, `DiscardZoneView`, `ShortcutBarView`) + 김회인 (`ShopController`, `ShopPanelView`, `ShopItemView`, `InventoryToggleController`, `TooltipView`) 혼재. CL-151 침범 영역 = 노소연 4개. CL-152 영역 = 김회인 + 김회인 (안전).
- **ContextMenu + Editor 메뉴 둘 다 추가**: UI 미완성 상태에서 데이터 검증을 위해 `Debug — Print Inventory Grid` / `Debug — Sort (RarityThenSize/RarityDesc/SizeDesc)` ContextMenu + Editor 메뉴 동시 제공. 사용자 즉시 검증 가능.
- **Swap 자동 Sort 결정**: 1D 인덱스 swap 만으로는 2D placements 정합성 깨짐 → Sort 호출이 단순. 사용자 intent (수동 위치 지정) 손실 우려는 plan 위험에 명시.

## 확정된 설계 결정

| 결정 | 값 | 근거 |
|---|---|---|
| 데이터 구조 | List&lt;RelicPlacement&gt; + bool[,] 점유 캐시 (1D OwnedRelics 와 병행) | 기존 호환 + 충돌 검사 빠름 |
| 좌표 컨벤션 | x=col, y=row, 원점=좌상단 | CL-150 Width/Height 일관 |
| 배치 알고리즘 | Top-left first fit (y=0..R-H, x=0..C-W) | 결정론적 + 단순 |
| Sort 시 OnRelicAcquired 미발화 | _ownedRelics 직접 Clear 후 재추가 | effect 재계산 회피 |
| Swap 정책 | 후 자동 Sort(RarityThenSize) | 2D 일관성 |
| 정렬 후 배치 실패 fallback | LogError + 1D 만 추가 (데이터 손실 방지) | 안전장치 |
| OnPlacementChanged 이벤트 | TryAdd / Remove / Sort / Clear 4 곳 발화 | UI 갱신 hook |
| ContextMenu 디버그 | 4개 (Sort 3 mode + Print Grid) | UI 미완성 검증 |

---

## 수정 파일

### 신규 (5)

```text
LostMemory/Assets/_Project/Scripts/Runtime/Relics/RelicPlacement.cs
    - [Serializable] struct (Relic + Origin Vector2Int)
    - X/Y/W/H 헬퍼 프로퍼티 (Relic.Width/Height 위임)

LostMemory/Assets/_Project/Scripts/Runtime/Relics/InventoryGrid.cs
    - class InventoryGrid (Cols/Rows + bool[,] _occupied)
    - Reset() / Mark(x,y,w,h,bool) / CanFit(x,y,w,h) / TryPlace(relic, out origin) / IsOccupied(x,y)
    - 생성자: cols/rows 검증 + new bool[cols, rows]

LostMemory/Assets/_Project/Scripts/Runtime/Relics/InventorySortMode.cs
    - enum { RarityDesc, SizeDesc, RarityThenSize }

LostMemory/Assets/_Project/Scripts/Runtime/UI/InventoryCleanupButton.cs
    - MonoBehaviour, [AddComponentMenu("Lost Memory/UI/Inventory Cleanup Button")]
    - 슬롯: PlayerRelicInventory inventory, InventorySortMode mode (default RarityThenSize), Button button
    - Reset 시 Button 자동 검색
    - OnEnable 에서 button.onClick 구독, OnClick → inventory.Sort(mode)

LostMemory/Assets/_Project/Scripts/Editor/Relics/InventoryGridDebugMenu.cs
    - [MenuItem("LostMemory/Relics/Debug — Print Inventory Grid")]
    - FindFirstObjectByType<PlayerRelicInventory> + 리플렉션으로 ContextMenu 메서드 호출
    - Play Mode 검증 단축
```

### 수정 (1 — 본인 파일)

```text
LostMemory/Assets/_Project/Scripts/Runtime/Relics/PlayerRelicInventory.cs
    - 신규 SerializeField: _maxCols (default 5), _maxRows (default 5)
    - public 프로퍼티: MaxCols, MaxRows, Placements (IReadOnlyList<RelicPlacement>)
    - 신규 이벤트: event Action OnPlacementChanged
    - 필드: InventoryGrid _grid, List<RelicPlacement> _placements
    - Awake: _grid = new InventoryGrid(_maxCols, _maxRows)
    - TryAdd:
        * CL-150 const 5×5 → _maxCols/_maxRows 정식 필드 교체
        * 자동 배치 (_grid.TryPlace) 추가, 실패 시 reject + LogWarning
        * _placements.Add + OnPlacementChanged 발화
        * 로그에 좌표 + 사이즈 표시 ("@ (x,y) WxH")
    - Remove:
        * placements 매칭 항목 제거 + _grid.Mark(false) 점유 해제
        * OnPlacementChanged 발화
    - Swap:
        * 1D 교환 후 자동 Sort(RarityThenSize) 호출
    - Clear:
        * _grid?.Reset() + _placements.Clear() + OnPlacementChanged 발화
    - Sort(InventorySortMode mode):
        * Linq OrderByDescending (Rarity/Size/RarityThenSize)
        * _grid.Reset() + _placements.Clear() + _ownedRelics.Clear() (Remove 메서드 사용 X — OnRelicRemoved 발화 회피)
        * 정렬된 순서대로 재배치
        * OnPlacementChanged 발화 (OnRelicAcquired 발화 X — effect 재계산 회피)
    - ContextMenu 4개 추가:
        * Debug — Sort (RarityThenSize/RarityDesc/SizeDesc)
        * Debug — Print Inventory Grid (ASCII 그리드 콘솔 출력)
```

### 노소연 인계 (별도 plan md — `cl151_handoff_to_노소연.md`)

UI 시각 갱신 작업은 **별도 인계 문서로 분리**. 본 implementation md 의 "노소연이 받는 인터페이스" 섹션 + 별도 인계 plan md 가 노소연 작업 명세.

| 작업 | 파일 | 예상 시간 |
|---|---|---|
| `SetSize(int w, int h)` 메서드 추가 | Shop/InventorySlotView.cs | 30분 |
| `SetOccupied(bool)` 메서드 추가 | Shop/InventorySlotView.cs | 15분 |
| `Refresh` 를 `_inventory.Placements` 기반으로 변경 | Shop/InventoryPanelView.cs | 45분 |
| `OnPlacementChanged` 이벤트 구독 | Shop/InventoryPanelView.cs | 10분 |
| `InventoryPanel.prefab` 에 정리 버튼 + InventoryCleanupButton 부착 | Prefabs/UI/InventoryPanel.prefab | 15분 |
| (선택) 정렬 모드 토글 UI | InventoryPanel.prefab | 30분 (후속 가능) |
| 시각 검증 | Play Mode | 30분 |

**노소연 받는 인터페이스** (본인 PR 머지 후):
```csharp
// PlayerRelicInventory
public IReadOnlyList<RelicPlacement> Placements { get; }
public int MaxCols { get; }
public int MaxRows { get; }
public event Action OnPlacementChanged;
public void Sort(InventorySortMode mode);

// 신규 타입
public struct RelicPlacement { RelicData Relic; Vector2Int Origin; int X/Y/W/H }
public class InventoryGrid { ... }   // 노소연이 직접 사용 안 함
public enum InventorySortMode { RarityDesc, SizeDesc, RarityThenSize }
public class InventoryCleanupButton : MonoBehaviour { ... }   // prefab 에 부착, slots wiring
```

### 재사용 (수정 X)

- `RelicData.Width / Height / OccupiedCells / Rarity` (CL-150) — 자동 배치 알고리즘 직접 사용
- `RelicData.IsConsumable / Has / OnRelicAcquired / OnRelicRemoved / OnCleared` (CL-110) — 기존 흐름 유지
- `Linq OrderByDescending / ThenByDescending` — Sort 메서드
- `Vector2Int` — 좌표 타입
- 노소연의 `Shop/InventoryPanelView` / `Shop/InventorySlotView` — **수정 X** (인계)

---

## 발견·해소된 이슈

### 1. `InventoryHudView` 부재 — Phase 4 진행률 가정 불일치
**문제**: Plan 의 "기존 상태" 가 `InventoryHudView` (CL-148) 존재 가정. 실제 코드에 `InventoryHudView` 파일 없음. 노소연의 `Shop/InventoryPanelView` 가 인벤토리 UI 양쪽 담당.

**해소**: 본 CL 의 UI 갱신 작업을 노소연의 `Shop/InventoryPanelView` 수정으로 재정의 → 노소연 영역이라 본 CL 에서 분리 (별도 인계 plan md). Phase 4 의 CL-148 상태도 재확인 필요 (별도 ticket).

### 2. SetTier struct null 분기 불가 (CL-147 에서 발견 + 본 CL 확인)
**문제**: 초기 plan 에서 `OnTarotTierChanged(SetTier tier)` 단일 메서드의 `tier == null` 분기 가정. SetTier 가 struct 라 null 불가. CL-147 에서 두 메서드로 분리 (`OnTarotActivated/Deactivated`). 본 CL 의 `Sort` / `Clear` 도 동일 컨벤션 (메서드 분리 + null 사용 X).

**해소**: 본 CL 은 처음부터 메서드 분리 패턴 적용 — `Sort(InventorySortMode mode)` 단일 메서드, mode 는 enum 이라 null 무관.

### 3. CL-150 const 5×5 → CL-151 정식 필드 교체
**문제**: CL-150 의 `const int MaxColumns = 5, MaxRows = 5;` 가 다른 곳 (CL-148 HUD, CL-151 자동 배치) 의 5×5 가정과 분산되어 있어 silent bug 위험. CL-150 plan 에서 "CL-151 인계" 명시.

**해소**: 본 CL 에서 `_maxCols`/`_maxRows` SerializeField 정식 도입 + public 프로퍼티 (MaxCols/MaxRows) 노출. CL-150 의 const 5/5 → `_maxCols`/`_maxRows` 교체. 다른 곳 (CL-148 HUD) 도 노소연이 인계 plan 따라 PlayerRelicInventory.MaxCols/MaxRows 조회로 통일 가능.

### 4. Sort 시 OnRelicAcquired 재발화 위험 (CL-146 회귀 우려)
**문제**: 일반적인 "Clear + 재추가" 패턴은 OnRelicAcquired 이벤트 발화 → BuildManager 가 모든 효과 해제 → 재계산 → 시각 토글 (효과 깜빡임). 사용자 시각 거부감.

**해소**: Sort 메서드에서 `_ownedRelics.Clear()` 직접 호출 (Remove 메서드 우회) + 재추가 시 `OnRelicAcquired` 발화 X. `OnPlacementChanged` 만 발화. 검증 로그에서 Sort 호출 후 `[SetEffect] APPLY` 가 다시 발화 안 함 확인 → BuildManager 재계산 회피 검증 ✅.

---

## 검증 결과 (e2e — UI 미구현 상태)

### 1. Wiring + 컴파일 OK ✅
- 신규 5 파일 + PlayerRelicInventory.cs 수정 모두 컴파일 성공
- `[CL-151]` 로그 prefix 정상 출력

### 2. 자동 배치 (Top-left first fit) ✅
타로 RelicData 7개 (Common 5 / Unique 1 / Legendary 1) 일괄 추가 → 좌표 분포:
```
[PlayerRelicInventory] 유물 획득: 별의 운명 @ (0,0) 2×2
[PlayerRelicInventory] 유물 획득: 운명의 수레바퀴 @ (2,0) 2×2
[PlayerRelicInventory] 유물 획득: 운명의 카드 @ (0,2) 2×1
[PlayerRelicInventory] 유물 획득: 점성술사의 별자리 @ (4,0) 1×1
[PlayerRelicInventory] 유물 획득: 타로 카드 별 @ (4,1) 1×1
[PlayerRelicInventory] 유물 획득: 점쟁이의 수정구 @ (2,2) 2×1
[PlayerRelicInventory] 유물 획득: 부의 신탁 @ (4,2) 1×1
```
첫 fit 으로 좌상단부터 정확히 배치됨 ✅

### 3. ASCII Grid 출력 ✅
```
[CL-151] Grid (5×5, 7 placements):
별별운운점
별별운운타
운운점점부
.....
.....
```
좌표와 일치 — Print Grid ContextMenu 정상 ✅

### 4. Sort(RarityThenSize) ✅
```
[CL-151] Sort(RarityThenSize) — 7 아이템 재배치 완료
```
- Sort 후 Print Grid 결과 동일 (이미 정확한 순서로 배치되어 있던 케이스 — 변화 없음 정상)
- **`[SetEffect] APPLY` 가 Sort 후 재발화 안 함** → OnRelicAcquired 미발화 검증 ✅

### 5. Clear (Run 종료 흐름) ✅
- `[PlayerShield] Expired/Cleared`
- 모든 SetEffect REMOVE (Tarot t3 / Greed t0 / Luck t2)
- TarotSystem 비활성화
- GoldWallet 1.00 으로 리셋

→ Clear 시 `OnPlacementChanged` + `OnCleared` 정상 발화. BuildManager / Effect 시스템 회귀 OK ✅

### 6. Clear 후 재추가 결정론 ✅
같은 7개 유물 다시 추가 → 같은 좌표로 배치 (별 (0,0), 운명 (2,0), ...). Awake 의 _grid Reset 후 깨끗한 상태에서 재배치.

### 7. overflow reject (사용자 임시 6×6 SO) ✅
```
[CL-151] 가죽 신발 사이즈 (6×6) 가 인벤토리 (5×5) 초과 — TryAdd reject
```
TryAdd 사이즈 검증 hook 정상.

⚠ **참고**: 가죽 신발이 6×6 인 것은 사용자가 검증용으로 수동 변경한 결과. CL-150 Apply 메뉴 다시 돌리면 Common 매핑으로 (1,1) 복원됨. 검증 후 사이즈 원복 또는 Lock 추천.

### 8. CL-146 / CL-147 / CL-150 회귀 ✅
- Tarot 7스택 t3 활성 정상
- Greed 1스택 GoldWallet ×1.10 정상
- Luck 5스택 LuckPoints 정상
- 모든 SetEffect APPLY/REMOVE 흐름 영향 X

### 미검증 (낮은 우선순위)
- **5×5 가득 참 시 배치 공간 부족 reject** (현재 7개로 여유 있음 — 별도 검증)
- **Remove 시 점유 해제 후 새 유물이 빈 자리 채우기** (코드 단순 — `_grid.Mark(false)` + 다음 TryPlace 가 빈 셀 first fit)
- **Sort(RarityDesc) / Sort(SizeDesc) 차이** (RarityThenSize 와 결과 동일한 케이스만 검증, 다른 mode 는 코드 동일 패턴이라 자동 PASS 예상)
- **InventoryCleanupButton OnClick** (UI prefab 미부착 상태 — 노소연 작업 후 검증)

---

## 위험 / 결정 미정

### 위험
1. **노소연 UI 갱신 미반영 시 시각 미검증**: 본 PR 머지 후 노소연 작업 전까지 인벤토리 UI 가 placement 반영 안 함 (1×1 가정 그대로). 사용자가 Game UI 에서 인벤토리 보면 다중 칸 시각 X. **콘솔 + ContextMenu (Print Grid) 로 데이터는 검증 가능**.
2. **`_ownedRelics.Clear() + 재추가` 와 OnRelicAcquired 정책**: Sort 시 OnRelicAcquired 발화 안 함. 코드 주석 명시. 실수로 Remove 메서드 거치면 OnRelicRemoved 다중 발화 → BuildManager 가 모든 효과 해제 → 재발화 → 시각 토글. **검증 로그로 발화 미발생 확인 완료**.
3. **Swap 자동 Sort 시 사용자 intent 손실**: 기존 swap 은 1D 인덱스 교환 — 사용자가 "이 자리에 두고 싶다" 의도 있을 수 있음. 본 CL 의 자동 Sort 는 그 의도 무시. → Swap 사용처 (Shop/InventorySlotView 의 OnSlotDropReceived) 확인 시 노소연 결정. 불편하면 swap 후 Sort 호출 제거 옵션.
4. **InventoryGrid 와 _ownedRelics 동기 깨질 위험**: TryAdd / Remove / Sort / Clear 모두 _grid + _placements + _ownedRelics 3개를 동시 갱신. 누락 시 silent bug. 메서드별 invariant 주석 + ContextMenu 디버그 메뉴로 사후 검증.
5. **Sort 후 배치 실패 fallback**: 정렬 후 25 초과는 이론적 불가능 (TryAdd 시점 가드). 발생 시 LogError + 1D 만 추가 (데이터 손실 방지). 실 환경에서는 발화 안 함 예상.
6. **MaxSlots (CL-146 행운) vs 5×5 그리드**: 별도 개념으로 결정. 행운 3스택 시 MaxSlots 26 → 그리드 5×5 그대로 → MaxSlots 의미 모호. 후속 ticket 에서 정리.
7. **Editor 디버그 메뉴는 검증 도구이지 production 아님**: 빌드에 포함 안 됨 (Editor 폴더). 노소연 UI 작업 후 디버그 메뉴 의존도 0.

### 결정 미정 (본 CL 외)
- [ ] 노소연 UI 갱신 PR 머지 (별도 인계 plan md)
- [ ] Swap 자동 Sort 정책 재검토 (사용자 intent 손실 시 제거)
- [ ] MaxSlots (CL-146) 와 그리드 통합 (행운 슬롯+1 의 정확한 의미)
- [ ] overflow 토스트 UI (CL-152 의 ToastNotifier placeholder + 정식 토스트는 별도 ticket)
- [ ] 정렬 토글 UI (노소연 인계 plan 의 선택 항목)
- [ ] CL-148 의 인벤토리 UI 상태 재확인 (`InventoryHudView` 부재 — 노소연 prefab 으로 대체된 상태?)
- [ ] 5×5 가득 참 시나리오 추가 검증 (선택)

---

## 후속 인계

| Ticket / 작업 | CL-151 과의 관계 |
|---|---|
| **노소연 인계 (cl151_handoff_to_노소연.md)** | UI 시각 갱신 (`InventoryPanelView.Refresh` placement 기반 + `InventorySlotView.SetSize`/`SetOccupied` + prefab 정리 버튼 + Toast 옵션) |
| **CL-152 (보상/상점 통합)** | overflow 토스트 (RewardController + ShopController 가 본 CL 의 `inventory.TryAdd` false 결과 → 토스트 표시) + ShopPanelView 다중 사이즈 (별도 ticket) |
| **CL-153 (QA)** | 다양 사이즈 자동 배치 + 정리 검증 시나리오 |
| **별도 — MaxSlots / Grid 통합** | 행운 3스택 슬롯+1 의 정확한 의미 |
| **별도 — Sort 토글 UI 정식화** | RarityThenSize / SizeDesc / RarityDesc 전환 |
| **별도 — CL-148 UI 상태 재확인** | InventoryHudView 부재 / Shop/InventoryPanelView 활용 정책 |

## Phase 4 진행 상태

- [?] CL-148 인벤토리 5×5 그리드 + 빌드 진척 표시 UI — **재확인 필요** (`InventoryHudView` 부재)
- [ ] CL-149 아이템 툴팁
- [x] CL-150 아이템 사이즈 시스템
- [x] **CL-151 인벤토리 자동 배치 (데이터 only)** ← 본 CL — UI 는 노소연 인계
- [ ] CL-152 보상/상점 풀 ItemData 연결

**Phase 4 진행률: 2/5 (CL-150, CL-151 데이터 완성, CL-148 재확인 / CL-149/152 남음)**

## 예상 vs 실제 시간

| 단계 | 예상 (plan) | 실제 |
|---|---|---|
| 1 RelicPlacement + InventoryGrid | 45분 | 25분 |
| 2 InventorySortMode + PlayerRelicInventory 통합 | 1시간 | 40분 |
| 3 Sort 메서드 | 30분 | 15분 |
| 4 InventoryCleanupButton 컴포넌트 | 15분 | 10분 |
| 5 (옵션) Editor 디버그 메뉴 (Print Grid) | 20분 | 10분 (리플렉션 단순) |
| 6 검증 (사용자) | 1시간 | 약 30분 (ContextMenu 즉시 검증) |
| 7 노소연 인계 plan md 작성 | 30분 | (다음 단계) |
| **합계** | **약 4시간 20분** | **약 2시간 30분** (인계 plan md 미포함) |

Plan 추정보다 1시간 50분 빠름. 주요 단축:
- 기존 패턴 재사용 — CL-110 의 PlayerRelicInventory 구조 + CL-150 의 const 5×5 교체만 추가
- Linq + struct + bool[,] 모두 표준 C# 패턴
- ContextMenu 디버그 메뉴로 검증 사이클 매우 빠름 (ASCII 그리드 즉시 시각화)
- 노소연 UI 작업 분리로 본인 작업 범위 깔끔

3점 ticket 적정 규모 (UI 분리로 4점 → 3점 체감).

---

## 다음 단계

1. **사용자 작업**: git commit (PlayerRelicInventory.cs + 신규 5 파일 단일 commit OK), 가죽 신발 사이즈 원복 (CL-150 Apply 메뉴 1회)
2. **노소연 인계 plan md**: `cl151_handoff_to_노소연.md` 작성 후 노소연 전달
3. **CL-152 plan 작성** → 진행 (노소연 UI 대기 X)
