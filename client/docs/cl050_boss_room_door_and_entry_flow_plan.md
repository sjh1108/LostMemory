# CL-050 보스방 문 분기·입장 흐름 구현 계획

## 문서 목적

`CL-050 보스방 문 분기·입장 흐름 구현`의 방향을 정리한다.

`CL-049`가 `지금 보스방에 들어갈 수 있는가`를 계산하는 공통 기준을 만드는 스토리였다면,  
`CL-050`은 그 계산 결과를 실제 방 출구, 보스방 문, 플레이어 상호작용, 코옵 진입 흐름에 연결하는 단계다.

즉, 이번 스토리의 핵심은 다음 세 가지다.

- 보스방 선행 조건이 충족되기 전에는 보스방 문이 잠겨 있어야 한다.
- 선행 조건이 충족되면 다음 출구가 일반 다음 방이 아니라 보스방으로 분기되어야 한다.
- 솔로/코옵 모두 보스방 입장 상호작용이 일관되게 동작해야 한다.

## 대상 스토리

| Jira ID | 범위 | 담당 |
|---|---|---|
| CL-050 | 보스방 문 분기·입장 흐름 구현 | 클라2 |

## 선행 기준

- `CL-049 보스방 진입 조건 계산 구현` 완료
- `CL-035 적 전멸 기준 방 클리어 판정 구현`에서 방 완료 확정이 가능해야 함
- `CL-036 문 열림·다음 방 전환 흐름 구현`이 이후 들어오더라도 충돌 없이 합쳐질 수 있어야 함
- 멀티플레이 규칙 문서 기준 `호스트 기반 진행`, `보스방은 선행 전투를 모두 클리어해야 입장 가능`, `전원 상호작용 시 다음 방 이동 권장`

관련 문서:

- `client/docs/cl049_boss_room_entry_condition_plan.md`
- `docs/02_core_loop.md`
- `docs/04_multiplayer.md`
- `client/docs/commonness/global-event-keys-and-hook-points.md`
- `docs/14_client_jira_story_backlog.md`

## 현재 기준

- 1챕터 흐름은 `큰방 4개 -> 상점방 1개 -> 보스방 1개` 기준이다.
- 보스방은 선행 전투를 모두 클리어해야 입장 가능하다.
- 현재 프로젝트에는 보스방 문 상태를 제어하는 전용 런타임이 없다.
- `CL-049`에서 `BossRoomEntryTracker`와 계산 결과 구조를 추가해 `CanEnterBossRoom`, 남은 필수 방 수, 첫 미완료 방 정보를 조회할 수 있게 됐다.
- 현재 프로젝트에는 `StageRouteManager`, `RoomExitController`, `BossRoomDoorController` 같은 스테이지 흐름 런타임이 아직 정식으로 없다.
- 검색 결과, 재사용 가능한 공용 출구/포탈 시스템은 발견되지 않았다.
- 대신 테스트 전용 코드로 `LostMemory.TestKhi.TestKhiDoorAction`, `LostMemory.TestKhi.TestKhiInteractionZone`이 존재한다.
- 이 테스트 코드는 `ButtonActivatedZone` 기반 상호작용과 단순 문 열림/닫힘 토글 예시를 제공하지만, `Player1` 하드코딩과 테스트 전용 네임스페이스를 포함하므로 본 구현에 직접 사용하지 않는다.

즉, CL-050은 기존 보스방 문 구현을 수정하는 작업이 아니라,  
처음으로 `보스방 문 상태 + 보스방 입장 상호작용 + 입장 승인 흐름`을 만드는 작업이 된다.

## 구현 목표

- `CL-049` 계산 결과를 바탕으로 보스방 문이 `잠김/해제` 상태를 가진다.
- 마지막 선행 방이 클리어되면 보스방 문 상태가 잠김에서 해제로 전환된다.
- 플레이어는 해제된 보스방 문 또는 보스방 출구 오브젝트에 상호작용해 입장을 요청할 수 있다.
- 솔로에서는 플레이어 1명의 상호작용으로 즉시 보스방 입장 흐름이 확정된다.
- 코옵에서는 호스트가 준비 상태를 모으고, 조건이 충족되면 보스방 입장을 최종 확정한다.
- 보스방 입장 요청, 준비 취소, 입장 확정, 입장 시작 흐름이 디버깅 가능한 상태 값으로 드러나야 한다.
- 가능하면 `CL-036`의 일반 출구 흐름을 재사용하고, 보스방만 예외 분기 처리하도록 설계한다.

## 비목표

- 보스 AI, 보스 패턴, 보스 페이즈 구현
- 보스방 처치 판정 및 결과 화면 연결
- 정교한 UI/연출 완성
- NGO 동기화 구현 완료
- 씬 구조 전체 리팩터링
- 보스방 전용 최종 아트/사운드 확정

## 핵심 결정 사항

### 1. 보스방 문은 CL-049 결과를 직접 구독한다

보스방 문은 자체적으로 방 완료를 다시 계산하지 않는다.

권장 구조:

```text
Room clear / route progress
  -> BossRoomEntryTracker
  -> BossRoomDoorController
      -> lock / unlock / prompt / entry flow
```

피해야 할 구조:

```text
BossRoomDoorController
  -> 현재까지 클리어한 방 수를 여기서 직접 계산
```

### 2. 문 분기와 입장 확정은 분리한다

CL-050에서 “문 분기”는

- 다음 목적지가 보스방이라는 사실을 결정하는 것

이고,

“입장 흐름”은

- 실제 플레이어 상호작용과 준비 상태를 모아 보스방 진입을 시작하는 것

이다.

둘은 같은 스토리 범위지만 책임은 분리하는 편이 낫다.

### 2-1. 테스트용 문 스크립트는 참고만 한다

현재 프로젝트에는 테스트용 문/상호작용 스크립트가 있다.

- `TestKhiDoorAction`
- `TestKhiInteractionZone`

이 코드는 다음 패턴 참고용으로만 사용한다.

- collider/sprite 기반 문 잠금 상태 토글
- TDE `ButtonActivatedZone` 기반 상호작용 처리

직접 재사용하지 않는 이유:

- `LostMemory.TestKhi` 테스트 전용 네임스페이스
- 특정 테스트 씬 전제
- `Player1` 고정 조건 포함
- 보스방 조건 계산, 코옵 ready 집계, 호스트 확정, destination 분기 책임이 없음

따라서 CL-050 본 구현은 `LostMemory.Stage` 아래 새로 만든다.

### 3. 솔로와 코옵 흐름을 같은 상태 머신으로 다룬다

솔로는 코옵 흐름의 특수 케이스로 본다.

- 솔로: 준비 인원 1명
- 코옵: 준비 인원 2명 이상

이렇게 두면 분기 코드가 줄어든다.

### 4. 코옵 최종 확정은 호스트만 한다

멀티플레이에서는 보스방 입장 확정이 반드시 호스트 authoritative 이어야 한다.

클라이언트는 다음만 한다.

- 상호작용 요청
- 준비 취소 요청
- 입장 상태 UI 표시

최종적으로 보스방 문이 열리고 전환이 시작되는지는 호스트가 확정한다.

### 5. 다운된 플레이어는 준비 완료 인원에 포함하지 않는다

현재 멀티 규칙상 다운/전멸은 별도 처리 대상이다.

권장안:

- 보스방 입장 준비 인원은 `연결되어 있고, 살아 있으며, Down 상태가 아닌 플레이어`만 집계한다.
- 다운 플레이어가 있으면 먼저 부활시키거나 패배 흐름으로 가야 한다.

이 기준이 가장 단순하고 예외가 적다.

## 플레이어 체감 흐름

### 솔로

```text
마지막 선행 전투 클리어
  -> 보스방 문 해제
  -> 플레이어가 문에 상호작용
  -> 호스트(자기 자신) 즉시 입장 확정
  -> 보스방 입장 시작
```

### 코옵

```text
마지막 선행 전투 클리어
  -> 보스방 문 해제
  -> 플레이어 A 상호작용
  -> A ready
  -> 플레이어 B 상호작용
  -> B ready
  -> 호스트가 eligible player 전원 ready 확인
  -> 보스방 입장 확정
  -> 파티 전체 보스방 입장 시작
```

### 코옵 준비 취소

```text
플레이어 A ready
  -> 문에서 멀어짐 / cancel / 상태 이탈
  -> A ready 해제
  -> 전원 ready 조건 미충족
  -> 입장 시작 안 함
```

## 상태 모델 제안

보스방 문 또는 입장 게이트는 다음 상태를 가진다.

| 상태 | 설명 |
|---|---|
| `Locked` | CL-049 조건 미충족. 상호작용 불가 |
| `UnlockedIdle` | 조건 충족. 상호작용 가능, 아직 아무도 ready 아님 |
| `WaitingForParty` | 1명 이상 ready, 아직 전원 ready 아님 |
| `EntryConfirmed` | 호스트가 전원 ready를 확인하고 입장을 확정한 상태 |
| `Transitioning` | 실제 보스방 진입 처리 중 |

권장 규칙:

- `Locked -> UnlockedIdle`
  - `CanEnterBossRoom == true`가 되는 순간
- `UnlockedIdle -> WaitingForParty`
  - 첫 플레이어가 ready
- `WaitingForParty -> EntryConfirmed`
  - eligible player 전원 ready
- `EntryConfirmed -> Transitioning`
  - 보스방 진입 처리 시작
- `WaitingForParty -> UnlockedIdle`
  - ready 플레이어가 모두 해제됨
- 언제든 선행 조건이 다시 무효화되면 `Locked`로 강등
  - MVP에서는 거의 없겠지만 보호 로직은 두는 편이 안전

## 구성 요소 제안

### 1. BossRoomDoorController

보스방 문의 핵심 상태와 상호작용 흐름을 담당한다.

책임:

- `BossRoomEntryTracker` 구독
- 문 잠금/해제 상태 갱신
- 상호작용 가능 여부 판단
- ready 플레이어 상태 관리 요청
- 입장 확정 요청
- 입장 시작 이벤트 발행

권장 위치:

```text
Assets/_Project/Scripts/Runtime/Stage/
```

### 2. BossRoomDoorView

문 비주얼과 상태 표시를 담당한다.

책임:

- 잠금 상태 표시
- 해제 상태 표시
- ready 인원 수 표시
- 상호작용 안내 텍스트 표시

MVP에서는 없어도 되지만, 문 로직과 렌더링/표시를 분리할 수 있으면 유지보수에 유리하다.

### 2-1. BossRoomEntryInteractionZone

플레이어의 보스방 문 상호작용을 받는 전용 컴포넌트.

책임:

- 상호작용 가능 플레이어 감지
- 상호작용 입력 수신
- ready on/off 요청 전달
- 존 이탈 시 ready 해제 정책 반영

구현 방향:

- 테스트용 `TestKhiInteractionZone`처럼 TDE `ButtonActivatedZone` 패턴을 참고
- 하지만 프로젝트 전용 네임스페이스와 보스방 정책에 맞게 새로 구현

### 3. BossRoomEntryParticipantState

코옵 진입 준비 상태를 담는 얇은 구조체 또는 모델.

필드 예시:

- `PlayerId`
- `IsEligible`
- `IsInsideInteractionZone`
- `IsReady`

### 4. BossRoomEntryCoordinator

준비 인원 집계와 전원 ready 조건을 담당하는 별도 클래스로 분리할 수 있다.

MVP에서는 `BossRoomDoorController` 안에 포함해도 되지만,  
코옵 흐름이 길어질 경우 분리해두는 편이 낫다.

책임:

- eligible player 목록 갱신
- ready on/off 처리
- 전원 ready 판정
- ready 해제 처리

### 5. BossRoomDestinationResolver

문 분기 관점에서 “이 출구가 어디로 가야 하는가”를 결정한다.

가능한 구현 방향:

- `CL-036`의 일반 다음 방 전환 흐름이 있으면 그 위에 분기만 추가
- 없다면 보스방 전용 임시 resolver를 둔다

핵심은 문 로직이 씬 이동 세부 구현까지 직접 알지 않게 하는 것이다.

## 데이터 및 상태 값 제안

### 문 기본 필드

- `entryTracker`
- `interactionZone`
- `doorView`
- `requiredPlayersPolicy`
- `allowAutoEnterInSolo`
- `cancelReadyWhenLeaveZone`
- `debugLogging`

### 코옵 준비 정책

정책 예시:

- `SoloImmediate`
- `AllEligiblePlayersReady`

MVP에서는 내부적으로 자동 선택해도 된다.

- eligible player 수가 1명이면 `SoloImmediate`
- 2명 이상이면 `AllEligiblePlayersReady`

### 입장 타깃 정보

보스방 입장 처리에 필요한 최소 정보:

- `BossRoomId`
- `BossEntryPointId`
- 필요 시 `SceneName`

지금 단계에서 실제 씬 이동 방식이 확정되지 않았다면,  
문서는 “문은 destination key만 넘기고 실제 이동은 route/scene flow가 처리”하는 방향으로 잡는 것이 안전하다.

## 이벤트 / 훅 포인트 제안

기존 전역 이벤트 문서 기준으로 다음 키를 권장한다.

| 이벤트 키 | 설명 | 네트워크 범위 |
|---|---|---|
| `BossRoom.EntryUnlocked` | 보스방 문 잠금 해제 확정 | `Broadcast` |
| `BossRoom.EntryLocked` | 보스방 문 잠금 상태 복귀 | `Broadcast` |
| `BossRoom.EntryReadyChanged` | 파티 ready 상태 변경 | `Broadcast` |
| `BossRoom.EntryRequested` | 플레이어의 입장 상호작용 요청 | `ClientRequest` |
| `BossRoom.EntryConfirmed` | 호스트의 입장 확정 | `ServerAuthority` |
| `BossRoom.EntryStarted` | 실제 보스방 전환 시작 | `Broadcast` |

가능한 훅 포인트:

- `BossRoomEntryTracker.BossEntryConditionChanged`
  - 문 잠금/해제 판단
- `CharacterDetector` 또는 상호작용 Zone
  - ready 후보 판정
- `Input System` interact action
  - 입장 요청
- `SceneFlowManager` 또는 `StageRouteManager`
  - 실제 보스방 전환 시작

## 코옵 세부 규칙 제안

### Eligible player 정의

아래 조건을 모두 만족하는 플레이어만 ready 대상에 포함한다.

- 현재 세션에 연결되어 있음
- 현재 스테이지에 존재함
- 사망 또는 전멸 상태가 아님
- Down 상태가 아님

### Ready 조건

- 플레이어가 문 상호작용 존 안에 있어야 한다.
- 문이 해제 상태여야 한다.
- 플레이어가 상호작용 입력을 하면 ready가 된다.
- ready 이후 존을 벗어나면 ready를 자동 해제할지 여부는 정책으로 둔다.

MVP 권장안:

- 존 이탈 시 ready 자동 해제

이유:

- “문 앞에 모인다”는 체감이 더 명확하다.
- 준비 중 이탈 플레이어를 별도 예외 처리할 필요가 줄어든다.

### 전원 ready 판정

- 호스트는 eligible player 전원의 `IsReady == true`를 확인해야 한다.
- 이 순간 `EntryConfirmed` 상태로 넘어간다.
- 확인 이후 곧바로 `Transitioning`에 들어가 보스방 전환을 시작한다.

### 준비 취소

다음 경우 ready를 해제한다.

- 플레이어가 다시 상호작용해 cancel
- 플레이어가 상호작용 존을 벗어남
- 플레이어가 Down 상태가 됨
- 세션에서 이탈함

## 분기 처리 방향

CL-050의 “문 분기”는 실제로는 다음 중 하나를 의미한다.

### 방향 A. 마지막 일반 출구가 보스방으로 연결됨

```text
마지막 큰방 클리어
  -> 기존 다음 방 문 사용
  -> 목적지만 보스방으로 분기
```

장점:

- CL-036 일반 출구 흐름 재사용 가능
- 씬 구조 변경이 적음

단점:

- “보스방 전용 문” 존재감이 약할 수 있음
- 현재 프로젝트에는 이 방식을 바로 재사용할 공용 출구 런타임이 없음

### 방향 B. 보스방 전용 문/포탈이 별도로 열림

```text
마지막 큰방 클리어
  -> 일반 출구와 별개로 보스 문 활성화
```

장점:

- 플레이어 체감이 명확함
- 연출 포인트를 잡기 쉬움

단점:

- 씬/프리팹 변경 폭이 더 커질 수 있음

MVP 권장안:

- 현재는 공용 출구 흐름이 없으므로 방향 B 우선
- 단순한 전용 문/포탈 오브젝트를 만들고, 이후 CL-036 공용 출구 흐름이 생기면 통합 여부를 검토
- 즉, CL-050 1차 구현은 “보스 전용 입장 게이트 신설”이 현실적인 기준이다

핵심은 연출보다 흐름 안정성이다.

## Unity 작업 범위 주의사항

CL-050 구현은 문/포탈/상호작용 존을 실제 씬이나 프리팹에 연결해야 할 가능성이 높다.

즉, 코드만으로 끝나지 않을 가능성이 크다.

주의:

- 공용 씬 또는 공용 프리팹을 직접 수정해야 하면 사용자 승인 또는 담당자 합의가 먼저 필요하다.
- 외부 에셋 원본 프리팹은 수정하지 않는다.
- 가능하면 `_Project/Prefabs` 아래 복제본 또는 테스트 씬에서 먼저 검증한다.

권장 테스트 흐름:

- `Assets/_Project/Scenes/Test/` 또는 기존 테스트 씬 복제본 사용
- 보스방 입장 전용 문/포탈은 `_Project` 프리팹으로 복제 후 연결
- 테스트용 `test_khi.unity`의 문/상호작용 패턴은 참고 가능하지만, 해당 씬의 컴포넌트를 본 구현 자산으로 승격하는 방식은 피한다

## 구현 순서 제안

1. `CL-049` tracker를 구독하는 `BossRoomDoorController` 초안 작성
2. `BossRoomEntryInteractionZone` 초안 작성
3. 문 잠금/해제 상태 전환 구현
4. 상호작용 요청 처리 구현
5. 솔로 즉시 입장 흐름 구현
6. 코옵 ready 집계 구조 구현
7. 전원 ready 시 호스트 확정 처리 구현
8. 보스방 destination 분기 연결
9. 테스트 씬에서 수동 검증
10. 코옵 예외 흐름 검증

## 테스트 시나리오 제안

### 1. 조건 미충족

- 선행 전투 미완료 상태
- 문이 잠겨 있음
- 상호작용 불가 또는 안내 메시지 표시

### 2. 솔로 잠금 해제

- 마지막 선행 전투 완료
- 문 해제
- 1회 상호작용으로 즉시 입장 시작

### 3. 코옵 1명만 ready

- 문 해제
- 플레이어 A만 ready
- `WaitingForParty` 유지
- 입장 시작 안 됨

### 4. 코옵 전원 ready

- A ready, B ready
- 호스트 확정
- 보스방 전환 시작

### 5. 코옵 ready 취소

- A/B ready 후 A가 존 이탈
- ready 해제
- 다시 `WaitingForParty` 또는 `UnlockedIdle`

### 6. 다운 플레이어 예외

- 파티원 중 1명 Down
- ready 인원 집계에서 제외 또는 입장 불가 정책 확인

MVP 권장안:

- Down 플레이어가 있으면 입장 불가

이 편이 전투/부활 규칙과 충돌이 적다.

## 완료 기준

- 보스방 문이 `CL-049` 결과에 따라 잠금/해제된다.
- 선행 조건 미충족 상태에서는 보스방 입장 요청이 거부된다.
- 조건 충족 상태에서는 솔로/코옵 모두 상호작용이 가능하다.
- 솔로에서는 상호작용 1회로 보스방 입장이 시작된다.
- 코옵에서는 eligible player 전원 ready 후 호스트가 입장을 확정한다.
- 준비 취소, 존 이탈, 다운, 세션 이탈에 대응해 ready 상태가 정리된다.
- 문 상태와 입장 흐름이 디버깅 가능한 로그 또는 상태 값으로 확인 가능하다.
- 일반 다음 방 흐름과 보스방 분기 책임이 뒤섞이지 않게 유지된다.

## 리스크와 열린 이슈

### 1. CL-036 일반 출구와 책임 중복

현재는 공용 출구 구현이 없지만, 이후 CL-036이 정식 런타임으로 들어오면 책임 중복이 생길 수 있다.

대응:

- CL-050 1차 구현은 보스 전용 게이트를 독립적으로 만든다
- 이후 CL-036이 들어오면 상호작용/이동 공통 부분만 상위 계층으로 올리는 방식으로 정리한다

### 2. 씬 구조 미확정

보스방이 같은 씬 내부 이동인지, 별도 씬 로드인지 아직 구현 기준이 불명확할 수 있다.

대응:

- 문에서는 destination key만 결정
- 실제 이동은 route/scene flow 계층으로 위임

### 3. 코옵 ready 정책 세부 확정 필요

전원 상호작용이 “권장”인지 “필수”인지 문서상 완전히 강제되진 않았다.

MVP 권장안:

- CL-050에서는 전원 ready를 필수로 구현

이유:

- 멀티 상태 불일치를 줄이는 데 유리하다.
- 발표용 협동 체감도 더 명확하다.

### 4. Down 플레이어 처리

Down 플레이어를 보스방에 끌고 들어갈지, 먼저 부활을 강제할지 기획 확정이 필요하다.

MVP 권장안:

- Down 플레이어가 있으면 보스방 입장 불가

이 기준이 가장 단순하고 버그 가능성이 낮다.

## 최종 정리

CL-050은 “문을 여는 연출”보다

- 보스방 출구가 언제 해제되는지,
- 누가 입장을 요청할 수 있는지,
- 코옵에서 언제 최종 입장이 확정되는지

를 일관된 흐름으로 만드는 스토리다.

이번 단계에서 중요한 것은

- CL-049 계산 결과를 문 로직이 직접 재사용하고,
- 현재 공용 출구 런타임이 없다는 전제를 명확히 받아들이고,
- 테스트용 문 스크립트는 참고만 하되 본 구현은 `Stage` 계층에 새로 만들고,
- 문 분기와 입장 확정을 분리하며,
- 코옵에서는 호스트 authoritative 준비/확정 흐름을 명확히 두는 것

이다.

이 축이 잡혀야 이후 보스방 진입 연출, 보스 시작 이벤트, 보스 처치 후 결과 흐름까지 안정적으로 이어질 수 있다.
