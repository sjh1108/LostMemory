# CL-138 — ItemData/BuildData/SetData SO 구조 정의 (16세트 + 듀얼 태그 + 등급 + 사이즈)

## Context

Epic S(빌드 시스템)의 첫 ticket. **새로 만드는 게 아니라 기존 RelicData 시스템 확장**. Epic P(CL-106~110)에서 이미 단단한 기반이 구축되어 있음:

- `RelicData` (SO) — 단일 태그 + 등급 + EffectType 보유
- `PlayerRelicInventory` — 슬롯 기반 인벤토리 + 이벤트 발화
- `RelicEffectRegistry` — 효과 적용 권위 + StatModifierContainer 통합
- `PlayerStatModifierContainer` — Permanent/Timed/Conditional modifier 합산
- 18개 RelicData asset 이미 존재

회의록(2025-04 + 정리) 결정사항을 반영해야 할 변경점:
- **태그 3개 → 16개** 확장 (`RelicTag` enum)
- **단일 태그 → 듀얼 태그** (1 아이템 = 2 세트 기여)
- **사이즈 필드 추가** (1×1 / 1×2 / 2×2)
- **세트 효과 SO 신설** (`BuildSetData`) — 티어별 효과 정의

본 CL은 **데이터 구조 정의만** 책임. 매니저(CL-139)·효과 적용(CL-140) 은 후속.

---

## 결정사항

### 1. 명명 정책 — 기존 `Relic*` 접두사 유지

회의록의 "ItemData/BuildData/SetData" 표현은 기획 용어. **코드 레벨에선 기존 `Relic*` 유지**:

| 기획 용어 | 코드 명명 | 이유 |
|---|---|---|
| ItemData | `RelicData` (확장) | 18개 SO + 모든 참조 코드 그대로 |
| BuildData | (불필요) | 빌드 = "보유 아이템 태그 카운트 결과" — 별도 SO 없음 |
| SetData | `BuildSetData` (신규) | 16세트의 티어별 효과 정의 |

이유: rename 하면 모든 SO asset에 m_Script 재바인딩 필요 + 모든 using 변경 + GUID 보존 까다로움. 기획 용어와 코드 용어를 분리하는 게 표준.

### 2. RelicTag enum 확장: 3개 → 16개

**기존 (지울 것)**:
```csharp
public enum RelicTag { Assault, Guardian, Sprint }
```

**신규 (16세트)**:
```csharp
public enum RelicTag
{
    // 평타 친화 (5)
    AttackSpeed,    // 공속
    Critical,       // 치명타
    AttackPower,    // 일반뎀
    Ice,            // 얼음
    Lightning,      // 전기
    // 스킬 친화 (3)
    Cooldown,       // 쿨감
    Fire,           // 불
    Wind,           // 바람
    // 자동 발동 (1)
    MagicalGirl,    // 미소녀
    // 공통 (7)
    Health,         // 체력
    Defense,        // 방어력
    Dodge,          // 회피
    Range,          // 범위
    Luck,           // 행운
    Greed,          // 탐욕
    Tarot,          // 타로
}
```

기존 18개 SO의 _tag 값(0=Assault, 1=Guardian, 2=Sprint)는 새 enum의 인덱스와 다른 의미가 됨 → **마이그레이션 필요** (§5).

### 3. 듀얼 태그 적용

`RelicData`에 단일 `_tag` 필드를 두 개로:

```csharp
[Tooltip("듀얼 태그 첫 번째. 소모품일 때는 무시.")]
[SerializeField] private RelicTag _tagPrimary;

[Tooltip("듀얼 태그 두 번째. 단일 태그 아이템이면 _tagPrimary 와 동일하게 두거나 별도 None 처리.")]
[SerializeField] private RelicTag _tagSecondary;
```

**대안 검토**:
- (a) `RelicTag[] _tags` 배열 → 가변 길이, 향후 트리플 태그 확장 가능, 단 Inspector 표시 복잡
- (b) `_tag1`, `_tag2` 두 필드 → 항상 2개로 강제, 단순
- **채택: (b)**. 이유: 디자인 결정상 듀얼 고정. 1태그 아이템 (CL-138 단계엔 없음) 필요해지면 _tagSecondary를 _tagPrimary와 동일 또는 None으로 처리 (None enum 값 추가 검토)

`None` 값 추가 여부:
- 추가 시: 단일 태그도 표현 가능, 카운트 로직에서 None 무시 처리 필요
- 미추가 시: 항상 2개 듀얼 강제, 단순
- **채택: 추가** — `RelicTag.None = 0` 으로 두고 단일 태그 아이템 지원 (소모품도 None/None 가능)

### 4. 사이즈 필드 추가

```csharp
[Tooltip("인벤토리 점유 사이즈 (B-Lite 1). 등급별 자동 매핑 권장 (일반 1×1 / 레어 1×2 / 유니크·전설 2×2).")]
[SerializeField] private Vector2Int _size = new Vector2Int(1, 1);
```

**대안 검토**:
- (a) `Vector2Int _size` → 자유도 ↑, 어떤 크기든 가능
- (b) `enum ItemSize { Size1x1, Size1x2, Size2x2 }` → 정해진 옵션만, 단순
- **채택: (a)**. 이유: 향후 1×3 같은 변형 가능. 다만 등급별 기본값은 코드 또는 helper에서 강제.

**등급 → 사이즈 자동 매핑** (CL-150에서 구현, CL-138에서는 기본값만):

| 등급 | 권장 사이즈 |
|---|---|
| Common | 1×1 |
| Rare | 1×2 |
| Unique | 2×2 |
| Legendary | 2×2 |

자동 매핑 로직은 CL-150 (사이즈 시스템)으로 미룸. 본 CL에서는 **필드 정의 + Inspector 입력**만.

### 5. 신규 SO: `BuildSetData`

각 세트의 티어별 효과 정의용. 16개 인스턴스 생성 (불 / 얼음 / 행운 등).

**구조**:
```csharp
[CreateAssetMenu(fileName = "BuildSet_New", menuName = "LostMemory/Build Set Data")]
public class BuildSetData : ScriptableObject
{
    [SerializeField] private RelicTag _setTag;        // 어떤 세트인지
    [SerializeField] private string _displayName;     // "행운", "불", ...
    [SerializeField] private SetTier[] _tiers;        // 티어별 효과 (회의록 발동 조건+효과)

    public RelicTag SetTag => _setTag;
    public string DisplayName => _displayName;
    public IReadOnlyList<SetTier> Tiers => _tiers;
}

[Serializable]
public struct SetTier
{
    [Tooltip("이 티어 발동에 필요한 아이템 수 (예: 행운 1/3/5/7)")]
    public int RequiredCount;

    [Tooltip("티어 발동 시 적용 효과 분류 (RelicEffectType 재활용 또는 신규 SetEffectType 도입)")]
    public RelicEffectType EffectType;

    [Tooltip("효과 수치. 예: 0.25 = 25%")]
    public float Magnitude;

    [TextArea(1, 3)]
    public string Description;
}
```

**결정 미정**:
- `EffectType`을 기존 `RelicEffectType` 재활용할지, 신규 `SetEffectType` 만들지
  - 재활용: 기존 RelicEffectRegistry switch 그대로 사용 가능 (편함)
  - 신규: 세트 효과 전용 (분리 깔끔, 단 코드 중복)
  - **임시 채택: 재활용** + 부족하면 후속에서 enum 값 추가 (예: `SlowOnHit`, `BurnOnHit`, `ChainOnHit`)
- 미소녀 5스택 합체같은 특수 효과는 `EffectType.None` + 별도 핸들러 분기? (CL-145에서 결정)

### 6. SetEffectRegistry는 별도 (CL-139에서 신설)

`BuildManager` (CL-139) 가 보유 아이템들의 태그 카운트 → 도달한 세트 티어 계산 → `SetEffectType` 적용.

본 CL에서는 **데이터 구조만** 정의. 카운트 로직·이벤트 발화는 CL-139.

### 7. ⭐ 다중 효과 지원 — `EffectEntry[]` 배열 (V0.4 보완)

items_draft.md V0.4 분석 결과, 77개 아이템 대부분이 **2개 이상 효과**를 가짐:

| 예시 | 효과 텍스트 | 효과 수 |
|---|---|---|
| 노련한 검술서 | 공속 +5%, 치명타 +5% | 2 |
| 사냥꾼의 표적 | 치명타 +15%, 평타 +25% | 2 |
| 폭풍의 검 (전설) | 평타 시 얼음·전기 동시 트리거 +100% | 2~3 |
| 별의 운명 (전설) | 행운 +7, 타로 두 배 | 2 |

→ 기존 단일 `_effectType` 필드로는 표현 불가능. **`EffectEntry` struct + 배열 도입 필요**.

**신규 struct**:
```csharp
[Serializable]
public struct EffectEntry
{
    public RelicEffectType Type;
    public float Magnitude;
    public float Duration;
    public float Threshold;
}
```

**RelicData 수정**:
```csharp
// 기존 4개 단일 필드 → 배열로 통합
[SerializeField] private EffectEntry[] _effects;

public IReadOnlyList<EffectEntry> Effects => _effects;
```

**기존 4개 필드 (`_effectType`, `_magnitude`, `_duration`, `_threshold`)** 는 `[Obsolete] + [FormerlySerializedAs]` 로 보존 → 18개 기존 SO 직렬화 안전:

```csharp
[Obsolete("Use _effects instead. Migration: _effectsLegacy → _effects[0]")]
[FormerlySerializedAs("_effectType")]
[SerializeField] private RelicEffectType _effectTypeLegacy = RelicEffectType.None;

[Obsolete] [FormerlySerializedAs("_magnitude")]
[SerializeField] private float _magnitudeLegacy;
// _duration, _threshold 동일
```

**기존 RelicEffectRegistry 호환**:
- 단일 효과 케이스: `relic.Effects[0]` 또는 deprecated property `relic.EffectType` 으로 접근
- 다중 효과 케이스: `foreach (var e in relic.Effects)` 로 순회

**18개 기존 SO 마이그레이션**:
- 마이그레이션 단계에서 단일 효과 (legacy 필드 값) → `_effects[0]` 1회성 변환
- Editor 스크립트 또는 수동
- 마이그레이션 후 legacy 필드는 RelicData.cs에서 제거 가능 (CL-141 이후 청소)

---

## 핵심 파일

### 신규 파일

| 경로 | 내용 | 비고 |
|---|---|---|
| `Assets/_Project/Scripts/Runtime/Relics/BuildSetData.cs` | SetData SO + SetTier struct | 새 SO 타입 |
| `Assets/_Project/Scripts/Runtime/Relics/EffectEntry.cs` | 다중 효과 struct (V0.4 보완) | RelicData가 사용 |
| `Assets/_Project/ScriptableObjects/BuildSets/BuildSet_*.asset` (16개) | 16개 세트 인스턴스 | items_draft.md §1 세트 효과 표 그대로 입력 |

### 수정 파일

| 경로 | 변경 내용 | 영향 |
|---|---|---|
| `Assets/_Project/Scripts/Runtime/Relics/RelicTag.cs` | enum 3개 → 16개 (+ None=0) | 기존 18개 SO 마이그레이션 필요 |
| `Assets/_Project/Scripts/Runtime/Relics/RelicData.cs` | `_tag` → `_tagPrimary`/`_tagSecondary`, `_size` 추가, **`_effects[]` 추가** (V0.4), 기존 효과 필드 deprecated | 18개 SO 직렬화 영향 |

### 영향 없는 파일 (참조만 확인)

| 경로 | 이유 |
|---|---|
| `RelicEffectRegistry.cs` | RelicData.Tag 직접 참조 안 함 (EffectType만 봄) |
| `PlayerRelicInventory.cs` | 태그 안 봄 |
| `PlayerStatModifierContainer.cs` | 무관 |

---

## 구현 단계

### 1단계: enum 확장 (15분)
1. `RelicTag.cs` 수정 — 16개 + None=0
2. 컴파일 확인 — 기존 코드는 RelicData.Tag 참조 안 하므로 빌드 에러 없을 것
3. 18개 RelicData asset의 _tag 값이 0/1/2였던 것 → 의미 깨짐. 이건 §5 마이그레이션에서 해결

### 2단계: RelicData 필드 추가 (30분, V0.4 보완 포함)
1. `EffectEntry.cs` 신규 작성 (struct)
2. `RelicData.cs` — `_tagPrimary`, `_tagSecondary`, `_size`, `_effects[]` 필드 추가
3. 기존 `_tag` 필드 → `[FormerlySerializedAs("_tag")]` 어노테이션으로 `_tagPrimary` 보존
4. 기존 `_effectType`/`_magnitude`/`_duration`/`_threshold` 4개 필드 → `_effectTypeLegacy` 등으로 deprecated 처리
5. `Tag` 프로퍼티 → `TagPrimary`, `TagSecondary`, `Size`, `Effects` 프로퍼티 추가
6. 기존 `EffectType`/`Magnitude` 프로퍼티 → `[Obsolete]` + `_effects[0]` 우선 반환 (legacy fallback)

```csharp
// 마이그레이션 안전한 변경 (V0.4)
[FormerlySerializedAs("_tag")]
[SerializeField] private RelicTag _tagPrimary;
[SerializeField] private RelicTag _tagSecondary;
[SerializeField] private Vector2Int _size = new Vector2Int(1, 1);
[SerializeField] private EffectEntry[] _effects;

// Legacy (마이그레이션용, 18개 기존 SO 보존)
[Obsolete] [FormerlySerializedAs("_effectType")]
[SerializeField] private RelicEffectType _effectTypeLegacy = RelicEffectType.None;
[Obsolete] [FormerlySerializedAs("_magnitude")]
[SerializeField] private float _magnitudeLegacy;
[Obsolete] [FormerlySerializedAs("_duration")]
[SerializeField] private float _durationLegacy;
[Obsolete] [FormerlySerializedAs("_threshold")]
[SerializeField] private float _thresholdLegacy;

public RelicTag TagPrimary => _tagPrimary;
public RelicTag TagSecondary => _tagSecondary;
public Vector2Int Size => _size;
public IReadOnlyList<EffectEntry> Effects => _effects;

// 기존 RelicEffectRegistry 호환 (deprecated, _effects 우선)
[Obsolete("Use Effects[0] instead")]
public RelicEffectType EffectType => 
    (_effects != null && _effects.Length > 0) ? _effects[0].Type : _effectTypeLegacy;
[Obsolete] public float Magnitude => 
    (_effects != null && _effects.Length > 0) ? _effects[0].Magnitude : _magnitudeLegacy;
// Duration, Threshold 동일 패턴
```

### 3단계: BuildSetData 신설 (30분)
1. `BuildSetData.cs` 작성 (위 §5 구조)
2. `Assets/_Project/ScriptableObjects/BuildSets/` 폴더 생성
3. 16개 SO 인스턴스 생성 — Inspector에서 수동 또는 메뉴에서 일괄 생성
   - 각 SO에 회의록의 티어별 발동 조건 + 효과 입력 (items_draft.md §1 참조)
   - 예: `BuildSet_행운.asset` — RequiredCount = [1, 3, 5, 7], 각각 효과 입력

### 4단계: 18개 기존 RelicData 마이그레이션 (1~2시간)
1. 기존 _tag 값(Assault/Guardian/Sprint) → 새 16개 enum 중 적절한 것으로 매핑
2. _tagSecondary 도 입력
3. _size 입력 (등급 따라)

매핑 예시 (현재 18개 SO):
| 기존 SO | 기존 태그 | 새 _tagPrimary | 새 _tagSecondary | 컨셉 |
|---|---|---|---|---|
| 전사의 끈 | Assault | AttackPower | (Health 또는 Critical) | 일반뎀 빌드 |
| 분쇄의팔찌 | Assault | AttackPower | Critical | 일반뎀+치명타 |
| 전투북 | Assault | AttackPower | Health | 조건부 평타 |
| 붉은송곳니 | Assault | AttackSpeed | AttackPower | 공속 빌드 |
| 바람깃털 | Sprint | Wind | AttackSpeed | 바람+공속 |
| 수호의파편 | Guardian | Health | Defense | 체력 빌드 |
| 반격의표식 | Guardian | Defense | Dodge | 패링 보상 |
| ... (나머지 11개) | | | | |

전체 매핑은 별도 문서 (`docs/khi/cl138_relicdata_migration.md`) 또는 본 plan §부록.

### 5단계: 검증 (30분)
1. Unity Editor에서 컴파일 에러 0
2. 기존 RelicData asset 18개 모두 m_Script 재바인딩 자동 완료 (FormerlySerializedAs 덕분)
3. Inspector에서 _tagPrimary/_tagSecondary/_size 보이는지 확인
4. BuildSetData 16개 SO 인스턴스 생성 가능한지 확인
5. 회의록 §1 세트 효과 표와 BuildSetData 값 1:1 비교

---

## 검증 방법 (e2e)

```
1. Unity Editor 열기
2. Console 에러 0 확인
3. 18개 RelicData asset 차례로 클릭 → Inspector에서:
   - _tagPrimary 가 적절한 새 enum 값으로 보임 (FormerlySerializedAs 덕)
   - _tagSecondary 입력 가능
   - _size 표시
4. RelicData 메뉴 → Create → "LostMemory/Relic Data" 로 새 RelicData 생성 가능 확인
5. BuildSetData 메뉴 → Create → "LostMemory/Build Set Data" 로 16개 인스턴스 생성:
   - BuildSet_행운, BuildSet_치명타, ... BuildSet_타로
6. 각 BuildSetData 의 Tiers 배열에 회의록 §1 표 그대로 입력
7. Play 모드 진입 → 기존 게임 흐름 정상 (CL-138은 데이터만 추가, 로직 변경 X)
```

---

## 위험 / 결정 미정

### 위험
1. **기존 SO 직렬화 깨짐 가능성**: `_tag` 제거 시 Unity가 기존 값 잃을 수 있음. → `FormerlySerializedAs("_tag")` 로 `_tagPrimary` 보존하면 안전. **사전 백업 필수**.
2. **enum 값 변경**: RelicTag.Assault(=0) → AttackPower(=1) 같은 인덱스 시프트가 일어나면 기존 SO의 _tag=0이 None으로 바뀜. **기존 _tag=0(Assault) 인 SO들은 마이그레이션 매핑 표를 따라 수동 재할당 필요**.
3. **Sprint·Guardian이었던 SO들**도 같은 이슈. 18개 모두 점검 필요.

### 결정 (확정 완료)
- [x] **`EffectType` 재활용 + 16개 enum 값 추가** (별도 SetEffectType 신규 X)
- [x] **미소녀 5스택 합체 = `MagicalGirlFusion` enum 값 + 별도 핸들러 (CL-145)**
- [x] **`RelicTag.None = 0` 추가** — 단일 태그 / 소모품 케이스 지원
- [x] **18 SO 마이그레이션 매핑 확정** — 본 plan §부록 표 그대로
- [x] **미소녀+원소 아이템 (6개) 효과 = `[원소] 공격` 형식** — 일반 등급은 기본, 강화 등급은 "강화 [원소] 공격"
- [ ] BuildSetData 16개 SO를 자동 스크립트로 생성? 수동? (16개라 수동도 빠름) — **CL-138 작업 단계에서 결정**

### EffectType 추가 16개 enum 값 (확정)

```csharp
// CL-138 추가 (세트 효과용)
CriticalChancePercent,        // 치명타
CooldownReductionPercent,     // 쿨감
AttackRangePercent,           // 범위
DodgeChancePercent,           // 회피
DefenseFlat,                  // 방어력 (+4, +8 flat)
GoldGainPercent,              // 탐욕
LuckPoints,                   // 행운 (스택 수)
BurnOnHit,                    // 불 도트 (on-hit 트리거)
SlowOnHit,                    // 얼음 슬로우
FreezeOnHit,                  // 얼음 빙결 (3티어)
ChainOnHit,                   // 전기 체인
WindAOE,                      // 바람 광역
MagicalGirlSummon,            // 미소녀 1~4 (entity)
MagicalGirlFusion,            // 미소녀 5합체 (entity, 특수)
MagicalGirlElementalAttack,   // 미소녀 속성 공격 (38~41 일반)
MagicalGirlElementalEnhanced, // 미소녀 강화 속성 공격 (45, 47 등)
TarotProc,                    // 타로 발동
LuckSlotExpand,               // 행운 3스택 슬롯+1
LuckLegendaryGuarantee,       // 행운 7스택 무조건 전설
```

→ 총 19개 추가. 기존 11 + 신규 19 + None = **31개 enum 값**.

---

## 후속 ticket 영향

| Ticket | 본 CL과의 관계 |
|---|---|
| **CL-139** (BuildManager) | 본 CL의 `BuildSetData` 16개 SO + `RelicData` 듀얼 태그 사용. PlayerRelicInventory.OnRelicAcquired 구독 → 태그 카운트 갱신 → 도달 티어 계산 → 효과 적용 트리거 |
| **CL-140** (EffectApplicator) | 본 CL의 `SetTier.EffectType` 분기 → PlayerStatModifierContainer 호출 (CL-139의 핸들러 통합) |
| **CL-141** (75 ItemData 일괄 생성) | 본 CL의 RelicData 신 구조 사용. items_draft.md → SO 자동 변환 스크립트 권장 |
| **CL-150** (사이즈 시스템) | 본 CL의 `_size` 필드 활용. 등급 → 사이즈 자동 매핑 helper 구현 |

---

## 예상 시간

| 단계 | 시간 |
|---|---|
| 1단계 (enum 확장) | 15분 |
| 2단계 (RelicData 수정) | 15분 |
| 3단계 (BuildSetData 신설 + 16개 SO) | 30분 |
| 4단계 (18개 마이그레이션) | 1~2시간 |
| 5단계 (검증) | 30분 |
| **합계** | **2.5~3.5시간** (티켓 점수 3점에 부합) |

---

## 부록: 마이그레이션 매핑표 (18개 RelicData → 신 듀얼 태그) — **확정**

> 5개 미확인 SO 추가 분석 후 전부 확정. 작업 시 이 표 그대로 입력하면 됨.

### 비-소모품 15개

| 기존 이름 | 등급 (rarity) | 효과 메커니즘 | _tagPrimary | _tagSecondary | _size |
|---|---|---|---|---|---|
| 전사의 끈 | Common (0) | 공격력 +5% | AttackPower | Health | 1×1 |
| 분쇄의 팔찌 | Rare (1) | 3타 마무리 +% | AttackPower | Critical | 1×2 |
| 전투 북 | Rare (1) | HP≥50% 시 공격력 +% | AttackPower | Health | 1×2 |
| 붉은 송곳니 | Rare (1) | 처치 시 공속 +% (임시) | AttackSpeed | AttackPower | 1×2 |
| 바람 깃털 | Common (0) | 이속 +% | Wind | AttackSpeed | 1×1 |
| 수호의 파편 | Common (0) | 최대 체력 +% | Health | Defense | 1×1 |
| 반격의 표식 | Rare (1) | 패링 시 보호막 | Defense | Dodge | 1×2 |
| 철의 깃 | Common (0) | 회복 +% | Health | Defense | 1×1 |
| 질풍 장화 | Common (0) | 대시 쿨감 -% | AttackSpeed | Dodge | 1×1 |
| 추적자의 망토 | Rare (1) | 대시 종료 시 이속 +% (임시) | AttackSpeed | Wind | 1×2 |
| 광전사의 문장 | Unique (2) | 연속 적중 시 공격력 중첩 (미구현) | AttackPower | AttackSpeed | 2×2 |
| 잔상의 목걸이 | Rare (1) | 대시 후 회피 강화 (미구현) | Dodge | AttackSpeed | 1×2 |
| 성벽의 궤 | Legendary (3) | 방패 수 +2 (미구현) | Defense | Health | 2×2 |
| 순환의 방패 핵 | Unique (2) | 궤도 방패 재생 시간 감소 (미구현) | Defense | Cooldown | 2×2 |
| 시공의 파편 | Legendary (3) | 기본 이속 추가 증가 (미구현) | Wind | AttackSpeed | 2×2 |

### 소모품 3개

| 이름 | _tagPrimary | _tagSecondary | _size |
|---|---|---|---|
| 작은 회복약 | None | None | 1×1 |
| 큰 회복약 | None | None | 1×1 |
| 랜덤박스 | None | None | 1×1 |

> **5개 "미구현" 표시**: 광전사의 문장, 잔상의 목걸이, 성벽의 궤, 순환의 방패 핵, 시공의 파편. 모두 `_effectType = 0 (None)` 상태. CL-138은 태그·사이즈만 입력. 효과 메커니즘 구현은 별도 ticket.
