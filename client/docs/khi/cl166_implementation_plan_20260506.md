# CL-166 구현 계획서 (2026-05-06)

원본 plan: [`cl166_plan.md`](cl166_plan.md) (김회인 작성)
상위 분할 plan: [`balance-editor-snoopy-star.md`](../../../../Users/AD/.claude/plans/balance-editor-snoopy-star.md) — Balance Editor 카테고리 확장 전체 ticket 분할 (CL-166 ~ CL-173)
본 문서: 사용자와의 결정 합의 후 **실행 가능한 구현 계획** + 구현 결과.

---

## Context

Epic U 의 **마지막 ticket**. CL-163 의 `IBalanceCategoryProvider` 인터페이스에 추가 Provider 구현체 등록. Provider 추가만으로 트리/검색/Dirty/JSON Import/Export/Auto-save 자동 적용.

2점 P1, CL-165 + CL-090 + CL-138 의존.

### 분담
- **본 CL-166**: WeaponCategoryProvider / SkillCategoryProvider / ShopConfigCategoryProvider 신설 + BalanceEditorWindow 등록 + 알파벳 정렬
- 후속 별도 ticket: CL-167 (WeaponData 튜닝 필드 확장), CL-168~170 (Player 풀 마이그레이션), CL-171~173 (Enemy 풀 마이그레이션)
- 별도 ticket 후보: EnemyData SO 도입, RewardPool 등록 (SO ref import 선행), Reflection 자동 Provider 등록, AI 패턴 SO

---

## 1. 현황 (Phase 1)

### 1.1 CL-165 산출물 — 진입점 식별

[BalanceEditorWindow.cs](../../LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/BalanceEditorWindow.cs):
- `_providers` (List 125-129) — Provider 등록 위치. 본 CL 의 핵심 수정점
- `BuildTreeData` (471-) — 트리 빌드, 카테고리 정렬 적용 위치
- `CountAllDirty()` / `_treeView.RefreshItems()` / `SuppressAssetWatcher` — 신규 카테고리 자동 적용 (코드 변경 X)
- JSON Import/Export 헬퍼 — Provider 인터페이스 통해 자동 동작 (코드 변경 X)
- `BalanceEditorAssetWatcher` — 모든 SO 변경 감지, 신규 카테고리 자동 새로고침 (코드 변경 X)

[IBalanceCategoryProvider.cs](../../LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/IBalanceCategoryProvider.cs):
- 시그니처: `CategoryName / AssetTypeFilter (string) / LoadAll()`
- string 필터 패턴 — Editor asmdef 가 Assembly-CSharp SO 타입 직접 참조 안 함

[Providers/RelicCategoryProvider.cs](../../LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/Providers/RelicCategoryProvider.cs):
- 본 CL 의 Provider 패턴 원본 (복사 + 타입 교체)

### 1.2 인프라 자동 적용 매핑

| 기능 | 작동 방식 | 본 CL 영향 |
|---|---|---|
| 트리 표시 | `BuildTreeData` 가 `_providers` 순회 | Provider List 추가 |
| 검색 필터 | `FilterTree` 가 카테고리/리프 필터링 | 자동 |
| Dirty 마커 | `DirtyTracker.IsDirty` + `RefreshItems` | 자동 |
| Save All / Ctrl+S | `AssetDatabase.SaveAssets` 전체 | 자동 |
| Undo/Redo | `Undo.undoRedoPerformed` 핸들러 | 자동 |
| JSON Export | `provider.LoadAll()` 순회 | 자동 |
| JSON Import | 폴더 import 시 SO name 매핑 | 자동 |
| Auto-save | `CountAllDirty()` 발화 조건 | 자동 |
| AssetWatcher | `OnPostprocessAllAssets` → `RebuildTree` | 자동 |

→ **신규 코드는 Provider 3개 클래스 + InitializeProviders 수정 + 알파벳 정렬 1줄**. 그 외 모든 기능 자동 적용.

---

## 2. 결정사항

### 2.1 cl166_plan.md 의 결정 (원본)

| # | 항목 | 결정 |
|---|---|---|
| 1 | 단일 SO 카테고리 (ShopConfig) | **카테고리 등록** (일관성) |
| 2 | 카테고리 표시 순서 | **알파벳순** |
| 3 | 상위 그룹화 | **평면** (5개 카테고리) |
| 4 | RewardPool 등록 | **별도 ticket** (SO ref import 선행) |
| 5 | EnemyData 처리 | **존재 시 추가, 미존재 시 별도 ticket** |
| 6 | Reflection 자동 Provider 등록 | **별도 ticket** (수동 List 추가) |

### 2.2 본 plan 묶음 결정 (2026-05-06 사용자 합의)

| # | 항목 | 결정 |
|---|---|---|
| A | CL-166 의 위치 | 본 plan 묶음의 **1단계** (즉시 실행) |
| B | Player 도메인 | 별도 후속 ticket — **풀 SO 마이그레이션 (CL-168~170)** |
| C | Enemy 도메인 | 별도 후속 ticket — **EnemyData SO + prefab 마이그레이션 (CL-171~173)** |
| D | WeaponData 튜닝 필드 확장 | 별도 후속 ticket — **CL-167** (디자이너 회의 선행) |
| E | AI 패턴 / 시각 도구 | 본 plan 외, **별도 ticket 후보**로만 명시 |

---

## 3. 핵심 파일

### 3.1 신규 (3개)

| 경로 | 책임 |
|---|---|
| [Providers/WeaponCategoryProvider.cs](../../LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/Providers/WeaponCategoryProvider.cs) | `t:WeaponData` (`Runtime/Data/WeaponData.cs`) |
| [Providers/SkillCategoryProvider.cs](../../LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/Providers/SkillCategoryProvider.cs) | `t:SkillData` (클래스 미존재 — 0개 결과) |
| [Providers/ShopConfigCategoryProvider.cs](../../LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/Providers/ShopConfigCategoryProvider.cs) | `t:ShopConfig` (`Runtime/Shop/ShopConfig.cs`) |

### 3.2 수정 (1개)

| 경로 | 변경 |
|---|---|
| [BalanceEditorWindow.cs](../../LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/BalanceEditorWindow.cs) | `InitializeProviders` (125-132) 에 3개 Provider 추가 + `BuildTreeData` (475) 에 `OrderBy(p => p.CategoryName)` 알파벳 정렬 |

---

## 4. 구현 결과

### 4.1 단계 1~3: Provider 3개 신설

세 파일 모두 RelicCategoryProvider 패턴 그대로, `SearchFolders` 미명시 (전체 프로젝트 검색).

```csharp
// Editor/BalanceEditor/Providers/WeaponCategoryProvider.cs
public class WeaponCategoryProvider : IBalanceCategoryProvider
{
    public string CategoryName => "Weapons";
    public string AssetTypeFilter => "WeaponData";

    public IEnumerable<ScriptableObject> LoadAll()
    {
        var guids = AssetDatabase.FindAssets($"t:{AssetTypeFilter}");
        return guids
            .Select(g => AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                AssetDatabase.GUIDToAssetPath(g)))
            .Where(s => s != null);
    }
}
```

`SkillCategoryProvider` / `ShopConfigCategoryProvider` 도 동일 패턴, `CategoryName` / `AssetTypeFilter` 만 교체.

#### SearchFolders 결정 (RelicCategoryProvider 와의 차이)

`RelicCategoryProvider` 는 `SearchFolders = { "Assets/_Project/ScriptableObjects/Relics/Generated" }` 명시. 본 CL Provider 는 미명시 — 이유:

1. **Weapon 폴더명이 단수형 (`Weapon`)** — `Weapons` 가정 시 미스매치
2. **Skill 폴더 미존재** — CL-160 미완으로 위치 미정
3. **Shop 폴더에 `TestShopData` (다른 타입) 혼재** — 폴더 한정 시 불필요
4. **cl166_plan §6/7/8 코드 예시도 미명시**

전체 프로젝트 검색 비용은 SO 100~200개 수준이라 무시 가능.

### 4.2 단계 4: BalanceEditorWindow 등록 + 알파벳 정렬

```csharp
// 125-132
_providers = new List<IBalanceCategoryProvider>
{
    new RelicCategoryProvider(),
    new BuildSetCategoryProvider(),
    new WeaponCategoryProvider(),
    new SkillCategoryProvider(),
    new ShopConfigCategoryProvider(),
};
```

```csharp
// 471-475 — BuildTreeData
private List<TreeViewItemData<TreeNode>> BuildTreeData()
{
    var roots = new List<TreeViewItemData<TreeNode>>();
    int id = 0;
    foreach (var provider in _providers.OrderBy(p => p.CategoryName))
    {
        // ...
```

### 4.3 단계 5: EnemyData 미존재 검증

```
Grep: class EnemyData\b → No matches found (Assets/_Project/Scripts)
```

→ EnemyDataCategoryProvider 미포함. cl166_plan §3 결정 반영. CL-171 에서 EnemyData SO 신설 시 추가.

---

## 5. 위험 / 알려진 상황

### 5.1 SkillData 클래스 미정의 (CL-160 미완)

`Grep: class \w*Skill\w* : ScriptableObject → No matches`. CL-160 산출물이 아직 없음. 결과:

- `AssetDatabase.FindAssets("t:SkillData")` → 0개 반환 (예외 없음)
- 트리에 `Skills (0)` 표시
- cl166_plan §위험 2 결정 반영: "본 CL: 0개도 OK (Provider 자체는 등록)"

CL-160 완료 시 자동으로 트리에 SkillData asset 등장. 본 CL 추가 작업 불필요.

### 5.2 WeaponData asset 1개만 존재 (CL-157~160 미완)

현재 `Sword_Default.asset` 1개만. 28개 (검 7 + 활 7 + 봉 7 + 스태프 7) 가정은 CL-157~160 산출물. 트리에 `Weapons (1)` 표시. cl166_plan §위험 1 결정 반영.

### 5.3 ShopConfig 폴더에 다른 타입 혼재

`Assets/_Project/ScriptableObjects/Shop/` 안에:
- `ShopConfig.asset` (타입: ShopConfig) ✓ 본 카테고리
- `TestShopData.asset` (타입: 다른 클래스) — 자동 제외 (`t:ShopConfig` 필터)

### 5.4 EnemyData 미존재 — 별도 ticket

`class EnemyData` grep 결과 없음. EnemyCatalog (id→prefab 매핑) 만 존재. CL-171 에서 EnemyData SO 신설 + CL-172 에서 적 prefab 마이그레이션 + CL-173 에서 Provider 등록.

### 5.5 RelicCategoryProvider 의 SearchFolders 잠재 미스매치 (본 CL 외)

`RelicCategoryProvider` 의 `SearchFolders = "Assets/_Project/ScriptableObjects/Relics/Generated"` 인데 실제 RelicData asset 들이 `Relics/` 직접 하위에도 존재. 본 CL 범위 외 — 별도 점검 ticket 후보.

### 5.6 단일 SO 카테고리 디자이너 인지

`ShopConfig (1)` 표시 — 카테고리 펼치면 자식 1개. 의미 약함. cl166_plan §1 결정대로 일관성 우선. 향후 디자이너 피드백 시 단일 SO 평면화 ticket 검토.

---

## 6. 검증 시나리오 (사용자 Unity Editor 진행)

### 6.1 cl166_plan §6단계 12 시나리오

| # | 시나리오 | 통과 기준 |
|---|---|---|
| 1 | 트리 카테고리 표시 | Balance Editor 좌측 트리에 5개 카테고리, **알파벳순**: BuildSets / Relics / ShopConfig / Skills / Weapons |
| 2 | WeaponData 선택 + 디테일 | "Weapons (1)" 펼쳐 `Sword_Default` 클릭 → 우측 InspectorElement 에 `_displayName / _baseDamage / _comboInputWindow / _steps[]` 등 모든 필드 표시 |
| 3 | SkillData 선택 | "Skills (0)" 펼쳐도 자식 없음 — 카테고리 라벨만 정상 표시. 콘솔 에러/경고 없음 |
| 4 | ShopConfig 선택 | "ShopConfig (1)" 펼쳐 `ShopConfig` 클릭 → 디테일에 가격/가중치 등 모든 필드 |
| 5 | 검색 | "sword" 입력 → Weapons 카테고리만 표시. "config" 입력 → ShopConfig 표시 |
| 6 | 편집 + Dirty + Save | WeaponData `_baseDamage` 변경 → 트리 `* Sword_Default` 마커 + 윈도우 타이틀 `(1 unsaved)` → Ctrl+S → dirty 사라짐 |
| 7 | Undo | WeaponData 편집 → Ctrl+Z → 원복. 트리 마커 사라짐 |
| 8 | Export Selected | `Sword_Default` 선택 → Export ▾ > Selected SO → `Sword_Default.json` 파일 + 모든 필드 포함 |
| 9 | Import to Selected | 외부에서 JSON 수정 → Import ▾ > To Selected → SO 갱신 + Undo 가능 |
| 10 | Auto-save | Auto-save ON → WeaponData 편집 → Delay 후 자동 저장. statusbar 시각 표시 |
| 11 | 자동 새로고침 | Project 창에서 새 WeaponData asset 생성 → Balance Editor 트리 자동 갱신 (Weapons 2) |
| 12 | EnemyData 처리 | 트리에 EnemyData 카테고리 **미표시** (의도된 동작) |

### 6.2 환경 보강 (CL-166 신규)

| # | 시나리오 | 통과 기준 |
|---|---|---|
| A | 알파벳 정렬 안정성 | Refresh 버튼 여러 번 클릭 → 카테고리 순서 변하지 않음 |
| B | Skills 빈 카테고리 동작 | Skills 펼침/접기 → 자식 없음 표시. 클릭해도 디테일 패널 빈 상태 유지 |
| C | Export Current Category | Weapons 카테고리에서 Export ▾ > Current Category → `Sword_Default.json` 1개 파일 |
| D | Export All Categories | Export ▾ > All Categories → 폴더에 `Relics/`, `BuildSets/`, `Weapons/`, `Skills/`, `ShopConfig/` 하위 폴더 (Skills 는 비어 있을 수 있음) |
| E | 카테고리 별 Dirty count | 한 카테고리에서 여러 SO 편집 → 카테고리 라벨에 `[N dirty]` 표시 |

---

## 7. 후속 ticket 영향

| Ticket | CL-166 와의 관계 |
|---|---|
| **CL-167** WeaponData 튜닝 필드 확장 | 본 CL 의 Weapons 카테고리에 신규 필드가 InspectorElement 에 자동 표시 |
| **CL-168 ~ CL-170** Player 풀 마이그레이션 | PlayerStatsCategoryProvider 추가 → 본 CL 의 알파벳 정렬에 의해 자동 위치 |
| **CL-171 ~ CL-173** Enemy 풀 마이그레이션 | EnemyCategoryProvider 추가 — EnemyData SO 신설 후 |
| 별도: SO ref Import 처리 | RewardPool 등록 가능해짐 |
| 별도: Reflection 자동 Provider 등록 | 수동 List 추가 → Assembly scan |
| 별도: 단일 SO 카테고리 평면화 | ShopConfig 같은 1개 SO 가 카테고리 노드 없이 우측 패널 직접 노출 |
| 별도: GUID 기반 SO ref 직렬화 | JSON Import/Export 의 instanceID 깨짐 해결 |
| 별도: enum-name 직렬화 | enum 순서 변경 안전성 |

---

## 8. 예상 시간 vs 실제

| 단계 | 예상 (cl166_plan) | 실제 |
|---|---|---|
| 1단계 (WeaponCategoryProvider) | 15분 | ~5분 |
| 2단계 (SkillCategoryProvider) | 15분 | ~3분 |
| 3단계 (ShopConfigCategoryProvider) | 15분 | ~3분 |
| 4단계 (등록 + 정렬) | 15분 | ~5분 |
| 5단계 (EnemyData 확인) | 5분 | ~2분 (Grep 1회) |
| **코드 작성 합계** | 65분 | **~18분** |
| 6단계 (검증 12 시나리오) | 45분 | 사용자 진행 (별도) |

→ 패턴 복사 + 1줄 정렬 추가라 단순. 2점 ticket 부합.

---

## 9. 작업 순서 (사용자 + Claude 분담)

### Claude 작성 (코드) — 완료 ✓
- [x] 단계 1~3: Provider 3개 .cs 파일 생성
- [x] 단계 4: BalanceEditorWindow.cs InitializeProviders 수정 + BuildTreeData OrderBy 추가
- [x] 단계 5: EnemyData grep 검증

### 사용자 작업 (Unity 에디터)
- [ ] Unity 컴파일 모니터링 (Console 빨간 에러 없음 — namespace `LostMemory.Editor.BalanceEditor.Providers` 가 기존 asmdef 에 자동 포함)
- [ ] §6.1 검증 12 시나리오 실행
- [ ] §6.2 환경 보강 5 시나리오 실행
- [ ] 사용자 입장 — Balance Editor 가 디자이너에게 전달 가능 상태 확인
- [ ] commit / push / MR (메모리 가이드: Claude 영역 외)
- [ ] Jira 상태 변경 (메모리 가이드: Claude 영역 외)

### 환경 전환 시
- 핸드오프 양식 (`cl166_phase_1_handoff_<YYYYMMDD>.md`) 누적 기록

---

## 10. 결정사항 변경 시 영향

| 변경 시도 | 영향 |
|---|---|
| 카테고리 표시 순서 변경 (사용 빈도 / 등록 순) | `OrderBy` 변경 1줄 |
| 상위 그룹화 (Combat/Build/Economy 등) | `IBalanceCategoryProvider` 인터페이스에 `GroupName` 필드 추가 + `BuildTreeData` 2단계 그룹핑 — 인프라 변경 |
| RewardPool 등록 | RewardPoolCategoryProvider 추가 — 단 SO ref Import 깨짐 위험 (별도 ticket 선행) |
| Reflection 자동 등록 | `InitializeProviders` 를 `Assembly.GetExecutingAssembly().GetTypes().Where(t => typeof(IBalanceCategoryProvider).IsAssignableFrom(t))` 로 교체 — 별도 ticket |
| 단일 SO 카테고리 평면화 | `BuildTreeData` 가 SO 1개인 카테고리는 카테고리 노드 생략하고 리프만 추가 — UX 비일관성 위험 |
| EnemyDataCategoryProvider 추가 | EnemyData SO 클래스 정의 후 CL-173 에서 진행 — 본 CL 외 |

---

## 11. 핵심 인사이트 — 인프라 재활용

본 CL 은 **CL-163~165 의 인프라를 그대로 재활용**:

1. **`IBalanceCategoryProvider` 인터페이스** — string 기반 (`AssetTypeFilter`) 으로 Editor asmdef 와 Assembly-CSharp 의 결합도 0
2. **`BuildTreeData` 가 List 순회** — `OrderBy` 1줄로 정렬 정책 변경 가능
3. **CL-164 의 Dirty/Save/Undo 패턴** — Provider 추가만으로 자동 적용
4. **CL-165 의 JSON Import/Export + Auto-save** — Provider 인터페이스 통해 자동 동작
5. **`BalanceEditorAssetWatcher`** — 모든 SO 변경 감지, 신규 카테고리도 자동 새로고침

→ **2점 ticket 은 인프라가 일반화되었음을 보여주는 검증 과정**. Phase 2 (CL-167~170) / Phase 3 (CL-171~173) 도 동일 인프라 위에서 Provider 추가 + 마이그레이션 패턴.

---

## 12. Epic U 진행률 (CL-166 후)

| Ticket | Plan | Code |
|---|---|---|
| CL-162 EditorWindow 셸 | ✅ | ✅ |
| CL-163 트리뷰 + 디테일 + 검색 | ✅ | ✅ |
| CL-164 Dirty + Undo/Redo | ✅ | ✅ |
| CL-165 JSON Import/Export + Auto-save | ✅ | ✅ |
| **CL-166 데이터 카테고리 연결** | ✅ | ✅ ← 본 CL |

**Epic U: 5/5 ✅**

본 plan 묶음 (`balance-editor-snoopy-star.md`) 의 다음 단계: **Phase 1 의 CL-167** (WeaponData 튜닝 필드 확장 — 디자이너 회의 선행).
