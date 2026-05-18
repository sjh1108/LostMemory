# CL-013 플레이어 피격 경직 구현 기록

작성일: 2026-04-23

대상 씬: `LostMemory/Assets/Scenes/test_khi.unity`

## 목적

CL-013의 목표는 플레이어가 피해를 실제로 받은 순간 짧은 제어 불가 상태(경직)와
후속 i-frame을 부여하는 것이다. CL-014 다운/부활, CL-015 상태머신 정비,
CL-017 히트스톱/플래시가 얹힐 공통 블록을 제공한다.

이번 구현은 경직 로직에만 집중한다. 히트스톱, 플래시, 넉백, 방향별/강약 경직은
후속 CL에서 다룬다.

## 설계 기준

- 피해 적용 직후 짧은 경직(0.12s) + 후속 i-frame(0.15s)
- 경직 중에는 이동, 공격, 대시 모두 차단
- 진행 중인 공격 스윙은 경직 진입 시 즉시 중단
- Parry가 permits를 소유 중이면 Hit 경직은 진입하지 않는다 (충돌 회피)
- TDE 원본 (Health, CharacterMovement 등)은 수정하지 않는다
- 피해 소스 측 수정 없이 `Health.OnHit` 구독만으로 트리거
- 씬/프리팹/inputactions YAML은 직접 편집하지 않는다
- CL-012 parry 컨트롤러의 permits 캐시 / 복원 패턴을 동일하게 사용

## 확정된 설계 결정

| 결정 | 값 | 근거 |
|---|---|---|
| 공격 중 피격 | 즉시 취소 (`AbortCurrentAttack`) | "제어 불가" 기획 의도와 정확히 일치. 변경 10줄 미만. |
| post-hit i-frame | 0.15s | 다단히트 스턴락 방지. Parry 성공 후 0.12s 무적과 동일 패턴. |
| Parry FailureRecovery 중 피해 | Hit 경직 발동 안 함 | Permits 소유권 race 회피. Parry 후딜이 이미 제어 불가 상태 제공. |
| 이동 차단 방식 | `CharacterMovement.MovementForbidden` | Parry와 동일한 permits 패턴. CL-015 condition-state 정비와 충돌 없음. |
| 기본 `hitStunDuration` | 0.12s | 기획 "아주 짧은 경직". 강경직은 후속 `TriggerHitStun(duration)` override로. |
| 애니메이터 트리거 | `"Hit"` | `Health.cs` L512의 `"Damage"`와 이름 분리. 현재 프리팹에 Animator 없음 → null-safe. |

## 추가/수정된 파일

신규 파일:

```text
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiHitStunController.cs
```

수정 파일:

```text
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeComboController.cs
```

문서:

```text
client/docs/khi/cl013_player_hit_stun_implementation.md
```

Editor 수작업 (유저가 처리):

- `TestKhi_MinimalCharacter2D.prefab`에 `KhiHitStunController` 컴포넌트 부착

## 컴포넌트 구조

### KhiHitStunController

- 역할: `Health.OnHit` 구독, 경직 상태머신, 공격/대시/이동 permits 토글, 후속 i-frame 관리.
- 상태: `Idle`, `HitStun`, `PostHitIFrame`.
- 입력 훅: `Health.OnHit` 델리게이트 필드 (`OnEnable`에서 subscribe, `OnDisable`/`OnDestroy`에서 unsubscribe).
- `[DefaultExecutionOrder(65)]` — parry(60) 이후에 평가.

전이:

```text
Idle → HitStun          : Health.OnHit에서 선행 조건 모두 통과 시
HitStun → PostHitIFrame : Time.time >= _stateEndTime (경직 시간 경과)
PostHitIFrame → Idle    : i-frame 시간 경과. postHitInvulnerability==0이면 스킵
```

진입 선행 조건 (하나라도 실패 시 스킵):

1. `health.CurrentHealth > 0`
2. `health.LastDamage > 0`
3. `CurrentState == Idle`
4. Parry가 `ParryWindow` 또는 `FailureRecovery`가 아닐 것

공개 API:

```text
KhiHitStunState CurrentState { get; }
bool IsStunned
bool IsInvulnerable
float CurrentStateRemaining { get; }

event Action<float> HitStunStarted   // duration 전달 (CL-017 히트스톱/플래시용)
event Action        HitStunEnded
event Action        HitReceived       // PlayerCombatReporter용

void TriggerHitStun(float overrideDuration = -1f)   // CL-014/보스용 수동 훅
```

경직 진입 시 permits 적용:

```text
handleWeapon.AbilityPermitted   = false
dashController.PermitAbility(false)
meleeCombo.ExternalBlock        = true
characterMovement.MovementForbidden = true
meleeCombo.AbortCurrentAttack()      // 현재 공격 즉시 중단
health.DamageDisabled() + DamageEnabled(hitStunDuration + postHitInvulnerability)
```

Permits 복원:

- `HitStun → PostHitIFrame` 전이 시: permits 복원 (i-frame 중에는 입력 가능, 피해만 차단)
- `OnDisable` / `OnDestroy` / `LateUpdate` 안전망에서 `_hasCachedPermits`가 남아 있으면 강제 복원

### KhiMeleeComboController (수정)

- 추가: `public void AbortCurrentAttack()` — 진행 중인 공격 코루틴 즉시 중단 요청
- 내부 플래그: `_externalAbortRequested`
- `RunAttack` 코루틴 세 지점에 abort 체크:
  - startup `WaitForSeconds` 직후
  - active phase while 루프 조건 + 루프 직후
  - recovery while 루프 조건 + 루프 직후
- abort 감지 시 `FinalizeAbortedAttack()`으로 콤보 상태 초기화 후 `yield break`
- `AttackActiveEnded` 이벤트는 active phase가 한 번이라도 시작된 경우에만 발사되도록 유지

`ExternalBlock`은 기존 역할(신규 입력·버퍼 차단) 그대로 유지. `AbortCurrentAttack`은 별도 경로.

## 주요 설계 결정

### 1. Health.OnHit 구독 (피해 소스 수정 없음)

피해 소스 측 수정 없이 `Health.OnHit` 델리게이트 구독만으로 경직을 트리거한다.

- Parry는 피해 소스에서 `TryResolveIncomingDamage`를 호출해 **피해 적용 전**에 무효화해야
  한다. 그래서 CL-012는 `TestKhiDamageTrap`에 훅을 직접 박았다.
- Hit 경직은 피해가 **실제로 적용된 직후**에 발동하면 되므로, `Health.Damage` 내부에서
  발사되는 `OnHit` 콜백이 단일 진실 소스다.
- 이 방식 덕분에 `TestKhiDamageTrap` 변경 없이도 동작하고, 이후 일반 `DamageOnTouch`
  기반 피해 소스가 추가되어도 자동으로 경직이 적용된다.

### 2. Parry FailureRecovery 중 피해 시 Hit 경직 스킵

패링 실패 후딜(0.45s) 중 감쇠 피해를 받으면 `Health.OnHit`이 발사된다. 이때 Hit 경직을
추가로 걸면 permits 캐시 race가 발생한다:

```text
Parry가 이미 캐시한 permits (예: WeaponPermitted=true)
→ Parry가 permits를 false로 세팅
→ HitStun이 진입하며 또 캐시 시도 (현재 값 false를 캐시)
→ HitStun이 permits를 false로 세팅
→ HitStun 종료 → false로 "복원" (원래 Parry가 캐시했던 true가 아닌)
→ Parry 종료 시 복원 순서가 꼬이거나 덮어씌움
```

선행 조건 #4 (`parryController.IsParryWindowActive || IsParryRecovering`이면 스킵)로
이 race를 원천 차단. 시각 피드백이 부족하면 CL-017에서 parry 이벤트를 별도로 구독해
플래시만 보강하는 방향.

### 3. MovementForbidden vs CharacterConditions.Stunned

이동 차단은 TDE `CharacterMovement.MovementForbidden` 로컬 플래그를 사용한다.

- TDE `CharacterStates.CharacterConditions.Stunned`를 세팅하는 방법도 있지만, 이는
  글로벌 condition이라 `KhiDashController.HandleInput` 등 "Normal일 때만 동작"하는
  여러 ability에 예기치 못한 영향을 준다.
- Parry가 이미 permits 방식을 쓰고 있어 패턴 일관성 유지가 쉽다.
- CL-015 상태머신 정리에서 condition-state로 한꺼번에 승격 가능.

### 4. 공격 즉시 취소는 `AbortCurrentAttack` 별도 경로

`KhiMeleeComboController.ExternalBlock`은 **신규 입력 차단**에만 반응하고, 이미 진행 중인
`RunAttack` 코루틴의 두 while 루프를 끊지 못한다. 기획 "제어 불가" 의도를 정확히 지키려면
진행 중인 스윙도 끊어야 하므로 별도 public 메서드 `AbortCurrentAttack()`을 추가했다.

- 내부 플래그 `_externalAbortRequested`는 세 지점에서 체크 (startup, active, recovery).
- Active phase를 한 번이라도 시작한 경우 `AttackActiveEnded` 이벤트는 정상 발사하도록
  유지 (hitbox preview 제거, 비주얼 정리 코드가 여기 연결돼 있음).
- `_externalAbortRequested`는 매 `RunAttack` 진입 시 false로 리셋하여 stale abort가
  다음 공격에 전염되지 않도록 한다.

### 5. post-hit i-frame 포함

`TestKhiDamageTrap.OnTriggerStay2D`는 이미 자체 `damageInterval`로 자기 자신의
재타격은 차단하지만, 이후 일반 투사체/겹치는 히트박스는 짧은 시간 내에 재타격을
가할 수 있다. 경직이 종료되자마자 다시 경직으로 들어가면 스턴락이 발생한다.

`health.DamageDisabled() + DamageEnabled(hitStunDuration + postHitInvulnerability)`로
경직 + 0.15s 동안 피해 자체를 막아 스턴락을 차단한다. Parry 성공 후 0.12s 무적과 동일한
패턴이다.

## 검증 결과

### 자동 검증

```text
dotnet build LostMemory/Assembly-CSharp.csproj --no-restore
→ 기존 2개 deprecation 경고만, 신규 에러/경고 0
```

```text
git diff --check (KhiHitStunController.cs, KhiMeleeComboController.cs)
→ 공백 이슈 없음
```

참고: 로컬 `Assembly-CSharp.csproj`(gitignore)가 stale 상태라 Khi*·TestKhiDamageTrap
항목을 수동 추가 후 빌드. Unity 재실행 시 자동 재생성되므로 저장소 영향 없음.

### 수동 검증 (플레이 테스트)

완료:

- [x] 트랩 진입 시 `EnterHitStun duration=0.120` 로그
- [x] 경직 타이밍 정확 (HitStun 0.12s, PostHitIFrame 0.15s)
- [x] 경직 종료 후 `HitStun -> PostHitIFrame`, `PostHitIFrame -> Idle` 로그
- [x] 경직 중 이동/공격/대시 입력 모두 차단됨
- [x] 경직 종료 후 0.15s i-frame 동안 체력 감소 없음
- [x] i-frame 종료 후 재진입 정상 (재타격 시 새 경직 시작)
- [x] 콤보 스윙 중 트랩 진입 시 스윙 즉시 중단되고 경직 상태로 전이
- [x] 패링 성공 시 경직 발동 안 함 (OnHit 미발사)
- [x] 패링 실패 후딜 중 감쇠 피해 수신 시 Hit 경직 발동 안 함
- [x] 대시 중 트랩 통과 시 경직 발동 안 함 (Invulnerable로 OnHit 미발사)
- [x] 컴포넌트 비활성화 시 permits 복원 (이동 가능)

미완 (이번 CL 범위 밖):

- [ ] `setAnimatorTrigger` 토글 및 Hit 애니메이션 재생 검증 → 프리팹에 Animator 추가 시점에 자연스럽게 검증
- [ ] 사망 진입 시 Hit 경직 스킵 검증 → CL-014 다운/부활에서 함께 검증

실측 로그 샘플:

```text
[KhiHitStun] EnterHitStun duration=0.120 t=4.620
[KhiHitStun] HitStun -> PostHitIFrame  t=4.740  (delta 0.120)
[KhiHitStun] PostHitIFrame -> Idle     t=4.891  (delta 0.151)
[KhiHitStun] EnterHitStun duration=0.120 t=6.340  (재진입 정상)
```

## 후속 CL 연결

- **CL-014 다운/부활**: `Health.OnDeath` 구독 + 필요 시 `TriggerHitStun(overrideDuration)`
  수동 호출로 긴 경직 후 Down 상태 진입.
- **CL-015 상태머신 정리**: `MovementForbidden` + permits 조합을
  `CharacterConditions.Stunned` 중심으로 승격 가능. `KhiDashController.HandleInput`의
  `Normal` 체크와 함께 재정비.
- **CL-017 히트스톱·피격 플래시**: `HitStunStarted(duration)` 구독 →
  `Time.timeScale` 짧은 감속 / SpriteRenderer 색 ping-pong. Parry 실패 감쇠 피해에 대한
  시각 피드백이 필요하면 이 시점에 `KhiParryController.ParryFailed` 이벤트를 별도
  구독 (KhiHitStunController 수정 없이).
- **PlayerCombatReporter (`Player.Hit` 글로벌 이벤트)**:
  `HitReceived` 구독 + `health.LastDamage`, `health.LastDamageDirection`을 payload로
  전달.

## 관련 문서

- 구현 계획 (로컬): `C:\Users\AD\.claude\plans\humming-swinging-hickey.md`
- 직전 CL: `client/docs/khi/cl012_timing_parry_implementation.md`
- 전투 시스템 기획: `docs/03_combat_system.md` (피격 경직 권장안 L43-48)
- 클라1 마스터 플랜: `client/docs/khi/client1_tasks_master_plan.md` (CL-013 정의 L74-75)
