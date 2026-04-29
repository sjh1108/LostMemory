# CL-106 Wave A — RelicData 확장 + StatModifierContainer + 18 asset 채우기

[CL-106 본 plan](cl106_relic_effect_apply_plan.md) 의 *Wave A* 만 분리한 작업 plan.

## 진행 상태 (handoff)

| 단계 | 상태 | 비고 |
|---|---|---|
| **1. 코드 변경 (4 파일)** | ✅ **완료** | disk 에 저장됨, 미커밋. 아래 *완료된 코드* 섹션 |
| **2. Asset 18 개 채우기** | ⬜ 미진행 | Unity Inspector 에서 수동. 아래 *남은 작업 — Asset* 섹션 |
| **3. PlayMode 검증** | ⬜ 미진행 | 시나리오 5 개. 아래 *남은 작업 — 검증* 섹션 |
| **4. 커밋 + 완료 보고 doc** | ⬜ 미진행 | 검증 끝난 후 |

다음에 작업 재개할 때 **Step 2 부터** 시작하면 됨.

---

## Context

[CL-106 본 plan](cl106_relic_effect_apply_plan.md) 의 *Wave A — 데이터/기반 인프라*. 후속 Wave B/C/D 가 기댈 *토대* 작업.

**현재 상태 (Wave A 전):**
- `RelicData.cs` 가 보유한 효과 정보 = `_effectDescription` *문자열* 뿐 (예: `"공격력 +5%"`). 코드가 못 읽음
- 18 개 RelicData asset (MVP 10 + 비-MVP 5 + 소모품 3) 이미 존재. 모두 `_effectDescription` 문자열만 채워짐
- `RewardPool` / `PlayerRelicInventory` / `RewardPanelView` 등 추첨/UI/인벤토리 인프라 완성 — 효과 *적용* 만 비어있음

**Wave A 의 목적**: *기계가 읽을 수 있는 효과 데이터* + *modifier 합산 컨테이너* 를 만들어서, 후속 Wave 의 hook 들이 한 곳에서 modifier 를 조회/등록할 수 있게 한다. **가시 효과 = 0** (의도된 무가시 인프라).

## 결정사항

- **비-MVP 5 종 처리**: 같이 채움 (단 EffectType=None — 효과 hook 자체가 후속). asset 의 *모든 필드가 채워진 완전 상태* 로 두기 위함
- **검증 방식**: PlayMode 디버그 로그 + Inspector 확인. EditMode 단위 테스트 인프라 신설은 본 CL 범위 밖

---

## 완료된 코드 (참고)

### 신규 파일 3개

#### `Relics/RelicEffectType.cs` (enum 12 값)
```csharp
public enum RelicEffectType
{
    None = 0,
    AttackPowerPercent,               // 전사의 끈
    AttackSpeedOnKillTimed,           // 붉은 송곳니
    FinisherDamagePercent,            // 분쇄의 팔찌
    AttackPowerConditional,           // 전투 북 (HP ≥ Threshold)
    MaxHealthPercent,                 // 수호의 파편
    ShieldOnParry,                    // 반격의 표식
    HealReceivedPercent,              // 철의 깃
    MoveSpeedPercent,                 // 바람 깃털
    DashCooldownPercent,              // 질풍 장화 (음수 = 단축)
    MoveSpeedAfterDashTimed,          // 추적자의 망토
    HealConsumablePercent,            // 회복약 (Magnitude = 회복률)
}
```

#### `Combat/StatId.cs` (enum 7 값)
```csharp
public enum StatId
{
    AttackPower, AttackSpeed, MoveSpeed, MaxHealth,
    FinisherDamage, DashCooldown, HealReceived,
}
```

#### `Combat/PlayerStatModifierContainer.cs`
영구/임시/조건부 modifier 보관 + `GetTotalMultiplier(StatId)` 합산 조회. `Log Modifier Changes` 토글 + ContextMenu 디버그 4개 (`Add +5% AttackPower` / `Add +12% AttackSpeed for 3s` / `Log all totals` / `Clear all modifiers`).

### 수정 파일 1개

#### `Relics/RelicData.cs` — 4 필드 + 접근자 추가
```csharp
[Header("Effect (CL-106 Wave A)")]
[SerializeField] private RelicEffectType _effectType = RelicEffectType.None;
[SerializeField] private float _magnitude;
[SerializeField, Min(0f)] private float _duration;
[SerializeField, Min(0f)] private float _threshold;

public RelicEffectType EffectType => _effectType;
public float Magnitude => _magnitude;
public float Duration => _duration;
public float Threshold => _threshold;
```

> **Unity 자동 직렬화 동작**: 새 필드 추가 → 기존 18 asset 들이 *기본값 (None / 0)* 으로 자동 채워짐. asset re-import 만 일어남, 데이터 손실 없음.

---

## 남은 작업 — Asset (Inspector 수동, 18 개)

`Assets/_Project/ScriptableObjects/Relics/` 의 각 .asset 을 Inspector 에서 열어 신규 4 필드 채움. 새 섹션 *Effect (CL-106 Wave A)* 가 보임.

| Asset | EffectType | Magnitude | Duration | Threshold | 비고 |
|---|---|---|---|---|---|
| **RelicData_전사의끈** | AttackPowerPercent | 0.05 | 0 | 0 | 영구 +5% |
| **RelicData_붉은송곳니** | AttackSpeedOnKillTimed | 0.12 | 3 | 0 | 적 처치 시 3초 |
| **RelicData_분쇄의팔찌** | FinisherDamagePercent | 0.20 | 0 | 0 | 3타에만 적용 (Wave B 분기) |
| **RelicData_전투북** | AttackPowerConditional | 0.10 | 0 | 0.5 | HP ≥ 50% |
| **RelicData_수호의파편** | MaxHealthPercent | 0.12 | 0 | 0 | 등록 시 1회 적용 (Wave B) |
| **RelicData_반격의표식** | ShieldOnParry | 0.08 | 3 | 0 | 패링 성공 시 maxHp*8% 보호막 3초 |
| **RelicData_철의깃** | HealReceivedPercent | 0.25 | 0 | 0 | 회복량 +25% |
| **RelicData_바람깃털** | MoveSpeedPercent | 0.08 | 0 | 0 | 영구 +8% |
| **RelicData_질풍장화** | DashCooldownPercent | **-0.12** | 0 | 0 | 음수 = 단축 |
| **RelicData_추적자의망토** | MoveSpeedAfterDashTimed | 0.20 | 2 | 0 | 대시 종료 후 2초 |
| **RelicData_작은회복약** | HealConsumablePercent | 0.25 | 0 | 0 | maxHp*25% 회복 (Wave C) |
| **RelicData_큰회복약** | HealConsumablePercent | 0.50 | 0 | 0 | maxHp*50% 회복 (Wave C) |
| **RelicData_광전사의문장** | None | 0 | 0 | 0 | 비-MVP, hook 후속 |
| **RelicData_성벽의궤** | None | 0 | 0 | 0 | 비-MVP, hook 후속 |
| **RelicData_순환의방패핵** | None | 0 | 0 | 0 | 비-MVP, hook 후속 |
| **RelicData_시공의파편** | None | 0 | 0 | 0 | 비-MVP, hook 후속 |
| **RelicData_잔상의목걸이** | None | 0 | 0 | 0 | 비-MVP, hook 후속 |
| **RelicData_랜덤박스** | None | 0 | 0 | 0 | 메커니즘 자체 후속 |

> **주의**: 질풍장화의 Magnitude = **-0.12** (음수). Unity Inspector 에서 `[Min(0f)]` 같은 제약은 `_duration` / `_threshold` 에만 걸려있어서 `_magnitude` 는 음수 입력 가능. 단축이라 음수 multiplier (1 + (-0.12) = 0.88x).

> 비-MVP 6 종은 enum 값 추가 + 효과 hook 도 *후속 Wave* 에서 같이. Wave A 는 *MVP 10 + 회복약 2 = 실효 12 개*만 실효 데이터.

---

## 남은 작업 — 검증 (PlayMode)

### Step A) Player 에 컴포넌트 부착

1. Hierarchy 에서 **`TestKhi_MinimalCharacter2D`** 선택
2. Inspector → **Add Component** → `Lost Memory/Combat/Player Stat Modifier Container`
3. 새로 추가된 컴포넌트의 **Debug → `Log Modifier Changes`** ✅
4. Scene 저장

### Step B) 시나리오 1 — 영구 modifier 등록

1. Play
2. Hierarchy 에서 `TestKhi_MinimalCharacter2D` 선택 → Inspector 의 `PlayerStatModifierContainer` 컴포넌트 우상단 **︙** 메뉴
3. **"Debug — Add +5% AttackPower (permanent)"** 클릭
4. **기대 로그** (Console):
   ```
   [StatModifier] AddPermanent AttackPower +5.0% src=DebugContextMenu
   [StatModifier] AttackPower total = 1.050
   ```

### Step C) 시나리오 2 — 임시 modifier + 만료

1. Play 중에 **︙** → **"Debug — Add +12% AttackSpeed for 3s (timed)"** 클릭
2. **기대 로그 (등록 직후)**:
   ```
   [StatModifier] AddTimed AttackSpeed +12.0% for 3s src=DebugContextMenu
   [StatModifier] AttackSpeed total = 1.120
   ```
3. **3초 후 자동 (만료)**:
   ```
   [StatModifier] Expired 1 timed mods at t=...
   ```
4. **︙** → **"Debug — Log all totals"** 클릭 → AttackSpeed = 1.000 확인

### Step D) 시나리오 3 — 합산 검증 (영구 + 영구)

1. **︙** → "Add +5% AttackPower" *두 번* 연속 클릭
2. **︙** → "Log all totals" 클릭
3. **기대**: `AttackPower = 1.100 (count=2)` — 두 modifier 합산 (1 + 0.05 + 0.05)

### Step E) 시나리오 4 — Asset 직렬화 확인

1. Play 종료
2. Project view → `Assets/_Project/ScriptableObjects/Relics/RelicData_전사의끈.asset` 클릭
3. Inspector 에 **Effect (CL-106 Wave A)** 섹션 펼쳐 확인:
   - EffectType = `Attack Power Percent`
   - Magnitude = `0.05`
   - Duration = `0`
   - Threshold = `0`
4. `RelicData_광전사의문장.asset` 클릭 → EffectType = `None` / 모두 0 확인

### Step F) 시나리오 5 — Clear

1. Play
2. **︙** → "Add +5% AttackPower" 한 번 클릭
3. **︙** → "Add +12% AttackSpeed for 3s" 한 번 클릭
4. **︙** → "Debug — Clear all modifiers" 클릭
5. **기대 로그**:
   ```
   [StatModifier] Cleared all modifiers.
   ```
6. **︙** → "Log all totals" → 모든 Stat = 1.000 (count=0)

---

## 완료 기준

- [ ] Unity 컴파일 통과 (Console 빨간 에러 없음)
- [ ] Asset 18 개 모두 *Effect (CL-106 Wave A)* 섹션 채워짐
  - [ ] MVP 10 + 회복약 2 = 12 개 실효 데이터
  - [ ] 비-MVP 6 = None
- [ ] 시나리오 1 (영구 등록) 통과
- [ ] 시나리오 2 (임시 + 만료) 통과
- [ ] 시나리오 3 (합산) 통과
- [ ] 시나리오 4 (asset 직렬화) 통과
- [ ] 시나리오 5 (Clear) 통과

---

## 검증 후 다음 단계

1. **커밋**: 코드 4 파일 + asset 18 파일 변경
2. **완료 보고 doc** (`docs/khi/cl106_a.md`) 작성 — `cl048.md` 와 같은 스타일
3. **다음 CL** 결정:
   - 옵션 1: **CL-XXX (보상 UI ↔ 방 클리어 연동)** — 이전 논의대로 Wave B 보다 먼저
   - 옵션 2: **CL-106-B (Wave B 전투 hook)** — 진도 우선

---

## 핵심 파일

### 신규 (이미 작성됨)
- [client/LostMemory/Assets/\_Project/Scripts/Runtime/Relics/RelicEffectType.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Relics/RelicEffectType.cs)
- [client/LostMemory/Assets/\_Project/Scripts/Runtime/Combat/StatId.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Combat/StatId.cs)
- [client/LostMemory/Assets/\_Project/Scripts/Runtime/Combat/PlayerStatModifierContainer.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Combat/PlayerStatModifierContainer.cs)

### 수정 (이미 변경됨)
- [client/LostMemory/Assets/\_Project/Scripts/Runtime/Relics/RelicData.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Relics/RelicData.cs) — 4 필드 + 접근자

### Asset 18 개 (수동 작업 대상)
- `client/LostMemory/Assets/_Project/ScriptableObjects/Relics/RelicData_*.asset`

---

## 위험 / 결정 보류

1. **비-MVP 6 종 (5 비-MVP + 랜덤박스) None 으로 둠** — 후속에서 enum 값 추가 + asset 재채우기 필요. 본 doc 의 *남은 작업 표* 에 후속 작업으로 명시하여 드리프트 방지
2. **합산 정책 변경 시점** — 향후 곱연산이 필요한 효과 (예: critical multiplier) 가 들어오면 `GetTotalMultiplier` 시그니처 확장 또는 별도 메서드 추가. 현재 1차 MVP 는 합연산만
3. **Source 식별** — `object source` 로 두어 `RemoveBySource` 가 RelicData 인스턴스 ref 로 동작 가능. Wave D 의 RelicEffectRegistry 가 RelicData 인스턴스를 source 로 등록 권장
4. **Update 비용** — `_timed.RemoveAll` 이 매 프레임 호출. 동시 임시 modifier 5~10 개 추정 → 무시 가능
5. **멀티 분기점** — 본 Wave 는 *순수 로컬*. modifier 등록/조회는 호스트/클라이언트 동일 동작 (RelicData 가 정적 자산이라). Wave D 의 `IRelicEffectAuthority` 가 *등록 시점* 에만 권위 분기

---

## 후속 Wave 와의 인터페이스

### Wave B (전투 hook) 가 사용할 API
```csharp
float damageMul = container.GetTotalMultiplier(StatId.AttackPower);
damage *= damageMul;
```

### Wave C (대시/보호막/회복) 가 사용할 API
```csharp
float healMul = container.GetTotalMultiplier(StatId.HealReceived);
health.GetHealth(baseAmount * healMul, source);
```

### Wave D (RelicEffectRegistry) 가 사용할 API
```csharp
switch (relic.EffectType) {
    case RelicEffectType.AttackPowerPercent:
        container.AddPermanent(StatId.AttackPower, relic.Magnitude, relic);
        break;
    case RelicEffectType.AttackSpeedOnKillTimed:
        // OnEnemyKilled 이벤트 구독 → AddTimed(AttackSpeed, magnitude, duration, relic)
        break;
    // ...
}
```
