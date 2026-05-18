# CL-151 인벤토리 자동 배치 — UI 갱신 인계 (노소연)

작성일: 2026-05-06
작성자: 김회인 → 노소연

본 CL-151 의 데이터 작업은 김회인이 완료. **UI 시각 갱신** 만 노소연 인계.

기준 문서:
- 데이터 작업 기록: [cl151_implementation.md](cl151_implementation.md)
- 원본 plan: [cl151_plan.md](cl151_plan.md)

---

## 배경 (왜 인계인가)

CL-151 의 책임 범위는 (1) 자동 배치 알고리즘 + (2) 정리 동작 + (3) 다중 칸 시각화. 이 중 **(1), (2) 데이터 작업은 김회인** (PlayerRelicInventory + InventoryGrid + Sort) 완료. **(3) UI 시각화** 만 노소연 영역 (Shop/InventoryPanelView, Shop/InventorySlotView, InventoryPanel.prefab 모두 노소연 작성).

**침범 방지** — 김회인 PR 은 노소연 코드 수정 X. 데이터 인터페이스만 노출.

---

## 김회인이 노출한 인터페이스

### PlayerRelicInventory 새 API

```csharp
namespace LostMemory.Relics
{
    public class PlayerRelicInventory : MonoBehaviour
    {
        // 그리드 dim (Inspector SerializeField — Editor 에서 5/5 디폴트)
        public int MaxCols { get; }   // 가로 셀 수
        public int MaxRows { get; }   // 세로 셀 수

        // 현재 배치 정보 (UI 가 직접 순회)
        public IReadOnlyList<RelicPlacement> Placements { get; }

        // 정리 (정렬 + 재배치) — InventoryCleanupButton 이 호출
        public void Sort(InventorySortMode mode);

        // 배치 변경 시 발화 — UI 가 구독
        public event Action OnPlacementChanged;

        // 기존 API (수정 X)
        public IReadOnlyList<RelicData> OwnedRelics { get; }
        public bool TryAdd(RelicData relic);
        public bool Remove(RelicData relic);
        public void Clear();
        public event Action<RelicData> OnRelicAcquired;
        public event Action<RelicData> OnRelicRemoved;
        public event Action OnCleared;
    }
}
```

### 신규 데이터 타입

```csharp
namespace LostMemory.Relics
{
    [Serializable]
    public struct RelicPlacement
    {
        public RelicData  Relic;
        public Vector2Int Origin;   // 좌상단 셀 (x=col, y=row)
        public int X => Origin.x;
        public int Y => Origin.y;
        public int W => Relic.Width;   // RelicData.Width
        public int H => Relic.Height;  // RelicData.Height
    }

    public enum InventorySortMode { RarityDesc, SizeDesc, RarityThenSize }

    public class InventoryGrid { ... }   // 노소연이 직접 사용 안 함, PlayerRelicInventory 내부용
}
```

### 신규 UI 컴포넌트

```csharp
namespace LostMemory.UI
{
    [AddComponentMenu("Lost Memory/UI/Inventory Cleanup Button")]
    public class InventoryCleanupButton : MonoBehaviour
    {
        [SerializeField] private PlayerRelicInventory inventory;
        [SerializeField] private InventorySortMode mode = InventorySortMode.RarityThenSize;
        [SerializeField] private Button button;
        // OnClick → inventory.Sort(mode)
    }
}
```

---

## 작업 목록 (예상 총 2시간 30분)

### 작업 1: `InventorySlotView.SetSize(int w, int h)` (30분)

**파일**: `LostMemory/Assets/_Project/Scripts/Runtime/Shop/InventorySlotView.cs`

**목적**: 다중 칸 점유 시각화 — 좌상단 슬롯의 RectTransform 또는 아이콘을 w×h 크기로 확장.

**구현 옵션**:
- (a) **추천**: 슬롯의 아이콘 Image 만 W×H 셀 크기로 확장 (RectTransform 그대로). GridLayoutGroup 셀 병합 필요 X.
- (b) RectTransform 자체 확장 + GridLayoutGroup 끄기 + 수동 위치 계산.

**(a) 권장 코드 스케치**:
```csharp
[SerializeField] private RectTransform _iconRect;
[SerializeField] private float _cellSize = 64f;   // 슬롯 1칸 픽셀 (Inspector 입력 또는 prefab 측정)

public void SetSize(int w, int h)
{
    if (_iconRect == null) return;
    _iconRect.sizeDelta = new Vector2(_cellSize * w, _cellSize * h);
    // 좌상단 정렬
    _iconRect.anchorMin = new Vector2(0, 1);
    _iconRect.anchorMax = new Vector2(0, 1);
    _iconRect.pivot     = new Vector2(0, 1);
    _iconRect.anchoredPosition = Vector2.zero;
}
```

### 작업 2: `InventorySlotView.SetOccupied(bool)` (15분)

**목적**: 점유된 빈 슬롯 표시 (좌상단이 아닌 다중 칸의 다른 셀들 — 아이콘 X, 단 빈 셀과 구분).

**구현 옵션**:
- 아이콘 Image hide (`_icon.gameObject.SetActive(false)`)
- 또는 회색 배경 표시
- 또는 클릭 무반응 (드롭 zone 비활성화)

**권장 코드 스케치**:
```csharp
public void SetOccupied(bool occupied)
{
    if (occupied)
    {
        // 아이콘 hide, 클릭 비활성화 등
        if (_icon != null) _icon.enabled = false;
        // 또는 회색 처리: _background.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
    }
    else
    {
        if (_icon != null) _icon.enabled = true;
    }
}
```

### 작업 3: `InventoryPanelView.Refresh` placement 기반 변경 (45분)

**파일**: `LostMemory/Assets/_Project/Scripts/Runtime/Shop/InventoryPanelView.cs`

**목적**: 1D `OwnedRelics[i]` → `slots[i].SetRelic(relic)` 패턴 → 2D `Placements` 순회 패턴.

**현재 (1×1 가정)**:
```csharp
public void Refresh(PlayerRelicInventory inventory, int gold)
{
    for (int i = 0; i < _slots.Length; i++)
    {
        var relic = i < inventory.OwnedRelics.Count ? inventory.OwnedRelics[i] : null;
        _slots[i].SetRelic(relic);
    }
    // ...
}
```

**신규 (다중 칸)**:
```csharp
public void Refresh(PlayerRelicInventory inventory, int gold)
{
    int cols = inventory.MaxCols;
    int rows = inventory.MaxRows;

    // 1. 모든 슬롯 클리어
    foreach (var slot in _slots)
    {
        slot.SetRelic(null);
        slot.SetOccupied(false);
        slot.SetSize(1, 1);
    }

    // 2. Placement 별 점유 셀 표시
    foreach (var p in inventory.Placements)
    {
        if (p.Relic == null) continue;

        // 좌상단 셀 — 아이콘 + 사이즈 표시
        int originIdx = p.Y * cols + p.X;
        if (originIdx >= 0 && originIdx < _slots.Length)
        {
            _slots[originIdx].SetRelic(p.Relic);
            _slots[originIdx].SetSize(p.W, p.H);
        }

        // 나머지 점유 칸 — Occupied 표시
        for (int dy = 0; dy < p.H; dy++)
        for (int dx = 0; dx < p.W; dx++)
        {
            if (dx == 0 && dy == 0) continue;
            int idx = (p.Y + dy) * cols + (p.X + dx);
            if (idx >= 0 && idx < _slots.Length)
                _slots[idx].SetOccupied(true);
        }
    }

    // 3. 골드 표시 (기존 그대로)
    if (_goldText != null) _goldText.text = $"{gold}";
}
```

### 작업 4: `OnPlacementChanged` 이벤트 구독 (10분)

**목적**: 자동 배치 변경 시 자동 Refresh — 사용자가 새 유물 획득하거나 정리 버튼 누르면 즉시 시각 갱신.

```csharp
public void Init(PlayerRelicInventory inventory, int gold)
{
    _inventory = inventory;
    _gold = gold;

    // 슬롯 이벤트 (기존)
    foreach (var slot in _slots) { ... }

    // CL-151 신규 — 배치 변경 시 자동 Refresh
    _inventory.OnPlacementChanged -= HandlePlacementChanged;
    _inventory.OnPlacementChanged += HandlePlacementChanged;

    Refresh(inventory, gold);
}

private void OnDestroy()
{
    if (_inventory != null)
        _inventory.OnPlacementChanged -= HandlePlacementChanged;
}

private void HandlePlacementChanged()
{
    Refresh(_inventory, _gold);   // _gold 는 cached
}
```

### 작업 5: `InventoryPanel.prefab` 정리 버튼 + InventoryCleanupButton 부착 (15분)

**파일**: `Assets/_Project/Prefabs/UI/InventoryPanel.prefab`

**작업**:
1. InventoryPanel prefab 안에 새 Button GameObject 생성 (예: `CleanupButton`, "정리" 텍스트)
2. Button GameObject 에 `Lost Memory > UI > Inventory Cleanup Button` 컴포넌트 부착
3. `Inventory` 슬롯에 PlayerRelicInventory 드래그 (Player root)
4. `Mode` 슬롯 = `RarityThenSize` (디폴트 그대로 OK)
5. `Button` 슬롯은 Reset 시 자동 검색됨 (같은 GameObject 의 Button)
6. UI 위치는 노소연 디자인 — 보통 인벤토리 패널 하단 또는 우상단 코너

### 작업 6 (선택, 후속 가능): 정렬 모드 토글 UI (30분)

**목적**: 사용자가 RarityThenSize / SizeDesc / RarityDesc 전환 가능.

**옵션**:
- (a) Dropdown UI — 3 옵션 + Sort 호출 시 mode 전달
- (b) Toggle 버튼 3개 — 클릭 시 mode 변경 + 즉시 Sort
- (c) **MVP 미포함 — 디폴트 RarityThenSize 만**

권장: **(c)** 본 CL 단순화. 후속 ticket 에서 추가.

### 작업 7: 시각 검증 (30분)

**Play Mode 검증 시나리오**:

1. PlayerRelicInventory 의 Debug Relics To Add 에 다양 사이즈 7개 입력 (Common 5 + Unique 1 + Legendary 1)
2. Play Mode 진입 → InventoryToggleController 등으로 InventoryPanel 열기
3. 인벤토리 시각:
   - 별의 운명 (Legendary 2×2) — 좌상단 4칸 차지
   - 운명의 수레바퀴 (Unique 2×2) — 그 옆 4칸
   - Common 1×1 들 — 우측 컬럼 4번째
4. 정리 버튼 클릭 → 등급 ↓ 후 사이즈 ↓ 순서로 재정렬, 시각 즉시 갱신
5. 5×5 가득 채운 후 추가 유물 시도 → 콘솔 로그만 (UI 변화 X), 사용자가 직접 정리 후 재시도

**김회인 측 ContextMenu 도구로 데이터 검증**:
- PlayerRelicInventory 컴포넌트 ︙ → `Debug — Print Inventory Grid` → ASCII 그리드 콘솔 출력
- ︙ → `Debug — Sort (RarityThenSize)` → Sort 동작 확인

---

## 핵심 파일 변경 요약

### 수정 (3 파일 — 모두 노소연 영역)

| 파일 | 변경 |
|---|---|
| `Shop/InventorySlotView.cs` | `SetSize(int w, int h)` + `SetOccupied(bool)` 메서드 추가 |
| `Shop/InventoryPanelView.cs` | `Refresh` placement 기반 변경 + `Init` 에서 `OnPlacementChanged` 구독 + `OnDestroy` 해제 + `HandlePlacementChanged` |
| `Prefabs/UI/InventoryPanel.prefab` | 정리 버튼 GameObject 추가 + InventoryCleanupButton 컴포넌트 부착 + slots wiring |

### 참조 (수정 X — 김회인 작업)

- `Runtime/Relics/PlayerRelicInventory.cs` — `Placements`, `MaxCols`, `MaxRows`, `Sort`, `OnPlacementChanged` 사용
- `Runtime/Relics/RelicPlacement.cs` — 데이터 타입 (struct)
- `Runtime/Relics/InventorySortMode.cs` — 정렬 모드 enum
- `Runtime/UI/InventoryCleanupButton.cs` — 정리 버튼 컴포넌트

---

## 위험 / 주의사항

1. **GridLayoutGroup 셀 병합 안 됨**: 작업 1 의 (a) 옵션 (아이콘 RectTransform 만 확장) 권장. (b) 옵션 (RectTransform 자체 확장 + GridLayoutGroup 끄기) 은 슬롯 위치 수동 계산 필요 → 복잡.
2. **OnPlacementChanged 이벤트 다중 구독 위험**: Init 에서 `-=` 후 `+=` 순서로 중복 방지. OnDestroy 에서 해제 잊지 말기.
3. **Drop & Swap 정책**: 기존 `OnSlotDropReceived` (드래그 스왑) 호출 시 PlayerRelicInventory.Swap 이 자동 Sort(RarityThenSize) 호출 → 사용자가 swap 한 위치 의도 손실. **노소연 결정**: (a) 그대로 두기 (Sort 가 일관성 보장) (b) Swap 후 Sort 안 호출 (수동 위치 유지) — 김회인 의 PlayerRelicInventory.Swap 메서드 수정 필요. 사용자 UX 우선순위에 따라.
4. **Drop 위치와 placement 좌표 불일치**: 사용자가 (3,2) 슬롯에 드롭해도 Swap 후 Sort 가 (0,0) 부터 재배치. 사용자 confusion 가능. → drag&drop UX 자체 재검토 별도 ticket.
5. **상점 패널 (`ShopPanelView`) 영향**: 본 CL 의 InventoryPanel 만 갱신. 상점 우측 인벤토리 패널은 InventoryPanelView 가 그대로 재사용 (양쪽 사용) → 자동 적용. ShopPanelView 자체 (왼쪽 진열) 는 1×1 가정 유지 (별도 ticket).

---

## 검증 후 인계 완료 보고

작업 끝나면 김회인에게 다음 정보 공유:
1. 시각 검증 결과 (다중 칸 표시 정상 / 정리 버튼 동작 정상)
2. Drop & Swap 정책 결정 (그대로 / Sort 제거)
3. 발견된 이슈 (있다면)

김회인이 cl151_implementation.md 의 "노소연 UI 미반영" 위험 항목을 갱신.

---

## 예상 총 시간

| 작업 | 시간 |
|---|---|
| 1 SetSize | 30분 |
| 2 SetOccupied | 15분 |
| 3 Refresh placement 기반 | 45분 |
| 4 OnPlacementChanged 구독 | 10분 |
| 5 prefab 정리 버튼 배치 | 15분 |
| 6 (선택) 정렬 토글 UI | 30분 (후속 가능) |
| 7 시각 검증 | 30분 |
| **합계** | **약 2시간 30분** (작업 6 제외) |

---

## 참고: 김회인 측 검증 도구 (UI 미완성 시 활용 가능)

- PlayerRelicInventory 우상단 ︙ → `Debug — Print Inventory Grid` (ASCII 콘솔 출력)
- ︙ → `Debug — Sort (RarityThenSize / RarityDesc / SizeDesc)` (정렬 동작)
- ︙ → `Debug — Add all assigned relics` (디버그 추가)
- 메뉴 `LostMemory > Relics > Debug — Print Inventory Grid` (단축키)

이 도구들로 데이터 측은 검증됨. UI 만 맞추면 됨.
