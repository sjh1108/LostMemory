# CL-055 보스 클리어 후 결과 흐름 연결 구현 계획

## 문서 목적

CL-055 구현 범위와 작업 순서를 정리한다.

CL-054에서 보스 사망 시 `RoomCleared`가 발생하는 흐름까지 구현했다. CL-055는 이 클리어 이벤트 이후 실제 런 종료 결과 화면까지 자연스럽게 이어지도록 `RunManager`와 결과 UI 데이터 연결을 완성하는 작업으로 잡는다.

> 정확한 Jira 제목이 별도로 있다면 이 문서는 해당 제목에 맞춰 범위를 조정한다. 현재 코드 기준으로는 `RunCleared -> Resulting -> RunResultPanelView.Show(data)` 연결이 가장 자연스러운 다음 단계다.

## 대상 스토리

| Jira ID | 범위 | 담당 |
|---|---|---|
| CL-055 | 보스 클리어 후 결과 화면 데이터 연결 | 클라 |

## 현재 상태 정리

현재 코드베이스에는 다음 기반이 이미 존재한다.

- `BossDefeatRoomClearController`
  - 보스 `Health.OnDeath`를 감지한다.
  - `RoomEntryRuntimeController.NotifyCustomRoomCleared()`를 호출한다.
- `RoomEntryRuntimeController`
  - `RoomCleared` 이벤트를 발행한다.
  - 보스방 `RoomData`의 `roomType`은 `Boss`다.
- `RunManager`
  - `RoomEntryRuntimeController.RoomCleared`를 구독한다.
  - 보스방 클리어 시 `RunCleared` 상태로 전환한다.
  - 일정 시간 뒤 `Resulting` 상태로 전환한다.
  - 현재 `ShowResultingUI()`는 `RunResultPanelView`를 단순 활성화하는 stub 상태다.
- `RunResultData`
  - 결과 화면에 표시할 값 구조체가 존재한다.
  - `KillCount`, `BossKillCount`, `TotalDamage`, `PlayTime`, `MemoryFragments` 필드를 가진다.
- `RunResultPanelView`
  - `Show(RunResultData data)` API가 존재한다.
  - 받은 데이터를 텍스트 UI에 표시한다.
- `RewardPanelView`
  - 보상 3택 UI는 존재하지만 현재 런 종료 흐름과 직접 연결되어 있지는 않다.

즉 CL-055의 핵심은 결과 UI 자체를 새로 만드는 것이 아니라, 런 진행 중 필요한 결과 데이터를 수집하고 `RunManager`가 `Resulting` 진입 시 `RunResultPanelView.Show(data)`를 호출하도록 연결하는 것이다.

## 목표 흐름

```text
Boss Health.OnDeath
  -> BossDefeatRoomClearController
  -> RoomEntryRuntimeController.RoomCleared
  -> RunManager: InRun -> RunCleared
  -> delay
  -> RunManager: RunCleared -> Resulting
  -> RunResultData 생성
  -> RunResultPanelView.Show(data)
```

## 구현 범위

### CL-055 MVP

- `RunManager`가 런 시작 시점부터 플레이 시간을 기록한다.
- `RunManager`가 보스방 클리어 시 보스 처치 수를 결과 데이터에 반영한다.
- `RunManager.ShowResultingUI()`에서 `RunResultPanelView.Show(RunResultData)`를 호출한다.
- 결과 패널이 없을 때는 기존처럼 로그만 출력하고 흐름은 중단하지 않는다.
- 결과 패널 버튼 이벤트가 필요하면 `CloseResulting()`에 연결한다.
- `BossTest` 또는 통합 씬에서 `RoomCleared -> RunCleared -> Resulting -> 결과 UI 표시` 흐름을 검증한다.

### CL-055에서 보류할 항목

- 실제 전체 킬 수 집계
- 실제 총 피해량 집계
- 실제 메모리 조각 보상 계산
- 보상 3택 UI와 결과 화면의 최종 UX 연결
- 저장 데이터 반영
- 멀티플레이 동기화

위 항목들은 데이터 소스와 정책이 더 필요하므로 CL-055에서는 기본값 또는 임시 집계로 처리하고, 후속 CL에서 정확한 수집기로 확장하는 편이 안전하다.

## 권장 구현 구조

### 1. RunManager에 런 결과 누적 상태 추가

`RunManager` 내부에 런 단위 누적 필드를 추가한다.

```csharp
private float runStartedAt;
private int killCount;
private int bossKillCount;
private int totalDamage;
private int memoryFragments;
```

초기화 시점:

```text
StartRun()
  -> runStartedAt = Time.time
  -> 누적 값 0으로 초기화
```

### 2. 보스방 클리어 시 보스 처치 수 반영

`HandleRoomCleared(RoomClearedPayload payload)`에서 보스방 클리어를 감지했을 때 `bossKillCount`를 증가시킨다.

중복 방지를 위해 `RunManager`가 이미 `RunCleared`로 전환된 뒤에는 추가 집계하지 않도록 한다.

```text
if current state is InRun and payload.Data.RoomType == Boss
  -> bossKillCount += 1
  -> RunCleared 전환
```

### 3. RunResultData 생성 메서드 추가

`RunManager`에 결과 데이터를 생성하는 private 메서드를 둔다.

```csharp
private RunResultData BuildRunResultData()
```

초기 구현 값:

- `KillCount`: 현재 집계값, 데이터 소스 없으면 0
- `BossKillCount`: 보스방 클리어 시 증가한 값
- `TotalDamage`: 현재 집계값, 데이터 소스 없으면 0
- `PlayTime`: `Time.time - runStartedAt`
- `MemoryFragments`: 현재 집계값, 데이터 소스 없으면 0

이렇게 하면 후속 CL에서 킬/피해량/재화 수집 로직을 추가해도 결과 UI API는 그대로 유지할 수 있다.

### 4. ShowResultingUI stub 제거

현재 `ShowResultingUI()`는 패널을 단순 활성화한다.

변경 후:

```text
if runResultPanelView == null
  -> 로그 출력 후 return

RunResultData data = BuildRunResultData()
runResultPanelView.Show(data)
```

이때 `RunResultPanelView.Show()`가 이미 `gameObject.SetActive(true)`를 수행하므로 `RunManager`에서 직접 `SetActive(true)`만 호출하지 않도록 한다.

### 5. 결과 패널 버튼 연결

`RunResultPanelView`에는 이미 `OnRestart`, `OnLobby` 이벤트가 존재한다.

CL-055 MVP에서는 최소한 다음 정도만 연결한다.

```text
OnLobby 또는 OnRestart
  -> RunManager.CloseResulting()
```

실제 씬 전환, 재시작 빌드, 로비 이동은 후속 CL에서 구현한다.

## 구현 순서

1. `RunManager`에 결과 누적 필드와 초기화 메서드 추가
2. `StartRun()` 또는 `HandleDungeonBuilt()` 시점에 런 시작 시간/결과 값 초기화
3. 보스방 `RoomCleared` 처리 시 `bossKillCount` 증가
4. `BuildRunResultData()` 추가
5. `ShowResultingUI()`가 `runResultPanelView.Show(data)`를 호출하도록 변경
6. `RunResultPanelView` 버튼 이벤트를 `CloseResulting()`에 연결
7. `dotnet build client/LostMemory/Assembly-CSharp.csproj --no-restore` 실행
8. Unity Play Mode에서 보스 처치 후 결과 패널 표시 확인

## 테스트 체크리스트

### 보스 클리어 흐름

- 보스 처치 시 `RoomCleared`가 1회 발생하는가
- `RunManager`가 `RoomCleared`를 수신하는가
- 보스방 클리어 시 `InRun -> RunCleared`로 전환되는가
- 지연 시간 이후 `RunCleared -> Resulting`으로 전환되는가

### 결과 UI

- `RunResultPanelView.Show(data)`가 호출되는가
- 결과 패널이 활성화되는가
- 보스 처치 수가 1로 표시되는가
- 플레이 시간이 0보다 큰 값으로 표시되는가
- 아직 데이터 소스가 없는 항목은 0으로 표시되어도 오류가 없는가

### 버튼

- 결과 패널의 로비/재시작 버튼 클릭 시 예외가 발생하지 않는가
- 버튼 클릭 시 `CloseResulting()`으로 상태가 `None`으로 돌아가는가

## 예상 리스크와 대응

### 1. 실제 통합 씬에 RunResultPanelView가 연결되어 있지 않을 수 있음

대응:

- `runResultPanelView == null`이면 경고 로그만 출력하고 예외를 내지 않는다.
- 프리팹/씬 연결은 별도 체크리스트로 관리한다.

### 2. KillCount, TotalDamage 데이터 소스가 아직 없음

대응:

- CL-055에서는 0 또는 현재 수집 가능한 값만 표시한다.
- 후속 CL에서 적 처치 이벤트, 피해 이벤트, 재화 획득 이벤트를 `RunManager` 또는 별도 `RunResultTracker`로 연결한다.

### 3. BossTest에서는 RunManager 전체 흐름을 보기 어려움

대응:

- `BossTest`는 `RoomCleared` 검증용으로 유지한다.
- CL-055 검증은 `RunManager`가 존재하고 `RoomEntryRuntimeController`를 구독하는 통합 씬에서 수행한다.

### 4. 결과 패널 버튼의 최종 동작이 아직 정해지지 않음

대응:

- MVP에서는 `CloseResulting()`만 연결한다.
- 로비 이동, 재시작, 저장 반영은 후속 CL에서 처리한다.

## 완료 기준

CL-055는 다음 조건을 만족하면 완료로 본다.

1. 보스방 `RoomCleared` 후 `RunManager`가 `RunCleared` 상태로 전환한다.
2. 일정 시간 후 `Resulting` 상태로 전환한다.
3. `RunResultData`가 생성된다.
4. `RunResultPanelView.Show(data)`가 호출된다.
5. 결과 패널에 최소한 보스 처치 수와 플레이 시간이 표시된다.
6. 결과 패널 버튼 클릭 시 예외 없이 닫기 흐름이 동작한다.
7. `dotnet build client/LostMemory/Assembly-CSharp.csproj --no-restore`가 통과한다.

## 결론

CL-055는 새 보스 로직을 추가하는 작업이 아니라, CL-054에서 열린 보스방 클리어 이벤트를 런 종료 결과 화면까지 연결하는 작업으로 진행한다.

가장 작은 안전한 구현은 `RunManager`가 결과 데이터를 직접 최소 집계하고, `Resulting` 진입 시 기존 `RunResultPanelView.Show(data)`를 호출하게 만드는 것이다. 이렇게 하면 결과 UI 표시 흐름을 먼저 완성하고, 킬 수/피해량/보상 같은 세부 데이터는 후속 작업에서 안정적으로 확장할 수 있다.
