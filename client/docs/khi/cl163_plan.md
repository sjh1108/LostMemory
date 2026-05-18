# CL-163 — 좌측 카테고리 트리뷰 + 우측 디테일 패널 + 검색

## Context

Epic U 의 본체 ticket. **CL-162 의 빈 패널을 실제 데이터 표시 / 편집으로 채움**.

5점 P1, CL-162 의존.

### 본 CL 책임 범위 (3축)

1. **좌측 트리뷰** — 카테고리 노드 (Relics / BuildSets / Weapons / Skills 등) + 자식 SO 리프
2. **우측 디테일 패널** — 트리뷰 선택 시 해당 SO 의 SerializedObject 편집 UI
3. **검색 박스** — 트리뷰 필터링 (이름 substring)
4. **선택 동기화** — 트리뷰 ↔ 디테일 ↔ Project 윈도우 (선택 시 highlight)

→ **데이터 카테고리 연결은 본 CL 에서 일부만** (RelicData / BuildSetData 위주). 모든 카테고리 (WeaponData / SkillData / Enemy / Shop) 통합은 **CL-166**.

→ **Dirty 마커 / Undo 는 CL-164** — 본 CL 은 단순 SerializedObject.ApplyModifiedProperties.

### 기존 상태 (CL-162 산출물)

- ✅ `BalanceEditorWindow` (CL-162) — 빈 패널 + `PopulateLeftPanel` / `PopulateRightPanel` virtual
- ✅ UXML/USS 셸 — `LeftPanel` / `RightPanel` named element
- ❌ 트리뷰 / 디테일 / 검색 — **본 CL 신설**

---

## 결정사항

### 1. 트리뷰 컴포넌트 — UI Toolkit `TreeView` ⭐

**옵션**:
- (a) **UI Toolkit `TreeView`** ⭐ — Unity 6 정식 지원
- (b) `ListView` 평면화 + 카테고리 그룹 헤더
- (c) Custom VisualElement 트리

**채택: (a)**.
- 표준 컴포넌트, 학습 곡선 적음
- 가상화 지원 (대량 SO 도 빠름)
- 확장 / 축소 자체 처리

```csharp
var treeView = new TreeView();
treeView.makeItem = () => new Label();
treeView.bindItem = (element, index) => {
    var item = treeView.GetItemDataForIndex<TreeNode>(index);
    ((Label)element).text = item.DisplayName;
};
treeView.SetRootItems(BuildTreeData());
```

### 2. 카테고리 정의 — IBalanceCategoryProvider 인터페이스

확장 가능한 구조:
```csharp
public interface IBalanceCategoryProvider
{
    string CategoryName { get; }            // "Relics", "BuildSets" 등
    IEnumerable<ScriptableObject> LoadAll();
    Type SoType { get; }                     // RelicData, BuildSetData 등
}
```

본 CL 구현 카테고리 (2~3개):
- `RelicCategoryProvider` — 75 RelicData
- `BuildSetCategoryProvider` — 16 BuildSetData
- (선택) `WeaponCategoryProvider` — 28 WeaponData

→ 본 CL 은 인터페이스 + 위 2~3개. WeaponData / SkillData / Enemy 등 나머지는 **CL-166**.

CL-166 이 추가 provider 등록만 하면 트리에 자동 등장.

### 3. 트리 데이터 구조

```csharp
public class TreeNode
{
    public string DisplayName;
    public ScriptableObject So;          // null = 카테고리 노드
    public string CategoryName;
    public List<TreeNode> Children = new();
}
```

루트 = 가짜 노드, 자식 = 카테고리, 손자 = SO 리프.

```
Root
├ Relics (75)
│  ├ 전사의 끈
│  ├ 분쇄의 팔찌
│  └ ...
├ BuildSets (16)
│  ├ BuildSet_불
│  ├ BuildSet_얼음
│  └ ...
└ (CL-166: Weapons / Skills / Enemies / Shop)
```

### 4. 디테일 패널 — `InspectorElement` 활용

UI Toolkit 의 `InspectorElement` 가 SerializedObject 자동 그려줌 (Unity 2022+):

```csharp
private void ShowDetail(ScriptableObject so)
{
    rightPanel.Clear();
    if (so == null) return;

    var inspector = new InspectorElement(so);
    rightPanel.Add(inspector);
}
```

→ Unity 가 모든 필드 자동 표시 (Custom Editor 불필요). RelicData / BuildSetData 의 모든 SerializedField 자동 노출.

**대안**:
- Custom UXML per SO 타입 — 디자이너 친화 폴리시. 별도 ticket.

### 5. 검색 박스 — 트리뷰 필터링

```csharp
var searchField = new ToolbarSearchField();
searchField.RegisterValueChangedCallback(evt => {
    string query = evt.newValue;
    var filtered = FilterTree(allNodes, query);
    treeView.SetRootItems(filtered);
    treeView.Rebuild();
});
```

`FilterTree`:
- query 가 빈 문자열 → 전체 트리
- query 가 있음 → 카테고리 노드 유지 + 자식 중 substring match 만 표시
- 매치 자식 없는 카테고리는 제외 (또는 빈 카테고리 표시 옵션)

→ 매치는 case-insensitive + substring.

### 6. 선택 흐름

1. 트리뷰 노드 클릭 → 디테일 패널 갱신
2. (옵션) Project 윈도우에서 같은 SO highlight (`EditorGUIUtility.PingObject(so)`)
3. status bar 에 "Selected: {so.name}" 표시

```csharp
treeView.selectionChanged += (selected) => {
    var node = selected.FirstOrDefault() as TreeNode;
    if (node?.So != null)
    {
        ShowDetail(node.So);
        EditorGUIUtility.PingObject(node.So);
        UpdateStatus($"Selected: {node.So.name}");
    }
};
```

### 7. 트리 새로고침 — 수동 + AssetDatabase 변경 감지

**옵션**:
- (a) 수동 새로고침 버튼 (toolbar)
- (b) `AssetPostprocessor` — 새 SO 추가/삭제 시 자동
- (c) 양쪽

**채택: (c)**. (a) 가 안전망, (b) 가 편의.

```csharp
public class BalanceEditorAssetWatcher : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFromAssetPaths)
    {
        if (BalanceEditorWindow.IsOpen)
            BalanceEditorWindow.RefreshTree();
    }
}
```

본 CL: AssetPostprocessor + toolbar 새로고침 버튼.

### 8. Splitter — 좌측/우측 너비 조절

UI Toolkit `TwoPaneSplitView`:
```csharp
var splitter = new TwoPaneSplitView(0, 250, TwoPaneSplitViewOrientation.Horizontal);
splitter.Add(leftPanel);
splitter.Add(rightPanel);
```

- 좌측 디폴트 너비 250
- 사용자가 드래그로 조절
- EditorPrefs 에 너비 저장 (다음 세션 복원, 별도 ticket)

CL-162 의 `be-splitter` 는 단순 시각만 → 본 CL 에서 `TwoPaneSplitView` 로 교체.

### 9. 상태 표시 (StatusBar)

CL-162 의 `StatusLabel` 활용:
- "Ready" — 디폴트
- "Selected: {so.name}" — 선택 시
- "Loaded {N} categories, {M} items" — 트리 새로고침 후

### 10. 카테고리 노드 visualization

카테고리 라벨 = `[카테고리명 (count)]` 형식:
- `Relics (75)`
- `BuildSets (16)`
- `Weapons (28)`

→ 한눈에 SO 수 파악.

---

## 핵심 파일

### 신규

| 경로 | 내용 |
|---|---|
| `Editor/BalanceEditor/IBalanceCategoryProvider.cs` | 카테고리 인터페이스 |
| `Editor/BalanceEditor/Providers/RelicCategoryProvider.cs` | RelicData 75개 |
| `Editor/BalanceEditor/Providers/BuildSetCategoryProvider.cs` | BuildSetData 16개 |
| `Editor/BalanceEditor/TreeNode.cs` | 트리 데이터 구조 |
| `Editor/BalanceEditor/BalanceEditorAssetWatcher.cs` | AssetPostprocessor |

### 수정

| 경로 | 변경 |
|---|---|
| `BalanceEditorWindow.cs` (CL-162) | TreeView / SearchField / TwoPaneSplitView 추가, PopulateLeftPanel / PopulateRightPanel override |
| `BalanceEditorWindow.uxml` (CL-162) | toolbar 에 ToolbarSearchField + Refresh 버튼 추가 |
| `BalanceEditorWindow.uss` (CL-162) | 트리뷰 / 검색 / 디테일 추가 스타일 |

---

## 구현 단계

### 1단계: IBalanceCategoryProvider 인터페이스 + 2개 구현 (45분)

§2 인터페이스 + RelicCategoryProvider + BuildSetCategoryProvider:

```csharp
public class RelicCategoryProvider : IBalanceCategoryProvider
{
    public string CategoryName => "Relics";
    public Type SoType => typeof(RelicData);

    public IEnumerable<ScriptableObject> LoadAll()
    {
        var guids = AssetDatabase.FindAssets("t:RelicData");
        return guids
            .Select(g => AssetDatabase.LoadAssetAtPath<RelicData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(r => r != null)
            .Cast<ScriptableObject>();
    }
}
```

BuildSetProvider 동일 패턴.

### 2단계: TreeNode + 트리 빌드 (30분)

§3 구조 + BuildTreeData 메서드:

```csharp
private List<TreeViewItemData<TreeNode>> BuildTreeData()
{
    var roots = new List<TreeViewItemData<TreeNode>>();
    int id = 0;
    foreach (var provider in _providers)
    {
        var children = new List<TreeViewItemData<TreeNode>>();
        var sos = provider.LoadAll().ToList();
        foreach (var so in sos)
        {
            var leafNode = new TreeNode { So = so, DisplayName = so.name, CategoryName = provider.CategoryName };
            children.Add(new TreeViewItemData<TreeNode>(id++, leafNode));
        }
        var catNode = new TreeNode {
            DisplayName = $"{provider.CategoryName} ({sos.Count})",
            CategoryName = provider.CategoryName,
        };
        roots.Add(new TreeViewItemData<TreeNode>(id++, catNode, children));
    }
    return roots;
}
```

### 3단계: BalanceEditorWindow 통합 (1시간)

1. `_providers: List<IBalanceCategoryProvider>` 필드 (Awake 또는 OnEnable 시 등록)
2. `PopulateLeftPanel(panel)` override:
   - TreeView 생성 + 데이터 바인딩
   - selectionChanged 등록
3. `PopulateRightPanel(panel)` override:
   - 비움 (디테일은 선택 시 채움)
4. `ShowDetail(so)` 메서드 — InspectorElement 표시
5. UpdateStatus 메서드

### 4단계: 검색 박스 + 필터링 (45분)

1. UXML toolbar 에 `ToolbarSearchField` 추가
2. ValueChangedCallback 등록
3. FilterTree 메서드:

```csharp
private List<TreeViewItemData<TreeNode>> FilterTree(string query)
{
    if (string.IsNullOrEmpty(query)) return BuildTreeData();
    var lower = query.ToLowerInvariant();
    // 카테고리 + match 자식만 포함
    // ...
}
```

### 5단계: TwoPaneSplitView (30분)

CL-162 의 단순 splitter → TwoPaneSplitView 교체:
- UXML 수정 또는 코드 동적 추가
- 좌측 디폴트 250px
- Horizontal orientation

### 6단계: AssetPostprocessor 자동 새로고침 + Refresh 버튼 (30분)

1. `BalanceEditorAssetWatcher` 작성
2. `BalanceEditorWindow.IsOpen` static 프로퍼티
3. `BalanceEditorWindow.RefreshTree()` static 메서드
4. UXML toolbar 에 `Button` 추가 (text: "Refresh")
5. 클릭 시 RefreshTree 호출

### 7단계: 검증 (1시간 30분)

```
시나리오 1: 트리 표시
- 윈도우 열기 → 좌측 트리에 "Relics (75)" / "BuildSets (16)" 표시
- 카테고리 노드 펼치기 → 자식 SO 75개/16개 표시

시나리오 2: SO 선택
- "전사의 끈" 클릭 → 우측 디테일에 InspectorElement
- _tagPrimary / _tagSecondary / _size / _effects 등 모든 필드 표시
- Project 윈도우에서 해당 asset highlight (PingObject)

시나리오 3: 디테일 편집
- _baseDamage 슬라이더 변경 → asset 즉시 업데이트
- (Dirty 마커 / Undo 는 CL-164 에서)

시나리오 4: 검색
- 검색 박스에 "전사" 입력 → "전사의 끈" 만 표시
- 빈 입력 → 전체 트리 복원
- "z" 입력 → 매치 0 → 빈 카테고리만 (또는 빈 트리)

시나리오 5: 카테고리 카운트
- "Relics (75)" 라벨 정확
- "BuildSets (16)" 라벨 정확

시나리오 6: 자동 새로고침
- 새 RelicData asset 생성 (Project 윈도우)
- 트리 자동 갱신 (Relics 76)

시나리오 7: 수동 Refresh 버튼
- toolbar Refresh 클릭 → 트리 재빌드

시나리오 8: Splitter 드래그
- 좌측/우측 경계 드래그 → 너비 조절
- 좌측 100~600 범위

시나리오 9: 상태 표시
- 선택 시 status bar "Selected: 전사의 끈"
- Refresh 후 "Loaded 2 categories, 91 items"

시나리오 10: 윈도우 닫기/재오픈
- 닫고 다시 열어도 정상 작동
- 도메인 리로드 후도 OK
```

---

## 위험 / 결정 미정

### 위험

1. **TreeView API Unity 버전 의존**: Unity 6 의 `TreeView` 와 2022 의 시그니처 차이. → Unity 6 기준 작성 (`TreeViewItemData<T>`).
2. **InspectorElement 가 모든 필드 자동 표시**: `[HideInInspector]` 또는 `[SerializeField] private` 만 표시. 본 plan 의 RelicData/BuildSetData 호환 점검.
3. **75 + 16 SO 로드 성능**: AssetDatabase.FindAssets + LoadAsset = ~100 SO 로드. 즉시 1초 이내. 향후 1000+ 시 lazy loading 고려.
4. **검색 필터링 시 트리 펼침 상태 손실**: SetRootItems → 모두 접힘. → 검색 시 자동 펼침 (`treeView.ExpandAll()` 호출).
5. **SerializedObject 변경 즉시 저장 X**: ApplyModifiedProperties 호출해도 disk 저장은 별도 (`AssetDatabase.SaveAssets()` 또는 Ctrl+S). → CL-164 에서 Dirty + 명시적 저장 처리. 본 CL 은 즉시 적용 (Unity 표준).
6. **AssetPostprocessor 정적 — 윈도우 닫혀도 호출**: BalanceEditorWindow.IsOpen 가드.
7. **카테고리 SO 폴더 위치 가정**: AssetDatabase.FindAssets 가 전체 프로젝트 검색. 폴더 한정 옵션 (RelicCategoryProvider 가 폴더 명시). 본 plan: 전체 검색 (단순).
8. **InspectorElement 가 Editor 빌드만 작동**: asmdef Editor 전용 보장 (CL-162 처리).
9. **SO 이름 변경 시 트리 갱신**: AssetPostprocessor 가 OnPostprocessAllAssets 의 moved 항목 감지. 자동 처리.

### 결정 미정

- [ ] TreeView vs ListView — 본 plan: **TreeView**
- [ ] 디테일 패널 — InspectorElement 자동 / Custom UXML — 본 plan: **InspectorElement** (자동)
- [ ] 검색 매치 시 트리 펼침 — 본 plan: **자동 펼침**
- [ ] 카테고리 폴더 한정 — 본 plan: **전체 프로젝트 검색** (단순)
- [ ] Splitter 너비 영속성 — 본 plan: **별도 ticket**
- [ ] Custom UXML per SO 타입 — 본 plan: **별도 ticket**

---

## 후속 ticket 영향

| Ticket | CL-163 과의 관계 |
|---|---|
| **CL-164 (Dirty + Undo)** | 본 CL 의 InspectorElement 변경 → Dirty 표시 + Undo 통합 |
| **CL-165 (JSON Import/Export)** | toolbar 에 버튼 추가 |
| **CL-166 (데이터 카테고리 연결)** | WeaponProvider / SkillProvider / EnemyProvider / ShopProvider 추가 (본 CL 인터페이스 구현) |
| **별도 ticket: Custom UXML per SO** | InspectorElement 자동 → 디자이너 친화 정렬 |
| **별도 ticket: Splitter 너비 영속성** | EditorPrefs 저장 |
| **별도 ticket: 트리 펼침 상태 영속성** | 다음 세션 복원 |

---

## 예상 시간

| 단계 | 시간 |
|---|---|
| 1단계 (Provider 인터페이스 + 2개) | 45분 |
| 2단계 (TreeNode + 트리 빌드) | 30분 |
| 3단계 (TreeView 통합) | 1시간 |
| 4단계 (검색 박스) | 45분 |
| 5단계 (TwoPaneSplitView) | 30분 |
| 6단계 (자동 새로고침 + Refresh 버튼) | 30분 |
| 7단계 (검증 10 시나리오) | 1시간 30분 |
| **합계** | **약 5시간 30분** |

→ 5점 ticket 에 부합.

---

## 결정 요청

| # | 질문 | 옵션 | 추천 |
|---|---|---|---|
| 1 | 트리뷰 컴포넌트 | **TreeView** / ListView | **TreeView** |
| 2 | 디테일 패널 | **InspectorElement** / Custom UXML | **InspectorElement** (단순) |
| 3 | 검색 매치 시 펼침 | **자동** / 수동 | **자동** |
| 4 | 본 CL 카테고리 수 | **2개 (Relics + BuildSets)** / 3개 (+ Weapons) | **2개** (CL-166 위임) |
| 5 | Splitter | **TwoPaneSplitView** / Custom | **TwoPaneSplitView** (표준) |
| 6 | 자동 새로고침 | **AssetPostprocessor + 버튼** / 버튼만 | **둘 다** |
| 7 | Splitter 너비 영속성 | 본 CL / **별도** | **별도** |

전부 추천대로면 **TreeView + InspectorElement + 자동펼침 + 2카테고리 + TwoPane + 둘다 + 별도**.

---

## Epic U 진행률 (CL-163 후)

| Ticket | Plan |
|---|---|
| CL-162 EditorWindow 셸 | ✅ |
| **CL-163 트리뷰 + 디테일 + 검색** | ✅ ← 방금 |
| CL-164 Dirty + Undo/Redo | ⏳ |
| CL-165 JSON Import/Export | ⏳ |
| CL-166 데이터 카테고리 연결 | ⏳ |

**Epic U: 2/5**

---

## 다음 plan

| 옵션 | Ticket | 점수 | 비고 |
|---|---|---|---|
| **A** | CL-164 Dirty + Undo/Redo | 3점 | 본 CL 의 InspectorElement 위에 Dirty/Undo 추가 |
| B | CL-165 JSON Import/Export | 3점 | CL-164 의존 (Dirty 마커 후 import 충돌 방지) |
| C | CL-166 데이터 카테고리 연결 | 2점 | 본 CL 의 Provider 패턴 활용, 추가 Provider |

**추천: A (CL-164)** — Epic U 차례로. 본 CL 의 편집 흐름 위에 안전성 (Dirty/Undo) 추가가 자연스러움.

뭐로 갈까요?
