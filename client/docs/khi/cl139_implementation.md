# CL-139 BuildManager 구현 — 구현 기록

작성일: 2026-05-04

브랜치: `feat/S14P31C201-383/cl-139-build-manager-구현`

기준 plan: [cl139_plan.md](cl139_plan.md), 사전 작업 plan: [`~/.claude/plans/139-plan-compiled-eclipse.md`](C:/Users/SSAFY/.claude/plans/139-plan-compiled-eclipse.md)

**상태**: 🟢 **코드·검증 완료** — Unity Editor Play 테스트에서 카운트 정확도, 티어
양방향 발화(상승·하락), Run Clear 다중 발화 모두 검증. 추가로 **CL-107~109 잠재 버그
(TryAdd OnRelicAcquired 누락) 동시 해소** — RelicEffectRegistry 가 이번에 처음으로
실제 동작 확인됨.

---

## 목적

Epic S Phase 1 의 두 번째 ticket. CL-138 데이터 구조(`RelicData` 듀얼 태그 + 16
`BuildSetData`)를 **런타임에서 사용하는 첫 매니저** 구축.

본 CL 책임: 보유 RelicData 듀얼 태그 카운트 → 각 세트의 활성 티어 산정 → 변화 시
이벤트 발화. **효과 적용은 X** (CL-140 EffectApplicator 책임).

---

## 설계 기준

- **단일 책임 원칙**: BuildManager 는 카운트·티어 산정·이벤트만. 효과 적용 분기 없음.
- **기존 RelicEffectRegistry 패턴 재활용**: `[DisallowMultipleComponent]` +
  `IRelicEffectAuthority` 게이트 + `OnEnable/OnDisable` 이벤트 구독·해제.
- **Dirty 마커 + LateUpdate 1회 재계산**: 한 프레임에 N번 변화 시 중복 계산 방지.
  보상 다중 선택 시나리오 대비.
- **데이터 SerializeField 명시 할당**: `Resources.LoadAll` 자동 로드 대신 16 SO 를
  Inspector 드래그. 누락 검출 즉시 가능 (Awake 경고).
- **legacy 호환**: PlayerRelicInventory 의 기존 `OnRelicAcquired`, `OnCleared` 그대로
  사용. `OnRelicRemoved` 만 신규 추가 (1줄 invoke).

## 확정된 설계 결정

| 결정 | 값 | 근거 |
|---|---|---|
| 카운트 정책 | TagPrimary == TagSecondary 면 +2 | 단일 강한 빌드용 아이템 가능 (현 풀에는 없으나 확장성) |
| Dirty 정책 | LateUpdate 1회 재계산 | 한 프레임 N개 추가 시 중복 계산 방지 |
| 티어 정책 | 최고 단계만 활성 (`-1`=미발동, `0+`=인덱스) | 회의록 결정 |
| 빈 스택 | 효과 없음 | 다음 임계치 도달해야 발동 |
| 권위 게이트 | `NetworkRelicEffectAuthority` 재사용 | 기존 패턴 일관성 |
| 16 SO 로드 | SerializeField + Awake 검증 | Resources.LoadAll 보다 누락 검출 쉬움 |
| OnSetTierChanged 시그니처 | `Action<RelicTag, int oldTier, int newTier>` | XML 주석에 -1 의미 명시 |
| OnEnable 1회 강제 재계산 | `_isDirty = true` | 컴포넌트 활성화 시점에 인벤토리에 이미 유물이 있을 수 있음 |
| 단위 테스트 | 본 CL 생략 | 프로젝트에 Tests asmdef 자체 없음 (별도 ticket) |

---

## 수정 파일

### 신규 (1)

| 경로 | 내용 |
|---|---|
| `LostMemory/Assets/_Project/Scripts/Runtime/Relics/BuildManager.cs` | 본 ticket 메인 — 카운트·티어 산정·이벤트 발화 |

### 수정 (1)

```text
LostMemory/Assets/_Project/Scripts/Runtime/Relics/PlayerRelicInventory.cs
    - OnRelicRemoved 이벤트 신규 선언 (라인 23)
    - Remove() 내부 OnRelicRemoved?.Invoke(relic) 호출 추가 (라인 80)
    - TryAdd() 내부 OnRelicAcquired?.Invoke(relic) 호출 추가 (라인 58)
      ↑ CL-107~109 잠재 버그 fix (#1) — 본 CL 작업 중 발견
    - Debug — Clear inventory ContextMenu 추가 (라인 124)
      ↑ CL-139 검증 보조용
```

### Unity Editor 작업

`TestKhi_MinimalCharacter2D.prefab` 에 `BuildManager` 컴포넌트 부착 + Inventory 슬롯 +
`_setDatabase` 16 SO 드래그 + `_logTierChanges` ✅. (`TestKhi_Net_AD.prefab` 과
`Dungeon.unity` scene instance 의 wiring 은 향후 작업 시 적용 예정.)

`BuildSets/*.asset` × 16 SO 인스턴스 신규 생성. 본 검증에서는 `BuildSet_AttackPower`,
`BuildSet_AttackSpeed` 두 개의 `Tiers` 배열에 `RequiredCount = 2/4/6` 만 입력.
나머지 14 SO 의 Tiers 본격 채우기는 CL-140·141 작업과 병행 예정.

---

## 발견·해소된 잠재 버그 — `PlayerRelicInventory.TryAdd` 의 `OnRelicAcquired` 누락

**증상**: BuildManager 가 정상 wiring 되고 OnEnable 에서 구독했음에도 유물 추가 시
카운트 갱신 0. 진단 로그 결과 `HandleInventoryChanged` 호출 0건이었으나
`HandleInventoryCleared` 는 정상 호출 → 이벤트 자체가 발화되지 않음을 확인.

**원인**: `PlayerRelicInventory.TryAdd` 가 `_ownedRelics` 에는 추가하지만
`OnRelicAcquired?.Invoke(relic)` 호출이 누락. 이벤트 선언만 있고 발화 코드가 0건.

**파급**: CL-107~109 의 `RelicEffectRegistry.HandleAcquired` 도 사실은 한 번도 호출된
적이 없었음. 즉 보상 카드로 전사의 끈 받아도 공격력 +5% 가 실제로는 적용되지 않음.

**수정**: `TryAdd` 성공 분기에 한 줄 추가:
```csharp
OnRelicAcquired?.Invoke(relic);
```

**수정 후 검증 로그** (CL-107~109 효과 처음 동작 확인):
```
[StatModifier] AddPermanent AttackPower +5.0% src=RelicData_전사의끈
[StatModifier] AddConditional AttackPower +10.0% src=RelicData_전투북
[StatModifier] AddPermanent MoveSpeed +8.0% src=RelicData_바람깃털
[StatModifier] AddPermanent MaxHealth +12.0% src=RelicData_수호의파편
[StatModifier] AddPermanent HealReceived +25.0% src=RelicData_철의깃
[StatModifier] AddPermanent DashCooldown -12.0% src=RelicData_질풍장화
[StatModifier] AddPermanent MoveSpeed +20.0% src=RelicData_추적자의망토
```

**밸런스 영향 주의**: 게임 밸런스가 "효과 미적용" 기준으로 맞춰져 있었다면 갑자기
강해 보일 수 있음. 의도된 정상화이지만 플레이테스트 시 체감 차이 모니터링 필요.

---

## 검증 결과 (e2e)

### 시나리오
1. Play 진입 (Dungeon scene)
2. PlayerRelicInventory ContextMenu `Debug — Add all assigned relics` → 11개 시도
   (10 유물 + 1 소모품 라우팅 + 1 중복 거부)
3. BuildManager ContextMenu `Debug — Print all counts/tiers`
4. PlayerRelicInventory ContextMenu `Debug — Clear inventory` (CL-139 검증용 신규)

### 1. OnEnable wiring 확인 ✅
```
[BuildManager] OnEnable — inventory 구독 시작.
  host=TestKhi_MinimalCharacter2D, inventoryHost=TestKhi_MinimalCharacter2D, ownedCount=0
```

### 2. 티어 양방향 발화 ✅
```
[BuildManager] AttackSpeed: tier -1 → 1 (count=4)   ← Add 후 점프
[BuildManager] AttackPower: tier -1 → 1 (count=4)
... (Tiers 채운 2개 세트만)

[BuildManager] AttackSpeed: tier 1 → -1 (count=0)   ← Clear 후 다중 -1
[BuildManager] AttackPower: tier 1 → -1 (count=0)
```

`-1 → 1` 점프는 dirty 배칭으로 인한 정상 동작 (한 프레임에 4 add 후 LateUpdate 1회만
실행되며 임계치 2/4 동시 통과 → tier 인덱스 1 도착).

### 3. 카운트 정확도 — 검산 100% 일치 ✅

`Debug — Print all counts/tiers` 결과:

| 태그 | 검산 | 실측 |
|---|---|---|
| AttackSpeed | 붉은송곳니+바람깃털+질풍장화+추적자의망토 | **4** ✅ |
| AttackPower | 전사의끈+붉은송곳니+분쇄의팔찌+전투북 | **4** ✅ |
| Health | 전사의끈+전투북+수호의파편+철의깃 | **4** ✅ |
| Defense | 수호의파편+철의깃+반격의표식 | **3** ✅ |
| Wind | 바람깃털+추적자의망토 | **2** ✅ |
| Dodge | 질풍장화+반격의표식 | **2** ✅ |
| Critical | 분쇄의팔찌 | **1** ✅ |
| 나머지 9 세트 | (보유 유물 없음) | **0** ✅ |

### 4. 소모품 라우팅·중복 처리 ✅
- 큰회복약 (`IsConsumable=true`) → 인벤토리 미등록 (`소모품은 단축키바로 라우팅`)
  → 카운트 미반영
- 철의 깃 두 번째 시도 → `이미 보유 중` 거부 → 카운트 중복 안 됨

### 5. CL-107~109 효과 적용 ✅ (보너스)
TryAdd 버그 fix 결과 RelicEffectRegistry 가 처음으로 실제 동작. 7개 유물의 효과가
PlayerStatModifierContainer 에 정상 등록됨 (위 §발견 버그 섹션 로그 참조).

---

## 후속 인계

| Ticket | 본 CL과의 관계 |
|---|---|
| **CL-140 EffectApplicator** | `BuildManager.OnSetTierChanged` 구독 → 이전 티어 효과 제거 + 새 티어 효과 적용. 본 CL 의 이벤트가 핵심 인터페이스 |
| **CL-141 (75 ItemData 일괄)** | 무관 (RelicData 구조만 사용) |
| **CL-148 인벤토리 UI** | `BuildManager.AllCounts`, `AllActiveTiers` 조회 → 빌드 진척 표시 |
| **BuildSets 16 SO Tiers 본격 입력** | 별도 ticket 또는 CL-140 작업과 병행. 현재 2 SO 만 임시값 입력된 상태 |
| **`TestKhi_Net_AD.prefab` + `Dungeon.unity` 의 BuildManager wiring** | 작업 시 동일 패턴으로 부착. 현재는 `TestKhi_MinimalCharacter2D` 만 작업됨 |
| **legacy 청소 (CL-141 이후)** | RelicEffectRegistry.cs 의 `relic.EffectType` 등 deprecated property → `relic.Effects[0].Type` 으로 교체 + RelicData 의 Legacy 필드 제거 |

## 위험 / 제약

- **밸런스 변동**: TryAdd OnRelicAcquired 발화가 처음으로 실제 작동 → CL-107~109 의
  유물 효과가 "이번 빌드부터" 진짜 적용. 게임 체감 강해질 수 있음.
- **BuildSets 14 SO Tiers 미입력**: CL-140 작업 전에 채워야 EffectApplicator 가
  동작 가능.
- **OnEnable diagnostic 로그**: 한 줄짜리 wiring 확인용. 정식 운영 전에 제거 또는
  `[Conditional("UNITY_EDITOR")]` 처리 검토.
- **RelicEffectRegistry CS0618 경고**: CL-138 부터 deprecated property 사용 중.
  legacy 청소 ticket 에서 같이 정리 예정.

## 예상 vs 실제 시간

| 단계 | 예상 (plan) | 실제 |
|---|---|---|
| 1단계 OnRelicRemoved | 5분 | 3분 |
| 2단계 BuildManager 작성 | 40분 | 30분 |
| 3단계 prefab/scene wiring | 15분 | 15분 |
| 4단계 검증 | 15분 | **약 60분** (TryAdd 잠재 버그 진단·수정 포함) |
| **합계 (코드)** | **75분** | **약 110분** |

진단·수정 시간이 plan 대비 길어진 이유: TryAdd 의 OnRelicAcquired 누락은 plan 단계에서
예상 못 한 잠재 버그. 단계적 진단 로그 추가 → 재테스트 → 원인 특정 → fix 의 사이클로
약 30분 추가 소요. 결과적으로 CL-107~109 의 효과 적용 경로까지 함께 정상화된 가치
있는 작업이 됨.
