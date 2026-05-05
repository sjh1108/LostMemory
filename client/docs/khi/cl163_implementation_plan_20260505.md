# CL-163 구현 계획서 (2026-05-05)

원본 plan: [`cl163_plan.md`](cl163_plan.md) (김회인 작성)
본 문서: 사용자와의 결정 합의 후 **실행 가능한 구현 계획**.

---

## Context

Epic U (밸런스 에디터) 의 **본체 ticket**. CL-162 의 빈 EditorWindow 셸을 **실제 데이터 표시 + 편집 + 검색** 으로 채움. 5점 P1, CL-162 의존.

### 분담
- **본 CL-163**: TreeView 인프라 + 2개 카테고리 (Relics + BuildSets) + 디테일 (InspectorElement) + 검색 + 자동 새로고침
- CL-164: Dirty + Undo
- CL-165: JSON Import/Export
- CL-166: 추가 카테고리 (Weapons / Skills / ShopConfig) — Provider 추가만
- CL-103 (포트폴리오 단계): 디자이너 친화 Custom UXML per SO

---

## 1. 코드베이스 현황 (Phase 1 탐색 결과)

### 1.1 CL-162 산출물 (이미 존재)
- [`BalanceEditorWindow.cs`](LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/BalanceEditorWindow.cs) — namespace `LostMemory.Editor.BalanceEditor`
  - `protected virtual void PopulateLeftPanel(VisualElement panel)` ✓
  - `protected virtual void PopulateRightPanel(VisualElement panel)` ✓
  - `CreateGUI` 에서 hook 호출 중
- [`Resources/BalanceEditorWindow.uxml`](LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/Resources/BalanceEditorWindow.uxml) — `LeftPanel` / `RightPanel` / `StatusLabel` named element
- [`Resources/BalanceEditorWindow.uss`](LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/Resources/BalanceEditorWindow.uss) — `.be-toolbar` 안에 `<!-- CL-163: 검색 박스 등 추가 예정 -->` 마커
- [`LostMemory.BalanceEditor.Editor.asmdef`](LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/LostMemory.BalanceEditor.Editor.asmdef) — `includePlatforms: ["Editor"]`, `references: []`, `autoReferenced: true`

### 1.2 데이터 SO 현황
| 카테고리 | 타입 | 위치 | 수 |
|---|---|---|---|
| Relics (본 CL 대상) | `LostMemory.Relics.RelicData` | `Assets/_Project/ScriptableObjects/Relics/Generated/` | **77** |
| Relics (제외) | 동일 | `Assets/_Project/ScriptableObjects/Relics/` (root, Manual) | 18 (트리 X) |
| BuildSets (본 CL 대상) | `LostMemory.Relics.BuildSetData` | `Assets/_Project/ScriptableObjects/BuildSets/` | **16** |

### 1.3 Unity / 컴포넌트 가용성
- Unity **6000.3.13f1** — `TreeView`, `TreeViewItemData<T>`, `InspectorElement`, `TwoPaneSplitView`, `ToolbarSearchField` 모두 정식 지원
- 프로젝트 내 첫 도입 (기존 Editor 코드 IMGUI/SerializedObject 만)

### 1.4 RelicData 핵심 필드 (CL-138 완료)
- 듀얼 태그: `_tagPrimary`, `_tagSecondary` (RelicTag 17개)
- 사이즈: `_size` (Vector2Int)
- 다중 효과: `_effects` (`EffectEntry[]`)
- 레거시: `_effectTypeLegacy`, `_magnitudeLegacy`, `_durationLegacy`, `_thresholdLegacy` ([Obsolete] / FormerlySerializedAs)

→ **InspectorElement 가 모든 필드 자동 표시**. 레거시 필드도 노출됨 (CL-103 폴리시에서 정렬/숨김 처리).

---

## 2. 결정사항 (사용자 합의)

| # | 항목 | 결정 |
|---|---|---|
| 1 | 트리뷰 컴포넌트 | UI Toolkit `TreeView` |
| 2 | 디테일 패널 | `InspectorElement` (자동) — 디자이너 친화 UXML 은 CL-103 위임 |
| 3 | Splitter | `TwoPaneSplitView` |
| 4 | 검색 | `ToolbarSearchField` + 매치 시 자동 펼침 |
| 5 | 카테고리 수 | **2개**: Relics + BuildSets |
| 6 | Relics 범위 | **Generated 77개만** (Manual 18 제외) |
| 7 | 자동 새로고침 | `AssetPostprocessor` + toolbar Refresh 버튼 (둘 다) |
| 8 | Splitter 너비 영속성 | 별도 ticket |
| 9 | 트리 펼침 영속성 | 별도 ticket |
| 10 | hook 활용 | `PopulateLeftPanel/RightPanel` override |

---

## 3. 핵심 파일

### 3.1 신규 (5개)

| 경로 | 책임 |
|---|---|
| `Editor/BalanceEditor/IBalanceCategoryProvider.cs` | 카테고리 인터페이스 (CL-166 이 구현 추가) |
| `Editor/BalanceEditor/Providers/RelicCategoryProvider.cs` | RelicData 로드 (Generated 폴더만) |
| `Editor/BalanceEditor/Providers/BuildSetCategoryProvider.cs` | BuildSetData 로드 |
| `Editor/BalanceEditor/TreeNode.cs` | 트리 노드 데이터 구조 (DisplayName / SO / CategoryName) |
| `Editor/BalanceEditor/BalanceEditorAssetWatcher.cs` | `AssetPostprocessor` — SO 변경 감지 자동 새로고침 |

### 3.2 수정 (3개, CL-162 산출물)

| 경로 | 변경 |
|---|---|
| `BalanceEditorWindow.cs` | TreeView/SearchField/TwoPaneSplitView 설치, `PopulateLeftPanel/RightPanel` override, `IsOpen`/`RefreshTree` static API, `ShowDetail`/`UpdateStatus` 메서드 |
| `BalanceEditorWindow.uxml` | toolbar 에 `ToolbarSearchField` + `Button(Refresh)` 추가, `be-splitter` element 제거 (TwoPaneSplitView 가 코드로 대체) |
| `BalanceEditorWindow.uss` | TreeView/SearchField/Refresh 버튼 스타일 추가 |

---

## 4. 구현 단계

### 4.1 단계 1: `IBalanceCategoryProvider` 인터페이스 + 2개 구현 (45분)

```csharp
// Editor/BalanceEditor/IBalanceCategoryProvider.cs
namespace LostMemory.Editor.BalanceEditor
{
    public interface IBalanceCategoryProvider
    {
        string CategoryName { get; }
        Type SoType { get; }
        IEnumerable<ScriptableObject> LoadAll();
    }
}
```

```csharp
// Editor/BalanceEditor/Providers/RelicCategoryProvider.cs
namespace LostMemory.Editor.BalanceEditor.Providers
{
    public class RelicCategoryProvider : IBalanceCategoryProvider
    {
        // Generated 폴더만 (Manual 18 제외 — 합의 §2.6)
        private static readonly string[] SearchFolders =
            { "Assets/_Project/ScriptableObjects/Relics/Generated" };

        public string CategoryName => "Relics";
        public Type SoType => typeof(RelicData);

        public IEnumerable<ScriptableObject> LoadAll()
        {
            var guids = AssetDatabase.FindAssets("t:RelicData", SearchFolders);
            return guids
                .Select(g => AssetDatabase.LoadAssetAtPath<RelicData>(
                    AssetDatabase.GUIDToAssetPath(g)))
                .Where(r => r != null);
        }
    }
}
```

```csharp
// Editor/BalanceEditor/Providers/BuildSetCategoryProvider.cs
namespace LostMemory.Editor.BalanceEditor.Providers
{
    public class BuildSetCategoryProvider : IBalanceCategoryProvider
    {
        private static readonly string[] SearchFolders =
            { "Assets/_Project/ScriptableObjects/BuildSets" };

        public string CategoryName => "BuildSets";
        public Type SoType => typeof(BuildSetData);

        public IEnumerable<ScriptableObject> LoadAll()
        {
            var guids = AssetDatabase.FindAssets("t:BuildSetData", SearchFolders);
            return guids
                .Select(g => AssetDatabase.LoadAssetAtPath<BuildSetData>(
                    AssetDatabase.GUIDToAssetPath(g)))
                .Where(b => b != null);
        }
    }
}
```

> ⚠️ asmdef references 비어있음 — Auto Referenced ON 으로 Assembly-CSharp (RelicData, BuildSetData) 자동 참조. 컴파일 실패 시 asmdef 에 references 추가 필요.

### 4.2 단계 2: `TreeNode` 데이터 + 트리 빌드 (30분)

```csharp
// Editor/BalanceEditor/TreeNode.cs
namespace LostMemory.Editor.BalanceEditor
{
    public class TreeNode
    {
        public string DisplayName;
        public ScriptableObject So;          // null = 카테고리 노드
        public string CategoryName;
    }
}
```

`BalanceEditorWindow.BuildTreeData()` 메서드:

```csharp
private List<TreeViewItemData<TreeNode>> BuildTreeData()
{
    var roots = new List<TreeViewItemData<TreeNode>>();
    int id = 0;
    foreach (var provider in _providers)
    {
        var leaves = new List<TreeViewItemData<TreeNode>>();
        var sos = provider.LoadAll().OrderBy(so => so.name).ToList();
        foreach (var so in sos)
        {
            var leaf = new TreeNode {
                DisplayName = so.name, So = so,
                CategoryName = provider.CategoryName };
            leaves.Add(new TreeViewItemData<TreeNode>(id++, leaf));
        }
        var category = new TreeNode {
            DisplayName = $"{provider.CategoryName} ({sos.Count})",
            CategoryName = provider.CategoryName };
        roots.Add(new TreeViewItemData<TreeNode>(id++, category, leaves));
    }
    return roots;
}
```

### 4.3 단계 3: `BalanceEditorWindow` 통합 (1시간)

```csharp
public class BalanceEditorWindow : EditorWindow
{
    public static bool IsOpen { get; private set; }
    private static BalanceEditorWindow _instance;
    public static void RefreshTree()
    {
        if (_instance != null) _instance.RebuildTree();
    }

    private List<IBalanceCategoryProvider> _providers;
    private TreeView _treeView;
    private VisualElement _detailContainer;
    private Label _statusLabel;
    private ToolbarSearchField _searchField;

    private void OnEnable() { _instance = this; IsOpen = true; }
    private void OnDisable() { IsOpen = false; if (_instance == this) _instance = null; }

    public void CreateGUI()
    {
        // (CL-162 코드 유지) UXML/USS 로드
        // ...

        _providers = new List<IBalanceCategoryProvider> {
            new RelicCategoryProvider(),
            new BuildSetCategoryProvider(),
        };

        // be-splitter element 는 UXML 에서 제거됨, body 에 TwoPaneSplitView 동적 삽입
        var body = root.Q<VisualElement>(className: "be-body");
        var leftPanel = root.Q<VisualElement>("LeftPanel");
        var rightPanel = root.Q<VisualElement>("RightPanel");
        body.Clear();
        var split = new TwoPaneSplitView(0, 250, TwoPaneSplitViewOrientation.Horizontal);
        split.Add(leftPanel);
        split.Add(rightPanel);
        body.Add(split);

        // toolbar SearchField + Refresh 버튼은 UXML 에서 미리 자리 잡음, 코드에서 wire-up
        _searchField = root.Q<ToolbarSearchField>("SearchField");
        _searchField.RegisterValueChangedCallback(evt => RebuildTree(evt.newValue));
        var refreshBtn = root.Q<Button>("RefreshButton");
        refreshBtn.clicked += () => RebuildTree(_searchField.value);

        _statusLabel = root.Q<Label>("StatusLabel");

        PopulateLeftPanel(leftPanel);
        PopulateRightPanel(rightPanel);
        RebuildTree();
    }

    protected override void PopulateLeftPanel(VisualElement panel)
    {
        panel.Clear();
        _treeView = new TreeView {
            makeItem = () => new Label(),
            bindItem = (el, idx) => {
                var node = _treeView.GetItemDataForIndex<TreeNode>(idx);
                ((Label)el).text = node.DisplayName;
            }
        };
        _treeView.selectionChanged += OnTreeSelectionChanged;
        panel.Add(_treeView);
    }

    protected override void PopulateRightPanel(VisualElement panel)
    {
        panel.Clear();
        _detailContainer = new VisualElement();
        panel.Add(_detailContainer);
    }

    private void RebuildTree(string filter = null)
    {
        var data = BuildTreeData();
        if (!string.IsNullOrEmpty(filter)) data = FilterTree(data, filter);
        _treeView.SetRootItems(data);
        _treeView.Rebuild();
        if (!string.IsNullOrEmpty(filter)) _treeView.ExpandAll();
        UpdateStatus($"Loaded {data.Count} categories, {CountLeaves(data)} items");
    }

    private void OnTreeSelectionChanged(IEnumerable<object> selected)
    {
        var first = selected.FirstOrDefault();
        if (first is not TreeNode node || node.So == null) return;
        ShowDetail(node.So);
        EditorGUIUtility.PingObject(node.So);
        UpdateStatus($"Selected: {node.So.name}");
    }

    private void ShowDetail(ScriptableObject so)
    {
        _detailContainer.Clear();
        _detailContainer.Add(new InspectorElement(so));
    }

    private void UpdateStatus(string msg) { if (_statusLabel != null) _statusLabel.text = msg; }
}
```

> 메모: TreeView 의 `bindItem` 에서 `GetItemDataForIndex<TreeNode>` 를 사용. Unity 6 에서 `TreeViewItemData<T>` 에 직접 데이터 저장.

### 4.4 단계 4: 검색 박스 + 필터링 (45분)

```csharp
private List<TreeViewItemData<TreeNode>> FilterTree(
    List<TreeViewItemData<TreeNode>> data, string filter)
{
    var lower = filter.ToLowerInvariant();
    int newId = 10000;  // 충돌 방지
    var result = new List<TreeViewItemData<TreeNode>>();
    foreach (var category in data)
    {
        var matchedLeaves = category.children
            .Where(c => c.data.So != null &&
                        c.data.DisplayName.ToLowerInvariant().Contains(lower))
            .ToList();
        if (matchedLeaves.Count == 0) continue;
        var newCat = new TreeNode {
            DisplayName = $"{category.data.CategoryName} ({matchedLeaves.Count})",
            CategoryName = category.data.CategoryName };
        result.Add(new TreeViewItemData<TreeNode>(
            newId++, newCat, matchedLeaves));
    }
    return result;
}
```

UXML toolbar 수정:
```xml
<ui:VisualElement class="be-toolbar">
    <ui:Label text="Balance Editor" class="be-title" />
    <uie:ToolbarSearchField name="SearchField" class="be-search" />
    <ui:Button name="RefreshButton" text="Refresh" class="be-refresh-btn" />
</ui:VisualElement>
```

> `xmlns:uie="UnityEditor.UIElements"` 가 이미 UXML 에 있음 (CL-162) — 추가 import 불필요.

### 4.5 단계 5: TwoPaneSplitView 통합 (30분)

UXML 에서 `<ui:VisualElement class="be-splitter" />` 제거 → 단계 3 코드에서 동적 `TwoPaneSplitView` 삽입.

### 4.6 단계 6: AssetPostprocessor 자동 새로고침 (30분)

```csharp
// Editor/BalanceEditor/BalanceEditorAssetWatcher.cs
namespace LostMemory.Editor.BalanceEditor
{
    public class BalanceEditorAssetWatcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] imported, string[] deleted, string[] moved, string[] movedFromAssetPaths)
        {
            if (!BalanceEditorWindow.IsOpen) return;
            // RelicData / BuildSetData 변경만 트리거
            bool relevant = imported.Concat(deleted).Concat(moved)
                .Any(p => p.EndsWith(".asset") &&
                          (p.Contains("/Relics/Generated/") ||
                           p.Contains("/BuildSets/")));
            if (relevant) BalanceEditorWindow.RefreshTree();
        }
    }
}
```

### 4.7 단계 7: USS 추가 (15분)

```css
.be-search {
    flex-grow: 1;
    margin-left: 8px;
    margin-right: 8px;
    max-width: 300px;
}
.be-refresh-btn {
    height: 22px;
    margin-right: 4px;
}
.be-detail-empty {
    color: rgb(150, 150, 150);
    -unity-font-style: italic;
    margin-top: 16px;
}
```

### 4.8 단계 8: 검증 (1시간 30분) — §6 시나리오

---

## 5. 위험 / Gotchas

### 5.1 asmdef references 부재 — Auto Referenced 의존
- 본 CL 코드가 `RelicData`, `BuildSetData` 참조. asmdef references 비었으니 Auto Referenced ON 이 작동해야 컴파일됨.
- **검증**: 단계 1 후 즉시 Unity 컴파일 확인. 빨간 에러 (`The type or namespace 'RelicData' could not be found`) 발생 시 asmdef 에 references 추가:
  ```json
  "references": ["GUID:<Assembly-CSharp>"]
  ```
  단 Assembly-CSharp 은 GUID 참조 불가 — 이 경우 별도 Runtime asmdef 신설 검토 (별도 ticket).

### 5.2 Manual 18 제외 — Provider 스코프 한정
- `RelicCategoryProvider.SearchFolders` 가 `Generated/` 폴더만 명시 → Manual 18 트리 미포함
- 향후 전체 사용으로 전환 시 `SearchFolders` 를 `["Assets/_Project/ScriptableObjects/Relics"]` 로 변경 (Generated 포함됨)

### 5.3 InspectorElement 가 레거시 필드도 표시
- RelicData 의 `_effectTypeLegacy`, `_magnitudeLegacy`, `_durationLegacy`, `_thresholdLegacy` 자동 노출
- 디자이너에게 혼란스러울 수 있음 → CL-103 (디자이너 친화 폴리시) 또는 Custom UXML 별도 ticket 에서 `[HideInInspector]` 정리 또는 UXML 으로 그룹화

### 5.4 검색 시 트리 펼침 손실 → 자동 펼침
- `SetRootItems` 후 모두 접힘 → 매치 결과 숨겨짐
- 해결: `if (filter) _treeView.ExpandAll()` (단계 3 코드에 포함)

### 5.5 SerializedObject 변경 즉시 disk 저장 X
- InspectorElement 가 SetDirty 만 호출, disk 저장은 `Ctrl+S` 또는 `AssetDatabase.SaveAssets()`
- 본 CL 은 Unity 표준 그대로 — Dirty 마커 + 명시적 Save 는 **CL-164**

### 5.6 AssetPostprocessor 정적 — 윈도우 닫혀도 호출
- `BalanceEditorWindow.IsOpen` 가드 (단계 6 코드에 포함)
- 추가로 폴더 한정 (`/Relics/Generated/` or `/BuildSets/`) 으로 노이즈 제거

### 5.7 TreeView 가상화 — 77 + 16 SO 성능
- Unity 6 TreeView 가상화 자체. 100 미만은 즉시. 성능 이슈 무.

### 5.8 SO 이름 변경 시 트리 갱신
- `OnPostprocessAllAssets` 의 `moved` 항목 감지. 자동 처리.

---

## 6. 검증 시나리오 (10개)

| # | 시나리오 | 통과 기준 |
|---|---|---|
| 1 | 트리 표시 | 윈도우 열기 → 좌측 트리에 `Relics (77)` / `BuildSets (16)` 표시 |
| 2 | 카테고리 펼치기 | `Relics (77)` 펼치기 → 자식 SO 77개 (Generated 폴더 것들만, Manual 18 미포함) |
| 3 | SO 선택 → 디테일 | 첫 RelicData 클릭 → 우측에 InspectorElement, `_displayName/_tagPrimary/_tagSecondary/_size/_effects` 등 모든 필드 표시 |
| 4 | 디테일 편집 | `_size` 변경 → asset 즉시 업데이트 (Project 저장은 Ctrl+S 까지 disk 반영 X — CL-164 에서 해결) |
| 5 | Project 윈도우 highlight | 선택 시 Project 창에서 해당 .asset 파일 ping (PingObject) |
| 6 | 검색 — 매치 | 검색 박스에 `Fire` 입력 → 매치되는 RelicData 만 + 매치된 카테고리 자동 펼침 |
| 7 | 검색 — 빈 입력 | 빈 문자열 → 전체 트리 복원 |
| 8 | 검색 — 매치 0 | `xyz` 입력 → 빈 트리 |
| 9 | 자동 새로고침 | Project 창에서 Generated/ 폴더에 RelicData 1개 추가 → 트리 자동 갱신 (`Relics (78)`) |
| 10 | 수동 Refresh 버튼 | toolbar Refresh 클릭 → 트리 재빌드 |
| 11 | Splitter 드래그 | 좌우 경계 드래그 → 너비 조절 가능 |
| 12 | 상태 표시 | 선택 시 status bar `Selected: <name>`, Refresh 후 `Loaded 2 categories, 93 items` |
| 13 | 윈도우 닫기/재오픈 | 닫고 다시 열어도 정상 작동, 도메인 리로드 후도 OK (`IsOpen` 정확) |

---

## 7. 후속 ticket 영향

| Ticket | CL-163 과의 관계 |
|---|---|
| **CL-164** (Dirty + Undo) | 본 CL 의 InspectorElement 위에 `EditorUtility.IsDirty` 시각 + Undo 통합 |
| **CL-165** (JSON Import/Export) | toolbar 에 Export/Import 버튼 추가, Auto-save 토글 |
| **CL-166** (추가 카테고리) | `IBalanceCategoryProvider` 구현 3개 (Weapon/Skill/Shop) 추가 → 자동 트리 등장 |
| **CL-103** (디자이너 친화 폴리시) | InspectorElement → Custom UXML per SO 타입 (`_effectTypeLegacy` 등 숨김, 그룹화) |
| **별도: Splitter 너비 영속성** | `EditorPrefs` 저장/복원 |
| **별도: 트리 펼침 영속성** | 다음 세션 복원 |

---

## 8. 예상 시간

| 단계 | 시간 |
|---|---|
| 1단계 (Provider 인터페이스 + 2개) | 45분 |
| 2단계 (TreeNode + 빌드) | 30분 |
| 3단계 (TreeView 통합) | 1시간 |
| 4단계 (검색) | 45분 |
| 5단계 (TwoPaneSplitView) | 30분 |
| 6단계 (AssetPostprocessor + Refresh 버튼) | 30분 |
| 7단계 (USS 추가) | 15분 |
| 8단계 (검증 13 시나리오) | 1시간 30분 |
| **합계** | **약 5시간 45분** |

→ 5점 ticket 부합.

---

## 9. 작업 순서 (사용자 + Claude 분담)

### Claude 작성 (코드)
- 단계 1~7 의 .cs / .uxml / .uss 변경 모두

### 사용자 작업 (Unity 에디터)
- Unity 컴파일 모니터링 (Console 빨간 에러)
- §6 검증 13 시나리오 실행
- (필요 시) asmdef references 조정 (§5.1 fallback)
- commit / push / MR

### 환경 전환 시
- 핸드오프 양식 (`cl163_phase_1_handoff_<YYYYMMDD>.md`) 으로 누적 기록 (CL-162 패턴 따름)

---

## 10. 결정사항 변경 시 영향

| 변경 시도 | 영향 |
|---|---|
| Manual 18 다시 포함 | `RelicCategoryProvider.SearchFolders` 1줄 변경 |
| BuildSets 제외 | `_providers` 리스트에서 1줄 제거 |
| InspectorElement → Custom UXML | `ShowDetail` 메서드 + 타입별 UXML 추가 (별도 ticket) |
| 카테고리 추가 (Weapons 등) | 신규 Provider 클래스 + `_providers` 등록 (CL-166) |
