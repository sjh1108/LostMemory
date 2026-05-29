# 크리티컬 시스템 전체 무기·스킬 확장 계획

## Context

현재 크리티컬은 **평타(`KhiMeleeComboController`)에만 구현**. 나머지 모든 공격 진입점(화살, 마법탄, 메테오, 패리 반격, 체인/풍속 보조효과, 마법소녀 동료)은 `isCritical=false` 고정 또는 stat 미접근 상태. 사용자가 활을 주력으로 쓰는 도중 크리티컬이 안 떠 발견됨.

목표: **플레이어가 적에게 가하는 모든 공격**에 크리티컬 시스템 적용. 단일 `StatId.Critical` / `StatId.CriticalDamage` 사용 (통합). DamagePopup의 노란 CRITICAL 라벨이 무기 무관 발동.

## 결정 사항 (사용자 합의)

- **범위**: 캐릭터가 몬스터에 가하는 **모든 공격** (9개 진입점)
- **Stat 구조**: 단일 `StatId.Critical` 통일 — 근접/원거리 분리 없음
- **권한**: Host 결정 (실제 데미지는 host authority. client는 본인 popup용으로만 local roll)
- **보조효과**: 각자 별도 critical 판정 (평타 크리 여부와 무관)

## 핵심 발견 (탐색 결과)

| # | 진입점 | 파일 | statContainer | 멀티 경로 |
|---|--------|------|---------------|----------|
| 1 | 평타 (Melee) | `KhiMeleeComboController.cs:342` | ✅ | TargetHit event | **이미 완료** |
| 2 | 화살 (Arrow) | `KhiArrowProjectile.cs:388` | ❌ | `RelayProjectileDamage` |
| 3 | 스태프 마법탄 (Bolt) | `KhiStaffController` → ArrowProjectile | ✅ (Staff에 있음) | 화살 경로 재사용 |
| 4 | 스태프 차징전 (Fireball) | 화살 경로 동일 | ✅ | 화살 경로 |
| 5 | 스태프 차징후 (Meteor) | `KhiMeteor.cs:291,295` | ❌ | `RelayDamage` |
| 6 | 패리 반격 | `KhiParryDamageOnTouch.cs:171` | ❌ | `RelayDamage` |
| 7 | 체인 번개 | `OnHitEffectRegistry.cs:290` (ApplyChain) | ✅ | `RelayDamage` |
| 8 | 풍속 검기 | `OnHitEffectRegistry.cs` (ApplyWindBlade) | ✅ | `RelayDamage` |
| 9 | 마법소녀 투사체 | `MagicalGirlProjectile.cs:87` | ❌ | (미구현) |
| 10 | 마법소녀 AOE | `MagicalGirlAOE.cs:115` | ❌ | (미구현) |

> **2/3/4는 같은 `KhiArrowProjectile` 사용** — 한 번 손보면 셋 다 해결.

## 설계 — 공통 유틸 추출 + 진입점 hook

### 1. 새 유틸 `CriticalRoller`

평타에 박혀있는 로직을 정적 헬퍼로 추출. **9개 진입점이 모두 같은 한 줄로 호출**.

```csharp
// 신규 파일: _Project/Scripts/Runtime/Combat/CriticalRoller.cs
public static class CriticalRoller
{
    /// <summary>
    /// Critical roll. statContainer null 이면 그대로 반환 (no crit).
    /// </summary>
    public static float Roll(PlayerStatModifierContainer stats, float baseDamage, out bool wasCritical)
    {
        wasCritical = false;
        if (stats == null || baseDamage <= 0f) return baseDamage;

        float chance = Mathf.Max(0f, stats.GetTotalMultiplier(StatId.Critical) - 1f);
        if (chance <= 0f || UnityEngine.Random.value >= chance) return baseDamage;

        float dmgBonus = 0.5f + Mathf.Max(0f, stats.GetTotalMultiplier(StatId.CriticalDamage) - 1f);
        wasCritical = true;
        return baseDamage * (1f + dmgBonus);
    }
}
```

`KhiMeleeComboController.cs:342-355` 의 기존 로직을 이 헬퍼 호출로 **교체** (한 줄). 동작 동일성 보장.

### 2. 진입점별 변경

**원칙**: 각 진입점에서 데미지 확정 직전에 `CriticalRoller.Roll()` 호출 → `finalDamage` + `wasCritical` 획득 → `DamagePopupSpawner` 에 둘 다 전달.

**A. 화살 계열 (#2, #3, #4) — `KhiArrowProjectile.cs`**
- `Launch()` 시 호출자(KhiBow/Staff)가 stats 참조 같이 전달 또는 — 더 단순: 화살 자체가 `_attacker.GetComponentInParent<PlayerStatModifierContainer>()` 캐싱
- Spawn 시점에 미리 `Roll()` → `_damage`에 결과 저장 + `_wasCritical` 필드 추가
- `TryApplyHit` line 396: `NotifyArrowDamage(health, _damage, isCritical: _wasCritical)` 로 false 고정 제거
- **호스트 RPC 분기**: `RelayProjectileDamage` 시그니처에 `bool isCritical` 추가. server에서 그대로 사용. 또는 더 단순 — host 측에서도 다시 roll. 디자인 결정: **spawn 시 1회 roll 고정** (예측 일관성)

**B. 메테오 (#5) — `KhiMeteor.cs`**
- spawn 시점에 statContainer 캐싱 + roll 1회 → `damage` 필드 갱신 + `_wasCritical` 저장
- line 291/295 `RelayDamage`/`health.Damage` 직후 `DamagePopupSpawner.Instance.NotifyMeleeDamage(target, damage, _wasCritical)` (메테오는 평타로 분류해도 됨, 카테고리 차별 원하면 SpawnerAPI에 새 Notify 추가)

**C. 패리 반격 (#6) — `KhiParryDamageOnTouch.cs`**
- 패리 발동 시점에 attacker stats 참조 → roll
- line 171 `health.Damage` 직전 `damage = CriticalRoller.Roll(stats, damage, out bool wasCrit)` + popup notify

**D. 보조효과 (#7, #8) — `OnHitEffectRegistry.cs`**
- `ApplyChain` / `ApplyWindBlade` 안에서 각 적별 `chainDamage` / `bladeDamage` 적용 직전 roll
- 이미 `statContainer` 있음 (line 39). 한 줄 추가만:
  ```csharp
  chainDamage = CriticalRoller.Roll(statContainer, chainDamage, out bool wasCrit);
  // ... Damage 적용
  DamagePopupSpawner.Instance.NotifySubEffectDamage(t, chainDamage, wasCrit); // signature 확장
  ```
- `DamagePopupSpawner.NotifySubEffectDamage` 시그니처에 `bool isCritical` 추가 (현재는 색상만 Sub 색)

**E. 마법소녀 (#9, #10) — `MagicalGirlProjectile.cs` / `MagicalGirlAOE.cs`**
- 둘 다 spawn 시 owner(`MagicalGirlSpawner`) 참조 보유 → spawner를 통해 attacker GameObject 알 수 있음
- `Awake` 또는 첫 `Update`에서 `GetComponentInParent<PlayerStatModifierContainer>()` lazy resolve (spawner의 anchor 통해)
- 데미지 적용 직전 roll
- popup: `NotifyMeleeDamage` 재사용 (또는 새 카테고리 — 향후 확장 가능)

## 핵심 파일

**신규**:
- `client/LostMemory/Assets/_Project/Scripts/Runtime/Combat/CriticalRoller.cs` — 정적 유틸

**수정** (대표 경로 — 위 표와 동일):
- `Combat/OnHitEffectRegistry.cs` — ApplyChain, ApplyWindBlade
- `TestKhi/KhiMeleeComboController.cs` — 기존 로직 → CriticalRoller 호출로 교체 (동작 동일)
- `TestKhi/KhiBowController.cs`, `TestKhi/KhiStaffController.cs` — spawn 시 stats 캐싱 후 화살로 전달
- `TestKhi/KhiArrowProjectile.cs` — `_wasCritical` 필드 + popup 호출 시 전달
- `TestKhi/KhiMeteor.cs` — spawn 시 roll + popup notify
- `TestKhi/KhiParryDamageOnTouch.cs` — roll + popup notify
- `MagicalGirl/MagicalGirlProjectile.cs`, `MagicalGirl/MagicalGirlAOE.cs` — stats lazy resolve + roll
- `_Project/Scripts/Runtime/UI/DamagePopup/DamagePopupSpawner.cs` — `NotifySubEffectDamage` 시그니처에 `bool isCritical` 추가, `NotifyArrowDamage`는 이미 받음

## 멀티플레이 권한

- **Host**: 데미지 적용 권한. host 측에서 `Roll()` 실행 → server가 적의 Health 줄임
- **Client**: 본인 popup용 local roll. **host와 결과 다를 수 있음** (랜덤 시드 다름). 시각만 약간 다르지만 게임플레이엔 영향 없음 — host 데미지가 진실
- **RPC 시그니처**: `RelayProjectileDamage`, `RelayDamage` 등에 `bool isCritical` 추가 안 함 (스코프 증가). 대신 host도 자체 roll → 보조효과는 host만 실행이라 자연스러움. 화살은 client local + host local 각자.

> **간소화**: client owner 화살의 경우 client에서 `_damage`만 보내고 host가 자체 roll → host 결과로 popup. 즉 client는 자기 popup도 host RPC response 받고 띄움. 약간의 latency 있지만 정합성 우선.

→ **최종 결정**: 일단 각자 local roll (간단). desync 보고되면 host authority로 변경.

## 진단 (이전 60% 미발동 이슈)

이 plan과 별개로 **사용자가 보고한 "Critical 60% 적용 안 됨"**은 별도 진단 필요:
1. KhiMeleeComboController에 추가한 임시 `[KhiMelee/CritDiag]` 로그 확인 (평타 1회 쳐서)
2. 60%로 set한 경로 확인 (재능? 유물? 인스펙터 직접?)
3. 로그가 `multiplier=1.000 (=> critChance=0%)` 이면 stat 적용 자체가 안 된 것 → 별도 이슈
4. `multiplier=1.6 (=> critChance=60%)` 이면 평타는 정상. 활은 코드상 무조건 미발동 (이 plan으로 해결)

## 구현 순서

1. **`CriticalRoller.cs` 신규 + KhiMeleeComboController 교체** (no-op refactor, 검증 쉬움)
2. **화살 (KhiArrowProjectile)** — 2/3/4 동시 해결 (체감 가장 큼)
3. **메테오 + 패리 반격** — 5/6
4. **보조효과 (ApplyChain/Wind)** — 7/8 + DamagePopupSpawner 시그니처 확장
5. **마법소녀 동료** — 9/10 (난이도 높음, 마지막)
6. **임시 디버그 로그 제거** — `KhiMeleeComboController.cs` 의 `[KhiMelee/CritDiag]` 한 블록

## 검증

각 단계 후:
1. KhiStats의 `StatId.Critical` 인스펙터에서 임시 multiplier `2.0` (=100% 확률) set
2. 해당 무기로 적 타격 → 노란 CRITICAL popup 매번 발동 확인
3. multiplier `1.0` 으로 되돌리면 절대 발동 안 함 확인 (음수 가드)
4. host + guest 멀티에서 본인 화면에 본인 데미지 popup만 보이는지 (기존 본인-only 로직 유지 확인)
5. 모든 단계 후 `[KhiMelee/CritDiag]` 임시 로그 제거 확인

## 의도적으로 안 하는 것

- **TestKhiDamageTrap** (#11) — 환경 트랩, 사용자 공격 아님. 스코프 제외
- **근접/원거리 stat 분리** — 사용자 결정대로 통일
- **Critical 별도 popup 카테고리 (방어구 관통 등)** — 향후 확장. 현재는 색/라벨만 차별
- **호스트 권위 RPC 동기화 강제** — 일단 각자 local roll, 추후 desync 보고되면 강화
