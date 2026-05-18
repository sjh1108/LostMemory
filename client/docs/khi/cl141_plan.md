# CL-141 — 77개 ItemData SO 일괄 생성 (items_draft.md → SO)

## Context

Epic S Phase 2의 단일 ticket. **CL-138 데이터 구조에 77개 아이템 풀 채워넣기**.

데이터 소스: [items_draft.md](items_draft.md) V0.4 (총 77개)
- 7테마 + 교차 보강
- 듀얼 태그 + 등급 + 사이즈
- 이름 + 효과 텍스트 정의

**자동화 필요성**:
- 77 items × ~12 fields = 924 데이터 입력
- 수동 입력: 한 SO당 5~10분 → **6~13시간** 소요
- Editor 스크립트로 자동 생성: **30분 이내**
- 향후 V0.5 이후 추가 시에도 재사용 가능

→ Editor 스크립트 + CSV 데이터 소스 채택.

---

## ⚠️ Critical: CL-138 데이터 구조 보완 필요

본 plan 작성 중 발견한 이슈. **CL-138 작업 시 같이 반영해야 함**.

### 문제

현재 `RelicData`는 **단일 `_effectType` + 단일 magnitude**만 지원:
```csharp
[SerializeField] private RelicEffectType _effectType;
[SerializeField] private float _magnitude;
[SerializeField] private float _duration;
[SerializeField] private float _threshold;
```

하지만 items_draft.md의 아이템 효과 텍스트는 **대부분 2개 이상 효과**:

| 아이템 | 효과 텍스트 | 효과 수 |
|---|---|---|
| 노련한 검술서 | 공속 +5%, **치명타 +5%** | 2 |
| 강철 손목보호대 | 공속 +5%, **평타 데미지 +10%** | 2 |
| 사냥꾼의 표적 | 치명타 +15%, **평타 데미지 +25%** | 2 |
| 폭풍의 검 (전설) | 평타 시 얼음·전기 동시 트리거, **양쪽 효과 +100%** | 2~3 |
| 별의 운명 (전설) | 행운 +7, **타로 카드 효과 두 배** | 2 |

→ 단일 `_effectType`로는 표현 불가능.

### 해결 방안

**옵션 A: 효과를 `EffectEntry[]` 리스트로 변경** ⭐ 추천

```csharp
[Serializable]
public struct EffectEntry
{
    public RelicEffectType Type;
    public float Magnitude;
    public float Duration;
    public float Threshold;
}

[CreateAssetMenu(...)]
public class RelicData : ScriptableObject
{
    [FormerlySerializedAs("_tag")]
    [SerializeField] private RelicTag _tagPrimary;
    [SerializeField] private RelicTag _tagSecondary;
    [SerializeField] private Vector2Int _size = new Vector2Int(1, 1);
    
    // ⭐ 신규: 다중 효과
    [SerializeField] private EffectEntry[] _effects;
    
    // 기존 필드 (deprecation 마이그레이션용)
    [Obsolete("Use _effects instead")]
    [FormerlySerializedAs("_effectType")]
    [SerializeField] private RelicEffectType _effectTypeLegacy;
    [Obsolete] [FormerlySerializedAs("_magnitude")]
    [SerializeField] private float _magnitudeLegacy;
    [Obsolete] [FormerlySerializedAs("_duration")]
    [SerializeField] private float _durationLegacy;
    [Obsolete] [FormerlySerializedAs("_threshold")]
    [SerializeField] private float _thresholdLegacy;
    
    // API
    public IReadOnlyList<EffectEntry> Effects => _effects;
    
    // 기존 RelicEffectRegistry 호환용 (단일 효과 케이스)
    [Obsolete] public RelicEffectType EffectType => _effects.Length > 0 ? _effects[0].Type : _effectTypeLegacy;
    [Obsolete] public float Magnitude => _effects.Length > 0 ? _effects[0].Magnitude : _magnitudeLegacy;
    // ...
}
```

**장점**:
- 다중 효과 자연스럽게 표현
- 기존 18개 SO는 `_effectTypeLegacy` 필드 보존 (FormerlySerializedAs)
- 점진적 마이그레이션 가능

**작업량**: CL-138에 +30분 (필드 추가 + Obsolete 처리)

**옵션 B: 태그 기반 자동 매핑 (정해진 패턴만)**

태그가 정해져 있으니 태그 → StatId 자동 매핑:
- `RelicTag.AttackPower` → `StatId.AttackPower`, magnitude 0.05 (자동 5%)
- `RelicTag.Critical` → `StatId.Critical`, magnitude 0.05

→ **단순 아이템에만** 적용 가능. 특수 트리거 효과 (폭풍의 검의 "동시 트리거") 표현 못 함.

**채택: 옵션 A**. 유연성 + 호환성.

### CL-138 Plan 업데이트 필요사항

`docs/khi/cl138_plan.md`에 추가:
- [ ] `_effects` 배열 필드 추가
- [ ] `EffectEntry` struct 정의
- [ ] 기존 4개 효과 필드를 `_effectTypeLegacy` 등으로 deprecated 처리
- [ ] 18개 기존 SO의 단일 효과를 `_effects[0]`으로 자연스럽게 매핑 (마이그레이션 시)

→ **CL-138 작업 시 본 보완 같이 반영**. CL-141 작업 시작 시점에 이 구조가 있어야 함.

---

## 결정사항

### 1. 데이터 소스 — CSV 파일 채택

**옵션**:
- (a) items_draft.md 파싱 (마크다운 표 파서 필요)
- (b) **CSV 파일** ⭐ — Excel/Sheets 친화, 디자이너 편집 쉬움
- (c) JSON
- (d) Editor 스크립트에 inline 데이터

**채택: (b) CSV**.
- Editor 스크립트가 CSV 읽고 RelicData 생성
- CSV는 사용자가 별도 관리 (items_draft.md 수정 시 CSV도 동기화)

### 2. CSV 위치 + 형식

**경로**: `Assets/_Project/ScriptableObjects/Relics/_source/items.csv`
- `_source` 접두어로 "데이터 소스" 표시 (Unity import 무시)
- `.csv.meta`는 자동 생성되지만 import 안 됨 (TextAsset로 인식되긴 함)

**형식 (헤더)**:
```csv
ID,Name,Theme,Rarity,TagPrimary,TagSecondary,SizeX,SizeY,EffectsJSON,EffectDescription
```

**예시 행**:
```csv
1,노련한 검술서,검사,Common,AttackSpeed,Critical,1,1,"[{""Type"":""AttackSpeedPercent"",""Magnitude"":0.05},{""Type"":""CriticalChancePercent"",""Magnitude"":0.05}]",공속 +5% / 치명타 +5%
```

**효과 JSON 인코딩**: CSV의 한 셀에 JSON 배열로 효과 리스트 저장. CSV 파서 + JSONUtility로 RelicData에 채움.

**대안**: 효과를 별도 컬럼으로 (Effect1Type, Effect1Mag, Effect2Type, Effect2Mag) — 단순하지만 컬럼 수 ↑. JSON 방식이 깔끔.

### 3. Editor 스크립트 위치 + 메뉴

**경로**: `Assets/_Project/Scripts/Editor/Relics/RelicDataBatchGenerator.cs`

**메뉴**: `LostMemory/Relics/Generate RelicData from CSV`

기존 `BerthaAnimationSetupEditor` 패턴 따름 (같은 `LostMemory` 메뉴 카테고리).

### 4. 출력 폴더

**경로**: `Assets/_Project/ScriptableObjects/Relics/Generated/`
- 기존 18개 SO와 분리 (`Generated/` 하위 폴더)
- 일괄 재생성 시 충돌 방지
- 기존 18개는 그대로 유지하되 마이그레이션 후 동일 폴더로 이동 가능

또는 단일 폴더 (`Relics/`)에 추가 — 단 기존 SO와 이름 충돌 주의:
- 기존: `RelicData_전사의끈.asset` (한국어)
- 신규 (items_draft 기반): `RelicData_전사의끈.asset`도 가능

→ **채택: `Generated/` 하위 폴더로 분리**. 마이그레이션 후 정리.

### 5. 파일 명명 규칙

`RelicData_<DisplayName>.asset` (기존 패턴 그대로):
- `RelicData_노련한검술서.asset`
- `RelicData_분홍리본.asset`
- ...

공백 제거 (디스플레이 이름의 공백은 파일명에서 제거).

**ID 충돌 처리**: 기존 18개 SO와 이름이 같으면 (예: 둘 다 "전사의 끈") **기존 SO 우선**. 신규 SO는 만들지 않음 (스킵 + 로그).

### 6. 아이콘 처리

**Step 1 (CL-141 기본)**: 아이콘 필드는 비어둠 (`_icon = null`)
- 또는 placeholder 스프라이트 1개 자동 할당

**Step 2 (별도 ticket)**: 실제 아이콘 매핑
- 디자이너가 아이콘 폴더 정리
- CSV에 `IconPath` 컬럼 추가
- 재생성 시 매핑

본 CL은 **placeholder 또는 null**.

### 7. 기존 18개 SO 처리

**옵션**:
- (a) 기존 SO 무시. 신규 77개만 생성 (총 95개)
- (b) **기존 SO 마이그레이션 + 신규만 생성** ⭐
- (c) 기존 SO 폐기, 77개 새로 (기존 작업 손실)

**채택: (b)**. 기존 18개는 CL-138 마이그레이션 (이미 plan에 있음)으로 듀얼 태그·사이즈 추가. 신규 77개 중 기존과 이름 같은 것은 스킵.

### 7.5 items_draft.md vs 기존 18개 명명 충돌 점검

items_draft 77개 중 기존 18개와 이름 같은 것:

| 기존 | items_draft에 동명? | 처리 |
|---|---|---|
| 전사의 끈 | ❌ 없음 | 기존만 유지 |
| 바람 깃털 | ❌ "바람의 깃털" (#41, 다른 효과) | **이름 충돌**: 띄어쓰기로 구분 가능하지만 혼란 |
| 수호의 파편 | ❌ 없음 | 기존만 유지 |
| 작은 회복약 / 큰 회복약 / 랜덤박스 | ❌ items_draft에 소모품 없음 | 기존만 유지 |

→ items_draft 77개 + 기존 18개 = 약 95개 (중복 거의 없음, 약간 명명 혼선 가능). 마이그레이션 후 사용자 검수 필요.

### 8. 효과 JSON 입력 부담

77개 모두 효과 JSON을 CSV에 적기는 부담. **반자동 생성** 옵션:

**스크립트가 효과 텍스트를 파싱해서 자동 생성**:
- "공속 +5%, 치명타 +5%" → `[{Type: AttackSpeedPercent, Mag: 0.05}, {Type: CriticalChancePercent, Mag: 0.05}]`
- 패턴 매칭 (정규식): `(\w+) ([+-]?\d+(\.\d+)?)\%?` → enum + magnitude

**한계**:
- 모든 효과 패턴 자동 매핑 어려움 (특수 효과는 수동)
- 에러 시 fallback (수동 입력)

**채택: 반자동 + 수동 검수**:
- 스크립트가 자동 생성 시도
- 실패 (특수 효과)는 EffectsJSON 컬럼에 빈 값 → 수동 입력 필요
- 또는 특수 아이템만 별도 csv에 수동 정의 후 자동 합치기

### 9. 검증 모드

**Dry-run 메뉴 추가**: `LostMemory/Relics/Validate CSV (Dry Run)`
- CSV 파싱만 하고 SO 생성 안 함
- 에러·경고 출력 (잘못된 enum 이름, 빈 필드 등)
- 사용자가 검수 후 실제 생성 메뉴 실행

---

## 핵심 파일

### 신규

| 경로 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Editor/Relics/RelicDataBatchGenerator.cs` | Editor 스크립트 (메인) |
| `Assets/_Project/ScriptableObjects/Relics/_source/items.csv` | 77개 아이템 데이터 |
| `Assets/_Project/ScriptableObjects/Relics/Generated/` | 출력 폴더 |

### 수정

| 경로 | 변경 내용 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Relics/RelicData.cs` | EffectEntry[] 추가 (CL-138 보완) |

### 참조

| 경로 | 사용 |
|---|---|
| `RelicTag.cs` (CL-138) | enum 매핑 |
| `RelicEffectType.cs` (CL-138) | enum 매핑 |
| `BuildSetData.cs` (CL-138) | 무관 (참고용) |

---

## 구현 단계

### 1단계: items_draft.md → CSV 변환 (1시간, 수동 또는 스크립트)

선택지:
- (a) 사용자가 직접 items.csv 만들기 (Excel/Notion에서 export)
- (b) 임시 Python 스크립트로 .md → .csv 변환
- (c) Claude가 채팅에서 CSV 형식으로 출력 → 사용자가 파일로 저장

**채택: (c)** — 가장 빠름. 사용자는 CSV 텍스트 받아서 파일로 저장만 하면 됨.

CSV 컬럼:
```
ID, Name, Theme, Rarity, TagPrimary, TagSecondary, SizeX, SizeY, Effect1Type, Effect1Mag, Effect2Type, Effect2Mag, EffectDescription
```

(JSON 대신 컬럼 분할로 단순화. 효과 최대 2개 가정 — 더 필요하면 Effect3, Effect4 추가)

### 2단계: RelicData 구조 보완 (30분, CL-138 기여)

CL-138에서 작업한 RelicData.cs에 EffectEntry[] 추가. 본 CL이 시작되기 전 완료되어야 함.

### 3단계: RelicDataBatchGenerator 작성 (2시간)

```csharp
public static class RelicDataBatchGenerator
{
    private const string CsvPath = "Assets/_Project/ScriptableObjects/Relics/_source/items.csv";
    private const string OutputFolder = "Assets/_Project/ScriptableObjects/Relics/Generated/";

    [MenuItem("LostMemory/Relics/Generate RelicData from CSV")]
    public static void GenerateFromCsv()
    {
        // 1. CSV 읽기
        TextAsset csvAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(CsvPath);
        if (csvAsset == null) { Debug.LogError("CSV not found"); return; }

        // 2. 파싱
        var rows = ParseCsv(csvAsset.text);

        // 3. 기존 SO 이름 수집 (충돌 회피)
        var existing = AssetDatabase.FindAssets("t:RelicData")
            .Select(g => AssetDatabase.GUIDToAssetPath(g))
            .Select(p => Path.GetFileNameWithoutExtension(p))
            .ToHashSet();

        // 4. 각 행마다 RelicData 생성
        int created = 0, skipped = 0;
        foreach (var row in rows)
        {
            string fileName = $"RelicData_{row.Name.Replace(" ", "")}";
            if (existing.Contains(fileName))
            {
                Debug.Log($"Skip (exists): {fileName}");
                skipped++;
                continue;
            }

            var data = ScriptableObject.CreateInstance<RelicData>();
            // SerializedObject로 private 필드 설정
            var so = new SerializedObject(data);
            so.FindProperty("_displayName").stringValue = row.Name;
            so.FindProperty("_rarity").enumValueIndex = (int)row.Rarity;
            so.FindProperty("_tagPrimary").enumValueIndex = (int)row.TagPrimary;
            so.FindProperty("_tagSecondary").enumValueIndex = (int)row.TagSecondary;
            so.FindProperty("_size").vector2IntValue = new Vector2Int(row.SizeX, row.SizeY);
            
            // 효과 배열
            var effectsProp = so.FindProperty("_effects");
            effectsProp.arraySize = row.Effects.Count;
            for (int i = 0; i < row.Effects.Count; i++)
            {
                var elem = effectsProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("Type").enumValueIndex = (int)row.Effects[i].Type;
                elem.FindPropertyRelative("Magnitude").floatValue = row.Effects[i].Magnitude;
            }

            so.FindProperty("_effectDescription").stringValue = row.EffectDescription;
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(data, $"{OutputFolder}{fileName}.asset");
            created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[RelicDataBatchGenerator] Created {created}, Skipped {skipped}");
    }

    [MenuItem("LostMemory/Relics/Validate CSV (Dry Run)")]
    public static void ValidateCsv() { /* ... */ }
}
```

### 4단계: Dry-run 검증 (15분)

- 메뉴 실행 → 콘솔 로그 확인
- enum 이름 오타 없는지
- 빈 필드 없는지
- 77개 행 파싱 정상

### 5단계: 실제 생성 + 검수 (30분)

- 메뉴 실행 → 77개 SO 생성
- Project 창에서 Generated/ 폴더 확인 (77개 파일)
- 무작위 5~10개 SO Inspector 확인:
  - 이름·등급·태그·사이즈 정확
  - Effects 배열 정확
- 게임 실행 안 함 (별도 검증 ticket)

### 6단계: items.csv 백업 + 문서화 (15분)

- items.csv가 데이터 소스이므로 git 추적
- README 업데이트: "아이템 추가 시 items.csv 수정 → 메뉴 재실행"

---

## 검증 방법 (e2e)

```
1. CSV 파일 생성 (Claude 출력 → 파일 저장)
2. Unity Editor에서 LostMemory > Relics > Validate CSV 실행
   - 콘솔에 "77 rows parsed, 0 errors" 같은 출력
3. LostMemory > Relics > Generate RelicData from CSV 실행
   - 콘솔에 "Created 77, Skipped 0" 출력 (또는 기존 SO와 이름 같으면 Skipped 1+)
4. Project 창 → Assets/_Project/ScriptableObjects/Relics/Generated/ 확인
   - 77개 파일 (RelicData_*.asset)
5. 무작위 5개 클릭 → Inspector 검증:
   - 이름·등급·태그·사이즈
   - Effects 배열 (대부분 2개)
6. PlayerRelicInventory의 Debug 메뉴로 1~5개 추가 → BuildManager 카운트 변화 확인 (CL-139 검증과 통합)
```

---

## 위험 / 결정 미정

### 위험
1. **EffectEntry 마이그레이션**: 기존 18개 SO의 `_effectType`/`_magnitude`를 `_effects[0]`로 자동 변환하는 마이그레이션 로직 필요. CL-138 보완 작업에 포함.
2. **enum 인덱스 vs 이름**: CSV에 enum을 이름으로 저장하면 readable, 인덱스로 저장하면 robust. **이름 권장** (Type.Parse 사용).
3. **JSON 인코딩 대신 컬럼 분할**: Effect1Type, Effect1Mag, Effect2Type, Effect2Mag로 충분 (최대 2 효과 기준). 향후 3개 이상 필요하면 컬럼 추가.
4. **placeholder 아이콘 없으면 null 허용**: RelicData에서 _icon == null도 정상 동작해야 함 (UI 처리 시 fallback).
5. **77개 한 번에 생성 시 Unity 멈춤**: SaveAssets 마지막에 한 번만 호출 (배치 처리). Refresh 한 번만.

### 결정 미정
- [ ] CSV에 효과를 컬럼 분할 vs JSON 셀 — 컬럼 분할 추천 (가독성)
- [ ] 효과 텍스트 자동 파싱 시도 vs 사용자가 CSV에 명시 입력 — **명시 입력 추천** (자동 파싱 에러 위험)
- [ ] Generated/ 폴더 vs 기존 Relics/ 폴더에 합치기 — Generated/ 권장 (기존 SO 보호)
- [ ] 전설 5개의 특수 효과 (트리거 효과) 표현 — 옵션 A의 다중 효과로 가능 but 복잡한 트리거는 effectType만 정의하고 매그니튜드는 0?

---

## 후속 ticket 영향

| Ticket | 본 CL과의 관계 |
|---|---|
| **CL-142~CL-147 (효과 구현)** | 본 CL의 SO들을 보유하면 효과 발동. SO 데이터가 정확해야 효과 검증 가능 |
| **CL-148 (인벤토리 UI)** | 본 CL의 SO들 아이콘·텍스트 표시 |
| **CL-152 (보상/상점 풀)** | 본 CL의 SO들을 풀에 등록 |
| **CL-153 (QA)** | 77개 아이템 동작 검증 |

---

## 예상 시간

| 단계 | 시간 |
|---|---|
| 1단계 (items.csv 생성) | 1시간 (Claude가 CSV 생성하면 5분) |
| 2단계 (RelicData 보완, CL-138 기여) | 30분 |
| 3단계 (Editor 스크립트 작성) | 2시간 |
| 4단계 (Dry-run 검증) | 15분 |
| 5단계 (실제 생성 + 검수) | 30분 |
| 6단계 (백업 + 문서화) | 15분 |
| **합계** | **4~5시간** (티켓 점수 5점에 부합) |

---

## 다음 단계 (사용자 결정 필요)

1. **CL-138 plan에 EffectEntry[] 보완 사항 반영?** — 본 plan의 § "Critical" 부분
2. **items.csv 형식 확정** — 컬럼 분할 vs JSON
3. **Claude가 items.csv 텍스트 생성?** — 사용자가 파일로 저장
4. **Editor 스크립트 코딩 들어가기 전 검토?**

추천: **YES → 컬럼 분할 → 본 ticket 작업 시 같이**
