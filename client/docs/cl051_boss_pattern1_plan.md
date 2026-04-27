# CL-051 보스 1종 패턴 1 구현 계획

## 문서 목적

`CL-051 보스 1종 패턴 1 구현`을 현재 프로젝트 상태에 맞게 잘라서 정의한다.

이번 계획서는 아래 4가지를 먼저 고정한다.

- 보스 패턴 카탈로그를 7개로 확정한다.
- 보스 공격 철학은 `직접 접촉딜`보다 `범위공격 + 돌진` 중심으로 둔다.
- 조합 패턴은 별도 신규 시스템이 아니라 `기본 패턴 조합`으로 설계한다.
- CL-051 범위는 `Entry 연결 + 일반 돌진 패턴 1개`까지로 제한한다.

## 대상 스토리

| Jira ID | 범위 | 담당 |
|---|---|---|
| CL-051 | 보스 1종 패턴 1 구현 | 클라2 |

## 현재 기준 정리

현재 코드베이스에서 바로 활용 가능한 기반은 아래와 같다.

- `CL-049`, `CL-050` 범위에서 보스방 진입 조건과 진입 흐름이 이미 정리되어 있다.
- 보스방 진입 이벤트와 로컬 플레이어 이동 처리 코드는 `_Project/Scripts/Runtime/Stage` 아래에 존재한다.
- 돌진 텔레그래프 계열 코드는 이미 존재한다.
  - `LostMemory.Combat.Telegraph.AttackTelegraph2DView`
  - `LostMemory.Combat.Telegraph.AIBrainDashTelegraphDriver`
  - `LostMemory.Combat.Telegraph.AIDecisionDistanceToTargetAndDashReady`
- Bertha 보스 아트 리소스는 이미 존재한다.
  - `Assets/_Project/Art/Enemies/Boss/1_Bertha`
  - `Entry`, `Idle`, `Walk`, `Stun`, `Tired`, `Attacks/ComboAtk`, `Attacks/DashAtk`, `Death`
- 아직 Bertha 전용 프리팹, 애니메이터, 보스 전용 런타임 스크립트는 없다.

즉 CL-051은 "보스 전체 완성"이 아니라 아래 목표를 만드는 작업으로 보는 것이 맞다.

- 보스방 진입 후 Bertha가 `Entry`를 1회 재생한다.
- Entry 종료 후 실제 전투 루프에 진입한다.
- 그 전투 루프의 첫 구현 패턴은 `일반 돌진`이다.

## 확정 패턴 카탈로그

이번 보스의 공격 패턴은 아래 7개로 고정한다.

| 분류 | 패턴명 | 설명 | 구현 형태 |
|---|---|---|---|
| 근접 범위형 | 약공격 1 | 짧은 전방 범위 타격 | 단일 패턴 |
| 근접 범위형 | 약공격 2 | 약공격 1 후속 범위 타격 | 단일 패턴 |
| 근접 범위형 | 강공격 | 느리지만 넓은 범위 타격 | 단일 패턴 |
| 연계형 | 풀콤보 | 약공격1 -> 약공격2 -> 강공격 | 조합 패턴 |
| 돌진형 | 일반 돌진 | 직선 돌진 공격 | 단일 패턴 |
| 범위형 | 내려찍기 | 제자리 또는 목표 지점 충격 범위 공격 | 단일 패턴 |
| 복합형 | 돌진후 내려찍기 | 돌진 후 종점 또는 근접 지점에서 내려찍기 | 조합 패턴 |

중요한 해석:

- `약공격`, `강공격`이라는 이름은 근접 무기 평타 같은 의미가 아니라 `짧은 범위형 AoE 패턴`으로 해석한다.
- 이 보스는 플레이어와 몸이 닿는 것만으로 피해를 주는 타입이 아니라, `명시적인 액티브 구간`에서만 피해를 주는 타입으로 설계한다.
- `풀콤보`, `돌진후 내려찍기`는 새 패턴을 처음부터 별도 구현하는 것이 아니라, 이미 구현된 기본 패턴을 순서대로 이어붙이는 방식으로 만든다.

## 패턴 설계 원칙

### 1. 기본 패턴과 조합 패턴을 분리한다

기본 패턴은 아래 5개다.

- 약공격 1
- 약공격 2
- 강공격
- 일반 돌진
- 내려찍기

조합 패턴은 아래 2개다.

- 풀콤보 = 약공격1 + 약공격2 + 강공격
- 돌진후 내려찍기 = 일반 돌진 + 내려찍기

이 구조를 쓰면 `패턴 7개`를 모두 독립 구현하지 않아도 된다. 실제 실행 로직은 5개만 만들고, 나머지 2개는 시퀀스 조합으로 해결할 수 있다.

### 2. 모든 패턴은 같은 생명주기를 가진다

권장 공통 구조:

```text
Telegraph
  -> Active
  -> Recover
  -> Cooldown
```

패턴에 따라 중간 단계가 추가될 수 있다.

- 돌진형: `Telegraph -> Dash -> Recover -> Cooldown`
- 내려찍기형: `Telegraph -> Slam -> Recover -> Cooldown`
- 조합형: `(단일 패턴 1) -> (단일 패턴 2) -> ...`

즉 패턴마다 완전히 다른 상태기를 만들기보다, 공통 실행 프레임을 공유하고 액티브 구간만 바꾸는 방향이 유지보수에 유리하다.

### 3. 보스 공격은 접촉딜보다 텔레그래프 기반 액티브 히트로 처리한다

이번 보스는 아래 철학을 유지한다.

- `일반 돌진`은 방향 고정 후 직선 돌진으로 위협을 만든다.
- `내려찍기`는 바닥 충격 범위로 위협을 만든다.
- `약공격 1`, `약공격 2`, `강공격`도 근접 접촉 판정이 아니라 짧은 전방 혹은 부채꼴 범위로 처리한다.

즉 플레이어는 "보스 몸에 닿지 않기"보다 "보스가 만들어낸 위험 구역을 피하기"를 핵심 플레이로 가져가게 된다.

### 4. 같은 패턴 안에서는 같은 대상에게 1회만 피해를 준다

모든 패턴은 액티브 구간 중 다단 히트가 과하게 들어가지 않도록 `1회 타격 게이트`를 기본으로 둔다.

이 원칙은 아래 패턴 모두에 동일하다.

- 일반 돌진
- 내려찍기
- 약공격 1
- 약공격 2
- 강공격
- 풀콤보 각 타
- 돌진후 내려찍기의 각 단계

## 추천 구현 순서

실제 구현은 아래 순서가 가장 안전하다.

1. 일반 돌진
2. 내려찍기
3. 돌진후 내려찍기
4. 약공격 1
5. 약공격 2
6. 강공격
7. 풀콤보

이 순서를 추천하는 이유:

- `일반 돌진`은 기존 텔레그래프 코드 재사용 폭이 가장 크다.
- `내려찍기`를 만들면 이후 AoE 패턴 공통 실행부를 만들 수 있다.
- `돌진후 내려찍기`는 앞의 두 패턴을 연결하는 조합 패턴이다.
- `풀콤보`는 약1, 약2, 강공격이 먼저 있어야 조합이 가능하다.

즉 CL-051에서는 전체 패턴 카탈로그를 확정하되, 실제 구현은 `1. 일반 돌진`만 완료하는 것이 맞다.

## CL-051 범위 재정의

CL-051에서 완료해야 하는 범위는 아래와 같다.

- Bertha 보스 Entry 연출 연결
- 보스방 입장 직후 플레이어 이동/입력 잠금
- Entry 중 보스 인트로 대사 재생 훅 연결
- Entry 종료 후 AI 전투 시작
- 일반 돌진 패턴 1개 구현
- 보스방 진입 이벤트와 Entry 시작 연결
- 추후 패턴 확장을 고려한 기본 구조 확보

CL-051에서 하지 않는 것:

- 범용 대사 시스템 전체 구현
- 내려찍기 구현
- 돌진후 내려찍기 구현
- 약공격 1 구현
- 약공격 2 구현
- 강공격 구현
- 풀콤보 구현
- 체력 비율 기반 페이즈 전환
- Tired / Stun 상세 설계
- 보스 처치 후 보상, 결과 UI, 출구 연결

## Entry 포함 전투 흐름

이번 스토리에서는 Entry를 전투 바깥의 별도 시작 단계로 본다.

권장 흐름:

```text
BossRoomEntryStarted
  -> 플레이어 Freeze
  -> 플레이어 텔레포트
  -> Boss Intro Sequence Begin
  -> Bertha Entry 재생
  -> Entry 종료 대기
  -> 대사 재생
  -> Intro 종료
  -> 플레이어 Unfreeze
  -> Bertha Combat Begin
  -> Idle
  -> Chase
  -> Telegraph
  -> Dash
  -> Recover
  -> Chase
```

핵심 원칙:

- 플레이어는 인트로가 완전히 끝날 때까지 움직일 수 없어야 한다.
- Entry 중에는 추적하지 않는다.
- Entry 중에는 공격하지 않는다.
- Entry 중에는 피해 판정을 열지 않는다.
- 대사는 전투 로직 안에 박아넣지 않고 별도 인트로 시퀀스에서 재생한다.
- 인트로 종료 시점에만 AI와 공격 시스템을 활성화한다.

### 인트로는 별도 시퀀스 레이어로 분리한다

나중에 쉽게 수정하려면 `BossRoom -> Intro -> Combat`를 분리해야 한다.

권장 책임 분리:

- `BossRoomLocalTransitionDriver`: 플레이어를 입구 위치로 이동시키는 것까지만 담당
- `BossIntroSequenceController`: 플레이어 잠금, 보스 Entry, 대사, 종료 후 해제 담당
- `BerthaBossEncounterController`: 인트로 종료 이후 실제 전투 시작만 담당

이 구조를 쓰면 나중에 아래 변경이 쉬워진다.

- Entry 길이 조정
- 대사 순서 변경
- 대사 제거 후 바로 전투 시작
- 특정 보스만 다른 인트로 사용
- 스킵 기능 추가

### 인트로 데이터는 코드가 아니라 설정으로 뺀다

수정 용이성을 위해 아래 값은 코드 하드코딩을 피한다.

- Entry 사용 여부
- 대사 사용 여부
- 대사 라인 목록 또는 대사 키 목록
- 각 단계 사이 대기 시간
- 플레이어 잠금 해제 시점

권장 방식:

- `ScriptableObject` 또는 직렬화된 시퀀스 데이터 사용
- 대사 재생기는 인터페이스로 분리

예시 구조:

```text
BossIntroSequenceData
  - playEntryAnimation
  - lockPlayersDuringIntro
  - dialogueCueIds[]
  - delayBeforeDialogue
  - delayAfterDialogue
  - unlockPlayersOnComplete
```

현재 프로젝트에 범용 대사 시스템이 아직 명확하지 않다면, CL-051에서는 실제 대사 구현보다 `대사를 재생할 수 있는 훅`을 먼저 준비하는 편이 안전하다.

## 추천 런타임 구조

보스를 7개 패턴 개별 하드코딩으로 만들기보다, 아래 레이어로 분리하는 편이 낫다.

| 레이어 | 책임 |
|---|---|
| Intro | 플레이어 잠금, Entry, 대사, 전투 시작 타이밍 제어 |
| Encounter | 전투 시작, 사망/종료 처리 |
| Decision | 현재 거리, 쿨다운, 페이즈 기준 패턴 선택 |
| Execution | 선택된 패턴의 Telegraph / Active / Recover 실행 |
| Damage Gate | 같은 패턴 내 중복 타격 방지 |

### 권장 스크립트

```text
Assets/_Project/Scripts/Runtime/Enemies/Boss/Bertha/
  BerthaBossAuthoring.cs
  BerthaBossEncounterController.cs
  BerthaBossAnimatorBridge.cs
  BerthaBossPatternSelector.cs
  BerthaBossPatternRunner.cs
  BerthaDashHitGate.cs
Assets/_Project/Scripts/Runtime/Stage/
  BossRoomEncounterStarter.cs
  BossIntroSequenceController.cs
  BossIntroSequenceData.cs
  IBossIntroDialoguePlayer.cs
```

필요 시 이후 확장:

```text
Assets/_Project/Scripts/Runtime/Enemies/Boss/Bertha/
  BerthaSlamHitGate.cs
  BerthaBossPhaseController.cs
```

### 역할 요약

`BossIntroSequenceController`

- 보스방 진입 이벤트 이후 인트로 시작
- 플레이어 Freeze / Unfreeze
- 보스 Entry 재생 요청
- 대사 재생 요청
- 인트로 완료 시 `BerthaBossEncounterController`에 전투 시작 전달

`BossIntroSequenceData`

- 인트로 순서와 지연값 보관
- 대사 라인 또는 대사 키 보관
- 수정 가능한 연출 파라미터 외부화

`IBossIntroDialoguePlayer`

- 실제 대사 시스템과 인트로 시퀀스 사이 어댑터
- CL-051 시점에는 임시 구현 또는 no-op 구현도 가능

`BerthaBossEncounterController`

- 인트로 완료 후 전투 시작 호출 수신
- 전투 시작 시 AI 활성화
- 전투 중복 시작 방지

`BerthaBossAuthoring`

- 감지 거리
- 대시 거리
- 텔레그래프 시간
- 회복 시간
- 쿨다운
- 패턴별 데미지

`BerthaBossPatternSelector`

- 현재 거리와 조건에 따라 패턴 ID 선택
- CL-051에서는 사실상 `일반 돌진`만 반환
- 이후 `내려찍기`, `풀콤보`, `돌진후 내려찍기` 확장 시 재사용

`BerthaBossPatternRunner`

- 선택된 패턴 실행
- 공통 `Telegraph -> Active -> Recover` 흐름 담당
- 조합 패턴은 단일 패턴 시퀀스 실행 방식으로 처리

`BerthaDashHitGate`

- 돌진 액티브 구간에서만 타격 허용
- 같은 돌진 동안 같은 대상은 1회만 타격
- Recover / Death 시 캐시 초기화

## 조합 패턴 설계 방향

### 풀콤보

`풀콤보`는 아래처럼 새 로직을 만들지 말고, 단일 패턴 연쇄로 처리한다.

```text
Light1
  -> Light2
  -> Heavy
```

필요한 것은 "다음 패턴으로 이어붙이는 제어"이지, 별도의 여섯 번째 근접 패턴이 아니다.

### 돌진후 내려찍기

`돌진후 내려찍기`도 동일하다.

```text
Dash
  -> Slam
```

이 구조를 택하면 아래 이점이 있다.

- 대시 단독 쿨다운 조정이 쉽다.
- 내려찍기 단독 패턴도 그대로 재사용 가능하다.
- 조합 패턴 밸런싱 시 연결 시간만 조정하면 된다.

## 애니메이션/리소스 관점 정리

현재 확인된 리소스 기준으로 당장 연결 가능한 것은 아래다.

- `Entry`
- `Idle`
- `Walk`
- `DashAtk`
- `ComboAtk`
- `Hit`
- `Death`

리스크:

- `내려찍기` 전용 리소스가 명확히 분리되어 있지 않다.
- 따라서 `내려찍기`는 아래 둘 중 하나가 필요할 수 있다.
  - `ComboAtk` 일부 프레임을 활용한 신규 클립 분리
  - 아예 신규 애니메이션 리소스 확보

즉 CL-051을 `일반 돌진`으로 두는 판단은 리소스 관점에서도 안전하다.

## 추천 프리팹 / 애니메이터 구성

```text
Assets/_Project/
  Prefabs/
    Enemies/
      Boss/
        Bertha_CL051.prefab
  Art/
    Animations/
      Enemies/
        Boss/
          Bertha/
            Bertha.controller
            Bertha_Entry.anim
            Bertha_Idle.anim
            Bertha_Walk.anim
            Bertha_DashAtk.anim
            Bertha_Hit.anim
            Bertha_Death.anim
```

권장 루트 구조:

```text
Bertha_CL051
  Model
  ChargeDamageArea
  TelegraphOrigin
  HitVfxAnchor
```

## 권장 단계별 작업

### 1단계. 보스 인트로 시퀀스 계약 먼저 정의

먼저 아래 책임을 분리한다.

- 플레이어 잠금
- 보스 Entry
- 대사 재생
- 전투 시작 통지

최소 산출물:

- `BossIntroSequenceController`
- `BossIntroSequenceData`
- `IBossIntroDialoguePlayer`

완료 기준:

- 인트로 길이와 대사 사용 여부를 코드 수정 없이 바꿀 수 있다.
- 대사가 아직 없어도 no-op 구현으로 인트로 흐름이 돈다.

### 2단계. Entry 포함 최소 애니메이터 구성

이번 스토리에서 우선 필요한 상태:

- `Entry`
- `Idle`
- `Walk`
- `DashAtk`
- `Hit`
- `Death`

완료 기준:

- Entry가 1회 재생된 뒤 Idle로 진입한다.
- DashAtk 상태를 독립적으로 재생할 수 있다.

### 3단계. Bertha 프리팹 조립

필수 컴포넌트 후보:

- `Character`
- `TopDownController2D`
- `CharacterMovement`
- `CharacterOrientation2D`
- `CharacterDash2D`
- `CharacterDamageDash2D`
- `Health`
- `AIBrain`
- `Animator`
- `AttackTelegraph2DView`
- `AIBrainDashTelegraphDriver`
- `BerthaBossEncounterController`
- `BerthaBossAnimatorBridge`
- `BerthaDashHitGate`

### 4단계. 인트로와 전투 시작 분리

`BossIntroSequenceController` 책임:

- `BeginIntro()` 이전에는 인트로 미시작
- 플레이어 잠금 시작
- Entry 재생 시작
- 대사 재생 시작
- 대사 종료 후 플레이어 잠금 해제
- 전투 시작 통지

`BerthaBossEncounterController` 책임:

- `BeginEncounter()` 이전에는 전투 비활성
- 인트로 완료 후 AI와 공격 활성
- 중복 시작 방지

### 5단계. 보스방 진입 이벤트 연결

권장 연결용 얇은 스크립트:

`Assets/_Project/Scripts/Runtime/Stage/BossRoomEncounterStarter.cs`

책임:

- `BossRoomDoorController.BossRoomEntryStarted` 구독
- 플레이어 텔레포트 완료 후 `BossIntroSequenceController.BeginIntro()` 호출
- 인트로 완료 시 `BerthaBossEncounterController.BeginEncounter()` 호출

### 6단계. 일반 돌진 패턴 실행

CL-051 핵심 구현:

- `Telegraph`
- 방향 고정
- `Dash`
- `Recover`
- `Cooldown`

기존 `AIBrainDashTelegraphDriver`를 최대한 재사용한다.

### 7단계. 단일 패턴 1회 타격 게이트 보정

`BerthaDashHitGate` 최소 책임:

- Dash 중에만 히트 허용
- 같은 Dash 동안 동일 대상 1회만 타격
- Recover / Death 시 초기화

### 8단계. 테스트 씬 검증

`Test_Bertha_CL051.unity`에서 우선 아래를 확인한다.

- 보스방 진입 후 Entry 재생
- 인트로 동안 플레이어 이동 불가
- 인트로 동안 대사 재생 또는 대사 훅 호출
- Entry 중 추적/타격 없음
- 인트로 종료 후 플레이어 이동 가능
- Entry 종료 후 Chase 진입
- Telegraph 표시
- 방향 고정 후 Dash 실행
- Dash 중 1회 타격
- Recover 후 다시 전투 루프 복귀

## 시작값 권장안

| 항목 | 시작값 | 메모 |
|---|---:|---|
| EntryDuration | 0.9s | 입장 연출 최소 길이 |
| DelayBeforeDialogue | 0.2s | Entry 후 대사 시작 전 여유 |
| DelayAfterDialogue | 0.2s | 대사 후 전투 시작 전 여유 |
| DetectionRadius | 8.0 | 플레이어 감지 반경 |
| DashStartDistance | 5.0 | 돌진 개시 거리 |
| TelegraphDuration | 0.6s | 회피 가능성 확보 |
| DashDistance | 4.5 | 첫 체감 조정값 |
| DashDuration | 0.32s | 무게감 있는 속도 |
| RecoverDuration | 0.7s | 돌진 후 빈틈 |
| DashCooldown | 1.8s | 반복 압박 방지 |
| DashDamage | 18 | CL-051 기준 테스트값 |
| MaxHealth | 120 | 테스트 시작값 |

## 완료 기준

CL-051은 아래를 만족하면 완료로 본다.

1. Bertha 프리팹이 존재한다.
2. Bertha가 `Entry`, `Idle`, `Walk`, `DashAtk`, `Hit`, `Death`를 가진다.
3. 보스방 진입 이벤트로 Entry 시작을 걸 수 있다.
4. 보스방 입장 직후 플레이어는 인트로 종료 전까지 움직일 수 없다.
5. Entry는 1회만 재생된다.
6. 인트로 중 대사 재생 훅이 호출되거나 임시 대사 플레이어로 흐름이 유지된다.
7. Entry 중에는 추적, 공격, 피해 판정이 없다.
8. 인트로 종료 후에만 플레이어가 다시 움직일 수 있고 Chase 상태로 전환된다.
9. 일반 돌진 패턴이 Telegraph 후 실행된다.
10. 같은 돌진 동안 같은 대상에게 중복 히트가 발생하지 않는다.
11. Entry 길이, 대사 사용 여부, 대사 순서를 코드 수정 없이 조정할 수 있다.
12. 이후 `내려찍기`, `돌진후 내려찍기`, `약공격1`, `약공격2`, `강공격`, `풀콤보`를 붙일 수 있는 구조가 확보된다.

## 리스크와 대응

### 1. 내려찍기 전용 애니메이션이 부족할 수 있음

대응:

- CL-051은 돌진만 구현해서 리소스 리스크를 피한다.
- CL-052에서 `ComboAtk` 재활용 가능 여부를 먼저 검토한다.

### 2. Entry와 AI 시작 시점이 충돌할 수 있음

대응:

- Entry와 대사는 `BossIntroSequenceController`에서 제어한다.
- 인트로 종료 시점에만 전투를 활성화한다.

### 3. 돌진 히트가 다단으로 들어갈 수 있음

대응:

- 패턴 단위 히트 캐시를 둔다.
- Recover / Death 시 반드시 캐시를 초기화한다.

### 4. 대사 시스템이 나중에 바뀌면 인트로 코드가 같이 흔들릴 수 있음

대응:

- 인트로 쪽에서는 직접 UI를 띄우지 않는다.
- `IBossIntroDialoguePlayer` 인터페이스 뒤로 숨긴다.
- 실제 대사 시스템 교체 시 어댑터만 바꾼다.

### 5. 조합 패턴 구현 시 코드 중복이 생길 수 있음

대응:

- 조합 패턴은 단일 패턴 시퀀스 호출로만 설계한다.
- `DashSlam`과 `FullCombo`를 별도 하드코딩 패턴으로 만들지 않는다.

## 후속 스토리 연결

### CL-052

- `내려찍기` 추가
- 원형 또는 전방 AoE 텔레그래프 확장
- Dash / Slam 중 어떤 패턴을 쓸지 선택 로직 확장

### CL-053

- `돌진후 내려찍기` 추가
- 체력 비율 기반 페이즈 전환
- `Tired`, `Stun` 연결

### 이후 확장

- `약공격 1`
- `약공격 2`
- `강공격`
- `풀콤보`
- 거리, 체력 비율, 쿨다운 기반 패턴 선택 정교화

### CL-054

- 보스 처치 확정
- 보스방 클리어 처리
- 출구 / 결과 / 보상 흐름 연결

## 결론

이번 보스의 최종 패턴 카탈로그는 아래 7개다.

1. 약공격 1
2. 약공격 2
3. 강공격
4. 풀콤보
5. 일반 돌진
6. 내려찍기
7. 돌진후 내려찍기

하지만 구현은 `기본 패턴 5개 + 조합 패턴 2개`로 생각해야 한다.

- 기본 패턴: 약1, 약2, 강, 돌진, 내려찍기
- 조합 패턴: 풀콤보, 돌진후 내려찍기

이 기준으로 보면 CL-051의 가장 안전한 범위는 `플레이어 잠금 + 보스 Entry + 대사 훅 + 일반 돌진 구현`이다. 이후 `내려찍기`, `돌진후 내려찍기`, `근접 범위 3종`, `풀콤보`를 순차적으로 확장하면 된다.
