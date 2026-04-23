# CL-012 타이밍 패링 구현 기록

작성일: 2026-04-23

대상 씬: `LostMemory/Assets/Scenes/test_khi.unity`

## 목적

CL-012의 목표는 플레이어가 우클릭으로 짧은 패링 창을 열어 그 안에 들어온 피해를
무효화하는 기능을 도입하는 것이다.

이번 작업은 방향 판정 없이 **타이밍만** 보는 단순 패링이며, 이후 CL에서 방향 패링,
투사체 반사, 적 기반 공격 시스템과 연결하기 위한 기반이다.

## 설계 기준

- 방향 판정 없이 타이밍만 본다.
- 성공: 피해 무효화 + 즉시 행동 가능 + 짧은 무적 (0.12s).
- 실패: 긴 후딜 (0.45s), 후딜 중 피해는 원래의 20% (최소 1).
- 자동 자해 피해 없음.
- TDE 원본 `Health`, `DamageOnTouch`는 수정하지 않는다.
- 피해 소스 레벨에서 가로챈다. `Health.OnHit`은 post-damage라 부적합.
- `InputSystem_Actions.inputactions`와 씬/프리팹 YAML은 직접 편집하지 않는다.

이전 구현 시도에서 scene/inputactions 복구 과정에 이동 입력이 꼬인 사고가 있었다.
그래서 이번 CL은 **스크립트만으로** 구현하고, 로직 안정 후 후속 CL에서 정식
InputAction을 추가한다.

## 타이밍 파라미터

| 이름 | 값 | 설명 |
|---|---|---|
| `parryWindow` | 0.16s | 우클릭 직후 피해 무효화 창 |
| `parryFailureRecovery` | 0.45s | 창 만료 시 후딜, 감쇠 피해 구간 |
| `parryCooldown` | 0.45s | 성공/실패 이후 재패링 차단 구간 |
| `successfulParryInvulnerability` | 0.12s | 성공 시 짧은 `Health` 무적 |
| `failedParryDamageRatio` | 0.20 | 실패 후딜 중 피해 감쇠 비율 |
| `minFailedParryDamage` | 1 | 감쇠 후 최소 피해량 |

## 추가/수정된 파일

신규 파일:

```text
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiParryController.cs
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiParryDamageOnTouch.cs
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiParryFeedbackPresenter.cs
```

수정 파일:

```text
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeComboController.cs
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/TestKhiDamageTrap.cs
LostMemory/Assets/Scenes/test_khi.unity
```

문서:

```text
docs/khi/cl012_parry_pending_verification.md
docs/khi/cl012_timing_parry_implementation.md
```

## 컴포넌트 구조

### KhiParryController

- 역할: 패링 상태머신, 입력 처리, 공격/대시 permits 토글, 무적 트리거.
- 상태: `Idle`, `ParryWindow`, `FailureRecovery`, `Cooldown`.
- 입력: `Mouse.current.rightButton.wasPressedThisFrame` fallback.
  - `InputActionReference parryAction` 필드는 optional로 준비만 해두고 현재는 비워둔다.
- 공개 API:

```text
ParryState CurrentState { get; }
bool IsParryWindowActive { get; }
bool IsParryRecovering { get; }
bool IsParryInCooldown { get; }

event Action ParryStarted
event Action ParrySucceeded
event Action ParryFailed
event Action ParryEnded

bool TryResolveIncomingDamage(
    GameObject instigator,
    Vector2 incomingDirection,
    float incomingDamage,
    out float resolvedDamage)
```

- 피해 판정 흐름:

```text
state == Idle, Cooldown         -> false, base damage 그대로
state == ParryWindow            -> true, resolved = 0 (성공)
state == FailureRecovery        -> true, resolved = max(min, inc * ratio)
```

- Safety:
  - `LateUpdate`에서 Idle인데 permits 캐시가 남아 있으면 강제 복원.
  - `OnDisable`, `OnDestroy`에서 permits 복원.

### KhiParryDamageOnTouch (현재 미사용)

- `DamageOnTouch`를 상속해 `OnCollideWithDamageable`을 override한다.
- 상대에 `KhiParryController`가 붙어 있으면 `TryResolveIncomingDamage`를 거치고,
  성공 시 피해 스킵, 실패 후딜 시 감쇠 피해를 적용한다.
- 감쇠 피해 경로는 TDE `DamageOnTouch.OnCollideWithDamageable`의 라인 634~676
  순서를 수동으로 재현한다 (전략 A).
- **현재 씬에서는 사용하지 않는다**. 이유는 "주요 설계 결정" 3번 참조.
- 후속 CL에서 일반 `DamageOnTouch` 기반 적/트랩이 생기면 이 클래스를 교체
  컴포넌트로 사용한다.

### KhiParryFeedbackPresenter

- 역할: 임시 시각 피드백.
- `KhiParryController` 이벤트 4개를 구독한다.
- `LineRenderer` 기반 ring 자동 생성 가능.
- 기본 색상: window = cyan, success = white, failure = red.
- ring은 창 지속 동안 플레이어에 고정되고 남은 시간에 따라 알파 페이드.

### KhiMeleeComboController (수정)

- 추가: `public bool ExternalBlock { get; set; }`
- `Update()`와 `RequestAttack()` 진입부에서 `ExternalBlock`이 true면 입력 무시.
- 기본값 false이므로 기존 공격 흐름에는 영향 없음.
- 이 플래그는 `KhiParryController`가 패링 창 중 공격 입력을 차단하기 위해 사용한다.

### TestKhiDamageTrap (수정)

- `OnTriggerStay2D`의 `targetHealth.Damage()` 호출 직전에 패링 훅을 삽입한다.

```text
1. targetHealth에서 KhiParryController 해결 (self + parent)
2. TryResolveIncomingDamage 호출
3. 반환 false     -> 원래 피해 그대로 Damage()
4. 반환 true, 0   -> 피해 스킵 (성공)
5. 반환 true, >0  -> 감쇠 값으로 Damage()
```

## 주요 설계 결정

### 1. MonoBehaviour 채택 (CharacterAbility 아님)

`CharacterAbility`를 상속하는 경우 CL-011 대시 코드와의 상호작용에서 상태 복구가
어려웠다. 패링은 공격/대시와 독립적으로 토글만 하면 되므로 독립 MonoBehaviour가
더 단순하다.

### 2. 피해 소스 레벨에서 가로채기

`Health.OnHit` 이벤트는 체력이 이미 깎인 뒤에 호출되므로 피해 무효화를 구현할
수 없다. 대신 피해를 주는 쪽에서 `KhiParryController.TryResolveIncomingDamage`를
먼저 호출해 무효화/감쇠를 결정한다.

### 3. TestKhiDamageTrap에 훅을 직접 박은 이유

`TestKhiDamageTrap`은 TDE `DamageOnTouch`를 쓰지 않고 `OnTriggerStay2D`에서
`Health.Damage()`를 직접 호출한다. 또한 `TestKhiSceneBootstrap.EnsureDamageTrap()`이
Play 시 자동으로 `TestKhiDamageTrap` 컴포넌트를 붙이기 때문에, Editor에서
`KhiParryDamageOnTouch`를 수동으로 추가해도 실행 중에는 사용되지 않는다.

따라서 이번 CL에서는 `TestKhiDamageTrap`의 피해 호출 지점에 직접 훅을 박았다.
`KhiParryDamageOnTouch`는 후속 CL에서 일반 `DamageOnTouch` 기반 피해 소스가
생겼을 때 사용하기 위해 남겨둔다.

### 4. Mouse.current fallback만 사용 (InputAction 미정의)

이전 구현에서 `InputSystem_Actions.inputactions` 수정 도중 이동 액션맵이 깨진
적이 있어, 이번 CL은 입력 자산을 건드리지 않는다. `Mouse.current` 직접 읽기는
액션맵 활성/비활성과 독립이므로 UI 액션맵의 RightClick과 충돌 가능성이 있지만
현재 테스트 씬에서는 UI가 없으므로 문제가 되지 않는다.

정식 `Parry` 액션 추가는 후속 CL로 분리한다.

### 5. KhiMeleeComboController.ExternalBlock 추가

`KhiMeleeComboController`는 `Mouse.current.leftButton.wasPressedThisFrame`을
직접 읽기 때문에 TDE `HandleWeapon.AbilityPermitted` 토글만으로는 공격 입력이
차단되지 않는다. 최소 변경으로 `ExternalBlock` 플래그를 추가해
`KhiParryController`가 패링 창 동안 공격 입력을 막을 수 있게 했다.

### 6. 성공 후 무적과 쿨다운 분리

성공 시에는 permits를 즉시 복원해 플레이어가 바로 공격/대시할 수 있게 한다.
다만 `Health.DamageDisabled()`로 0.12초 짧은 무적을 주어 다단히트 트랩에서
연속 피해를 막는다. 재패링 입력은 `parryCooldown` 0.45초 동안 차단한다.

## 공격/대시 permits 토글 순서

`KhiParryController.EnterParryWindow`:

```text
1. CachePermitsIfNeeded (idempotent)
2. handleWeapon.AbilityPermitted = false
3. dashController.PermitAbility(false)
4. meleeCombo.ExternalBlock = true
5. state = ParryWindow, ParryStarted 이벤트 발행
```

`TransitionToCooldownAsSuccess`:

```text
1. RestorePermits (공격/대시 즉시 복원)
2. health.DamageDisabled + StartCoroutine(health.DamageEnabled(0.12))
3. state = Cooldown, ParrySucceeded 발행
```

`TransitionToFailureRecovery`:

```text
1. permits는 그대로 유지 (여전히 차단)
2. state = FailureRecovery, ParryFailed 발행
```

`FailureRecovery -> Cooldown`:

```text
1. RestorePermits
```

`Cooldown -> Idle`:

```text
1. RestorePermits (idempotent)
2. ParryEnded 발행
```

## 검증 결과

### 자동 검증

- Rider `get_file_problems` 전 파일 통과.
- 컴파일 에러 없음.

### 수동 검증 (플레이 테스트)

완료:

- [x] 우클릭 시 `KhiParry EnterParryWindow` 로그.
- [x] 창 만료 시 `ParryWindow timeout -> FailureRecovery` 로그.
- [x] 실패 후 `FailureRecovery -> Cooldown -> Idle` 로그.
- [x] 트랩 위에서 타이밍 맞춰 우클릭 시 `ParrySuccess -> Cooldown` 로그와
      트랩 `TestKhiDamageTrap.OnTriggerStay2D` 호출 스택 확인.
- [x] 성공 시 즉시 공격/이동 가능 (공격 로그 확인).

미완 (수동 확인 미뤄둠, `cl012_parry_pending_verification.md` 참조):

- [ ] 감쇠 피해 20% 재현 로그.
- [ ] 성공 시 0.12초 무적 (다단히트 소스 필요).
- [ ] 시각 피드백 ring/flash.
- [ ] 패링 창 중 공격/대시 입력 차단.
- [ ] 쿨다운 중 재패링 차단.
- [ ] 자해 피해 없음 최종 체크.
- [ ] 컨트롤러 비활성화 시 permits 복원.
- [ ] 다른 트랩의 피해는 패링 영향 없음.

## 후속 CL

- `InputSystem_Actions.inputactions`의 Player 액션맵에 정식 `Parry` 액션 추가,
  `KhiParryController.parryAction` 연결. UI 액션맵 RightClick 충돌 검토.
- `KhiParryDamageOnTouch`를 일반 `DamageOnTouch` 기반 피해 소스에 실제로 붙여
  동작 확인 후 생존 여부 결정.
- 방향 패링 / 투사체 반사 / 완벽 패링 등 상위 기능은 CL-013 이후에 도입.
- 다단히트 트랩 혹은 투사체로 `successfulParryInvulnerability` 튜닝.
- 디버그 로그 필드(`logStateTransitions`, `logDamageToConsole` 등) 기본값은
  false로 둔다. Inspector에서 켠 값은 씬에 저장되니 테스트 후 Editor에서 해제.

## 관련 문서

- 구현 계획 (로컬): `C:\Users\SSAFY\.claude\plans\indexed-jingling-sketch.md`
- 추후 수동 확인 체크리스트: `docs/khi/cl012_parry_pending_verification.md`
- 직전 CL: `docs/khi/cl011_dash_invulnerability_plan.md`
