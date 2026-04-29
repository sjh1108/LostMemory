# CL-054 보스 처치 판정 및 보스방 클리어 처리 구현 계획

## 문서 목적

`CL-054 보스 처치 판정 구현`의 구현 범위와 순서를 정리한다.

CL-051~CL-053에서 Bertha 보스의 등장, 전투 패턴, 체력 페이즈 전환까지 구현했다. CL-054는 보스가 사망했을 때 보스방을 클리어 처리하고, 이후 런 성공/결과 흐름으로 이어지도록 연결하는 작업이다.

## 대상 스토리

| Jira ID | 범위 | 담당 |
|---|---|---|
| CL-054 | 보스 처치 판정 구현 | 클라2 |

## 현재 기준 정리

현재 코드베이스에는 다음 기반이 존재한다.

- `BerthaBossEncounterController`
  - 인트로 종료 후 Bertha 전투 시작을 담당한다.
  - 사망 후 방 클리어와는 아직 연결되어 있지 않다.
- `Health`
  - 보스 사망 시 `OnDeath` 이벤트를 받을 수 있다.
- `RoomEntryRuntimeController`
  - `RoomCleared` 이벤트를 발행할 수 있다.
  - `HandleRoomCleared`에서 출구 벽을 해제하고 `BossRoomEntryTracker.TryMarkRoomCompleted`를 호출한다.
  - 현재 외부에서 직접 “이 방 클리어”를 요청하는 public API는 없다.
- `RunManager`
  - `RoomEntryRuntimeController.RoomCleared`를 구독한다.
  - `RoomData.RoomType == StageRoomType.Boss`인 방이 클리어되면 `RunCleared -> Resulting` 흐름으로 전환한다.
- `RoomData_Sample_Boss.asset`
  - 현재 `roomType: Boss`
  - 현재 `clearCondition: AllEnemiesDefeated`

즉, 런 종료 흐름 자체는 이미 준비되어 있고, CL-054의 핵심은 “보스 사망”을 “보스방 RoomCleared”로 연결하는 것이다.

## 목표 흐름

목표 런타임 흐름은 다음과 같다.

```text
Bertha Health.OnDeath
  -> BossDefeatRoomClearController가 보스 사망 감지
  -> RoomEntryRuntimeController.NotifyCustomRoomCleared()
  -> RoomEntryRuntimeController.RoomCleared 발행
  -> 보스방 출구/벽 해제
  -> RunManager가 Boss RoomCleared 감지
  -> RunCleared
  -> Resulting
```

## 구현 범위

### CL-054 MVP

- 보스 `Health.OnDeath`를 구독하는 브릿지 컴포넌트 추가
- 보스 사망 시 보스방 `RoomCleared`를 1회만 발행
- `RoomEntryRuntimeController`에 외부 클리어 요청 API 추가
- 보스방 `RoomData`의 클리어 조건을 `Custom`으로 변경
- `RunManager`의 기존 보스방 클리어 흐름과 연결 확인
- 보스 사망 후 출구/벽 해제 확인

### CL-054 Stretch

- 보스 처치 후 짧은 지연 시간 후 클리어 처리
- 보스 처치 카메라/사운드/VFX 연출 훅 추가
- 보상 UI 또는 결과 UI 데이터 연결
- 보스 처치 후 플레이어 입력 잠금/해제 정책 정리

### 이번 문서 범위 밖

- 보상 3택 UI 최종 구현
- 결과 UI 데이터 집계
- 멀티플레이 동기화
- 신규 보스 추가
- TopDown Engine 원본 코드 수정

## 추천 구현 구조

### 1. RoomEntryRuntimeController 외부 클리어 API 추가

현재 `HandleRoomCleared(RoomClearedPayload payload)`는 tracker 내부 이벤트로만 호출된다. 보스 사망처럼 외부 조건으로 방을 클리어하려면 public API가 필요하다.

추천 추가 메서드:

```csharp
public void NotifyCustomRoomCleared()
```

동작:

- 이미 클리어된 방이면 무시한다.
- `roomData`가 null이면 경고 후 무시한다.
- `new RoomClearedPayload(roomData.RoomId, roomData)`를 생성한다.
- 기존 `HandleRoomCleared(payload)` 흐름을 재사용한다.

중복 방지를 위해 `RoomEntryRuntimeController`에 `roomCleared` bool을 추가한다.

권장 구조:

```text
NotifyCustomRoomCleared()
  -> FireRoomCleared(new RoomClearedPayload(...))

HandleRoomCleared(payload)
  -> FireRoomCleared(payload)

FireRoomCleared(payload)
  -> 중복 방지
  -> exitWalls 해제
  -> RoomCleared 이벤트 발행
  -> bossTracker.TryMarkRoomCompleted
```

이렇게 하면 일반방 tracker 클리어와 보스 사망 클리어가 같은 후처리 경로를 사용한다.

### 2. BossDefeatRoomClearController 추가

신규 컴포넌트 위치:

```text
Assets/_Project/Scripts/Runtime/Stage/BossDefeatRoomClearController.cs
```

책임:

- 보스 `Health` 참조
- 보스방 `RoomEntryRuntimeController` 참조
- `Health.OnDeath` 구독
- 보스 사망 시 `RoomEntryRuntimeController.NotifyCustomRoomCleared()` 호출
- 중복 호출 방지

권장 필드:

```csharp
[SerializeField] private Health bossHealth;
[SerializeField] private RoomEntryRuntimeController roomController;
[SerializeField] private float clearDelaySeconds;
[SerializeField] private bool debugLogging;
```

`clearDelaySeconds`는 기본 0으로 시작한다. 사망 애니메이션을 끝까지 보고 싶으면 Inspector에서 조정할 수 있게 둔다.

### 3. RoomData_Sample_Boss 클리어 조건 변경

현재 보스방 데이터는 다음 상태다.

```text
roomType: 4      // Boss
clearCondition: 0 // AllEnemiesDefeated
```

보스방은 웨이브 적 전체 처치가 아니라 보스 사망으로 클리어되어야 하므로 다음으로 변경한다.

```text
clearCondition: 2 // Custom
```

이렇게 해야 `RoomEntryRuntimeController`가 기본 `AllEnemiesDefeatedTracker`로 즉시 클리어되거나 잘못된 적 카운트에 의존하지 않는다.

### 4. 보스방 프리팹/테스트 씬 연결

연결 대상:

```text
BossDefeatRoomClearController
  Boss Health     -> BerthaRoot 또는 BerthaRoot2의 Health
  Room Controller -> 보스방 RoomEntryRuntimeController
```

검증 기준 테스트 장소는 `BossArea_Test` 또는 보스방을 포함한 실제 테스트 씬으로 잡는다. 현재 `BossTest`는 보스 전투 단독 테스트 성격이 강하므로, 보스방 클리어/런 종료 흐름 검증에는 `RoomEntryRuntimeController`가 존재하는 씬 또는 프리팹이 필요하다.

## 구현 순서

1. `RoomEntryRuntimeController`에 중복 클리어 방지용 `roomCleared` bool 추가
2. `RoomEntryRuntimeController.NotifyCustomRoomCleared()` 추가
3. 기존 `HandleRoomCleared`를 공통 `FireRoomCleared` 경로로 정리
4. `BossDefeatRoomClearController` 추가
5. `RoomData_Sample_Boss.asset`의 `clearCondition`을 `Custom`으로 변경
6. 보스방 프리팹 또는 테스트 씬에 `BossDefeatRoomClearController` 배치
7. 보스 `Health`와 `RoomEntryRuntimeController` 연결
8. 보스 사망 시 `RoomCleared`가 1회만 발생하는지 확인
9. `RunManager`가 보스방 클리어를 `RunCleared -> Resulting`으로 처리하는지 확인

## 테스트 체크리스트

### 보스 사망 판정

- 보스 체력이 0 이하가 되면 `Health.OnDeath`가 발생하는가
- `BossDefeatRoomClearController`가 사망 이벤트를 1회만 처리하는가
- 보스 사망 후 `RoomEntryRuntimeController.NotifyCustomRoomCleared()`가 호출되는가

### 보스방 클리어

- `RoomCleared` 이벤트가 1회만 발행되는가
- 보스방 출구/벽이 해제되는가
- `BossRoomEntryTracker.TryMarkRoomCompleted`가 호출되는가
- 일반방 클리어 흐름에는 영향이 없는가

### 런 종료 흐름

- `RoomData.RoomType == Boss`인 상태에서 `RoomCleared`가 발생하는가
- `RunManager`가 `RunCleared`로 전환하는가
- 일정 시간 후 `Resulting`으로 전환하는가
- 결과 UI가 연결된 경우 활성화되는가

## 예상 리스크와 대응

### 1. 보스방 RoomData가 AllEnemiesDefeated이면 즉시 클리어될 수 있음

보스방에 encounter wave가 비어 있으면 `AllEnemiesDefeatedTracker`는 빈 wave를 즉시 클리어로 처리할 수 있다.

대응:

- 보스방 `RoomData.ClearCondition`을 `Custom`으로 변경한다.
- 보스 사망 브릿지가 직접 `NotifyCustomRoomCleared()`를 호출하게 한다.

### 2. 보스 사망과 tracker 클리어가 동시에 발생할 수 있음

방 설정이 잘못되어 tracker와 보스 사망 브릿지가 동시에 클리어를 발행할 수 있다.

대응:

- `RoomEntryRuntimeController`에 `roomCleared` bool을 두어 최종 발행을 1회로 제한한다.

### 3. BossTest에는 RoomEntryRuntimeController가 없을 수 있음

보스 전투 단독 테스트 씬에서는 방 클리어 흐름을 검증할 수 없다.

대응:

- 보스 패턴 검증은 `BossTest`에서 진행한다.
- CL-054 클리어/런 종료 검증은 `BossArea_Test` 또는 RoomEntryRuntimeController가 있는 씬에서 진행한다.

### 4. 보스 사망 애니메이션이 끝나기 전에 Resulting으로 넘어갈 수 있음

사망 즉시 클리어 처리하면 결과 화면이 너무 빨리 뜰 수 있다.

대응:

- `BossDefeatRoomClearController.clearDelaySeconds`를 둔다.
- 초기값은 0으로 두고, 연출 필요 시 Inspector에서 조정한다.

## 완료 기준

CL-054는 아래 조건을 만족하면 완료로 본다.

1. 보스 사망 시 보스방 클리어가 발생한다.
2. 보스방 `RoomCleared` 이벤트가 중복 발행되지 않는다.
3. 보스방 출구/벽이 해제된다.
4. `RunManager`가 보스방 클리어를 감지해 `RunCleared`로 전환한다.
5. `RunCleared` 이후 `Resulting`으로 전환된다.
6. 일반방 클리어 흐름이 기존과 동일하게 유지된다.
7. `dotnet build client/LostMemory/Assembly-CSharp.csproj --no-restore`가 통과한다.
8. Unity Play Mode에서 보스 처치 후 클리어 흐름을 수동 검증한다.

## 후속 연결

- 보스 처치 보상 UI 연결
- 결과 UI 데이터 집계
- 보스 처치 연출/VFX/SFX 추가
- 멀티플레이 권한 및 RPC 동기화

## 결론

CL-054는 새로운 보스 패턴을 추가하는 작업이 아니라, 보스 전투를 런 진행 흐름에 닫아주는 작업이다.

가장 안전한 구현 방향은 `Health.OnDeath`를 직접 방 시스템에 흘려보내지 않고, `BossDefeatRoomClearController`를 통해 `RoomEntryRuntimeController`의 공통 `RoomCleared` 경로를 재사용하는 것이다. 이렇게 하면 출구 해제, 보스방 완료 기록, `RunManager`의 `RunCleared -> Resulting` 전환을 기존 구조와 일관되게 사용할 수 있다.
