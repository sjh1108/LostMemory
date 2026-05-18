# CL-151 — 인벤토리 자동 배치 (Top-left first fit) + 정리 버튼

## Context

Epic S Phase 4의 마지막 ticket. **B-Lite 1 인벤토리의 핵심 메커니즘**.

회의록 컨셉:
- **자동 배치, 드래그 없음** (B-Lite 1)
- 5×5 그리드에 다중 사이즈 아이템 (1×1 / 2×1 / 2×2)
- **정리 버튼**: 한 번 클릭 → 보유 아이템 재정렬 + 재배치

### 기존 상태 (확인 완료)

- ✅ `PlayerRelicInventory` (CL-110) — 단순 1D `List<RelicData>` 기반, `TryAdd`/`Remove`/`Swap`/`Clear`
- ✅ `RelicData.Width`/`Height`/`OccupiedCells` (CL-150) — 사이즈 정보
- ✅ `InventoryHudView` (CL-148) — 단순 1×1 슬롯 가정 (본 CL 후 수정 필요)
- ❌ 2D 셀 그리드 데이터 구조 — **본 CL 신설**
- ❌ Top-left first fit 배치 알고리즘 — **본 CL 신설**
- ❌ 정리 버튼 (정렬) — **본 CL 신설**

### 본 CL 책임 범위

1. 2D 셀 그리드 자료구조 (`InventoryGrid`)
2. Top-left first fit 배치 알고리즘
3. `PlayerRelicInventory` 통합 — 1D List 유지 + 2D Placement 병행
4. 정리 버튼 + 정렬 기준 (등급/사이즈 토글)
5. `InventoryHudView` 다중 칸 점유 시각화 갱신
6. overflow 처리 (들어갈 자리 없음)

---

## 결정사항

### 1. 데이터 구조 — `RelicPlacement` struct + 2D 그리드 유지

**옵션**:
- (a) `RelicData[,]` 5×5 셀 배열 (다중 셀은 같은 ref 4번 저장)
- (b) **`List<RelicPlacement>` + 점유 비트맵 캐시** ⭐
- (c) `Dictionary<Vector2Int, RelicData>` 키 = 셀 좌표

**채택: (b)**.
- `RelicPlacement` = `(RelicData relic, Vector2Int origin)` — origin = 좌상단 셀
- 점유 캐시 `bool[,] _occupied` — 충돌 검사 빠름
- 1D `List<RelicData>` 와 병행: 기존 코드 호환 유지 + 2D 정보 추가

```csharp
[Serializable]
public struct RelicPlacement
{
    public RelicData Relic;
    public Vector2Int Origin;   // 좌상단 셀 (x=col, y=row)

    public int X => Origin.x;
    public int Y => Origin.y;
    public int W => Relic.Width;
    public int H => Relic.Height;
}
```

### 2. 그리드 좌표 컨벤션

- **(x=column, y=row)** — Vector2Int.x = 가로 인덱스, y = 세로 인덱스
- **원점 = 좌상단 (0,0)**, x 증가 = 오른쪽, y 증가 = 아래
- CL-150 의 Width/Height 와 일관 (Width=가로 셀 수, Height=세로 셀 수)
- 5×5 그리드: x∈[0,4], y∈[0,4]

### 3. 배치 알고리즘 — Top-left first fit

```csharp
public bool TryPlace(RelicData relic, out Vector2Int origin)
{
    int W = relic.Width, H = relic.Height;
    for (int y = 0; y <= MaxRows - H; y++)
    {
        for (int x = 0; x <= MaxCols - W; x++)
        {
            if (CanFit(x, y, W, H))
            {
                Mark(x, y, W, H, true);
                origin = new Vector2Int(x, y);
                return true;
            }
        }
    }
    origin = default;
    return false;
}

private bool CanFit(int x, int y, int W, int H)
{
    for (int dy = 0; dy < H; dy++)
        for (int dx = 0; dx < W; dx++)
            if (_occupied[x + dx, y + dy]) return false;
    return true;
}
```

복잡도: 5×5 그리드 → 25 × 4 = 100 ops max per item. 무시 가능.

### 4. TryAdd 통합 — 자동 배치

```csharp
public bool TryAdd(RelicData relic)
{
    // 기존 검증 (null, 소모품, 중복)
    if (relic == null || relic.IsConsumable || Has(relic)) return false;

    // CL-150: 사이즈 초과 검증
    if (relic.Width > MaxCols || relic.Height > MaxRows) { ... return false; }

    // CL-151: 자동 배치
    if (!_grid.TryPlace(relic, out var origin))
    {
        Debug.LogWarning($"[CL-151] {relic.name} 배치 공간 부족 (5×5 가득 참)");
        return false;
    }

    _ownedRelics.Add(relic);
    _placements.Add(new RelicPlacement { Relic = relic, Origin = origin });
    OnRelicAcquired?.Invoke(relic);
    OnPlacementChanged?.Invoke();   // 신규 이벤트
    return true;
}
```

### 5. Remove 통합 — 점유 해제

```csharp
public bool Remove(RelicData relic)
{
    int idx = _placements.FindIndex(p => p.Relic.name == relic.name);
    if (idx < 0) return false;

    var p = _placements[idx];
    _grid.Mark(p.X, p.Y, p.W, p.H, false);
    _placements.RemoveAt(idx);
    _ownedRelics.Remove(relic);
    OnPlacementChanged?.Invoke();
    return true;
}
```

### 6. 정리 버튼 — 정렬 + 재배치

```csharp
public enum InventorySortMode { RarityDesc, SizeDesc, RarityThenSize }

public void Sort(InventorySortMode mode)
{
    var sorted = mode switch
    {
        InventorySortMode.RarityDesc      => _ownedRelics.OrderByDescending(r => (int)r.Rarity),
        InventorySortMode.SizeDesc        => _ownedRelics.OrderByDescending(r => r.OccupiedCells),
        InventorySortMode.RarityThenSize  => _ownedRelics
                                                .OrderByDescending(r => (int)r.Rarity)
                                                .ThenByDescending(r => r.OccupiedCells),
        _ => _ownedRelics.AsEnumerable(),
    };

    _grid.Reset();
    _placements.Clear();
    var newOrder = sorted.ToList();
    _ownedRelics.Clear();

    foreach (var relic in newOrder)
    {
        if (_grid.TryPlace(relic, out var origin))
        {
            _ownedRelics.Add(relic);
            _placements.Add(new RelicPlacement { Relic = relic, Origin = origin });
        }
        else
        {
            // 정렬 후에도 안 들어가는 경우 = 사이즈 합이 25 초과 (불가능, 버그)
            Debug.LogError($"[CL-151] 정리 후 {relic.name} 배치 실패");
        }
    }
    OnPlacementChanged?.Invoke();
}
```

**정렬 기준 토글**:
- **디폴트**: `RarityThenSize` (회의록 추천 "둘다 토글" 의 합리적 디폴트)
- 정리 버튼 옆 작은 토글로 전환 (UI 는 옵션, MVP 는 디폴트만)

### 7. 정렬 시 fragmentation 회피 — 큰 것 먼저

`OrderByDescending(r => r.OccupiedCells)` 는 의도적. 2×2 먼저 배치 → 그 다음 2×1 → 1×1 순서로 가면 빈 칸 fragmentation 최소.

**예시 시나리오**:
```
초기: 2×2 (A) + 2×1 (B) + 1×1 (C) ×3 = 4+2+3 = 9칸 사용

랜덤 배치 (큰거 먼저 안 함):
. C . . .       → 2×2 가 들어갈 자리 없을 수 있음
. C . . .
. . . . .
. . . . .
. . . . .

큰 것 먼저:
A A B B C       → 깔끔
A A . . .
. . . . .
. . . . .
. . . . .
```

### 8. InventoryHudView 갱신 (CL-148 보완)

**기존**: 25개 1×1 슬롯, OwnedRelics[i] → slots[i].SetRelic(relic)

**신규**: Placement 기반 다중 칸 시각화.

```csharp
// InventoryHudView.RefreshSlots 변경 (CL-148 → CL-151)
private void RefreshSlots()
{
    // 1. 모두 클리어
    foreach (var slot in slots) slot.Clear();

    // 2. Placement 별 점유 셀들 표시
    foreach (var p in inventory.Placements)
    {
        // 좌상단 슬롯에 아이콘 표시
        int originIdx = p.Y * MaxCols + p.X;
        slots[originIdx].SetRelic(p.Relic);
        slots[originIdx].SetSize(p.W, p.H);   // 신규: 시각적으로 W×H 칸 차지

        // 나머지 칸은 "occupied" 표시 (아이콘 없음, 단 점유 색)
        for (int dy = 0; dy < p.H; dy++)
            for (int dx = 0; dx < p.W; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int idx = (p.Y + dy) * MaxCols + (p.X + dx);
                slots[idx].SetOccupied(true);
            }
    }
}
```

**`InventorySlotView` 확장** (CL-148 보완):
- `SetSize(int w, int h)` — 슬롯 RectTransform 을 w×h 셀 크기로 확장 (또는 아이콘만 확장)
- `SetOccupied(bool)` — 점유된 빈 슬롯 (아이콘 없지만 다른 아이템 위치로 표시 안 됨)

### 9. overflow 정책 — 보상 거부 + 알림

5×5 가득 차면 새 보상 픽 시 `TryAdd` false. 보상 시스템이 어떻게 처리할지:
- (a) 보상 거부 + 알림 ("인벤토리 가득 참")
- (b) 강제 swap (기존 1개 버리기 선택)
- (c) **수령은 되나 배치 안 됨 + 자동 버림 알림** ⭐

**채택: (a)**. 가장 단순. 사용자가 직접 인벤토리 정리 후 다시 픽.

→ `RewardController` 가 `TryAdd` false 시 토스트 메시지 표시. 본 CL 은 inventory 측 reject 만, UI 메시지는 후속.

---

## 핵심 파일

### 신규

| 경로 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Relics/RelicPlacement.cs` | struct (Relic + Origin) |
| `Assets/_Project/Scripts/Runtime/Relics/InventoryGrid.cs` | 2D 셀 그리드 + Top-left first fit |
| `Assets/_Project/Scripts/Runtime/Relics/InventorySortMode.cs` | enum (RarityDesc/SizeDesc/RarityThenSize) |
| `Assets/_Project/Scripts/Runtime/UI/InventoryCleanupButton.cs` | 정리 버튼 컨트롤러 |

### 수정

| 경로 | 변경 |
|---|---|
| `PlayerRelicInventory.cs` | `_grid` / `_placements` 필드, `TryAdd`/`Remove` 자동 배치 통합, `Sort(mode)` 메서드, `OnPlacementChanged` 이벤트, `Placements` 프로퍼티, `MaxCols=5`/`MaxRows=5` 상수 |
| `InventoryHudView.cs` (CL-148) | `RefreshSlots` placement 기반으로 변경 |
| `InventorySlotView` (Shop 또는 HUD 신규) | `SetSize(w, h)` / `SetOccupied(bool)` 추가 |
| `Shop/InventoryPanelView.cs` | (이미 1×1 가정. CL-151 의 다중 칸은 일단 HUD 만 적용. 상점은 별도 ticket — 본 plan 위험에 명시) |

### Prefab 수정

| 경로 | 변경 |
|---|---|
| `InventoryHud.prefab` | "정리" 버튼 추가, 토글 (선택) |
| `InventorySlot_HUD.prefab` | RectTransform 확장 가능 구조 |

---

## 구현 단계

### 1단계: RelicPlacement struct + InventoryGrid (45분)

1. `RelicPlacement.cs` — struct 정의
2. `InventoryGrid.cs`:
   - `bool[,] _occupied` 필드
   - `Reset()` / `Mark(x, y, w, h, bool)` / `CanFit(x, y, w, h)` / `TryPlace(relic, out origin)`
   - 5×5 고정 상수
3. 단위 테스트 (선택, MonoBehaviour 디버그 메뉴):
   - 2×2 추가 → (0,0) 배치
   - 2×1 추가 → (2,0) 배치
   - 1×1 추가 → (4,0) 배치

### 2단계: PlayerRelicInventory 통합 (1시간)

1. 필드 추가:
   ```csharp
   [SerializeField] private int _maxCols = 5;
   [SerializeField] private int _maxRows = 5;
   private readonly InventoryGrid _grid;
   private readonly List<RelicPlacement> _placements = new();
   public int MaxCols => _maxCols;
   public int MaxRows => _maxRows;
   public IReadOnlyList<RelicPlacement> Placements => _placements;
   public event Action OnPlacementChanged;
   ```
2. `Awake` 에서 `_grid = new InventoryGrid(_maxCols, _maxRows)`
3. `TryAdd` 수정 — 자동 배치 추가
4. `Remove` 수정 — 점유 해제
5. `Clear` 수정 — grid + placements 리셋
6. `Swap` — 본 CL 에서는 일단 1D List 만 동작 (B-Lite 1 = 드래그 없음). 2D swap 은 후속.

### 3단계: Sort 메서드 + 정렬 기준 (30분)

§6 코드 그대로. 3가지 mode.

### 4단계: InventoryCleanupButton + UI (30분)

1. `InventoryCleanupButton.cs`:
   ```csharp
   [SerializeField] private PlayerRelicInventory inventory;
   [SerializeField] private InventorySortMode mode = InventorySortMode.RarityThenSize;
   [SerializeField] private Button button;

   private void OnEnable() => button.onClick.AddListener(OnClick);
   private void OnDisable() => button.onClick.RemoveListener(OnClick);
   private void OnClick() => inventory.Sort(mode);
   ```
2. `InventoryHud.prefab` 에 정리 버튼 배치

### 5단계: InventoryHudView placement 기반 갱신 (1시간)

1. `RefreshSlots` 변경 — §8 코드 그대로
2. `InventorySlotView` 에 `SetSize(w, h)` / `SetOccupied(bool)` 추가
   - SetSize: RectTransform.sizeDelta 또는 GridLayoutGroup 칸 병합 (어려우면 아이콘만 확장)
   - SetOccupied: 빈 슬롯 표시 (회색 또는 빈 처리)
3. `OnPlacementChanged` 이벤트 구독 추가

**주의**: GridLayoutGroup 은 셀 병합 안 됨. 두 가지 처리:
- (a) GridLayoutGroup 그대로 + 좌상단 슬롯의 아이콘만 W×H 크기로 표시 (다른 셀들은 빈 슬롯)
- (b) GridLayoutGroup 제거 + 수동 RectTransform 위치 계산
- **채택: (a)** (단순, 시각적 차이 미미)

### 6단계: 검증 (1시간)

```
시나리오 1: 다양 사이즈 자동 배치
- 2×2 (전사의 끈 변형) + 2×1 (분쇄의팔찌) + 1×1 ×5
- 모두 5×5 안에 첫 fit 위치로 배치
- 인덱스 확인

시나리오 2: 가득 참
- 5×5 = 25 칸 모두 사용 (1×1 ×25 또는 2×2 ×6 + ...)
- 다음 보상 시 TryAdd false + LogWarning

시나리오 3: 정리 버튼 (RarityThenSize)
- 무작위로 추가된 6개 아이템
- 정리 클릭 → 전설 → 유니크 → 레어 → 일반 순서, 같은 등급 내 큰 것 먼저
- 시각적으로 좌상단부터 깔끔히 정렬

시나리오 4: 제거 후 자동 정리?
- 2번째 슬롯 아이템 제거 → 빈 칸 발생
- 정리 안 누름 → 빈 칸 유지 (자동 정리 X)
- 정리 누름 → 빈 칸 채워짐

시나리오 5: 정렬 모드 토글
- 디폴트 RarityThenSize
- (옵션) 토글로 SizeDesc 전환 후 정리 → 사이즈만 기준

시나리오 6: HUD 시각화
- 2×2 아이템 → HUD 에서 4칸 점유 (좌상단 아이콘 + 3칸 빈 표시)
- 2×1 → 2칸 점유

시나리오 7: 행운 슬롯 +1 (CL-146 연동)
- inventory.MaxSlots 증가 시 본 CL 의 5×5 그리드 어떻게 확장?
- 본 plan 결정: MaxSlots ≠ MaxCols×MaxRows. 일단 별도 개념. 슬롯 +1 은 후속에서 그리드 확장 또는 별도 단축키바.
```

---

## 위험 / 결정 미정

### 위험

1. **CL-148 InventoryHudView 가 1×1 가정으로 만들어졌음**: GridLayoutGroup 셀 병합 처리 필요. → §5 (a) 옵션 (좌상단 아이콘만 W×H 확장)
2. **Shop/InventoryPanelView 와의 불일치**: 상점 패널은 1×1 가정 + 드래그 가능. 본 CL 의 다중 사이즈 적용 안 함. → **별도 ticket 명시** ("CL-152 또는 후속에서 상점 패널 다중 사이즈 대응")
3. **Swap 미지원**: B-Lite 1 = 드래그 없음 정책이지만, 상점에서 swap 사용 중. 상점에서 swap 시 2D 그리드 일관성 깨짐. → 본 CL 에서는 swap 시 자동 재배치 (Sort 강제 호출) 또는 swap 무시. **권장: swap 후 자동 재배치 호출** (단순).
4. **정리 시 효과 재발화 위험**: Sort 가 _ownedRelics 를 clear → 재추가 시 OnRelicAcquired 발화하면 BuildManager / Effect 재계산 → 효과 토글로 보일 수 있음. → **Sort 는 OnPlacementChanged 만 발화, OnRelicAcquired 발화 X** (effect 변경 없음).
5. **MaxSlots (CL-146 행운) vs 5×5 그리드 충돌**: 본 plan 결정 — 별도 개념. 행운 3스택의 슬롯+1 은 본 CL 의 grid 확장과 무관 (별도 단축키바 또는 후속 검토).
6. **5×5 가득 참 시 보상 처리**: 본 CL 은 reject + LogWarning. UI 토스트는 RewardController 후속.
7. **2×2 가 5×5 에 6개 안 들어감 (5×5=25, 2×2 최대 6=24)**: 사이즈 큰 아이템 다수 시 가득 빨리 참. 정상 게임 흐름.

### 결정 미정

- [ ] 정렬 디폴트 — 본 plan: **RarityThenSize**
- [ ] 정렬 토글 UI — 본 plan: **MVP 미포함 (디폴트만)**, 토글은 후속
- [ ] Swap 지원 여부 — 본 plan: **swap 후 자동 Sort 호출**
- [ ] OnPlacementChanged vs OnRelicAcquired 발화 정책 — 본 plan: **Sort 시 placement 만 발화**
- [ ] HUD 셀 병합 시각화 방식 — 본 plan: **(a) 좌상단 아이콘 W×H 확장**
- [ ] 5×5 가득 참 토스트 UI — **본 CL 미포함** (RewardController 후속)
- [ ] MaxSlots (CL-146) 와 그리드 통합 — **별도** (본 CL 5×5 고정)

---

## 후속 ticket 영향

| Ticket | CL-151 과의 관계 |
|---|---|
| **CL-152 (보상/상점 통합)** | overflow 시 토스트 메시지, 상점 패널 다중 사이즈 대응 |
| **CL-153 (QA)** | 다양 사이즈 자동 배치 + 정리 검증 |
| **별도 ticket: 상점 InventoryPanelView 사이즈 대응** | 본 CL 은 HUD 만, 상점 추후 |
| **별도 ticket: 정렬 토글 UI** | RarityDesc / SizeDesc / RarityThenSize 전환 |
| **별도 ticket: MaxSlots (행운 3스택) 와 그리드 통합** | 슬롯 +1 의 정확한 의미 재정의 |

---

## 예상 시간

| 단계 | 시간 |
|---|---|
| 1단계 (RelicPlacement + InventoryGrid) | 45분 |
| 2단계 (PlayerRelicInventory 통합) | 1시간 |
| 3단계 (Sort 메서드) | 30분 |
| 4단계 (정리 버튼 UI) | 30분 |
| 5단계 (HudView placement 갱신) | 1시간 |
| 6단계 (검증) | 1시간 |
| **합계** | **약 4시간 45분** |

→ 4점 ticket 에 부합 (알고리즘 단순하지만 통합 영역 넓음).

---

## 결정 요청

| # | 질문 | 옵션 | 추천 |
|---|---|---|---|
| 1 | 정렬 디폴트 | RarityDesc / SizeDesc / **RarityThenSize** | **RarityThenSize** |
| 2 | 정렬 토글 UI | 본 CL / 별도 ticket | **별도** (MVP 단순) |
| 3 | Swap 시 정책 | 자동 재배치 / swap 무시 / swap 허용 | **자동 재배치** |
| 4 | HUD 셀 병합 | (a) 아이콘만 확장 / (b) 수동 RectTransform | **(a)** |
| 5 | overflow 토스트 | 본 CL / **별도 ticket** | **별도** |
| 6 | 상점 InventoryPanelView 갱신 | 본 CL / **별도 ticket** | **별도** |

전부 추천대로면 **RarityThenSize + 토글별도 + Swap자동재배치 + 아이콘확장 + 토스트별도 + 상점별도**.

---

## Phase 4 진행률 (CL-151 후)

| Ticket | Plan |
|---|---|
| CL-148 인벤토리 + 진척 UI | ✅ |
| CL-149 아이템 툴팁 | ⏳ |
| CL-150 사이즈 시스템 | ✅ |
| **CL-151 자동 배치 + 정리** | ✅ ← 방금 |

**Phase 4: 3/4 (CL-149 툴팁만 남음)**

---

## 다음 plan

| 옵션 | Ticket | 점수 | 비고 |
|---|---|---|---|
| **A** | CL-149 툴팁 | 2점 | Phase 4 마지막. CL-148 의 hover 활용 |
| B | CL-152 보상/상점 통합 | 2점 | Phase 5. CL-151 overflow 토스트 + 상점 다중 사이즈 |
| C | CL-153 1차 통합 QA | 5점 | Phase 5 후반. plan 보다 실 플레이 비중 |

**추천: A (CL-149)**. Phase 4 마무리 후 Phase 5 진입이 흐름상 자연스러움. 툴팁은 가벼운 ticket 이라 plan 도 짧음.

뭐로 갈까요?
