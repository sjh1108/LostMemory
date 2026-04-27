# CL-036 문 열림·다음 방 전환 흐름 구현 계획

## 목적

`CL-036 문 열림·다음 방 전환 흐름 구현` 의 방향을 정리한다.

본 작업은 Epic D (전투 공간 흐름·방 인터랙션) 의 마지막 티켓이며, CL-034 가 진입 시 발행한 `ExitDoorsLockRequested(roomId)` 와 CL-035 가 클리어 시 발행한 `RoomCleared(roomId, RoomData)` 시그널을 받아, 방의 *출구 벽 GameObject 를 잠그고 풀어* player 가 다음 방으로 *물리적으로 걸어 이동* 할 수 있게 한다.

다음 방 진입은 *재사용* — player 가 다음 방의 `RoomEntryZone` 에 닿으면 CL-034 가 만든 흐름 (`BeginRoomEntry`) 이 그대로 재발동. 본 작업은 *current 방의 출구 토글* 만 책임.

후속:
- `CL-047` (런 상태 전이 표) / `CL-048` (런 상태 머신) — 전역 현재 방 추적·런 종료 판정·런 시드 도입.
- `CL-049` (보스방 진입 조건) — 이미 `BossRoomEntryTracker.TryMarkRoomCompleted` 가 본 작업의 `RoomCleared` 흐름에서 자동 호출됨 (CL-035 산출물).
- 분기형 던전 / 출구별 다른 잠금 정책 / 미니맵 UI — 본 작업 비범위. `RoomExitSpec.NextRoomId` 와 `exitAnchorTag` 가 그 자리에 미리 잡혀 있다.

## 사용자 의도 (확정)

> "한 층에 대한 미리 세팅을 마치고 방 클리어시 잠긴 문이 열려서 다른 방 갈 수 있음. 방 전투시 들어가는 건 되지만 나가는 것은 불가능. 클리어 즉시 자동 전환 (문 없이)."

**씬에 미리 배치** + **출구 collider 잠금/해제** + **자동 통과** 패턴. 보스의 *텔레포트* 도 아니고 동적 *Instantiate* 도 아닌, *물리적 통로 개통* 모델.

## 결정 사항

- 방 인스턴스화 패턴 = **씬에 미리 배치**. 한 층의 모든 방 layout prefab 이 *씬 안에 물리적으로 인접 배치*. 카메라는 기존 `KhiPlayerCamera` 가 player 추적 → 방 사이 자연 이동.
- 문 동작 = **출구 벽 GameObject 의 SetActive 토글, 시각 없음**. 디자이너가 자유롭게 prefab 안에서 sprite·collider 셋업. controller 는 GameObject 토글만.
- 다음 방 진입 = **CL-034 흐름 재사용**. player 가 출구 벽이 사라진 통로를 걸어 다음 방의 `RoomEntryZone` 에 닿으면 그 방 controller 가 `BeginRoomEntry` 호출.
- **`RoomDataCatalog` 신설 X** — 한 층 미리 배치라 *id → RoomData 동적 lookup* 불필요.
- **Stage Run 오케스트레이터 신설 X** — 각 방 controller 자율. 전역 추적은 `BossRoomEntryTracker` 가 이미 처리. CL-047/048 시점에 흡수.
- 출구별 다른 잠금 정책 (분기형 던전), 출구 시각 페이드/애니메이션, 카메라 confiner, 여러 층 전환, player 사망/실패 — 모두 본 작업 비범위.
- TDE 원본 미수정.

## 자료구조

### `RoomExitSpec` 확장 (CL-032 산출물에 1 필드 추가)

`Assets/_Project/Scripts/Runtime/Stage/Data/RoomExitSpec.cs`. namespace `LostMemory.Stage.Data`, `[Serializable]`.

| 필드 | 타입 | 의미 |
|---|---|---|
| `exitAnchorTag` | `string` | layout prefab 의 출구 GameObject (`RoomExitWall`) 식별 태그. `RoomEntryAnchor.anchorTag` 와 같은 결. |
| `nextRoomId` | `string` | 이 출구를 통해 이동할 다음 방의 RoomData id. 본 CL 에서는 *읽지 않음* — 후속 procedural / 미니맵 CL 에서 사용. |

### `RoomExitWall` 신규 (MonoBehaviour)

`Assets/_Project/Scripts/Runtime/Stage/RoomExitWall.cs`. namespace `LostMemory.Stage`. `[DisallowMultipleComponent]`, `[AddComponentMenu("Lost Memory/Stage/Room Exit Wall")]`.

- `[SerializeField] string exitTag = string.Empty;`
- `string ExitTag => exitTag;`
- `bool Matches(string candidateTag)` — 정확 일치 + 빈 exitTag 거부.
- 본 컴포넌트 자체는 enable/disable 로직 없음. `RoomEntryRuntimeController` 가 GameObject 단위로 `SetActive(true/false)` 토글.

### `RoomEntryRuntimeController` 확장

`Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeController.cs`.

새 SerializeField:

- `[SerializeField] private RoomExitWall[] exitWalls = Array.Empty<RoomExitWall>();`
- Awake / Reset 에서 `GetComponentsInChildren<RoomExitWall>(true)` 자동 캐시.

흐름 변경:

- `ApplyInitContext` 안 `init.LockExitDoors` 분기 — 기존 `ExitDoorsLockRequested` 시그널 발행에 *앞서* `SetExitWallsActive(true)` 호출.
- `HandleRoomCleared` 안 — 기존 `RoomCleared` 발행 + `bossTracker.TryMarkRoomCompleted` 에 *앞서* `SetExitWallsActive(false)` 호출.

새 private 메서드:

```csharp
private void SetExitWallsActive(bool active)
{
    // 모든 출구 벽 일괄 토글. 출구별 다른 정책은 후속 CL.
    foreach (RoomExitWall wall in exitWalls)
    {
        if (wall != null) wall.gameObject.SetActive(active);
    }
}
```

## 사용 흐름

```text
방 A 진입 (RoomEntryZone)
  controller.BeginRoomEntry
    → ApplyInitContext
        → playerSpawnAnchor 정렬 / facing
        → init.LockExitDoors == true:
            → SetExitWallsActive(true)        ← CL-036 신규 (벽 활성화)
            → ExitDoorsLockRequested 발행
        → BGM stub log
    → BeginEncounter (Wave spawn 시작)
    → RoomEntered 발행

전투 진행 (CL-034 / CL-035 흐름 그대로)
  ...

방 A 클리어 (모든 적 사망)
  tracker.OnRoomCleared 발행
    → controller.HandleRoomCleared
        → SetExitWallsActive(false)            ← CL-036 신규 (벽 비활성화)
        → RoomCleared 발행
        → bossTracker?.TryMarkRoomCompleted

player 가 출구 통로 통과 → 방 B 의 RoomEntryZone 진입
  방 B 의 controller.BeginRoomEntry  ← CL-034 흐름 재사용
    → ApplyInitContext (방 B 의 출구 벽 활성화)
    → BeginEncounter (방 B Wave spawn)
    ...
```

## 코드 구조 (변경 범위)

추가 (1 파일):

```
Assets/_Project/Scripts/Runtime/Stage/
  RoomExitWall.cs                       # 식별 컴포넌트 (exitTag + Matches)
```

수정:

```
Assets/_Project/Scripts/Runtime/Stage/Data/
  RoomExitSpec.cs                       # exitAnchorTag 필드 추가, nextRoomId 유지

Assets/_Project/Scripts/Runtime/Stage/
  RoomEntryRuntimeController.cs         # exitWalls SerializeField + Awake/Reset 자동 캐시
                                        # SetExitWallsActive(bool) private
                                        # ApplyInitContext / HandleRoomCleared 토글 호출
```

(`RoomData`, `RoomEncounterSpec`, `IRoomClearConditionTracker`, `AllEnemiesDefeatedTracker`, `EnemyEncounterSpawner`, `BossRoom*`, `RoomEntryAnchor`, `RoomEntryZone` 는 손대지 않는다.)

## 명명·관례

- namespace `LostMemory.Stage` (RoomExitWall) / `LostMemory.Stage.Data` (RoomExitSpec) — 기존 결 유지.
- `[AddComponentMenu("Lost Memory/Stage/Room Exit Wall")]` — `RoomEntryAnchor` / `RoomEntryZone` 와 동일.
- `Khi*` 접두 미사용.
- TDE 원본 미수정.

## 멀티플레이 고려

- `RoomEntryRuntimeController.IsAuthority` 가드 그대로 유지. SetExitWallsActive 는 controller 의 진입/클리어 흐름 안에서만 호출되므로 자동으로 호스트 권위.
- 클라이언트 동기화 — 1차에는 *씬 안 GameObject SetActive* 가 단일 인스턴스 단위로 동작. 네트워크 CL 시점에 NetworkIdentity / RPC 추가.
- 출구 벽이 *호스트에서 disable 되었는데 클라이언트에는 enable* 인 race condition 은 후속 네트워크 CL 영역.

## 디자이너 수동 작업 (Unity 에디터)

### 1. `RoomData_Sample_Combat_Small_B.asset` 생성

기존 `RoomData_Sample_Combat_Small.asset` 를 복제 → `RoomData_Sample_Combat_Small_B`.

- Room Id: `room_combat_small_sample_b`
- Encounter: 단순화 (Wave 1, Orc 2 정도) — 검증 시간 단축.
- Exits: 비워둠 (마지막 방).

### 2. `CombatRoom_Sample_Small.prefab` 의 출구 벽 자식 추가

prefab edit 모드에서 자식 추가:

```
CombatRoom_Sample_Small
├── EntryAnchor_Default               (CL-034)
├── EntryZone                         (CL-034)
├── SpawnPoints                       (CL-034)
└── ExitWall_East                     (신규 CL-036)
      Components:
        BoxCollider2D (Is Trigger=false, Size 적당히 — 출구 통로를 막는 벽)
        SpriteRenderer (선택 — 잠긴 벽 시각)
        RoomExitWall (Exit Tag = "east")
```

루트의 `Room Entry Runtime Controller` 의 `Exit Walls` 슬롯이 자동 캐시 (Awake 에서). 또는 컴포넌트 헤더 ⋮ → `Reset` 으로 재캐시.

### 3. `CombatRoom_Sample_Small_B.prefab` 신설

Room A 와 동일한 자식 구조 + 차이:

- 루트의 `RoomData` 슬롯 = `RoomData_Sample_Combat_Small_B`.
- 출구 벽 *없음* (마지막 방).
- EntryZone 은 좌측 (Room A 의 출구 통로 근처) 에 배치 → player 가 자연스럽게 진입.

### 4. 검증 씬 셋업

기존 검증 씬 (또는 `Save As` 로 복제):

- Room A 인스턴스 → Position `(0, 0, 0)`
- Room B 인스턴스 → Position `(20, 0, 0)` (또는 충분히 떨어진 곳)
- Room A 의 ExitWall_East 와 Room B 의 EntryZone 사이 *통로* 가 있도록 layout 조정.

## CL-036 완료 기준

수동 (Unity 에디터):

**시나리오 A — 출구 벽 잠금 (진입 시)**
- ▶ Play → Room A 의 EntryZone 진입 → 적 spawn.
- Hierarchy 의 Room A `ExitWall_East` GameObject 가 *active* 인지 확인.
- player 우측 이동 시도 → 벽에 막혀 못 나감.

**시나리오 B — 출구 벽 해제 (클리어 시)**
- 적 모두 죽임 → RoomCleared 발행.
- Hierarchy 의 Room A `ExitWall_East` 가 *inactive* 로 전환.
- player 우측 자유 이동 가능.

**시나리오 C — 다음 방 자동 진입**
- player 가 우측으로 걸어 Room B 의 EntryZone 진입.
- Console 에 Room B 의 `BGM stub` log + (디버그 환경이라면) RegisterEnemy log 발생.
- Room B 의 적 spawn 확인.

**시나리오 D — 출구가 없는 방 회귀**
- Room B 의 prefab 에는 `RoomExitWall` 자식이 없음 → controller `exitWalls` 배열 비움.
- Room B 진입 시 `SetExitWallsActive` no-op → 에러 없음.

자동: 본 CL 은 단순 SetActive 토글이라 EditMode test 가치 낮음 — 1차 작성 안 함.

## 후속 작업

- `CL-047` / `CL-048` 가 *전역 현재 방 추적* + *런 종료 판정* + *런 시드* 흡수.
- `CL-049` (보스방 진입 조건) — 이미 자동 mark wiring 됨, 별도 작업 없음.
- 분기형 던전 / 출구별 잠금 정책 → `RoomExitSpec.LockPolicy` enum + controller 분기 추가.
- 출구 시각 페이드 / 문 애니메이션 → 별도 시각 컴포넌트 (예: `RoomExitWallView`) + 이벤트 hook.
- 카메라 confiner / 방 단위 카메라 영역 — 카메라 시스템 후속 CL.
- 여러 층 (floor) 전환 — 별도 패턴 (계단 / 포털 등) 의 후속 CL.
- procedural 생성 / 미니맵 → `RoomExitSpec.NextRoomId` 와 (필요 시) RoomDataCatalog 도입.
- player 사망 / room failure → CL-014 / CL-048 영역.

## 위험·결정 보류

- **출구별 다른 잠금 정책**: 본 CL 은 *모든 출구 동시 토글*. 분기형 던전 도입 시 `RoomExitSpec.LockPolicy` 같은 필드 + controller 분기.
- **출구 시각 (sprite / 애니메이션)**: 본 CL 은 *GameObject SetActive* 만. 디자이너가 prefab 에 sprite 박으면 자동 보임/사라짐. 페이드 등 연출은 후속.
- **카메라 confiner**: `KhiPlayerCamera` 가 player 자유 추적. 방 단위 카메라 영역은 후속 CL.
- **`RoomExitSpec.NextRoomId` 의 1차 무관성**: 본 CL 에서 *읽지 않음*. 후속 procedural / 미니맵 / 분기형 던전 도입 시 활성화.
- **`RoomExitSpec.exitAnchorTag` 의 1차 무관성**: 현재 controller 는 *모든 ExitWall 일괄 토글*. tag 매칭은 *출구별 정책 분기* 시 활성화.
- **여러 층 전환** / **player 사망** — 본 CL 비범위.
- **멀티플레이 동기화**: 호스트 권위 가드 그대로 유지, RPC wiring 은 네트워크 CL 영역.

## 구현 결과

(2026-04-27 기준 1차 코드 구현 완료 — Unity 에디터 검증은 사용자 수동 단계)

추가된 런타임 코드:

- `Assets/_Project/Scripts/Runtime/Stage/RoomExitWall.cs` — 식별 컴포넌트. `exitTag` + `Matches`.

수정된 코드:

- `Assets/_Project/Scripts/Runtime/Stage/Data/RoomExitSpec.cs` — `exitAnchorTag` 필드 + 접근자 추가. `nextRoomId` 유지. Tooltip 명시.
- `Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeController.cs` — `exitWalls` SerializeField + Awake/Reset 자동 캐시 + `SetExitWallsActive(bool)` private + `ApplyInitContext` / `HandleRoomCleared` 토글 호출.

자산·prefab·씬:

- `RoomData_Sample_Combat_Small_B.asset`, `CombatRoom_Sample_Small_B.prefab`, Room A prefab 의 ExitWall 자식 추가, 검증 씬에 두 Room 인스턴스 배치 — 디자이너/구현자 수동 단계.

## 현재 검증 결과

2026-04-27 Unity 에디터 수동 검증 통과.

검증 셋업:
- Room A: 기존 `CombatRoom_Sample_Small.prefab` + `ExitWall_East` 자식 추가 (BoxCollider2D Is Trigger=false, Room Exit Wall 컴포넌트 Exit Tag=`east`).
- Room B: `CombatRoom_Sample_Small_B.prefab` 신설 (Room A 복제 → RoomData 교체 → ExitWall 제거 → EntryZone 좌측 이동).
- 검증 씬에 Room A `(0,0,0)` + Room B `(20,0,0)` 인접 배치.

통과 항목:

| 시나리오 | 결과 |
|---|---|
| A — 진입 시 출구 벽 활성화 (player 못 나감) | ✅ |
| B — 클리어 시 출구 벽 비활성화 (player 자유 이동) | ✅ |
| C — Room A 통로 통과 → Room B EntryZone 발동 → 새 spawn 사이클 | ✅ |
| D — Room B (출구 없는 방) 진입 시 SetExitWallsActive no-op, 에러 없음 | ✅ |
| Awake 자동 캐시 (`exitWalls` 배열) | ✅ — Room A 의 ExitWall 자식이 자동 인식 |
| Is Trigger=false 의 *물리적 벽* 동작 | ✅ — player 가 활성화 상태에서 통과 못 함 |

## CL-036 1차 완료 판단

코드 기준으로 1차 완료로 본다. 확정된 항목:

- `RoomExitSpec` 의 `exitAnchorTag` 필드.
- `RoomExitWall` 식별 컴포넌트.
- `RoomEntryRuntimeController` 의 출구 벽 자동 캐시 + 진입 시 활성화 + 클리어 시 비활성화.
- 적 측 / 보상 UI / Player failure / Stage Run 오케스트레이터 영역 침범 없음.
- 후속 CL (047/048/049 및 분기형 / 미니맵 / procedural) 가 본 CL 산출물 위에 자연스럽게 얹힐 수 있는 상태.

완료를 막지 않는 후속 조정:

- 디자이너가 sample asset/prefab/scene 작성 + 시나리오 A~D 검증.
- CL-047/048 가 전역 런 상태 머신을 도입하면서 *현재 방 추적* 흡수.
- 분기형 던전 / 출구별 정책 / 시각 연출 등은 별도 CL.
