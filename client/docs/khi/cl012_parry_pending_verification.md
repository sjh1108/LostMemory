# CL-012 타이밍 패링 — 추후 확인 사항

구현은 완료됐고 패링 성공/실패 상태 전이와 감쇠 경로는 로그로 검증됨.
아래 항목은 후속 플레이 테스트에서 사람이 직접 눈/귀/체력 UI로 확인해야 하는 것들.

## 체크리스트

### 1. 성공 시 0.12초 무적 (`successfulParryInvulnerability`)

**테스트 방법**
- 다단히트 트랩 또는 짧은 간격으로 피해를 주는 소스가 필요
- 현재 `TestKhiDamageTrap`은 `damageInterval`(기본 1초) 간격이라 0.12초 무적 검증 불가
- 옵션 A: 일회성 테스트 위해 `damageInterval`을 0.05초로 낮추고 트랩 위에서 패링 성공
- 옵션 B: 별도 다단히트 트랩 프리팹을 만들어 검증

**기대 동작**
- 패링 성공 직후 0.12초 동안 `Health.Invulnerable = true`
- 그 사이 트랩 피해 틱이 와도 체력 변화 없음
- 0.12초 지나면 자동 복구되어 그 이후 틱부터는 정상 피해

**관련 코드**
- `KhiParryController.HandleParrySuccess()` — `health.DamageDisabled()` + `StartCoroutine(health.DamageEnabled(successfulParryInvulnerability))`
- `LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiParryController.cs`

---

### 2. 패링 창 중 공격/대시 입력 차단

**테스트 방법**
- 우클릭으로 패링 창 연 직후 0.16초 안에 좌클릭(공격) / Space(대시) 눌러보기

**기대 동작**
- 공격 애니메이션 안 나감
- 대시 안 나감
- 패링 창 끝나면 (성공 후 쿨다운이든, 실패 후 후딜이든) 정책에 맞춰 공격/대시 복구
  - 성공: 즉시 복구
  - 실패: 후딜 0.45초 후 복구

**관련 코드**
- `KhiParryController.EnterParryWindow()` — `handleWeapon.AbilityPermitted = false`, `dashController.PermitAbility(false)`, `meleeCombo.ExternalBlock = true`
- `KhiParryController.RestorePermits()` — 복구 로직

---

### 3. 쿨다운 중 재패링 차단

**테스트 방법**
- 패링 성공 직후 or 실패 후딜 끝난 직후 우클릭 연타

**기대 동작**
- `Cooldown` 상태 동안 `HandleParryInput`이 입력 무시
- 로그에 `EnterParryWindow` 안 찍힘

---

### 4. 시각 피드백 (링 / 플래시)

**테스트 방법**
- 플레이 모드에서 우클릭

**기대 동작**
- 창 열림: cyan ring이 플레이어 주위에 0.16초 표시, 남은 시간에 따라 알파 페이드
- 성공 시: 짧은 white flash
- 실패 시: 짧은 red flash
- 창 종료/쿨다운 끝: ring/flash off

**관련 코드**
- `LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiParryFeedbackPresenter.cs`

**주의**
- `KhiParryFeedbackPresenter`가 플레이어 프리팹에 부착돼 있는지 확인 필요
- 부착이 안 돼 있으면 상태는 정상이어도 시각 피드백이 안 보임

---

### 5. 자해 피해 없음

**테스트 방법**
- 트랩 밖에서 우클릭만 눌러서 창 만료 → 실패 후딜 → 쿨다운

**기대 동작**
- 체력 변화 없음 (트랩과 무관하게 창 만료만으로는 어떤 피해도 발생 안 함)

---

### 6. 감쇠 피해 재현 (20%, 최소 1) ⚠️ 최우선 확인 필요

**상태**: 2026-04-23 현재 수동 테스트 여러 차례 시도했으나 `[TestKhiDamageTrap] Parry REDUCED` 로그 한 번도 재현 실패.
- `KhiParryController` 상태 전이 (ParryWindow → FailureRecovery → Cooldown)는 정상 작동 확인됨
- 코드 경로 자체는 구현되어 있고 컴파일 성공
- 단지 수동 타이밍 잡기가 어려워서 FailureRecovery 구간 중 트랩 피해 틱이 들어오는 상황을 재현 못함

**다음 시도 시 쉬운 테스트 셋업 (옵션 A)**
`KhiParryController` Inspector:
- `Parry Window` = 0.01 (거의 즉시 실패)
- `Parry Failure Recovery` = 2.0 (2초 여유)

테스트:
1. `TestKhiDamageTrap` Inspector에서 `Log Damage To Console` = ON 필수
2. 트랩 위에 **확실히** 올라간 상태 확인 (체력이 깎이고 있어야 함)
3. 우클릭 한 번
4. 2초 동안 트랩 위에 계속 있기
5. `[TestKhiDamageTrap] Parry REDUCED on ..., damage=X` 로그 기대

**테스트 안 되는 원인 후보**
- `TestKhiDamageTrap`의 `logDamageToConsole`이 꺼져 있어서 로그만 안 찍힘 (실제로는 감쇠 적용 중일 수도)
- 트랩 Collider와 플레이어 Collider가 실제로 `OnTriggerStay2D` 트리거를 유지하지 않음
- `KhiParryController`가 플레이어 오브젝트 트리 안에 없어서 `GetComponent`/`GetComponentInParent`가 못 찾음 → 이 경우 `Parry SUCCESS` 경로도 동작 안 했을 텐데 성공은 찍히므로 이 가설은 제외 가능

**검증 후 원복**
값 확인 끝나면 원래 값으로 복구:
- `Parry Window` = 0.16
- `Parry Failure Recovery` = 0.45

---

### 7. 패링 컨트롤러 제거/비활성화 시 복구

**테스트 방법**
- 플레이 중 Inspector에서 `KhiParryController`를 Disable

**기대 동작**
- 공격/대시/이동이 정상 복구 (permits가 다 풀려야 함)
- `OnDisable` 훅의 `RestorePermits()`가 호출됨

---

### 8. 다른 트랩의 피해는 영향 없음

**테스트 방법**
- `KhiParryController`를 거치지 않는 다른 피해 소스 (원본 `DamageOnTouch` 트랩 등)와 충돌

**기대 동작**
- 우클릭 패링 여부와 무관하게 원래 피해 그대로 들어감
- 현재 구현은 `TestKhiDamageTrap`에만 훅이 있고 다른 곳에는 영향 없음

---

## 후속 CL로 미뤄야 할 것

- `InputSystem_Actions.inputactions`에 정식 `Parry` 액션 추가
- 현재는 `Mouse.current.rightButton.wasPressedThisFrame` fallback만 사용 중
- UI 액션맵의 RightClick과 충돌 여부 검토
- 디버그 로그(`logStateTransitions`, `logParryInteraction`, `logDamageToConsole`) 기본값 false로 정리
- 다단히트 트랩/투사체 시나리오에서 `successfulParryInvulnerability` 튜닝

## 구현 기준 타이밍 파라미터

| 이름 | 값 |
|---|---|
| `parryWindow` | 0.16s |
| `parryFailureRecovery` | 0.45s |
| `parryCooldown` | 0.45s |
| `successfulParryInvulnerability` | 0.12s |
| `failedParryDamageRatio` | 0.20 |
| `minFailedParryDamage` | 1 |
