# CL-107 Wave B — 전투 hook 4곳 + 유물 6종 wiring

## Context

CL-106 Wave A 가 RelicData 확장 + StatModifierContainer + asset 채우기까지 마쳤지만, *효과 적용은 0* 인 상태 (의도된 무가시 인프라). CL-107 의 목적은 **MVP 6 종 유물의 효과를 실제로 발현** 시키는 것.

대상 6 종:

| 유물 | EffectType | Magnitude | 트리거 |
|---|---|---|---|
| 전사의 끈 | AttackPowerPercent | +0.05 | 영구 |
| 분쇄의 팔찌 | FinisherDamagePercent | +0.20 | 영구 (3타에만 적용) |
| 전투 북 | AttackPowerConditional | +0.10 | HP ≥ 50% 조건부 |
| 붉은 송곳니 | AttackSpeedOnKillTimed | +0.12 / 3s | 적 처치 시 3초 |
| 바람 깃털 | MoveSpeedPercent | +0.08 | 영구 |
| 수호의 파편 | MaxHealthPercent | +0.12 | 등록 시 1회 |

**전투 hook 4곳 (제목의 "4곳"의 정의):**
1. **Damage** (KhiMeleeComboController L201) — AttackPower / FinisherDamage / AttackPowerConditional 적용
2. **AttackSpeed** (KhiMeleeComboController RunAttack 진입부) — step durations 단축
3. **MoveSpeed** (신규 Applier, LateUpdate) — TDE CharacterMovement.MovementSpeedMultiplier 갱신
4. **MaxHealth** (신규 Applier, ModifiersChanged 구독) — TDE Health.MaximumHealth 재계산

---

## 결정사항

1. **유물 → 효과 적용 어댑터** = `PlayerRelicInventory.OnRelicAcquired` 이벤트 + 별도 `RelicEffectApplier` 컴포넌트 가 구독. (RewardPanelView 직접 wiring 은 View 책임 분리 위반이라 기각)
2. **TDE 원본 보호** — `CharacterMovement` / `Health` 직접 수정 금지. 어댑터 컴포넌트 (`PlayerMovementStatApplier`, `PlayerHealthStatApplier`) 신설로 multiplier 만 갱신
3. **MaxHealth 정책** = 등록 시 1회 적용 + **현재 HP 도 같은 비율 증가** (예: MaxHP 100→112 일 때 CurrentHP 100→112). 정책 미정이었으나 "수호의 파편" 의도에 부합. CL-108 회복약 hook 과도 일관됨
4. **OnEnemyKilled 이벤트** = `KhiMeleeComboController` 에 신규 `event EnemyKilledByPlayer` 추가. TargetHit 이벤트 발화 직후 `health.CurrentHealth <= 0` 체크하여 발화. (TDE Health.OnDeath 직접 구독은 *플레이어가 죽인 건지* 구분 못해서 부적절)
5. **AttackSpeed 적용 방식** = `AttackStepData` 는 ScriptableObject 일 가능성 높아 **in-place 변경 절대 금지**. RunAttack 진입부에서 startupDuration / activeDuration / recoveryDuration 을 *local 변수* 로 캡처 후 `1/multiplier` 적용. 이후 코드의 `step.startupDuration` 등 참조를 모두 local 변수로 치환
6. **AttackSpeed multiplier 의미** = "공격 빈도 +12%" → duration ÷ 1.12 ≈ 0.893× (단축). plan doc 에 명시
7. **AttackPower 곱셈 위치** = `KhiMeleeComboController.cs:201` 의 `weaponData.BaseDamage * step.damageMultiplier` 에 추가 multiplier 곱셈
8. **FinisherDamage 위치** = L201 직전에 `step.comboStep == 3` 체크 (기존 FinisherHit 이벤트는 사후 통지라 데미지 적용엔 부적절)
9. **CL-109 마이그레이션** — `RelicEffectApplier` 는 *임시 구현*. CL-109 의 RelicEffectRegistry 통합 시 흡수/삭제. `IRelicEffectAuthority` 분리도 CL-109

---

## 핵심 파일

### 신규 (3 파일 + 1 인벤토리 이벤트)

| 파일 | 역할 |
|---|---|
| `Relics/RelicEffectApplier.cs` | `OnRelicAcquired` 구독 → switch (EffectType) → container.Add* |
| `Combat/PlayerMovementStatApplier.cs` | LateUpdate 마다 `CharacterMovement.MovementSpeedMultiplier = container.GetTotalMultiplier(MoveSpeed)` |
| `Combat/PlayerHealthStatApplier.cs` | `ModifiersChanged` 구독 → `Health.MaximumHealth` 재계산 + CurrentHealth 비례 증가 |

### 수정

| 파일 | 변경 |
|---|---|
| [LostMemory/Assets/_Project/Scripts/Runtime/Relics/PlayerRelicInventory.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Relics/PlayerRelicInventory.cs) | `event Action<RelicData> OnRelicAcquired` 추가, TryAdd 성공 시 (L47 직전) 발화 |
| [LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeComboController.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeComboController.cs) | `[SerializeField] PlayerStatModifierContainer statContainer` 필드 + Health 참조 + `event EnemyKilledByPlayer` + L156 직후 AttackSpeed local 변수 + L201 데미지 multiplier + L206 직후 사망 체크 |

### 참조 (수정 없음, 이미 완성)

- `Relics/RelicData.cs` (CL-106 에서 4 필드 + 접근자 추가됨)
- `Relics/RelicEffectType.cs` (CL-106)
- `Combat/StatId.cs` (CL-106)
- `Combat/PlayerStatModifierContainer.cs` (CL-106) — API 시그니처:
  - `AddPermanent(StatId, float, object)`
  - `AddTimed(StatId, float, float, object)`
  - `AddConditional(StatId, float, Func<bool>, object)`
  - `RemoveBySource(object)`
  - `GetTotalMultiplier(StatId) → float`
  - `event Action<StatId> ModifiersChanged`

---

## 작업 단계 (구현 순서)

### Step 1 — Inventory 이벤트 추가 (5분)
`PlayerRelicInventory.cs`:
```csharp
public event Action<RelicData> OnRelicAcquired;
// ...
_ownedRelics.Add(relic);
OnRelicAcquired?.Invoke(relic);  // L45 직후
```

### Step 2 — RelicEffectApplier 신설 (30분)
신규 컴포넌트. Player root 에 부착. `OnEnable` 에서 inventory.OnRelicAcquired 구독.

```csharp
private void HandleAcquired(RelicData relic)
{
    switch (relic.EffectType)
    {
        case RelicEffectType.AttackPowerPercent:
            container.AddPermanent(StatId.AttackPower, relic.Magnitude, relic);
            break;
        case RelicEffectType.FinisherDamagePercent:
            container.AddPermanent(StatId.FinisherDamage, relic.Magnitude, relic);
            break;
        case RelicEffectType.AttackPowerConditional:
            container.AddConditional(
                StatId.AttackPower, relic.Magnitude,
                () => playerHealth != null && playerHealth.MaximumHealth > 0
                      && playerHealth.CurrentHealth / playerHealth.MaximumHealth >= relic.Threshold,
                relic);
            break;
        case RelicEffectType.MoveSpeedPercent:
            container.AddPermanent(StatId.MoveSpeed, relic.Magnitude, relic);
            break;
        case RelicEffectType.MaxHealthPercent:
            container.AddPermanent(StatId.MaxHealth, relic.Magnitude, relic);
            break;
        case RelicEffectType.AttackSpeedOnKillTimed:
            // EnemyKilledByPlayer 이벤트 구독 (Step 5 에서 추가)
            // 등록 시 nothing → 처치 이벤트 시 AddTimed
            _onKillSubscriptions.Add((relic.Magnitude, relic.Duration, relic));
            break;
        // 그 외 6 종 (CL-108, CL-109 분담) 은 case 만 비워둠 (None 포함)
    }
}
```

### Step 3 — Damage hook (KhiMeleeComboController L201) (15분)
L201 줄 교체:
```csharp
float attackMul = statContainer != null ? statContainer.GetTotalMultiplier(StatId.AttackPower) : 1f;
float finisherMul = (statContainer != null && step.comboStep == 3)
    ? statContainer.GetTotalMultiplier(StatId.FinisherDamage) : 1f;
float finalDamage = weaponData.BaseDamage * step.damageMultiplier * attackMul * finisherMul;
int sampledHitCount = hitbox != null
    ? hitbox.Sample(sampleRequest, step, finalDamage, _alreadyHitThisSwing, _hitsThisSample)
    : 0;
```

### Step 4 — AttackSpeed hook (L156 직후) (10분)
RunAttack 코루틴 진입부:
```csharp
AttackStepData step = GetStep(comboStep);
_currentComboStep = step.comboStep;

float speedMul = statContainer != null ? statContainer.GetTotalMultiplier(StatId.AttackSpeed) : 1f;
float startupDur  = step.startupDuration  / speedMul;
float activeDur   = step.activeDuration   / speedMul;
float recoveryDur = step.recoveryDuration / speedMul;
```

이후 `step.startupDuration` / `step.activeDuration` / `step.recoveryDuration` 의 모든 사용처 (L178, L182, L192, L231) 를 local 변수로 교체.

### Step 5 — EnemyKilledByPlayer 이벤트 (KhiMeleeComboController) (15분)
- 신규 `public event Action<KhiAttackRequest, AttackStepData, Health> EnemyKilledByPlayer;`
- L206 의 TargetHit 발화 직후:
```csharp
TargetHit?.Invoke(sampleRequest, step, _hitsThisSample[i]);
if (_hitsThisSample[i] != null && _hitsThisSample[i].CurrentHealth <= 0)
{
    EnemyKilledByPlayer?.Invoke(sampleRequest, step, _hitsThisSample[i]);
}
```
- RelicEffectApplier 가 EnemyKilledByPlayer 구독 → `_onKillSubscriptions` 순회하며 `container.AddTimed(StatId.AttackSpeed, mag, dur, source)`

### Step 6 — PlayerMovementStatApplier (10분)
신규 컴포넌트. Player root 부착.
```csharp
private void LateUpdate()
{
    if (characterMovement == null || statContainer == null) return;
    characterMovement.MovementSpeedMultiplier = statContainer.GetTotalMultiplier(StatId.MoveSpeed);
}
```

### Step 7 — PlayerHealthStatApplier (15분)
신규 컴포넌트. ModifiersChanged 이벤트 구독.
```csharp
private float _lastAppliedMaxHealth;  // base
private float _lastAppliedMul = 1f;

private void OnEnable()
{
    _lastAppliedMaxHealth = health.MaximumHealth;  // base 캡처
    container.ModifiersChanged += HandleChanged;
}

private void HandleChanged(StatId stat)
{
    float newMul = container.GetTotalMultiplier(StatId.MaxHealth);
    if (Mathf.Approximately(newMul, _lastAppliedMul)) return;

    float oldMax = health.MaximumHealth;
    float ratio = health.CurrentHealth / Mathf.Max(oldMax, 0.0001f);
    health.MaximumHealth = _lastAppliedMaxHealth * newMul;
    health.SetHealth(health.MaximumHealth * ratio);  // 비례 증가
    _lastAppliedMul = newMul;
}
```

### Step 8 — Player prefab 부착 + Inspector wiring (10분)
`TestKhi_MinimalCharacter2D` 에:
- PlayerStatModifierContainer (CL-106 의 Wave A 검증 단계에서 이미 부착했어야 함, 미부착이면 추가)
- RelicEffectApplier
- PlayerMovementStatApplier
- PlayerHealthStatApplier
- KhiMeleeComboController 의 `statContainer` 필드에 같은 GameObject 의 container 드래그
- 각 Applier 의 container, characterMovement, health, inventory 참조 드래그

---

## 검증 (PlayMode, 7 시나리오)

### 사전 준비
- 디버그 진입점: `PlayerRelicInventory` 에 ContextMenu 추가하여 RelicData asset 1 개 즉시 TryAdd. 또는 임시 디버그 UI
- `PlayerStatModifierContainer` 의 `Log Modifier Changes` ✅
- 기준 적: 1 hit 으로 안 죽도록 HP 충분한 적 1 마리 (예: 근접형 적)

### 시나리오 1 — 전사의 끈 (영구 +5% AttackPower)
1. `RelicData_전사의끈` TryAdd
2. **기대 로그**: `[StatModifier] AddPermanent AttackPower +5.0% src=RelicData_전사의끈`
3. 검 1타 공격
4. **기대 데미지**: base × stepMul × **1.05**

### 시나리오 2 — 분쇄의 팔찌 (3타 +20% Finisher)
1. `RelicData_분쇄의팔찌` TryAdd
2. 1타→2타→3타 콤보 완성
3. **기대**: 1타/2타 데미지 = base × stepMul × 1.0 (Finisher 미적용), **3타 데미지 = base × stepMul × 1.2**

### 시나리오 3 — 전투 북 (조건부 +10%)
1. `RelicData_전투북` TryAdd
2. HP 만피 상태 → 공격 → 데미지 × **1.10**
3. 적의 공격으로 HP < 50% 으로 감소 → 공격 → 데미지 × 1.0 (조건 불만족)
4. 회복 후 다시 HP ≥ 50% → 데미지 × 1.10 복귀 (조건부 modifier 의 동적 평가 검증)

### 시나리오 4 — 붉은 송곳니 (적 처치 시 3초)
1. `RelicData_붉은송곳니` TryAdd
2. HP 1 인 적 공격해서 처치
3. **기대 로그**: `[StatModifier] AddTimed AttackSpeed +12.0% for 3s src=RelicData_붉은송곳니`
4. 즉시 다음 콤보 → step durations 단축 (체감)
5. Inspector 의 PlayerStatModifierContainer 의 디버그 메뉴 "Log all totals" → **AttackSpeed = 1.120**
6. 3초 후 자동 만료 → AttackSpeed = 1.000

### 시나리오 5 — 바람 깃털 (영구 +8% MoveSpeed)
1. `RelicData_바람깃털` TryAdd
2. 이동 → CharacterMovement Inspector 의 `MovementSpeedMultiplier = 1.08` 확인
3. 체감: 약간 더 빨라짐

### 시나리오 6 — 수호의 파편 (MaxHealth +12%)
1. 시작 시 Health.MaximumHealth = 100 (예시), CurrentHealth = 100
2. `RelicData_수호의파편` TryAdd
3. **기대**: MaximumHealth = **112**, CurrentHealth = **112** (비례 증가)
4. 또는 시작 시 CurrentHealth = 50 이면 → MaxHealth 112 / Current = 56 (비율 50% 유지)

### 시나리오 7 — 6 종 동시 보유 (회귀)
1. 6 종 모두 TryAdd
2. 디버그 메뉴 "Log all totals":
   - **AttackPower = 1.05** (영구만, HP 만피 시 1.15)
   - **FinisherDamage = 1.20**
   - **AttackSpeed = 1.000** (적 처치 후 일시 1.120)
   - **MoveSpeed = 1.08**
   - **MaxHealth = 1.12**
3. 적 공격해서 처치 → AttackSpeed 일시 1.120 / 3 초 후 1.000

---

## 완료 기준

- [ ] Unity 컴파일 통과 (Console 빨간 에러 없음)
- [ ] 신규 3 파일 + 인벤토리 이벤트 추가 + KhiMeleeComboController 수정
- [ ] Player prefab 의 4 컴포넌트 (Container + Applier 3 개) 부착 + Inspector wiring 완료
- [ ] 시나리오 1 ~ 7 모두 통과
- [ ] 디버그 로그로 modifier 등록·만료·조회 확인 (`logModifierChanges = true`)

---

## 위험 / 결정 보류

1. **MaxHealth 정책 (비례 vs 절대)** — 본 plan 은 *비례 증가*. 기획에서 "MaxHP 만 증가, CurrentHP 유지" 로 확정되면 Step 7 의 SetHealth 호출 제거
2. **AttackStepData SO 여부** — 코드만으로는 모호. **반드시 in-place 변경 금지**. local 변수로 처리. 실제로 SO 면 Step 4 의 패턴이 정답
3. **AttackSpeed multiplier 방향** — "더 빠른 공격" → duration ÷ multiplier. 헷갈리면 "총 공격 횟수가 multiplier 배" 로 이해
4. **EnemyKilledByPlayer 정확성** — TargetHit 직후 CurrentHealth ≤ 0 체크는 "이번 hit 으로 죽은 적" 판별에 충분. 단, 다른 시스템이 DOT 로 동시에 죽이는 경우 false-positive 가능. MVP 단계에서 무시
5. **CL-109 마이그레이션 비용** — RelicEffectApplier 가 흡수될 때 switch 문은 RelicEffectRegistry 로 그대로 이전 가능. inventory 이벤트 구독부만 변경
6. **Multiplayer 권위** — 본 CL 은 *순수 로컬*. CL-109 에서 `IRelicEffectAuthority` 분기 도입

---

## 후속 CL 인터페이스

| CL | 추가될 case (RelicEffectApplier → CL-109 RelicEffectRegistry) |
|---|---|
| **CL-108** (Shield/Healing/Dash + 4종) | ShieldOnParry, HealReceivedPercent, DashCooldownPercent, MoveSpeedAfterDashTimed, HealConsumablePercent (회복약) |
| **CL-109** | RelicEffectApplier → RelicEffectRegistry 흡수 + `IRelicEffectAuthority` 분리 + 10 종 통합 검증 |
| **CL-110** | 보상 UI ↔ 방 클리어 연동 (RewardController 신설). 본 CL 과 독립. PlayerRelicInventory.OnRelicAcquired 이벤트는 CL-110 의 *RewardController 가 RewardPanel 닫고 다음 방 진행* 흐름과도 자연스럽게 연동

---

## 18 → 16 asset 이슈 (CL-106 후속, 본 CL 과 무관)

본 CL 은 6 종만 다루므로 영향 없음. 그러나 cl106_a_plan.md 의 18 개 표가 ticket 의 16 개와 불일치 — 어느 2 개를 제외하는지 ticket 원문 (Jira) 에서 확인 후 cl106_a_plan.md 의 표 수정 필요. 가능 후보: 랜덤박스 + 비-MVP 1 개 (가장 자연), 또는 회복약 큰/작은 중 1 개.
