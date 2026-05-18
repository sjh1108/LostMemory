# CL-164 구현 계획서 (2026-05-05)

원본 plan: [`cl164_plan.md`](cl164_plan.md) (김회인 작성)
본 문서: 사용자와의 결정 합의 후 **실행 가능한 구현 계획**.

---

## Context

Epic U 의 **안전성 ticket**. CL-163 의 트리뷰 + 디테일 + 검색 위에 **변경 추적 / 명시적 저장 / Undo·Redo 통합** 추가. 디자이너가 안심하고 편집할 수 있도록.

3점 P1, CL-163 의존.

### 분담
- **본 CL-164**: Dirty 시각 (prefix + 빨간 글자) + SaveAll + Ctrl+S + 창 닫기 다이얼로그 + Undo·Redo UI 동기화
- CL-165: JSON Import/Export + Auto-save 토글
- CL-166: 추가 카테고리 (자동 적용)
- 별도 ticket: 지연 저장 모드 / Discard All / Dirty 시각 폴리시 (★ 아이콘 등)

---

## 1. 현황 (Phase 1)

### 1.1 CL-163 산출물 — 진입점 식별

[BalanceEditorWindow.cs](LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/BalanceEditorWindow.cs):
- `OnEnable() / OnDisable()` — 본 CL: Undo 구독/해제 + KeyDown 등록 추가 자리
- `CreateGUI()` — Save 버튼 wire-up + InspectorElement 변경 hook 추가 자리
- `PopulateLeftPanel(panel)` — `bindItem` 콜백에 dirty prefix + USS class toggle 추가
- `RebuildTree(filter)` — 변경 후 트리 갱신용으로 그대로 활용
- `ShowDetail(so)` — InspectorElement 생성 후 변경 이벤트 등록 자리
- `UpdateStatus(msg)` — SaveAll 결과 표시
- 신규 메서드: `UpdateTitle()`, `UpdateTreeLabels()`, `SaveAll()`, `OnUndoRedo()`, `OnInspectorChanged()`

[BalanceEditorWindow.uxml](LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/Resources/BalanceEditorWindow.uxml):
- 현재 toolbar: `[Title] [Search] [Spacer] [Refresh]`
- 본 CL 에서 추가: `[Save All]` ToolbarButton

[BalanceEditorWindow.uss](LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/Resources/BalanceEditorWindow.uss):
- 본 CL 에서 추가: `.be-tree-item-dirty { color: rgb(255, 100, 100); }` 스타일

### 1.2 CL-163 의 dirty 추적 가능성
- InspectorElement 가 SerializedObject 변경 시 자동 SetDirty (Unity 표준)
- `EditorUtility.IsDirty(so)` 로 조회 가능 — plan §1
- 별도 dirty 자료구조 불필요

---

## 2. 결정사항 (사용자 합의)

| # | 항목 | 결정 |
|---|---|---|
| 1 | 저장 모델 | **즉시 저장 + Dirty 시각** (Unity 표준) |
| 2 | Dirty 마커 시각 | **prefix `* ` + 빨간 글자** (둘 다) |
| 3 | 카테고리 dirty count | 표시: `Relics (77) [3 dirty]` |
| 4 | Ctrl+S 단축키 | 추가 (윈도우 포커스 시) |
| 5 | 창 닫기 다이얼로그 | Save Now / Later |
| 6 | Auto-save | CL-165 위임 |
| 7 | Discard All | 별도 ticket |
| 8 | 변경 감지 hook | InspectorElement 의 `SerializedPropertyChangeEvent` (대안: trickle-down `ChangeEvent<>`) |

---

## 3. 핵심 파일

### 3.1 신규 (1개)

| 경로 | 책임 |
|---|---|
| `Editor/BalanceEditor/DirtyTracker.cs` | Dirty 조회 헬퍼 — `EditorUtility.IsDirty` wrapper |

### 3.2 수정 (3개)

| 경로 | 변경 |
|---|---|
| `BalanceEditorWindow.cs` | SaveAll / Ctrl+S / Undo 구독 / OnDestroy 다이얼로그 / UpdateTitle / Tree dirty mark / InspectorElement 변경 hook |
| `Resources/BalanceEditorWindow.uxml` | toolbar 에 `<uie:ToolbarButton name="SaveButton">` 추가 |
| `Resources/BalanceEditorWindow.uss` | `.be-tree-item-dirty` (빨간 글자) 추가 |

---

## 4. 구현 단계

### 4.1 단계 1: DirtyTracker (15분)

```csharp
// Editor/BalanceEditor/DirtyTracker.cs
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LostMemory.Editor.BalanceEditor
{
    public static class DirtyTracker
    {
        public static bool IsDirty(ScriptableObject so)
            => so != null && EditorUtility.IsDirty(so);

        public static int CountDirty(IEnumerable<ScriptableObject> universe)
            => universe.Count(IsDirty);

        public static IEnumerable<ScriptableObject> GetAllDirty(
            IEnumerable<ScriptableObject> universe)
            => universe.Where(IsDirty);
    }
}
```

### 4.2 단계 2: TreeView dirty 마커 (30분)

`PopulateLeftPanel` 의 `bindItem` 수정:

```csharp
bindItem = (element, index) =>
{
    var node = _treeView.GetItemDataForIndex<TreeNode>(index);
    var label = (Label)element;
    string display = node?.DisplayName ?? string.Empty;

    bool isDirty = node?.So != null && DirtyTracker.IsDirty(node.So);
    if (isDirty)
    {
        label.text = "* " + display;
        label.AddToClassList("be-tree-item-dirty");
    }
    else
    {
        label.text = display;
        label.RemoveFromClassList("be-tree-item-dirty");
    }
};
```

카테고리 dirty count: `BuildTreeData` 의 카테고리 라벨 생성 시 dirty 수 추가:

```csharp
int dirtyInCategory = sos.Count(DirtyTracker.IsDirty);
string categoryLabel = dirtyInCategory > 0
    ? $"{provider.CategoryName} ({sos.Count}) [{dirtyInCategory} dirty]"
    : $"{provider.CategoryName} ({sos.Count})";
```

`RebuildTree` 시 또는 dirty 변화 감지 시 재계산.

### 4.3 단계 3: 윈도우 타이틀 갱신 (15분)

```csharp
private void UpdateTitle()
{
    int dirtyCount = CountAllDirty();
    string suffix = dirtyCount > 0 ? $" ({dirtyCount} unsaved)" : "";
    titleContent = new GUIContent("Balance Editor" + suffix);
}

private int CountAllDirty()
{
    if (_providers == null) return 0;
    int count = 0;
    foreach (var p in _providers)
    {
        count += p.LoadAll().Count(DirtyTracker.IsDirty);
    }
    return count;
}
```

호출 시점: 변경 감지 / Undo·Redo / SaveAll / Refresh.

### 4.4 단계 4: SaveAll + Ctrl+S + Save 버튼 (45분)

UXML toolbar 수정:
```xml
<uie:Toolbar class="be-toolbar">
    <ui:Label text="Balance Editor" class="be-title" />
    <uie:ToolbarSearchField name="SearchField" class="be-search" />
    <ui:VisualElement class="be-toolbar-spacer" />
    <uie:ToolbarButton name="RefreshButton" text="Refresh" class="be-refresh-btn" />
    <uie:ToolbarButton name="SaveButton" text="Save All" class="be-save-btn" />
</uie:Toolbar>
```

`CreateGUI()` 에 wire-up:
```csharp
var saveBtn = root.Q<Button>("SaveButton");
if (saveBtn != null) saveBtn.clicked += SaveAll;

root.RegisterCallback<KeyDownEvent>(OnKeyDown);
```

```csharp
private void OnKeyDown(KeyDownEvent e)
{
    if (e.ctrlKey && e.keyCode == KeyCode.S)
    {
        SaveAll();
        e.StopPropagation();
    }
}

private void SaveAll()
{
    AssetDatabase.SaveAssets();
    UpdateTitle();
    _treeView?.Rebuild();   // dirty 마커 갱신
    UpdateStatus($"Saved at {System.DateTime.Now:HH:mm:ss}");
}
```

### 4.5 단계 5: Undo·Redo 구독 (30분)

`OnEnable` / `OnDisable` 수정:
```csharp
private void OnEnable()
{
    _instance = this;
    Undo.undoRedoPerformed += OnUndoRedo;
}

private void OnDisable()
{
    Undo.undoRedoPerformed -= OnUndoRedo;
    if (_instance == this) _instance = null;
}

private void OnUndoRedo()
{
    UpdateTitle();
    _treeView?.Rebuild();
    UpdateStatus("Undo/Redo applied");
}
```

> Ctrl+Z / Ctrl+Y 는 Unity 가 처리. 본 CL 은 UI 동기화만.

### 4.6 단계 6: OnDestroy 다이얼로그 (15분)

```csharp
private void OnDestroy()
{
    // Editor 종료 중에는 다이얼로그 호출 금지 (Unity 멈춤 가능)
    if (EditorApplication.isQuittingOrExiting()) return;

    int dirtyCount = CountAllDirty();
    if (dirtyCount <= 0) return;

    bool save = EditorUtility.DisplayDialog(
        "Balance Editor",
        $"{dirtyCount} 개의 SO 가 dirty 상태입니다.\n" +
        "지금 저장하지 않으면 Unity 가 다음 SaveAssets 까지 메모리에 보관합니다.",
        "Save Now",
        "Later");
    if (save) AssetDatabase.SaveAssets();
}
```

> ⚠️ Unity 6 에 `EditorApplication.isQuittingOrExiting` 가 없으면 `EditorApplication.isPlayingOrWillChangePlaymode` 등으로 대체 또는 try-catch 가드.

### 4.7 단계 7: InspectorElement 변경 hook (15분)

`ShowDetail(so)` 수정:
```csharp
private void ShowDetail(ScriptableObject so)
{
    if (_detailContainer == null) return;
    _detailContainer.Clear();
    var inspector = new InspectorElement(so);
    inspector.RegisterCallback<SerializedPropertyChangeEvent>(_ => OnInspectorChanged());
    _detailContainer.Add(inspector);
}

private void OnInspectorChanged()
{
    UpdateTitle();
    _treeView?.Rebuild();
}
```

> 위험: `SerializedPropertyChangeEvent` 가 InspectorElement 위에서 발화 안 할 가능성. 대안:
> - `inspector.RegisterCallback<ChangeEvent<UnityEngine.Object>>(...)` (모든 ChangeEvent 캐치)
> - `EditorApplication.update` 콜백에서 dirty count 변화 폴링 (마지막 fallback)

### 4.8 단계 8: USS dirty 빨간 글자 (10분)

```css
.be-tree-item-dirty {
    color: rgb(255, 120, 120);
    -unity-font-style: bold;
}

.be-save-btn {
    flex-shrink: 0;
    flex-grow: 0;
    width: 80px;
    margin-left: 4px;
}
```

### 4.9 단계 9: 검증 (1시간) — §6 시나리오

---

## 5. 위험 / Gotchas

### 5.1 InspectorElement + SerializedPropertyChangeEvent 호환성 (신규 위험)

InspectorElement 는 자체 SerializedObject 관리. `SerializedPropertyChangeEvent` 가 정상 발화하는지 검증 필요.

**검증 방법**: 단계 7 구현 직후 임시 `Debug.Log` 로 발화 확인:
```csharp
inspector.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
{
    Debug.Log($"[CL-164] Inspector changed: {evt.changedProperty?.propertyPath}");
    OnInspectorChanged();
});
```
필드 변경 시 Console 에 로그 안 뜨면 → 대안 hook 사용.

**대안**:
- `inspector.RegisterCallback<ChangeEvent<UnityEngine.Object>>(...)` — generic ChangeEvent
- `EditorApplication.update += () => { if (dirtyChanged) UpdateTitle(); }` — 폴링 (60 FPS, 가벼움)

### 5.2 Unity 6 Undo 의 dirty flag 동작 (plan §위험 1)

Ctrl+Z 후 SO 가 dirty 유지? 아니면 dirty 해제? Unity 6 동작 검증 필요.

**검증 시나리오**: §6 시나리오 4 (Undo) 와 5 (Redo) 통과 시 동작 확인. 만약 Undo 시 dirty 해제 안 되면 사용자가 SaveAll 안 해도 디스크 저장 안 됨 (Unity 메모리만). 이는 Unity 표준 동작이므로 그대로 둬도 안전.

### 5.3 OnDestroy 다이얼로그가 Unity 종료 시 멈춤 (plan §위험 6)

`EditorUtility.DisplayDialog` 는 modal 이라 종료 중 호출하면 멈춤 가능.

**가드**: `EditorApplication.isQuittingOrExiting` 또는 try-catch + 무시. Unity 6 에 정확한 API 명 확인 필요. 없으면:
```csharp
private bool _isQuitting = false;
private void OnEnable() { EditorApplication.quitting += () => _isQuitting = true; ... }
private void OnDestroy() { if (_isQuitting) return; ... }
```

### 5.4 Ctrl+S 의 Unity 표준 SaveAll 충돌 (plan §위험 3)

본 윈도우 포커스 시 Ctrl+S → 본 CL SaveAll 호출. Unity 표준 SaveAssets 와 결과 동일이라 충돌 없음. `e.StopPropagation()` 으로 중복 호출 방지.

다른 윈도우 포커스 시 Ctrl+S → Unity 가 처리 (본 CL hook 미발화). 정상.

### 5.5 카테고리 dirty count 갱신 시점

`BuildTreeData` 가 매 RebuildTree 마다 호출되면 자동 갱신. 단 변경 직후 RebuildTree 호출 안 하고 `_treeView.Rebuild()` 만 부르면 카테고리 라벨 stale. 해결:
- `OnInspectorChanged` 에서 `RebuildTree(_lastFilter)` 호출 (filter 유지)
- 또는 카테고리 라벨도 `bindItem` 에서 재계산

본 plan: **`RebuildTree(_lastFilter)` 호출** (간단, 정확).

### 5.6 SaveAll 비용 (plan §위험 8)

77 + 16 SO 모두 dirty 시 SaveAssets ~수백 ms. Save 버튼/Ctrl+S 클릭 시 사용자 인지. 비동기화는 별도 ticket.

### 5.7 Refresh / Save 버튼 toolbar 정렬

CL-163 toolbar 핫픽스 (`<uie:Toolbar>`) 후 `<uie:ToolbarButton>` 추가는 표준 패턴. 깨짐 위험 낮음.

---

## 6. 검증 시나리오 (10개)

| # | 시나리오 | 통과 기준 |
|---|---|---|
| 1 | Dirty 마커 — 단일 | RelicData 선택 → `_size` 변경 → 트리에 `* RelicData_가죽 신발` (빨간색) + 타이틀 `Balance Editor (1 unsaved)` + 카테고리 `Relics (77) [1 dirty]` |
| 2 | Dirty 마커 — 다중 | 3개 SO 변경 → 타이틀 `(3 unsaved)` + 카테고리 `[3 dirty]` |
| 3 | SaveAll | Save All 버튼 클릭 또는 Ctrl+S → dirty 마커 모두 사라짐 + 타이틀 `Balance Editor` + statusbar `Saved at HH:mm:ss` |
| 4 | Undo | `_size` 1→2 변경 → Ctrl+Z → 1 으로 복원, dirty 상태 변화 관찰 (Unity 6 동작) |
| 5 | Redo | Undo 후 Ctrl+Y → 2 으로 복원, dirty 다시 표시 |
| 6 | 외부 변경 감지 | Project 윈도우에서 직접 SO 편집 → 본 윈도우의 AssetPostprocessor 트리거 → dirty 표시 |
| 7 | 창 닫기 — Save Now | 변경 후 X → 다이얼로그 → Save Now → SaveAssets + 닫힘 |
| 8 | 창 닫기 — Later | 다이얼로그 → Later → SaveAssets X + 닫힘 + 다음 오픈 시 dirty 그대로 |
| 9 | 창 닫기 — 변경 없음 | dirty 0 → 다이얼로그 X 즉시 닫힘 |
| 10 | 다중 SO Undo 영향 | A 변경 → B 변경 → Ctrl+Z 두 번 → 둘 다 원복, 트리 dirty 갱신 |

추가 (CL-163 환경 보강):
- toolbar 에 `[Title] [Search] [Spacer] [Refresh] [Save All]` 가로 한 줄 정상
- 검색 필터 적용 중에도 dirty 마커 갱신
- AssetPostprocessor 자동 새로고침과 Undo·Redo UI 갱신 충돌 없음

---

## 7. 후속 ticket 영향

| Ticket | CL-164 와의 관계 |
|---|---|
| **CL-165** (JSON Import/Export + Auto-save) | Auto-save 토글 ON 시 본 CL `SaveAll()` 주기 호출. Import 시 dirty 충돌 → 다이얼로그 활용 |
| **CL-166** (추가 카테고리) | WeaponData/SkillData/ShopConfig provider 추가 — 본 CL dirty 추적 자동 적용 |
| 별도: 지연 저장 모드 | InspectorElement 의 ApplyModifiedProperties 우회 + custom UXML |
| 별도: Discard All | 모든 dirty SO 의 메모리 변경 취소 |
| 별도: Dirty 시각 폴리시 | ★ 아이콘 / 카테고리 색상 구분 등 |

---

## 8. 예상 시간

| 단계 | 시간 |
|---|---|
| 1단계 (DirtyTracker) | 15분 |
| 2단계 (트리 dirty 마커 + 빨간 글자 클래스 toggle) | 35분 |
| 3단계 (UpdateTitle) | 15분 |
| 4단계 (Save 버튼 + Ctrl+S + UXML 수정) | 45분 |
| 5단계 (Undo 구독) | 30분 |
| 6단계 (OnDestroy 다이얼로그) | 20분 (Unity 6 종료 가드 검증 +5분) |
| 7단계 (InspectorElement 변경 hook + 호환성 검증) | 30분 (대안 fallback 시 +15분) |
| 8단계 (USS dirty 스타일) | 10분 |
| 9단계 (검증 10 시나리오 + 환경 보강) | 1시간 |
| **합계** | **약 4시간 20분** |

→ 3점 ticket. 원본 plan 대비 +30분 (빨간 글자 클래스 toggle + InspectorElement 호환성 검증 fallback 여유).

---

## 9. 작업 순서 (사용자 + Claude 분담)

### Claude 작성 (코드)
- 단계 1~8 의 .cs / .uxml / .uss 변경

### 사용자 작업 (Unity 에디터)
- Unity 컴파일 모니터링 (Console 빨간 에러)
- §6 검증 10 시나리오 + 환경 보강 실행
- 단계 7 의 InspectorElement hook 발화 여부 Debug.Log 확인 (필요 시 fallback 적용)
- commit / push / MR

### 환경 전환 시
- 핸드오프 양식 (`cl164_phase_1_handoff_<YYYYMMDD>.md`) 으로 누적 기록 (CL-162/163 패턴)

---

## 10. 결정사항 변경 시 영향

| 변경 시도 | 영향 |
|---|---|
| 즉시 → 지연 저장 | InspectorElement 우회 또는 custom UXML — 대규모 변경, 별도 ticket |
| Dirty 시각 단순화 (prefix 만) | USS class 추가 제거 + bindItem 1줄 변경 |
| Dirty 시각 ★ 아이콘 | bindItem 의 prefix 문자 변경 + USS unicode 폰트 처리 |
| Auto-save 추가 | CL-165 (별도 ticket) |
| Discard All 버튼 | toolbar 1개 + 메서드 (모든 dirty SO 의 `Undo.RegisterCompleteObjectUndo` + 메모리 reset) — 별도 ticket |
| Ctrl+S 제거 | KeyDown 콜백 미등록 — Save 버튼만 사용 |
