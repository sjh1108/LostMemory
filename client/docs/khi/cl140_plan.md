# CL-140 — EffectApplicator 구현 (Player 스탯 hook + 효과 적용/해제)

## Context

Epic S Phase 1의 마지막 ticket. **Foundation 완성**.

데이터 흐름 정리:
```
CL-138 (data) → CL-139 (count/tier) → CL-140 (apply) ← 본 CL
─────────────                                          
RelicData                                              
BuildSetData         BuildManager                      
   ↓ 정의           ↓ 카운트                          
태그/티어/효과      현재 티어                          
                    ↓ 이벤트 발화                      
                                  EffectApplicator
                                  ↓ 라우팅
                                  ┌─────┬─────┬─────┐
                                  ↓     ↓     ↓     ↓
                              StatMod  Entity OnHit  System
```

CL-139 결과물:
- `BuildManager.OnSetTierChanged` 이벤트 (RelicTag, oldTier, newTier)
- `BuildManager.GetActiveTierData(tag)` 조회 API

**CL-140의 책임 (단일)**: BuildManager의 이벤트를 받아서 **`SetTier.EffectType`에 따라 적절한 시스템에 라우팅**.

→ 본 CL은 "**라우터**". 실제 효과 처리는 각 도메인 시스템 (PlayerStatModifierContainer, MagicalGirlSpawner 등) 책임.

---

## 결정사항

### 1. 컴포넌트 위치 — Player GameObject

기존 `RelicEffectRegistry`와 같은 위치. BuildManager와 짝.

```
[Player GameObject]
  ├─ PlayerRelicInventory
  ├─ PlayerStatModifierContainer
  ├─ RelicEffectRegistry      (개별 유물 효과)
  ├─ BuildManager             (CL-139, 세트 카운트/티어)
  └─ SetEffectApplicator      (CL-140, 라우터) ⭐ 신규
```

**명명**: `SetEffectApplicator` (직관적). `EffectApplicator`도 가능하지만 RelicEffectRegistry와 구분 위해 `Set` 접두사 권장.

### 2. 이벤트 처리 패턴 — 티어 전환 모델

`OnSetTierChanged(tag, oldTier, newTier)` 이벤트 받으면:

```
1. oldTier 효과 제거 (있었으면)
2. newTier 효과 적용 (활성이면)
```

→ **이전 티어 효과는 항상 정리 후 새 티어 적용**. 혼선 방지.

**제거 메커니즘**:
- `PlayerStatModifierContainer`는 `RemoveBySource(object source)` 이미 있음 (CL-106)
- source = 본 SetEffectApplicator + (tag, oldTier) 식별자
- 또는 source = `BuildSetData` SO 자체 (TagPrimary 단위)

**채택**: source = `(BuildSetData set, int tier)` 튜플 또는 별도 식별자 클래스.

```csharp
private struct SetEffectSource
{
    public BuildSetData Set;
    public int TierIndex;
}

// 등록
container.AddPermanent(stat, magnitude, new SetEffectSource(set, newTier));
// 제거
container.RemoveBySource(/* matching predicate */);
```

`RemoveBySource`는 `Equals` 비교를 사용하므로 source 비교 필요 → struct + IEquatable 구현.

### 3. EffectType → 시스템 라우팅 표

CL-138 정의된 31개 enum 값을 4개 시스템으로 분배:

| EffectType | 라우팅 시스템 | 메서드 |
|---|---|---|
| `AttackPowerPercent` | StatModifier | `AddPermanent(AttackPower, magnitude)` |
| `MaxHealthPercent` | StatModifier | `AddPermanent(MaxHealth, magnitude)` |
| `MoveSpeedPercent` | StatModifier | `AddPermanent(MoveSpeed, magnitude)` |
| `CriticalChancePercent` | StatModifier | `AddPermanent(Critical, magnitude)` ※ Critical StatId 추가 필요 |
| `CooldownReductionPercent` | StatModifier | `AddPermanent(Cooldown, magnitude)` ※ |
| `AttackRangePercent` | StatModifier | `AddPermanent(Range, magnitude)` ※ |
| `DodgeChancePercent` | StatModifier | `AddPermanent(Dodge, magnitude)` ※ |
| `DefenseFlat` | StatModifier (flat) | `AddPermanent(Defense, magnitude)` ※ |
| `GoldGainPercent` | System hook (GoldManager) | `goldManager.AddMultiplier(magnitude)` |
| `LuckPoints` | System hook (RewardPool) | `rewardPool.AddLuckPoints(magnitude)` |
| `BurnOnHit` | OnHitEffectRegistry | `onHitRegistry.AddBurn(magnitude, duration)` |
| `SlowOnHit` | OnHitEffectRegistry | `onHitRegistry.AddSlow(magnitude)` |
| `FreezeOnHit` | OnHitEffectRegistry | `onHitRegistry.AddFreeze(duration)` |
| `ChainOnHit` | OnHitEffectRegistry | `onHitRegistry.AddChain(damage)` |
| `WindAOE` | OnHitEffectRegistry | `onHitRegistry.AddWind(magnitude)` |
| `MagicalGirlSummon` | MagicalGirlSpawner | `mgSpawner.SetCount(tier+1)` |
| `MagicalGirlFusion` | MagicalGirlSpawner | `mgSpawner.TriggerFusion()` |
| `MagicalGirlElementalAttack` | MagicalGirlSpawner | `mgSpawner.SetElement(element)` |
| `MagicalGirlElementalEnhanced` | MagicalGirlSpawner | `mgSpawner.SetElement(element, enhanced=true)` |
| `TarotProc` | TarotSystem | `tarotSystem.SetProcRate(magnitude)` |
| `LuckSlotExpand` | InventoryManager | `inventory.ExpandSlots(1)` |
| `LuckLegendaryGuarantee` | RewardPool | `rewardPool.SetLegendaryGuarantee(true)` |

※ **StatId enum 확장 필요** (CL-140 작업 시):
- `Critical` (치명타 확률)
- `Cooldown` (쿨감)
- `Range` (범위)
- `Dodge` (회피)
- `Defense` (방어력 flat)

기존 7개 → 12개로 확장.

### 4. 의존 시스템 vs 미존재 시스템

본 CL 시점에 **존재하는 시스템**:
- ✅ `PlayerStatModifierContainer` (CL-106)
- ✅ `BuildManager` (CL-139)

**미존재 시스템** (후속 CL에서 만듦):
- ❌ `OnHitEffectRegistry` (CL-142, CL-143에서 신설 예정)
- ❌ `MagicalGirlSpawner` (CL-144, CL-145에서 신설 예정)
- ❌ `TarotSystem` (CL-147에서 신설 예정)
- ❌ `GoldManager` 멀티플라이어 hook (CL-152 통합 시 결정)
- ❌ `RewardPool` 행운 hook (CL-152 통합 시 결정)
- ❌ `InventoryManager.ExpandSlots()` (CL-148/151에서 신설 예정)

→ **CL-140은 라우터 골격만**. 미존재 시스템 case는 **TODO 마커 + Debug.LogWarning** 으로 두고, 후속 CL에서 채움.

```csharp
case RelicEffectType.BurnOnHit:
    // TODO: CL-142에서 OnHitEffectRegistry 연결
    Debug.LogWarning($"[SetEffectApplicator] BurnOnHit not yet handled (CL-142)");
    break;
```

### 5. 인증 게이트

기존 패턴 재활용:

```csharp
private readonly IRelicEffectAuthority _authority = new NetworkRelicEffectAuthority();

private void HandleSetTierChanged(...)
{
    if (!_authority.IsAuthority) return;
    // ...
}
```

### 6. Run 종료 처리

`PlayerRelicInventory.OnCleared` 이벤트 → BuildManager가 모든 티어 -1로 리셋 → SetEffectApplicator가 모든 효과 제거.

본 CL에서:
```csharp
private void HandleRunCleared()
{
    if (!_authority.IsAuthority) return;
    container.RemoveBySource(/* all SetEffectSource */);
    // 미존재 시스템 정리도 후속 CL에서 채움
}
```

### 7. 공개 API (없음)

본 CL은 **이벤트 받기 전용**. 외부 호출 API 없음. 단지 BuildManager 이벤트 구독 + 라우팅.

조회는 BuildManager로 가면 됨 (`GetActiveTier`, `AllActiveTiers` 등).

---

## 핵심 파일

### 신규

| 경로 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Relics/SetEffectApplicator.cs` | 본 ticket 메인 컴포넌트 |

### 수정

| 경로 | 변경 내용 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Combat/StatId.cs` | enum 5개 추가 (Critical, Cooldown, Range, Dodge, Defense) |
| `Assets/_Project/Scripts/Runtime/Combat/PlayerStatModifierContainer.cs` | 추가된 StatId 기본값 처리 (필요 시) |

### 참조 (수정 X)

| 경로 | 사용 목적 |
|---|---|
| `BuildManager.cs` (CL-139) | OnSetTierChanged 이벤트 구독 |
| `BuildSetData.cs` (CL-138) | SetTier 데이터 조회 |
| `RelicEffectType.cs` (CL-138 확장) | switch 디스패치 |

---

## 구현 단계

### 1단계: StatId enum 확장 (5분)

```csharp
public enum StatId
{
    AttackPower,        // 기존
    AttackSpeed,        // 기존
    MoveSpeed,          // 기존
    MaxHealth,          // 기존
    FinisherDamage,     // 기존
    DashCooldown,       // 기존
    HealReceived,       // 기존
    // CL-140 추가
    Critical,           // 치명타 확률
    Cooldown,           // 스킬 쿨감
    Range,              // 공격 범위
    Dodge,              // 회피 확률
    Defense,            // 방어력 (flat)
}
```

추가된 StatId의 실제 적용은 별도 ticket (예: 치명타 적용 로직은 CL-142). 본 CL은 enum만.

### 2단계: SetEffectApplicator 골격 (30분)

- 클래스 선언, SerializeField (statContainer, buildManager 참조)
- OnEnable / OnDisable 이벤트 구독 / 해제
- `SetEffectSource` struct (IEquatable 구현)

### 3단계: HandleSetTierChanged 메인 디스패처 (1시간)

```csharp
private void HandleSetTierChanged(RelicTag tag, int oldTier, int newTier)
{
    if (!_authority.IsAuthority) return;

    BuildSetData set = buildManager.GetSetDataForTag(tag);
    if (set == null) return;

    // 1) 이전 티어 효과 제거
    if (oldTier >= 0)
    {
        var oldSource = new SetEffectSource(set, oldTier);
        statContainer.RemoveBySource(oldSource);
        // 미존재 시스템 제거도 여기서 (후속 CL)
    }

    // 2) 새 티어 효과 적용
    if (newTier >= 0)
    {
        SetTier tier = set.Tiers[newTier];
        DispatchEffect(set, newTier, tier);
    }
}
```

### 4단계: DispatchEffect — 효과 종류별 라우팅 (1시간)

```csharp
private void DispatchEffect(BuildSetData set, int tierIndex, SetTier tier)
{
    var source = new SetEffectSource(set, tierIndex);
    
    switch (tier.EffectType)
    {
        // ── StatModifier 라우팅 ──
        case RelicEffectType.AttackPowerPercent:
            statContainer.AddPermanent(StatId.AttackPower, tier.Magnitude, source);
            break;
        case RelicEffectType.MaxHealthPercent:
            statContainer.AddPermanent(StatId.MaxHealth, tier.Magnitude, source);
            break;
        case RelicEffectType.CriticalChancePercent:
            statContainer.AddPermanent(StatId.Critical, tier.Magnitude, source);
            break;
        // ... (기타 stat 효과들)

        // ── OnHit 라우팅 (TODO: CL-142, CL-143) ──
        case RelicEffectType.BurnOnHit:
        case RelicEffectType.SlowOnHit:
        case RelicEffectType.FreezeOnHit:
        case RelicEffectType.ChainOnHit:
        case RelicEffectType.WindAOE:
            Debug.LogWarning($"[SetEffectApplicator] {tier.EffectType} 미구현 (CL-142/143)");
            break;

        // ── Entity 라우팅 (TODO: CL-144, CL-145) ──
        case RelicEffectType.MagicalGirlSummon:
        case RelicEffectType.MagicalGirlFusion:
        case RelicEffectType.MagicalGirlElementalAttack:
        case RelicEffectType.MagicalGirlElementalEnhanced:
            Debug.LogWarning($"[SetEffectApplicator] {tier.EffectType} 미구현 (CL-144/145)");
            break;

        // ── 시스템 hook 라우팅 (TODO) ──
        case RelicEffectType.GoldGainPercent:
        case RelicEffectType.LuckPoints:
        case RelicEffectType.LuckSlotExpand:
        case RelicEffectType.LuckLegendaryGuarantee:
        case RelicEffectType.TarotProc:
            Debug.LogWarning($"[SetEffectApplicator] {tier.EffectType} 미구현 (CL-146/147)");
            break;

        case RelicEffectType.None:
            // 무효화. 의도된 비-MVP 효과
            break;
    }
}
```

### 5단계: Run 종료 처리 (15분)

```csharp
private void HandleRunCleared()
{
    if (!_authority.IsAuthority) return;
    // BuildManager가 OnSetTierChanged(tag, oldTier, -1) 다 발화하면 자동 정리됨
    // 단 안전망으로 명시적 정리도 추가
    statContainer.ClearAll();  // 또는 source 매칭 정리
}
```

### 6단계: 디버그 로그 + 검증 (15분)

```csharp
[SerializeField] private bool logEffectDispatch = false;

if (logEffectDispatch)
    Debug.Log($"[SetEffect] {tag} tier {oldTier}→{newTier}: applying {tier.EffectType}");
```

---

## 검증 방법 (e2e)

```
1. Unity Editor Play 모드
2. PlayerRelicInventory.Debug — Add all assigned relics 로 6~8개 RelicData 추가
3. Console 로그 확인:
   - [BuildManager] AttackPower: tier 0 → 1 (count=3)        ← CL-139 발화
   - [SetEffect] AttackPower tier 0→1: applying AttackPowerPercent  ← CL-140 발화
   - [StatModifier] AddPermanent AttackPower +10% src=...    ← CL-106 발화
4. Player 공격력이 실제로 증가했는지 데미지 수치로 검증
5. RelicData 1개 제거 → 카운트 감소 → 티어 하락 시:
   - [SetEffect] AttackPower tier 1→0: removing previous, applying tier 0
   - [StatModifier] Removed 1 mods by source=...
6. Run 종료 → 모든 효과 정리
7. 미구현 효과 (불, 미소녀 등) 도달 시 LogWarning 출력 확인 (정상)
```

---

## 위험 / 결정 미정

### 위험
1. **SetEffectSource Equals 비교 정확성**: struct로 IEquatable 구현 필요. 잘못하면 RemoveBySource가 작동 안 함.
2. **이벤트 순서**: BuildManager 가 LateUpdate에서 dirty 처리 → 그 안에서 OnSetTierChanged 발화 → SetEffectApplicator 핸들러 실행. 한 프레임에 여러 티어 변화 시 정상 처리.
3. **StatId 추가가 기존 코드에 영향**: 기존 RelicEffectRegistry는 7개 StatId만 사용. 새 5개 (Critical, Cooldown 등) 사용 안 함 → 영향 없음. 단 enum 인덱스 시프트 X (값 추가만).
4. **TODO 라우팅이 게임플레이에 영향**: 일부 세트 효과 미적용 → 디버깅 시 헷갈림. LogWarning 명확히.

### 결정 미정
- [ ] Critical/Cooldown/Range/Dodge/Defense의 실제 적용 hook 위치
  - Critical: KhiMeleeComboController?
  - Cooldown: 각 Skill 컴포넌트 (스태프 스킬 위주)?
  - Range: 무기별 사거리 컴포넌트?
  - Dodge: PlayerHealth.TryDodge()?
  - Defense: PlayerHealth 데미지 감소?
- [ ] `AddPermanent` 의 magnitude 단위 — 0.05 = 5% 통일 (기존 RelicEffectRegistry와 동일)
- [ ] DefenseFlat은 `magnitude=4` 같은 정수값. AddPermanent의 % 합산 정책과 어떻게 통합? → 별도 hook 또는 StatId.Defense를 flat 처리하는 분기 필요

---

## 후속 ticket 영향

| Ticket | CL-140과의 관계 |
|---|---|
| **CL-141 (75 ItemData)** | 본 CL과 무관 (데이터만) |
| **CL-142 (평타 5세트)** | 본 CL의 TODO 채움 (Critical/AttackSpeed/AttackPower 적용 + Slow/Freeze/Chain on-hit) |
| **CL-143 (스킬 3세트)** | TODO 채움 (Cooldown 적용 + Burn/Wind on-hit) |
| **CL-144 (미소녀 1~4)** | TODO 채움 (MagicalGirlSummon, ElementalAttack 처리) |
| **CL-145 (미소녀 5합체)** | TODO 채움 (MagicalGirlFusion 처리) |
| **CL-146 (공통 7세트)** | TODO 채움 (Health/Defense/Dodge/Range 적용 + Luck/Greed system hook) |
| **CL-147 (타로)** | TODO 채움 (TarotProc) |
| **CL-148 (인벤토리 UI)** | LuckSlotExpand의 InventoryManager hook 제공 |

→ **본 CL이 라우터의 골격을 만들면, 후속 CL들은 각자의 case만 채우면 됨**. 작업 분담 깔끔.

---

## 예상 시간

| 단계 | 시간 |
|---|---|
| 1단계 (StatId 확장) | 5분 |
| 2단계 (SetEffectApplicator 골격) | 30분 |
| 3단계 (HandleSetTierChanged 메인) | 1시간 |
| 4단계 (DispatchEffect 라우팅) | 1시간 |
| 5단계 (Run 종료 처리) | 15분 |
| 6단계 (디버그 + 검증) | 15분 |
| **합계** | **3시간** (티켓 점수 3점에 부합) |

---

## Phase 1 완성 체크리스트 (CL-138 + CL-139 + CL-140)

본 CL 완료 시 Foundation 완성:

- [x] CL-138: 데이터 구조 (RelicData 듀얼 태그 + BuildSetData 16개)
- [x] CL-139: BuildManager (카운트 + 티어 + 이벤트)
- [x] CL-140: SetEffectApplicator (라우터 + StatModifier 적용)

**Foundation의 가치**:
- 후속 ticket들은 각자 시스템(OnHit, MagicalGirl, Tarot 등) 구현 + DispatchEffect의 자기 case 채우기만 하면 됨
- 새 효과 추가 시: enum 값 추가 + DispatchEffect case 추가 + 시스템 구현 → 명확한 패턴

다음 ticket: **CL-141** (75 ItemData 일괄 생성). 본 CL의 RelicData 신 구조에 데이터 채움.
