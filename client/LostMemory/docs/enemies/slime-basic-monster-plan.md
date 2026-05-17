# 슬라임 기본 몬스터 구현 계획서

## 목적

`Assets/_Project/Art/Enemies/Slime/Sprites` 리소스를 활용해 초반 구간용 기본 슬라임 몬스터를 구현한다. 슬라임은 빠르게 압박하는 적이 아니라, 짧게 이동하고 멈추는 리듬을 통해 플레이어가 행동을 읽을 수 있는 근접 몬스터로 설계한다.

## 참고 구조

슬라임 스프라이트 구성은 `Assets/_Project/Art/Enemies/chobomb` 구조를 참고한다.

Chobomb 참고 방식:

- 상태별 스프라이트 시트 분리
- `idle`, `walk`, `hurt`, `boom`, `fx` 같은 동작 단위 관리
- 애니메이션 클립은 상태별로 구성

슬라임 적용 방향:

- `Slime_Idle`
- `Slime_HopMove`
- `Slime_PreAttack`
- `Slime_Attack`
- `Slime_Hurt`
- `Slime_Death`

현재 슬라임 원본은 `momo_spriteSheet1.png`, `momo_spriteSheet2.png`, `momo_spriteSheet3.png`에 여러 동작이 한 장에 들어있으므로, 구현 시 필요한 프레임 범위를 상태별 애니메이션 클립으로 매핑한다.

## 기본 컨셉

- 초반용 근접 몬스터
- 접촉 피해 없음
- 몸통 콜라이더와 공격 히트박스를 분리
- 플레이어가 공격 사거리에 들어와도 즉시 공격하지 않고, 잠깐 멈춘 뒤 공격
- 공격 쿨타임은 오크 공격 쿨타임의 3배
- 이동은 계속 추적하지 않고 `2번 이동 후 멈춤` 패턴을 사용

## 행동 흐름

```text
Idle
 -> Detect Player
 -> HopMove 1
 -> HopMove 2
 -> Pause
 -> Player In Attack Range?
      yes -> PreAttackPause -> Attack -> Recover -> LongCooldown
      no  -> HopMove 반복
```

## 상태별 동작

### Idle

- 플레이어가 탐지 거리 밖에 있을 때 대기한다.
- 기본 idle 애니메이션을 재생한다.

### HopMove

- 플레이어 방향으로 짧게 이동한다.
- 한 사이클에서 최대 2번 이동한다.
- 2번 이동 후에는 반드시 `Pause` 상태로 전환한다.
- 이동 애니메이션은 일반 걷기보다 통통 튀는 느낌을 우선한다.

### Pause

- 이동 2회 이후 잠깐 멈춘다.
- 플레이어가 공격 사거리 안에 있으면 `PreAttackPause`로 전환한다.
- 공격 사거리 밖이면 다시 `HopMove` 사이클을 시작한다.

### PreAttackPause

- 공격 전 예고 상태다.
- 플레이어가 사거리 안에 들어왔다고 바로 공격하지 않고, 이 상태에서 짧게 멈춘다.
- 가능하면 몸을 움츠리거나 커지는 프레임을 사용해 공격 타이밍을 읽을 수 있게 한다.

### Attack

- 공격 애니메이션 중 특정 구간에서만 공격 히트박스를 활성화한다.
- 몸통 충돌로는 피해를 주지 않는다.
- 공격 히트박스가 켜진 동안에만 플레이어에게 피해를 준다.

### Recover / LongCooldown

- 공격 후 짧은 후딜을 둔다.
- 이후 오크 공격 쿨타임의 3배에 해당하는 긴 쿨타임을 적용한다.
- 쿨타임 중에는 바로 연속 공격하지 않고 이동/정지 패턴으로 돌아간다.

### Hurt

- 플레이어 공격에 피격되면 hurt 애니메이션을 재생한다.
- 단, 피격 후 AI 상태가 완전히 꼬이지 않도록 현재 이동/공격 루틴은 명확히 복구되어야 한다.

### Death

- 체력이 0이 되면 death 애니메이션 또는 사라지는 연출을 재생한다.
- 기존 적 스폰 시스템에서 사망 처리와 카운트가 정상 동작해야 한다.

## 충돌 및 피해 규칙

- 몸통 콜라이더:
  - 플레이어 공격 피격용
  - 이동/물리 충돌용
  - 플레이어에게 접촉 피해를 주지 않음

- 공격 히트박스:
  - 기본 비활성화
  - `Attack` 애니메이션의 유효 타이밍에만 활성화
  - 피해 적용 후 중복 타격 방지를 위한 짧은 내부 쿨타임 필요

- `EnemyContactDamage`:
  - 슬라임에는 기본적으로 사용하지 않음
  - 접촉 피해가 없는 몬스터 컨셉을 유지

## 밸런스 기준

초기값은 테스트용으로 아래 기준을 사용한다.

- 체력: 오크보다 낮거나 비슷한 초반 몬스터 수준
- 이동 속도: 오크보다 느리게
- 이동 패턴: 짧은 이동 2회 후 정지
- 공격 사거리: 근접
- 공격 전 대기: 짧게 존재
- 공격 쿨타임: 오크 공격 쿨타임 x 3
- 접촉 피해: 없음

정확한 공격 쿨타임 값은 구현 시 `Assets/_Project/Prefabs/Enemies/Orc_CL037.prefab`의 현재 공격 쿨타임을 확인한 뒤 3배로 계산한다.

## 구현 대상

예상 추가/수정 파일:

- `Assets/_Project/Prefabs/Enemies/Slime_Test.prefab`
- `Assets/_Project/ScriptableObjects/Enemies/EnemyData_Slime.asset`
- `Assets/_Project/ScriptableObjects/Enemies/EnemyCatalog_Default.asset`
- `Assets/_Project/Art/Animations/Enemies/Slime/Slime.controller`
- `Assets/_Project/Art/Animations/Enemies/Slime/*.anim`
- 필요 시 `Assets/_Project/Scripts/Runtime/Enemies/SlimeEnemyController.cs`

## 구현 원칙

- TopDownEngine 원본은 수정하지 않는다.
- 기존 적 스폰 구조와 `EnemyCatalog` 흐름을 따른다.
- 슬라임 전용 행동이 기존 AI 컴포넌트로 과하게 복잡해질 경우, `_Project` 아래에 슬라임 전용 컨트롤러를 둔다.
- 스프라이트 원본 PNG는 직접 수정하지 않는다.
- Unity 메타/프리팹/애니메이션 파일 수정 시 변경 범위와 위험을 별도로 기록한다.

## 확인 항목

- 슬라임이 플레이어를 탐지하는가
- 2번 이동 후 멈추는 패턴이 유지되는가
- 플레이어가 공격 사거리 안에 들어와도 즉시 공격하지 않는가
- 공격 전 멈춤이 시각적으로 인지되는가
- 몸에 닿아도 플레이어가 피해를 받지 않는가
- 공격 히트박스가 켜진 타이밍에만 피해가 들어가는가
- 공격 쿨타임이 오크의 3배로 적용되는가
- 피격/사망 후 스폰 시스템 카운트가 정상 처리되는가
