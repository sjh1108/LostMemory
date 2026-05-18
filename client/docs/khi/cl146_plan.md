# CL-146 — 공통 7세트 효과 (체력/방어/회피/범위 + 행운/탐욕/타로 hook)

## Context

Epic S Phase 3의 다섯 번째 ticket. **공통 카테고리 7세트** 처리.

7세트 분류:
- **Stat 4세트** (값 변경): 체력 / 방어력 / 회피 / 범위
- **System 3세트** (게임 시스템 hook): 행운 / 탐욕 / 타로

기존 시스템 분석:
- ✅ `GoldWallet` (CL-113 구현됨) — Add/Spend/Reset, Changed 이벤트
- ✅ `RewardPool` SO — DrawThree, RarityWeights (60/25/10/5)
- ✅ `PlayerRelicInventory` (CL-110) — TryAdd/Remove/Swap, 슬롯 관리
- ⏳ `TarotSystem` (CL-147에서 신설)
- ❌ Damage receive hook (방어/회피용) — 신규 또는 TDE Health 활용

⚠️ **본 ticket이 5점이지만 실제 6~7점급** (7세트 + 인프라 일부 신설). CL-142 패턴으로 plan만 분할.

---

## 결정사항

### 1. 체력 (MaxHealth) — 검증만

이미 작동:
- StatId.MaxHealth 존재
- PlayerHealthStatApplier가 매 LateUpdate 적용
- SetEffectApplicator (CL-140)에 case 추가만

```csharp
case RelicEffectType.MaxHealthPercent:
    statContainer.AddPermanent(StatId.MaxHealth, tier.Magnitude, source);
    break;
```

**작업**: case 채우기 + BuildSet_체력.asset.

### 2. 방어력 (Defense) — 신규 적용 hook

**StatId.Defense** (CL-140 추가됨), **DefenseFlat** EffectType.

**메커니즘**: 적이 플레이어를 공격할 때 데미지에서 일정 값 차감.

**적용 위치**: TDE Health.Damage() 호출 직전 또는 Damage 메서드 가로챔.

**구현 옵션**:
- (a) **PlayerDamageReceiver 컴포넌트 신설** ⭐
  - TDE Health의 OnHit/OnDamage 이벤트 구독
  - 데미지 처리 전에 -Defense 적용
  - Player에 부착
- (b) Health 자체 수정 (TDE 원본 X — 회피)
- (c) Health 어댑터 패턴 (래퍼)

**채택: (a)**. TDE 안 건드리면서 어댑터로.

```csharp
public class PlayerDamageReceiver : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private PlayerStatModifierContainer container;

    private void OnEnable() => health.OnHit += ModifyIncomingDamage;
    private void OnDisable() => health.OnHit -= ModifyIncomingDamage;

    // TDE Health 가 OnHit 또는 OnTakeDamage 같은 hook 제공한다고 가정
    // 없으면 별도 인터셉트 방식 필요 (TDE 문서 확인 필요)
    private void ModifyIncomingDamage(...)
    {
        float defense = container.GetTotalMultiplier(StatId.Defense) - 1f;
        // -1 한 이유: GetTotalMultiplier는 1 + Σ(modifier) 반환
        // DefenseFlat 의 magnitude는 정수값 (4, 8 등)
        // 합산 정책 변경 필요? -> §결정 미정
    }
}
```

⚠️ **Defense는 flat 값**: `+4`, `+8` 같은 정수. 기존 `GetTotalMultiplier` 정책 (1 + 합산) 과 안 맞음.

**해결**:
- StatModifierContainer에 `GetTotalFlat(StatId)` 메서드 추가 → flat 합산 반환
- 또는 Defense 전용 별도 처리 (다른 세트와 분리)

**채택**: `GetTotalFlat(StatId)` 추가 (PlayerStatModifierContainer 확장).

### 3. 회피 (Dodge) — 신규 적용 hook

**StatId.Dodge** (CL-140 추가), **DodgeChancePercent** EffectType.

**메커니즘**: 데미지 받을 때 dice roll → 회피 성공 시 데미지 0.

**적용 위치**: PlayerDamageReceiver (방어와 같은 위치).

```csharp
private void ModifyIncomingDamage(...)
{
    float dodgeChance = container.GetTotalMultiplier(StatId.Dodge) - 1f;
    if (Random.value < dodgeChance)
    {
        // 회피 성공: 데미지 0
        StopDamage();  // TDE API 활용
        return;
    }

    // 방어 적용
    float defense = container.GetTotalFlat(StatId.Defense);
    float finalDamage = Mathf.Max(0, originalDamage - defense);
    OverrideDamage(finalDamage);
}
```

**시각**: 회피 성공 시 "MISS" 텍스트 (별도 ticket).

### 4. 범위 (AttackRange) — 신규 적용 hook (여러 곳)

**StatId.Range** (CL-140 추가), **AttackRangePercent** EffectType.

**적용 대상**:
- 평타 hitbox 크기 (KhiMeleeHitbox)
- 미소녀 사거리 (MagicalGirlAI.attackRange)
- OnHit 효과 (WindAOE 반경, ChainOnHit 사거리 등)
- 스킬 범위 (CL-160 후)

**구현 패턴**: 각 컴포넌트가 매 사용 시 `GetTotalMultiplier(StatId.Range)` 조회.

```csharp
// KhiMeleeHitbox.Sample 내부
Vector2 baseSize = step.hitboxSize;
float rangeMul = statContainer != null ? statContainer.GetTotalMultiplier(StatId.Range) : 1f;
Vector2 finalSize = baseSize * rangeMul;
// ...Physics2D.OverlapBox(center, finalSize, ...)
```

```csharp
// MagicalGirlAI.FindClosestEnemy 내부
float baseRange = 5f;
float rangeMul = playerStat.GetTotalMultiplier(StatId.Range);
Physics2D.OverlapCircleAll(transform.position, baseRange * rangeMul, enemyLayers);
```

**작업량**: 적용처 4~5곳 수정 (15분 × 5 = 1시간).

### 5. 행운 (LuckPoints) — 4단계 system hook

회의록:
- **1스택**: 행운 아이템 등장률↑ (단계별 증가)
- **3스택**: 인벤토리 슬롯 +1
- **5스택**: 보상 아이템 2개 동시 픽 가능
- **7스택**: 무조건 레전드리

**5-1: 행운 1스택 (행운템 등장률↑)**
- "행운 아이템" = 운명 테마 아이템들 (LuckPoints 태그 가진 것)
- RewardPool에 luck multiplier 추가
- DrawThree 시 LuckPoints 태그 가진 아이템 가중치 ↑

```csharp
// RewardPool.DrawThree 수정
public List<RelicData> DrawThree(..., int luckPoints = 0)
{
    foreach (var item in available)
    {
        int weight = GetWeight(item.Rarity);
        if (item.HasLuckTag()) weight += luckPoints * 2;  // 행운 1당 +2 가중치
        // ...
    }
}
```

→ RewardController가 호출 시 BuildManager에서 luck count 가져와서 전달.

**5-2: 행운 3스택 (슬롯 +1)**
- 인벤토리 슬롯 동적 확장
- 현재 PlayerRelicInventory는 List 기반, 슬롯 명시적 X
- **신규**: 슬롯 개념 도입 또는 인벤토리 max size 추가

```csharp
public class PlayerRelicInventory
{
    [SerializeField] private int baseMaxSlots = 25;  // 5×5 인벤토리

    public int MaxSlots => baseMaxSlots + _bonusSlots;
    private int _bonusSlots;

    public void AddSlots(int amount) { _bonusSlots += amount; }
    public void RemoveSlots(int amount) { _bonusSlots -= amount; }
}
```

CL-148 (인벤토리 UI)와 통합 필요.

**5-3: 행운 5스택 (보상 2개)**
- RewardController 수정: 보상 선택 후 한 번 더 선택 허용
- 또는 보상 카드 5장 표시 → 2장 선택

```csharp
public class RewardController
{
    public int PicksAllowed { get; private set; } = 1;

    public void SetPicksAllowed(int amount) { PicksAllowed = amount; }

    private void OnCardSelected(...)
    {
        _picksMade++;
        if (_picksMade >= PicksAllowed) ClosePanel();
        // else: 카드 1장 disable, 나머지 선택 대기
    }
}
```

**5-4: 행운 7스택 (무조건 전설)**
- RewardPool에 forceLegendary flag
- DrawThree 시 모두 Legendary만 후보

```csharp
public class RewardPool
{
    public bool ForceLegendary { get; set; }

    public List<RelicData> DrawThree(...)
    {
        if (ForceLegendary)
        {
            available = available.Where(r => r.Rarity == RelicRarity.Legendary).ToList();
        }
        // ...
    }
}
```

### 6. 탐욕 (GoldGainPercent) — 골드 multiplier

**메커니즘**: GoldWallet.Add() 호출 시 multiplier 적용.

**구현**:
```csharp
public class GoldWallet
{
    private float _gainMultiplier = 1f;

    public void SetGainMultiplier(float mul) { _gainMultiplier = mul; }

    public void Add(int amount)
    {
        amount = Mathf.RoundToInt(amount * _gainMultiplier);
        // 기존 로직
    }
}
```

SetEffectApplicator → SetGainMultiplier(1 + magnitude) 호출.

탐욕 추가 효과 ("상점 아이템 추가") — Shop 시스템과 통합 필요. **MVP는 골드만**, 상점 추가는 별도 ticket.

### 7. 타로 (TarotProc) — Hook only (실제 시스템 CL-147)

본 CL은 hook만:
```csharp
case RelicEffectType.TarotProc:
    if (tarotSystem != null)
        tarotSystem.SetProcRate(tier.Magnitude);
    else
        Debug.LogWarning("[CL-146] TarotSystem 미존재 (CL-147에서 구현)");
    break;
```

CL-147에서 TarotSystem.cs 신설하고 CL-146의 hook 활성화.

---

## 작업 단위 가이드 (Plan 분할, Ticket 단일)

> ticket은 **CL-146 단일**, plan에서만 작업 단위로 분할.

### 작업 단위 A (1.5~2시간) — Stat 4세트
- 체력 / 방어력 / 회피 / 범위
- PlayerDamageReceiver 신설
- StatModifierContainer.GetTotalFlat 추가
- Range 적용 (4~5곳)
- BuildSetData 4개

### 작업 단위 B (2.5~3시간) — System 3세트
- 행운 4단계 (RewardPool 가중치 / InventorySlot / RewardController / ForceLegendary)
- 탐욕 (GoldWallet multiplier)
- 타로 hook (placeholder)
- BuildSetData 3개

---

## 핵심 파일

### 신규

| 경로 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Combat/PlayerDamageReceiver.cs` | 방어/회피 어댑터 |
| `Assets/_Project/ScriptableObjects/BuildSets/BuildSet_체력.asset` | 체력 set |
| `Assets/_Project/ScriptableObjects/BuildSets/BuildSet_방어력.asset` | 방어 set |
| `Assets/_Project/ScriptableObjects/BuildSets/BuildSet_회피.asset` | 회피 set |
| `Assets/_Project/ScriptableObjects/BuildSets/BuildSet_범위.asset` | 범위 set |
| `Assets/_Project/ScriptableObjects/BuildSets/BuildSet_행운.asset` | 행운 set (4단계) |
| `Assets/_Project/ScriptableObjects/BuildSets/BuildSet_탐욕.asset` | 탐욕 set |
| `Assets/_Project/ScriptableObjects/BuildSets/BuildSet_타로.asset` | 타로 set (placeholder) |

### 수정

| 경로 | 변경 내용 |
|---|---|
| `PlayerStatModifierContainer.cs` | `GetTotalFlat(StatId)` 메서드 추가 (Defense용) |
| `PlayerRelicInventory.cs` (CL-110) | `MaxSlots`, `AddSlots(int)` 추가 (행운 3스택) |
| `RewardPool.cs` | `LuckPoints` 가중치 + `ForceLegendary` flag 추가 |
| `RewardController.cs` | `PicksAllowed` 다중 선택 (행운 5스택) |
| `GoldWallet.cs` | `SetGainMultiplier(float)` 추가 |
| `KhiMeleeHitbox.cs` | Range multiplier 적용 |
| `MagicalGirlAI.cs` (CL-144) | Range multiplier 적용 |
| `OnHitEffectRegistry.cs` (CL-142) | WindAOE/Chain의 반경에 Range 적용 |
| `SetEffectApplicator.cs` (CL-140) | 7개 case 채움 |

---

## 구현 단계

### 단위 A: Stat 4세트

#### 1단계: GetTotalFlat 추가 (15분)
PlayerStatModifierContainer에 flat 합산 메서드:
```csharp
public float GetTotalFlat(StatId stat)
{
    float sum = 0;
    foreach (var m in _permanent) if (m.Stat == stat) sum += m.Magnitude;
    foreach (var m in _timed) if (m.Stat == stat) sum += m.Magnitude;
    foreach (var m in _conditional) if (m.Stat == stat && m.Predicate()) sum += m.Magnitude;
    return sum;
}
```

#### 2단계: PlayerDamageReceiver (30분)
- TDE Health 의 OnHit/OnTakeDamage 이벤트 구독 (TDE 문서 확인 후 정확한 이름 결정)
- 회피 roll → 회피 시 데미지 무효화
- 방어 적용 → 데미지 - Defense flat
- 발동 순서: 회피 → 방어

#### 3단계: Range 적용 (1시간)
- KhiMeleeHitbox.Sample 수정
- MagicalGirlAI.FindClosestEnemy 수정
- OnHitEffectRegistry.WindAOE/Chain 반경 수정
- PlayerStatModifierContainer.GetTotalMultiplier(StatId.Range) 호출

#### 4단계: BuildSetData (Stat 4개) 작성 (15분)

#### 5단계: SetEffectApplicator case (15분)

### 단위 B: System 3세트

#### 6단계: GoldWallet multiplier (15분)
SetGainMultiplier 추가, SetEffectApplicator → 호출.

#### 7단계: 행운 1스택 — RewardPool 가중치 (30분)
DrawThree에 luckPoints 파라미터 추가 + LuckTag 가중치 보너스. RewardController가 호출 시 전달.

#### 8단계: 행운 3스택 — 인벤토리 슬롯 (30분)
PlayerRelicInventory에 MaxSlots 도입. CL-148 (인벤토리 UI)에서 시각적 적용.

#### 9단계: 행운 5스택 — 다중 선택 (30분)
RewardController.PicksAllowed 도입.

#### 10단계: 행운 7스택 — ForceLegendary (15분)
RewardPool.ForceLegendary flag.

#### 11단계: 타로 hook (15분)
TarotSystem 미존재 → LogWarning + TODO.

#### 12단계: BuildSetData (System 3개) (15분)
행운은 4단계 (LuckPoints, LuckSlotExpand, LuckPicksDouble, LuckLegendaryGuarantee).

#### 13단계: 검증 (1시간)

```
시나리오 1: 체력 4스택 → 체력 +60% 적용
시나리오 2: 방어력 4스택 → 적 데미지 -8 적용
시나리오 3: 회피 4스택 → 25% 회피 발동
시나리오 4: 범위 5스택 → 평타 hitbox 2배 / 미소녀 사거리 2배
시나리오 5: 행운 7스택 → 다음 보상 모두 전설
시나리오 6: 탐욕 3스택 → 골드 +30% 적용
시나리오 7: 타로 카운트 → LogWarning만 출력 (CL-147 후 활성)
```

---

## 위험 / 결정 미정

### 위험
1. **TDE Health OnHit/OnDamage 정확한 hook 이름**: TDE 문서 확인 필요. 이벤트 이름 다르면 PlayerDamageReceiver 구현 변경.
2. **Defense flat vs % 합산 정책 충돌**: GetTotalFlat 추가로 분리하지만 기존 코드와 혼용 시 헷갈림.
3. **행운 5스택 (다중 선택) UI**: 보상 화면 변경 큼. RewardPanelView 수정 필요. 단순 +1 picks 아님.
4. **MaxSlots와 인벤토리 UI 동기화**: CL-148에서 함께 검토. CL-146 단계에서는 데이터만.
5. **Range가 미소녀에 적용되어야 하는지**: 미소녀 자체가 빌드의 일부 → 적용 권장. 단 일관성 확인.

### 결정 미정
- [ ] DefenseFlat 합산 정책 (덧셈 vs 곱셈) — **덧셈** (4 + 4 = +8)
- [ ] 회피 시각 (MISS 텍스트) — 별도 ticket
- [ ] 행운 5스택 UI 변경 (5장 중 2장 vs 3장 중 2장) — **5장 중 2장** 추천
- [ ] 행운 1스택 가중치 boost 양 — luckPoints × 2 (조정 가능)
- [ ] 탐욕 "상점 아이템 추가" — MVP에서 제외 (별도 ticket)

---

## 후속 ticket 영향

| Ticket | CL-146과의 관계 |
|---|---|
| **CL-147 (타로)** | TarotProc hook 본 CL에서 placeholder, CL-147에서 실 시스템 활성 |
| **CL-148 (인벤토리 UI)** | 본 CL의 MaxSlots 도입 → UI에서 동적 슬롯 표시 |
| **CL-149 (툴팁)** | 무관 |
| **CL-152 (보상/상점 통합)** | 본 CL의 RewardController 수정과 연동 |
| **CL-153 (QA)** | 7세트 검증 |

---

## 예상 시간

### 단위 A (Stat 4세트)
| 단계 | 시간 |
|---|---|
| GetTotalFlat | 15분 |
| PlayerDamageReceiver | 30분 |
| Range 적용 | 1시간 |
| BuildSetData 4개 | 15분 |
| SetEffectApplicator case | 15분 |
| **A 합계** | **약 2시간** |

### 단위 B (System 3세트)
| 단계 | 시간 |
|---|---|
| GoldWallet multiplier | 15분 |
| 행운 1스택 (가중치) | 30분 |
| 행운 3스택 (슬롯) | 30분 |
| 행운 5스택 (다중 선택) | 30분 |
| 행운 7스택 (ForceLegendary) | 15분 |
| 타로 hook | 15분 |
| BuildSetData 3개 | 15분 |
| **B 합계** | **약 2.5시간** |

| 검증 | 1시간 |
| **전체 합계** | **약 5.5시간** |

→ 5점 ticket 적정 (CL-142와 비슷한 규모).

---

## 결정 요청

| # | 질문 | 옵션 | 추천 |
|---|---|---|---|
| 1 | DefenseFlat 합산 | 덧셈 / 곱셈 | **덧셈** |
| 2 | 회피 시각 (MISS 텍스트) | 본 CL / 별도 ticket | **별도** (시각 폴리싱) |
| 3 | 행운 5스택 UI | 5장 중 2장 / 3장 중 2장 | **5장 중 2장** |
| 4 | 행운 1스택 가중치 boost | luckPoints × 1 / × 2 / × 3 | **× 2** |
| 5 | 탐욕 상점 아이템 추가 | MVP 포함 / 별도 ticket | **별도** |

전부 추천대로면 **덧셈 + MISS 별도 + 5장중2장 + ×2 + 상점추가 별도**.

---

## Phase 3 진행률 (CL-146 후)

| Ticket | Plan |
|---|---|
| CL-142 평타 5세트 | ✅ |
| CL-143 스킬 3세트 | ✅ |
| CL-144 미소녀 1~4 | ✅ |
| CL-145 미소녀 5합체 | ✅ |
| **CL-146 공통 7세트** | ✅ ← 방금 |
| CL-147 타로 | ⏳ P2 |

**Phase 3: 5/6 (P0/P1 모두 완성, P2만 남음)**

---

## 다음 plan

| 옵션 | Ticket | 점수 | 비고 |
|---|---|---|---|
| **A** | CL-147 타로 | 5점 P2 | Phase 3 마무리, 메이저 아르카나 8장 |
| B | CL-148 인벤토리 UI | 3점 P0 | Phase 4 진입, B-Lite 1 인벤토리 |
| C | 일단 plan 정리 끝 | - | Phase 4까지 다 plan 작성했으니 코딩 진입 준비 |

**추천: B** (CL-148 Phase 4). 타로는 P2니까 우선순위 낮음. Phase 4 인벤토리 UI가 데모 시연에 더 필요.

뭐로 갈까요?
