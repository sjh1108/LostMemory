# CL-035 적 전멸 기준 방 클리어 판정 구현 계획

## 목적

`CL-035 적 전멸 기준 방 클리어 판정` 의 방향을 정리한다.

본 작업은 `CL-034` 가 만든 빈 인터페이스 `IRoomClearConditionTracker` 의 본체를 채워, 방에 spawn 된 모든 적이 사망했을 때 *방 클리어* 시그널을 발행한다. 동시에 *Wave 간 의존* (이전 wave 사망률이 임계 도달 시 다음 wave spawn) 도 본 작업의 책임이다 — `CL-034` 의 절대 시간 startDelay 만으로는 표현 불가능한 흐름을 함께 지원한다.

산출물은 다음 작업의 *입력* 으로 쓰인다.

- `CL-036` 문 열림·다음 방 전환 흐름 — `RoomCleared` 이벤트를 구독하여 출구 문 열림 흐름 트리거.
- 보상 UI 브랜치 — 같은 이벤트의 `payload.Data.RewardPool` 을 읽어 보상 3택 트리거.
- `BossRoomEntryTracker.TryMarkRoomCompleted` 자동 호출 — 보스 진입 조건 갱신.

본 작업이 *만들지 않는 것*:

- `RoomClearConditionType.InteractionComplete` / `Custom` 분기 — 본 CL 비범위. `StubRoomClearConditionTracker` 로 fallback. 후속 CL (이벤트방, 보스 처치 판정 등) 가 본체 채움.
- Player 사망 처리 / Room failure 시그널 — 본 CL 비범위. `CL-014 다운·부활` / `CL-048 런 상태 머신` 영역. 본 작업의 design doc 에 *gap memo* 만 남김.
- 보상 UI / 문 열림 / UI 메시지 — 모두 *후속 CL 또는 다른 브랜치* 가 `RoomCleared` 이벤트 구독 형태로 hook in.
- `EnemyCombatReporter` 같은 적 측 사망 보고 컴포넌트 — 적 담당자(클라2) 영역. 본 CL 의 tracker 가 *직접 `Health.OnDeath` 구독* 으로 우회.
- 멀티플레이 동기화 — `controller.IsAuthority` 가드 그대로 유지. 네트워크 CL 시점에 wiring.

## 결정 사항

> **사망률 트리거 모델 — B (wave 가 자기 종료 조건 보유) 채택**.
> "이 wave 의 적이 X% 사망 시 *다음* wave spawn" 으로 디자이너가 시간순 사고와 일치. 마지막 wave 는 다음이 없어 ratio 무시.

- `RoomEncounterWave` 에 `triggerNextWaveDeathRatio` (float [0, 1]) 필드 추가. 0 = 비활성, >0 = **이 wave 의 적이 그 비율 사망 시 *다음* wave spawn**. 마지막 wave 는 다음이 없어 무시 (모델 B — wave 가 자기 종료 조건 보유).
- Wave spawn 트리거는 *다음 wave 의 시간 (startDelay) 와 현재 wave 의 사망률* OR — **둘 중 먼저 만족하는 쪽**. spawner 측이 wave 별 idempotent 로 두 trigger 중 어느 쪽이 와도 1회만 spawn.
- `BossRoomEntryTracker.TryMarkRoomCompleted` 호출까지 본 CL 범위 — `OnRoomCleared` 발행 시 controller 가 자동 호출.
- Post-clear 액션은 *`OnRoomCleared` 발행만*. payload 에 `RoomData` 포함하여 구독자가 자유 사용.
- Tracker 는 *spawner / controller 직접 참조 없음* — 이벤트로만 통신, 결합도 최소.
- TDE 원본은 건드리지 않는다.

## 자료구조

### `RoomEncounterWave` 확장 (CL-033 산출물에 1 필드 추가)

`Assets/_Project/Scripts/Runtime/Stage/Data/RoomEncounterWave.cs`.

새 필드:

| 필드 | 타입 | 의미 |
|---|---|---|
| `triggerNextWaveDeathRatio` | `float` (Range 0~1) | 이 wave 의 적이 본 비율 사망 시 *다음* wave 가 spawn. 0 = 비활성. 마지막 wave 는 무시 (다음 wave 없음). 모델 B — wave 가 자기 종료 조건 보유. |

기존 필드 (`label`, `startDelay`, `enemies`) 변동 없음.

### `RoomClearedPayload` 신설 (RoomEntryRuntimeEvents)

`Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeEvents.cs`. `[Serializable]` 아닌 `readonly struct`.

- `string RoomId`
- `RoomData Data` — 구독자가 RewardPool / ClearCondition 등 자유 참조

### `IRoomClearConditionTracker` 시그너처 확장

`Assets/_Project/Scripts/Runtime/Stage/IRoomClearConditionTracker.cs`.

```csharp
event Action<RoomClearedPayload> OnRoomCleared;
event Action<int> OnNextWaveReady;          // 다음에 spawn 할 wave index
void Begin(RoomData data);
void RegisterEnemy(EnemySpawnedPayload payload);
void NotifyWaveSpawned(WaveSpawnedPayload payload);
```

이전 시그너처 (`RegisterEnemy(GameObject)`) 는 *wave 정보 없음* 이라 사망률 판정 불가 → payload 받는 형태로 변경.

### `StubRoomClearConditionTracker` (no-op 유지)

새 시그너처 따라 모든 메서드 / 이벤트 add/remove no-op. `InteractionComplete` / `Custom` 분기 fallback 용도.

### `AllEnemiesDefeatedTracker` 신설 (CL-035 본체)

`Assets/_Project/Scripts/Runtime/Stage/AllEnemiesDefeatedTracker.cs`. namespace `LostMemory.Stage`. `internal sealed`.

내부 상태:

- `RoomData data` — 캐시
- `Dictionary<int, int> spawnedPerWave` — wave 별 spawn 적 수 (NotifyWaveSpawned 시 채움)
- `Dictionary<int, int> deathPerWave` — wave 별 사망 카운트
- `Dictionary<GameObject, int> enemyToWave` — 적 → wave index 역참조 (Health.OnDeath 시 wave 식별용)
- `HashSet<int> nextWaveTriggered` — 다음 wave 트리거 1회 보장 (sourceWaveIndex 키)
- `bool allWavesSpawned`, `bool clearedFired` — 1회 보장 플래그

메서드:

- `Begin(data)` — 데이터 캐시. wave 수 0 이면 즉시 `OnRoomCleared` 발행.
- `RegisterEnemy(payload)` — `enemyToWave[enemy] = waveIndex`. `enemy.GetComponent<Health>().OnDeath += () => HandleEnemyDeath(enemy)` 구독.
- `NotifyWaveSpawned(payload)` — `spawnedPerWave[waveIndex] = spawnedCount`. 마지막 wave 면 `allWavesSpawned = true` + 클리어 판정 재시도.
- `HandleEnemyDeath(enemy)` — wave 식별 → 사망 카운트 ++ → `CheckNextWaveTrigger` + `FireRoomClearedIfReady`.
- `CheckNextWaveTrigger(sourceWaveIndex)` — *현재 wave (sourceWaveIndex)* 의 `TriggerNextWaveDeathRatio` 임계 도달 시 `OnNextWaveReady(sourceWaveIndex + 1)` 1회 발행. 마지막 wave 면 무시.
- `FireRoomClearedIfReady()` — `allWavesSpawned` && `totalSpawned == totalDeaths` (또는 totalSpawned == 0) 시 `OnRoomCleared` 1회 발행.

### `EnemyEncounterSpawner` 변경 (CL-034 산출물)

`Assets/_Project/Scripts/Runtime/Combat/EnemyEncounterSpawner.cs`.

변경:

- `Begin(...)` 시 *모든 wave 의 `ScheduleWave` 코루틴 큐* 시작 (CL-034 동작 + idempotent flag 추가).
- 신규 `public bool SpawnNextWave()` — 외부 강제 트리거 (가장 작은 미시작 wave 즉시 spawn, startDelay 무시).
- 내부 `ScheduleWave(idx, wave)` — `wave.StartDelay` 후 `StartWaveOnce` 호출.
- 내부 `StartWaveOnce(idx, wave)` — `waveStarted[idx]` flag 로 1회만 RunWave 실행.
- `RunWave` 내부의 `WaitForSeconds(StartDelay)` 제거 — `ScheduleWave` 가 처리.

이로 인해 *시간 트리거* (자체 startDelay) 와 *사망률 트리거* (외부 `SpawnNextWave`) 가 *둘 중 먼저 도착* 으로 OR 동작.

### `RoomEntryRuntimeController` 변경 (CL-034 산출물)

`Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeController.cs`.

새 필드 / 이벤트:

- `[SerializeField] BossRoomEntryTracker bossTracker;` (옵션, null 허용)
- `event Action<RoomClearedPayload> RoomCleared;`

변경된 흐름:

- `BeginEncounter()`:
  - `clearTracker = CreateTrackerFor(roomData.ClearCondition)` — `AllEnemiesDefeated` → `AllEnemiesDefeatedTracker`, 그 외 → `Stub`.
  - `clearTracker.OnNextWaveReady += HandleNextWaveReady` — controller 가 `spawner.SpawnNextWave()` 호출.
  - `clearTracker.OnRoomCleared += HandleRoomCleared` — controller 가 `RoomCleared` 발행 + `bossTracker?.TryMarkRoomCompleted(roomId)`.
  - 기존 spawner 시작 + `RoomCombatStarted` 발행은 그대로.
- `HandleSpawned(payload)` — `clearTracker?.RegisterEnemy(payload)` (시그너처 변경).
- `HandleWaveCompleted(payload)` — `clearTracker?.NotifyWaveSpawned(payload)` (신규 forward).
- `OnDestroy` — tracker 이벤트 unsubscribe.

## 사용 흐름

```text
BeginRoomEntry(initiator)
  → progress.MarkVisited()
  → ApplyInitContext (CL-034)
  → BeginEncounter
      → clearTracker = AllEnemiesDefeatedTracker (또는 Stub)
      → clearTracker.OnNextWaveReady += controller.HandleNextWaveReady
      → clearTracker.OnRoomCleared += controller.HandleRoomCleared
      → clearTracker.Begin(roomData)
      → spawner.Spawned += controller.HandleSpawned
      → spawner.WaveCompleted += controller.HandleWaveCompleted
      → spawner.Begin(...)  // 모든 wave 의 ScheduleWave 코루틴 큐 시작
      → RoomCombatStarted 발행

전투 진행:
  spawner 가 wave N 의 startDelay 후 spawn
    → Spawned 이벤트 N마리 발행
    → controller.HandleSpawned → clearTracker.RegisterEnemy(payload) → Health.OnDeath 구독
  spawner 가 wave N spawn 완료
    → WaveCompleted 이벤트
    → controller.HandleWaveCompleted → clearTracker.NotifyWaveSpawned

  적 사망 (Health.OnDeath 발화)
    → tracker.HandleEnemyDeath
        → wave N 사망 카운트 ++
        → CheckNextWaveTrigger(N): 현재 wave N 의 ratio 임계 도달 시 OnNextWaveReady(N+1)
            → controller.HandleNextWaveReady → spawner.SpawnNextWave() → wave (N+1) 조기 spawn
            (단 spawner 가 이미 startDelay 로 시작했다면 idempotent flag 로 skip)
        → FireRoomClearedIfReady: 모든 wave spawn 완료 + 모든 적 사망 시 OnRoomCleared 발행
            → controller.HandleRoomCleared
                → RoomCleared 이벤트 발행 (payload = roomId + RoomData)
                → bossTracker?.TryMarkRoomCompleted(roomId)
```

## 코드 구조 (변경 범위)

추가 (1 파일):

```
Assets/_Project/Scripts/Runtime/Stage/
  AllEnemiesDefeatedTracker.cs
```

수정:

```
Assets/_Project/Scripts/Runtime/Stage/Data/
  RoomEncounterWave.cs                  # triggerNextWaveDeathRatio 필드 추가

Assets/_Project/Scripts/Runtime/Stage/
  IRoomClearConditionTracker.cs         # 시그너처 확장
  StubRoomClearConditionTracker.cs      # 새 시그너처 no-op
  RoomEntryRuntimeEvents.cs             # RoomClearedPayload 추가 + RoomData using
  RoomEntryRuntimeController.cs         # bossTracker 필드 + RoomCleared 이벤트 + tracker 분기·와이어링

Assets/_Project/Scripts/Runtime/Combat/
  EnemyEncounterSpawner.cs              # ScheduleWave/StartWaveOnce + SpawnNextWave + waveStarted idempotent
```

(`RoomData`, `RoomEncounterSpec`, `RoomEntryAnchor`, `RoomEntryZone`, `EnemyCatalog`, `BossRoom*`, `HardenSpawnedInstance` 헬퍼는 손대지 않는다.)

## 명명·관례

- namespace `LostMemory.Stage` — tracker 본체와 stub 동일.
- `AddComponentMenu` 없음 — tracker 는 internal class, 인스펙터 노출 X.
- `Khi*` 접두 미사용.
- TDE 원본 미수정.

## 멀티플레이 고려

- `RoomEntryRuntimeController.IsAuthority` 가드 그대로 — tracker 는 호스트 권위 단일 인스턴스.
- 클라이언트는 호스트의 `RoomCleared` 시그널을 수신해 동일 처리. 네트워크 CL 시점에 RPC wiring.
- `OnNextWaveReady` 도 호스트 권위 — 클라이언트에서 별도 spawn 안 됨 (spawner 자체가 호스트 권위).
- 적 `Health.OnDeath` 구독은 호스트 측에서만 의미 있음. 클라이언트는 호스트의 RoomCleared 만 신뢰.

## CL-035 완료 기준

수동 (Unity 에디터):

**시나리오 A — CL-034 회귀 (1 wave 셋업)**
- 기존 `RoomData_Sample_Combat_Small` (Wave 1 = Orc 2 + Archer 1) 그대로.
- ▶ Play → zone 진입 → spawn → 모두 죽임 → Console 에 RoomCleared 관련 로그 (구독자가 있다면).
- 모든 CL-034 동작 (sprite 사라짐, race 회피) 회귀 정상.

**시나리오 B — 사망률 기반 wave 트리거 (모델 B)**
- `RoomData_Sample_Combat_Small` 의 Encounter 를 2 wave 로 수정:
  - Wave 0: Orc 2, startDelay 0, **triggerNextWaveDeathRatio 0.5** (← Wave 0 의 종료 조건)
  - Wave 1: Archer 1, **startDelay 999** (사실상 비활성), triggerNextWaveDeathRatio 0
- ▶ Play → wave 0 즉시 spawn → Orc 1마리 죽임 → Wave 0 사망률 50% 도달 → wave 1 spawn (Archer 등장) → 남은 적 모두 죽임 → RoomCleared 발행.

**시나리오 C — startDelay vs ratio OR 동작 (모델 B)**
- Wave 0 의 triggerNextWaveDeathRatio 0.5 + Wave 1 의 startDelay 5초 둘 다 set.
- Wave 0 사망률 5초 안에 50% 도달 → 사망률 트리거가 먼저 → Wave 1 spawn (사망률 우선).
- Wave 0 사망률 5초 안에 미도달 → Wave 1 의 startDelay 5초 만료 → Wave 1 spawn (시간 우선).
- 두 경우 모두 Wave 1 이 *한 번만* spawn (idempotent).

**시나리오 D — InteractionComplete / Custom 회귀**
- `RoomData.ClearCondition` 을 `InteractionComplete` 로 바꾸면 stub tracker 사용 → 적이 다 죽어도 RoomCleared 발행 없음 (의도된 동작 — 본 CL 비범위).

**시나리오 E — Boss 통합 검증 (옵션)**
- 검증 씬의 `RoomEntryRuntimeController` 에 `BossRoomEntryTracker` 인스턴스 연결.
- 클리어 시 `BossRoomEntryTracker` 의 `BossEntryConditionChanged` 이벤트가 발화하는지 (Inspector / log 확인).

자동 (선택, EditMode):
- `AllEnemiesDefeatedTracker` 의 사망률 임계 계산 단위 테스트 (waveIndex / spawnedCount / deathCount → ratio).
- Edge: triggerNextWaveDeathRatio = 1.0 (전원 사망 후 다음 wave) / 0.0 (비활성).

## 후속 작업

- `CL-036` 가 controller 의 `RoomCleared` 이벤트를 구독해 출구 문 열림 흐름 작성.
- 보상 UI 브랜치가 같은 이벤트의 `payload.Data.RewardPool` 을 구독해 보상 3택 트리거.
- `CL-014` / `CL-048` 가 player 사망 → room failure 시그널을 본 CL 의 RoomEntryRuntimeEvents 에 추가 (`RoomFailedPayload` 같은).
- `RoomClearConditionType.InteractionComplete` 본체는 이벤트방 구현 CL (CL-098) 가 채움.
- `RoomClearConditionType.Custom` 본체는 보스 처치 판정 CL (CL-054) 가 채움.

## 위험·결정 보류

- **Player 사망 / room failure 미처리** — 본 CL 비범위. tracker 는 적 사망만 카운트. CL-014 / CL-048 작업 시 추가.
- **사망률 트리거 race condition** — 한 프레임에 여러 적 동시 사망 시 임계 통과 판정. dictionary count + 비교 + 1회 보장 (`nextWaveTriggered` set) 으로 안전.
- **재진입 / cleanup** — `RoomEntryRuntimeController.OnDestroy` 에서 tracker 의 OnNextWaveReady / OnRoomCleared unsubscribe. spawner unsubscribe 도 동일. 단 *적 GameObject 에 구독한 Health.OnDeath* 는 적이 destroy 될 때 자연 GC — 별도 unsubscribe 불필요.
- **멀티플레이 권위** — `IsAuthority` 가드 유지. 사망 카운트·트리거는 호스트 권위, 클라이언트는 호스트 시그널 수신만. 네트워크 CL 시점에 wiring.
- **InteractionComplete / Custom 분기** — 본 CL 비범위. 후속 CL 가 추가 tracker 본체로 채움.
- **wave count = 0 edge** — `Encounter.Waves` 가 비어있으면 `Begin` 시 즉시 `OnRoomCleared` 발행 (빈 방 = 즉시 통과).
- **Boss tracker 옵션** — `bossTracker` 가 null 이어도 동작 정상 (보스 무관 방). 보스방 자체의 클리어는 CL-054 영역.
- **Health 가 없는 적** — `RegisterEnemy` 시 Health 컴포넌트 없으면 경고 log + skip. 사망 추적 안 됨 → 적 카운트에 포함되었지만 사망 카운트는 영영 미증가 → RoomCleared 영영 미발행 가능. 적 prefab 셋업 검증 필요 (적 담당자 영역).

## 구현 결과

(2026-04-27 기준 1차 코드 구현 완료 — Unity 에디터 검증은 사용자 수동 단계)

추가된 런타임 코드:

- `Assets/_Project/Scripts/Runtime/Stage/AllEnemiesDefeatedTracker.cs` — RoomClearConditionType.AllEnemiesDefeated 분기 본체. Health.OnDeath 직접 구독 + wave 별 사망 카운트 + ratio 임계 판정 + RoomCleared 발행.

수정된 코드:

- `Assets/_Project/Scripts/Runtime/Stage/Data/RoomEncounterWave.cs` — `triggerNextWaveDeathRatio` 필드 + 접근자.
- `Assets/_Project/Scripts/Runtime/Stage/IRoomClearConditionTracker.cs` — 시그너처 확장 (RegisterEnemy payload, NotifyWaveSpawned, OnNextWaveReady, OnRoomCleared(payload)).
- `Assets/_Project/Scripts/Runtime/Stage/StubRoomClearConditionTracker.cs` — 새 시그너처 no-op.
- `Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeEvents.cs` — `RoomClearedPayload` struct 추가 + `LostMemory.Stage.Data` using.
- `Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeController.cs` — `bossTracker` SerializeField + `RoomCleared` 이벤트 + tracker 분기 (`CreateTrackerFor`) + `OnNextWaveReady` / `OnRoomCleared` 와이어링.
- `Assets/_Project/Scripts/Runtime/Combat/EnemyEncounterSpawner.cs` — `ScheduleWave` / `StartWaveOnce` / `SpawnNextWave` + `waveStarted` idempotent flag. `RunWave` 의 startDelay 처리는 ScheduleWave 로 이동.

자산·prefab·씬:

- 검증을 위한 `RoomData_Sample_Combat_Small.asset` 의 wave 분리 셋업 (시나리오 B/C) 은 디자이너/구현자 수동 단계.

## 현재 검증 결과

2026-04-27 Unity 에디터 수동 검증 통과.

검증 셋업:
- `RoomData_Sample_Combat_Small`: Wave 0 (Orc 4 + Archer 1, ratio 0.5) + Wave 1 (Archer 1, startDelay 5초)
- `Clear Condition: AllEnemiesDefeated`
- `bossTracker` 슬롯 비움 (옵션 검증)

통과 항목:

| 항목 | 결과 |
|---|---|
| Wave 0 자동 spawn (진입 즉시) | ✅ |
| Wave 별 RegisterEnemy + Health.OnDeath 구독 | ✅ 5+1=6마리 모두 구독 |
| 사망률 트리거 (Wave 0 60% 사망 → OnNextWaveReady) | ✅ |
| 시간 트리거 (Wave 1 startDelay 5초 자동 spawn) | ✅ |
| OR 동작 idempotent (시간으로 이미 spawn 후 사망률 트리거 와도 SpawnNextWave False) | ✅ |
| 1회 보장 (`nextWaveTriggered` set / `clearedFired` flag) | ✅ "wave 0 already triggered" log 확인 |
| 마지막 wave ratio 무시 ("wave 1 is last (count=2)") | ✅ |
| `OnRoomCleared` 1회 발행 (totalSpawned=6 / totalDeaths=6) | ✅ |
| controller 가 RoomCleared 이벤트 수신 | ✅ |
| `bossTracker` null 시 mark skip | ✅ "bossTracker=False" |
| Stub fallback (`Clear Condition: InteractionComplete`) | ✅ 적 다 죽여도 RoomCleared 발행 안 함 |

검증 시 일시적으로 추가했던 디버그 로그 (`[Tracker]` / `[Controller]` / `[Spawner]`) 는 production 상태로 모두 제거.

## CL-035 1차 완료 판단

코드 기준으로 1차 완료로 본다. 확정된 항목:

- `IRoomClearConditionTracker` 시그너처 확장 + Stub 동기화.
- `AllEnemiesDefeatedTracker` 본체 (Health.OnDeath 구독, wave 별 카운팅, ratio 임계, RoomCleared 발행).
- `EnemyEncounterSpawner` 의 OR 트리거 지원 (시간 + 외부 강제, idempotent).
- `RoomEntryRuntimeController` 의 ClearCondition 분기 + Boss 자동 mark + RoomCleared 이벤트 노출.
- 적 측 / 보상 UI / 문 흐름 / Player failure 영역 침범 없음.

완료를 막지 않는 후속 조정:

- 디자이너가 sample asset 의 wave 분리 셋업 작성 + 수동 검증 (시나리오 A~E).
- CL-036 가 RoomCleared 구독으로 문 흐름 wiring.
- 보상 UI 브랜치가 RoomCleared 구독으로 보상 트리거.
- CL-014 / CL-048 가 player 사망 영역 채움.
