# CL-164 Phase 1 완료 보고서 (2026-05-05)

## 문서 목적

CL-164 (Dirty 마커 + 명시적 저장 + Undo/Redo) 의 *체크포인트 보고서*. 이 문서를 읽으면 컨텍스트 0 인 상태에서도 CL-164 종결 상태와 후속 ticket 진입 정보를 파악 가능.

본 문서로 **CL-164 종결 처리**.

원본 plan: [`cl164_plan.md`](cl164_plan.md) (김회인)
구현 계획서: [`cl164_implementation_plan_20260505.md`](cl164_implementation_plan_20260505.md) (사용자 결정 반영)

---

## 1. CL-163 ↔ CL-164 연결

### CL-163 종료 시점 상태
- ✅ TreeView + InspectorElement + 검색 + AssetPostprocessor 자동 새로고침
- ✅ `IBalanceCategoryProvider` 인터페이스 + RelicCategoryProvider (Generated 77) + BuildSetCategoryProvider (16)
- ✅ Unity 표준 toolbar (`<uie:Toolbar>` 핫픽스 후)
- ❌ Dirty 추적 / 명시적 저장 / Undo·Redo UI 동기화 — **본 CL 신설**

### CL-164 진입 시점
- 동일 환경, 같은 브랜치 (`S14P31C201-401-cl-162-editorwindow-ui-toolkit`) 에서 연속 작업
- 환경 전환 없음

---

## 2. 이번 환경에서 한 작업

### 2.1 Phase 1 — plan 검토 + 결정 합의

원본 plan ([`cl164_plan.md`](cl164_plan.md)) 의 결정 7개 중:
- 사용자 합의 핵심: 저장 모델 (즉시) / Dirty 시각 (prefix + **빨간 글자 둘 다**)
- 나머지 5개는 plan 추천 그대로 (카테고리 dirty count 표시 / Ctrl+S 추가 / 다이얼로그 Save Now·Later / Auto-save CL-165 위임 / Discard All 별도)

→ [`cl164_implementation_plan_20260505.md`](cl164_implementation_plan_20260505.md) 작성.

### 2.2 Phase 2 — 코드 작성 (1개 신규 + 5개 수정)

#### 신규 (1개)
```text
LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/
  DirtyTracker.cs                              ← EditorUtility.IsDirty wrapper
```

#### 수정 (5개)
```text
BalanceEditorWindow.cs                         ← Save / Ctrl+S / Undo / OnDestroy / dirty marker / Title / Inspector hook / 선택 보존
TreeNode.cs                                    ← CachedSos 필드 추가 (동적 카테고리 라벨용)
BalanceEditorAssetWatcher.cs                   ← SuppressAssetWatcher 체크 추가
Resources/BalanceEditorWindow.uxml             ← Save All ToolbarButton 추가
Resources/BalanceEditorWindow.uss              ← .be-save-btn / .be-tree-item-dirty 추가
```

### 2.3 Phase 3 — 핫픽스 3건

검증 도중 발견된 버그를 누적 수정:

1. **버그 #1**: 클릭 → 디테일 안 뜨고 닫힘
2. **버그 #2**: Save All 클릭 → 디테일 닫힘 (다른 경로)
3. **버그 #3**: Refresh 버튼 / 외부 변경 → 디테일 닫힘 (일반 케이스)

→ §4 Gotchas 에서 상세 분석.

### 2.4 Phase 4 — 검증 (사용자, Unity 에디터)

13개 시나리오 통과 (Undo 후 dirty 잔존은 Unity 표준 동작으로 수용).

---

## 3. 검증 결과 표

| # | 시나리오 | 결과 | 메모 |
|---|---|---|---|
| 1 | Dirty 마커 단일 | ✅ | `* RelicData_xxx` (빨간색) + 타이틀 `(1 unsaved)` + 카테고리 `[1 dirty]` |
| 2 | Dirty 마커 다중 | ✅ | 타이틀 `(N unsaved)` + 카테고리 `[N dirty]` |
| 3 | SaveAll | ✅ | Save All 버튼 / Ctrl+S 모두 dirty 마커 사라짐 + statusbar `Saved at HH:mm:ss` + **디테일 유지** |
| 4 | Undo | ⚠️ 부분 | 값 원복 OK, **dirty 마커 유지** (Unity 6 표준 — Option A 채택) |
| 5 | Redo | ✅ | 값 재적용 + dirty 표시 |
| 6 | 외부 변경 감지 | ✅ | AssetPostprocessor 발화 후 트리 갱신 + 선택 보존 |
| 7 | 창 닫기 — Save Now | ✅ | 다이얼로그 → SaveAssets + 닫힘 |
| 8 | 창 닫기 — Later | ✅ | SaveAssets X + 닫힘, 재오픈 시 dirty 그대로 |
| 9 | 창 닫기 — 변경 없음 | ✅ | 다이얼로그 X 즉시 닫힘 |
| 10 | 다중 SO Undo | ⚠️ 부분 | 값 원복 OK, dirty 잔존 (시나리오 4 와 동일 사유) |
| 11 | Splitter 드래그 | ✅ | (CL-163 통과 그대로 유지) |
| 12 | 상태 표시 | ✅ | `Selected: <name>` / `Saved at ...` / `Undo/Redo applied` |
| 13 | 재오픈 / 도메인 리로드 | ✅ | (CL-163 통과 + Undo 구독 해제/재등록 정상) |

→ **CL-164 검증 통과**. Undo 후 dirty 잔존은 의도된 동작.

---

## 4. Gotchas — 이번 환경에서 새로 발견

### 4.1 InspectorElement 의 SerializedPropertyChangeEvent 즉시 발화 (버그 #1)

**증상**: 트리 항목 클릭 → 디테일 안 뜨고 즉시 닫힘.

**원인 트레이스**:
```
1. 트리 클릭 → ShowDetail(so) → InspectorElement 생성
2. InspectorElement 초기화 시 SerializedPropertyChangeEvent 즉시 발화 (Unity 6 동작)
3. OnInspectorChanged → RebuildTree → SetRootItems → tree IDs 재생성 → 선택 손실
4. tree 가 빈 selectionChanged 발화 → OnTreeSelectionChanged([]) → ClearDetail()
5. 디테일 사라짐
```

**해결**: 변경 감지 시 `RebuildTree` 대신 `_treeView.RefreshItems()` 사용. RefreshItems 는 bindItem 만 재실행 → 선택 유지 + ID 재생성 X. 카테고리 라벨이 동적으로 갱신되도록 TreeNode 에 `CachedSos` 필드 추가하고 bindItem 에서 dirty count 동적 계산.

**향후 가이드라인**:
- InspectorElement 의 변경 감지는 **idempotent UI 갱신**으로 응답 (RefreshItems 정도). 전체 트리 재구성 (SetRootItems) 은 피해야 함
- 다른 EditorWindow 도 동일 패턴 권장

### 4.2 AssetDatabase.SaveAssets 의 AssetPostprocessor 캐스케이드 (버그 #2)

**증상**: Save All 버튼 클릭 → 디테일 닫힘.

**원인 트레이스**:
```
1. SaveAll → AssetDatabase.SaveAssets()
2. SaveAssets 가 AssetPostprocessor.OnPostprocessAllAssets 트리거
3. BalanceEditorAssetWatcher → BalanceEditorWindow.RefreshTree()
4. RefreshTree → RebuildTree → SetRootItems → 선택 손실 → ClearDetail()
```

본 CL 의 SaveAll 자체는 RefreshItems 만 사용했으나, 우회로로 watcher 가 RebuildTree 를 깨움.

**해결**: `SuppressAssetWatcher` 정적 플래그 추가. SaveAll 의 try-finally 블록에서 SaveAssets 동안만 ON → AssetPostprocessor 가 우리 자체 저장은 무시.

```csharp
SuppressAssetWatcher = true;
try { AssetDatabase.SaveAssets(); }
finally { SuppressAssetWatcher = false; }
```

**향후 가이드라인**:
- 자체 작업으로 SaveAssets 호출 시 watcher 억제 패턴 활용
- CL-165 의 JSON Import 도 동일 패턴 적용 권장 (Import 도중 watcher 폭주 방지)

### 4.3 RebuildTree 자체의 선택 손실 (버그 #3)

**증상**: Refresh 버튼 클릭 / 외부 SO 변경 / 검색 입력 → 디테일 닫힘.

**원인**: RebuildTree 가 `SetRootItems` 호출 시 tree IDs 가 재생성되어 선택 손실 → 빈 selectionChanged → ClearDetail.

**해결 — 일반화된 선택 보존 패턴**:
```csharp
private ScriptableObject _currentlyShownSo;
private bool _inRebuild;

private void RebuildTree(string filter = null)
{
    var prevSelectedSo = _currentlyShownSo;
    _inRebuild = true;
    try {
        // ... SetRootItems / Rebuild ...
        if (prevSelectedSo != null) {
            int? newId = FindIdForSo(prevSelectedSo, data);
            if (newId.HasValue) {
                _treeView.SetSelectionById(newId.Value);
                _treeView.ScrollToItemById(newId.Value);
            }
        }
    }
    finally { _inRebuild = false; }
}

private void OnTreeSelectionChanged(IEnumerable<object> selected)
{
    var node = selected.OfType<TreeNode>().FirstOrDefault();
    if (node?.So == null) {
        if (_inRebuild) return;   // 리빌드 중 빈 selection 무시 → 디테일 보존
        ClearDetail();
        _currentlyShownSo = null;
        return;
    }
    if (node.So == _currentlyShownSo) {
        // 같은 SO 재선택 — 인스펙터 재생성 안 함 (깜빡임 없음)
        UpdateStatus($"Selected: {node.So.name}");
        return;
    }
    _currentlyShownSo = node.So;
    ShowDetail(node.So);
    ...
}
```

세 가지가 핵심:
- `_currentlyShownSo` 추적 — 리빌드 후 같은 SO 재선택용
- `_inRebuild` 플래그 — 빈 selectionChanged 차단
- 같은 SO 재선택 시 ShowDetail 스킵 — InspectorElement 재생성 방지 (스크롤/foldout 상태 유지)

**향후 가이드라인**:
- TreeView 가 있는 EditorWindow 에서 RebuildTree 류 작업 시 동일 패턴 적용
- CL-165/166 에서도 트리 재빌드 (Import / 카테고리 추가) 시 자동 혜택

### 4.4 Unity 6 Undo 가 dirty flag 유지 (시나리오 4/10)

**증상**: Ctrl+Z 후 값은 원복되지만 dirty 마커 (`* ` + 빨간 글자) 유지.

**원인**: Unity 의 Undo 가 SO 값 원복을 "또 한 번의 수정" 으로 처리 → `SetDirty` 내부 호출 → `EditorUtility.IsDirty(so)` true 유지.

**검토한 옵션 3가지**:
- (A) Unity 표준 그대로 — dirty 유지, Save All 로 정리
- (B) OnUndoRedo 에서 강제 ClearDirty — 위험 (Undo 안 한 다른 변경 같이 손실)
- (C) Disk 와 SerializedObject 비교 후 일치 시 ClearDirty — 정확하지만 비용 큼 (~수백 ms)

**채택**: **(A) Unity 표준 그대로**.
- 가장 안전 (false negative 가능성 0)
- Save All 로 사용자가 정리 가능
- 디스크 영향 무 (idempotent SaveAssets)
- 시각적 거슬림은 별도 폴리시 ticket (CL-103 등) 에서 (C) 도입 검토

**향후 가이드라인**:
- 디자이너에게 "Ctrl+Z 후에도 별표 보이면 Save All 한 번 누르세요" 가이드
- 강제 종료 안전망은 **CL-165 의 Auto-save 토글** 이 담당

---

## 5. 최종 산출물 트리

```text
LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/
  BalanceEditorWindow.cs                       (CL-162 작성, CL-163/164 수정 누적)
  IBalanceCategoryProvider.cs                  (CL-163 신규)
  TreeNode.cs                                  (CL-163 신규, CL-164 CachedSos 추가)
  BalanceEditorAssetWatcher.cs                 (CL-163 신규, CL-164 SuppressAssetWatcher 체크 추가)
  DirtyTracker.cs                              (CL-164 신규)
  LostMemory.BalanceEditor.Editor.asmdef       (CL-162 Phase 2)
  Providers/
    RelicCategoryProvider.cs                   (CL-163 신규)
    BuildSetCategoryProvider.cs                (CL-163 신규)
  Resources/
    BalanceEditorWindow.uxml                   (CL-162 작성, CL-163/164 누적 수정)
    BalanceEditorWindow.uss                    (CL-162 작성, CL-163/164 누적 수정)
```

각 .cs/.uxml/.uss/.asmdef 에 .meta 동반.

문서:
```text
client/docs/khi/
  cl162_plan.md
  cl162_phase_1_handoff_20260505.md
  cl162_phase_2_handoff_20260505.md
  cl163_plan.md
  cl163_implementation_plan_20260505.md
  cl163_phase_1_handoff_20260505.md
  cl164_plan.md
  cl164_implementation_plan_20260505.md
  cl164_phase_1_handoff_20260505.md            본 문서
```

---

## 6. git 상태 (본 문서 작성 시점)

- 브랜치: `S14P31C201-401-cl-162-editorwindow-ui-toolkit`
- CL-163 commit 후 추가된 변경:
  - 신규 1개 (DirtyTracker.cs)
  - 수정 5개 (BalanceEditorWindow.cs / .uxml / .uss / TreeNode.cs / BalanceEditorAssetWatcher.cs)
  - 본 문서 + 구현 계획서
- **사용자가 직접 commit / push / MR 처리 예정**
- commit 메시지에 **CL-164** 명시 권장 (브랜치명이 CL-162 라서)

---

## 7. 후속 ticket 진입점

| Ticket | 진입 준비도 | hook / 진입점 |
|---|---|---|
| **CL-165** (JSON Import/Export + Auto-save) | ✅ **즉시 진입 가능** | toolbar 에 Import/Export `<uie:ToolbarButton>` 추가, Auto-save 토글은 본 CL `SaveAll()` 주기 호출, JSON Import 시 `SuppressAssetWatcher` 패턴 활용 |
| CL-166 (추가 카테고리) | CL-165 후 | `IBalanceCategoryProvider` 구현체 추가 — 본 CL dirty 추적 자동 적용 |
| CL-103 (디자이너 친화 폴리시) | MVP 후 | InspectorElement → Custom UXML per SO 타입. 필드별 dirty 마커 (`* Size`) 도 여기서 자연스럽게 추가 |
| 별도: 지연 저장 모드 | (b) 옵션 |
| 별도: Discard All | 모든 dirty SO 의 메모리 변경 취소 |
| 별도: Undo 후 dirty 자동 정리 (Option C) | Disk 비교 후 ClearDirty |

자연스러운 다음 흐름: **CL-165** ([`cl165_plan.md`](cl165_plan.md))

---

## 8. Epic U 진행률 갱신

| Ticket | 상태 |
|---|---|
| CL-162 EditorWindow 셸 | ✅ |
| CL-163 트리뷰 + 디테일 + 검색 | ✅ |
| **CL-164 Dirty + Undo/Redo** | ✅ **완료** |
| CL-165 JSON Import/Export + Auto-save | ⏳ 다음 |
| CL-166 데이터 카테고리 연결 | ⏳ |

**Epic U: 3/5 완료**

---

## 9. 다음 핸드오프 작성 시점

- 다음 환경 전환 또는 장기간 휴지 시 *동일 양식*으로 신규 doc
- CL-165 진입 + 환경 전환이라면: `client/docs/khi/cl165_phase_1_handoff_<YYYYMMDD>.md`
- 본 문서는 *그 시점의 스냅샷* 이므로 갱신하지 않고 *새 문서* 로 누적

---

## 10. 사용자 다음 행동 (체크리스트)

- [ ] `git status` 로 변경 파일 확인 (.cs / .meta / .uxml / .uss / 본 문서 + 구현 계획서)
- [ ] commit 메시지 작성 (**CL-164** 명시) + commit
- [ ] push
- [ ] (선택) MR 생성
- [ ] CL-165 plan ([`cl165_plan.md`](cl165_plan.md)) 확인 + 설계 진입

---

## 11. 결정 변경 시 영향 (참조용)

| 변경 시도 | 비용 | 영향 |
|---|---|---|
| Undo 후 dirty 자동 clear (Option B) | OnUndoRedo 에서 모든 dirty clear | **위험** — Undo 안 한 다른 변경 손실 |
| Undo 후 dirty 똑똑한 clear (Option C) | 별도 ticket — disk 비교 모듈 | 안전하지만 비용 큼 |
| Dirty 시각 단순화 (prefix 만 또는 빨간 글자만) | bindItem / USS 1줄 변경 | UX 폴리싱 |
| 즉시 → 지연 저장 | InspectorElement 우회 / Custom UXML | 대규모 변경, 별도 ticket |
| Auto-save | CL-165 (별도 ticket) | 강제 종료 안전망 |
| 필드별 dirty 마커 (`* Size` 등) | CL-103 또는 별도 — Custom UXML 도입 필요 | 대규모, 디자이너 친화 폴리시 |

---

## 12. 핵심 인사이트 — Editor Window 패턴

본 CL 에서 **TreeView + InspectorElement 가 있는 Editor Window 의 흔한 함정 3가지** 를 모두 만남:

1. **InspectorElement 의 즉시 발화 이벤트** — 변경 감지에는 idempotent UI 갱신만
2. **AssetDatabase.SaveAssets 의 AssetPostprocessor 캐스케이드** — 자체 저장은 watcher 억제
3. **TreeView Rebuild 의 선택 손실** — `_currentlyShownSo` 추적 + `_inRebuild` 가드 + ID 재검색 패턴

→ CL-165/166 에서 동일 패턴 재발 시 본 §4 Gotchas 참조하여 즉시 해결 가능.
