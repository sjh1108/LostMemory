# CL-108 Wave C — PlayerShield + PlayerHealing 신설 + Dash 이벤트 + 유물 4종 + 회복약 2종

## Context

CL-107 (Wave B) 가 전투 hook 4 곳 + MVP 6 종 (영구/조건부/시간부/Finisher 패턴) wiring 을 마침. CL-108 의 목적은 **나머지 MVP 4 종 + 회복약 2 종** 의 효과를 발현시키는 것. 새 메커니즘 두 가지 (보호막 / 회복) 가 신설된다.

대상 4 종 + 소모품 2 종:

| 유물 | EffectType | Magnitude | Duration | 트리거 |
|---|---|---|---|---|
| 반격의 표식 | ShieldOnParry | +0.08 | 3s | 패링 성공 시 maxHp×8% 보호막 부여 |
| 철의 깃 | HealReceivedPercent | +0.25 | — | 모든 회복량 +25% (영구) |
| 질풍 장화 | DashCooldownPercent | -0.12 | — | 대시 cooldown × 0.88 (영구) |
| 추적자의 망토 | MoveSpeedAfterDashTimed | +0.20 | 2s | 대시 종료 후 2초간 이동속도 +20% |
| 작은 회복약 (소모품) | HealConsumablePercent | 0.25 | — | 즉시 maxHp×25% 회복 |
| 큰 회복약 (소모품) | HealConsumablePercent | 0.50 | — | 즉시 maxHp×50% 회복 |

**신설 컴포넌트 2개 + 이벤트 2개:**
1. `PlayerShield` — 보호막 잔량 관리, 만료 처리, 데미지 흡수 진입점
2. `PlayerHealing` — 회복 진입점 (HealReceived multiplier 적용 + 회복약 사용)
3. `KhiDashController.OnDashEnded` event 신설
4. `PlayerRelicInventory.TryUseOrAdd` API 신설 (소모품 분기)

---

## 결정사항

1. **패링 성공 이벤트** — `KhiParryController.ParrySucceeded` event (L59) 가 이미 존재. **신설 불필요**. RelicEffectApplier 가 구독만 하면 됨
2. **보호막 hook 위치** — `KhiParryDamageOnTouch.OnCollideWithDamageable` (L20) 이 Player 공격 진입점. 이 안에서 PlayerShield 가로채기. 두 경로 모두 적용:
   - **base.OnCollideWithDamageable 호출 경로 (L25, L34, L48)** — base 가 TDE 원본 호출이라 hook 불가 → KhiParryDamageOnTouch 에 *PlayerShield resolve* 추가하여 base 경로도 PlayerShield 거치도록 우회 (KhiParryController resolve 패턴과 동일)
   - **ApplyReducedDamage 경로 (L80-133)** — Health.Damage (L119) 호출 전에 PlayerShield 차감
3. **보호막 만료 처리** — PlayerShield 자체 `_shieldExpiresAt` + `Update()` 카운트다운. `PlayerStatModifierContainer.AddTimed` 는 *비율 multiplier* 용이라 보호막 잔량 (절대값) 과 의미 다름
4. **보호막 누적 정책** — 새 패링 성공 시 *기존 보호막 + 신규 보호막* 합산. 만료 시간은 *둘 중 더 늦은 것*. (또는 신규로 갱신만 — 정책 기획에 명시 필요)
5. **대시 cooldown multiplier** — `DashStart()` override 안에서 `base.DashStart()` 호출 직전에 `Cooldown.ConsumptionDuration = _baseCooldown * (1 + statContainer.GetTotalMultiplier(StatId.DashCooldown) - 1)` = `_baseCooldown * statContainer.GetTotalMultiplier(StatId.DashCooldown)`. 음수 magnitude (-0.12) → multiplier 0.88 → 단축
6. **대시 종료 이벤트** — `KhiDashController` 에 `public event Action OnDashEnded` 신설. TDE `CharacterDash2D` 는 `DashStop()` virtual 메서드 제공 (가정 — 미확인 시 매 frame `IsDashing` 변화 감지로 대체)
7. **회복 진입점 통합** — `PlayerHealing.Heal(baseAmount, source)` 가 *유일한* 회복 API. Health.ReceiveHealth 직접 호출은 부활 (CL-014) 외에는 금지. 향후 모든 회복 (시간 회복, 이벤트 회복) 도 이 API 거침
8. **부활 회복 마이그레이션** — CL-014 `KhiDownController.Revive` 의 `Health.SetHealth` 직접 호출을 `PlayerHealing.Heal` 로 변경. HealReceivedPercent 가 부활 회복에도 적용됨 ("철의 깃 + 부활 = +25% 보너스 회복")
9. **소모품 사용 트리거** — `PlayerRelicInventory.TryUseOrAdd(relic)` 신규 API. IsConsumable 분기에서 `PlayerHealing.UseConsumable(relic)` 호출. RewardPanelView 는 TryAdd 대신 TryUseOrAdd 호출. 향후 랜덤박스 등 다른 소모품도 동일 경로
10. **TDE 원본 보호** — `Health`, `CharacterDash2D`, `DamageOnTouch` 모두 직접 수정 금지. 어댑터 / override / 우회 패턴
11. **보호막 ↔ 패링 무적시간 충돌** — 패링 성공 시 `successfulParryInvulnerability = 0.12s` 동안 무적 (KhiParryController L264). 그 동안 보호막은 데미지 받을 일 없음. 무적 종료 후 보호막이 활성 상태로 남아 데미지 흡수 시작. **충돌 없음, plan 명시**

---

## 핵심 파일

### 신규 (2 컴포넌트)

| 파일 | 역할 |
|---|---|
| `Combat/PlayerShield.cs` | 보호막 잔량/만료 관리 + `TryAbsorb(damage) → remainingDamage` API |
| `Combat/PlayerHealing.cs` | `Heal(baseAmount, source)` + `UseConsumable(relic)` API. HealReceived multiplier 적용 |

### 수정

| 파일 | 변경 |
|---|---|
| [Relics/PlayerRelicInventory.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Relics/PlayerRelicInventory.cs) | `TryUseOrAdd(relic)` API 추가 + `event OnConsumableUsed` (PlayerHealing 참조 추가) |
| [Rewards/RewardPanelView.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Rewards/RewardPanelView.cs) | OnCardSelected (L43-47) 에서 TryAdd → TryUseOrAdd |
| [TestKhi/KhiDashController.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiDashController.cs) | `event OnDashEnded` + `_baseCooldown` 캐시 + DashStart() override 에서 cooldown multiplier 적용 + DashStop() override (또는 IsDashing 변화 감지) |
| [TestKhi/KhiParryDamageOnTouch.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiParryDamageOnTouch.cs) | base.OnCollideWithDamageable 호출 직전 PlayerShield 차감 hook (3 경로 모두) + ApplyReducedDamage L119 직전 PlayerShield 차감 |
| [TestKhi/KhiDownController.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiDownController.cs) | Revive 의 `Health.SetHealth` 호출을 `PlayerHealing.Heal` 로 마이그레이션 (HealReceivedPercent 적용 가능) |
| `Relics/RelicEffectApplier.cs` (CL-107 산출물) | 6 종 case 추가: ShieldOnParry, HealReceivedPercent, DashCooldownPercent, MoveSpeedAfterDashTimed, HealConsumablePercent (회복약 2 종) |

### 참조 (수정 없음)

- `KhiParryController.ParrySucceeded` event (L59) — RelicEffectApplier 가 구독
- `Health.ReceiveHealth(amount, instigator)` (TDE L1022) — PlayerHealing 이 호출
- `MMCooldown.ConsumptionDuration` (TDE) — KhiDashController 가 set

---

## 작업 단계 (구현 순서)

### Step 1 — PlayerShield 신설 (40분)
신규 컴포넌트 `Combat/PlayerShield.cs`:

```csharp
public sealed class PlayerShield : MonoBehaviour
{
    [SerializeField] private bool logShieldEvents = false;

    private float _currentShield;
    private float _expiresAt;

    public float CurrentShield => _currentShield;
    public bool IsActive => _currentShield > 0f && Time.time < _expiresAt;

    public event Action<float> ShieldChanged;  // (newAmount)

    /// <summary>
    /// 보호막 부여. 누적 정책: 잔량 합산, 만료 시간은 더 늦은 쪽.
    /// </summary>
    public void GrantShield(float amount, float duration)
    {
        _currentShield = (IsActive ? _currentShield : 0f) + amount;
        _expiresAt = Mathf.Max(IsActive ? _expiresAt : 0f, Time.time + duration);
        ShieldChanged?.Invoke(_currentShield);
        if (logShieldEvents)
            Debug.Log($"[PlayerShield] Grant {amount:F1} for {duration}s, total={_currentShield:F1}, expiresAt={_expiresAt:F2}");
    }

    /// <summary>
    /// 들어온 데미지를 보호막이 먼저 흡수. 남은 데미지 반환.
    /// </summary>
    public float TryAbsorb(float incomingDamage)
    {
        if (!IsActive || incomingDamage <= 0f) return incomingDamage;
        float absorbed = Mathf.Min(_currentShield, incomingDamage);
        _currentShield -= absorbed;
        ShieldChanged?.Invoke(_currentShield);
        if (logShieldEvents)
            Debug.Log($"[PlayerShield] Absorb {absorbed:F1}, remain={_currentShield:F1}, dmg passthrough={incomingDamage - absorbed:F1}");
        if (_currentShield <= 0f) ClearShield();
        return incomingDamage - absorbed;
    }

    private void Update()
    {
        if (_currentShield > 0f && Time.time >= _expiresAt) ClearShield();
    }

    private void ClearShield()
    {
        _currentShield = 0f;
        _expiresAt = 0f;
        ShieldChanged?.Invoke(0f);
        if (logShieldEvents) Debug.Log("[PlayerShield] Expired/Cleared");
    }
}
```

### Step 2 — PlayerHealing 신설 (25분)
신규 컴포넌트 `Combat/PlayerHealing.cs`:

```csharp
public sealed class PlayerHealing : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private PlayerStatModifierContainer container;
    [SerializeField] private bool logHeals = false;

    private void Awake()
    {
        health ??= GetComponent<Health>();
        container ??= GetComponent<PlayerStatModifierContainer>();
    }

    public void Heal(float baseAmount, object source)
    {
        if (health == null || baseAmount <= 0f) return;
        float mul = container != null ? container.GetTotalMultiplier(StatId.HealReceived) : 1f;
        float effective = baseAmount * mul;
        health.ReceiveHealth(effective, source as GameObject ?? gameObject);
        if (logHeals)
            Debug.Log($"[PlayerHealing] Heal base={baseAmount:F1} mul={mul:F3} → {effective:F1} src={source}");
    }

    public void UseConsumable(RelicData consumable)
    {
        if (consumable == null || !consumable.IsConsumable) return;
        if (consumable.EffectType != RelicEffectType.HealConsumablePercent) return;  // 향후 다른 소모품 분기
        float baseAmount = (health != null ? health.MaximumHealth : 0f) * consumable.Magnitude;
        Heal(baseAmount, consumable);
    }
}
```

### Step 3 — PlayerRelicInventory.TryUseOrAdd (10분)
```csharp
[SerializeField] private PlayerHealing playerHealing;
public event Action<RelicData> OnConsumableUsed;

public bool TryUseOrAdd(RelicData relic)
{
    if (relic == null) { Debug.LogWarning("[PlayerRelicInventory] relic is null"); return false; }
    if (relic.IsConsumable)
    {
        if (playerHealing != null) playerHealing.UseConsumable(relic);
        OnConsumableUsed?.Invoke(relic);
        Debug.Log($"[PlayerRelicInventory] 소모품 사용: {relic.DisplayName}");
        return true;
    }
    return TryAdd(relic);  // 기존 로직 재사용
}
```

`TryAdd` 의 IsConsumable 분기 (L31-36) 는 *디버그용 호출 호환성* 위해 그대로 유지 (false 반환).

### Step 4 — RewardPanelView 변경 (5분)
L45 `_inventory.TryAdd(selected)` → `_inventory.TryUseOrAdd(selected)`

### Step 5 — KhiDashController 확장 (30분)

```csharp
[SerializeField] private PlayerStatModifierContainer statContainer;
private float _baseCooldownDuration;

public event Action OnDashEnded;

protected override void Initialization()
{
    base.Initialization();
    aim ??= GetComponent<KhiPlayerAim>();
    meleeCombo ??= GetComponent<KhiMeleeComboController>();
    statContainer ??= GetComponent<PlayerStatModifierContainer>();
    DashMode = DashModes.Script;
    _baseCooldownDuration = Cooldown.ConsumptionDuration;  // base 캐시
}

public override void DashStart()
{
    DashMode = DashModes.Script;
    DashDirection = ResolveDashDirection();

    // 질풍 장화: cooldown × multiplier (음수 magnitude 면 단축)
    if (statContainer != null)
    {
        float mul = statContainer.GetTotalMultiplier(StatId.DashCooldown);
        Cooldown.ConsumptionDuration = _baseCooldownDuration * mul;
    }

    base.DashStart();
}

// TDE CharacterDash2D 가 DashStop() virtual 제공 시 override.
// 미제공 시 IsDashing 변화 감지 (LateUpdate)
private bool _wasDashingLastFrame;
private void LateUpdate()
{
    if (_wasDashingLastFrame && !IsDashing)
    {
        OnDashEnded?.Invoke();
    }
    _wasDashingLastFrame = IsDashing;
}
```

> **확인 필요**: TDE `CharacterDash2D` 의 `DashStop()` virtual 여부. virtual 이면 override 가 깔끔, 아니면 LateUpdate 폴링.

### Step 6 — KhiParryDamageOnTouch 보호막 hook (40분)

가장 복잡한 부분. `OnCollideWithDamageable` 의 모든 경로 (base 호출 3 회 + ApplyReducedDamage) 에 PlayerShield 차감 끼워넣기.

**전략**: base.OnCollideWithDamageable 을 직접 호출하지 않고 *수동 재현* 방식으로 변경. ApplyReducedDamage 와 동일하게 PlayerShield 차감 → Health.Damage 호출. 단 base 가 처리하던 KnockBack/Feedback 등 부수 효과도 같이 재현해야 함.

대안 (단순함): PlayerShield 가 *완전 소진되지 않은 한* 모든 데미지 흡수 시도. base 경로도 PlayerShield 차감 후 *base.OnCollideWithDamageable 호출 자체를 건너뜀* — 이 경우 보호막이 충분하면 KnockBack/Feedback 도 재생 안 됨. 기획상 보호막이 데미지뿐 아니라 피격 자체를 무효화한다면 OK.

```csharp
protected override void OnCollideWithDamageable(Health health)
{
    if (health == null) { base.OnCollideWithDamageable(health); return; }

    // PlayerShield 가로채기 (PlayerShield 컴포넌트가 health 와 같은 GO 또는 부모에 있다고 가정)
    PlayerShield shield = ResolvePlayerShield(health);
    KhiParryController parry = ResolveParryController(health);

    float randomDamage = Random.Range(MinDamageCaused, Mathf.Max(MaxDamageCaused, MinDamageCaused));
    float dmgAfterShield = shield != null ? shield.TryAbsorb(randomDamage) : randomDamage;

    if (dmgAfterShield <= 0f)
    {
        // 보호막이 완전 흡수. 피드백/이벤트 스킵 (정책)
        return;
    }

    // 패링 처리는 *보호막으로 안 막힌 데미지* 에 대해서만
    if (parry == null)
    {
        // 보호막 일부 흡수 후 base 경로. 단 base 는 randomDamage 를 다시 굴림.
        // 정합성 위해 base 호출 대신 ApplyReducedDamage(health, dmgAfterShield) 사용.
        ApplyReducedDamage(health, dmgAfterShield);
        return;
    }

    if (!parry.TryResolveIncomingDamage(gameObject, ..., dmgAfterShield, out float resolved))
    {
        ApplyReducedDamage(health, dmgAfterShield);  // 패링 무관
        return;
    }

    if (resolved <= 0f) return;  // 패링 성공
    ApplyReducedDamage(health, resolved);
}

private static PlayerShield ResolvePlayerShield(Health health) =>
    health.gameObject.GetComponent<PlayerShield>() ??
    health.gameObject.GetComponentInParent<PlayerShield>();
```

> **위험**: ApplyReducedDamage 가 base 의 모든 부수 효과를 재현하는지 ApplyReducedDamage 코드 자체를 다시 검토 필요 (현재 L80-133, base TDE 라인 634-676 의 동일 순서라고 주석에 명시). KnockBack, Feedback, SelfDamage 까지 재현 → OK 추정

### Step 7 — KhiDownController 부활 회복 마이그레이션 (10분)
`KhiDownController.cs` 의 `_health.SetHealth(reviveHp)` 호출 부분 (cl014_player_down_revive_implementation.md 참고, 줄 번호는 실제 코드에서 확인) 을:

```csharp
[SerializeField] private PlayerHealing playerHealing;
// ...
// _health.SetHealth(reviveHp);  // 변경 전
playerHealing.Heal(reviveHp, this);  // 변경 후 (HealReceivedPercent 적용)
```

### Step 8 — RelicEffectApplier 6 case 추가 (20분)
CL-107 의 `Relics/RelicEffectApplier.cs` 에 case 추가:

```csharp
case RelicEffectType.ShieldOnParry:
    // KhiParryController.ParrySucceeded 구독 (Step 9 에서 등록)
    _onParrySuccessSubscriptions.Add((relic.Magnitude, relic.Duration, relic));
    break;
case RelicEffectType.HealReceivedPercent:
    container.AddPermanent(StatId.HealReceived, relic.Magnitude, relic);
    break;
case RelicEffectType.DashCooldownPercent:
    container.AddPermanent(StatId.DashCooldown, relic.Magnitude, relic);
    break;
case RelicEffectType.MoveSpeedAfterDashTimed:
    _onDashEndSubscriptions.Add((relic.Magnitude, relic.Duration, relic));
    break;
case RelicEffectType.HealConsumablePercent:
    // 소모품은 OnRelicAcquired 가 아닌 OnConsumableUsed 경로로 옴.
    // 효과는 PlayerHealing.UseConsumable 이 직접 처리. 여기 case 는 no-op
    break;
```

추가:
- `parryController.ParrySucceeded += HandleParrySuccess` 구독
- `dashController.OnDashEnded += HandleDashEnded` 구독
- HandleParrySuccess: `_onParrySuccessSubscriptions` 순회 → `playerShield.GrantShield(playerHealth.MaximumHealth * mag, dur)`
- HandleDashEnded: `_onDashEndSubscriptions` 순회 → `container.AddTimed(StatId.MoveSpeed, mag, dur, source)`

### Step 9 — Asset 효과 필드 채우기 (10분)
CL-106 자산 채우기에서 이미 처리됐어야 함. 미처리 상태면:
- `RelicData_반격의표식.asset`: ShieldOnParry / 0.08 / 3 / 0
- `RelicData_철의깃.asset`: HealReceivedPercent / 0.25 / 0 / 0
- `RelicData_질풍장화.asset`: DashCooldownPercent / **-0.12** / 0 / 0
- `RelicData_추적자의망토.asset`: MoveSpeedAfterDashTimed / 0.20 / 2 / 0
- `RelicData_작은회복약.asset`: HealConsumablePercent / 0.25 / 0 / 0
- `RelicData_큰회복약.asset`: HealConsumablePercent / 0.50 / 0 / 0

### Step 10 — Player prefab 부착 + Inspector wiring (15분)
`TestKhi_MinimalCharacter2D` 에:
- PlayerShield (신규)
- PlayerHealing (신규)
- 각 컴포넌트의 health, container 참조 드래그
- KhiDashController 의 statContainer 참조 드래그
- PlayerRelicInventory 의 playerHealing 참조 드래그
- RelicEffectApplier 의 parryController, dashController, playerShield, playerHealth 참조 드래그

---

## 검증 (PlayMode, 8 시나리오)

### 사전 준비
- 기준 적: 패링 가능한 근접 적 + 일반 데미지 적 각 1
- 디버그: PlayerShield, PlayerHealing 모두 logShieldEvents/logHeals = true

### 시나리오 1 — 반격의 표식 (패링 시 보호막)
1. `RelicData_반격의표식` TryUseOrAdd → 인벤토리 등록 (소모품 X)
2. 적 근접 → 패링 성공
3. **기대 로그**: `[PlayerShield] Grant {maxHp*0.08} for 3s, total={...}`
4. 적의 다음 공격 → 보호막이 데미지 흡수 → `[PlayerShield] Absorb {n}, remain={...}`
5. 3 초 경과 → `[PlayerShield] Expired/Cleared`

### 시나리오 2 — 보호막 누적 (2회 패링)
1. 패링 성공 후 1 초 안에 다시 패링 성공
2. **기대**: 보호막 잔량 = (1차 잔량) + (maxHp×0.08), 만료 시간 = 더 늦은 쪽

### 시나리오 3 — 철의 깃 + 회복약 (회복 multiplier)
1. `RelicData_철의깃` TryUseOrAdd → 영구 등록
2. HP 일부 감소 후 `RelicData_큰회복약` TryUseOrAdd → 즉시 사용
3. **기대 로그**: `[PlayerHealing] Heal base={maxHp*0.5} mul=1.250 → {maxHp*0.625}`
4. CurrentHealth 만피 cap 까지 회복

### 시나리오 4 — 질풍 장화 (대시 cooldown 단축)
1. `RelicData_질풍장화` TryUseOrAdd → 영구 등록
2. 대시 → cooldown 88% (예: base 1.0s → 0.88s)
3. **기대**: KhiDashController 의 Cooldown.ConsumptionDuration = baseCooldown × 0.88
4. 0.88 초 후 대시 가능, 0.88 초 전엔 불가

### 시나리오 5 — 추적자의 망토 (대시 종료 후 이동속도 +20% / 2s)
1. `RelicData_추적자의망토` TryUseOrAdd → 영구 등록
2. 대시 → 대시 종료 직후 OnDashEnded 발화
3. **기대 로그**: `[StatModifier] AddTimed MoveSpeed +20.0% for 2s src=RelicData_추적자의망토`
4. 2 초 동안 이동속도 1.20× (바람 깃털과 합산 시 1.28×)
5. 2 초 후 만료 → 이동속도 1.00× 복귀

### 시나리오 6 — 회복약 단독 (소모품 트리거)
1. `RelicData_작은회복약` TryUseOrAdd
2. **기대**: 인벤토리 미등록 (소모품) + PlayerHealing.UseConsumable 즉시 호출
3. CurrentHealth 가 maxHp × 0.25 만큼 회복 (cap 까지)
4. 인벤토리 OwnedRelics 에 회복약 미포함 확인

### 시나리오 7 — 부활 시 회복 multiplier 적용 (CL-014 마이그레이션 검증)
1. `RelicData_철의깃` 보유 상태에서 다운
2. 부활 → PlayerHealing.Heal 경로로 회복
3. **기대**: 부활 회복량 = baseRevive × 1.25

### 시나리오 8 — 4 종 + 회복약 동시 (회귀)
1. 반격의표식 / 철의깃 / 질풍장화 / 추적자의망토 모두 TryUseOrAdd → 인벤토리 등록
2. 큰회복약 TryUseOrAdd → 즉시 사용
3. 패링 성공 → 보호막 부여
4. 대시 → cooldown 단축 + 종료 후 MoveSpeed buff
5. 디버그 메뉴 "Log all totals":
   - HealReceived = 1.25
   - DashCooldown = 0.88
   - MoveSpeed = 1.00 (대시 직후만 1.20)

---

## 완료 기준

- [ ] Unity 컴파일 통과
- [ ] 신규 2 컴포넌트 (PlayerShield, PlayerHealing)
- [ ] 수정 4 파일 (PlayerRelicInventory, RewardPanelView, KhiDashController, KhiParryDamageOnTouch, KhiDownController, RelicEffectApplier)
- [ ] Player prefab Inspector wiring 완료
- [ ] 시나리오 1 ~ 8 모두 통과
- [ ] CL-014 부활 회복이 PlayerHealing.Heal 경로 거치는 것 확인

---

## 위험 / 결정 보류

1. **TDE CharacterDash2D 의 DashStop() virtual 여부** — override 안 되면 LateUpdate 의 IsDashing 변화 감지로 대체. Step 5 코드에 두 경로 모두 명시
2. **KhiParryDamageOnTouch 의 base 호출 우회** — Step 6 의 *모든 경로 ApplyReducedDamage 통합* 패턴은 base 의 부수 효과를 ApplyReducedDamage 가 충분히 재현한다고 가정. 만약 base 와 ApplyReducedDamage 사이에 차이가 발견되면 ApplyReducedDamage 도 보강 필요
3. **보호막 누적 정책** — "잔량 합산 + 만료 시간 더 늦은 쪽" vs "신규로 갱신" — 본 plan 은 합산 채택. 기획 확정 시 수정
4. **보호막 흡수 시 KnockBack/Feedback** — 본 plan 은 *보호막이 완전 흡수하면 피드백 스킵*. "보호막 깨질 때 깜빡임" 같은 별도 피드백 원하면 PlayerShield 에 ShieldHit feedback 추가
5. **소모품 효과 확장성** — 현재 PlayerHealing.UseConsumable 은 HealConsumablePercent 만 처리. 향후 마나 회복약 등 추가되면 소모품 effect dispatcher 분리 필요
6. **랜덤박스 (RelicEffectType.None)** — 본 CL 범위 외. TryUseOrAdd 호출되면 PlayerHealing.UseConsumable 진입 → EffectType 체크에서 no-op 처리. 후속 CL 에서 랜덤박스 자체 메커니즘 구현
7. **부활 회복 정책 모호** — "철의 깃이 부활에도 적용되는 게 맞는가" 기획 확인 필요. 본 plan 은 "적용됨" 채택 (회복 시스템 통일 원칙)
8. **다른 적 공격 진입점** — KhiParryDamageOnTouch 외에 Player 를 공격하는 다른 진입점 (투사체 ranged enemy 등) 있다면 동일 PlayerShield hook 필요. cl041 ranged enemy plan 확인 필요. 본 CL 검증에서 미커버 시 후속 ticket 으로

---

## 후속 CL 인터페이스

| CL | 추가될 작업 |
|---|---|
| **CL-109** | RelicEffectApplier → RelicEffectRegistry 흡수. PlayerShield, PlayerHealing 은 그대로 유지. IRelicEffectAuthority 분리 시 Shield 부여 / Heal 호출 권위 분기 |
| **CL-110** | 보상 UI ↔ 방 클리어 연동 (RewardController). RewardPanelView.OnCardSelected 호출처가 RewardController 로 변경되지만 TryUseOrAdd 호출은 그대로 |
| **후속 ticket** | 랜덤박스 자체 메커니즘. 다른 적 공격 진입점 (투사체) 의 PlayerShield 통합. 보호막 HUD 표시 |

---

## 참고 — explore 검증 결과

- `KhiParryController.cs:59` — `event Action ParrySucceeded` 존재 ✅
- `KhiParryController.cs:271` — HandleParrySuccess 안에서 발화 ✅
- `KhiParryDamageOnTouch.cs:20-62` — Player 공격 데미지 진입점. 보호막 hook 위치 ✅
- `KhiDashController.cs:9` — `: CharacterDash2D` (TDE 상속) ✅
- `KhiDashController.cs:17` — `IsDashing` public ✅
- `KhiDashController.cs:21` — Initialization() override 가능 ✅
- `KhiDashController.cs:56` — `DashStart()` 이미 override ✅
- `Health.ReceiveHealth` (TDE L1022) — 회복 API 존재, 호출처 0 → PlayerHealing 이 첫 호출자
- `RelicData_작은회복약.asset` — `_isConsumable: 1` ✅, 효과 필드는 CL-106 자산 채우기에서 추가 예정
- 6 종 asset 모두 `Assets/_Project/ScriptableObjects/Relics/` 에 존재 ✅
