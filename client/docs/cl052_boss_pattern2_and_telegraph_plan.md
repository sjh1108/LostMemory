# CL-052 보스 1종 패턴 2 및 전조 구현 계획

## 문서 목적

`CL-052` 범위를 현재 Bertha 보스 구현 상태에 맞게 다시 자른다.

이번 계획서는 아래 4가지를 먼저 고정한다.

- 강공격은 기존 근접 판정 위에 `ComboAtk/FX` 기반 8방향 추가 투사체를 얹는다.
- 풀콤보는 단일 박스 공격이 아니라 `2회 fan burst + 마지막 omni burst` 구조로 확장한다.
- 투사체 로직은 기존 `BerthaAreaAttackController`를 비대하게 만들지 않고 별도 사이드카 컴포넌트로 붙인다.
- `Entry` 역재생 느낌의 상승 전조 + 맵 좌측에서 우측까지 가로지르는 돌진은 `Stretch` 범위로 둔다.

## 대상 스토리

| Jira ID | 범위 | 담당 |
|---|---|---|
| CL-052 | Bertha 보스 패턴 2 + 전조 구현 | 클라2 |

## 이번 요청에서 고정할 요구사항

- 강공격 패턴에서 `Assets/_Project/Art/Enemies/Boss/1_Bertha/Attacks/ComboAtk/FX`를 활용한 추가 투사체를 발사한다.
- 강공격 추가 투사체는 `8방향 radial burst`로 고정한다.
- 풀콤보에서는 중간에 여러 갈래 투사체를 2번 발사한 뒤 마지막에 모든 방향으로 한 번 더 발사한다.
- MVP는 아니지만, `Entry`를 역재생한 것처럼 보이는 상승 연출 뒤에 보스가 맵 왼쪽 끝에서 플레이어 위치 기준 라인을 잡고 맵 오른쪽 끝까지 돌진하는 패턴도 준비한다.

## 현재 기준 정리

현재 코드베이스에서 바로 재사용 가능한 기반은 아래와 같다.

- Bertha 전투 상태기는 이미 단일 bootstrap으로 묶여 있다.
  - `Assets/_Project/Scripts/Runtime/Enemies/Boss/Bertha/BerthaLightAttack1Bootstrap.cs`
- 패턴 선택기는 이미 존재한다.
  - `BerthaCombatPatternSelector`
  - 현재 `LightAttack1`, `LightAttack2`, `HeavyAttack`, `Dash`, `FullCombo`를 고른다.
- 강공격과 풀콤보는 현재 `BerthaAreaAttackController` 기반의 단일 근접 박스 공격만 수행한다.
  - `BerthaHeavyAttackController`
  - `BerthaFullComboController`
- 돌진 텔레그래프와 인트로 흐름은 이미 분리되어 있다.
  - `AttackTelegraph2DView`
  - `AIBrainDashTelegraphDriver`
  - `BossIntroSequenceController`
- `ComboAtk/FX`에는 `ShockWave1~4` 스프라이트만 있고, 아직 이를 발사체로 쓰는 프로젝트 전용 프리팹/애니메이션/런타임 로직은 없다.
- `Entry` 애니메이션과 기본 Animator Controller는 이미 존재한다.
  - `Assets/_Project/Art/Animations/Enemies/Boss/Bertha/BerthaEntry.anim`
  - `Assets/_Project/Art/Animations/Enemies/Boss/Bertha/Bertha.controller`
- 현재 `BossIntroSequenceController`는 정방향 Entry 재생만 담당한다. 역재생 연출이나 좌우 끝 돌진용 위치 보정은 없다.

즉 CL-052는 새 보스를 처음 만드는 작업이 아니라, 이미 있는 Bertha 전투 루프에 `투사체 확장`과 `새 전조/돌진 축`을 추가하는 작업으로 보는 것이 맞다.

## 구현 목표

### 1. 강공격을 단순 근접 패턴에서 복합 패턴으로 확장한다

- 기존 강공격의 근접 박스 판정은 유지한다.
- 강공격 액티브 구간 중 지정 시점에 `8방향 radial burst`를 1회 발사한다.
- 플레이어 입장에서는 `근접 범위 회피 + 퍼지는 추가 투사체 회피`를 동시에 요구하는 패턴이 된다.

### 2. 풀콤보를 단일 히트가 아니라 단계형 패턴으로 확장한다

- 기존 풀콤보의 메인 애니메이션은 유지한다.
- 다만 실제 위협은 아래 3단계로 구성한다.
  - 1차 fan burst
  - 2차 fan burst
  - 마무리 omni burst
- 중간 2회 fan burst는 고정 각도 하드코딩보다 `발사 수`, `spreadAngle`, `속도`를 Inspector에서 튜닝 가능하게 둔다.
- 마지막 omni burst는 기본값을 `8방향 radial`로 두되, 필요하면 12방향 이상으로 늘릴 수 있게 직렬화 값으로 뺀다.

### 3. 전조와 돌진은 별도 축으로 분리한다

- `Entry`를 역재생한 것처럼 보이는 상승 연출은 강공격/풀콤보의 세부 동작에 끼워 넣지 않는다.
- 이 연출은 새 돌진 패턴의 전용 telegraph 단계로 분리한다.
- 실제 맵 횡단 돌진은 기존 `DashTelegraph -> DashCharge`와 다른 규칙을 가지므로, 기존 짧은 돌진 상태를 재활용하지 않고 별도 상태명을 쓰는 것이 맞다.

## 범위 정의

### CL-052 MVP

- 강공격 추가 투사체 8방향 발사
- 풀콤보 2회 fan burst + 1회 omni burst
- `ComboAtk/FX` 기반 프로젝트 전용 투사체 프리팹/애니메이션 구성
- 기존 Bertha 통합 bootstrap에 새 투사체 컴포넌트 연결
- 중복 피격 방지, 생존/사망 중단 처리, 쿨다운 검증

### CL-052 Stretch

- `Entry` 역재생 느낌의 상승 전조
- 맵 왼쪽 끝 앵커에서 플레이어 위치 기준 라인으로 진입
- 맵 오른쪽 끝까지 가로지르는 sweep dash
- 새 패턴을 `BerthaCombatPatternSelector`에 선택지로 추가

### 이번 문서 범위 밖

- 외부 에셋 원본 수정
- TopDown Engine 원본 코드 수정
- 공용 씬 YAML 직접 편집
- 보스 페이즈 2 전체 설계
- 사망/보상/출구 후속 흐름 재설계

## 핵심 설계 결정

### 1. 투사체는 `BerthaAreaAttackController`에 직접 우겨 넣지 않는다

현재 `BerthaAreaAttackController`는 아래 책임만 맡고 있다.

- telegraph 표시
- 방향 고정
- 근접 박스 판정 1회 적용

이 클래스에 발사체 다중 타이밍, fan/radial 분기, VFX까지 넣기 시작하면 `LightAttack1/2`까지 불필요하게 복잡해진다.

따라서 CL-052에서는 아래 구조를 추천한다.

- 근접 판정: 기존 `BerthaAreaAttackController` 유지
- 추가 투사체: 별도 `Projectile Burst Emitter` 컴포넌트
- 패턴 타이밍 추적: `AIStateEvent` 또는 애니메이션 normalized time을 감시하는 전용 listener

즉 강공격/풀콤보는 `근접 공격 본체 + 투사체 보조 패턴`의 조합으로 구현하는 편이 맞다.

### 2. 투사체는 Bertha 전용 경량 구현으로 간다

현재 `_Project` 아래에는 바로 재사용할 프로젝트 전용 보스 투사체 스크립트가 없다.

따라서 CL-052에서는 아래 정도의 얇은 구조가 적절하다.

- `BerthaBossProjectile`
  - 직선 이동
  - 수명 타이머
  - 단일 대상 피격 처리
  - 충돌 시 소멸 또는 관통 여부 옵션
- `BerthaProjectileBurstEmitter`
  - radial burst
  - fan burst
  - 발사 수, 각도, 속도, 수명, 데미지 직렬화

보스 전용 패턴에 굳이 `ProjectileWeapon` 계층을 억지로 연결하기보다, 이쪽이 구현 비용과 디버깅 비용이 더 낮다.

### 3. 풀콤보 중간 burst는 fan, 마지막은 omni로 분리한다

유저 요청상 강공격은 `8방향`, 풀콤보는 `여러 갈래 2번 + 모든 방향 마지막 1번`이다.

이 해석을 아래처럼 고정한다.

- 강공격: `radial`
- 풀콤보 1차/2차: `fan`
- 풀콤보 마지막: `radial`

이렇게 구분해야 플레이 감각도 분리되고, 패턴 간 역할도 겹치지 않는다.

### 4. `Entry` 역재생은 실제 구현상 새 reversed clip으로 처리하는 쪽이 안전하다

런타임에서 Animator 속도를 음수로 돌려 정방향 클립을 뒤집어 재생하는 방식은 스프라이트 스왑 애니메이션에서 관리가 까다롭다.

따라서 CL-052 Stretch에서는 아래 방식을 기본안으로 둔다.

- 시각적으로는 `Entry 역재생`
- 실제 구현은 `Entry`와 동일 프레임을 역순으로 갖는 프로젝트 소유 clip 생성

즉 연출 의도는 유지하되, 구현은 예측 가능한 방식으로 가져간다.

## 추천 런타임 구조

| 레이어 | 책임 |
|---|---|
| `BerthaLightAttack1Bootstrap` | 기존 통합 상태기 유지, 신규 컴포넌트 배치와 참조 연결 |
| `BerthaHeavyAttackController` | 기존 근접 강공격 박스 판정 유지 |
| `BerthaFullComboController` | 기존 근접 풀콤보 박스 판정 유지 |
| `BerthaProjectileBurstEmitter` | radial/fan 투사체 다발 발사 |
| `BerthaHeavyProjectilePatternDriver` 또는 공용 state listener | 강공격 시점 1회 burst 트리거 |
| `BerthaFullComboProjectilePatternDriver` 또는 공용 state listener | 풀콤보 내 3회 burst 타이밍 관리 |
| `BerthaBossProjectile` | 실제 이동/피해 적용/수명 처리 |
| `BerthaSweepDashController` | Stretch 범위의 좌측 시작점 정렬, 상승 전조, 우측 끝 돌진 |

중요한 방향성:

- 새 별도 bootstrap을 또 만들기보다 `BerthaLightAttack1Bootstrap`에 통합하는 편이 맞다.
- 다만 상태기 내부 로직까지 한 클래스에 몰아넣지는 않는다.

## 상태기 설계

### 강공격

기본 흐름은 유지한다.

```text
HeavyTelegraph
  -> HeavyAttack
  -> HeavyRecover
```

다만 `HeavyAttack` 안에서 아래 이벤트를 추가한다.

```text
HeavyAttack Enter
  -> 근접 공격 애니메이션 재생
  -> 지정 시점에 8방향 radial projectile burst 1회
  -> 기존 박스 판정 1회 유지
```

### 풀콤보

AI 상태는 굳이 더 잘게 나누지 않고, 기존 `FullTelegraph -> FullComboAttack -> FullRecover`를 유지하는 편이 안전하다.

```text
FullTelegraph
  -> FullComboAttack
  -> FullRecover
```

`FullComboAttack` 안에서만 내부 타이밍을 3개로 나눈다.

```text
FullComboAttack
  -> burst A : fan
  -> burst B : fan
  -> burst C : radial
```

이 구조를 쓰면 selector와 state machine을 크게 흔들지 않고도 패턴 밀도를 올릴 수 있다.

### Stretch 돌진 패턴

별도 상태로 분리한다.

```text
SweepTelegraph
  -> SweepRise
  -> SweepDash
  -> SweepRecover
```

권장 의미는 아래와 같다.

- `SweepTelegraph`: 플레이어 기준 라인 샘플링, 전조 시작
- `SweepRise`: 역재생 Entry 느낌의 상승/이탈 연출
- `SweepDash`: 좌측 앵커에서 우측 앵커까지 수평 돌진
- `SweepRecover`: 종료 후 기본 전투 루프로 복귀

## 좌표와 앵커 기준

맵 좌우 끝을 코드에서 자동 추론하려고 시작하면 방 구조 의존성이 커진다.

Stretch 1차 구현은 아래처럼 단순하게 고정하는 편이 낫다.

- Bertha 오브젝트 또는 보스룸 루트에 `leftSweepAnchor`, `rightSweepAnchor`를 직렬화한다.
- 패턴 시작 시 플레이어 위치에서 `Y`를 샘플링한다.
- 최종 돌진 라인은 아래처럼 잡는다.

```text
start = (leftSweepAnchor.x, sampledPlayerY)
end = (rightSweepAnchor.x, sampledPlayerY)
```

- `sampledPlayerY`는 필요하면 보스룸 허용 범위로 clamp한다.

이 방식이 초반 구현 리스크가 가장 낮다.

## Unity Editor 작업 방향

다음 자산은 `_Project` 아래 프로젝트 소유 파일로 만든다.

- `ShockWave` 스프라이트를 사용하는 투사체 애니메이션 클립
- Bertha 전용 투사체 프리팹
- 필요 시 `Entry Reverse` 애니메이션 클립
- 필요 시 sweep dash용 Animator state 추가

주의:

- `.anim`, `.controller`, `.prefab`, `.asset` 변경은 Unity Editor에서 `_Project` 자산만 대상으로 진행한다.
- 외부 에셋 원본이나 TopDown Engine 데모 프리팹은 수정하지 않는다.

## 예상 수정 파일 범위

### C# 신규 또는 확장 후보

- `Assets/_Project/Scripts/Runtime/Enemies/Boss/Bertha/BerthaProjectileBurstEmitter.cs`
- `Assets/_Project/Scripts/Runtime/Enemies/Boss/Bertha/BerthaBossProjectile.cs`
- `Assets/_Project/Scripts/Runtime/Enemies/Boss/Bertha/BerthaHeavyProjectilePatternDriver.cs`
- `Assets/_Project/Scripts/Runtime/Enemies/Boss/Bertha/BerthaFullComboProjectilePatternDriver.cs`
- `Assets/_Project/Scripts/Runtime/Enemies/Boss/Bertha/BerthaSweepDashController.cs`
- `Assets/_Project/Scripts/Runtime/Enemies/Boss/Bertha/BerthaCombatPatternSelector.cs`
- `Assets/_Project/Scripts/Runtime/Enemies/Boss/Bertha/BerthaLightAttack1Bootstrap.cs`

### 자산 신규 또는 확장 후보

- `Assets/_Project/Prefabs/Enemies/Boss/Bertha/`
- `Assets/_Project/Art/Animations/Enemies/Boss/Bertha/`
- `Assets/_Project/Art/Enemies/Boss/1_Bertha/Attacks/ComboAtk/FX/`

## 추천 구현 순서

1. `ShockWave` 기반 투사체 프리팹과 런타임 이동/피격 로직 구성
2. `BerthaProjectileBurstEmitter` 작성
3. 강공격에 8방향 radial burst 연결
4. 풀콤보에 3단계 burst 타이밍 연결
5. `BerthaLightAttack1Bootstrap`에 신규 컴포넌트 자동 연결
6. 수치 튜닝
7. Stretch로 `SweepTelegraph/SweepDash` 상태 추가
8. `Entry Reverse` 연출 자산 연결
9. selector에 sweep dash 선택 조건 추가

## 테스트 체크리스트

- 강공격에서 근접 판정은 기존처럼 1회만 적용되는가
- 강공격 추가 투사체는 정확히 8방향으로 1회만 발사되는가
- 풀콤보 fan burst가 2회만 발사되는가
- 풀콤보 마지막 radial burst가 1회만 발사되는가
- 투사체가 같은 대상에게 프레임마다 중복 타격되지 않는가
- 보스 사망 시 대기 중인 burst가 취소되는가
- 패턴 중 intro 상태와 충돌하지 않는가
- sweep dash 사용 시 시작점과 끝점이 앵커 기준으로 안정적으로 고정되는가
- sweep dash 라인이 플레이어의 현재 위치 기준으로 읽히는가

## 리스크와 사전 결정 사항

### 1. 풀콤보 `여러 갈래`의 정확한 발사 수는 아직 미확정이다

문서 단계에서는 아래처럼 정리한다.

- 구조는 확정
  - fan
  - fan
  - omni
- fan의 정확한 `발사 수`와 `spreadAngle`은 튜닝 값으로 둔다.

### 2. 역재생 Entry는 실제로는 별도 clip 생성이 더 안전하다

요구사항 표현은 `역재생`으로 유지하되, 구현은 `reversed clip`을 기본안으로 둔다.

### 3. 좌우 끝 돌진은 자동 bounds 추론보다 앵커 방식이 우선이다

초기 구현에서 방 경계 자동 판별까지 풀려 하면 CL-052 범위를 초과하기 쉽다.

따라서 Stretch 1차는 직렬화 앵커 기반으로 끝낸다.

## 결론

CL-052의 핵심은 새 상태기를 많이 추가하는 것이 아니라, 이미 있는 Bertha 전투 루프에 `추가 투사체 레이어`를 안정적으로 덧씌우는 것이다.

우선순위는 아래처럼 잡는 것이 맞다.

1. 강공격 8방향 burst
2. 풀콤보 3단계 burst
3. Stretch로 역재생 Entry 기반 sweep dash

이 순서를 지키면 기존 Bertha 구조를 깨지 않고 패턴 2의 밀도와 전조 품질을 올릴 수 있다.
