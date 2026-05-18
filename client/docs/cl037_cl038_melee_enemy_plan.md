# CL-037 / CL-038 구현 계획

## 문서 목적

`CL-037 근접형 일반 적 이동·추격 상태 구현`과 `CL-038 근접형 일반 적 공격·피격·사망 처리 구현`의 구현 방향을 정리한다.

이 문서는 첫 번째 일반 적 타입을 빠르게 플레이 가능 상태로 만들기 위해 `상태 전이`, `전투 처리 축`, `TopDown Engine 확장 범위`, `후속 적 타입과의 공통 기반`을 먼저 고정하는 용도다.

## 대상 스토리

| Jira ID | 범위 | 담당 |
|---|---|---|
| CL-037 | 근접형 일반 적 이동·추격 상태 구현 | 클라2 |
| CL-038 | 근접형 일반 적 공격·피격·사망 처리 구현 | 클라2 |

## 현재 상태

- `CL-032`, `CL-033` 기준으로 방/웨이브/스폰 포인트 계획은 정리됐지만, 적 전용 커스텀 런타임 구조는 아직 없다.

- 현재 Unity 프로젝트에는 커스텀 `Assets/Game/Gameplay/Enemies` 폴더와 적 프리팹 기준점이 아직 없다.

- `TopDown Engine`에는 적 구현에 바로 활용 가능한 축이 이미 있다.

| 분류 | 확인된 TDE 구성 요소 | 이번 문서에서의 용도 |
|---|---|---|
| AI 브레인 | `AIBrain`, `AIActionMoveTowardsTarget2D`, `AIActionDoNothing`, `AIActionFaceTowardsTarget2D` | 대기/추격/정면 고정 |
| 감지/판정 | `AIDecisionDetectTargetRadius2D`, `AIDecisionDistanceToTarget`, `AIDecisionTargetIsAlive`, `AIDecisionTargetIsNull`, `AIDecisionHit` | 감지, 공격 거리 진입, 타깃 상실, 피격 반응 |
| 공격 | `AIActionShoot2D`, `CharacterHandleWeapon`, `MeleeWeapon`, `DamageOnTouch` | 공격 입력, 근접 히트박스 활성화 |
| 생명주기 | `Health`, `Health.OnHit`, `Health.OnDeath`, `MMLifeCycleEvent` | 피격, 사망, 룸 클리어 연결 |

- 따라서 이번 구현은 별도 AI 시스템을 새로 만드는 방식보다 `TDE 브레인 + 프로젝트 규칙용 래퍼/설정 컴포넌트` 조합이 맞다.

## 구현 목표

- MVP 기준으로 근접형 일반 적 1종이 `대기 -> 감지 -> 추격 -> 공격 -> 피격 -> 사망` 흐름을 안정적으로 수행하게 한다.

- 엔진 원본 수정 없이 `Assets/Game` 하위 확장만으로 구현 가능하도록 설계한다.

- `CL-039 ~ CL-042`의 돌진형/원거리형 적이 재사용할 수 있는 공통 적 구조를 남긴다.

- `CL-035 적 전멸 기준 방 클리어 판정 구현`이 적 사망 이벤트를 안정적으로 집계할 수 있게 연결 지점을 마련한다.

## 비목표

- 돌진형 적의 전조/충돌 로직 구현

- 원거리 적의 투사체/거리 유지 로직 구현

- 네트워크 동기화 완성

- 보상 드롭, 전리품 연출, 고급 군중 제어 상태 이상 구현

- 최종 아트/애니메이션 품질 확정

## 설계 원칙

- 적의 상태 머신은 커스텀 거대 FSM 대신 `AIBrain` 상태 조합으로 관리한다.

- 적의 공격은 단순 접촉 지속 데미지보다 `명시적 공격 윈도우`를 가진 근접 무기 방식으로 처리한다.

- 적의 수치와 반경은 프리팹 하드코딩보다 데이터 정의에서 조정 가능하게 둔다.

- 피격과 사망은 `Health`를 단일 진실 소스로 삼고, 룸 진행 로직은 이 이벤트를 구독하는 형태로 연결한다.

- MVP에서는 `예측 가능성`, `디버깅 용이성`, `후속 적 타입 확장성`을 우선한다.

## 추천 폴더 구조

```text
client/LostMemory/Assets/Game/
  Gameplay/
    Enemies/
      Common/
        Data/
          EnemyDefinition.cs
        Runtime/
          EnemyCombatReporter.cs
          EnemyRuntimeContext.cs
      Melee/
        Data/
          MeleeEnemyDefinition.cs
        Runtime/
          MeleeEnemyInstaller.cs
          MeleeEnemyAttackGate.cs
          MeleeEnemyHitReaction.cs
        Prefabs/
          MeleeEnemy_Base.prefab
  Data/
    Enemies/
      Melee/
```

`EnemyDefinition.cs`는 후속 적 타입 공통 필드를 담고, `MeleeEnemyDefinition.cs`는 근접형 전용 반경/타이밍/데미지 수치를 가진 확장 데이터로 두는 구성이 무난하다.

## 추천 프리팹 구성

```text
MeleeEnemyRoot
  Character
  Model
  WeaponAttachment
    EnemyMeleeWeapon
  DetectionOrigin
  HitVfxAnchor
```

권장 컴포넌트 기준:

- 루트: `Character`, `Health`, `AIBrain`

- 이동/방향: TDE 기본 이동 능력 + `AIActionMoveTowardsTarget2D`, `AIActionFaceTowardsTarget2D`

- 공격: `CharacterHandleWeapon` + `MeleeWeapon`

- 프로젝트 확장: `MeleeEnemyInstaller`, `EnemyCombatReporter`, `MeleeEnemyHitReaction`

## CL-037 구현 계획

### 1. 최소 상태 모델

MVP의 근접형 적은 아래 상태만 있어도 충분하다.

| 상태 | 설명 | 추천 TDE 축 | 진입 조건 | 이탈 조건 |
|---|---|---|---|---|
| `Idle` | 스폰 직후 대기 | `AIActionDoNothing` | 초기 상태 | 플레이어 감지 시 `Chase` |
| `Chase` | 타깃을 향해 이동 | `AIActionMoveTowardsTarget2D` | 감지 반경 진입, 타깃 생존 | 공격 시작 거리 진입 시 `AttackReady`, 타깃 상실 시 `Reset` |
| `AttackReady` | 공격 시작 거리 유지, 방향 정렬 | `AIActionDoNothing` + `AIActionFaceTowardsTarget2D` | 추격 중 공격 거리 진입 | 공격 실행 시 `Attack`, 타깃 이탈 시 `Chase` |
| `Reset` | 타깃 해제 및 기본 상태 복귀 | `AIActionResetTarget` 또는 커스텀 초기화 | 타깃 사망, 범위 이탈, 룸 종료 | 즉시 `Idle` |

이번 단계에서 순찰 패턴은 필수가 아니다. 방 전투 구조상 적은 스폰 직후 대기하다가 플레이어 감지 후 추격만 안정적으로 되면 MVP 요구를 만족한다.

### 2. 감지와 추격 규칙

- 타깃 획득은 `AIDecisionDetectTargetRadius2D`를 기본으로 사용한다.

- 추격 종료 조건은 최소 두 가지가 필요하다.

| 종료 조건 | 추천 구현 |
|---|---|
| 타깃 사망 | `AIDecisionTargetIsAlive` |
| 타깃 참조 상실 | `AIDecisionTargetIsNull` |

- 공격 진입 거리는 `AIDecisionDistanceToTarget` 기준으로 판정한다.

- `AttackReady` 상태에서는 계속 전진하지 말고, 적이 플레이어를 향해 정면을 맞춘 뒤 공격 상태로 넘어가게 한다.

- 룸 기반 게임이므로 추격 범위는 무한으로 두지 않고 `DetectionRadius`, `AttackStartDistance`, `ChaseBreakDistance` 세 값으로 관리하는 편이 안전하다.

### 3. 권장 데이터 필드

`MeleeEnemyDefinition.cs`에는 최소 아래 필드를 둔다.

| 필드 | 설명 |
|---|---|
| `EnemyId` | 웨이브 정의에서 참조할 적 타입 ID |
| `MoveSpeed` | 추격 이동 속도 |
| `DetectionRadius` | 플레이어 감지 반경 |
| `AttackStartDistance` | 공격 상태 진입 거리 |
| `ChaseBreakDistance` | 추격 포기 거리 |
| `FacingLockDuration` | 공격 전 정면 정렬 시간 |
| `MaxHealth` | 체력 |
| `ContactPriority` | 좁은 공간에서 우선 처리용 정렬 값 |

핵심은 `CL-037`에서 최소한 `반경`, `속도`, `상태 전이 거리`가 데이터화되어야 한다는 점이다.

### 4. 추천 구현 범위

- `MeleeEnemyInstaller.cs`
  적 프리팹에 연결된 데이터 정의를 읽어 `Health`, 무브 속도, 브레인 파라미터를 세팅한다.

- `AIBrain` 상태 프리셋
  `Idle`, `Chase`, `AttackReady`, `Reset` 상태를 프리팹 레벨에서 조립한다.

- 디버그 가시화
  `DetectionRadius`, `AttackStartDistance`, `ChaseBreakDistance`를 Gizmo로 볼 수 있으면 조정 속도가 빨라진다.

## CL-038 구현 계획

### 1. 공격 처리 전략

근접형 적의 공격은 `AIActionShoot2D` + `CharacterHandleWeapon` + `MeleeWeapon` 조합을 기본으로 두는 것이 가장 자연스럽다.

이 조합을 쓰면 적 AI는 "지금 공격하라"는 입력만 보내고, 실제 히트박스 생성/활성 시간은 `MeleeWeapon`이 담당하게 된다.

`MeleeWeapon` 기준으로 중요한 축:

| 항목 | TDE 필드 | 용도 |
|---|---|---|
| 공격 선딜 | `InitialDelay` | 플레이어가 피할 수 있는 틈 확보 |
| 공격 판정 시간 | `ActiveDuration` | 실제 데미지 히트박스 활성 시간 |
| 공격 범위 | `AreaOffset`, `AreaSize` | 근접 범위 조정 |
| 피해량 | `MinDamageCaused`, `MaxDamageCaused` | 기본 데미지 |
| 후경직 | `Knockback`, `KnockbackForce` | 피격 체감 |
| 타깃 제한 | `TargetLayerMask` | 아군/적군 오타격 방지 |

즉, CL-038의 핵심은 적이 겹쳐 있기만 해도 계속 데미지를 주는 구조가 아니라, 공격 상태에서만 히트박스가 열리도록 만드는 것이다.

### 2. 공격 상태 전이

| 상태 | 설명 | 추천 구현 |
|---|---|---|
| `Attack` | 무기 사용 입력 실행 | `AIActionShoot2D` |
| `Recover` | 다음 공격 전 짧은 회복 시간 | `AIDecisionTimeInState` 또는 무기 재사용 시간 기반 |
| `ReturnToChase` | 공격 후 거리 재평가 | 공격 거리 밖이면 `Chase`, 안이면 재공격 대기 |

권장 규칙:

- 공격은 한 번 실행 후 즉시 다시 발동하지 않도록 쿨다운을 둔다.

- 적이 공격 중일 때 타깃이 범위 밖으로 벗어나더라도 현재 공격 윈도우는 끝까지 진행하게 한다.

- 공격 방향은 `AttackReady` 단계에서 정렬해두고, 실제 공격 중에는 과도한 방향 보정을 최소화한다.

### 3. 피격 처리

피격은 `Health`를 기준으로 처리하고, 프로젝트 측에서 필요한 연출과 상태 반응만 추가한다.

권장 처리 축:

- `Health.OnHit`
  피격 피드백, 피격 이펙트, 짧은 경직 연출 연결

- `AIDecisionHit`
  최근 피격 여부를 감지해 짧은 반응 상태를 두고 싶을 때 사용

- `CharacterHandleWeapon.GettingHitInterruptsAttack`
  공격 중 피격 시 공격 취소가 필요한지 적 타입별로 결정

MVP 기준 권장 방향:

- 일반 근접 적은 `짧은 피격 연출 + 체력 감소 + 선택적 공격 취소`까지만 구현한다.

- 긴 스턴, 넉백 면역, 슈퍼아머 같은 세부 전투 규칙은 뒤로 미룬다.

### 4. 사망 처리

사망은 `Health.OnDeath` 또는 `MMLifeCycleEvent.Death` 기준으로 룸 진행 로직과 연결한다.

필수 요구:

- 사망 이벤트가 한 번만 발행된다.

- 사망 직후 충돌과 추가 공격 판정이 비활성화된다.

- 룸 클리어 판정에서 해당 적을 살아있는 적 목록에서 제거할 수 있다.

- 필요 시 `DestroyOnDeath` 또는 지연 파괴를 사용하되, 집계 전에 오브젝트가 사라져 참조가 끊기지 않게 순서를 맞춘다.

프로젝트 확장용 컴포넌트 예시:

- `EnemyCombatReporter.cs`
  `Health.OnHit`, `Health.OnDeath`를 구독해 룸/웨이브 시스템에 전달

- `MeleeEnemyHitReaction.cs`
  피격 시 애니메이션 파라미터, 깜빡임, 경직 시간을 제어

## 후속 스토리 연결

| 후속 스토리 | 연결 방식 |
|---|---|
| CL-035 적 전멸 기준 방 클리어 판정 구현 | `EnemyCombatReporter`가 사망 이벤트를 룸 시스템에 전달 |
| CL-039 ~ CL-040 돌진형 적 | `EnemyDefinition` 공통 필드와 브레인 설치 방식 재사용 |
| CL-041 ~ CL-042 원거리 적 | `CharacterHandleWeapon`, `AIActionShoot2D` 재사용, 무기만 투사체형으로 변경 |
| CL-043 이후 보상 흐름 | 적 사망/웨이브 종료 이벤트가 보상 진입 조건이 됨 |

## 구현 순서 제안

1. `Assets/Game/Gameplay/Enemies/Common`, `Melee` 폴더 구조를 만든다.

2. `EnemyDefinition`, `MeleeEnemyDefinition` 데이터 구조를 만든다.

3. `MeleeEnemy_Base.prefab` 초안을 만들고 `Character`, `Health`, `AIBrain`, `CharacterHandleWeapon`, `MeleeWeapon` 조합을 붙인다.

4. `Idle -> Chase -> AttackReady` 상태 전이를 먼저 붙여 `CL-037`을 플레이 가능하게 만든다.

5. 공격 윈도우와 데미지 판정, 피격 반응, 사망 보고를 붙여 `CL-038`을 마무리한다.

6. 마지막으로 룸 전투 샌드박스에서 `스폰 -> 추격 -> 공격 -> 처치 -> 적 전멸 집계` 흐름을 검증한다.

## 산출물 기준

- `MeleeEnemyDefinition.cs`

- `MeleeEnemyInstaller.cs`

- `EnemyCombatReporter.cs`

- `MeleeEnemyHitReaction.cs`

- 근접형 일반 적 베이스 프리팹 1종

- 테스트용 웨이브 또는 샌드박스 씬 1종

## 완료 기준

- 플레이어가 감지 반경 안에 들어오면 근접형 적이 안정적으로 타깃을 잡고 추격한다.

- 공격 거리 안에 들어오면 적이 멈추고 방향을 맞춘 뒤 명시적 공격 윈도우로 데미지를 준다.

- 공격은 쿨다운과 판정 시간이 분리되어 과도한 연속 타격이 발생하지 않는다.

- 적이 피격되면 체력 감소와 최소 피드백이 보인다.

- 적이 사망하면 룸 클리어 집계가 가능한 형태로 이벤트가 전달된다.

- 같은 기반 구조를 사용해 돌진형/원거리형 적으로 확장 가능한 수준의 공통 축이 남는다.

## 열린 이슈

- 근접형 적의 기본 대기 상태를 `정지 대기`로 할지 `짧은 순찰`로 할지는 실제 룸 크기와 시야 판독성을 보고 결정하는 편이 좋다.

- 피격 시 공격 취소를 기본값으로 둘지, 적 종류별 옵션으로 둘지는 플레이 감각 테스트 후 확정하는 편이 맞다.

- 사망 후 즉시 파괴와 지연 파괴 중 무엇이 룸 집계와 연출에 더 안정적인지는 `CL-035` 연결 시점에 같이 검증해야 한다.
