# CL121 — Bertha 무적/피격 구조 점검

## Context
외부 피드백: "공격자가 피격자의 무적 시간을 부여하는 TDE 기본 패턴이 보스전에서 문제가 될 수 있으니, 플레이어→보스에는 무적을 0으로 두고 멀티히트는 `alreadyHit` 패턴으로 막자. 페이즈 전환은 별도 컨트롤러로." 피드백 작성자 본인도 "현재 넣은 Bertha 일반 피격 후 다음 프레임 무적 해제는 안전장치"라고 표현.

본 플랜은 ① 피드백의 권장 구조가 실제로 현재 코드/프리팹에 적용되어 있는지 진단하고 ② 그 결과에 따라 추가 변경이 필요한지 정한다.

## 진단 결과 (.prefab YAML grep)

**플레이어 측 `KhiMeleeHitbox`**
- `LostMemory/Assets/_Project/Prefabs/Characters/TestKhi_Net_AD.prefab:680` → `targetInvincibilityDuration: 0`
- `LostMemory/Assets/_Project/Prefabs/Characters/TestKhi_MinimalCharacter2D.prefab:686` → `targetInvincibilityDuration: 0`
- 코드 기본값: `KhiMeleeHitbox.cs:13` = `0f`
- **코드와 프리팹 모두 이미 0**

**보스 측 (`BerthaRoot.prefab`, `BerthaRoot2.prefab` 의 hitbox들)**
- BerthaLightAttack1: 0.5
- BerthaLightAttack2: 0.5
- BerthaHeavyAttack: 0.5
- BerthaFullCombo: 0.5
- BerthaDashAttack: 0.5
- 모두 일관, 의도대로 보스→플레이어 0.5초 무적 부여

**그 외 일반 적**
- StoneGolem_Test 1.0, Moose1_Test 1.0, Chobomb_CL212 0.45 — 별도 검토 영역, 본 작업 범위 밖

## 피드백 vs 실제 상태

| 피드백 권장 | 실제 상태 |
|---|---|
| 보스→플레이어 0.3~0.5초 유지 | ✅ 모든 Bertha 공격 0.5 |
| 플레이어→보스 무적 0 또는 매우 짧게 | ✅ **이미 0**. 피드백 작성자가 오진 |
| 페이즈 전환은 별도 컨트롤러 | ✅ `BerthaHealthThresholdReactionController`로 분리 |
| 멀티히트는 `alreadyHit`로 차단 | ✅ `KhiMeleeComboController._alreadyHitThisSwing` 존재 |

피드백이 권장한 구조는 **이미 구현되어 있음**. 별도 값 변경이 필요 없음.

## Step A 재조사 결과 (read-only)

### A-1. KhiParryDamageOnTouch 부착 위치/값
- `KhiParryDamageOnTouch.cs.meta` GUID = `c86c15c21b727704890303ac3febbda4`
- `Assets/_Project/Prefabs` 하위 grep 결과: **부착된 프리팹 0건**
- 즉 현재 씬/프리팹에서 인스턴스 없음. 보스 데미지 경로와 무관

### A-2. Layer mask + 자기-피격 가능성
- `TagManager.asset`: layer 10 = **Player**, layer 13 = **Enemies**
- 모든 Bertha 공격 hitbox `targetLayerMask.m_Bits: 1024` (= 2^10) → **Player layer만 타격**
- 보스 자기 자신은 Enemies layer라 Bertha 공격에 절대 안 맞음. **자기-피격 가능성 0** (후보 1 배제)

### A-3. 보스에게 데미지를 줄 수 있는 경로 전수조사
플레이어 측 `health.Damage()` 호출 지점:
- `KhiMeleeHitbox.cs:83` — `targetInvincibilityDuration` 변수 전달, 코드 기본값 0, 프리팹 값 0 (이미 확인)
- `KhiParryDamageOnTouch.cs:140` — 적 → 플레이어 방향, 보스에게 영향 없음
- `TestKhiDamageTrap.cs:98` — `0f, 0f` 하드코딩 (트랩, 무적 0)

보스 측 `DamageOnTouch` 상속 컴포넌트:
- `BerthaDashHitGate.cs` (BerthaRoot.prefab, BerthaRoot2.prefab 부착)
  - `InvincibilityDuration: 0.5` (플레이어 측 무적, 정상)
  - `DamageTakenInvincibilityDuration: 0.5`이지만 `DamageTakenDamageable: 0` + `DamageTakenEveryTime: 0` → `SelfDamage()` 트리거 안 됨
  - **보스 자기 무적 부여 없음**

`health.Invulnerable` 토글하는 모든 위치 (Bertha 전체):
- `BerthaHealthThresholdReactionController.cs:531,532,543,554` — 페이즈 전환 무적 (정상)
- `BerthaHitReactionPresenter.cs:172` — 안전장치 해제 (검토 대상)
- 그 외 위치 없음

### A-4. 안전장치 도입 commit 분석
- `git log -S "QueueNonReactionInvulnerabilityClear"` → 도입 commit `6d2e41048 [feat] 보스 피격 판정 개선 및 타격 범위 변경`, 후속 수정 `e34407da0`
- 6d2e41048 동일 commit에 들어온 변경들:
  - `BerthaAreaAttackController`, `BerthaBossProjectile`, `BerthaDashAttackController`의 `OverlapBox*NonAlloc` → `Physics2D.OverlapCircle(ContactFilter2D)` 로 전환 (보스 공격 hit detection 개선)
  - `BerthaHealthThresholdReactionController` 에 `IsReactionInvulnerabilityActive` 추가
  - `BerthaHitReactionPresenter` 에 `thresholdReactionController` 참조 + 안전장치 추가
- commit 메시지에 안전장치의 진짜 트리거 원인 명시 없음. 페이즈 무적 통합/정비와 묶여 들어옴

### 종합
- 피드백 작성자가 진단한 "플레이어 공격 0.5초 무적이 보스에 적용된다" 가설은 **모든 경로에서 확인되지 않음**
- 보스 자기-피격, 패링 무적 누수, 다른 트랩, 차지샷 등 후보 모두 배제 또는 영향 없음
- 안전장치의 진짜 트리거는 commit history만으로는 특정 불가. 도입 시점엔 필요했을 수 있으나 **현재 코드/프리팹 상태로는 동작에 영향을 미치지 않을 확률이 높음**

## 권장 행동

### Step B-1. 안전장치 비활성화 시험 (회귀 검증)
- `BerthaHitReactionPresenter.cs:31` 의 `ClearNonReactionInvulnerabilityAfterHit` 를 일시적으로 `false` 로 변경
- 검증 시나리오 5건(아래 검증 섹션) 모두 통과하면 안전장치 불필요 입증
- 회귀가 발생하면 즉시 `true` 복구 + 회귀가 일어난 정확한 경로를 새 단서로 조사

### Step B-2. 결과별 후속 조치
- **회귀 없음** → 안전장치 코드 제거 또는 플래그 `false` 고정 + 코드 주석 정리
- **회귀 발생** → 안전장치 유지하되 주석으로 "원인 미특정 보험 — 회귀 재현 시나리오: <시나리오>" 명시

### Step C. 변경하지 않을 것 (확정)
- `KhiMeleeHitbox` 코드/프리팹 값 (이미 0)
- 모든 Bertha 공격 hitbox `targetInvincibilityDuration: 0.5` (의도대로)
- `BerthaHealthThresholdReactionController` 페이즈 전환 무적 로직 (정상)
- 데미지 계약 자체 (TDE 공격자→피격자 무적 부여 패턴 유지)

## 수정 대상 파일
| 파일 | 변경 |
|---|---|
| `BerthaHitReactionPresenter.cs:31` | `ClearNonReactionInvulnerabilityAfterHit` 플래그 토글 (시험 → 결정) |

## 재사용 가능한 기존 자산
- `KhiMeleeComboController._alreadyHitThisSwing` — swing 단위 멀티히트 차단
- `BerthaHealthThresholdReactionController.IsReactionInvulnerabilityActive` — 페이즈 vs 일반 무적 가드
- `BerthaHitReactionPresenter.QueueNonReactionInvulnerabilityClear()` — 백업 안전장치 (Step B 결과에 따라 거취 결정)

## 검증 (Step B-1 회귀 시험)
1. Bertha 1페이즈에서 플레이어 콤보 3히트가 모두 데미지 적용되는지
2. Bertha 페이즈 전환 컷신 중 콤보가 데미지를 주지 못하는지 (페이즈 무적 검증)
3. 페이즈 임계 HP를 한 swing 안에 통과시키는 시나리오 — 페이즈 무적이 정상 발동
4. 보스 처치(체력 0) 직전 콤보 후속 타격이 사망 후 발동되지 않는지 — `CurrentHealth <= 0` 가드 검증
5. 보스가 빠른 연속 피격을 받을 때 무적이 누적되어 막히지 않는지
6. (회귀 발생 시) 어느 시나리오에서 막혔는지를 안전장치 주석에 기록
