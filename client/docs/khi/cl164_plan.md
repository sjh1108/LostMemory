# CL-164 — Dirty 마커 + 명시적 저장 + Undo/Redo

## Context

Epic U 의 안전성 ticket. **CL-163 의 편집 흐름 위에 변경 추적 + 명시적 저장 + Undo 통합**.

3점 P1, CL-163 의존.

### 본 CL 책임 범위

1. **Dirty 마커** — 변경된 SO 시각 표시 (트리뷰 노드 / 윈도우 타이틀)
2. **명시적 저장** — Ctrl+S / Save 버튼 → `AssetDatabase.SaveAssets()`
3. **Undo/Redo 통합** — Ctrl+Z / Ctrl+Y, `Undo.RecordObject` + `Undo.undoRedoPerformed` 구독
4. **창 닫기 시 변경 확인 다이얼로그** — "저장 안 한 변경이 있습니다"

### 기존 상태 (CL-162/163)

- ✅ `BalanceEditorWindow` + InspectorElement (CL-163) — SerializedObject 자동 편집 + Unity 기본 즉시 저장 동작
- ❌ Dirty 추적 — **본 CL 신설**
- ❌ 명시적 저장 (지연 저장) — **본 CL 신설**
- ⚠️ Undo — Unity InspectorElement 가 일부 자동 처리. 본 CL 에서 보장 + UI 갱신.

### 디자인 결정 — 즉시 저장 vs 지연 저장

Unity 표준은 **즉시 저장** (Inspector 변경 = AssetDatabase.SetDirty + 다음 Ctrl+S 또는 자동 저장 시 disk).

**옵션**:
- (a) Unity 표준 즉시 저장 유지, Save 버튼은 단순 SaveAssets
- (b) **지연 저장 (Defer)** — 변경은 메모리만, Save 버튼 클릭 시 disk
- (c) 토글 (디자이너 선택)

**채택: (a) + Dirty 시각만**.
- Unity InspectorElement 의 즉시 SetDirty 동작 유지 (안전, Unity 표준)
- Save 버튼은 `AssetDatabase.SaveAssets()` 트리거 (`Project Save` 와 동일)
- Dirty 마커는 "SetDirty 후 SaveAssets 안 한 상태" 표시
- (b) 지연 저장은 SerializedObject 의 ApplyModifiedProperties 안 부르면 가능하지만 Unity 표준과 충돌. 별도 ticket.

→ **본 CL: Dirty 시각 + 명시적 SaveAssets + Undo 보장**.

---

## 결정사항

### 1. Dirty 추적 — `EditorUtility.IsDirty(so)` 활용

Unity 가 SO 변경 시 자동으로 dirty flag 설정 (`SetDirty`). `EditorUtility.IsDirty(so)` 로 조회.

```csharp
public static class DirtyTracker
{
    public static IEnumerable<ScriptableObject> GetAllDirty(IEnumerable<ScriptableObject> universe)
        => universe.Where(so => so != null && EditorUtility.IsDirty(so));

    public static int CountDirty(IEnumerable<ScriptableObject> universe)
        => universe.Count(so => so != null && EditorUtility.IsDirty(so));
}
```

→ 별도 추적 자료구조 불필요. Unity 의 dirty flag 재활용.

### 2. 트리뷰 Dirty 마커 — 노드 라벨 prefix `*`

```csharp
treeView.bindItem = (element, index) => {
    var node = treeView.GetItemDataForIndex<TreeNode>(index);
    string prefix = (node.So != null && EditorUtility.IsDirty(node.So)) ? "* " : "";
    ((Label)element).text = prefix + node.DisplayName;
};
```

→ "* 전사의 끈" 처럼 변경된 SO 앞에 별표.

→ 카테고리 노드의 라벨에도 dirty count 추가 가능: `Relics (75) [3 dirty]`

### 3. 윈도우 타이틀 Dirty 마커

```csharp
private void UpdateTitle()
{
    int dirtyCount = DirtyTracker.CountDirty(GetAllSoInTree());
    string suffix = (dirtyCount > 0) ? $" ({dirtyCount} unsaved)" : "";
    titleContent = new GUIContent("Balance Editor" + suffix);
}
```

→ "Balance Editor (3 unsaved)" 형식.

### 4. 명시적 저장 — Save 버튼 + Ctrl+S 단축키

**Save 버튼 (toolbar)**:
```csharp
var saveBtn = new ToolbarButton(() => SaveAll()) { text = "Save All" };
toolbar.Add(saveBtn);

private void SaveAll()
{
    AssetDatabase.SaveAssets();
    UpdateTitle();
    UpdateTreeLabels();
    UpdateStatus($"Saved at {DateTime.Now:HH:mm:ss}");
}
```

**Ctrl+S 단축키**:
```csharp
private void OnEnable()
{
    rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown);
}

private void OnKeyDown(KeyDownEvent e)
{
    if (e.ctrlKey && e.keyCode == KeyCode.S)
    {
        SaveAll();
        e.StopPropagation();
    }
}
```

→ Unity 의 Project Save (`Ctrl+S`) 와 일관.

### 5. Undo/Redo 통합

Unity InspectorElement 는 SerializedProperty 변경 시 자동으로 `Undo.RegisterCompleteObjectUndo` 호출 → **Undo/Redo 가 자동 작동**.

본 CL 추가 작업:
- `Undo.undoRedoPerformed` 구독 → 트리/디테일 UI 갱신
- 외부 Undo 시 (예: Project 윈도우에서 변경) 본 윈도우도 갱신

```csharp
private void OnEnable()
{
    Undo.undoRedoPerformed += OnUndoRedo;
}

private void OnDisable()
{
    Undo.undoRedoPerformed -= OnUndoRedo;
}

private void OnUndoRedo()
{
    treeView.RefreshItems();   // dirty 마커 갱신
    UpdateTitle();
    UpdateStatus("Undo/Redo applied");
}
```

→ Ctrl+Z / Ctrl+Y 는 Unity 가 처리. 본 CL 은 UI 동기화만.

### 6. 창 닫기 시 변경 확인 다이얼로그

```csharp
private void OnDestroy()
{
    int dirtyCount = DirtyTracker.CountDirty(GetAllSoInTree());
    if (dirtyCount > 0)
    {
        bool save = EditorUtility.DisplayDialog(
            "Balance Editor",
            $"{dirtyCount} 개의 저장 안 한 변경이 있습니다. 저장하시겠습니까?",
            "Save",
            "Discard");
        if (save) AssetDatabase.SaveAssets();
    }
}
```

→ 사용자가 윈도우 X 클릭 시 확인.

⚠️ **Discard 의 의미 모호**: Unity 표준 즉시 저장이라 "Discard" 가 메모리만 변경 취소? Disk 는 SaveAssets 안 호출하면 그대로. → 다이얼로그 메시지 명확화.

**대안 메시지**:
```
"{N} 개의 SO 가 dirty 상태입니다. 저장하지 않으면 Unity 가 다음 SaveAssets 까지 메모리에 보관합니다. 저장하시겠습니까?"
- "Save Now" / "Later"
```

→ 본 plan: **명확한 "Save Now / Later" 메시지**.

### 7. 자동 저장 — 본 CL 미포함

**Auto-save 토글** 은 회의록 명시 → **CL-165 와 묶음** (JSON Import/Export 와 같이).

본 CL: **수동 저장 + Dirty 시각**만.

### 8. Dirty 갱신 트리거

언제 트리/타이틀 갱신?
- 디테일 패널 편집 직후 (InspectorElement 의 변경 이벤트)
- Undo/Redo 후
- SaveAll 후
- AssetPostprocessor 에서 (외부 변경 감지)

```csharp
inspector.RegisterCallback<SerializedPropertyChangeEvent>(_ => {
    treeView.RefreshItems();
    UpdateTitle();
});
```

→ 매 변경마다 갱신 (퍼포먼스 OK, 트리뷰는 가상화).

### 9. SerializedObject vs Direct Edit

InspectorElement 가 자동으로 SerializedObject 사용 + ApplyModifiedProperties + Undo 통합.

본 CL 은 추가 작업 없음 (Unity 표준 흐름 유지).

→ Custom UXML per SO (별도 ticket) 도입 시 직접 SerializedObject.Update / ApplyModifiedProperties 책임.

### 10. 일괄 Discard 옵션 — 본 CL 미포함

"모든 변경 취소 (Discard All)" 버튼은 위험 → **별도 ticket**.

본 CL: SaveAll 만.

---

## 핵심 파일

### 신규

| 경로 | 내용 |
|---|---|
| `Editor/BalanceEditor/DirtyTracker.cs` | Dirty 조회 헬퍼 |

### 수정

| 경로 | 변경 |
|---|---|
| `BalanceEditorWindow.cs` (CL-162/163) | SaveAll / Ctrl+S 단축키 / Undo 구독 / OnDestroy 다이얼로그 / UpdateTitle / UpdateTreeLabels |
| `BalanceEditorWindow.uxml` (CL-162) | toolbar 에 Save 버튼 추가 |
| `BalanceEditorWindow.uss` (CL-162) | dirty 라벨 스타일 (선택, 빨간 색 등) |

---

## 구현 단계

### 1단계: DirtyTracker (15분)

§1 정적 클래스. 두 메서드.

### 2단계: 트리뷰 dirty 마커 (30분)

1. bindItem 수정 (§2)
2. RefreshItems 호출 시점 (§8)
3. 카테고리 노드의 dirty count 표시 (선택)

### 3단계: 윈도우 타이틀 갱신 (15분)

§3 UpdateTitle 메서드. SaveAll / 변경 / Undo 후 호출.

### 4단계: Save 버튼 + Ctrl+S (45분)

1. UXML toolbar 에 ToolbarButton 추가
2. SaveAll 메서드 (§4)
3. KeyDownEvent 등록 + Ctrl+S 처리
4. status bar 에 "Saved at HH:mm:ss" 표시

### 5단계: Undo/Redo 구독 (30분)

§5 코드:
- OnEnable / OnDisable 에서 Undo.undoRedoPerformed +=/−=
- OnUndoRedo 콜백 (트리 + 타이틀 갱신)

검증: Ctrl+Z / Ctrl+Y 가 작동, 트리 dirty 마커 즉시 갱신.

### 6단계: 창 닫기 다이얼로그 (15분)

§6 코드. OnDestroy 에서 EditorUtility.DisplayDialog.

### 7단계: 변경 이벤트 hook (15분)

InspectorElement 의 SerializedPropertyChangeEvent 구독 (§8). 매 변경마다 트리/타이틀 갱신.

### 8단계: 검증 (1시간)

```
시나리오 1: Dirty 마커
- "전사의 끈" 선택 → _baseDamage 값 변경
- 트리에 "* 전사의 끈" 표시
- 윈도우 타이틀 "Balance Editor (1 unsaved)"
- 카테고리 노드 "Relics (75) [1 dirty]"

시나리오 2: 다중 dirty
- 3개 SO 변경 → "Balance Editor (3 unsaved)"
- "Relics (75) [3 dirty]"

시나리오 3: SaveAll
- Save 버튼 클릭 또는 Ctrl+S
- Dirty 마커 모두 사라짐
- 타이틀 "Balance Editor"
- status bar "Saved at 14:23:45"

시나리오 4: Undo
- _baseDamage 10 → 20 변경
- Ctrl+Z → 10 으로 복원
- 트리 dirty 마커 사라짐 (또는 유지?)
- ⚠️ Undo 후 dirty 상태는 Unity 동작 따라 다름. 검증 필요.

시나리오 5: Redo
- Undo 후 Ctrl+Y → 20 으로 복원
- dirty 마커 다시 표시

시나리오 6: 외부 변경 감지
- Project 윈도우에서 직접 SO 편집
- 본 윈도우 Refresh 또는 자동 갱신 → dirty 표시

시나리오 7: 창 닫기 - Save
- 변경 후 X 클릭 → 다이얼로그 "3 unsaved..."
- "Save Now" → SaveAssets 호출 + 닫힘

시나리오 8: 창 닫기 - Later
- "Later" → SaveAssets 호출 X + 닫힘
- 다음에 다시 열면 dirty 그대로 (Unity 메모리 보관)

시나리오 9: 창 닫기 - 변경 없음
- dirty 0 → 다이얼로그 X, 즉시 닫힘

시나리오 10: 다중 SO Undo 영향
- A SO 변경 → B SO 변경 → Ctrl+Z 두 번
- A 와 B 모두 원복
- 트리 dirty 갱신
```

---

## 위험 / 결정 미정

### 위험

1. **Undo 후 Dirty 상태 모호**: Unity Undo 가 SO 의 dirty flag 도 복원하는지? Unity 6 동작 검증 필요. → 검증 시나리오 4 / 5.
2. **InspectorElement 의 자동 Undo 가 모든 변경 cover X**: 일부 필드 (예: 배열 추가/제거) 는 Undo 안 될 수 있음. → 본 CL 검증으로 식별 + 후속 ticket.
3. **Ctrl+S 가 Unity 의 다른 단축키와 충돌**: Project Save (Ctrl+S) 가 동시 발동? StopPropagation 으로 회피. 단 Unity 표준 SaveAssets 는 본 CL SaveAll 과 동일 결과 → 충돌 없음.
4. **외부 Undo 감지 불완전**: 본 윈도우 외부 (Project 또는 다른 Inspector) 에서 Undo → undoRedoPerformed 발화 → 본 윈도우 갱신. OK.
5. **카테고리 dirty count 성능**: 매 변경마다 75 + 16 SO 순회. 1ms 이내 OK. 대량 (10000+) 시 캐싱.
6. **OnDestroy 다이얼로그가 Editor 종료 시 깨짐**: Unity 종료 중 EditorUtility.DisplayDialog 호출하면 멈춤 가능. → OnDestroy 에서 EditorApplication.isQuitting 체크.
7. **Dirty 마커 시각 구분 약함**: "* " prefix 만으로 약함 → 빨간 글자 또는 ★ 옵션. 본 plan: prefix 만.
8. **AssetDatabase.SaveAssets 비용**: 75+16 SO 모두 dirty 시 디스크 저장 ~수백 ms. Save 버튼 클릭 시 사용자 인지 OK.

### 결정 미정

- [ ] 즉시 저장 vs 지연 — 본 plan: **Unity 표준 즉시 저장 + Dirty 시각**
- [ ] Dirty 마커 시각 — 본 plan: **`* ` prefix**
- [ ] 카테고리 dirty count 표시 — 본 plan: **표시 (`Relics (75) [3 dirty]`)**
- [ ] 창 닫기 다이얼로그 옵션 — 본 plan: **Save Now / Later**
- [ ] 자동 저장 — 본 plan: **CL-165 위임**
- [ ] Discard All — 본 plan: **별도 ticket**

---

## 후속 ticket 영향

| Ticket | CL-164 와의 관계 |
|---|---|
| **CL-165 (JSON Import/Export + Auto-save)** | Auto-save 토글 신설 시 본 CL 의 SaveAll 호출. Import 시 dirty 충돌 처리 |
| **CL-166 (데이터 카테고리 연결)** | 추가 카테고리도 본 CL dirty 추적 자동 적용 |
| **별도 ticket: 지연 저장 모드** | (b) 옵션 |
| **별도 ticket: Discard All** | 모든 변경 취소 |
| **별도 ticket: Dirty 시각 폴리싱** | 빨간 글자 / ★ 아이콘 |

---

## 예상 시간

| 단계 | 시간 |
|---|---|
| 1단계 (DirtyTracker) | 15분 |
| 2단계 (트리 dirty 마커) | 30분 |
| 3단계 (타이틀 갱신) | 15분 |
| 4단계 (Save 버튼 + Ctrl+S) | 45분 |
| 5단계 (Undo 구독) | 30분 |
| 6단계 (창 닫기 다이얼로그) | 15분 |
| 7단계 (변경 이벤트 hook) | 15분 |
| 8단계 (검증 10 시나리오) | 1시간 |
| **합계** | **약 3시간 45분** |

→ 3점 ticket 에 부합.

---

## 결정 요청

| # | 질문 | 옵션 | 추천 |
|---|---|---|---|
| 1 | 저장 모델 | **즉시 + Dirty 시각** / 지연 | **즉시** (Unity 표준) |
| 2 | Dirty 마커 시각 | **`* ` prefix** / 빨간 글자 / ★ | **prefix** (단순) |
| 3 | 카테고리 dirty count | **표시** / 미표시 | **표시** |
| 4 | Ctrl+S 단축키 | **추가** / Save 버튼만 | **추가** |
| 5 | 창 닫기 다이얼로그 | **Save Now / Later** / 자동 저장 | **다이얼로그** |
| 6 | Auto-save | 본 CL / **CL-165 위임** | **CL-165** |
| 7 | Discard All | 본 CL / **별도** | **별도** |

전부 추천대로면 **즉시 + prefix + 카테고리 count + Ctrl+S + 다이얼로그 + CL-165 + 별도**.

---

## Epic U 진행률 (CL-164 후)

| Ticket | Plan |
|---|---|
| CL-162 EditorWindow 셸 | ✅ |
| CL-163 트리뷰 + 디테일 + 검색 | ✅ |
| **CL-164 Dirty + Undo/Redo** | ✅ ← 방금 |
| CL-165 JSON Import/Export + Auto-save | ⏳ |
| CL-166 데이터 카테고리 연결 | ⏳ |

**Epic U: 3/5**

---

## 다음 plan

| 옵션 | Ticket | 점수 | 비고 |
|---|---|---|---|
| **A** | CL-165 JSON Import/Export + Auto-save | 3점 | 본 CL Dirty 흐름 위에 Auto-save + Import/Export |
| B | CL-166 데이터 카테고리 연결 | 2점 | Provider 추가만, 가벼움 |

**추천: A (CL-165)** — Epic U 안전성 마무리 (Auto-save). 본 CL 의 Dirty 추적 자연스럽게 활용.

뭐로 갈까요?
