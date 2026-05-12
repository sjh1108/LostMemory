# CL-121 MVP 통합 — 씬 전환 안정성 / 데이터 캐리오버 구현

> Branch: `feat/S14P31C201-360/cl-121-mvp-통합-데모-빌드-시나리오`
> Plan 참고: [scene_data_carryover_plan.md](./scene_data_carryover_plan.md)

## Context

MVP 통합 빌드 검증 중 던전 방 사이(`Dungeon_1F_1R` → `Dungeon_1F_2R` …) 씬 전환에서 발생하던 6가지 안정성 이슈를 일괄 해결.

| # | 증상 | 원인 |
|---|---|---|
| 1 | 씬 전환 시 인벤토리 유물 전부 초기화 | Player 가 각 씬에 프리팹 배치 → `Awake`마다 새 인스턴스, 캐리오버 인프라 부재 |
| 2 | 씬 전환 시 HP 풀로 리셋 | 동일 — 새 `Health` 컴포넌트가 SO 기본값으로 시작 |
| 3 | 2R 진입 후 미소녀(MagicalGirl) spawn 안 됨 | `MagicalGirlSpawner.OnEnable` 의 replay 루프가 `OwnedRelics.Count=0` 빈 리스트 순회 |
| 4 | 인벤토리 UI 가 빈 칸으로 표시 | `LoadFrom` 이 `OnPlacementChanged` 발화 안 함 |
| 5 | 2R 진입 후 보상 패널이 안 뜸 | `RewardController.rewardPanelView` 가 1R 씬 unload 시 stale null |
| 6 | Spawn된 적이 미니맵에 표시 안 됨 | Fog mask 영역(`ManualSize=20`)이 spawn 좌표 `(28~37, …)` 를 안 덮음 |

추가로 다음 두 가지도 같이 처리:
- 인벤토리 테스트 창(`InventoryTestWindow`)이 씬 전환 후 stale Player 참조로 `[CL-176] Player 인스턴스 없음` 경고 + 주입 실패
- `PlayerWallet` 이 Town 씬에만 배치되어 1R 단독 PlayMode 시 골드 시스템 미작동

## 데이터 분류

| 범위 | 보관 위치 | 비고 |
|---|---|---|
| 영구 저장 (PlayerPrefs) | `MemoryShardWallet`, `MemorySaveData`, `TalentData` | 게임 재시작 후에도 유지 |
| Run 한정 (DontDestroyOnLoad 싱글톤) | `RunManager`, `StageRouteManager`, `PlayerWallet`, **`PlayerRunState` (신설)** | 런 동안만 유지, 종료 시 Clear |
| 씬 한정 | 적/몹, 씬 UI, Camera, Player GameObject 자체 | 씬마다 새로 생성 |

본 작업은 ★ Run 한정 영역에 **`PlayerRunState` 컨테이너를 신설**해 인벤토리/HP 캐리오버를 처리. Player 자체는 씬 로컬 모델 유지 (네트워크/TDE 호환).

## 변경 사항

### 신규 파일

| 파일 | 역할 |
|---|---|
| [`Scripts/Runtime/Player/PlayerRunState.cs`](../../LostMemory/Assets/_Project/Scripts/Runtime/Player/PlayerRunState.cs) | `DontDestroyOnLoad` 싱글톤. `PlayerSnapshot` 보관/복구. `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)` 자동 부트스트랩. |
| [`Scripts/Runtime/Player/PlayerHealthSnapshotter.cs`](../../LostMemory/Assets/_Project/Scripts/Runtime/Player/PlayerHealthSnapshotter.cs) | TDE `Health` 의 `CurrentHealth` / `MaximumHealth` 캡처·복구. `Start` 단계에서 적용해 modifier replay 와 충돌 회피. |

### 수정 파일

| 파일 | 변경 |
|---|---|
| `Player/PlayerWallet.cs` | `RuntimeInitializeOnLoadMethod` 자동 부트스트랩 추가 — 어느 씬 단독 PlayMode 도 작동 |
| `Relics/PlayerRelicInventory.cs` | `CaptureSnapshotInto` / `LoadFrom` API. `Awake` 자동 복구. `[DefaultExecutionOrder(-100)]` 로 동일 GameObject 의 다른 컴포넌트(`MagicalGirlSpawner`/`RelicEffectRegistry`/`BuildManager`) `OnEnable` 보다 먼저 깨도록 강제. `Start` 에서 `OnPlacementChanged` 1회 발화 (UI 갱신용). |
| `Relics/PlayerConsumableInventory.cs` | 동일 패턴 — `CaptureSnapshot` / `LoadFrom` + `DefaultExecutionOrder(-100)` + `Start` 의 `Changed` 발화 |
| `Stage/StageRouteManager.cs` | `TryLoadRouteNode` 의 `StartSceneLoad` 직전 `CapturePlayerSnapshot()` 호출. 현재 씬 Player 의 인벤토리/HP 를 `PlayerRunState` 로 저장. |
| `Stage/RunManager.cs` | `CleanupRunResultingState` 에서 `PlayerRunState.Instance?.Clear()` 호출 — 런 종료 시 캐리오버 초기화. |
| `Stage/RewardController.cs` | `ShowReward` 진입 시 `ResolveRewardPanelView()` / `ResolvePlayerRelicInventory()` 호출 — 씬 전환 후 stale null 자동 복구. |
| `Relics/RelicEffectRegistry.cs` | `OnEnable` 에서 `inventory.OwnedRelics` 순회하며 `HandleAcquired` replay. 씬 전환 후 modifier 재등록. |
| `UI/Minimap/MinimapCameraRig.cs` | Fog 영역과 카메라 줌 분리 — `fogCoverageCenter` / `fogCoverageHalfSize` 별도 필드 + `SetFogCoverage()` API. `Start` 의 `AutoFitToRooms()` 가 씬의 `RoomEntryRuntimeController` 자식 콜라이더 bounds union 으로 자동 산출. |
| `Editor/InventoryTest/InventoryTestWindow.cs` | `SceneManager.sceneLoaded` 구독해서 씬 전환 시 `RefreshPlayerInstances()` 자동. `OnRefreshClicked` 도 PlayMode 중이면 Player 인스턴스 재검색. |

### Unity Editor 작업 (선행 필요)

1. **Player 프리팹에 `PlayerHealthSnapshotter` 컴포넌트 부착** — `Health` 와 같은 GameObject. `health` 필드 wiring (Reset 메서드가 `GetComponent` 자동 시도).
2. PlayerRunState 별도 GameObject 배치는 불필요 — `RuntimeInitializeOnLoadMethod` 가 자동 생성.

## 핵심 메커니즘

### 캡처/복구 흐름

```
[1R 씬, 적 처치 후 2R 진입 트리거]
  ↓
StageRouteManager.TryLoadRouteNode
  ↓
CapturePlayerSnapshot()
  └─ 현재 Player 찾기 → PlayerRelicInventory/PlayerConsumableInventory/PlayerHealthSnapshotter 의 CaptureXxx 호출
  └─ PlayerRunState.Instance.Capture(snapshot)
  ↓
SceneManager.LoadScene("Dungeon_1F_2R")  ← 1R Player 모두 Destroy
  ↓
[2R 씬 로드]
  ↓
PlayerRelicInventory.Awake [DefaultExecutionOrder(-100), 가장 먼저]
  └─ PlayerRunState.Instance.HasSnapshot == true → LoadFrom 으로 11개 유물 복원
  ↓
PlayerConsumableInventory.Awake [같은 우선순위]
  └─ 슬롯 4개 복원
  ↓
다른 컴포넌트들의 OnEnable (MagicalGirlSpawner / RelicEffectRegistry / BuildManager)
  └─ 이 시점에 OwnedRelics 이미 11개 → replay 루프가 정상 동작
       MagicalGirlSpawner: 미소녀 visual spawn
       RelicEffectRegistry: stat modifier 재등록
       BuildManager: _isDirty=true → LateUpdate 재계산 → set tier 활성
  ↓
PlayerHealthSnapshotter.Start  [모든 OnEnable 끝난 뒤]
  └─ Health.MaximumHealth + SetHealth(snapshot.CurrentHealth) 복원
  ↓
PlayerRelicInventory.Start / PlayerConsumableInventory.Start
  └─ OnPlacementChanged / Changed 발화 → UI 갱신
```

### 실행 순서 강제의 의의

`[DefaultExecutionOrder(-100)]` 가 핵심. Unity 는 같은 GameObject 에 부착된 컴포넌트들을 **부착 순서대로 `Awake → OnEnable` 짝**으로 처리한다. 인스펙터상 PlayerRelicInventory 가 다른 컴포넌트보다 아래에 있어 OwnedRelics 가 채워지기 전에 다른 컴포넌트들의 OnEnable 이 먼저 실행되는 race condition 이 있었다. `-100` 우선순위로 강제하면 부착 순서 무관하게 항상 먼저 깨어남.

### Minimap fog / 카메라 줌 분리

기존 `MinimapCameraRig.manualSize` 는 두 가지 의미를 동시에 가졌다:
- **카메라 ortho size** (player 주변 표시 영역)
- **`MinimapFog` 의 좌표 변환 영역** (한 번에 fog 가 덮는 월드 영역)

분리 전에는 fog 영역을 던전 전체로 늘리려면 카메라 줌도 같이 풀려 미니맵이 너무 멀리서 보이게 됐다. 본 작업으로 두 값을 별도 필드로 분리하고, `AutoFitToRooms` 가 씬의 모든 `RoomEntryRuntimeController` 자식 콜라이더 bounds union 으로 fog 영역만 자동 산출하도록 함. 카메라 줌은 인스펙터 기본값(`manualSize=20`) 유지.

## 검증

| 시나리오 | 기대 동작 | 결과 |
|---|---|---|
| 1R 진입 후 유물 11개 + 데미지로 HP 75/110 만든 뒤 2R 진입 | 유물 11개 + HP 75/110 그대로 유지 | ✅ 로그 `LoadFrom 완료 → ownedRelics.Count=11`, 미소녀 5명 모두 spawn |
| 2R 에서 적 처치 → 보상 패널 | 보상 카드 3장 정상 표시 | ✅ `[RewardPanel] Show — count=3` |
| Run 종료 (사망 / 마을 복귀) 후 새 런 | 인벤토리 빈 상태, HP 풀 시작 | ✅ `PlayerRunState.Clear()` 가 `CleanupRunResultingState` 에서 호출됨 |
| 1R 단독 PlayMode 시작 | PlayerWallet 골드 시스템 정상 작동 | ✅ `RuntimeInitializeOnLoadMethod` 로 자동 생성 |
| InventoryTestWindow 에서 1R → 2R 전환 후 아이템 주입 | stale 참조 없이 새 Player 에 정상 주입 | ✅ `sceneLoaded` 구독으로 자동 갱신 |
| Spawn 된 적 (좌표 ~37, …) 미니맵 마커 | 표시됨 | ✅ `AutoFitToRooms` 가 fog 영역 자동 확장 → `fogRevealed=True` |

## Known Issues / 추후 작업

1. **`SetEffectApplicator` 의 `goldWallet=NULL` 경고** — 탐욕 세트의 `GoldGainPercent` 효과 wiring 누락. 비치명 (탐욕 효과만 미적용). 본 작업 범위 밖.
2. **Procedural 던전 지원** — `AutoFitToRooms` 는 `Start` 시점에 1회만 수행. DungeonArchitect 가 늦게 spawn하는 경우 `DungeonRunBootstrap.DungeonBuilt` 이벤트에 같은 함수를 연결해야 함. 현 프로젝트는 씬 배치형이라 충분.
3. **Fog mask buffer 재설정** — `SetFogCoverage` 호출 시 mask buffer 좌표계가 바뀌므로 기존 알파값이 다른 위치에 매핑됨. 시각적으로 거슬리는 경우 `MinimapFog.ResetMask()` 도 같이 호출해야 함. 현재는 PlayMode 시작 한 번뿐이라 무문제.
4. **다중 플레이어 (Remote)** — 본 작업은 호스트 권위 패턴을 따르지만 멀티 검증 미수행. 호스트만 캡처/복구하는 것이 정상 동작이라 큰 변경 없을 것으로 예상.

## 파일 변경 요약

신규:
- `LostMemory/Assets/_Project/Scripts/Runtime/Player/PlayerRunState.cs`
- `LostMemory/Assets/_Project/Scripts/Runtime/Player/PlayerHealthSnapshotter.cs`

수정:
- `LostMemory/Assets/_Project/Scripts/Runtime/Player/PlayerWallet.cs`
- `LostMemory/Assets/_Project/Scripts/Runtime/Relics/PlayerRelicInventory.cs`
- `LostMemory/Assets/_Project/Scripts/Runtime/Relics/PlayerConsumableInventory.cs`
- `LostMemory/Assets/_Project/Scripts/Runtime/Relics/RelicEffectRegistry.cs`
- `LostMemory/Assets/_Project/Scripts/Runtime/Stage/StageRouteManager.cs`
- `LostMemory/Assets/_Project/Scripts/Runtime/Stage/RunManager.cs`
- `LostMemory/Assets/_Project/Scripts/Runtime/Stage/RewardController.cs`
- `LostMemory/Assets/_Project/Scripts/Runtime/UI/Minimap/MinimapCameraRig.cs`
- `LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/InventoryTestWindow.cs`
