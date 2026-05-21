# CL-230 follow-up — 씬 전환 시 무기 모드 / 마나 캐리오버

> 관련 작업: [CL-121 씬 전환 캐리오버](./cl121_scene_transition_carryover_implementation.md), [CL-230 무기 업그레이드](./cl230_sword_to_dagger_upgrade_plan.md)

## Context

CL-121 에서 인벤토리 / HP 의 씬 전환 캐리오버를 `PlayerRunState` 스냅샷 패턴으로 도입했지만, 이후 추가된 두 시스템이 누락돼 있었음:

| # | 증상 | 원인 |
|---|---|---|
| 1 | 맵 전환 시 마나가 0 으로 초기화 (`PlayerMana.startingMana = 0`) | 새 Player 인스턴스가 prefab 기본값으로 시작, 캐리오버 미적용 |
| 2 | 맵 전환 시 무기 모드가 항상 **Sword** 로 리셋 (Bow/Staff/Dagger/Flamethrower 모두) | `WeaponModeController.Awake → ApplyMode(initialMode=Sword)` 강제 초기화 |

특히 #2 는 **무기 시스템이 2겹** 구조라 원인을 한 번 잘못 진단:

- **`WeaponUpgradeService`** (CL-230) — `Default / Dagger / DaggerNinja` SO 교체 (검 SO 단위)
- **`WeaponModeController`** (별도) — `Sword / Dagger / Bow / Staff / Flamethrower` 모드 토글 (컴포넌트 enable + GameObject SetActive 단위)

처음에 `WeaponUpgradeService.CurrentKind` 를 캡처/복구했지만 Bow/Staff 모드여도 `CurrentKind` 는 늘 `Default` → 복구해도 변화 없음. 진짜 캡처 대상은 **`WeaponModeController.CurrentMode`** 였음.

## 데이터 분류 (CL-121 표 확장)

| 범위 | 보관 위치 | 본 작업에서 추가된 항목 |
|---|---|---|
| Run 한정 (`PlayerRunState` 스냅샷) | `PlayerSnapshot` 구조체 | `HasWeaponMode`, `WeaponMode` (int) |
| 씬 한정 (매 씬 회복) | `PlayerSceneTransitionReset` (신설) | 마나 풀 회복 |

마나는 굳이 스냅샷할 필요 없이 **매 씬 RestoreToMax** 정책으로 단순화 (게임플레이상 의도). 무기 모드는 player 가 픽업/선택한 상태이므로 보존.

## 변경 사항

### 신규 파일

| 파일 | 역할 |
|---|---|
| [`Scripts/Runtime/Combat/PlayerSceneTransitionReset.cs`](../../LostMemory/Assets/_Project/Scripts/Runtime/Combat/PlayerSceneTransitionReset.cs) | `SceneManager.sceneLoaded` (Single 모드만) hook → `PlayerMana.RestoreToMax()` 호출. 무기는 다루지 않음 (스냅샷이 담당). |

### 수정 파일

#### `Scripts/Runtime/Player/PlayerRunState.cs`

`PlayerSnapshot` 구조체에 무기 모드 필드 추가.

```csharp
public struct PlayerSnapshot
{
    // ... 기존 필드 (Placements / ConsumableSlots / Health 등)

    // CL-230: 씬 전환 시 무기 모드 보존.
    // WeaponModeController.ApplyMode 가 내부적으로 WeaponUpgradeService 교체도 호출하므로
    // 이 한 값만 복구하면 Dagger SO 교체까지 자동 전파됨.
    public bool HasWeaponMode;
    public int WeaponMode; // (int) LostMemory.TestKhi.WeaponMode enum.
}
```

#### `Scripts/Runtime/Combat/PlayerMana.cs`

런 종료 / 씬 전환용 공개 API 추가.

```csharp
/// <summary>현재 마나를 최대값으로 회복. 맵 전환 등 라운드 리셋 시 사용.</summary>
public void RestoreToMax()
{
    if (_currentMana == maxMana) return;
    _currentMana = maxMana;
    _recoveryAccumulator = 0f;
    ManaChanged?.Invoke(_currentMana, maxMana);
}
```

#### `Scripts/Runtime/TestKhi/WeaponModeController.cs`

`PlayerHealthSnapshotter` 와 동일 패턴으로 `CaptureInto` + `Start` 코루틴 추가.

```csharp
private void Start()
{
    // 다음 프레임까지 대기 — 다른 컴포넌트 Start 완료 후 안전하게 모드 교체.
    StartCoroutine(RestoreSnapshotNextFrame());
}

private IEnumerator RestoreSnapshotNextFrame()
{
    yield return null;

    PlayerRunState runState = PlayerRunState.Instance;
    if (runState == null || !runState.HasSnapshot) yield break;

    PlayerSnapshot snap = runState.Snapshot;
    if (!snap.HasWeaponMode) yield break;

    WeaponMode targetMode = (WeaponMode)snap.WeaponMode;
    if (targetMode == _currentMode) yield break;

    // ApplyMode 가 WeaponUpgradeService.UpgradeToDagger/RevertToDefault 까지 자동 호출.
    ApplyMode(targetMode, fireEvent: true);
}

public void CaptureInto(ref PlayerSnapshot snapshot)
{
    snapshot.HasWeaponMode = true;
    snapshot.WeaponMode = (int)_currentMode;
}
```

#### `Scripts/Runtime/Stage/StageRouteManager.cs`

`CapturePlayerSnapshot()` 에서 `WeaponModeController.CaptureInto` 호출 추가 (Health/Inventory 캡처 직후).

```csharp
LostMemory.TestKhi.WeaponModeController weaponMode =
    player.GetComponentInChildren<LostMemory.TestKhi.WeaponModeController>(true);
if (weaponMode != null)
{
    weaponMode.CaptureInto(ref snapshot);
}
```

#### `Scripts/Runtime/Combat/WeaponUpgradeService.cs`

멀티 호환을 위해 **싱글톤 제거** + 자기 부모 트리 controller 조회 로 변경 (이전 작업 산물).

```csharp
// (변경 전) 싱글톤 + FindFirstObjectByType — 멀티에서 호스트 한 명만 잡힘
// (변경 후) GetComponentInParent<KhiMeleeComboController>(true) — 자기 플레이어 트리만
```

호출자 (예: `KhiDaggerTeleportController`) 도 `WeaponUpgradeService.Instance` → `GetComponentInParent<WeaponUpgradeService>(true)` 캐시로 갱신.

## 동작 흐름

### 씬 전환 (Single 로드) 시퀀스

```
[기존 씬]
  StageRouteManager.TryLoadRouteNode
    └─ CapturePlayerSnapshot()
         ├─ Health         → snapshot.CurrentHealth / Maximum
         ├─ Inventory      → snapshot.Placements / ConsumableSlots
         └─ WeaponMode     → snapshot.WeaponMode (= Staff)         ← 신규
    └─ NetworkManager.SceneManager.LoadScene(..., Single)

[새 씬]
  Player prefab 새 인스턴스 spawn
    └─ WeaponModeController.Awake
         └─ ApplyMode(initialMode = Sword)                          ← 일단 Sword 로
    └─ PlayerSceneTransitionReset.OnEnable
         └─ SceneManager.sceneLoaded 구독

  SceneManager.sceneLoaded 이벤트 발화
    └─ PlayerSceneTransitionReset.HandleSceneLoaded
         └─ PlayerMana.RestoreToMax()                               ← 마나 풀 회복
    └─ StageRouteManager.HandleSceneLoaded
         └─ PlacePlayersAtSpawn(...)

  다음 프레임 (yield return null 이후)
    └─ WeaponModeController.RestoreSnapshotNextFrame
         └─ snapshot.WeaponMode = Staff → ApplyMode(Staff)          ← Staff 로 자동 복구
              └─ Staff 컴포넌트 enable, Sword 컴포넌트 disable
              └─ (Dagger 였다면) WeaponUpgradeService.UpgradeToDagger() 까지 자동
```

### 런 종료 시

`RunManager.CleanupRunResultingState` → `PlayerRunState.Clear()` 호출 (기존 동작). 다음 런은 빈 snapshot 으로 시작 → `Awake` 의 `initialMode = Sword` 가 그대로 유지.

## 멀티플레이 호환성

- `PlayerSceneTransitionReset`: 플레이어 prefab 자식 → 각 클라이언트가 자기 인스턴스 보유. `GetComponentInParent<PlayerMana>` 로 자기 트리만 처리.
- `WeaponModeController` / `WeaponUpgradeService`: 동일하게 플레이어 prefab 자식. `KhiDaggerTeleportController` 도 자기 부모 트리의 service 캐시.
- `StageRouteManager.CapturePlayerSnapshot`: 첫 번째 발견 Player Character 만 캡처 — 이는 기존 Health/Inventory 캡처 와 동일한 한계로, 추후 `LocalPlayerResolver` 패턴 도입 시 일괄 개선 가능.

## 디버깅 노트 — 진단 우회로

처음에는 `WeaponUpgradeService.CurrentKind` (Default/Dagger/DaggerNinja) 를 캡처/복구했으나 무기 모드가 여전히 Sword 로 리셋. 로그에서 두 가지 단서로 진짜 원인 발견:

1. `[WeaponMode] → Sword` (`WeaponModeController.ApplyMode` from `Awake`) — Awake 마다 강제 초기화
2. `[KhiStaff] Bolt seq=...` — user 가 사용 중인 무기는 Staff 인데 `WeaponUpgradeService.CurrentKind` 입장에선 늘 `Default` (Staff/Bow 는 검 SO 와 무관)

→ 캡처 대상을 `WeaponUpgradeService.CurrentKind` 에서 `WeaponModeController.CurrentMode` 로 변경. `ApplyMode` 가 내부에서 `WeaponUpgradeService.UpgradeToDagger/RevertToDefault` 까지 자동 호출하므로 Dagger SO 까지 한 번에 전파됨.

## 테스트 시나리오

| # | 절차 | 기대 결과 |
|---|---|---|
| 1 | Sword 모드로 던전 진입 → 방 이동 | Sword 유지 (변화 없음) |
| 2 | Bow/Staff 모드로 방 이동 | 다음 방에서 동일 모드 유지 |
| 3 | Dagger SO 픽업 (`UpgradeToDagger`) → Dagger 모드로 방 이동 | 다음 방에서 Dagger 모드 + 단검 SO + 보조 단검 GameObject 활성 모두 유지 |
| 4 | 매 방 진입 시 마나 | 항상 max 로 회복 |
| 5 | 보스 클리어 / 사망 → 다음 런 시작 | 빈 snapshot → Sword 모드로 시작 |

## 부착 위치 체크리스트

- [x] `PlayerSceneTransitionReset` — Player prefab (`TestKhi_MinimalCharacter2D`) 본체 또는 자식
- [x] `WeaponModeController` — Player prefab (이미 존재, 변경 없음)
- [x] `WeaponUpgradeService` — Player prefab 자식 `_WeaponUpgrade` (CL-230 에서 설정 완료)
- [x] `PlayerRunState` — `RuntimeInitializeOnLoadMethod` 로 자동 부트스트랩 (별도 배치 불필요)

`PlayerSceneTransitionReset` 의 `Log Resets` 토글은 진단용. 평상시 OFF 권장.
