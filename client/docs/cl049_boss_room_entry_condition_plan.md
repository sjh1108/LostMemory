# CL-049 보스방 진입 조건 계산 구현 계획

## 문서 목적

`CL-049 보스방 진입 조건 계산 구현`의 방향을 정리한다.

이 스토리는 보스방 문을 실제로 열거나 씬을 이동시키는 작업이 아니라,  
`지금 보스방에 들어갈 수 있는가`를 일관된 규칙으로 계산하는 축을 먼저 만드는 것이 목적이다.

후속 스토리인 `CL-050 보스방 문 분기·입장 흐름 구현`,  
`CL-053 보스 체력 단계별 패턴 전환 처리 구현`,  
`CL-054 보스 처치 판정 구현`이 모두 이 조건 계산을 전제로 붙게 된다.

따라서 이번 단계에서는

- 스테이지 진행 규칙상 어떤 방이 보스 진입 선행 조건에 포함되는지,
- 현재 진행 상태에서 보스방 진입 가능 여부를 어떤 값으로 계산하는지,
- 솔로/코옵에서 누가 이 값을 확정하는지

를 먼저 고정한다.

## 대상 스토리

| Jira ID | 범위 | 담당 |
|---|---|---|
| CL-049 | 보스방 진입 조건 계산 구현 | 클라2 |

## 현재 기준

- 기획 문서 기준 1챕터 흐름은 `큰방 4개 -> 상점 1개 -> 보스방 1개` 구조다.
- 큰방은 각각 작은방 5개를 모두 돌파해야 클리어로 본다.
- 멀티플레이 규칙상 `보스방은 선행 전투를 모두 클리어해야 입장 가능`하다.
- 현재 프로젝트에는 `StageRouteManager`, `RoomCombatController`, `RunManager` 같은 스테이지 진행 런타임이 아직 없다.
- 즉, CL-049는 기존 문 열림 로직에 조건문 하나를 추가하는 작업이 아니라, 이후 보스방 흐름이 기대는 공통 계산 모듈을 먼저 세우는 작업에 가깝다.

관련 문서 기준:

- `docs/02_core_loop.md`
- `docs/04_multiplayer.md`
- `docs/12_development_plan.md`
- `docs/14_client_jira_story_backlog.md`

## 구현 목표

- 현재 스테이지의 방 진행 상태를 바탕으로 `보스방 진입 가능 여부`를 계산할 수 있어야 한다.
- 계산 결과는 단순 bool 하나가 아니라, 디버깅과 후속 로직 연결에 필요한 부가 정보까지 포함해야 한다.
- 기본 규칙은 `첫 번째 보스방 이전의 선행 전투 방을 모두 완료해야 입장 가능`으로 둔다.
- 상점방, 이벤트방, 보스방 자체는 기본적으로 선행 전투 조건에서 제외하되, 필요 시 예외적으로 포함/제외를 override할 수 있어야 한다.
- 솔로와 코옵 모두 같은 규칙을 사용하되, 코옵에서는 `호스트 기준 단일 계산`으로 확장 가능해야 한다.
- Unity 씬/프리팹 수정 없이 `_Project/Scripts/Runtime/Stage` 범위의 C# 로직만으로 먼저 정리할 수 있어야 한다.

## 비목표

- 보스방 문을 실제로 여는 연출 구현
- 보스방 입장 상호작용 UI 구현
- 씬 이동 또는 포탈 이동 처리
- 보스 체력, 패턴, 페이즈 전환 구현
- NGO 동기화 코드 작성
- 룸 스폰, 적 전멸 판정, 결과 화면 연결 완성

즉, 이번 스토리는 `조건 계산`까지만 다룬다.

## 기획 규칙 정리

### 1. 보스방 진입 기본 규칙

- 보스방은 `선행 전투를 모두 클리어`했을 때만 입장 가능하다.
- 현재 챕터 기본 구조에서는 `큰방 4개 클리어`가 핵심 조건이다.
- 상점방은 기본적으로 보스방 진입 조건에 포함하지 않는다.
- 이벤트방도 기본적으로 보스방 진입 조건에 포함하지 않는다.

### 2. 판정 기준

보스방 진입 조건 계산은 `첫 번째 보스방 이전에 있는 방 목록`만 본다.

이유:

- 보스방 이후의 방이 존재하더라도 현재 보스 진입 조건과는 무관하다.
- 다중 보스나 분기 구조가 생기더라도, 최소한 “현재 목표 보스 이전 구간”만 평가하는 축이 더 안전하다.

### 3. 완료 기준

각 방은 최소 두 상태를 가진다.

- `Visited`: 플레이어가 진입했는가
- `Completed`: 해당 방의 진행 조건을 끝냈는가

보스방 진입 조건에는 `Visited`가 아니라 `Completed`만 사용한다.

이유:

- 방에 들어가기만 하고 적을 다 잡지 않은 상태는 선행 전투 완료로 볼 수 없다.
- 코옵에서 일부 플레이어만 먼저 진입한 상태도 완료로 잘못 처리하면 안 된다.

### 4. Fail-Closed 원칙

다음 경우에는 기본적으로 `입장 불가`로 본다.

- 방 시퀀스에 보스방이 없음
- 보스 이전 선행 조건 방이 비정상적으로 비어 있음
- 룸 진행 상태가 아직 구성되지 않음

즉, 정보가 불완전하면 열어주지 않는 쪽으로 간다.

## 데이터 모델 제안

### StageRoomType

방의 성격을 표현한다.

후보:

- `Combat`
- `Shop`
- `Event`
- `Boss`
- 필요 시 `Unknown`

### BossEntryRequirementMode

특정 방이 보스 선행 조건에 포함되는지 override한다.

후보:

- `Auto`
  - 기본 규칙에 따름
- `Required`
  - 기본 규칙과 무관하게 선행 조건에 강제 포함
- `Ignored`
  - 기본 규칙과 무관하게 선행 조건에서 제외

이 구조를 두는 이유:

- 현재는 Combat만 선행 조건으로 보면 충분하지만,
- 이후 이벤트방이 “필수 전투 이벤트”가 되거나,
- 상점 대신 다른 필수 진행 방이 들어올 수 있다.

### StageRoomProgress

방 1개의 진행 상태를 표현한다.

필수 필드:

- `RoomId`
- `RoomType`
- `BossEntryRequirementMode`
- `IsVisited`
- `IsCompleted`

필수 기능:

- 방문 처리
- 완료 처리
- 진행 초기화
- 현재 방이 보스 진입 조건에 포함되는지 계산

### BossRoomEntryConditionResult

계산 결과를 담는다.

필수 필드:

- `HasBossRoom`
- `HasConfiguredRequirements`
- `RequiredRoomCount`
- `CompletedRequiredRoomCount`
- `RemainingRequiredRoomCount`
- `FirstIncompleteRoomSequenceIndex`
- `FirstIncompleteRoomId`
- `CanEnterBossRoom`

핵심은 결과를 단순 bool로 끝내지 않는 것이다.

이유:

- CL-050에서 문 잠금 메시지를 만들 때 남은 방 수를 보여줄 수 있다.
- 디버그 시 “왜 안 열리는지”를 빠르게 확인할 수 있다.
- 코옵 상태 불일치가 생겼을 때 어떤 방이 미완료인지 추적 가능하다.

### BossRoomEntryTracker

현재 스테이지의 방 시퀀스와 진행 상태를 보유하고 계산을 관리한다.

필수 기능:

- 전체 방 시퀀스 설정
- 특정 방 방문 처리
- 특정 방 완료 처리
- 현재 조건 재계산
- 조건 변경 이벤트 발행

## 계산 알고리즘 제안

입력:

- 현재 스테이지의 방 시퀀스 목록

출력:

- `BossRoomEntryConditionResult`

계산 순서:

1. 방 시퀀스를 앞에서부터 순회한다.
2. 첫 번째 `Boss` 타입 방을 만나면 그 시점에서 순회를 종료한다.
3. 그 이전 방 중 `CountsAsBossRequirement == true`인 방만 선행 조건 후보로 집계한다.
4. 후보 방 수를 `RequiredRoomCount`로 기록한다.
5. 그중 `IsCompleted == true`인 방 수를 `CompletedRequiredRoomCount`로 기록한다.
6. 첫 번째 미완료 선행 방의 인덱스와 ID를 저장한다.
7. 남은 방 수를 `RemainingRequiredRoomCount`로 계산한다.
8. `HasBossRoom && HasConfiguredRequirements && RemainingRequiredRoomCount == 0`일 때만 `CanEnterBossRoom = true`를 반환한다.

## 기본 예시

### 예시 1. 정상 1챕터

```text
0 Combat_A
1 Combat_B
2 Shop_A
3 Combat_C
4 Combat_D
5 Boss_A
```

기본 규칙:

- `Combat_A/B/C/D`만 선행 조건
- `Shop_A`는 제외

결과:

- 4개 전투방 모두 완료 시 `CanEnterBossRoom = true`

### 예시 2. 전투 3개만 끝난 상태

```text
Combat_A = 완료
Combat_B = 완료
Combat_C = 완료
Combat_D = 미완료
```

결과:

- `RequiredRoomCount = 4`
- `CompletedRequiredRoomCount = 3`
- `RemainingRequiredRoomCount = 1`
- `FirstIncompleteRoomId = Combat_D`
- `CanEnterBossRoom = false`

### 예시 3. 이벤트방을 필수 진행으로 강제 포함

```text
0 Combat_A
1 Event_B (Required override)
2 Shop_A
3 Combat_C
4 Boss_A
```

결과:

- `Combat_A`, `Event_B`, `Combat_C`가 선행 조건에 포함된다.

## 코옵 / 멀티플레이 고려

이번 CL-049에서는 NGO 코드를 직접 넣지 않더라도, 계산 구조는 코옵 기준으로 설계해야 한다.

### 원칙

- 보스방 진입 조건은 `호스트만` 확정한다.
- 클라이언트는 독자적으로 조건을 확정하지 않는다.
- 방 완료 처리도 호스트 기준 룸 시스템이 확정한 뒤 tracker를 갱신해야 한다.

### 권장 흐름

```text
호스트 RoomCombatController
  -> 방 클리어 확정
  -> BossRoomEntryTracker.TryMarkRoomCompleted(roomId)
  -> BossEntryConditionChanged 발생
  -> 보스 문 잠금 상태 갱신
  -> 결과를 클라이언트에 동기화
```

### 피해야 할 구조

```text
각 클라이언트가 자기 로컬에서
  -> 방 클리어 여부를 따로 판단
  -> 보스방 입장 가능 여부를 따로 계산
  -> 문 열림을 따로 표시
```

이 구조는 코옵에서 바로 어긋난다.

### 이번 스토리에서 열어둘 연결 지점

- tracker는 순수 계산기 + 상태 보관자로 둔다.
- 네트워크 계층은 이후 `NetworkBehaviour` 또는 호스트 진행 매니저가 감싼다.
- 즉, CL-049는 `멀티 호환 가능한 로컬 규칙 모듈`까지를 책임진다.

## CL-050 연계 방향

CL-050에서는 이 계산 결과를 실제 보스방 입장 흐름에 연결한다.

필요 연결:

- 문/포탈/상호작용 오브젝트가 `CanEnterBossRoom`을 구독
- 미완료 시 잠금 유지
- 완료 시 문 분기 또는 상호작용 허용
- 필요하면 `RemainingRequiredRoomCount` 기반 안내 문구 표시

권장 연결 예시:

```text
BossRoomDoorController
  -> BossRoomEntryTracker.CurrentCondition 확인
  -> CanEnterBossRoom == true 면 열림
  -> false 면 잠금 + 남은 방 수 표시
```

즉, CL-049는 문을 여는 스토리가 아니라, CL-050이 문을 열 수 있게 만드는 스토리다.

## 권장 구현 위치

```text
Assets/_Project/Scripts/Runtime/Stage/
  StageRoomType.cs
  BossEntryRequirementMode.cs
  StageRoomProgress.cs
  BossRoomEntryConditionResult.cs
  BossRoomEntryConditionCalculator.cs
  BossRoomEntryTracker.cs
```

네임스페이스:

```csharp
namespace LostMemory.Stage
```

이 위치가 맞는 이유:

- 프로젝트 폴더 구조 문서 기준으로 스테이지 진행, 큰방/상점/보스방 흐름은 `Runtime/Stage` 책임이다.
- TDE 원본 수정 없이 프로젝트 전용 계층으로 유지할 수 있다.

## 구현 순서 제안

1. `StageRoomType`, `BossEntryRequirementMode` enum 정의
2. `StageRoomProgress` 모델 작성
3. `BossRoomEntryConditionResult` 결과 구조 작성
4. `BossRoomEntryConditionCalculator` 작성
5. `BossRoomEntryTracker` 작성
6. 인스펙터와 런타임 양쪽에서 방 시퀀스 주입 가능한 API 정리
7. 기본 시나리오 기준 수동 검증
8. CL-050 연결 포인트 문서화

## 완료 기준

- 보스방 이전 선행 방의 완료 여부를 기준으로 보스방 진입 가능 여부를 계산할 수 있다.
- 상점방과 이벤트방은 기본 제외되지만 override로 포함/제외가 가능하다.
- 결과에 남은 선행 방 수와 첫 미완료 방 정보가 포함된다.
- 계산은 첫 번째 보스방 이전 구간만 평가한다.
- 솔로/코옵 모두 같은 계산 규칙을 공유할 수 있다.
- 호스트 authoritative 구조로 확장 가능한 API가 준비되어 있다.
- Unity 씬/프리팹 수정 없이 C# 계층만으로 정리되어 있다.

## 리스크와 열린 이슈

### 1. 큰방 단위 vs 작은방 단위 집계

현재 기획 문서는 보상과 보스 진입 조건을 큰방 단위로 읽는 것이 자연스럽다.  
하지만 실제 런타임이 작은방 단위로만 상태를 갖게 될 수도 있다.

권장안:

- CL-049는 방 타입 이름을 “실제 추적 단위”에 맞춘다.
- 만약 작은방 5개를 묶어 큰방 완료를 계산해야 한다면, 그 집계는 `RoomCombatController` 또는 `StageRouteManager`가 맡고,
- tracker에는 “큰방 완료 결과”만 넣는 편이 낫다.

### 2. 이벤트방 대체 규칙

이벤트방이 큰방 하나를 대체하는 구조가 되면,  
보스 선행 조건에 포함할지 제외할지 기획 확정이 필요하다.

대응:

- 이번 단계에서는 override 필드를 열어두고 기본값은 제외로 둔다.

### 3. 보스방이 여러 개일 가능성

현재 MVP는 보스 1개 기준이지만,  
후속 확장에서 중간 보스나 분기 보스가 생길 수 있다.

대응:

- 현재는 첫 번째 보스방 기준만 지원한다.
- 복수 보스가 필요해지면 “현재 목표 보스 인덱스”를 입력받는 형태로 확장한다.

### 4. 코옵 중도 참가 동기화

CL-049 자체 범위는 아니지만,  
중도 참가자가 현재 방 진행 상태를 어떻게 받는지는 이후 네트워크 계층에서 정리해야 한다.

대응:

- tracker는 현재 조건 전체를 한 번에 재평가할 수 있게 둔다.
- 후속 단계에서 호스트가 현재 room sequence와 progress snapshot을 전파하면 된다.

## 최종 정리

CL-049는 보스방 문을 여는 스토리가 아니라,  
`보스방 문이 언제 열려야 하는지`를 계산하는 공통 기준을 만드는 스토리다.

이번 단계에서 중요한 것은 화려한 씬 연출이 아니라

- 선행 조건 규칙을 명확히 고정하고,
- 보스 이전 구간만 평가하며,
- 방 완료 상태와 코옵 호스트 권한을 분리해두는 것

이다.

이 축이 먼저 잡혀야 CL-050 이후의 문 분기, 보스 입장, 처치 판정, 결과 흐름이 안정적으로 이어진다.
