# CL-138 ItemData/BuildData/SetData SO 구조 정의 — 구현 기록

작성일: 2026-05-04

브랜치: `feat/S14P31C201-382/cl-138-itemdata-builddata-setdata-so`

기준 plan: [cl138_plan.md](cl138_plan.md)

**상태**: 🟢 **코드·자산 마이그레이션 완료 / Unity Editor 1차 시각 검증 통과** —
Console 에러 0, 18개 RelicData asset Inspector에서 신 필드 정상 표시. BuildSets
16개 SO 인스턴스 생성과 런타임 효과 적용 확인은 후속 작업(CL-139 진입 직전).

---

## 목적

Epic S(빌드 시스템) 첫 ticket. 새로 만드는 게 아니라 **CL-106~110(Epic P)에서 단단히
구축된 RelicData 시스템을 회의록 결정대로 확장**.

회의록 변경점 4가지:
1. RelicTag 3개 → 16개 + None
2. 단일 태그 → 듀얼 태그 (1 아이템 = 2 세트 기여)
3. 사이즈 필드 추가 (1×1 / 1×2 / 2×2)
4. 세트 효과 SO(`BuildSetData`) 신설 — 16세트 티어별 효과 정의

본 CL은 **데이터 구조 정의만** 책임. 카운트(CL-139)·효과 적용(CL-140)·77개 ItemData
일괄 생성(CL-141)은 후속.

---

## 설계 기준

- **이름은 기존 `Relic*` 유지** — 기획 용어("ItemData/BuildData/SetData")와 코드 용어
  분리. 18개 SO + 모든 참조 코드 재바인딩 회피.
- **기존 18 SO 직렬화 안전** — `[FormerlySerializedAs]` 로 `_tag → _tagPrimary`,
  `_effectType → _effectTypeLegacy` 등 자동 마이그레이션.
- **단일 효과 → 다중 효과(`EffectEntry[]`)** — V0.4 보완. items_draft.md 77개 중
  대부분이 2개 이상 효과 보유.
- **CL-106 hook 무수정 동작 유지** — `RelicEffectRegistry`가 `relic.EffectType` 등
  단일 property 호출 → deprecated property가 `Effects[0]` 우선, 비어 있으면 legacy
  필드 fallback. 기존 효과 그대로 작동.
- **사이즈는 `Vector2Int` 자유도 유지** — enum 대신 (1×3 같은 변형 가능성). 등급
  별 자동 매핑은 CL-150 으로 미룸.

## 확정된 설계 결정

| 결정 | 값 | 근거 |
|---|---|---|
| RelicTag.None = 0 추가 | 단일 태그 / 소모품 표현 가능 | 회의록의 "16 세트" 외에 None 케이스(랜덤박스 등) 흡수 |
| 듀얼 태그 표현 | `_tagPrimary`/`_tagSecondary` 두 필드 | 항상 2개 강제. 배열보다 Inspector 단순 |
| 다중 효과 표현 | `EffectEntry[] _effects` 배열 + Legacy 4필드 deprecated | items_draft.md V0.4 분석상 다수 아이템이 2+ 효과 |
| Legacy 보존 정책 | `[FormerlySerializedAs]` + `[Obsolete]` property fallback | 18 SO + RelicEffectRegistry 무수정 동작 |
| Tag property | `[Obsolete]` alias for TagPrimary, 호출 사이트 4곳 일괄 교체 | 컴파일 경고 0 목표 |
| 한국어 라벨 | `RelicTagLabels.ToKorean()` 단일 매퍼 신설 | Shop/Reward 4 뷰의 동일 switch 중복 제거 |
| EffectType enum | 기존 11개 + 신규 19개 + None = 31개 | 별도 SetEffectType 분리하지 않음 (CL-139 핸들러 단순화) |
| BuildSetData 16 SO 인스턴스 | Unity Editor 수동 생성 | 16개라 자동 스크립트 부담 < 수동 입력 시간 |

---

## 수정 파일

### 신규 (3개)

| 경로 | 내용 |
|---|---|
| `LostMemory/Assets/_Project/Scripts/Runtime/Relics/EffectEntry.cs` | 다중 효과 struct (Type/Magnitude/Duration/Threshold) |
| `LostMemory/Assets/_Project/Scripts/Runtime/Relics/BuildSetData.cs` | 세트 효과 SO + `SetTier` struct |
| `LostMemory/Assets/_Project/Scripts/Runtime/Relics/RelicTagLabels.cs` | RelicTag → 한국어 매퍼 (Shop/Reward 공유) |

### 코드 수정 (6개)

```text
LostMemory/Assets/_Project/Scripts/Runtime/Relics/RelicTag.cs
    - enum 3개 → 17개 (None=0 + 16세트)

LostMemory/Assets/_Project/Scripts/Runtime/Relics/RelicEffectType.cs
    - 12개 → 31개 (세트 효과용 19개 추가: CriticalChancePercent, CooldownReductionPercent,
      AttackRangePercent, DodgeChancePercent, DefenseFlat, GoldGainPercent, LuckPoints,
      BurnOnHit, SlowOnHit, FreezeOnHit, ChainOnHit, WindAOE, MagicalGirlSummon,
      MagicalGirlFusion, MagicalGirlElementalAttack, MagicalGirlElementalEnhanced,
      TarotProc, LuckSlotExpand, LuckLegendaryGuarantee)

LostMemory/Assets/_Project/Scripts/Runtime/Relics/RelicData.cs
    - 신 필드: _tagPrimary([FormerlySerializedAs("_tag")]), _tagSecondary, _size, _effects[]
    - Legacy 필드: _effectTypeLegacy, _magnitudeLegacy, _durationLegacy, _thresholdLegacy
      (모두 [FormerlySerializedAs] 로 기존 이름 매핑)
    - 신 property: TagPrimary, TagSecondary, Size, Effects
    - [Obsolete] property: Tag, EffectType, Magnitude, Duration, Threshold
      → Effects[0] 우선, 빈 배열이면 legacy 필드 반환

LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopItemView.cs:163
LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopPanelView.cs:166
LostMemory/Assets/_Project/Scripts/Runtime/Shop/TooltipView.cs:97
    - relic.Tag switch (Assault/Guardian/Sprint) → RelicTagLabels.ToKorean(relic.TagPrimary)

LostMemory/Assets/_Project/Scripts/Runtime/Rewards/RewardCardView.cs:35,53
    - 동일 패턴 + ToKoreanTag 헬퍼 제거 (RelicTagLabels 로 통합)
```

### 자산 마이그레이션 (18 RelicData asset)

`Assets/_Project/ScriptableObjects/Relics/` 내 18개 .asset YAML을 plan §부록 매핑표
그대로 일괄 수정.

각 파일에서:
- `_tag: <old>` 라인 → `_tagPrimary: <new>` + `_tagSecondary: <new>` + `_size: {x: W, y: H}`
- `_effectType/_magnitude/_duration/_threshold` → `_effectTypeLegacy/_magnitudeLegacy/...` rename
- `_effects: []` 추가 (빈 배열 → deprecated property가 legacy 필드 fallback)

#### 비-소모품 15개

| asset | _tagPrimary | _tagSecondary | _size |
|---|---|---|---|
| 전사의 끈 | AttackPower (3) | Health (10) | 1×1 |
| 분쇄의 팔찌 | AttackPower (3) | Critical (2) | 1×2 |
| 전투 북 | AttackPower (3) | Health (10) | 1×2 |
| 붉은 송곳니 | AttackSpeed (1) | AttackPower (3) | 1×2 |
| 바람 깃털 | Wind (8) | AttackSpeed (1) | 1×1 |
| 수호의 파편 | Health (10) | Defense (11) | 1×1 |
| 반격의 표식 | Defense (11) | Dodge (12) | 1×2 |
| 철의 깃 | Health (10) | Defense (11) | 1×1 |
| 질풍 장화 | AttackSpeed (1) | Dodge (12) | 1×1 |
| 추적자의 망토 | AttackSpeed (1) | Wind (8) | 1×2 |
| 광전사의 문장 | AttackPower (3) | AttackSpeed (1) | 2×2 |
| 잔상의 목걸이 | Dodge (12) | AttackSpeed (1) | 1×2 |
| 성벽의 궤 | Defense (11) | Health (10) | 2×2 |
| 순환의 방패 핵 | Defense (11) | Cooldown (6) | 2×2 |
| 시공의 파편 | Wind (8) | AttackSpeed (1) | 2×2 |

#### 소모품 3개

| asset | _tagPrimary | _tagSecondary | _size |
|---|---|---|---|
| 작은 회복약 | None (0) | None (0) | 1×1 |
| 큰 회복약 | None (0) | None (0) | 1×1 |
| 랜덤박스 | None (0) | None (0) | 1×1 |

### BuildSets 폴더 신설

`Assets/_Project/ScriptableObjects/BuildSets/` 폴더 생성 + `README.md` (16개 SO 수동
생성 가이드). **SO 인스턴스는 Unity Editor 작업자가 직접 생성** (Create > LostMemory >
Build Set Data × 16, items_draft.md §1 표 입력).

---

## Legacy fallback 동작 설명

`RelicEffectRegistry` 는 CL-106 시점 단일 효과 모델로 작성되어 `relic.EffectType /
Magnitude / Duration / Threshold` 를 직접 호출한다. CL-138 에서 이 4개 property는
deprecated 처리됐지만 다음 fallback 로직으로 **기존 동작 그대로 보존**:

```csharp
public RelicEffectType EffectType =>
    (_effects != null && _effects.Length > 0) ? _effects[0].Type : _effectTypeLegacy;
```

- 18개 기존 SO: `_effects = []` → legacy 필드(`_effectTypeLegacy` 등) 반환 → 기존 효과 동작
- CL-141 이후 신규 SO: `_effects` 배열에 EffectEntry 입력 → `Effects[0]` 반환

→ Unity 빌드 시 **CS0618 obsolete 경고 다수 발생**(RelicEffectRegistry.cs 약 11줄). **정상**.
청소는 plan에서 CL-141 이후 ticket 으로 분리.

---

## 검증

### 1차 (코드)

- [x] 컴파일 에러 0 (Unity Console)
- [x] CS0618 경고만 발생 (RelicEffectRegistry deprecated property 사용 — 의도된 fallback)
- [x] `relic.Tag` / `RelicTag.Assault|Guardian|Sprint` 사이트 0 (`_Project` 내)

### 2차 (Inspector 시각)

- [x] 18개 RelicData asset 클릭 → `Tag Primary` / `Tag Secondary` / `Size` 매핑표 그대로 표시
- [x] Legacy 섹션에 기존 `_effectType / _magnitude / _duration / _threshold` 값 보존

### 3차 (런타임 — 인계 시점에 실행)

- [ ] 기존 유물 1개(예: 전사의 끈) 인벤토리 추가 → 효과 적용 (legacy fallback 경로 증명)
- [ ] Create > LostMemory > Build Set Data 메뉴 동작 → 신규 SO 생성 가능
- [ ] BuildSets 16개 SO 수동 생성 (별도 작업 단계)

---

## 후속 ticket 인계 사항

| Ticket | 본 CL이 남긴 의존 |
|---|---|
| **CL-139** (BuildManager) | `BuildSetData` 16 SO + `RelicData.TagPrimary/TagSecondary` 사용. PlayerRelicInventory.OnRelicAcquired 구독해 듀얼 태그 카운트 → 도달 티어 계산 → 효과 적용 |
| **CL-140** (EffectApplicator) | `SetTier.EffectType` 분기로 PlayerStatModifierContainer 호출. 기존 RelicEffectRegistry 패턴 재활용 가능 (EffectType enum 통합) |
| **CL-141** (77 ItemData 일괄 생성) | `RelicData._effects[]` 배열에 EffectEntry 입력 (단일 효과 SO도 `Effects[0]` 사용 권장 → Legacy 필드 점진적 제거) |
| **CL-150** (사이즈 시스템) | `_size` Vector2Int 활용. 등급 → 사이즈 자동 매핑 helper 구현 |
| **legacy 청소** (CL-141 이후) | RelicEffectRegistry.cs 의 `relic.EffectType` 등을 `relic.Effects[0].Type` 으로 교체. RelicData.cs 의 deprecated property + Legacy 필드 4개 제거 |

## 위험 / 제약

- **전투북.asset** 만 일시적으로 한국어 raw UTF-8 형태로 저장됨 (다른 17개는 `\uXXXX`
  escape). Unity 양쪽 모두 read 가능. 다음 import/save 시 자동 normalize.
- **BuildSets 16개 SO 미생성**: README.md에 명시. Unity Editor 작업 필요.
- **RelicEffectRegistry CS0618 경고**: legacy fallback 의도. CL-141 이후 청소.

## 예상 vs 실제 시간

| 단계 | 예상 (plan) | 실제 |
|---|---|---|
| 1단계 enum 확장 | 15분 | 5분 |
| 2단계 RelicData 수정 | 15분 | 10분 |
| 3단계 BuildSetData 신설 | 30분 (+ 16 SO 수동) | 5분 (SO 생성은 인계) |
| 4단계 18 SO 마이그레이션 | 1~2시간 | 30분 (yaml 직접 편집) |
| 5단계 검증 | 30분 | 10분 (Inspector 시각만) |
| **합계** | **2.5~3.5시간** | **약 1시간** (BuildSet 16 SO 생성·런타임 검증 제외) |
