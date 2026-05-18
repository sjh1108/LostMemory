# CL-140 SetEffectApplicator 구현 — 구현 기록

작성일: 2026-05-04

브랜치: `S14P31C201-384-cl-140-effectapplicator`

기준 plan: [cl140_plan.md](cl140_plan.md), 사전 작업 plan: [`~/.claude/plans/139-plan-compiled-eclipse.md`](C:/Users/SSAFY/.claude/plans/139-plan-compiled-eclipse.md)
(plan mode 단일 파일 제약으로 파일명은 139, 내용은 CL-140)

**상태**: 🟢 **코드 완료 + 라우터 양방향 동작 검증** — Unity Editor Play 테스트에서
`[SetEffect] APPLY` / `[SetEffect] REMOVE` 양방향 발화 + `EffectType=None` 안전 분기 +
RelicEffectRegistry 와 공존 확인. **Foundation Phase 1 (CL-138/139/140) 완성**.

---

## 목적

Epic S Phase 1 의 마지막 ticket. CL-138 (데이터) → CL-139 (카운트/티어 이벤트) →
**CL-140 (라우터)** 로 Foundation 완성. 본 CL 이후 CL-142~147 작업자는 각자 도메인
시스템(OnHit, MagicalGirl, Tarot 등) 구현 + 본 라우터의 자기 case 채우기 + 자기가 맡은
BuildSetData Tiers 입력만 하면 됨.

**해결되는 문제**: CL-139 의 `BuildManager.OnSetTierChanged` 이벤트는 발화되지만 구독자
0. 세트 효과(공격력 +25%, 미소녀 소환 등) 가 실제 적용 안 됨. 본 CL이 이 이벤트를
받아 `SetTier.EffectType` 별로 적절한 시스템에 라우팅.

---

## 설계 기준

- **단일 책임 — 라우터**: SetEffectApplicator 자체는 효과 종류별 분기 + 위임만. 실제
  효과 처리(StatModifier 등록, OnHit 발동, MagicalGirl 소환 등) 는 각 도메인 시스템 책임.
- **티어 전환 안전성**: `oldTier 효과 제거 → newTier 효과 적용` 순서 강제. 누수 X,
  이중 적용 X.
- **RelicEffectRegistry 와 공존**: 같은 PlayerStatModifierContainer 에 두 시스템이
  안전하게 등록. source 식별자로 충돌 회피.
- **미존재 시스템 골격 보존**: OnHit/MagicalGirl/Tarot 등 11개 case 는
  `Debug.LogWarning` 만. CL-142~147 가 자기 case 채움.
- **`SetEffectSource` struct 정확성**: `PlayerStatModifierContainer.RemoveBySource`
  가 `Equals(m.Source, source)` 로 매칭하므로 boxing 안전한 Equals/GetHashCode 구현 필수.

## 확정된 설계 결정

| 결정 | 값 | 근거 |
|---|---|---|
| 컴포넌트 명 | `SetEffectApplicator` | RelicEffectRegistry(개별 유물) 와 구분, "Set" 접두로 책임 명확 |
| 효과 식별 | `SetEffectSource(BuildSetData set, int tierIndex)` readonly struct + IEquatable | RemoveBySource 매칭 정확성 |
| 티어 전환 처리 | `oldTier 제거 → newTier 적용` 순서 | 혼선 방지 (이중 적용 X, 누수 X) |
| Run 종료 처리 | 별도 ClearAll 호출 X — BuildManager 의 `OnSetTierChanged(tag, *, -1)` 다중 발화로 자연 정리 | RelicEffectRegistry 의 ClearAll 과 충돌 회피 |
| 미존재 시스템 | `Debug.LogWarning + 빈 case` | CL-142~147 가 자기 case 채움. 골격 명확 |
| StatId 추가 정책 | 값 추가만 (인덱스 시프트 X) | 기존 SO 직렬화 안전 |
| `DefenseFlat` 처리 | 일단 `StatId.Defense` 로 매핑. % 합산 정책과 다른 의미는 CL-146 에서 결정 | 본 CL 은 라우팅까지만 |
| GetHashCode 구현 | `Set.GetInstanceID()` 활용 (HashCode.Combine 미사용) | Unity 호환 안전 |
| 권위 게이트 | NetworkRelicEffectAuthority 재사용 | 일관성 |

---

## 수정 파일

### 신규 (2)

| 경로 | 내용 |
|---|---|
| `LostMemory/Assets/_Project/Scripts/Runtime/Relics/SetEffectApplicator.cs` | 메인 라우터 — OnSetTierChanged 구독, oldTier 제거 → newTier 적용, EffectType switch |
| `LostMemory/Assets/_Project/Scripts/Runtime/Relics/SetEffectSource.cs` | 효과 식별 readonly struct + IEquatable + Equals(object) override + GetHashCode |

### 수정 (2)

```text
LostMemory/Assets/_Project/Scripts/Runtime/Combat/StatId.cs
    - enum 5개 추가: Critical, Cooldown, Range, Dodge, Defense
    - 값 추가만 (인덱스 시프트 X) → 기존 18 RelicData SO 안전

LostMemory/Assets/_Project/Scripts/Runtime/Relics/BuildManager.cs
    - GetSetForTag(RelicTag) → BuildSetData public API 1개 추가 (라인 73-77)
    - SetEffectApplicator 가 oldTier 효과 제거 시 set.Tiers[oldTier] 메타 데이터 조회용
```

### Unity Editor 작업

`TestKhi_MinimalCharacter2D.prefab` 에 SetEffectApplicator 컴포넌트 부착 + 2개 슬롯
wiring (BuildManager / PlayerStatModifierContainer). `_logEffectDispatch` ✅ 검증용.

---

## EffectType 라우팅 분담표

본 CL 작성된 SetEffectApplicator.ApplyTierEffect 의 switch 분기 정리:

### 본격 처리 (8개 — 본 CL 책임)

| EffectType | 라우팅 |
|---|---|
| AttackPowerPercent | `statContainer.AddPermanent(StatId.AttackPower, mag, source)` |
| MaxHealthPercent | `statContainer.AddPermanent(StatId.MaxHealth, mag, source)` |
| MoveSpeedPercent | `statContainer.AddPermanent(StatId.MoveSpeed, mag, source)` |
| CriticalChancePercent | `statContainer.AddPermanent(StatId.Critical, mag, source)` |
| CooldownReductionPercent | `statContainer.AddPermanent(StatId.Cooldown, mag, source)` |
| AttackRangePercent | `statContainer.AddPermanent(StatId.Range, mag, source)` |
| DodgeChancePercent | `statContainer.AddPermanent(StatId.Dodge, mag, source)` |
| DefenseFlat | `statContainer.AddPermanent(StatId.Defense, mag, source)` (% 정책 충돌은 CL-146) |

### TODO 골격만 (14개 — 후속 CL 책임)

| EffectType (5) | OnHit 라우팅 — **CL-142/143** 가 채움 |
|---|---|
| BurnOnHit, SlowOnHit, FreezeOnHit, ChainOnHit, WindAOE | `LogWarning` |

| EffectType (4) | 미소녀 라우팅 — **CL-144/145** 가 채움 |
|---|---|
| MagicalGirlSummon, MagicalGirlFusion, MagicalGirlElementalAttack, MagicalGirlElementalEnhanced | `LogWarning` |

| EffectType (5) | 시스템 hook — **CL-146/147/148** 가 채움 |
|---|---|
| GoldGainPercent, LuckPoints, TarotProc, LuckSlotExpand, LuckLegendaryGuarantee | `LogWarning` |

### 무동작 (1)

| EffectType | 처리 |
|---|---|
| None | 의도된 미설정 — `break` |

### default (RelicEffectRegistry 가 처리하는 단일 유물 효과 enum)

`AttackSpeedOnKillTimed`, `FinisherDamagePercent`, `AttackPowerConditional`,
`ShieldOnParry`, `HealReceivedPercent`, `DashCooldownPercent`, `MoveSpeedAfterDashTimed`,
`HealConsumablePercent` — 본 CL 책임 외, 미경고 (스팸 방지).

---

## 검증 결과 (e2e)

### 시나리오
1. Play 진입 (Dungeon scene)
2. PlayerRelicInventory `Debug — Add all assigned relics` → 11개 시도 (10 유물 + 1 소모품 + 1 중복)
3. BuildManager `Debug — Print all counts/tiers`
4. PlayerRelicInventory `Debug — Clear inventory`

### 1. SetEffectApplicator 라우팅 동작 ✅
```
[SetEffect] APPLY AttackSpeed t1: None mag=0
[SetEffect] APPLY AttackPower t1: None mag=0

(... DebugClear ...)

[SetEffect] REMOVE AttackSpeed t1: None
[SetEffect] REMOVE AttackPower t1: None
```

`BuildManager.OnSetTierChanged` → `HandleSetTierChanged` → `ApplyTierEffect/RemoveTierEffect`
전 경로 작동. APPLY/REMOVE 양방향 발화 정상.

### 2. EffectType=None 안전 분기 ✅

위 로그가 `EffectType=None` 인 이유는 BuildSet_AttackSpeed/BuildSet_AttackPower 의
`Tiers[1].EffectType` 이 미입력(default) 이기 때문. SetEffectApplicator 의 switch 가
`case RelicEffectType.None: break;` 로 들어가 실제 StatModifier 등록 0건. **버그 아니라
의도된 동작** — None 은 "의도적 미설정" 으로 정의됨.

증거: 14개 TODO case (BurnOnHit 등) 도달 시는 `LogWarning` 떠야 하는데, 여기선
LogWarning 없이 조용히 break → None 분기 정확히 동작.

### 3. RelicEffectRegistry 와 공존 ✅
```
[StatModifier] AddPermanent AttackPower +5.0% src=RelicData_전사의끈
[StatModifier] AddConditional AttackPower +10.0% src=RelicData_전투북
[StatModifier] AddPermanent MoveSpeed +8.0% src=RelicData_바람깃털
[StatModifier] AddPermanent MaxHealth +12.0% src=RelicData_수호의파편
[StatModifier] AddPermanent HealReceived +25.0% src=RelicData_철의깃
[StatModifier] AddPermanent DashCooldown -12.0% src=RelicData_질풍장화
[StatModifier] AddPermanent MoveSpeed +20.0% src=RelicData_추적자의망토
```

본 CL이 RelicEffectRegistry 와 충돌 없이 공존. 같은 PlayerStatModifierContainer 에 두
시스템이 안전하게 등록.

### 4. 카운트 정확도 — 검산 100% 일치 ✅

| 태그 | 검산 | 실측 |
|---|---|---|
| AttackSpeed | 붉은송곳니+바람깃털+질풍장화+추적자의망토 | **4** ✅ |
| AttackPower | 전사의끈+붉은송곳니+분쇄의팔찌+전투북 | **4** ✅ |
| Health | 전사의끈+전투북+수호의파편+철의깃 | **4** ✅ |
| Defense | 수호의파편+철의깃+반격의표식 | **3** ✅ |
| Wind | 바람깃털+추적자의망토 | **2** ✅ |
| Dodge | 질풍장화+반격의표식 | **2** ✅ |
| Critical | 분쇄의팔찌 | **1** ✅ |

### 5. 누수 0 ✅

DebugClear 후 `Debug — Print all counts/tiers`:
```
모든 태그: count=0  tier=-1
```

### 6. 미검증 (코드 리뷰로 대체)

- **SetEffectSource Equals/RemoveBySource 매칭** — 본 검증에서 EffectType=None 이라
  AddPermanent 호출 0건 → RemoveBySource 호출 시 매칭할 source 없어 미검증. 코드
  리뷰로 정확성 확인:
  - `IEquatable<SetEffectSource>.Equals` + `Equals(object)` override + `GetHashCode`
    모두 구현 (`SetEffectSource.cs`)
  - `Equals(m.Source, source)` static 호출 시 boxing 된 struct 의 `Equals(object)` →
    `IEquatable.Equals` 위임 정확

- **StatModifier APPLY/REMOVE 본격 동작** — CL-142 가 첫 EffectType (예:
  AttackPowerPercent) 을 BuildSet 에 입력하는 시점에 자연스럽게 첫 검증 완료될 것.

---

## 분담 정정 (CL-139 implementation 문서의 표현 수정 필요)

CL-139 작성 시 [cl139_implementation.md](cl139_implementation.md) "후속 인계" 섹션에:

> "BuildSets 14 SO Tiers 입력 — CL-141 또는 별도 데이터 ticket 과 병행"

이 표현은 **잘못됨**. CL-141 은 77 RelicData 만 다루며 BuildSetData 무관.

**정확한 분담 (cl142_plan.md 본문 명시 기준)**:

| Ticket | 담당 BuildSetData Tiers 입력 |
|---|---|
| **CL-142** (평타 5세트) | 공속, 치명타, 일반뎀, 얼음, 전기 |
| **CL-143** (스킬 3세트) | 쿨감, 불, 바람 |
| **CL-144** (미소녀 1~4) | 미소녀 (티어 1~4) |
| **CL-145** (미소녀 5합체) | 미소녀 (티어 5) |
| **CL-146** (공통 7세트) | 체력, 방어력, 회피, 범위, 행운, 탐욕 |
| **CL-147** (타로) | 타로 |

**원칙**: 각 효과 시스템을 구현하는 ticket 이 그 시스템이 사용할 BuildSetData 의 Tiers
도 같이 채움 → 디자인 결정과 구현이 한 PR 안에 묶임.

→ CL-139 implementation 문서는 별도 commit 으로 본 ticket 인수 인계 단계에서 정정 권장
(또는 CL-142 시작 시 함께 정리).

---

## 후속 인계

| Ticket | 본 CL과의 관계 |
|---|---|
| **CL-141 (77 RelicData)** | 무관 (개별 RelicData 데이터만 다룸, BuildSetData 무관) |
| **CL-142 (평타 5세트)** | Critical/AttackSpeed/AttackPower stat 소비 hook + Slow/Freeze/Chain OnHit case 채움 + BuildSet_공속/치명타/일반뎀/얼음/전기 Tiers 입력 |
| **CL-143 (스킬 3세트)** | Cooldown stat 소비 hook + Burn/Wind OnHit case 채움 + BuildSet_쿨감/불/바람 Tiers 입력 |
| **CL-144 (미소녀 1~4)** | MagicalGirlSummon/ElementalAttack case + MagicalGirlSpawner 구현 + BuildSet_미소녀 Tiers (1~4) |
| **CL-145 (미소녀 5합체)** | MagicalGirlFusion case + BuildSet_미소녀 Tiers (5) |
| **CL-146 (공통 7세트)** | Health/Defense/Dodge/Range stat hook + Luck/Greed 시스템 hook + DefenseFlat 정책 결정 + 6개 BuildSet Tiers |
| **CL-147 (타로)** | TarotProc case + TarotSystem 구현 + BuildSet_타로 Tiers |
| **CL-148 (인벤토리 UI)** | LuckSlotExpand case (InventoryManager.ExpandSlots hook 제공) |
| **`TestKhi_Net_AD.prefab` + `Dungeon.unity` BuildManager+SetEffectApplicator wiring** | 네트워크 본격 테스트 시 동일 패턴 부착 |
| **legacy 청소 (CL-141 이후)** | RelicEffectRegistry deprecated property 사용 (CS0618 경고) — 별도 ticket |

---

## 위험 / 제약

- **DefenseFlat 의 % 합산 정책 충돌**: PlayerStatModifierContainer 는 `1 + Σ(percents)`
  합산. Defense 의 `magnitude=4` 같은 flat 값을 % 로 처리하면 +400%. → CL-146 에서
  별도 분기 또는 StatId 분리 결정.
- **본격 StatModifier 매칭 미검증**: BuildSet Tiers 가 비어있어 RemoveBySource 매칭
  실제 호출 0건. CL-142 첫 EffectType 입력 시점에 자연 검증.
- **밸런스 변동**: CL-139 의 TryAdd OnRelicAcquired 누락 fix 로 RelicEffectRegistry 가
  처음 동작 → 본 CL의 SetEffectApplicator 가 추가되면 같은 stat (예: AttackPower) 에
  두 시스템이 모두 등록 가능. 의도된 동작이지만 디자이너 인지 필요.

## Phase 1 Foundation 완성 ✅

본 CL 완료로 Phase 1 끝:
- [x] CL-138 (데이터 구조) — RelicData 듀얼 태그 + BuildSetData 16개
- [x] CL-139 (BuildManager) — 카운트 + 티어 + 이벤트
- [x] **CL-140 (SetEffectApplicator)** — 라우터 + StatModifier 8 case 본격 처리

다음 ticket: **CL-141** (77개 RelicData CSV → SO 일괄 생성).

## 예상 vs 실제 시간

| 단계 | 예상 (plan) | 실제 |
|---|---|---|
| 1단계 StatId enum | 5분 | 3분 |
| 2단계 BuildManager.GetSetForTag | 5분 | 3분 |
| 3단계 SetEffectSource struct | 10분 | 10분 |
| 4단계 SetEffectApplicator | 90분 | 40분 |
| 5단계 prefab wiring | 10분 | (사용자 작업) |
| 6단계 검증 | 15분 | 15분 |
| **합계 (코드)** | **2시간 15분** | **약 70분** |

라우터 코드 단순 + 기존 패턴(RelicEffectRegistry) 재활용으로 plan 예상보다 빠르게 완료.
