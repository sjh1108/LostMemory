# CL-039 / CL-040 돌진형 적 구현 계획

## 문서 목적

`CL-039 ~ CL-040 돌진형 일반 적 구현`의 방향을 정리한다.

`CL-037 / CL-038`에서 구성한 Orc 근접형 적은 `감지 -> 추격 -> 근접 공격 -> 피격 -> 사망`의 기본 루프를 확인하는 데 목적이 있었다. 돌진형 적은 같은 기반을 재사용하되, 공격 방식이 일반 근접 타격이 아니라 `전조 -> 방향 고정 -> 돌진 -> 충돌 피해 -> 회복`으로 달라진다.

현재 1차 구현 대상은 `_Project/Art/Enemies/Orc rider`에 추가된 `Orc Rider` 에셋이다. CL-039 / CL-040에서는 우선 `Attack03`을 돌진 공격으로 사용하고, 이후 `Attack01`, `Attack02`, `Block` 등 다른 공격을 스테이지별 패턴으로 확장할 수 있게 구조를 열어둔다.

따라서 이번 문서는 돌진형 적의 상태 전이, TopDown Engine 사용 범위, 프리팹 구성, 수치 시작값, 패턴 확장 방향, 완료 기준을 먼저 고정하는 용도다.

## 대상 스토리

| Jira ID | 범위 | 담당 |
|---|---|---|
| CL-039 | 돌진형 일반 적 이동·추격·돌진 준비 상태 구현 | 클라2 |
| CL-040 | 돌진형 일반 적 돌진 공격·피격·사망 처리 구현 | 클라2 |

## 현재 기준

- `CL-037 / CL-038`에서 Orc 적의 기본 AI, 공격, 피격, 사망 흐름을 `_Project` 아래 프리팹 기준으로 구성했다.
- TopDown Engine에는 돌진형 적에 활용 가능한 컴포넌트가 이미 있다.
- 돌진형 1차 몬스터용으로 `Assets/_Project/Art/Enemies/Orc rider` 에셋이 추가됐다.
- Orc Rider 에셋에는 `Idle`, `Walk`, `Attack01`, `Attack02`, `Attack03`, `Block`, `Hurt`, `Death` 스프라이트가 있고, `Attack03`은 1차 돌진 공격 애니메이션으로 사용한다.
- `Orc rider(Split Effects)` 폴더에는 공격 이펙트 분리본이 있으므로, 이후 본체 애니메이션과 슬래시/돌진 이펙트를 분리해서 붙일 수 있다.

| 분류 | 확인된 TDE 구성 요소 | 이번 문서에서의 용도 |
|---|---|---|
| AI 브레인 | `AIBrain`, `AIActionMoveTowardsTarget2D`, `AIActionDoNothing`, `AIActionDash` | 대기, 추격, 전조, 돌진 실행 |
| 감지/전이 | `AIDecisionDetectTargetRadius2D`, `AIDecisionDistanceToTarget`, `AIDecisionTimeInState`, `AIDecisionTargetIsAlive` | 감지, 돌진 거리 진입, 전조/회복 시간, 타깃 생존 확인 |
| 돌진 | `CharacterDash2D`, `CharacterDamageDash2D` | 방향 고정 돌진, 돌진 중 충돌 피해 |
| 피해 | `DamageOnTouch`, `Health` | 플레이어 피해, 적 피격/사망 |
| 애니메이션 | `CharacterDash2D`의 `Dashing`, `DashingDirectionX`, `DashingDirectionY` | 돌진 애니메이션 파라미터 연결 |

## 구현 목표

- `Orc Rider`가 `대기 -> 감지 -> 추격 -> 전조 -> 돌진 -> 회복 -> 재추격` 흐름을 수행한다.
- 돌진 시작 순간의 방향을 고정해 플레이어가 보고 피할 수 있게 한다.
- 돌진 중에만 피해 판정이 켜지고, 같은 돌진에서 같은 대상에게 중복 피해가 들어가지 않게 한다.
- 피격과 사망은 `Health` 기반으로 처리하고, 사망 후 이동/돌진/피해 판정이 비활성화되게 한다.
- 외부 에셋 원본은 수정하지 않고 `_Project` 아래 복제 프리팹과 프로젝트 스크립트만 사용한다.
- 1차 구현은 `Attack03` 기반 단일 돌진 공격만 포함한다.
- 후속 작업에서 스테이지별로 `Attack01`, `Attack02`, `Block`, 다른 돌진 공격을 조합할 수 있도록 패턴 데이터 구조를 고려한다.

## 비목표

- 보스급 돌진 패턴 구현
- 벽 충돌 후 스턴, 튕김, 파괴 가능한 오브젝트 연동
- 네트워크 동기화 완성
- 최종 이펙트/사운드 품질 확정
- 경로 예측 라인, 위험 범위 UI의 최종 디자인 확정

## 결정 사항

- 1차 돌진형 몬스터는 `Orc Rider`로 구현한다.
- 1차 공격 패턴은 `Attack03` 기반 돌진 공격 하나만 사용한다.
- 돌진 방향은 돌진 시작 순간의 플레이어 위치를 기준으로 고정한다.
- 전조 시간 동안 적은 정지하고, 플레이어 방향을 바라보거나 전조 이펙트를 보여준다.
- 돌진 중에는 경로를 다시 보정하지 않는다.
- 돌진 피해는 접촉 지속 피해가 아니라 `돌진 active 동안 1회 충돌 피해`로 본다.
- 돌진 실패 또는 종료 후에는 짧은 회복 상태를 거친 뒤 다시 추격한다.
- MVP에서는 `CharacterDamageDash2D`를 우선 사용한다.
- `CharacterDamageDash2D`만으로 중복 타격 제어나 전조/회복 제어가 부족하면 `_Project` 아래 얇은 래퍼 컴포넌트를 추가한다.
- 스테이지마다 공격 패턴을 다르게 만드는 요구는 `Enemy prefab`을 직접 분기하지 않고, 별도 패턴 프로필 또는 정의 데이터로 분리한다.
- 다른 돌진형 몬스터가 추가되어도 `돌진 실행/피해/회복` 코드는 재사용하고, 애니메이션/수치/패턴 목록만 교체하는 방향으로 설계한다.

## 권장 상태 모델

| 상태 | 설명 | 추천 TDE 축 | 진입 조건 | 이탈 조건 |
|---|---|---|---|---|
| `Idle` | 스폰 직후 대기 | `AIActionDoNothing` | 초기 상태 | 플레이어 감지 시 `Chase` |
| `Chase` | 타깃을 향해 이동 | `AIActionMoveTowardsTarget2D` | 타깃 감지 | 돌진 시작 거리 진입 시 `Telegraph`, 타깃 상실 시 `Reset` |
| `Telegraph` | 돌진 전조, 방향 고정 | `AIActionDoNothing` + 방향 고정 처리 | 돌진 거리 진입 또는 패턴 선택 | 전조 시간 종료 시 `Charge` |
| `Charge` | 고정 방향으로 돌진, 1차에서는 `Attack03` 재생 | `AIActionDash` + `CharacterDamageDash2D` | 전조 종료 | 돌진 시간 종료 시 `Recover` |
| `Recover` | 돌진 후 후딜 | `AIActionDoNothing` | 돌진 종료 | 회복 시간 종료 후 거리 재평가 |
| `Hit` | 피격 반응 | `Health.OnHit`, 선택적 `AIDecisionHit` | 피격 | 짧은 반응 후 이전 루프 복귀 |
| `Dead` | 사망 | `Health.OnDeath` | 체력 0 | 이동/돌진/피해 판정 중지 |

후속 패턴이 늘어나면 `Telegraph`, `AttackExecute`, `Recover`를 공통 상태로 두고, 현재 선택된 패턴 데이터가 실제 애니메이션과 판정 방식을 결정하게 한다.

## 시작 수치

초기 수치는 플레이 테스트로 조정한다. 돌진형 적은 플레이어가 보고 피할 수 있어야 하므로, 전조와 회복 시간을 너무 짧게 잡지 않는다.

| 항목 | 시작값 | 설명 |
|---|---:|---|
| `DetectionRadius` | 6.5 | 플레이어 감지 반경 |
| `ChargeStartDistance` | 4.5 | 돌진 준비 상태로 들어가는 거리 |
| `ChaseBreakDistance` | 9.0 | 추격 포기 또는 리셋 거리 |
| `TelegraphDuration` | 0.45s | 돌진 전조 시간 |
| `ChargeDistance` | 4.0 | 실제 돌진 거리 |
| `ChargeDuration` | 0.28s | 실제 돌진 시간 |
| `RecoverDuration` | 0.45s | 돌진 후 회복 시간 |
| `ChargeCooldown` | 1.2s | 다음 돌진까지 최소 간격 |
| `Damage` | 15 | 돌진 충돌 피해 시작값 |
| `InvincibilityDuration` | 0.5s | 플레이어 피격 후 무적 시간 |
| `DamageHitboxSize` | 0.9 x 0.9 | 돌진 중 충돌 판정 크기 |

## TopDown Engine 사용 범위

사용 유지:

- `Character`
- `TopDownController2D`
- `CharacterMovement`
- `CharacterOrientation2D`
- `AIBrain`
- `Health`
- `CharacterDash2D`
- `CharacterDamageDash2D`
- `AIActionDash`
- `DamageOnTouch`

프로젝트 전용으로 처리:

- 돌진형 적 프리팹 설정
- 돌진 전조 상태 구성
- 돌진 방향 고정 정책
- 돌진 피해 중복 방지 필요 시 래퍼
- 피격/사망 시 돌진 중단 처리
- 디버그용 돌진 경로 표시

사용하지 않을 것:

- TopDown Engine 원본 스크립트 직접 수정
- 일반 근접 무기 `MeleeWeapon`으로 돌진 피해를 억지로 처리하는 방식
- 돌진 중 매 프레임 플레이어 방향으로 계속 꺾이는 유도 돌진

## 추천 프리팹 구성

```text
OrcRider_CL039
  OrcRiderModel
  ChargeDamageArea
    BoxCollider2D
    DamageOnTouch
  TelegraphVfxAnchor
  HitVfxAnchor
```

권장 컴포넌트:

- 루트
  - `Character`
  - `TopDownController2D`
  - `CharacterMovement`
  - `CharacterOrientation2D`
  - `CharacterDamageDash2D`
  - `Health`
  - `AIBrain`
- `ChargeDamageArea`
  - `BoxCollider2D`
  - `DamageOnTouch`
- 필요 시 프로젝트 컴포넌트
  - `ChargeEnemyTelegraphPresenter`
  - `ChargeEnemyDashReporter`
  - `ChargeEnemyHitReaction`

권장 생성 위치:

```text
Assets/_Project/Prefabs/Enemies/OrcRider_CL039.prefab
Assets/_Project/Art/Animations/Enemies/OrcRider/OrcRider.controller
Assets/_Project/Art/Animations/Enemies/OrcRider/OrcRider_Attack03_Charge.anim
```

원본 스프라이트는 현재 추가된 아래 경로를 사용한다.

```text
Assets/_Project/Art/Enemies/Orc rider/Orc rider/Orc rider-Attack03.png
Assets/_Project/Art/Enemies/Orc rider/Orc rider(Split Effects)/Orc rider-Attack03_Effect.png
```

## 코드 구조 제안

### ChargeEnemyDefinition

돌진형 적의 공통 수치를 담는다. 처음에는 프리팹 필드로 시작해도 되지만, 후속 적 타입 확장을 고려하면 ScriptableObject로 분리 가능한 구조가 좋다.

필드 후보:

- `EnemyId`
- `DisplayName`
- `MoveSpeed`
- `DetectionRadius`
- `ChargeStartDistance`
- `ChaseBreakDistance`
- `DefaultPatternProfile`
- `StagePatternOverrides`
- `MaxHealth`

### ChargeEnemyAttackPattern

실제 공격 하나를 표현한다. 1차 구현에서는 `Attack03Charge` 하나만 만든다.

필드 후보:

- `PatternId`
- `AttackKind`
- `AnimatorTrigger`
- `TelegraphDuration`
- `ChargeDistance`
- `ChargeDuration`
- `RecoverDuration`
- `ChargeCooldown`
- `Damage`
- `DamageHitboxSize`
- `CanBeInterrupted`
- `DebugColor`

`AttackKind` 후보:

- `Charge`
- `MeleeSwing`
- `Block`
- `Projectile`
- `Special`

### ChargeEnemyPatternProfile

스테이지별 공격 패턴 묶음을 표현한다.

예시:

| 프로필 | 포함 패턴 | 용도 |
|---|---|---|
| `OrcRider_Stage1` | `Attack03Charge` | CL-039 / CL-040 1차 구현 |
| `OrcRider_Stage2` | `Attack03Charge`, `Attack01Slash` | 후속 스테이지 변주 |
| `OrcRider_Stage3` | `Attack03Charge`, `Attack02WideSwing`, `Block` | 고난도 변주 |

처음에는 프로필 파일을 만들지 않고 프리팹에 직접 패턴 하나를 두어도 된다. 다만 코드 구조는 나중에 `PatternProfile`을 붙여도 변경 폭이 작게 설계한다.

### ChargeEnemyInstaller

프리팹에 연결된 정의를 읽어 TDE 컴포넌트 값을 세팅한다.

담당:

- `Health` 최대 체력 세팅
- `CharacterMovement` 이동 속도 세팅
- `CharacterDamageDash2D` 돌진 거리/시간 세팅
- `DamageOnTouch` 피해량/타깃 레이어 세팅
- AIBrain 상태 전이 거리/시간 세팅
- 현재 스테이지에 맞는 `ChargeEnemyPatternProfile` 적용

### ChargeEnemyTelegraphPresenter

돌진 전조 시각화를 담당한다.

담당:

- 전조 시작 이벤트 수신
- 적 정지 상태에서 방향 표시
- 바닥 경고선 또는 방향 화살표 표시
- 전조 종료 시 표시 제거

현재 MVP에서는 없어도 돌진은 동작해야 한다.

### ChargeEnemyDashReporter

돌진 시작/종료/명중 이벤트를 프로젝트 시스템에 전달한다.

담당:

- `ChargeStarted`
- `ChargeHit`
- `ChargeEnded`
- `ChargeInterrupted`

후속 룸/피드백/사운드 시스템과 직접 강결합하지 않도록 이벤트만 발행한다.

### ChargeEnemyPatternRunner

패턴 선택과 실행을 담당한다.

1차 구현에서는 항상 `Attack03Charge`만 반환해도 된다. 이후 스테이지별 패턴이 필요해지면 이 컴포넌트가 현재 스테이지, 쿨다운, 거리 조건, 확률을 보고 다음 패턴을 선택한다.

담당:

- 현재 사용 가능한 패턴 목록 관리
- 패턴별 쿨다운 관리
- 현재 스테이지 프로필 적용
- 선택된 패턴의 전조/실행/회복 수치 제공
- 선택된 패턴의 Animator trigger 제공

### ChargeEnemyHitReaction

피격/사망 시 돌진 상태를 정리한다.

담당:

- 피격 시 선택적으로 돌진 취소
- 사망 시 dash, damage area, collider 비활성화
- 사망 애니메이션 트리거 호출

## 애니메이션 대응

초기에는 `Orc rider-Attack03.png`를 돌진 공격 애니메이션으로 사용한다. 해당 스프라이트는 `1100 x 100` 크기라서 100x100 기준 11프레임으로 슬라이스하는 방향이 맞다.

권장 Animator 파라미터:

- `Walking` bool
- `Dashing` bool
- `DashingDirectionX` float
- `DashingDirectionY` float
- `Telegraph` trigger
- `Attack03` trigger
- `Damage` trigger
- `Death` trigger

필요 애니메이션:

- `Idle`
- `Walk`
- `Telegraph`
- `Attack03_Charge`
- `Hit`
- `Death`

전조와 돌진은 몸체 애니메이션보다 방향 경고 이펙트가 더 중요하다. MVP에서는 `Telegraph` 전용 클립이 없어도 동작하게 두고, 돌진 실행 때 `Attack03`을 재생한다.

후속 확장 시 매핑:

| 스프라이트 | 1차 사용 여부 | 예상 용도 |
|---|---|---|
| `Attack03` | 사용 | 돌진 공격 |
| `Attack01` | 보류 | 근접 베기 또는 스테이지 2 패턴 |
| `Attack02` | 보류 | 넓은 베기 또는 스테이지 3 패턴 |
| `Block` | 보류 | 방어/전조/특수 패턴 |
| `Hurt` | 사용 가능 | 피격 |
| `Death` | 사용 가능 | 사망 |

## 이펙트 대응

필요 이벤트:

- 전조 시작
- 돌진 시작
- 돌진 명중
- 돌진 종료
- 돌진 중단
- 피격
- 사망

초기 디버그 시각화:

- 돌진 전 방향선 표시
- 돌진 중 DamageArea gizmo 또는 반투명 박스 표시
- 명중 시 간단한 색상 플래시

최종 연출 후보:

- 전조 바닥 경고선
- 돌진 먼지 이펙트
- `Orc rider-Attack03_Effect.png` 기반 돌진/베기 이펙트
- 충돌 시 히트스톱/카메라 흔들림
- 벽 충돌 시 충격 이펙트

`Orc rider(Split Effects)`의 이펙트 분리본은 본체 애니메이션 없이도 별도 SpriteRenderer나 VFX Presenter에서 재생할 수 있게 둔다. 판정 코드는 이펙트 재생 여부와 분리한다.

## 멀티플레이 고려

돌진형 적은 위치 변화와 충돌 판정이 크기 때문에, 이후 멀티플레이에서는 호스트 권한 기준으로 처리해야 한다.

솔로 흐름:

```text
AI 상태 전이
-> 전조 시작
-> 돌진 방향 고정
-> 로컬 돌진 실행
-> 로컬 충돌 판정
-> 로컬 데미지 확정
-> 이펙트 재생
```

멀티 흐름:

```text
호스트 AI 상태 전이
-> 호스트가 전조 시작/방향/sequenceId 확정
-> 호스트가 돌진 실행/충돌 판정/데미지 확정
-> 클라이언트는 전조/돌진/명중 이펙트 재생
```

공격 실행 데이터는 명시적으로 남긴다.

- `attacker`
- `target`
- `sequenceId`
- `chargeDirection`
- `startPosition`
- `startTime`
- `chargeDistance`
- `chargeDuration`
- `patternId`
- `stagePatternProfileId`

피해야 할 구조:

- 각 클라이언트가 독립적으로 돌진 충돌 판정을 확정하는 구조
- 돌진 중 매 프레임 랜덤 또는 클라이언트별 보정으로 방향이 달라지는 구조
- 돌진 코드가 룸, 보상, 유물 시스템을 직접 수정하는 구조

## CL-039 완료 기준

- `Orc Rider` 에셋 기반 프리팹이 생성된다.
- 돌진형 적이 플레이어를 감지한다.
- 감지 후 플레이어를 추격한다.
- 돌진 시작 거리 안에 들어오면 추격을 멈추고 전조 상태로 진입한다.
- 전조 중 돌진 방향이 고정된다.
- 전조 시간이 끝나면 돌진 상태로 전환된다.
- 돌진 상태에서 `Attack03` 애니메이션이 재생된다.
- 돌진 종료 후 회복 상태를 거쳐 다시 추격 또는 재공격한다.

## CL-040 완료 기준

- 돌진 중에만 피해 판정이 활성화된다.
- 플레이어가 돌진 DamageArea에 닿으면 TDE `Health`에 데미지가 적용된다.
- 같은 돌진에서 같은 대상에게 중복 피해가 들어가지 않는다.
- 피격 시 돌진형 적의 피격 반응이 재생된다.
- 사망 시 돌진, 이동, 피해 판정이 중지된다.
- 사망 이벤트가 한 번만 발생한다.
- 전조/돌진/회복 타이밍을 Inspector에서 조정할 수 있다.
- 현재 구현은 `Attack03Charge` 단일 패턴으로 동작한다.
- 후속 패턴 추가를 막지 않도록 패턴 ID 또는 패턴 데이터 연결 지점이 남아 있다.

## 구현 순서

1. `Orc Rider` 스프라이트를 100x100 기준으로 슬라이스한다.
2. `Idle`, `Walk`, `Attack03_Charge`, `Hurt`, `Death` 애니메이션 클립과 컨트롤러를 만든다.
3. Orc 또는 기존 근접형 적 프리팹을 복제해 `OrcRider_CL039` 작업용 프리팹을 만든다.
4. `CharacterDamageDash2D`와 `ChargeDamageArea`를 구성한다.
5. `AIBrain`에 `Idle`, `Chase`, `Telegraph`, `Charge`, `Recover`, `Dead` 상태를 구성한다.
6. `AIActionDash`가 전조 종료 후 한 번만 실행되도록 상태 전이를 조정한다.
7. `DamageOnTouch`의 타깃 레이어를 `Player`로 설정하고 데미지 값을 적용한다.
8. `Attack03` 또는 `Dashing` Animator 파라미터를 돌진 실행과 연결한다.
9. 돌진 중 중복 타격이 발생하면 프로젝트 래퍼로 한 번만 타격되게 막는다.
10. 피격/사망 애니메이터 파라미터를 연결한다.
11. 플레이어 상하좌우 위치에서 돌진 방향과 피해 판정을 테스트한다.
12. 수치와 전조 시간을 조정한다.
13. 후속 패턴 추가를 위해 `Attack03Charge` 패턴 ID 또는 데이터 연결 지점을 남긴다.

## 예상 리스크

- `Orc rider` 폴더명과 파일명에 공백이 있어서 스크립트 참조 문자열이나 자동화 작업에서 실수할 수 있다.
- Unity 에셋 경로 자체는 유지하되, 프리팹/애니메이션/코드 식별자는 `OrcRider`처럼 공백 없는 이름을 사용한다.
- `AIActionDash`가 상태에 머무는 동안 매 프레임 `DashStart()`를 호출할 수 있다.
- 이 경우 `Charge` 상태는 짧은 단발 상태로 구성하거나, 프로젝트 전용 `AIActionChargeOnce2D` 래퍼가 필요하다.
- `CharacterDamageDash2D`의 `DamageOnTouch`가 같은 돌진에서 같은 대상을 여러 번 때릴 수 있다.
- 이 경우 `ChargeEnemyDashReporter` 또는 전용 damage gate로 sequence 단위 중복 타격을 막는다.
- `Attack03` 애니메이션 길이와 실제 dash duration이 어긋나면 모션은 끝났는데 판정이 남거나, 판정은 끝났는데 모션이 남을 수 있다.
- 처음에는 dash duration을 애니메이션 길이에 맞추고, 이후 active 타이밍을 별도 조정한다.
- 돌진 중 벽/장애물 충돌 처리가 기대와 다를 수 있다.
- MVP에서는 벽 충돌 특수 처리 없이 dash 종료 또는 controller 충돌 결과를 우선 따른다.
- 사망 시 damage area가 켜진 채 남으면 플레이어에게 사망 후 피해를 줄 수 있다.
- 사망 이벤트에서 dash와 damage area를 반드시 비활성화해야 한다.

## 후속 작업

- 돌진 전조 경고선과 사운드를 추가한다.
- `Attack01`, `Attack02`, `Block`을 별도 공격 패턴으로 추가할지 결정한다.
- 스테이지별 `ChargeEnemyPatternProfile`을 만들어 같은 Orc Rider라도 스테이지마다 공격 조합을 다르게 한다.
- 다른 돌진형 몬스터 추가 시 공통 `ChargeEnemy` 런타임을 재사용하고, 에셋/수치/패턴 프로필만 교체한다.
- 벽 충돌 시 짧은 스턴 또는 회복 상태를 추가할지 결정한다.
- 돌진 성공/실패에 따라 회복 시간을 다르게 줄지 검토한다.
- 룸 클리어 적 사망 집계와 연결한다.
- CL-041 / CL-042 원거리형 적 구현 시 공통 적 정의와 피격/사망 처리 구조를 재사용한다.
