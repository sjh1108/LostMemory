# 상태창 base+bonus 표시 (Phase A — 하이브리드)

## Context

현재 `PlayerStatusPanel` 은 modifier 값만 표시 (`+27%`, `+12` 등). 메이플식 `Total (Base+Bonus)`
포맷으로 확장 요청.

전 stat 일괄 적용은 어울리지 않음:
- HP/이동속도/공격력 같은 stat 는 base+bonus 가 자연스러움 (`142 (100+42)`).
- Critical/CriticalDamage 같은 % stat 는 base 가 0% 라 `27% (0%+27%)` 가 어색.

**Phase A 결정 — 하이브리드:**
- "core" stat 5개 (HP / MoveSpeed / AttackPower / Defense / Critical / CriticalDamage) → base+bonus.
- 나머지 stat → 기존 `+N%` 유지.
- "core" stat 는 modifier=0 이어도 항상 패널에 표시 (메이플의 STR/DEX 가 항상 보이는 것처럼).

PlayerStatsData SO 의 wiring (CL-180) 은 본 작업과 분리 — Phase B 로 미룸. 본 작업은 `PlayerHealthStatApplier`
가 이미 쓰는 **TDE 컴포넌트 live query + 역산 패턴** 만 사용.

## 핵심 접근

`PlayerHealthStatApplier.CaptureBaseIfNeeded()` 의 역산 공식을 그대로 따른다:
```
MaximumHealth = (base + flatBonus) * multiplier
→ base = MaximumHealth / multiplier - flatBonus
```

같은 공식을 MoveSpeed / AttackPower 에도 적용. % 만 있는 stat (Critical 등) 은 base=0 으로 처리해
표시 단계에서 분기.

## 변경 파일

### 신규 — `LostMemory/Combat/StatBaseProvider.cs`

```csharp
namespace LostMemory.Combat
{
    /// <summary>
    /// Phase A: 상태창 표시용으로 (base, bonus, total) 을 산출.
    /// PlayerHealthStatApplier 와 동일한 역산 패턴 — TDE 컴포넌트 live query.
    /// </summary>
    public static class StatBaseProvider
    {
        public readonly struct StatDisplayValue
        {
            public readonly float Base;        // 0 = base 의미 없는 stat (% 류)
            public readonly float Bonus;       // = Total - Base
            public readonly float Total;
            public readonly bool HasBase;
            // ctor ...
        }

        // character 는 null 일 수 있음 (LocalPlayer 미할당) → HasBase=false 반환.
        public static StatDisplayValue Resolve(StatId stat, Character character, PlayerStatModifierContainer container)
        {
            switch (stat)
            {
                case StatId.MaxHealth:
                case StatId.MaxHealthFlat:
                    return ResolveMaxHealth(character, container);
                case StatId.MoveSpeed:
                    return ResolveMoveSpeed(character, container);
                case StatId.AttackPower:
                    return ResolveAttackPower(character, container);
                default:
                    // % stat — base 없음, bonus 만 표시.
                    return BonusOnly(stat, container);
            }
        }
    }
}
```

세부 구현:
- `ResolveMaxHealth` — `character.GetComponentInChildren<Health>()` + container.GetTotalFlat/Multiplier
- `ResolveMoveSpeed` — `character.GetComponentInChildren<CharacterMovement>().MovementSpeed` + 역산
  (TDE CharacterMovement 의 modifier 합성 공식이 다르면 PlayerHealthStatApplier 보다 신중히 — 단순 multiplier 형태일 가능성 큼)
- `ResolveAttackPower` — base = 무기 base damage (찾을 수 있으면), 못 찾으면 HasBase=false 로 폴백
- `BonusOnly` — base=0, total = bonus = sum of modifier magnitudes

### 수정 — `LostMemory/UI/Status/StatusPanelViewModel.cs`

`StatRow` 에 base 정보 추가:
```csharp
public readonly struct StatRow
{
    public readonly string Name;
    public readonly string TotalDisplay;   // 기존 — 그대로 사용 (이미 포맷팅된 문자열)
    public readonly string Description;
    // ctor 그대로
}
```
실제로는 ViewModel 구조 변경 안 함 — 표시 문자열은 Presenter 에서 생성하기 때문에 `TotalDisplay`
하나로 충분. (옵션) 필요 시 `BaseDisplay` 필드 추가하지만 Phase A 에선 불필요.

### 수정 — `LostMemory/UI/Status/StatIdLabels.cs`

기존 `FormatTotal(stat, total)` 유지. 새 포맷터 추가:

```csharp
/// <summary>
/// base 있는 stat 용 'Total (Base+Bonus)' 표기. 예: "142 (100+42)", "1.27 (1+0.27)".
/// </summary>
public static string FormatBaseAndBonus(StatId stat, float baseValue, float bonus, float total)
{
    // flat / multiplier 형태에 따라 소수점 자리수 조절.
    // % stat 는 절대 이 메서드 안 옴 (Presenter 에서 분기 후 호출).
    if (Mathf.Approximately(bonus, 0f))
        return $"{total:0.#}";

    string sign = bonus > 0f ? "+" : "";
    return $"{total:0.#} ({baseValue:0.#}{sign}{bonus:0.#})";
}
```

### 수정 — `LostMemory/UI/Status/PlayerStatusPanelPresenter.cs`

`BuildViewModel` 의 stat 섹션 로직 교체. 핵심 변경:

1. **Always-visible core stat 목록 정의** (필드 또는 static 배열):
   ```csharp
   private static readonly StatId[] s_alwaysVisible =
   {
       StatId.MaxHealth, StatId.AttackPower, StatId.MoveSpeed,
       StatId.Defense, StatId.Critical, StatId.CriticalDamage,
   };
   ```

2. **Core stat 먼저 처리** — `StatBaseProvider.Resolve` 호출:
   - HasBase=true → `FormatBaseAndBonus(stat, base, bonus, total)`
   - HasBase=false → `FormatTotal(stat, bonus)`, bonus=0 이면 `0` 또는 그냥 stat 이름만

3. **나머지 modifier 있는 stat** — 기존 로직 (modifier 합산 + FormatTotal). 단 core 와 중복 안 되도록
   필터.

4. `LocalPlayerResolver.LocalCharacter` 로 Character 잡기 (이미 있는 패턴 — 다른 Presenter 참조).

### 변경 없음

- `PlayerStatusPanelView.cs` — Render(vm) 시그니처 그대로. ViewModel 의 `TotalDisplay` 문자열을
  그대로 출력하면 됨.
- `PlayerStatusRowView.cs` — 이름/값/설명 3슬롯 그대로. 값 슬롯에 `"142 (100+42)"` 같이 들어가도 OK.
- prefab — 그대로.

## 의도적으로 안 하는 것

- **PlayerStatsData SO 통합 (CL-180)** — Phase B. 별도 plan.
- **무기 변경 시 AttackPower base 실시간 갱신** — Phase A 에선 ModifiersChanged 이벤트 트리거 시
  re-resolve 만. 무기 swap 이벤트 별도 구독은 안 함 (다음 modifier 변경 때 어차피 동기화됨).
- **OnHit 섹션** — 변경 없음. 효과 보유 여부만 표시하는 기존 로직 그대로.
- **base 가 안 잡힐 때 폴백** — Character/Health 컴포넌트 못 찾으면 HasBase=false 처리.
  멀티에서 LocalPlayer 미할당 시점에 잠깐 base 안 보일 수 있지만 다음 LocalPlayerReady 이벤트 때
  Refresh 되니 자가 회복.

## 검증

1. **솔로** — 던전 진입 → I 키 → 패널 열고 6개 core stat (HP/공격력/이속/방어/크리/크댐) 항상 보임 확인.
   유물 1개 획득 → 해당 stat 의 bonus 부분이 갱신.
2. **유물 획득 직전 vs 직후** — 표시 변경:
   - 직전: `최대 체력 100 (100+0)`
   - 직후 (HP+12% 유물): `최대 체력 112 (100+12)`
3. **% stat** — Critical 만 있는 유물 획득 후 `치명타 확률 +27%` 로 표시 (기존 형식 유지).
4. **멀티** — 호스트와 게스트 각자 자기 player 의 stat 만 보이는지. 다른 사람 stat 안 섞이는지.
5. **base 못 잡는 경우 회귀 방지** — `LocalPlayerResolver.LocalCharacter == null` 일 때
   panel 열어도 NullRef 없이 그냥 빈 값/`+N%` 로 fallback 동작.

## 후속 (Phase B 메모)

- `PlayerStatsData` SO 를 단일 base 소스로 wire (CL-180).
- 위 `StatBaseProvider.ResolveMaxHealth` 류 메서드 안에서 SO 사용으로 분기 가능.
- 그 외 stat (AttackSpeed, Range, Cooldown, DashCooldown, FinisherDamage, HealReceived,
  ManaRegen, Dodge) 도 base 등록 시 `s_alwaysVisible` 확장.
