# CL-166 — 데이터 카테고리 연결 (Weapons / Skills / Shop / 등)

## Context

Epic U 의 마지막 ticket. **CL-163 Provider 패턴 활용해서 모든 SO 카테고리 통합**.

2점 P1, CL-165 + CL-090 + CL-138 의존.

### 본 CL 책임 범위

CL-163 의 `IBalanceCategoryProvider` 인터페이스에 **추가 Provider 구현체** 등록:

1. **WeaponCategoryProvider** — WeaponData 28개 (CL-090/157~160)
2. **SkillCategoryProvider** — SkillData 2개 (CL-160)
3. **ShopConfigCategoryProvider** — ShopConfig 1개 (CL-152)
4. **(선택) EnemyDataCategoryProvider** — 존재 시 (현재 미존재 가능)

→ 신규 코드는 Provider 4개만. 기존 인프라 (트리뷰 / 디테일 / Dirty / Undo / Export/Import / Auto-save) 자동 적용.

→ **2점 ticket 표 적정** (~2시간).

### 기존 상태 (CL-162~165)

- ✅ EditorWindow 셸 + UXML/USS (CL-162)
- ✅ TreeView + InspectorElement + 검색 + IBalanceCategoryProvider 인터페이스 (CL-163)
- ✅ Dirty + Undo + 명시적 저장 + Ctrl+S (CL-164)
- ✅ JSON Import/Export + Auto-save 토글 (CL-165)
- ✅ 등록된 Provider 2개: RelicCategoryProvider, BuildSetCategoryProvider (CL-163)
- ❌ WeaponCategoryProvider — **본 CL 신설**
- ❌ SkillCategoryProvider — **본 CL 신설**
- ❌ ShopConfigCategoryProvider — **본 CL 신설**

### 카테고리 SO 인벤토리 점검

| 카테고리 | SO 타입 | 추정 수 | 상태 |
|---|---|---|---|
| Relics | RelicData | 75~77 | CL-163 등록됨 |
| BuildSets | BuildSetData | 16 | CL-163 등록됨 |
| Weapons | WeaponData | 28 (검 7 + 활 7 + 봉 7 + 스태프 7) | **본 CL** |
| Skills | SkillData | 2 (메테오, 힐) | **본 CL** |
| ShopConfig | ShopConfig | 1 | **본 CL** |
| RewardPool | RewardPool | 1 | 검토 (단일 SO, 트리에 의미?) |
| EnemyData | (미정) | (미정) | **존재 시 추가** — 현재 미정 |

→ **본 CL 추가 카테고리 = 3~4개**.

---

## 결정사항

### 1. 단일 SO 카테고리 (ShopConfig, RewardPool) 처리

**문제**: ShopConfig / RewardPool 은 1~2개 SO 만. 카테고리 노드 펼치면 자식 1개. 카테고리 의미 약함.

**옵션**:
- (a) 그래도 카테고리 등록 (일관성)
- (b) "Misc" 또는 "Singletons" 카테고리로 묶기
- (c) 단일 SO 는 우측 패널 직접 표시 (트리 노드 X)

**채택: (a) + 카테고리 라벨 명확화**.
- 일관성 우선 (Provider 패턴 그대로)
- 카테고리 라벨이 SO 1개라도 자연스러움 (`ShopConfig (1)`)
- (b) 통합은 디자이너 검색 어려움
- (c) 는 인터페이스 깨짐

### 2. RewardPool 등록 — 본 CL 미포함

**이유**:
- RewardPool 은 ItemData ref 배열 (CL-152) → JsonUtility Import 시 ref 깨짐 위험 (CL-165 §위험)
- 별도 ticket 으로 SO ref Import 처리 후 등록 안전

본 plan: **RewardPool 미등록**. 별도 ticket.

→ 본 CL 추가 = WeaponData / SkillData / ShopConfig **3개**.

### 3. EnemyData — 미존재 시 skip

`EnemyData` SO 가 현재 프로젝트에 존재하는지 확인:

```csharp
// 검증 단계에서:
var found = AssetDatabase.FindAssets("t:EnemyData");
Debug.Log($"EnemyData count: {found.Length}");
```

- 존재 시: EnemyDataCategoryProvider 추가
- 미존재 시: skip + 별도 ticket 예약 ("Enemy SO 도입 후 Provider 추가")

본 plan: **유연 처리** — 존재 시만 추가.

### 4. Provider 등록 위치

CL-163 의 `BalanceEditorWindow._providers` 초기화 (Awake 또는 OnEnable):

```csharp
private void InitializeProviders()
{
    _providers = new List<IBalanceCategoryProvider>
    {
        new RelicCategoryProvider(),
        new BuildSetCategoryProvider(),
        // CL-166 추가:
        new WeaponCategoryProvider(),
        new SkillCategoryProvider(),
        new ShopConfigCategoryProvider(),
        // 조건부:
        // new EnemyDataCategoryProvider(),
    };
}
```

→ 단순 List 추가. 자동 등록 (Reflection) 은 후속 ticket.

### 5. 카테고리 표시 순서

트리에서의 표시 순서 정책:
- (a) Provider 등록 순서
- (b) **알파벳순** ⭐
- (c) 사용 빈도 순

**채택: (b)**.
- 디자이너가 찾기 쉬움
- 신규 카테고리 추가 시 위치 자동 결정

```csharp
// BuildTreeData 정렬
foreach (var provider in _providers.OrderBy(p => p.CategoryName))
    // ...
```

→ 결과: BuildSets / Relics / ShopConfig / Skills / Weapons (알파벳).

### 6. WeaponCategoryProvider

```csharp
public class WeaponCategoryProvider : IBalanceCategoryProvider
{
    public string CategoryName => "Weapons";
    public Type SoType => typeof(WeaponData);

    public IEnumerable<ScriptableObject> LoadAll()
    {
        var guids = AssetDatabase.FindAssets("t:WeaponData");
        return guids
            .Select(g => AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(w => w != null)
            .OrderBy(w => w.name)   // 무기 이름 알파벳
            .Cast<ScriptableObject>();
    }
}
```

→ RelicCategoryProvider 패턴 그대로 (CL-163 §1단계 코드).

### 7. SkillCategoryProvider

```csharp
public class SkillCategoryProvider : IBalanceCategoryProvider
{
    public string CategoryName => "Skills";
    public Type SoType => typeof(SkillData);

    public IEnumerable<ScriptableObject> LoadAll()
    {
        var guids = AssetDatabase.FindAssets("t:SkillData");
        return guids
            .Select(g => AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(s => s != null)
            .OrderBy(s => s.name)
            .Cast<ScriptableObject>();
    }
}
```

### 8. ShopConfigCategoryProvider

```csharp
public class ShopConfigCategoryProvider : IBalanceCategoryProvider
{
    public string CategoryName => "ShopConfig";
    public Type SoType => typeof(ShopConfig);

    public IEnumerable<ScriptableObject> LoadAll()
    {
        var guids = AssetDatabase.FindAssets("t:ShopConfig");
        return guids
            .Select(g => AssetDatabase.LoadAssetAtPath<ShopConfig>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(c => c != null)
            .Cast<ScriptableObject>();
    }
}
```

### 9. 카테고리 그룹화 (선택, 본 CL 미포함)

회의록 (tickets-master) "빌드/무기/적/상점 SO들 묶기" → **상위 그룹** 가능?

```
Combat
 ├ Weapons (28)
 └ Skills (2)
Build
 ├ Relics (75)
 └ BuildSets (16)
Economy
 └ ShopConfig (1)
```

**옵션**:
- (a) **평면 카테고리** ⭐ (CL-163 패턴) — 5개 카테고리 평행
- (b) 상위 그룹 도입 — 트리 깊이 +1

**채택: (a)**.
- 5~6개 카테고리는 평면도 충분히 보기 쉬움
- (b) 는 인터페이스 변경 (CategoryGroup 필드 추가) → CL-163 인프라 영향
- 후속 ticket 에서 그룹화 (10+ 카테고리 시)

### 10. AssetPostprocessor 자동 새로고침 — 자동 적용

CL-163 의 `BalanceEditorAssetWatcher` 가 모든 SO 변경 감지 → 본 CL 추가 카테고리도 자동 새로고침. **추가 작업 X**.

---

## 핵심 파일

### 신규

| 경로 | 내용 |
|---|---|
| `Editor/BalanceEditor/Providers/WeaponCategoryProvider.cs` | WeaponData |
| `Editor/BalanceEditor/Providers/SkillCategoryProvider.cs` | SkillData |
| `Editor/BalanceEditor/Providers/ShopConfigCategoryProvider.cs` | ShopConfig |

### 수정

| 경로 | 변경 |
|---|---|
| `BalanceEditorWindow.cs` (CL-162~165) | InitializeProviders 에 3개 Provider 추가, 알파벳 정렬 |

### 영향 (사용자 visible)

| 변경 |
|---|
| Balance Editor 트리에 추가 카테고리 3개 표시 |
| 검색 / Dirty / Undo / Export/Import / Auto-save 자동 적용 |

---

## 구현 단계

### 1단계: WeaponCategoryProvider (15분)

§6 코드. RelicCategoryProvider 복사 + 타입 교체.

### 2단계: SkillCategoryProvider (15분)

§7 코드.

### 3단계: ShopConfigCategoryProvider (15분)

§8 코드.

### 4단계: BalanceEditorWindow 등록 + 알파벳 정렬 (15분)

§4 + §5 코드:
- `InitializeProviders` 에 3개 추가
- `BuildTreeData` 에서 `OrderBy(p => p.CategoryName)` 적용

### 5단계: EnemyData 존재 확인 (5분)

```bash
# 빠른 확인
grep -r "class EnemyData" Assets/_Project/Scripts/
```

존재 시: 4단계로 돌아가서 EnemyDataCategoryProvider 추가.
미존재 시: 본 plan §위험 에 명시 + 별도 ticket 예약.

### 6단계: 검증 (45분)

```
시나리오 1: 트리에 신규 카테고리 표시
- Balance Editor 열기
- 좌측 트리에 5개 카테고리 (알파벳순):
  - BuildSets (16)
  - Relics (75)
  - ShopConfig (1)
  - Skills (2)
  - Weapons (28)

시나리오 2: WeaponData 선택 + 디테일
- "Weapons (28)" 펼치기 → "Sword_Default" 등 28개
- "Sword_Dagger" 클릭 → 우측 InspectorElement
- _baseDamage / _stage / _upgrades / _element 등 모든 필드 표시

시나리오 3: SkillData 선택
- "Skills (2)" → "Skill_Meteor" / "Skill_Heal"
- 디테일 정상

시나리오 4: ShopConfig 선택
- "ShopConfig (1)" → 1개 SO
- 디테일에 가격 / 가중치 등 모든 필드

시나리오 5: 검색
- "sword" 입력 → Weapons 카테고리만 표시 (검 무기들)
- "skill_" 입력 → Skills 카테고리

시나리오 6: 편집 + Dirty + Save
- WeaponData _baseDamage 변경 → 트리 "* Sword_Dagger"
- 타이틀 "Balance Editor (1 unsaved)"
- Save 버튼 / Ctrl+S → dirty 사라짐

시나리오 7: Undo
- WeaponData 편집 → Ctrl+Z → 원복

시나리오 8: Export
- "Sword_Dagger" 선택 → Export ▾ > Selected
- "Sword_Dagger.json" 저장
- 파일 내용 확인 (모든 필드)

시나리오 9: Import
- 외부에서 JSON 수정 → Import ▾ > To Selected
- SO 갱신 + Undo 가능

시나리오 10: Auto-save
- Auto-save ON
- WeaponData 편집 → 30s 후 자동 저장

시나리오 11: 자동 새로고침
- 새 WeaponData asset 생성 → 트리 자동 갱신 (Weapons 29)

시나리오 12: EnemyData 처리
- 존재 시: 트리에 "EnemyData" 카테고리 표시
- 미존재 시: 트리에 4개 카테고리만 + 본 plan 위험에 명시
```

---

## 위험 / 결정 미정

### 위험

1. **WeaponData asset 미존재 가능성**: CL-090 의 Sword_Default 외 28개 (CL-157~160 산출물) 가 미생성 상태일 수 있음. → CL-157~160 완료 후 본 CL 시작 권장. 또는 1개만 표시 (자연스러움).
2. **SkillData asset 미존재 가능성**: CL-160 산출물. CL-160 미완 시 0개 표시. → 트리에 "Skills (0)" 표시 + dummy 가능. **본 CL: 0개도 OK** (Provider 자체는 등록).
3. **ShopConfig SO 위치 가정**: AssetDatabase.FindAssets 가 전체 프로젝트 검색. ShopConfig 가 정의되어 있는지 확인 필수 — CL-152 에서 사용된 타입이라 존재 가능성 높음.
4. **EnemyData 미존재 — 별도 ticket**: 현재 미정의. 적 SO 도입 시 추가. 본 plan 미포함.
5. **RewardPool 미등록 위험 인정**: SO ref 배열 → JSON Import 깨짐 (CL-165 §위험). 별도 ticket 예약. 디자이너가 RewardPool 편집 원하면 Project 윈도우에서 직접 (Balance Editor 외).
6. **카테고리 수 ~ 5개 — 트리 단순 OK**: 향후 10+ 시 그룹화 필요.
7. **단일 SO 카테고리 (ShopConfig (1))**: 카테고리 펼침 의미 약함. 디자이너 헷갈릴 수 있음. → §1 라벨 명확화 + 후속 ticket "단일 SO 평면화" 옵션.
8. **Reflection 자동 등록 미적용**: 향후 Provider 자동 발견 (Assembly scan + 인터페이스 구현체) 가능. 본 CL 미포함.

### 결정 미정

- [ ] 단일 SO 카테고리 — 본 plan: **카테고리 등록 (일관성)**
- [ ] 카테고리 표시 순서 — 본 plan: **알파벳순**
- [ ] 상위 그룹화 — 본 plan: **평면 (5개)**
- [ ] RewardPool 등록 — 본 plan: **별도 ticket**
- [ ] EnemyData 처리 — 본 plan: **존재 시만 추가, 미존재 시 별도 ticket**
- [ ] Reflection 자동 등록 — 본 plan: **수동 List 추가, 후속 자동화**

---

## 후속 ticket 영향

| Ticket | CL-166 과의 관계 |
|---|---|
| **Epic U 마무리** | 본 CL 후 Epic U 모두 완료 ✅ |
| **별도 ticket: SO ref Import 처리** | RewardPool 등록 가능해짐 |
| **별도 ticket: EnemyData Provider** | EnemyData 도입 후 |
| **별도 ticket: 카테고리 상위 그룹화** | 10+ 카테고리 시 |
| **별도 ticket: Reflection 자동 등록** | 새 Provider 클래스 추가 시 자동 발견 |
| **별도 ticket: Custom UXML per SO 타입** | 각 SO 타입별 디자이너 친화 정렬 |

---

## 예상 시간

| 단계 | 시간 |
|---|---|
| 1단계 (WeaponCategoryProvider) | 15분 |
| 2단계 (SkillCategoryProvider) | 15분 |
| 3단계 (ShopConfigCategoryProvider) | 15분 |
| 4단계 (등록 + 정렬) | 15분 |
| 5단계 (EnemyData 확인) | 5분 |
| 6단계 (검증 12 시나리오) | 45분 |
| **합계** | **약 1시간 50분** |

→ 2점 ticket 에 부합 (단순 Provider 추가).

---

## 결정 요청

| # | 질문 | 옵션 | 추천 |
|---|---|---|---|
| 1 | 단일 SO 카테고리 (ShopConfig) | **등록** / Misc 그룹 / 직접 표시 | **등록** |
| 2 | 카테고리 표시 순서 | **알파벳** / Provider 등록 순 / 사용 빈도 | **알파벳** |
| 3 | 상위 그룹화 | 본 CL / **별도** | **별도** |
| 4 | RewardPool 등록 | 본 CL / **별도** | **별도** (ref 깨짐 위험) |
| 5 | EnemyData | 존재 시 추가 / **항상 별도** | **존재 시 추가** |
| 6 | 자동 등록 (Reflection) | 본 CL / **별도** | **별도** |

전부 추천대로면 **등록 + 알파벳 + 별도그룹 + RewardPool별도 + 존재시추가 + 별도자동**.

---

## Epic U 진행률 (CL-166 후)

| Ticket | Plan | Code |
|---|---|---|
| CL-162 EditorWindow 셸 | ✅ | - |
| CL-163 트리뷰 + 디테일 + 검색 | ✅ | - |
| CL-164 Dirty + Undo/Redo | ✅ | - |
| CL-165 JSON Import/Export + Auto-save | ✅ | - |
| **CL-166 데이터 카테고리 연결** | ✅ ← 방금 | - |

**Epic U: 5/5 ✅**

---

## 🎉 Epic U 전체 완료 + 클라1 plan 진행률

| Epic | Tickets | Plan |
|---|---|---|
| **Epic S 빌드 시스템** | 17 (CL-138 ~ CL-154) | ✅ 완료 (CL-149 클라3 별도) |
| **Epic K 무기 확장** | 7 (CL-155 ~ CL-161) | ✅ 완료 |
| **Epic U 밸런스 에디터** | 5 (CL-162 ~ CL-166) | ✅ **완료** |

**클라1 plan: 29/29 ✅** (CL-149 제외 모든 클라1 ticket plan 완료)

---

## 다음 단계

Epic U 마무리 + 모든 클라1 ticket plan 완료. 다음 옵션:

| 옵션 | 작업 | 비고 |
|---|---|---|
| **A** | Epic V (스토리/컷씬) plan — CL-167~171 | 5개 ticket, 다른 Epic 진입 |
| B | 코드 진입 — Epic S Phase 1 (CL-138 부터) 구현 시작 | plan 다 끝났으니 실제 구현 |
| C | 다른 Epic / Epic 외 ticket | 사용자 우선순위 |
| D | plan 정리 / 통합 검토 (CL-103 통합 ticket 등) | meta 정리 |

**추천: B (코드 진입)** — plan 모두 끝났으니 이제 실제 구현 시작. CL-138 (Epic S 첫 ticket) 부터 순차 진행 가능.

뭐로 갈까요?
