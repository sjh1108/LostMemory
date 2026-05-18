# CL-034 방 진입 시 전투 시작 초기화 구현 계획

## 목적

`CL-034 방 진입 시 전투 시작 초기화` 의 방향을 정리한다.

본 작업은 `CL-032` 가 만든 빈 슬롯 `RoomInitContextSpec` 을 채우고, `CL-033` 이 정의한 `RoomEncounterSpec` / `RoomEncounterAnchor` 자료를 *실행* 한다 — 플레이어가 방에 들어오면 `RoomData` 를 읽어 진입 컨텍스트를 적용하고 첫 웨이브 적을 스폰한다.

산출물은 다음 작업의 *입력* 으로 쓰인다.

- `CL-035` 적 전멸 기준 방 클리어 판정 — 본 작업이 발행하는 `EnemySpawnedPayload`, `WaveSpawnedPayload` 와 `IRoomClearConditionTracker` 인터페이스를 사용
- `CL-036` 문 열림·다음 방 전환 흐름 — 본 작업이 발행하는 `ExitDoorsLockRequestPayload` 와 `RoomCombatStartedPayload` 를 받아 출구 문 시각/충돌 토글을 처리

본 작업이 *만들지 않는 것*:

- 적 prefab, EnemyData SO, 적 AI — 적 관리자 (CL-037~042) 영역.
- 클리어 조건 판정 본체 — `CL-035` 영역. 본 작업은 `IRoomClearConditionTracker` 시그니처와 더미 stub 만 제공.
- 출구 문의 시각/충돌 토글 본체 — `CL-036` 영역. 본 작업은 `OnExitDoorsLockRequested` 시그널만 발행.
- BGM 실제 재생 — 사운드 wrapper 후속 CL. 본 작업은 stub log 만.
- 카메라 confiner / orthographic size / 페이드 — 본 작업 비범위. 카메라는 기존 follow 유지.
- 시드 기반 스폰 무작위 / 멀티플레이 동기화 — 후속 네트워크 CL.

## 결정 사항

- 방 진입 트리거는 *자동 trigger zone* 1차. `RoomEntryZone` 의 `OnTriggerEnter2D` 가 `RoomEntryRuntimeController.BeginRoomEntry(Character)` 를 호출.
- 동시에 controller 의 `BeginRoomEntry` 는 *public API* 로 노출 — `CL-036` 이 zone 우회로 직접 호출 가능.
- 방 단위 권위는 *방 layout prefab 자체에 붙은 `RoomEntryRuntimeController`* 가 갖는다. 전역 Stage Run 오케스트레이터는 본 작업에서 신설하지 않는다.
- `enemyId → prefab` 매핑은 글로벌 `EnemyCatalog` SO 한 자산. 모든 방이 동일 자산을 참조 — 새 적 종류 추가 시 한 곳만 수정.
- `IEnemyPrefabResolver` 인터페이스는 1차에 만들지 않는다 (YAGNI). 적 관리자가 후속에 `EnemyData` SO 를 도입할 때 그 자리에서 인터페이스를 추출.
- 클리어 조건 판정은 본 작업에서 *인터페이스 + 더미 stub* 만 둔다. `CL-035` 가 `RoomClearConditionType` 분기별 구현체로 교체.
- 카메라 관련 필드는 `RoomInitContextSpec` 에 두지 않는다. *입구 막힘 연출* 만 `lockExitDoors` 시그널로 발행.
- 보스 흐름 (`BossRoom*`) 의 ready 투표·door visual·multi-player participant tracking 은 combat 룸에 *불필요* — 별도 컴포넌트로 분리.
- TDE 원본은 건드리지 않는다.
- 사용자 검증 단계 (sample prefab / asset / scene) 는 디자이너가 Unity 에디터에서 만든다. 본 작업은 컴포넌트만 제공.

## 자료구조

### `RoomInitContextSpec` (본 작업이 슬롯 채움)

`Assets/_Project/Scripts/Runtime/Stage/Data/RoomInitContextSpec.cs`. `[Serializable]`, namespace `LostMemory.Stage.Data`.

| 필드 | 타입 | 의미 |
|---|---|---|
| `playerSpawnAnchorTag` | string | 빈 문자열이면 player 정렬 skip. 일치하는 첫 `RoomEntryAnchor` 위치로 정렬. |
| `facing` | Vector2 | `Vector2.zero` 면 facing skip. 부호로 4방향 매핑 (`East/West/North/South`). |
| `bgmCueId` | string | 빈 문자열이면 skip. 본 작업은 Debug.Log 1회 (stub). 실제 재생은 사운드 wrapper CL. |
| `lockExitDoors` | bool | true 면 `ExitDoorsLockRequested` 시그널 발행. 실제 문 닫힘은 CL-036. 기본값 true. |

모든 필드는 *옵션* — 빈 값/zero/false 면 해당 단계 skip.

### `EnemyCatalog` (ScriptableObject, 신설)

`Assets/_Project/Scripts/Runtime/Combat/EnemyCatalog.cs`. namespace `LostMemory.Combat`. 메뉴 `LostMemory/Combat/EnemyCatalog`.

- `EnemyCatalogEntry { string id; GameObject prefab; }` 직렬화 클래스.
- `EnemyCatalogEntry[] entries`.
- `bool TryGetPrefab(string id, out GameObject prefab)` — 빈 id / 매칭 실패 / null prefab 은 false.

### `EnemyEncounterSpawner` (MonoBehaviour, 신설)

`Assets/_Project/Scripts/Runtime/Combat/EnemyEncounterSpawner.cs`. namespace `LostMemory.Combat`.

- `Begin(string roomId, RoomEncounterSpec spec, RoomEncounterAnchor anchor, EnemyCatalog catalog)` — wave 별로 `RunWave` 코루틴을 큐.
- `RunWave` — `wave.StartDelay` 기다림 → entries 순회 → `catalog.TryGetPrefab` → `anchor.GetSpawnPoints(filter)` → `entry.Count` 만큼 *결정적 순차 선택* (`PickSpawnIndex` static, modulo) → `Instantiate(prefab, point.position, identity)`.
- 이벤트: `event Action<EnemySpawnedPayload> Spawned`, `event Action<WaveSpawnedPayload> WaveCompleted`.
- `IReadOnlyList<GameObject> SpawnedEnemies` — 누적 spawn 결과.

### `RoomEntryAnchor` (MonoBehaviour, 신설)

`Assets/_Project/Scripts/Runtime/Stage/RoomEntryAnchor.cs`. namespace `LostMemory.Stage`.

- `[SerializeField] string anchorTag = "default";`
- `bool Matches(string candidateTag)` — 정확 일치 + 빈 anchorTag 거부.

### `RoomEntryZone` (MonoBehaviour, 신설)

`Assets/_Project/Scripts/Runtime/Stage/RoomEntryZone.cs`. `[RequireComponent(typeof(BoxCollider2D))]`.

- `OnTriggerEnter2D(Collider2D)` → `Character` 검색 → `Player` 타입만 → `controller.BeginRoomEntry(character)` 호출.
- `Reset()` 에서 `BoxCollider2D.isTrigger = true` 자동 설정.

### `RoomEntryRuntimeEvents` (struct payload 모음, 신설)

`Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeEvents.cs`.

5개 readonly struct. 신규 시그널 추가 시 본 파일에 모은다.

| Payload | 필드 |
|---|---|
| `RoomEnteredPayload` | RoomId, Initiator |
| `RoomCombatStartedPayload` | RoomId, WaveCount |
| `EnemySpawnedPayload` | RoomId, WaveIndex, Enemy |
| `WaveSpawnedPayload` | RoomId, WaveIndex, SpawnedCount |
| `ExitDoorsLockRequestPayload` | RoomId |

### `IRoomClearConditionTracker` + `StubRoomClearConditionTracker` (신설)

`Assets/_Project/Scripts/Runtime/Stage/`.

- 인터페이스: `event Action OnRoomCleared`, `void Begin(RoomData)`, `void RegisterEnemy(GameObject)`.
- Stub: 모든 메서드 no-op, 클리어 이벤트 절대 발행 X.
- 본 작업은 stub 만 사용. `CL-035` 가 `RoomClearConditionType.AllEnemiesDefeated` 등 분기별 본체로 교체.

### `RoomEntryRuntimeController` (MonoBehaviour, 신설 — 핵심)

`Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeController.cs`.

- 보유: `RoomData`, `EnemyCatalog`, `RoomEncounterAnchor`, `EnemyEncounterSpawner`, `RoomEntryAnchor[]`.
- 내부: `StageRoomProgress progress` (Awake 에서 `RoomData` 로 Configure), `IRoomClearConditionTracker clearTracker` (Stub), `bool entryConsumed` (1회 보장).
- `bool IsAuthority => true` — 후속 네트워크 CL 이 한 줄만 바꿈.
- `BeginRoomEntry(Character initiator)` public API:
  1. `IsAuthority` / `entryConsumed` / `roomData != null` 가드
  2. `progress.MarkVisited()`
  3. `ApplyInitContext(initiator)` — anchor 매칭 + facing 정렬 + lockExitDoors 시그널 + BGM stub log
  4. `BeginEncounter()` — Stub tracker 시작 + spawner 이벤트 구독 + `spawner.Begin(...)` + `RoomCombatStarted` 발행
  5. `RoomEntered` 발행
- 5종 이벤트 외부 노출 (RoomEntered / RoomCombatStarted / EnemySpawned / WaveSpawned / ExitDoorsLockRequested).
- player 정렬 알고리즘은 `BossRoomLocalTransitionDriver.TeleportCharacter` / `AlignFacingDirection` 결을 같은 컴포넌트에 옮김 (보스 driver 직접 호출 X).

## 사용 흐름

```text
Awake:
  controller 가 RoomData 로 progress 를 Configure
  자식에서 RoomEncounterAnchor / EnemyEncounterSpawner / RoomEntryAnchor[] 자동 캐시

Player 가 RoomEntryZone 안으로 이동:
  zone.OnTriggerEnter2D
    → controller.BeginRoomEntry(character)
        → progress.MarkVisited()
        → ApplyInitContext(initiator)
            → ResolveEntryAnchor(initContext.PlayerSpawnAnchorTag)
            → AlignCharacterTo(initiator, anchor.position, initContext.Facing)
            → ExitDoorsLockRequested(...) 발행 (lockExitDoors 면)
            → BGM stub log (bgmCueId 비어있지 않으면)
        → BeginEncounter()
            → clearTracker.Begin(roomData)
            → spawner.Spawned += HandleSpawned
            → spawner.Begin(roomId, encounter, anchor, catalog)
                → wave 별 RunWave 코루틴 큐
                    → wave.StartDelay 후
                    → entry 순회 → catalog.TryGetPrefab
                    → anchor.GetSpawnPoints(filter)
                    → count 만큼 modulo 순회 → Instantiate
                    → Spawned / WaveCompleted 발행
            → RoomCombatStarted(...) 발행
        → RoomEntered(...) 발행
```

## 코드 구조

추가:

```
Assets/_Project/Scripts/Runtime/Stage/
  RoomEntryRuntimeController.cs
  RoomEntryZone.cs
  RoomEntryAnchor.cs
  RoomEntryRuntimeEvents.cs
  IRoomClearConditionTracker.cs
  StubRoomClearConditionTracker.cs

Assets/_Project/Scripts/Runtime/Combat/
  EnemyCatalog.cs
  EnemyEncounterSpawner.cs
```

수정:

- `Assets/_Project/Scripts/Runtime/Stage/Data/RoomInitContextSpec.cs` — 빈 스텁에 4 필드 채움.

(`StageRoomProgress`, `RoomData`, `BossRoom*`, `KhiPlayerCamera` 는 손대지 않는다.)

## 명명·관례

- namespace `LostMemory.Stage` (runtime), `LostMemory.Stage.Data` (직렬화 데이터), `LostMemory.Combat` (적 카탈로그·스포너) — 기존 결 유지.
- AddComponentMenu 접두 `Lost Memory/Stage/...`, `Lost Memory/Combat/...` — `BossRoomEntryPoint` 와 동일 양식.
- `Khi*` 접두는 쓰지 않는다 (게임 시스템 코드 결).
- TDE 원본 미수정.

## 멀티플레이 고려

- 본 작업은 *single-player 가정*. `IsAuthority => true` 만 두고 진입부에 가드 박는다.
- `EnemyCatalog`, `RoomEncounterSpec`, `RoomInitContextSpec` 모두 정적 자산 → 모든 클라이언트 공유.
- 스폰 *결정* 은 호스트 권위 전제. 본 작업은 *결정적 순차 선택* (modulo) 만 사용해 시드 동기화 없이도 호스트/클라이언트 결과가 일치할 수 있게 한다.
- 시드 무작위 도입은 별도 CL.

## 디자이너 수동 작업 (Unity 에디터 단계)

본 작업의 *코드* 는 자동 생성된다. 검증을 위해 디자이너/구현자가 Unity 에디터에서 다음을 수동 작성한다.

### 1. `EnemyCatalog_Default.asset` 생성

1. Unity 에디터 → `Project` 창 → `Assets/_Project/ScriptableObjects/` 우클릭 → `Create` 폴더 → `Enemies` 폴더 신설.
2. 그 안에서 우클릭 → `Create` → `LostMemory/Combat/EnemyCatalog`. 파일명 `EnemyCatalog_Default`.
3. Inspector → `Entries` 배열에 다음 3개 추가:

| Id | Prefab |
|---|---|
| `enemy_melee_basic` | `Assets/_Project/Prefabs/Enemies/Orc_CL037.prefab` |
| `enemy_charger_basic` | `Assets/_Project/Prefabs/Enemies/OrcRider_CL039.prefab` |
| `enemy_ranged_basic` | `Assets/_Project/Prefabs/Enemies/SkeletonArcher_CL041.prefab` |

### 2. `RoomData_Sample_Combat_Small.asset` 채움

기존 `Assets/_Project/ScriptableObjects/Rooms/RoomData_Sample_Combat_Small.asset` 의 빈 슬롯을 채운다.

- `Init Context`:
  - Player Spawn Anchor Tag: `default`
  - Facing: `(1, 0)` (East)
  - BGM Cue Id: `combat_default`
  - Lock Exit Doors: `true`
- `Encounter`:
  - Waves[0]: Label `Wave 1`, Start Delay `0`
    - Enemies[0]: Enemy Id `enemy_melee_basic`, Count `2`, Spawn Group Filter `near_door`
    - Enemies[1]: Enemy Id `enemy_ranged_basic`, Count `1`, Spawn Group Filter `far`

### 3. `CombatRoom_Sample_Small.prefab` 작성

`Assets/_Project/Prefabs/Rooms/` 아래에 prefab 신설.

```
CombatRoom_Sample_Small (Empty GameObject)
├── (Components)
│     RoomEntryRuntimeController
│       Room Data: RoomData_Sample_Combat_Small
│       Enemy Catalog: EnemyCatalog_Default
│     RoomEncounterAnchor
│     EnemyEncounterSpawner
├── EntryAnchor_Default (Empty)
│     RoomEntryAnchor (Anchor Tag: default)
├── EntryZone (Empty)
│     BoxCollider2D (Is Trigger: true, Size 적당히)
│     RoomEntryZone (Controller: 부모의 RoomEntryRuntimeController)
└── SpawnPoints (Empty)
      ├── Spawn_NearDoor_0 — RoomEncounterSpawnPoint (Group Tag: near_door)
      ├── Spawn_NearDoor_1 — RoomEncounterSpawnPoint (Group Tag: near_door)
      ├── Spawn_Far_0       — RoomEncounterSpawnPoint (Group Tag: far)
      └── Spawn_Far_1       — RoomEncounterSpawnPoint (Group Tag: far)
```

루트 prefab 의 `RoomEncounterAnchor` 컴포넌트에서 ContextMenu `Refresh Spawn Points` 한 번 누름 (CL-033 와 동일 절차).

### 4. `CL034_RoomEntry_Manual.unity` 검증 씬

`Assets/Scenes/` 아래 신설.

- KhiPlayer (또는 TDE 의 `Character` Player 한 개)
- 위 `CombatRoom_Sample_Small.prefab` 인스턴스 1개
- (선택) 카메라가 player 를 follow 하도록 기존 `KhiPlayerCamera` 또는 TDE 카메라 사용

## CL-034 완료 기준

수동 (Unity 에디터):

- 위 `CL034_RoomEntry_Manual.unity` 에서 Play.
- 플레이어를 `RoomEntryZone` 안으로 이동 시:
  1. Console 에 `RoomEntered(roomId)` 1회.
  2. Player 가 `EntryAnchor_Default` 위치로 정렬, East 방향으로 회전.
  3. Console 에 `ExitDoorsLockRequested(roomId)` 1회.
  4. Console 에 `[RoomEntryRuntimeController] BGM stub: ... cue='combat_default'` 1회.
  5. 즉시 wave 1 스폰: `Orc_CL037` 2 (near_door) + `SkeletonArcher_CL041` 1 (far).
  6. zone 재진입 시 추가 스폰 / 추가 이벤트 없음 (1회 보장).
  7. controller.IsAuthority 를 false 로 토글한 빌드에서는 진입 시 모든 단계 skip.
- `RoomEncounterAnchor` 의 ContextMenu `Refresh Spawn Points` 가 자식 4개를 다시 잡는다 (기존 CL-033 검증과 동일).
- `EnemyCatalog.TryGetPrefab` 미매칭 id 입력 시 `false` 반환 + 스폰 skip + `[EnemyEncounterSpawner] enemyId '...' not found in catalog.` 경고.

자동 (선택):

- `EnemyCatalog.TryGetPrefab` EditMode test (id 매칭 / 미매칭 / null prefab).
- `EnemyEncounterSpawner.PickSpawnIndex` EditMode test (count > spawnPoints 일 때 modulo 순환, 음수 sequence 처리).

## 후속 작업

- `CL-035` 가 `IRoomClearConditionTracker` 본체 (`AllEnemiesDefeatedTracker` 등) 를 채우고 `StubRoomClearConditionTracker` 자리를 교체한다. `EnemySpawned` / `WaveSpawned` 이벤트로 추적.
- `CL-036` 이 `ExitDoorsLockRequested` 시그널을 받아 출구 문 prefab 의 시각/충돌을 토글하고, `OnRoomCleared` 발생 시 다음 방 전환 흐름을 잇는다. 필요 시 zone 우회로 `RoomEntryRuntimeController.BeginRoomEntry` 직접 호출.
- 적 관리자 측에서 `EnemyData` SO 가 도착하면 `EnemyCatalog` 를 흡수하거나 prefab 자리를 `EnemyData` 로 교체. 인터페이스 추출은 그 시점.
- 시드 기반 무작위 스폰은 멀티 동기화 도입 CL 에서 다룬다.
- 사운드 wrapper CL 에서 `bgmCueId` stub log 자리에 실제 재생을 잇는다.
- `RoomEntryZone` 과 `BossRoomEntryInteractionZone` 의 공통 추출은 두 흐름이 한 번 더 안정된 후 (CL-036 이후) 검토.

## 위험·결정 보류

- 시드 기반 무작위 스폰: 본 작업은 *결정적 순차* 만. 멀티 동기화 도입 시 별도 CL.
- BGM 재생: stub log. 사운드 wrapper CL 도착 시 한 줄 교체.
- 카메라 confiner / orthographic size / 페이드: 본 작업 비범위. 카메라는 기존 follow 유지.
- 출구 문 닫힘 시각/충돌 토글: 본 작업은 시그널 발행만. CL-036 책임.
- multi-player participant tracking: 본 작업은 single-player 가정.
- `RoomData.ClearCondition` 분기별 tracker 본체: 본 작업은 인터페이스 + stub 만. CL-035 책임.
- `IEnemyPrefabResolver` 인터페이스 추출: EnemyData SO 도착 시점 (CL-037~042 이후).
- `RoomEntryRuntimeController.IsAuthority`: 현재 항상 true. 네트워크 CL 한 줄 변경.
- 적 spawn 시 race condition (같은 프레임 다중 init): `EnemyEncounterSpawner.RunWave` 의 entry 사이 `yield return null` 로 회피 중. 적 prefab 의 `OnEnable` 멱등성이 정리되면 제거 가능.
- 적 prefab 의 `Health.DestroyOnDeath = false` 셋업과 충돌: spawn 직후 `HardenSpawnedInstance` 헬퍼가 `DestroyOnDeath = true` 로 강제 중. 적 담당자(클라2) 와 풀 사이클 정책 협의 후 제거 가능. 자세한 진단·격리 실험은 `cl034_troubleshooting.md` 참고.
- 풀링 전환 (Instantiate → 미리 생성 + SetActive 토글): 본 CL 비범위. `EnemyEncounterSpawner.Begin(...)` 외부 인터페이스 유지 → 별도 CL 에서 내부 구현만 교체 가능.

## 구현 결과

2026-04-27 기준 CL-034 1차 코드 구현을 완료했다.

추가된 런타임 코드:

- `Assets/_Project/Scripts/Runtime/Stage/`
  - `RoomEntryRuntimeController` — 방 단위 진입 권위. `BeginRoomEntry(Character)` public API + `IsAuthority` 가드 + 1회 보장 + 5종 이벤트.
  - `RoomEntryZone` — `BoxCollider2D` trigger 안 player 를 controller 로 forward.
  - `RoomEntryAnchor` — `anchorTag` 한 개 보유, `Matches(string)` 한 개.
  - `RoomEntryRuntimeEvents` — 5개 readonly struct payload.
  - `IRoomClearConditionTracker` — 인터페이스 (`Begin`, `RegisterEnemy`, `OnRoomCleared`).
  - `StubRoomClearConditionTracker` — 1차 더미 (모든 메서드 no-op).
- `Assets/_Project/Scripts/Runtime/Combat/`
  - `EnemyCatalog` — `[CreateAssetMenu]` SO. `EnemyCatalogEntry` 직렬화 + `TryGetPrefab(id, out)`.
  - `EnemyEncounterSpawner` — wave 별 코루틴, `PickSpawnIndex` static modulo, `Spawned`/`WaveCompleted` 이벤트.

수정된 코드:

- `Assets/_Project/Scripts/Runtime/Stage/Data/RoomInitContextSpec.cs` — 빈 스텁에 4 필드 (`playerSpawnAnchorTag`, `facing`, `bgmCueId`, `lockExitDoors`).

자산·prefab·씬:

- `EnemyCatalog_Default.asset`, `CombatRoom_Sample_Small.prefab`, `CL034_RoomEntry_Manual.unity` 작성은 디자이너/구현자 수동 단계 (§ "디자이너 수동 작업").
- `RoomData_Sample_Combat_Small.asset` 의 빈 슬롯 채움도 같은 단계.

## 현재 검증 결과

코드 단계 (Unity 에디터에서 다음을 확인할 항목):

- 컴파일 에러 없음. `RoomEncounterAnchor.GetSpawnPoints`, `Character.CharacterTypes.Player`, `CharacterOrientation2D.Face` 등 외부 참조 호환.
- `Add Component` 검색에서 `RoomEntryRuntimeController`, `RoomEntryZone`, `RoomEntryAnchor`, `EnemyEncounterSpawner` 가 노출.
- `Project` 창 우클릭 → `Create` → `LostMemory/Combat/EnemyCatalog` 메뉴 노출.

수동 검증 (디자이너 수동 작업 § 1~4 완료 후):

- `CL034_RoomEntry_Manual.unity` Play → zone 진입 → `RoomEntered` / `ExitDoorsLockRequested` / `RoomCombatStarted` log 1회씩, wave 적 3마리 스폰.
- zone 재진입 시 추가 스폰 없음.
- `RoomData.InitContext.PlayerSpawnAnchorTag` 를 빈 문자열로 바꾸면 player 정렬 skip (zone 통과 위치에 그대로 머무름).
- `EnemyCatalog_Default.asset` 의 `enemy_melee_basic` 항목을 잠시 빈 prefab 으로 두면 경고 + 해당 entry 만 skip, 나머지는 정상 스폰.

(자동 EditMode 테스트는 1차에서는 작성하지 않음 — 후속에 필요 시 추가.)

## CL-034 1차 완료 판단

코드 기준으로 CL-034 는 1차 완료로 본다.

완료된 항목:

- `RoomInitContextSpec` 4 필드 정의.
- `EnemyCatalog` SO + `TryGetPrefab` 룩업.
- `EnemyEncounterSpawner` wave/anchor/catalog 연결 코루틴 + `Spawned`/`WaveCompleted` 이벤트.
- `RoomEntryRuntimeController` 진입 1회 보장 + `ApplyInitContext` (anchor 정렬 + facing + BGM stub + lockExitDoors 시그널) + `BeginEncounter` (Stub tracker + spawner 시작).
- `RoomEntryZone` / `RoomEntryAnchor` / `RoomEntryRuntimeEvents` / `IRoomClearConditionTracker` / `StubRoomClearConditionTracker` 보조 컴포넌트·시그널 정의.
- 디자이너 수동 작업 가이드 (§ 1~4) 문서화.
- 적 관리자 / 사운드 / 카메라 / 클리어 판정 / 출구 문 본체 영역 침범 없음.

완료를 막지 않는 후속 조정:

- 디자이너가 sample asset/prefab/scene 을 작성하고 수동 검증을 마친다.
- CL-035 가 `StubRoomClearConditionTracker` 자리를 분기별 본체로 교체.
- CL-036 가 `ExitDoorsLockRequested` 시그널을 받아 문 흐름을 잇는다.
- 적 관리자가 `EnemyData` SO 를 도입하면 `EnemyCatalog` 의 prefab 자리를 마이그레이션.
