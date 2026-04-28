# CL-106: 유물(Relic) 효과 적용 시스템 구현 계획

소속 Epic: **Epic M. 유물 효과 / 빌드 시스템**

## 백로그 재배치

기존 CL-105 ~ CL-111을 뒤로 시프트하고 CL-105·CL-106에 새 항목을 끼워 넣는다.

| 새 ID | 새 명칭 | 비고 |
|---|---|---|
| CL-105 | Dungeon Architecture와 기존 방 생성 연동 | 신규 (별도 plan 필요) |
| CL-106 | 아이템(유물) 효과 적용 시스템 구현 | **본 plan**, Epic M |
| CL-107 | (구) CL-105 — 로그인 토큰 저장 및 세션 유지 최소 흐름 | 기존 항목 시프트 |
| CL-108 | (구) CL-106 — 진행 저장 요청 모델 정리 및 API 연동 | 기존 항목 시프트 |
| CL-109 | (구) CL-107 — 런 결과 저장 요청 모델 정리 및 API 연동 | 기존 항목 시프트 |
| CL-110 | (구) CL-108 — 저장 실패 재시도 및 안전 종료 UX | 기존 항목 시프트 |
| CL-111 | (구) CL-109 — 파티 스토리 진행도 비교 및 결정 로직 | 기존 항목 시프트 |
| CL-112 | (구) CL-110 — 파티 최저 진행도 기준 스토리 동기화 | 기존 항목 시프트 |
| CL-113 | (구) CL-111 — 파티 기준 스토리 상태 안내 UI | 기존 항목 시프트 |

후속 작업:
1. `docs/14_client_jira_story_backlog.md` 직접 수정해 위 표 반영 (별도 작업)
2. 진행 중 Jira 티켓이 있다면 ID 충돌 확인
3. CL-105 (DA·방 생성 연동)는 별도 plan 작업

## Epic M. 유물 효과 / 빌드 시스템

신규 epic. 기존 docs/14 epic 목록(A~L)에 이어 추가.

**범위**:
- 유물 효과 데이터 모델 및 적용 시스템 (CL-106 본 작업)
- 향후 유물 확장(15~20종)
- 광전사의 문장(중첩)·성벽의 궤(궤도 방패) 같은 복잡 효과
- 태그 세트 효과(맹공·수호·질주의 2세트/4세트)
- 빌드 시너지 디버그·시각화 도구

**경계**:
- 보상 추첨/3택 UI/소비형 분기는 기존 Epic D(런 진행)에 유지
- 본 epic은 "획득 후 어떻게 캐릭터에 작용하는가"의 시스템

## Context

`docs/05_content_scope.md`에 정의된 MVP 1차 유물 10종을 실제로 게임플레이에 적용한다. 현재 데이터/UI/풀/인벤토리 인프라는 모두 완성돼 있으나(`RelicData` SO 16개, `RewardPool` 가중치 추첨, `RewardPanelView` 3택 UI, `PlayerRelicInventory`, `RunResultPanel`), **유물의 효과를 실제 전투/이동/체력에 적용하는 런타임 로직만 비어있는 상태**다 — `RelicData`에 `EffectDescription` 문자열만 있고 효과 파라미터·실행 코드는 전무.

이 작업의 목표는 "보상 카드를 골라도 캐릭터에 변화가 없는 상태"를 해소하고, MVP 10종이 모두 체감되도록 하는 것이다. 작업 범위는 솔로 플레이 기준이며, 향후 NGO 멀티플레이 도입 시 서버 권한으로 옮겨야 할 부분은 `IRelicEffectAuthority` 인터페이스로 격리만 해둔다(구현은 솔로용 `LocalAuthority`만).

---

## MVP 적용 대상 유물 10종 (출처: `docs/05_content_scope.md`)

| 이름 | 등급 | 태그 | 효과 |
|---|---|---|---|
| 전사의 끈 | 일반 | 맹공 | 공격력 +5% |
| 붉은 송곳니 | 일반 | 맹공 | 적 처치 시 3초간 공격속도 +12% |
| 분쇄의 팔찌 | 레어 | 맹공 | 3타 마무리 피해 +20% |
| 전투 북 | 레어 | 맹공 | 체력 50% 이상일 때 공격력 +10% |
| 수호의 파편 | 일반 | 수호 | 최대 체력 +12% |
| 반격의 표식 | 일반 | 수호 | 패링 성공 시 최대체력 8% 보호막, 3초 |
| 철의 깃 | 레어 | 수호 | 회복량 +25% |
| 바람 깃털 | 일반 | 질주 | 이동속도 +8% |
| 질풍 장화 | 일반 | 질주 | 대시 쿨타임 -12% |
| 추적자의 망토 | 레어 | 질주 | 대시 후 2초간 이동속도 +20% |

---

## 추천 아키텍처 (요약)

원칙: **과도한 추상화 금지**. 인터페이스는 멀티 분기점에만. 작게 시작.

### 데이터 모델 — 하이브리드 (enum + 다목적 수치 3개)

`RelicData`에 다음 필드 추가:
- `RelicEffectType EffectType` — 10종 효과를 enum으로 분류
- `float Magnitude` — 비율 또는 양 (예 0.05 = 5%)
- `float Duration` — 임시 효과 지속시간 (없으면 0)
- `float Threshold` — 조건부 임계치 (전투 북의 0.5 등)

비채택안: 폴리모픽 SO 서브클래스(자산 폭발), 효과별 별도 필드(필드 수 비대).

### 런타임 컴포넌트 — 4개

1. **`PlayerStatModifierContainer`** — 영구/임시/조건부 +% 보유. `GetTotalMultiplier(StatId)` 제공. `StatId`: AttackPower, AttackSpeed, MoveSpeed, MaxHealth, FinisherDamage, DashCooldown, HealReceived.
2. **`PlayerCombatEvents`** — `KhiParryController.ParrySucceeded`, 적 `Health.OnDeath`, 대시 종료 시점을 한곳으로 라우팅.
3. **`PlayerShield`** — 보호막 누적/만료/흡수. Health 수정하지 않고 `KhiParryDamageOnTouch` 흡수 단계 추가.
4. **`RelicEffectRegistry`** — `PlayerRelicInventory.RelicAdded/RelicsCleared` 이벤트를 받아 위 3개에 등록/해제.

### 멀티 대비

`IRelicEffectAuthority` 인터페이스 1개만 분리(IsAuthority, RaiseEnemyKilled, GrantShield, ApplyMaxHpDelta). 이번엔 `LocalAuthority` 솔로용만 구현. `RoomEntryRuntimeController.IsAuthority => true` 패턴 차용.

---

## 유물 10종 Hook 매핑

| 유물 | 동작 종류 | Hook |
|---|---|---|
| 전사의 끈 | 영구 | `KhiMeleeHitbox.Sample` 직전 damage *= GetMul(AttackPower) |
| 붉은 송곳니 | 임시 (3초) | `OnEnemyKilled` → `AddTimed(AttackSpeed, 0.12, 3)`. 적용은 `KhiMeleeComboController` step duration 분모 |
| 분쇄의 팔찌 | 영구(조건) | `KhiMeleeComboController.cs:201` 데미지 식에서 `step.comboStep == 3`일 때 GetMul(FinisherDamage) 곱 |
| 전투 북 | 조건부 | `AddConditional(AttackPower, 0.10, () => HpRatio >= Threshold)`, 매 공격 시점 평가 |
| 수호의 파편 | 영구 | 등록 시 1회 `health.MaximumHealth *= 1.12` + CurrentHealth 보정 |
| 반격의 표식 | 이벤트 트리거 | `OnParrySucceeded` → `PlayerShield.Grant(maxHp*0.08, 3)` |
| 철의 깃 | 영구 | `PlayerHealing.Heal()`에서 `amount *= GetMul(HealReceived)` |
| 바람 깃털 | 영구 | TDE `CharacterMovement.MovementSpeedMultiplier` 곱 |
| 질풍 장화 | 영구 | TDE `MMCooldown.ConsumedDuration` (CharacterDash2D) 곱하기 (1+퍼센트), 음수 |
| 추적자의 망토 | 임시 (2초) | `OnDashEnded` → `AddTimed(MoveSpeed, 0.20, 2)` |

---

## 선결 작업 (P0, 반드시 먼저)

1. **`PlayerHealing` 컴포넌트 신설** — 회복 발생점 자체가 없음. `Heal(baseAmount, source)` 메서드 + modifier 적용 + `health.GetHealth()` 호출. (철의 깃 검증용으로 회복약 소모품 1종 연결 권장)
2. **`OnDashEnded` 이벤트** — `KhiDashController`에 `wasDashing && !IsDashing` 폴링으로 발행. (추적자의 망토)
3. **통합 적 처치 이벤트** — `EnemyEncounterSpawner.Spawned` 구독 → 인스턴스별 `Health.OnDeath` 구독 → `PlayerCombatEvents.OnEnemyKilled` 발행. 신규 클래스 불필요. (붉은 송곳니)
4. **`PlayerShield` 신설** — `Grant(amount, duration)`, 만료 처리, `KhiParryDamageOnTouch`에서 Health 차감 직전 흡수. (반격의 표식)

P1 (선택): 효과 디버그 오버레이(`KhiPlayerStateDebugOverlay` 패턴 재사용).

---

## 단계별 구현 순서 (Wave 단위 PR 분할 가이드)

통합 plan이지만 PR 단위로 쪼개는 가이드를 함께 둔다. 각 Wave 종료 시점에 통합 빌드가 동작 가능해야 한다.

### Wave A — 데이터/기반 인프라 (Step 1-2)

| Step | 작업 | 검증 |
|---|---|---|
| 1 | `RelicData` 필드 확장 + `RelicEffectType` enum + 16 .asset 채움 | 데이터만, 인스펙터 검증 |
| 2 | `PlayerStatModifierContainer` + `StatId` enum 신설 | 단위 테스트 또는 디버그 로그 |

### Wave B — 전투 hook (Step 3-6)

| Step | 작업 | 검증 유물 |
|---|---|---|
| 3 | `KhiMeleeComboController`/`KhiMeleeHitbox` 데미지·타이밍 hook | 전사의 끈, 분쇄의 팔찌, 전투 북, AttackSpeed 부분 |
| 4 | TDE `CharacterMovement` MoveSpeed multiplier hook | 바람 깃털 |
| 5 | TDE `Health.MaximumHealth` 변경 hook + 즉시 회복 처리 | 수호의 파편 |
| 6 | `PlayerCombatEvents` + Spawner 결선 (OnEnemyKilled) | 붉은 송곳니 |

### Wave C — 대시/보호막/회복 (Step 7-9)

| Step | 작업 | 검증 유물 |
|---|---|---|
| 7 | `KhiDashController.DashEnded` + Cooldown multiplier hook | 질풍 장화, 추적자의 망토 |
| 8 | `PlayerShield` + `KhiParryDamageOnTouch` 흡수 hook | 반격의 표식 |
| 9 | `PlayerHealing` 신설 + 회복약 소모품 연결 | 철의 깃 |

### Wave D — 통합/멀티 대비 (Step 10-11)

| Step | 작업 | 검증 |
|---|---|---|
| 10 | `RelicEffectRegistry` + `PlayerRelicInventory` 이벤트 hook | 10종 통합 검증 |
| 11 | `IRelicEffectAuthority` 인터페이스 분리 (LocalAuthority 구현만) | 솔로 동작 확인, 멀티 분기점 식별만 |

Jira 등록 시 CL-106을 단일 스토리로 등록하되, sub-task로 Wave A~D를 두는 방식 권장. 일정 압박이 있을 경우 Wave D Step 11(인터페이스 분리)을 먼저 컷하고 NGO 도입 시 함께 진행 가능.

---

## 변경/신규 파일

### 변경

- [RelicData.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Relics/RelicData.cs) — enum + 3 수치 필드 추가
- [PlayerRelicInventory.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Relics/PlayerRelicInventory.cs) — `RelicAdded`, `RelicsCleared` 이벤트 발행
- [KhiMeleeComboController.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeComboController.cs) — step duration multiplier (AttackSpeed), damage multiplier (AttackPower, FinisherDamage)
- [KhiMeleeHitbox.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeHitbox.cs) — Damage 호출 인자에 modifier 적용
- [KhiDashController.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiDashController.cs) — `DashEnded` 이벤트, Cooldown multiplier
- [KhiParryDamageOnTouch.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiParryDamageOnTouch.cs) — Shield 흡수 단계 삽입
- 16개 `.asset` (`client/LostMemory/Assets/_Project/ScriptableObjects/Relics/`) — EffectType / Magnitude / Duration / Threshold 채우기

### 신규

- `PlayerStatModifierContainer.cs` (Combat/)
- `PlayerCombatEvents.cs` (Combat/)
- `PlayerShield.cs` (Combat/)
- `PlayerHealing.cs` (Combat/)
- `RelicEffectRegistry.cs` (Relics/)
- `IRelicEffectAuthority.cs` + `LocalAuthority.cs` (Relics/)

---

## Verification (E2E)

테스트 전용 씬에서 다음 시나리오 수동 확인:

1. **수치 검증** — 디버그 오버레이로 modifier 활성 상태 표시. 각 유물 1개씩 추가 후 plain 상태 대비 변화 확인.
2. **전사의 끈** — 추가 전후 1타 데미지 비교, 5% 차이 확인.
3. **분쇄의 팔찌** — 1·2타와 3타 데미지 비율 변화 확인.
4. **전투 북** — HP 51%일 때와 49%일 때 데미지 차이 확인.
5. **붉은 송곳니** — 적 처치 직후 3초간 콤보 step duration 단축 확인.
6. **수호의 파편** — MaxHP 100 → 112 확인, CurrentHealth 즉시 회복 확인.
7. **반격의 표식** — 패링 성공 후 다음 피격 시 보호막 흡수 확인.
8. **철의 깃** — 회복약 사용 시 +25%만큼 더 회복 확인.
9. **바람 깃털** — 이동 속도 8% 증가 확인.
10. **질풍 장화** — 대시 쿨타임 12% 단축 확인.
11. **추적자의 망토** — 대시 종료 후 2초간 이동속도 20% 증가 후 원복 확인.

각 유물은 RewardPanel을 통해 추가하는 정상 경로로 검증한다.

기능적 통합 시나리오: 전사의 끈 + 분쇄의 팔찌 + 전투 북을 동시에 들고 3타 명중 데미지 측정 (합연산 누적 확인).

---

## 확정된 결정 사항

- **공격속도 의미**: step duration(선딜·판정·후딜)만 분모 단축. 콤보 입력 윈도우는 유지. 매 step 시작 시점에 modifier 실시간 평가.
- **MaxHealth 증가 시 CurrentHealth**: 증가분만큼 즉시 회복 (40/100 → 52/112).
- **회복약 소모품 적용**: 이번 작업에 포함. `PlayerHealing` + 작은/큰 회복약 SO 연결까지 진행.
- **modifier 합산**: 합연산 `total = 1 + Σ(percents)`. 영구·임시·조건부 모두 동일 누적.

## 권장 디테일 (별도 이의 없으면 이대로 진행)

- **대시 종료 정의**: `_dashing` 플래그 false 전이 시점 (무적시간 종료 아님).
- **전투 북 임계 평가**: 매 공격 1회 평가. 프레임 캐시 불필요.
- **데미지 modifier 적용 위치**: `KhiMeleeComboController` 데미지 식. 분쇄의 팔찌 finisher 분기와 자연스러움.
- **Reward 카드 효과 설명**: enum + Magnitude로 자동 포맷("공격력 +5%") + RelicData에 `OverrideDescription` 옵션 필드(비어있으면 자동, 채우면 수동).
- **유물 stack 정책**: 이번엔 `PlayerRelicInventory` 중복 차단 유지. MVP 10종 모두 단일 인스턴스.
- **광전사의 문장·성벽의 궤 등 비-MVP 유물**: 이번엔 enum/시스템만 확장 가능 구조로 두고, 실제 구현은 후속 작업.
- **멀티 적용 시점**: 이번엔 `IRelicEffectAuthority` 인터페이스 분리만. NGO 도입은 별도 작업.
