# CL-041 / CL-042 원거리형 일반 적 구현 계획

## 문서 목적

`CL-041 원거리형 일반 적 이동·거리 유지 상태 구현`과 `CL-042 원거리형 일반 적 투사체 공격·피격·사망 처리 구현`의 구현 방향을 정리한다.

`CL-037 / CL-038`의 근접형 Orc, `CL-039 / CL-040`의 돌진형 Orc Rider에 이어, 이번 단계에서는 `원거리형 일반 적`의 기본 루프를 안정적으로 만든다.

현재 아트 상태를 보면 1차 구현 대상은 `Assets/_Project/Art/Enemies/Skeleton Archer`가 가장 자연스럽다. 따라서 이번 문서는 `Skeleton Archer`를 기준 예시로 삼되, 다음 원거리 몬스터에도 재사용 가능한 최소 공통 규칙을 함께 고정하는 용도다.

## 대상 스토리

| Jira ID | 범위 | 담당 |
|---|---|---|
| CL-041 | 원거리형 일반 적 이동·거리 유지 상태 구현 | 클라2 |
| CL-042 | 원거리형 일반 적 투사체 공격·피격·사망 처리 구현 | 클라2 |

## 현재 기준

- `_Project` 아래에는 이미 일반 적 프리팹 기준점이 있다.
- `Assets/_Project/Prefabs/Enemies/Orc_CL037.prefab`는 기본 적 AI + 근접 무기 구조를 사용한다.
- `Assets/_Project/Prefabs/Enemies/OrcRider_CL039.prefab`는 같은 기반 위에 전조 + 돌진 공격만 추가한 구조다.
- 즉, 적 공통 골격은 이미 `Character`, `Health`, `AIBrain`, `CharacterHandleWeapon` 중심으로 맞춰지고 있다.
- 현재 추가된 원거리 적 아트는 `Assets/_Project/Art/Enemies/Skeleton Archer` 아래에 있다.
- 아직 `_Project` 아래 원거리 적 전용 무기 프리팹, 투사체 프리팹, 애니메이터 컨트롤러는 없다.

TopDown Engine에는 원거리 적 구현에 필요한 축이 이미 있다.

| 분류 | 확인된 TDE 구성 요소 | 이번 문서에서의 용도 |
|---|---|---|
| 이동/거리 유지 | `AIActionMoveTowardsTarget2D`, `AIActionMoveAwayFromTarget2D` | 접근, 이격 |
| 조준 | `AIActionAimWeaponAtTarget2D`, `CharacterOrientation2D` | 플레이어 방향 정렬, 무기 조준 |
| 발사 | `CharacterHandleWeapon`, `ProjectileWeapon`, `AIActionShoot2D` | 원거리 무기 사용, 투사체 발사 |
| 전이 | `AIDecisionDetectTargetRadius2D`, `AIDecisionDistanceToTarget`, `AIDecisionTargetIsAlive`, `AIDecisionTargetIsNull`, `AIDecisionTimeInState` | 감지, 사거리 판정, 쿨다운, 리셋 |
| 피해/사망 | `Projectile`, `DamageOnTouch`, `Health`, `MMLifeCycleEvent` | 투사체 피해, 적 피격, 사망 |

- 따라서 이번 구현은 `원거리 전용 거대 FSM`을 새로 만드는 방식보다 `TDE 기본 AI 상태 + 프로젝트 프리팹/무기 설정` 조합이 맞다.

## 구현 목표

- `Skeleton Archer`가 `대기 -> 감지 -> 접근 -> 거리 유지 -> 조준 -> 발사 -> 회복 -> 재전투` 흐름을 수행한다.
- 적이 플레이어에게 지나치게 붙지 않고, 원거리 적답게 일정 사거리 밴드를 유지한다.
- 투사체 발사는 `ProjectileWeapon` 기반으로 처리하고, 이후 다른 원거리 몬스터도 같은 구조를 재사용할 수 있게 한다.
- 피격과 사망은 기존 적과 동일하게 `Health`를 단일 기준으로 사용한다.
- 외부 에셋 원본은 수정하지 않고 `_Project` 아래 프리팹/애니메이션/스크립트 범위에서 해결한다.

## 비목표

- 유도 화살, 관통 화살, 분열탄 같은 특수 투사체 구현
- 원거리 적 전용 커스텀 전투 프레임워크 구축
- 보스급 패턴, 엄폐 행동, 군집 전술 구현
- 최종 SFX/VFX 품질 확정
- 네트워크 동기화 완성

## 결정 사항

- 1차 원거리 몬스터는 `Skeleton Archer`로 구현한다.
- 공격 방식은 `발사체 1발 사격`의 가장 단순한 형태로 시작한다.
- 이동은 `너무 멀면 접근`, `너무 가까우면 이격`, `적정 거리면 조준 및 공격` 구조로 한다.
- 공격은 `CharacterHandleWeapon + ProjectileWeapon + AIActionShoot2D` 조합을 그대로 사용한다.
- 적 공통 구조는 기존 근접/돌진형 적과 맞추고, 원거리 전용 부분은 `무기`, `ProjectileSpawn`, `상태 전이 거리`만 분리한다.
- 지금 단계에서 `RangedEnemyBase` 같은 큰 공통 스크립트는 만들지 않는다.
- 공통화는 먼저 `프리팹 계층`, `오브젝트 이름`, `애니메이터 파라미터`, `상태 이름`을 통일하는 방식으로 처리한다.

## 권장 상태 모델

| 상태 | 설명 | 추천 TDE 축 | 진입 조건 | 이탈 조건 |
|---|---|---|---|---|
| `Idle` | 스폰 직후 대기 | `AIActionDoNothing` | 초기 상태 | 플레이어 감지 시 `Approach` |
| `Approach` | 타깃과 거리가 너무 멀 때 접근 | `AIActionMoveTowardsTarget2D` | 감지 후 사거리 밖 | 적정 사거리 진입 시 `AimReady`, 타깃 상실 시 `Reset` |
| `Retreat` | 타깃이 너무 가까우면 이격 | `AIActionMoveAwayFromTarget2D` | 최소 안전 거리 미만 | 적정 사거리 복귀 시 `AimReady` |
| `AimReady` | 이동을 멈추고 조준 | `AIActionAimWeaponAtTarget2D` | 적정 사거리 진입 | 발사 가능 시 `Attack`, 거리 이탈 시 `Approach` 또는 `Retreat` |
| `Attack` | 투사체 발사 | `AIActionShoot2D` | 조준 완료, 쿨다운 준비 | 발사 후 `Recover` |
| `Recover` | 사격 후 짧은 후딜/재평가 | `AIActionDoNothing` 또는 계속 조준 | 발사 직후 | 거리 재평가 후 `AimReady`, `Approach`, `Retreat` |
| `Reset` | 타깃 해제 및 복귀 | 타깃 리셋용 기본 상태 | 타깃 사망, 타깃 상실, 전투 종료 | 즉시 `Idle` |

핵심은 `근접형처럼 무조건 접근하지 않고`, `돌진형처럼 전조를 길게 두지도 않으며`, `사거리 밴드 유지`를 기본 규칙으로 삼는 것이다.

## 시작 수치

초기 수치는 플레이 테스트를 통해 조정한다. 원거리 적은 너무 붙으면 근접형과 차이가 없어지고, 너무 멀면 방 구조에서 비합리적으로 느껴질 수 있다.

| 항목 | 시작값 | 설명 |
|---|---:|---|
| `DetectionRadius` | 7.5 | 플레이어 감지 반경 |
| `PreferredMinDistance` | 3.0 | 이보다 가까우면 뒤로 빠짐 |
| `PreferredMaxDistance` | 5.5 | 이보다 멀면 접근 |
| `AttackDistance` | 5.0 | 기본 공격 허용 거리 |
| `ChaseBreakDistance` | 9.0 | 추격/타깃 유지 포기 거리 |
| `AttackWindup` | 0.18s | 애니메이션과 발사 타이밍 맞춤용 선딜 |
| `AttackCooldown` | 0.9s | 연사 방지 |
| `ProjectileSpeed` | 8.0 | 화살 이동 속도 시작값 |
| `ProjectileDamage` | 10 | 투사체 피해 |
| `ProjectileLifetime` | 2.0s | 벽이나 먼 거리에서 정리용 |
| `MaxHealth` | 25 | 근접형보다 조금 낮게 시작 가능 |

## TopDown Engine 사용 범위

사용 유지:

- `Character`
- `TopDownController2D`
- `CharacterMovement`
- `CharacterOrientation2D`
- `CharacterHandleWeapon`
- `AIBrain`
- `Health`
- `ProjectileWeapon`
- `Projectile`
- `AIActionMoveTowardsTarget2D`
- `AIActionMoveAwayFromTarget2D`
- `AIActionAimWeaponAtTarget2D`
- `AIActionShoot2D`

프로젝트 전용으로 처리:

- `Skeleton Archer` 프리팹 구성
- 원거리 적 애니메이터 컨트롤러
- 발사 위치(`ProjectileSpawn`) 정렬
- 사거리 수치/상태 전이 조정
- 필요 시 피격/사망 리포터 연결
- 필요 시 공격 전조나 디버그 가시화

사용하지 않을 것:

- TDE 원본 스크립트 직접 수정
- 원거리 적 전용 커스텀 투사체 시스템 새로 구현
- 근접 무기로 활 공격을 억지로 흉내내는 방식
- 지금 단계에서 과한 추상화 스크립트 추가

## 추천 프리팹 구성

```text
SkeletonArcher_CL041
  SkeletonArcherModel
  WeaponAttachment
    SkeletonArcherBow
  ProjectileSpawn
  HitVfxAnchor
```

권장 컴포넌트:

- 루트
  - `Character`
  - `TopDownController2D`
  - `CharacterMovement`
  - `CharacterOrientation2D`
  - `CharacterHandleWeapon`
  - `Health`
  - `AIBrain`
- `WeaponAttachment`
  - 원거리 무기 프리팹
- 원거리 무기 프리팹
  - `ProjectileWeapon`
  - `WeaponAim`
  - 필요 시 `ObjectPooler`
- 투사체 프리팹
  - `Projectile`
  - `DamageOnTouch`
  - `Collider2D`

권장 생성 위치:

```text
Assets/_Project/Prefabs/Enemies/SkeletonArcher_CL041.prefab
Assets/_Project/Prefabs/Weapons/SkeletonArcherBow.prefab
Assets/_Project/Prefabs/Weapons/Projectiles/SkeletonArcherArrow.prefab
Assets/_Project/Art/Animations/Enemies/Skeleton_Archer/SkeletonArcher.controller
Assets/_Project/Art/Animations/Enemies/Skeleton_Archer/SkeletonArcher_Idle.anim
Assets/_Project/Art/Animations/Enemies/Skeleton_Archer/SkeletonArcher_Walk.anim
Assets/_Project/Art/Animations/Enemies/Skeleton_Archer/SkeletonArcher_Attack.anim
Assets/_Project/Art/Animations/Enemies/Skeleton_Archer/SkeletonArcher_Hurt.anim
Assets/_Project/Art/Animations/Enemies/Skeleton_Archer/SkeletonArcher_Death.anim
```

## 애니메이터 규칙

다음 원거리 몬스터까지 고려하면, 애니메이터 파라미터는 기존 적과 최대한 맞추는 편이 낫다.

권장 파라미터:

- `Walking` : 이동 여부
- `Attack` : 공격 시작
- `Death` : 사망 처리

선택 파라미터:

- `Hurt` : 피격 반응을 별도 트리거로 넣고 싶을 때 사용

중요한 점:

- 공격 애니메이션에서 실제 발사 프레임을 먼저 정하고, 그 프레임에 맞춰 `ProjectileWeapon.InitialDelay` 또는 무기 타이밍 값을 맞춘다.
- 먼저 애니메이션 프레임을 고정하고 무기 수치를 맞추는 편이 덜 꼬인다.

## CL-041 구현 계획

### 1. 이동·거리 유지 전략

원거리 적의 핵심은 플레이어를 쫓는 것이 아니라 `유효 사거리 밴드`를 유지하는 것이다.

권장 규칙:

- 플레이어가 `PreferredMaxDistance` 바깥이면 `Approach`
- 플레이어가 `PreferredMinDistance` 안쪽이면 `Retreat`
- 그 사이면 `AimReady`

이 방식은 단순하지만 다음 원거리 적에도 거의 그대로 재사용된다.

### 2. 상태 전이 방향

| 상황 | 다음 상태 |
|---|---|
| 감지 직후 | `Approach` |
| 너무 멀다 | `Approach` |
| 너무 가깝다 | `Retreat` |
| 적정 거리다 | `AimReady` |
| 타깃 상실 | `Reset` |
| 타깃 사망 | `Reset` |

### 3. 구현 포인트

- `AIActionMoveTowardsTarget2D`와 `AIActionMoveAwayFromTarget2D`만으로도 1차 거리 유지가 가능하다.
- 1차 구현에서는 `원형 이동`, `엄폐`, `좌우 스트레이프`는 넣지 않는다.
- 룸 구조상 너무 복잡한 회피 움직임은 오히려 어색할 수 있다.
- `AimReady` 상태에서는 이동을 멈추고 `AIActionAimWeaponAtTarget2D`만 실행하도록 두는 편이 가장 읽기 쉽다.

### 4. 완료 기준

- 플레이어 감지 후 무작정 붙지 않는다.
- 너무 가까우면 뒤로 빠진다.
- 적정 거리에서 멈추고 공격 준비를 한다.
- 벽이나 장애물 때문에 잠깐 꼬이더라도 전체 루프가 깨지지 않는다.

## CL-042 구현 계획

### 1. 투사체 공격 전략

공격은 `AIActionShoot2D + CharacterHandleWeapon + ProjectileWeapon` 조합을 기본으로 둔다.

이 조합을 쓰면:

- AI는 발사 입력만 보낸다.
- 무기는 발사 타이밍과 투사체 생성을 담당한다.
- 투사체는 `Projectile`과 `DamageOnTouch`로 실제 피해를 처리한다.

즉, 원거리 적도 기존 적과 마찬가지로 `AIBrain이 공격 시점을 고르고`, 실제 판정은 `무기/투사체`가 담당하는 구조를 유지할 수 있다.

### 2. 투사체 프리팹 기준

필수 항목:

- `Projectile`
- `DamageOnTouch`
- 적절한 `Collider2D`
- 필요 시 파괴/비활성화용 수명 설정

권장 규칙:

- 플레이어 레이어만 타격하도록 `TargetLayerMask`를 명확히 설정한다.
- 발사체는 일정 시간이 지나면 정리되게 한다.
- 발사체 속도와 피해는 먼저 단순값으로 고정하고, 이후 데이터화 여부를 결정한다.

### 3. 발사 위치와 애니메이션 정렬

원거리 적은 이 부분이 가장 중요하다.

우선순위:

1. 공격 애니메이션에서 화살이 나가야 하는 프레임을 정한다.
2. `ProjectileSpawn` 위치를 손/활 끝 기준으로 잡는다.
3. `ProjectileWeapon` 발사 타이밍을 그 프레임에 맞춘다.
4. 실제 게임뷰에서 화살 시작 위치가 어색하지 않은지 확인한다.

이 순서를 거꾸로 하면 조정 비용이 커진다.

### 4. 피격 처리

피격은 기존 적과 동일하게 `Health` 기준으로 처리한다.

권장 범위:

- 체력 감소
- 짧은 피격 애니메이션 또는 피드백
- 필요 시 공격 취소

MVP에서는 긴 경직이나 특수 상태 이상까지 확장하지 않는다.

### 5. 사망 처리

사망 처리도 기존 적과 같은 기준을 유지한다.

필수 요구:

- 사망 이벤트는 한 번만 처리된다.
- 사망 후 이동, 조준, 발사, 충돌이 비활성화된다.
- 룸 클리어 집계에서 정상 제거된다.
- 모델 비활성화/파괴 순서 때문에 집계가 누락되지 않게 한다.

### 6. 완료 기준

- 화살이 올바른 위치에서 발사된다.
- 발사 후 재사격까지 쿨다운이 적용된다.
- 플레이어에게 정상 피해가 들어간다.
- 피격, 사망, 룸 클리어 집계가 기존 적과 동일하게 동작한다.

## 공통화 전략

지금은 `원거리 적 공통 베이스 클래스`를 크게 만드는 시점이 아니다. 대신 아래 규칙을 고정하는 편이 더 효율적이다.

### 1. 프리팹 계층 이름 통일

다음 이름은 이후 원거리 몬스터에도 반복 사용한다.

- `WeaponAttachment`
- `ProjectileSpawn`
- `HitVfxAnchor`

### 2. 애니메이터 파라미터 통일

- `Walking`
- `Attack`
- `Death`

### 3. 상태 이름 통일

- `Idle`
- `Approach`
- `Retreat`
- `AimReady`
- `Attack`
- `Recover`
- `Reset`

### 4. 공통 스크립트 추가 기준

아래가 실제로 두 번 이상 반복될 때만 스크립트를 뽑는다.

- 발사 전조 표시
- 발사 포인트 자동 정렬
- 피격/사망 이벤트 리포팅
- 디버그 기즈모 표시

즉, 이번 티켓에서는 `규칙 통일`까지만 하고, `추상화는 반복이 보일 때` 한다.

## 에셋 슬라이스 이후 작업 순서

지금 `Skeleton Archer` 에셋 슬라이스 중이라면, 다음 순서로 가는 게 가장 낭비가 적다.

### 1. 애니메이션 클립 확정

- `Idle`
- `Walk`
- `Attack`
- `Hurt`
- `Death`

먼저 `Attack`에서 실제 발사 프레임을 정한다.

### 2. 애니메이터 컨트롤러 생성

- 기존 `Orc.controller`를 복제해서 시작한다.
- 파라미터는 `Walking`, `Attack`, `Death`를 유지한다.
- 필요하면 `Hurt`만 추가한다.

### 3. 적 프리팹 복제

- `Orc_CL037.prefab`를 복제해 `SkeletonArcher_CL041.prefab`를 만든다.
- 모델, 애니메이터, 수치, 상태 전이만 바꾼다.

### 4. 원거리 무기 프리팹 생성

- `SkeletonArcherBow.prefab`를 만든다.
- `ProjectileWeapon` 기반으로 설정한다.

### 5. 투사체 프리팹 생성

- `SkeletonArcherArrow.prefab`를 만든다.
- 아직 프로젝트 전용 화살 비주얼이 없으면 TDE 데모 투사체를 `_Project`로 복제해 임시로 사용한다.

### 6. 발사 위치 정렬

- `WeaponAttachment`
- `ProjectileSpawn`

이 두 위치를 게임뷰에서 먼저 맞춘다.

### 7. 테스트 씬 검증

우선 아래 4가지만 본다.

- 거리 유지가 되는가
- 화살이 올바른 위치에서 나가는가
- 피해가 정상 적용되는가
- 사망 후 정리가 되는가

### 8. 웨이브/스폰 연결

전투 루프가 안정화된 뒤에만 실제 스폰 데이터에 연결한다.

## 리스크와 대응

| 리스크 | 설명 | 대응 |
|---|---|---|
| 발사 프레임 불일치 | 애니메이션과 화살 생성 시점이 어긋남 | 공격 클립 프레임 먼저 확정 |
| 너무 잦은 후진 | 좁은 방에서 계속 뒤로만 빠질 수 있음 | `PreferredMinDistance`를 과도하게 크게 잡지 않음 |
| 발사 위치 어색함 | 손이 아닌 몸통에서 화살이 나감 | `ProjectileSpawn` 별도 자식 고정 |
| 과한 공통화 | 아직 한 종뿐인데 구조를 크게 뽑아버림 | 이번에는 규칙 통일까지만 진행 |
| 투사체 비주얼 부재 | 화살 전용 에셋이 아직 없음 | 임시 투사체로 먼저 전투 루프 검증 |

## 최종 완료 기준

- `Skeleton Archer`가 원거리 적답게 거리 유지와 투사체 공격을 수행한다.
- 피격/사망은 기존 적과 동일한 기준으로 안정적으로 동작한다.
- 프리팹 계층, 상태 이름, 애니메이터 파라미터가 다음 원거리 몬스터에도 그대로 재사용 가능하다.
- 불필요한 공통 베이스나 커스텀 전투 프레임워크 없이 `_Project` 범위에서 유지 가능한 구조를 남긴다.
