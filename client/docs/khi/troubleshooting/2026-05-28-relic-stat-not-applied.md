# 유물 stat 효과가 게임플레이에 반영되지 않던 문제 (3중 버그)

## 증상

사용자 보고:
1. **Critical stat 0% 미스터리** — 상태창에 Critical % 가 떠도 노란 popup 발동 0회
2. **평타 강화가 안 먹힘** — `사냥꾼의 표적 (Critical +15% / 평타 +25%)` 등 획득 후에도 적 HP 감소량이 base 그대로
3. **InventoryTestWindow 로 솔로에서 유물 추가 시 "Mirror 수정 차단" 다이얼로그** — 한 명만 있는데 mirror 라고 거부
4. **씬 전환 후 stat 손실 의심** — 새 던전 진입 후 강해진 느낌이 사라짐 (오해였음, 실제로는 정상 동작 — 단지 base 데미지 로그만 보고 판단)

콘솔에서 stat 자체는 잘 등록되는 게 보였음 (`[PlayerHealthStatApplier] MaxHealth 100 → 103`) — 하지만 공격력 / 치명타 효과는 게임플레이에 반영 안 됨.

## Root cause — 3중 누락 버그 (각각 독립)

### A. InventoryTestWindow 의 솔로 mirror 오인식 (Editor 도구)

[`InventoryTestWindow.cs:213`](../../LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/InventoryTestWindow.cs):

```csharp
// OLD — IsOwner 만 봄. NGO 미가동 솔로에선 IsOwner=false 라 mirror 로 오인식.
bool isLocal = no != null && no.IsOwner;
```

플레이어 prefab 에 `NetworkObject` 가 붙어 있음 (멀티 호환용). 솔로 모드에선 `NetworkManager` 가 안 돌아 `NetworkObject.IsSpawned == false`, `IsOwner` 는 false 반환. 그래서:

1. selector 라벨이 `"0P (Mirror, 데이터 sync 없음)"` 표시
2. `GuardEditOrShowDialog()` → "Mirror inventory 는 수정 불가" 다이얼로그 차단
3. `_relicInv.TryAdd(relic)` 도달 안 함 → 유물 자체가 인벤토리에 안 들어감

**왜 발생**: 다른 시스템 (BuildManager / SetEffectApplicator) 은 `HostAuthority.IsHost` 로 솔로/호스트 통일 처리하는데 InventoryTestWindow 만 `NetworkObject.IsOwner` 를 직접 사용. 패턴 일관성 깨짐.

### B. `RelicEffectRegistry` 의 다중 효과 array 미순회 ★ 핵심

[`RelicEffectRegistry.HandleAcquired`](../../LostMemory/Assets/_Project/Scripts/Runtime/Relics/RelicEffectRegistry.cs) OLD:

```csharp
// OLD — relic.EffectType 은 _effects[0].Type 하나만 반환 (legacy property).
// _effects[1..] 의 secondary effect 는 AttackSpeed/Defense 두 타입의 special helper 로만 살아남음.
switch (relic.EffectType)
{
    case RelicEffectType.AttackPowerPercent: ... break;
    // CriticalChancePercent case 없음 → default → no-op
}
```

이게 왜 치명적인지 — 4개 획득 유물의 `_effects[]`:

| 유물 | `_effects[0]` | `_effects[1]` | OLD 동작 |
|------|-----|------|---------|
| 사냥꾼의 표적 | Type=12 **Critical +15%** | Type=1 **AttackPower +25%** | **둘 다 드롭** ✗ (T0=12 switch case 없음, T1 array helper 도 없음) |
| 냉정한 명궁 | Type=12 **Critical +15%** | Type=16 Defense +3 | Critical 드롭, Defense 만 살아남 |
| 정전기 띠 | Type=1 **AttackPower +10%** | Type=22 ChainOnHit | AttackPower 정상 (T0 이 switch case 매칭) |
| 서리 검집 | Type=31 AttackSpeed | Type=20 SlowOnHit | AttackSpeed 정상 (array helper) |

**왜 발생**: CL-138 V0.4 에서 다중 효과 schema (`_effects[]` array) 도입했지만, RelicEffectRegistry 의 호환 path 가 `_effects[0]` 만 처리하고 나머지는 두 타입 (AttackSpeed/Defense) 전용 helper 로만 처리. Critical / Range / Cooldown / Dodge 같은 다른 set-type stat 은 개별 유물 등록 경로에 case 자체가 없었음.

**audit 결과**: 77개 유물 중 약 **40개가 같은 버그 영향권**. Fix 후 일제히 효과 부활.

### C. 원거리 무기들이 AttackPower stat 을 안 곱함

[`KhiBowController.SpawnArrow`](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiBowController.cs) OLD:

```csharp
// OLD — Critical roll 만 하고 AttackPower 안 곱함.
float finalDamage = CriticalRoller.Roll(statContainer, damage, out bool wasCritical);
projectile.Launch(aimDirection, arrowSpeed, finalDamage, gameObject, wasCritical);
```

검(Melee) 만 [`KhiMeleeComboController.cs:337`](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeComboController.cs) 에서 `attackMul` 곱하고 있었고, 다음 6개 진입점이 통째로 누락:

- KhiBowController 화살 (single + rapid)
- KhiStaffController 빙뢰탄 / 화염구
- KhiMeteor 폭격
- MagicalGirlProjectile 동료 투사체
- MagicalGirlAOE 동료 도트

→ 상태창에 +50% AttackPower 떠도 활/지팡이/메테오/동료 데미지는 base 그대로. 검만 +50% 적용.

**왜 발생**: 각 무기 컨트롤러에서 데미지 공식을 인라인으로 작성. Critical 은 이전 세션에 `CriticalRoller.Roll()` 로 통합했지만 AttackPower 곱은 통합 안 함. 새 무기 추가 시 매번 손으로 곱하는 패턴이라 누락 가능.

## Fix

### Fix A — InventoryTestWindow

[`InventoryTestWindow.cs`](../../LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/InventoryTestWindow.cs) — `HostAuthority.IsNetworkSessionActive` 게이트 추가, 솔로면 무조건 Local 처리:

```csharp
bool sessionActive = HostAuthority.IsNetworkSessionActive;
bool isLocal = !sessionActive || (no != null && no.IsOwner);
// 라벨도 "Solo (수정 가능)" / "{N}P (Local/Mirror)" 로 분리
```

### Fix B — RelicEffectRegistry array 순회 ★ 가장 큰 영향

[`RelicEffectRegistry.HandleAcquired`](../../LostMemory/Assets/_Project/Scripts/Runtime/Relics/RelicEffectRegistry.cs) — 새 `ApplyEffect(type, mag, dur, thr, source)` 헬퍼로 단일 dispatch, `relic.Effects[]` 전체 순회:

```csharp
IReadOnlyList<EffectEntry> effects = relic.Effects;
if (effects != null && effects.Count > 0)
{
    for (int i = 0; i < effects.Count; i++)
        ApplyEffect(effects[i].Type, effects[i].Magnitude, effects[i].Duration, effects[i].Threshold, relic);
    return;
}
// Legacy 단일 효과 fallback
ApplyEffect(relic.EffectType, relic.Magnitude, relic.Duration, relic.Threshold, relic);
```

새 switch 는 CL-138 set-type stat 5개 추가 (`CriticalChancePercent`, `CriticalDamagePercent`, `CooldownReductionPercent`, `AttackRangePercent`, `DodgeChancePercent`) — 이전엔 set tier 단위로만 처리됐지만 개별 유물 효과로도 등장 가능하므로.

### Fix C — 원거리 무기 6개에 AttackPower 곱 추가

각 무기 SpawnX 메서드의 `CriticalRoller.Roll(...)` 호출 직전에 1줄 추가:

```csharp
float attackMul = statContainer != null ? statContainer.GetTotalMultiplier(StatId.AttackPower) : 1f;
float finalDamage = CriticalRoller.Roll(statContainer, damage * attackMul, out bool crit);
```

적용 위치: `KhiBowController.SpawnArrow`, `KhiStaffController` (bolt + fireball), `KhiMeteor.Detonate`, `MagicalGirlProjectile.Init`, `MagicalGirlAOE.DoTick`.

## 영향 범위

- **Fix A**: Editor 전용. 빌드 무관.
- **Fix B**: 게임 밸런스 큰 영향 — 77개 유물 중 ~40개의 dropped effect 가 일제히 부활. Critical 12종, AttackPower 2종, Cooldown 8종, Range 10종, Dodge 10종 등.
- **Fix C**: 검 외 모든 원거리/AOE 데미지가 AttackPower stat 반영. 검과 동일한 스케일링.

세 fix 모두 NGO 시그니처 무변경. ClientRpc 영향 X. Join risk 없음. 멀티 / 솔로 양쪽 동일 동작.

## 진단 과정에서 추가했다가 정리한 진단 도구들

세션 중 root cause 격리를 위해 추가했던 임시 진단 (모두 cleanup 완료):
- `KhiMeleeComboController._logAttackMul` 토글 + `[MeleeDiag]` 로그
- `RelicEffectRegistry._logEffectApply` 토글 + OnEnable wiring + per-effect 로그
- `CriticalRoller.DebugForceCritChance` + `DebugLogRolls` 정적 필드
- `CriticalDebugController.cs` 컴포넌트 (파일 삭제)

**유지된 기존 토글** (필요 시 인스펙터에서 ON):
- `BuildManager._logTierChanges` — 세트 tier 변경 추적
- `SetEffectApplicator._logEffectDispatch` — 세트 효과 라우팅 추적

## 교훈

### 1. 다중 효과 schema 의 호환 path 가 함정

새 schema (`_effects[]`) 를 도입할 때 **모든** 소비처를 동시에 옮겨야 안전. `_effects[0]` 만 처리하는 "임시 호환" 코드를 두면 secondary 효과가 조용히 사라짐. 컴파일러가 알려주지 않음.

→ 호환 path 두지 말고 강제 마이그레이션이 안전. 부득이하다면 `_effects[1..]` 가 있으면 경고 로그라도 띄워야 함.

### 2. 무기별 데미지 공식 분산은 stat 추가마다 누락 사고

검만 AttackPower 곱하고 5개 원거리 무기가 누락된 게 정확히 이 패턴. Critical 은 `CriticalRoller.Roll()` 로 한 단계 통합했지만 AttackPower 는 안 했음.

→ **다음 stat 추가 직전에** `DamageCalculator.Compute(stats, baseDamage, out crit, isFinisher, weaponMul)` 같은 한 단계 헬퍼로 통합 권장. 무기별 코드는 그대로 두고 공식만 모음.

### 3. Solo/Multi 분기는 단일 게이트 통일

`HostAuthority.IsHost` / `IsNetworkSessionActive` 가 정식 단일 진입점인데 어떤 코드가 `NetworkManager.Singleton.IsHost` 또는 `NetworkObject.IsOwner` 를 직접 보면 솔로에서 false 가 나와 의도 외 차단. 새 코드에서 NGO 상태 분기할 때 무조건 `HostAuthority` 통과.

### 4. "로그 base 표시" ≠ "실제 적용 안 됨"

`[KhiBow] dmg=10.0` 로그는 SpawnArrow 의 `damage` 파라미터 (base) 만 표시. 그 다음 줄에서 `damage * attackMul` 로 amped 되지만 로그는 안 찍힘. 사용자가 "데미지 안 늘어남" 으로 오해할 수 있음.

→ 데미지 디버그 로그는 가급적 최종 amped 값을 찍거나, 변환 단계를 명시. 또는 적 HP 감소량을 보고 판단.

## 관련 파일

**Code fix (Permanent)**:
- [`Editor/InventoryTest/InventoryTestWindow.cs`](../../LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/InventoryTestWindow.cs)
- [`Runtime/Relics/RelicEffectRegistry.cs`](../../LostMemory/Assets/_Project/Scripts/Runtime/Relics/RelicEffectRegistry.cs)
- [`Runtime/TestKhi/KhiBowController.cs`](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiBowController.cs)
- [`Runtime/TestKhi/KhiStaffController.cs`](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiStaffController.cs)
- [`Runtime/TestKhi/KhiMeteor.cs`](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiMeteor.cs)
- [`Runtime/MagicalGirl/MagicalGirlProjectile.cs`](../../LostMemory/Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlProjectile.cs)
- [`Runtime/MagicalGirl/MagicalGirlAOE.cs`](../../LostMemory/Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlAOE.cs)

**관련 상위 문서**:
- [2026-05-27-damage-popup-and-critical-system-status.md](../2026-05-27-damage-popup-and-critical-system-status.md) — Critical / popup 시스템 전반 작업 현황 (본 문서로 해결)
- [2026-05-27-critical-system-expand-all-attacks-plan.md](../2026-05-27-critical-system-expand-all-attacks-plan.md) — Critical 9개 진입점 통합 plan (Critical 은 통합됐지만 AttackPower 는 누락됐던 점이 본 문서의 Fix C)
