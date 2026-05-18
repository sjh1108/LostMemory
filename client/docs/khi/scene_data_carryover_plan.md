# 씬 전환 시 데이터 캐리오버 plan

## Context

`Dungeon_1F_1R` → `Dungeon_1F_2R` 처럼 던전 내 방 사이를 이동할 때 인벤토리 아이템이 초기화되고, 체력도 max로 리셋되는 현상을 사용자가 보고함.

원인을 추적해보면 Player GameObject 가 **DontDestroyOnLoad 가 아니며, 각 씬에 프리팹으로 배치되어 있어** 씬 로드마다 새로 인스턴스화됨. 그 결과:

- [PlayerRelicInventory.cs:75](LostMemory/Assets/_Project/Scripts/Runtime/Relics/PlayerRelicInventory.cs) `Awake()` 가 호출되어 `_ownedRelics` 와 `InventoryGrid` 가 빈 상태로 재초기화
- [PlayerConsumableInventory.cs:17](LostMemory/Assets/_Project/Scripts/Runtime/Relics/PlayerConsumableInventory.cs) `_slots` 배열이 필드 초기화로 매번 빈 배열
- [PlayerHealthStatApplier.cs:24](LostMemory/Assets/_Project/Scripts/Runtime/Combat/PlayerHealthStatApplier.cs) `OnEnable` 이 TDE `Health.MaximumHealth` 를 기본값 기준으로 캡처 (실제 HP 저장처는 TDE `Health` 컴포넌트의 `CurrentHealth`/`MaximumHealth`)

반면 골드/파편/스테이지 진행도는 이미 DontDestroyOnLoad 싱글톤으로 분리되어 있어 정상 유지됨:
- [PlayerWallet.cs:30](LostMemory/Assets/_Project/Scripts/Runtime/Player/PlayerWallet.cs) - 골드, 런 파편
- [MemoryShardWallet.cs](LostMemory/Assets/_Project/Scripts/Runtime/Memory/MemoryShardWallet.cs) - 메타 파편 (영구)
- [RunManager.cs](LostMemory/Assets/_Project/Scripts/Runtime/Stage/RunManager.cs) - 런 상태
- [StageRouteManager.cs](LostMemory/Assets/_Project/Scripts/Runtime/Stage/StageRouteManager.cs) - 경로 진행

사용자 결정:
- **접근**: PlayerWallet 패턴을 그대로 따라 별도 영속 컨테이너(`PlayerRunState`) 신설. Player 자체를 DontDestroyOnLoad 로 만들지 않음 (씬 배치 워크플로우/멀티플레이 권위 패턴 유지).
- **HP 정책**: 일반 씬 전환 시 현재 HP 그대로 유지 (하데스 스타일).

## 데이터 분류 (전체 그림)

씬 전환 시 데이터가 어디에 속하는지 먼저 정리. 신규 추가가 필요한 건 ★ 항목 하나뿐.

### 1) 영구 저장 (PlayerPrefs / JSON) — 게임 재시작 후에도 유지
- `MemoryShardWallet` — 누적 메모리 파편
- `MemorySaveData` (via `MemoryMetaService`) — 메타 보너스, 슬롯, 보상 확률
- `TalentData` (via `TalentSaveService`) — 재능 트리

### 2) Run 한정 (DontDestroyOnLoad 싱글톤) — 던전 한 런 동안 유지, 마을 복귀/사망 시 Clear
- `RunManager` — 런 상태머신, 보스 클리어, 스테이지 진행 카운터
- `StageRouteManager` — 경로 노드 인덱스
- `PlayerWallet` — 런 골드, 런 파편
- ★ **`PlayerRunState` (신설)** — 인벤토리 + Health 스냅샷 (이번 fix 대상)

### 3) 씬 한정 (씬마다 새로 생성, 의도된 리셋)
- 적/몬스터, Spawner
- 씬 로컬 UI (TooltipView, MinimapRig 등)
- KhiHitStopController (씬 로컬 싱글톤)
- Player GameObject 자체 (각 씬에 프리팹 배치)

## 해결 설계

### A. 신규: PlayerRunState 싱글톤
경로: `LostMemory/Assets/_Project/Scripts/Runtime/Player/PlayerRunState.cs`

`PlayerWallet` 와 동일한 패턴 (싱글톤 + `DontDestroyOnLoad`). 씬 전환 직전 Player 의 데이터를 스냅샷에 캡처하고, 다음 씬에서 새 Player 가 깨어날 때 주입한다.

```csharp
public sealed class PlayerRunState : MonoBehaviour
{
    public static PlayerRunState Instance { get; private set; }

    public bool HasSnapshot { get; private set; }
    public PlayerSnapshot Snapshot { get; private set; }

    public void Capture(PlayerSnapshot snapshot) { Snapshot = snapshot; HasSnapshot = true; }
    public void Consume() { HasSnapshot = false; Snapshot = default; } // 복구 후 호출 (선택)
    public void Clear() { HasSnapshot = false; Snapshot = default; }   // 런 종료 시
}

[Serializable]
public struct PlayerSnapshot
{
    public List<RelicPlacementSnapshot> Relics;     // 좌표 포함
    public RelicData[] ConsumableSlots;             // 4칸 그대로
    public int BonusSlots;                          // PlayerRelicInventory._bonusSlots
    public float CurrentHealth;
    public float MaximumHealth;
}

public struct RelicPlacementSnapshot { public RelicData Data; public int X, Y; }
```

> `RelicData` 는 ScriptableObject 라 직렬화 안전. `Snapshot` 자체는 메모리 보관만 하면 되므로 JSON 직렬화 불필요.

### B. PlayerRelicInventory 변경
[PlayerRelicInventory.cs](LostMemory/Assets/_Project/Scripts/Runtime/Relics/PlayerRelicInventory.cs)

- `CaptureSnapshot()` 추가 — `_placements`, `_bonusSlots` 를 스냅샷으로 반환
- `LoadFrom(snapshot)` 추가 — 빈 상태로 시작해서 스냅샷의 (relic, x, y) 를 `_grid.MarkAt(x,y,...)` + `_placements.Add` + `_ownedRelics.Add` 로 직접 복원. `TryAdd` 의 first-fit 경로를 우회해야 원래 위치 보존됨 (그래서 `InventoryGrid` 에 `MarkAt(x,y,w,h)` 또는 `PlaceAt(x,y,relic)` 같은 진입점이 없으면 추가 필요).
- `Awake()` 끝에서 `PlayerRunState.Instance?.HasSnapshot == true` 이면 자동 `LoadFrom` 호출. UI 이벤트 (`OnPlacementChanged`, `OnRelicAcquired`) 발화 정책은 신중히: `OnRelicAcquired` 는 BuildManager 가 카운트하므로 복원 시 발화하지 말고, `OnPlacementChanged` 만 발화해서 UI 만 갱신. (Sort 메서드가 이미 이런 패턴을 쓰고 있음 — 그 패턴 답습.)

### C. PlayerConsumableInventory 변경
[PlayerConsumableInventory.cs](LostMemory/Assets/_Project/Scripts/Runtime/Relics/PlayerConsumableInventory.cs)

- `CaptureSnapshot()` — `_slots` 배열 복사 반환
- `LoadFrom(slots[])` — 배열 그대로 복사
- `Awake` 가 없으니 신설하고 동일 패턴으로 자동 복구

### D. HP 복구
TDE `Health` 컴포넌트가 실제 저장처. `PlayerHealthStatApplier` 와 별개로 새 컴포넌트 `PlayerHealthSnapshotter` (Player 프리팹에 부착) 를 만들고:

- `OnEnable` 에서 `PlayerRunState.Instance?.HasSnapshot == true` 이면 `health.MaximumHealth = snapshot.MaximumHealth; health.SetHealth(snapshot.CurrentHealth)` 로 복구.
- 단, **`PlayerHealthStatApplier.OnEnable` 이 `_baseMaxHealth = health.MaximumHealth` 를 캡처하는 시점보다 먼저 복구되면 baseMaxHealth 가 오염**됨. → `PlayerHealthSnapshotter` 는 `Start` 또는 `OnEnable` 의 [DefaultExecutionOrder] 를 `PlayerHealthStatApplier` 보다 늦게 두거나, `PlayerHealthStatApplier` 가 `_baseMaxHealth` 를 캐싱하는 방식을 살짝 바꿔야 함 (예: 첫 `OnEnable` 시 한 번만, 그것도 모디파이어 없는 SO 값을 직접 참조). 안전한 쪽은 **`PlayerHealthStatApplier` 가 `PlayerStatsData.BaseMaxHealth` 를 직접 인스펙터 참조해서 `_baseMaxHealth` 로 쓰도록 변경** — 그러면 Health 컴포넌트의 현재값과 무관해져서 스냅샷 복구 순서가 자유로워짐.

대안 (더 단순): `PlayerHealthSnapshotter` 가 `Start` 에서 한 프레임 늦게 복구 (`OnEnable` 보다 뒤). `_baseMaxHealth` 가 SO 기준 절대값이라면 그대로 두면 됨.

### E. 캡처 시점
[StageRouteManager.cs](LostMemory/Assets/_Project/Scripts/Runtime/Stage/StageRouteManager.cs) 의 `RequestAdvanceRouteNode` 가 다음 씬 로드를 호출하기 **직전**에 캡처. 이 함수가 권위 게이트도 거치므로 호스트만 캡처해도 멀티 상태 일관됨.

```csharp
// 씬 로드 직전
var player = FindObjectOfType<Character>(); // 또는 RunManager 가 추적 중인 ref
if (player != null && PlayerRunState.Instance != null)
{
    var snap = new PlayerSnapshot {
        Relics            = player.GetComponentInChildren<PlayerRelicInventory>()?.CaptureSnapshot(),
        ConsumableSlots   = player.GetComponentInChildren<PlayerConsumableInventory>()?.CaptureSnapshot(),
        BonusSlots        = ...,
        CurrentHealth     = player.GetComponent<Health>().CurrentHealth,
        MaximumHealth     = player.GetComponent<Health>().MaximumHealth,
    };
    PlayerRunState.Instance.Capture(snap);
}
// 그 다음 SceneManager.LoadScene(...)
```

### F. 런 종료 시 Clear
[RunManager.cs](LostMemory/Assets/_Project/Scripts/Runtime/Stage/RunManager.cs) `CloseResulting()` (또는 `PlayerWallet.Clear()` 호출하는 같은 자리) 에서 `PlayerRunState.Instance?.Clear()` 동시 호출. 그래야 다음 런에서 빈 인벤토리/풀HP 로 시작.

## 수정 대상 파일

- **신규**: `LostMemory/Assets/_Project/Scripts/Runtime/Player/PlayerRunState.cs`
- **신규**: `LostMemory/Assets/_Project/Scripts/Runtime/Player/PlayerHealthSnapshotter.cs`
- **수정**: [PlayerRelicInventory.cs](LostMemory/Assets/_Project/Scripts/Runtime/Relics/PlayerRelicInventory.cs) — `CaptureSnapshot`, `LoadFrom`, `Awake` 자동복구
- **수정**: [PlayerConsumableInventory.cs](LostMemory/Assets/_Project/Scripts/Runtime/Relics/PlayerConsumableInventory.cs) — `CaptureSnapshot`, `LoadFrom`, `Awake` 자동복구
- **수정**: [InventoryGrid.cs](LostMemory/Assets/_Project/Scripts/Runtime/Relics/InventoryGrid.cs) — 좌표 지정 배치 진입점 (`PlaceAt(x,y,relic)`) 이 없으면 추가
- **수정**: [PlayerHealthStatApplier.cs](LostMemory/Assets/_Project/Scripts/Runtime/Combat/PlayerHealthStatApplier.cs) — `_baseMaxHealth` 산정 방식 변경 (선택, 안전을 위해)
- **수정**: [StageRouteManager.cs](LostMemory/Assets/_Project/Scripts/Runtime/Stage/StageRouteManager.cs) — 씬 전환 직전 캡처 호출
- **수정**: [RunManager.cs](LostMemory/Assets/_Project/Scripts/Runtime/Stage/RunManager.cs) — `CloseResulting` 에서 `PlayerRunState.Clear()` 호출

## 씬 배치 작업
- Bootstrap 씬 또는 Town 씬에 `PlayerRunState` GameObject 1개 배치 (PlayerWallet 옆에). `DontDestroyOnLoad` 가 Awake 에서 적용되므로 한 번만 거치면 모든 씬에서 살아남음.

## Verification

1. **씬간 이동 (1R → 2R)**
   - 1R 진입 → 유물 보상 받기 → 데미지 받아 HP 절반으로
   - 2R 입장 직후: 인벤토리에 유물 그대로, HP 절반 그대로 → ✅
2. **보스 클리어 → 다음 스테이지**
   - 같은 검증. HP 캐리오버 확인.
3. **사망 → 결과창 → 재시작**
   - 인벤토리 빈 상태, HP 풀 → ✅ (RunManager.CloseResulting → PlayerRunState.Clear)
4. **마을 복귀**
   - 인벤토리 빈 상태, HP 풀 → ✅
5. **상점 진입/이탈 (Dungeon_1F_Shop ↔ 일반 방)**
   - 구매한 유물이 다음 방까지 유지되는지 확인 (이미 PlayerWallet 패턴이 있으므로 동일하게 동작해야 함)
6. **멀티플레이 호스트/클라 (선택)**
   - 호스트 권위 패턴이 깨지지 않는지: `HostAuthority.IsHost` 게이트 통과 후에만 캡처 호출되는지 확인
7. **인벤토리 그리드 좌표 보존**
   - 1R 에서 (2,3) 위치에 배치한 유물이 2R 에서도 (2,3) 에 있는지 (first-fit 으로 재배치되지 않는지)

## 주의/리스크

- `RelicEffectRegistry` 가 `OnRelicAcquired` 를 구독해 effect 를 등록한다면, 복원 시 중복 등록 위험. → `LoadFrom` 은 `OnRelicAcquired` 발화 없이 데이터만 채우고, **effect 재등록은 별도 경로 (예: 신규 메서드 `OnRelicRestored` 또는 Registry 측의 "재구독"이 필요할 수 있음)**. 구현 시 `RelicEffectRegistry` 의 OnEnable/Awake 가 현재 인벤토리를 읽어 effect 를 빌드하는 방식인지 먼저 확인 필요.
- `BuildManager` 도 동일하게 `OnRelicAcquired/Removed` 카운트 기반이면 복원 후 재집계 한 번 트리거 필요.
- TDE `Health` 의 `SetHealth` 가 OnDeath/UI 이벤트를 의도치 않게 발화시키는지 확인.
