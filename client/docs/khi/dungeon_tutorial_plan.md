# 던전 첫 진입 튜토리얼 구현 Plan

## Context

- 사용자 요청: 마을 튜토리얼처럼 던전(전투 영역)에 첫 진입할 때 안내 창이 자동으로 떠야 함.
- 안내 흐름: 조작키 → 방 갇힘 → 몬스터 처치 → 보상 선택 → 인벤토리 → 다음 방 → 포탈.
- 마을 튜토리얼 (`TownTutorialController`, `TutorialPanelView`, `TutorialWorldMarker`) 은 *정적 Page 배열 + 사용자 Next 클릭* 방식. 던전은 게임 이벤트가 단계를 전진시켜야 함 — 이게 핵심 차이.
- 던전 측에는 이미 `RoomEntered`, `ExitDoorsLockRequested`, `RoomCombatStarted`, `RoomCleared`, `RewardSelected`, `ShowPortal`, `DungeonBuilt` 등 hooking 포인트가 풍부히 존재 → 새 코드는 *구독자* 역할만.

## 사용자 결정 사항

- 진행 방식: **이벤트 기반 단계별 자동 진행** (Controls 페이지만 사용자 Next 클릭)
- 표시 빈도: **처음 1회만** + "다시 보지 않기" 토글 + 디버그 리셋
- 추가 단계: **조작키 안내**
- 월드 마커: **사용** (정적 + 동적 Transform 모두)

## 아키텍처 결정

- 새 컴포넌트 `DungeonTutorialController` 추가 (마을 컨트롤러와 분리). 마을 패턴 일반화는 보류 — 데이터 모델이 달라 무리한 공통화는 양쪽을 더 복잡하게 만든다.
- `TutorialPanelView` / `TutorialWorldMarker` 는 **재사용**, 소폭 API 추가만.
- 씬 구조: Town → `Dungeon_1F_1R.unity` (씬 전환) → `Dungeon_1F_2R.unity` → ... → `Dungeon_1F_Boss.unity`. 각 sub-scene 마다 자기만의 `DungeonRunBootstrap` 존재.
- 컨트롤러는 **`Dungeon_1F_1R.unity` (첫 던전 씬) 의 root GameObject** 로 1개만 배치. `Awake()` 에서 `DontDestroyOnLoad` 호출 → 2R, 3R, Boss 씬 전환에도 따라감. 2R 이후 씬에는 아무것도 배치하지 않음. Canvas 도 컨트롤러 GameObject 의 자식으로 두어 함께 살아남게.

## 단계 표

| Step | 페이지 표시 트리거 | 다음 단계로 전진 트리거 | 마커 |
| --- | --- | --- | --- |
| 0 Controls | `DungeonBuilt` 직후 (PlayerPrefs 미세팅 사용자만) | **사용자 Next 클릭** (유일한 수동) | None |
| 1 DoorsLocked | `RoomEntryRuntimeController.ExitDoorsLockRequested` | 1.5초 자동 | None |
| 2 Combat | `RoomEntryRuntimeController.RoomCombatStarted` | 같은 controller 의 `RoomCleared` | 동적 Transform = 가장 가까운 적 (1초마다 재탐색) |
| 3 Reward | `RewardController.IsShowing` 이 false→true 전이 (polling) | `RewardPanelView.RewardSelected` | None (텍스트로 "화면 중앙 보상 패널" 안내) |
| 4 Inventory | Step 3 종료 직후 | `InventoryToggleController.IsOpen` 이 false→true 전이 (polling) 후 0.5초 | None (텍스트로 "I 키" 안내) |
| 5 NextRoom | 클리어된 방 controller 의 `RoomCleared` 직후 + Step 4 완료 | 다음 `RoomEntered` 이벤트 | 동적 Transform = 가장 가까운 활성 `RoomExitWall` 의 부모 |
| 6 Portal | Boss 타입 방의 `RoomCleared` (payload.Data.RoomType == Boss) | 사용자 Close 또는 `RunManager.NotifyBossClearPortalEntered` 시도 감지 | 동적 Transform = `BossClearPortalController.portalVisualRoot` |

**중요**: 첫 방 `BeginRoomEntry` 는 `DungeonRunBootstrap.OnSpawnedManagedObjects` 가 *직접* 호출하므로 "걸어가서 첫 방 진입" 단계는 시스템상 불필요. 대신 Step 0 Controls 페이지에서 사용자가 Next 를 누르면 이미 발화된 (또는 곧 발화될) `ExitDoorsLockRequested` 가 Step 1 을 띄움.

## 데이터 모델

```csharp
public enum DungeonTutorialStep { Controls, DoorsLocked, Combat, Reward, Inventory, NextRoom, Portal }
public enum MarkerMode { None, StaticWorld, DynamicNearestEnemy, DynamicNearestExit, DynamicPortal }

[Serializable]
public struct EventDrivenPage {
    public DungeonTutorialStep Step;
    public string Title;
    [TextArea(2,5)] public string Body;
    public MarkerMode MarkerMode;
    public Vector3 StaticPosition;        // MarkerMode=StaticWorld
    public bool AutoAdvanceAfterSeconds;  // Step 1 (DoorsLocked) 용
    public float AutoAdvanceSeconds;
}
```

Pages 배열을 인스펙터에서 위 순서대로 정의. Step 0 만 `nextButton` 활성 — 나머지는 `View.SetNextButtonVisible(false)` 로 숨김.

## 이벤트 hooking 흐름

```
Start():
    PlayerPrefs.GetInt("DungeonTutorialSeen", 0) == 0 가드
    DungeonRunBootstrap.DungeonBuilt 구독

OnDungeonBuilt():
    FindObjectsOfType<RoomEntryRuntimeController>() 전체에
        RoomEntered / ExitDoorsLockRequested / RoomCombatStarted / RoomCleared 구독
    FindAnyObjectByType<RewardController>() → 매 프레임 IsShowing polling
    FindAnyObjectByType<InventoryToggleController>() → 매 프레임 IsOpen polling
    FindAnyObjectByType<BossClearPortalController>() → portalVisualRoot.activeSelf polling
    ShowPage(Step.Controls)

각 단계 transition 은 위 표대로. 마지막 (Portal) Close 시:
    PlayerPrefs.SetInt("DungeonTutorialSeen", 1)
    모든 이벤트 unsubscribe
    GameObject 자체 Destroy
```

**같은 RoomCleared 구독 우려**: `RewardController.SubscribeAllRoomControllers` 는 closure 로 capture — 우리도 동일 패턴 사용해서 어느 controller 의 RoomCleared 인지 구분 가능.

**구독 타이밍**: `DungeonBuilt` 는 `BeginRoomEntry` *직전*에 발행되므로 DungeonBuilt 콜백에서 구독해도 첫 방의 RoomEntered/ExitDoorsLockRequested/RoomCombatStarted 를 모두 catch. 안전.

## 기존 파일 수정 (최소)

### `TutorialPanelView.cs`
- `public void SetNextButtonVisible(bool visible)` 추가 — `_nextButton.gameObject.SetActive(visible)` 한 줄. 마을 동작 영향 없음 (마을은 호출 안 함).
- `public void SetPrevButtonVisible(bool visible)` 추가 — 같은 패턴 (Prev 도 던전에서 숨김).

### `TutorialWorldMarker.cs`
- 필드 추가: `private Transform _targetTransform;`
- 메서드 추가:
  - `public void SetTargetTransform(Transform target)` — `_targetTransform = target; _hasTarget = (target != null);`
  - `public void ClearTarget()` — `_hasTarget = false; _spriteRenderer.enabled = false;`
- `LateUpdate` 수정: `_targetTransform != null` 이면 `_targetWorld = _targetTransform.position` 매 프레임 갱신 후 기존 로직 진행. 정적 Vector3 호환 유지.
- 마을 호출 (`SetTarget(Vector3)`) 동작 변경 없음.

## 새로 만들 파일

- `Assets/_Project/Scripts/Runtime/Stage/DungeonTutorialController.cs` — 새 파일. 위 데이터 모델 + 이벤트 구독 + 단계 전이 + 마커 동적 lookup.
- `Assets/_Project/Prefabs/UI/DungeonTutorial.prefab` — 마을 `Tutorial.prefab` 을 복제하거나 prefab variant. 폰트 GUID `24e7867240b94b4458d8a28b7e2e0a93` 유지. 던전 UI Canvas 자식으로 배치.

## 동적 마커 lookup 구현

- **NearestEnemy**: Step 2 (Combat) 동안 1초마다 `FindObjectsOfType<Health>()` → 플레이어 거리 기준 최근접 적의 transform 을 `SetTargetTransform`. 적이 죽으면 다음 프레임에 새 적으로 자동 교체.
- **NearestExit**: Step 5 (NextRoom) 동안 1초마다 활성 상태(`gameObject.activeSelf`)인 `RoomExitWall` 의 부모 (실제로는 출구 방향) 의 위치로 갱신.
- **Portal**: Step 6 (Portal) 진입 시점에 `BossClearPortalController.portalVisualRoot.transform` 을 한 번만 잡으면 됨.

## PlayerPrefs / 디버그

- 키: `DungeonTutorialSeen` (마을 `TownTutorialSeen` 과 분리)
- `[ContextMenu("Debug — Reset Dungeon Tutorial")]` 으로 키 삭제 (마을 패턴 그대로)
- `[SerializeField] private bool _alwaysShow` 디버그 플래그

## 미구현 / 후속 작업 (이번 범위 X)

- 보스 방 진입 경고 (사용자가 추가 단계 후보 중 미선택)
- 상점 방 안내 (사용자가 미선택)
- 설정 패널의 "튜토리얼 다시 보기" 옵션 — 마을 튜토리얼도 미보유. 디버그 리셋만 제공.

## 검증 절차

1. 컨트롤러 컨텍스트 메뉴 → "Debug — Reset Dungeon Tutorial" 실행 (또는 `PlayerPrefs.DeleteKey("DungeonTutorialSeen")`)
2. Town → 던전 포탈 진입 → `Dungeon_1F_1R.unity` 로 씬 전환되는 즉시 Step 0 (Controls) 패널 자동 표시 확인. Controls 동안 `Time.timeScale=0` 으로 첫 방 BeginRoomEntry 의 적 공격 모션 차단.
3. Next 클릭 → Step 1 (DoorsLocked) 페이지로 자동 전환 + 1.5초 후 Step 2 (Combat) + 가까운 적에 마커 표시.
4. 모든 적 처치 → Step 3 (Reward) 자동 표시 (RewardController 가 자체적으로 timeScale=0 처리).
5. 보상 카드 선택 → Step 4 (Inventory) 페이지 표시 + "I 키" 안내.
6. I 키 누름 → Step 5 (NextRoom) 페이지 + 출구 방향 마커.
7. `Dungeon_1F_2R.unity` 진입 → DontDestroyOnLoad 로 컨트롤러가 따라감. 새 RoomEntered 수신 → Step 5 종료 + 패널 숨김. Step 2-5 는 *최초 1회만* 표시되도록 `_stepFired` 플래그로 가드.
8. `Dungeon_1F_Boss.unity` 클리어 → Step 6 (Portal) + portal 마커.
9. 포탈 사용 또는 Close → `PlayerPrefs.SetInt("DungeonTutorialSeen", 1)` + GameObject Destroy.
10. 다시 던전 진입 → 패널 안 뜸 확인.
11. "다시 보지 않기" 체크 후 중간 단계에서 Close → 다음 런부터 안 뜸 확인.

## Critical Files

수정:
- `Assets/_Project/Scripts/Runtime/Town/TutorialPanelView.cs` — `SetNextButtonVisible`, `SetPrevButtonVisible` 추가
- `Assets/_Project/Scripts/Runtime/Town/TutorialWorldMarker.cs` — `SetTargetTransform`, `ClearTarget` 추가 + LateUpdate 보강
- `Assets/_Project/Scenes/Dungeon/Dungeon_1F_1R.unity` — DungeonTutorial root GameObject + 자식 Canvas + Tutorial 패널 배치 + wiring. (2R/3R/4R/Boss 씬에는 배치 X — DontDestroyOnLoad 로 따라감.)

신규:
- `Assets/_Project/Scripts/Runtime/Stage/DungeonTutorialController.cs`
- `Assets/_Project/Prefabs/UI/DungeonTutorial.prefab` (또는 마을 `Tutorial.prefab` 인스턴스 그대로 사용)

참조 (수정 X, 구독만):
- `Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeController.cs` — RoomEntered/ExitDoorsLockRequested/RoomCombatStarted/RoomCleared events
- `Assets/_Project/Scripts/Runtime/Stage/RewardController.cs` — `IsShowing` polling
- `Assets/_Project/Scripts/Runtime/Shop/InventoryToggleController.cs` — `IsOpen` polling
- `Assets/_Project/Scripts/Runtime/Stage/BossClearPortalController.cs` — `portalVisualRoot` transform 참조
- `Assets/_Project/Scripts/Runtime/Stage/DungeonRunBootstrap.cs` — `DungeonBuilt` event
