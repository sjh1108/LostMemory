# CL-163 Phase 1 완료 보고서 (2026-05-05)

## 문서 목적

CL-163 (Epic U 본체 — 좌측 카테고리 트리뷰 + 우측 디테일 + 검색 + 자동 새로고침) 의 *체크포인트 보고서*. 이 문서를 읽으면 컨텍스트 0 인 상태에서도 CL-163 종결 상태와 후속 ticket 진입 정보를 파악 가능.

본 문서로 **CL-163 종결 처리**.

원본 plan: [`cl163_plan.md`](cl163_plan.md) (김회인)
구현 계획서: [`cl163_implementation_plan_20260505.md`](cl163_implementation_plan_20260505.md) (사용자 결정 반영)

---

## 1. CL-162 ↔ CL-163 연결

### CL-162 종료 시점 상태
- ✅ EditorWindow 셸 ([`BalanceEditorWindow.cs`](../../LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/BalanceEditorWindow.cs))
- ✅ UXML/USS 셸 (빈 좌/우 패널 placeholder)
- ✅ asmdef ([`LostMemory.BalanceEditor.Editor.asmdef`](../../LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/LostMemory.BalanceEditor.Editor.asmdef))
- ✅ `PopulateLeftPanel/RightPanel(VisualElement)` virtual hook

### CL-163 진입 시점
- 동일 환경, 같은 브랜치 `S14P31C201-401-cl-162-editorwindow-ui-toolkit` 에서 연속 작업 (브랜치명은 CL-162 명칭 유지, 후속 commit 으로 CL-163 작업 누적)

---

## 2. 이번 환경에서 한 작업

### 2.1 Phase 1 — 코드베이스 탐색 (3개 Explore 병렬)

1. **CL-162 산출물 검증**: BalanceEditorWindow.cs 의 hook 구조, UXML named element, asmdef 위치 모두 plan 가정과 일치 확인
2. **데이터 SO 인벤토리**:
   - RelicData: **95개** (root 18 + Generated 77) — plan 의 75 가정과 차이
   - BuildSetData: 16개 (plan 일치)
   - WeaponData: 1 (CL-166 위임)
3. **Unity 환경**: 6000.3.13f1 — UI Toolkit `TreeView`/`InspectorElement`/`TwoPaneSplitView`/`ToolbarSearchField` 모두 정식 지원

### 2.2 Phase 2 — 사용자 결정 합의

`AskUserQuestion` 4회로 확정:
1. 카테고리 수: **2개** (Relics + BuildSets) — Provider 패턴 검증 + 5세트 튜닝 워크플로우
2. RelicData 범위: **Generated 77개만** (Manual 18 제외) — 5세트 시스템 튜닝 집중
3. 디테일 패널: **InspectorElement 자동** (디자이너 친화 Custom UXML 은 CL-103 위임)
4. 자동 새로고침: **AssetPostprocessor + Refresh 버튼 둘 다**

### 2.3 Phase 3 — 코드 작성 (8개 파일)

#### 신규 (5개)

```text
LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/
  IBalanceCategoryProvider.cs                  ← 카테고리 인터페이스
  TreeNode.cs                                  ← 트리 노드 데이터
  BalanceEditorAssetWatcher.cs                 ← AssetPostprocessor 자동 새로고침
  Providers/
    RelicCategoryProvider.cs                   ← Generated 폴더 RelicData
    BuildSetCategoryProvider.cs                ← BuildSetData
```

#### 수정 (3개, CL-162 산출물)

```text
BalanceEditorWindow.cs                         ← TreeView/Search/Splitter/InspectorElement 통합
Resources/BalanceEditorWindow.uxml             ← uie:Toolbar 로 교체 (핫픽스 후)
Resources/BalanceEditorWindow.uss              ← 단순화 (Unity Toolbar 자체 스타일 신뢰)
```

### 2.4 Phase 4 — Gotcha 두 건 핫픽스

- **2.4.1 Assembly-CSharp 참조 불가** — 컴파일 에러 → string 기반 `AssetTypeFilter` 로 우회
- **2.4.2 Toolbar 가로 레이아웃 깨짐** — 일반 div + USS row → `<uie:Toolbar>` 표준 교체

### 2.5 Phase 5 — 검증 (사용자, Unity 에디터)

13개 시나리오, 10개 명시 통과 + 3개 자명/자동 통과.

---

## 3. 검증 결과 표

| # | 시나리오 | 결과 | 메모 |
|---|---|---|---|
| 1 | 트리 표시 | ✅ | `Relics (77)` / `BuildSets (16)` 노출 |
| 2 | 카테고리 펼치기 | ✅ | Generated 77 + BuildSet 16 자식 표시 (Manual 18 미포함 확인) |
| 3 | SO 선택 → 디테일 | ✅ | InspectorElement 가 RelicData 의 모든 필드 (Display/Tag/Inventory/Effects/Legacy) 자동 표시 |
| 4 | 디테일 편집 | ✅ | Size 등 변경 → asset 즉시 update |
| 5 | Project ping | (자동) | `EditorGUIUtility.PingObject(so)` 호출 동작 (자명) |
| 6 | 검색 매치 | ✅ | "가죽" 입력 → 매치 RelicData + 자동 펼침 |
| 7 | 검색 빈 입력 복원 | ✅ | 빈 문자열 → 전체 트리 복원 |
| 8 | 검색 매치 0 | (자명) | filter 무매치 시 빈 트리 (코드 검증) |
| 9 | 자동 새로고침 | ✅ | Generated/ 폴더 SO 추가 → 트리 자동 갱신 |
| 10 | 수동 Refresh 버튼 | ✅ | toolbar 핫픽스 후 정상 클릭 |
| 11 | Splitter 드래그 | ✅ | TwoPaneSplitView 좌우 너비 조절 OK |
| 12 | 상태 표시 statusbar | (자동) | `Selected: <name>` / `Loaded N categories, M items` 표시 (자명) |
| 13 | 재오픈 / 도메인 리로드 | ✅ | 닫고 열기 + 스크립트 수정 후도 OK |

→ **CL-163 검증 통과**.

---

## 4. Gotchas — 이번 환경에서 새로 발견

### 4.1 Assembly-CSharp 참조 불가

**증상**: 첫 컴파일 시 다음 에러 발생.
```
error CS0234: The type or namespace name 'Relics' does not exist
              in the namespace 'LostMemory'
```

**원인**:
- 우리 Editor asmdef (`LostMemory.BalanceEditor.Editor`) 가 `LostMemory.Relics` namespace (RelicData / BuildSetData) 를 참조 시도
- Unity 규칙: **custom asmdef 는 Assembly-CSharp 직접 참조 불가** (한 방향만 허용 — Assembly-CSharp → custom asmdef)
- CL-162 Phase 2 §4.1 에서 미리 플래그된 케이스, CL-163 plan §5.1 에서도 검증 항목으로 명시

**해결**: Provider 가 typed `RelicData` 를 직접 참조하지 않고 **문자열 기반 `AssetTypeFilter`** ("RelicData", "BuildSetData") 로 우회.
- `AssetDatabase.FindAssets($"t:{AssetTypeFilter}", ...)` 의 "t:" 필터는 string-based 라 타입 import 불필요
- Provider 는 `IEnumerable<ScriptableObject>` 반환 (typed RelicData[] 대신)
- InspectorElement 는 `ScriptableObject` 받아도 실제 타입의 모든 필드 자동 표시 → 기능 영향 무

**대안 검토 (탈락)**:
- (B) Runtime asmdef 신설: `Relics/` 폴더 의존성이 `LostMemory.Networking.Common`/`LostMemory.Combat`/`LostMemory.Data`/`LostMemory.TestKhi`/`LostMemory.MagicalGirl` 5개 namespace 로 캐스케이드 → 대규모 리팩터링
- (D) Editor asmdef 삭제: Editor 폴더 컨벤션이 동일 보호 제공하지만 CL-162 의 검증 5 (asmdef 분리) 잃음

→ **string filter 가 가장 작은 변경** — CL-162 작업 보존, 후속 ticket 도 동일 패턴.

**향후 가이드라인**:
- CL-166 의 추가 Provider (Weapon/Skill/Shop) 도 동일 string filter 패턴 사용
- 만약 나중에 typed 참조 필요 (예: SerializedObject 의 typed 접근) 하면 그 때 Runtime asmdef 분리 검토

### 4.2 Toolbar 가로 레이아웃 깨짐

**증상**: 첫 빌드 후 toolbar 가 가로 한 줄이 아닌 세로로 쌓임. SearchField 와 Refresh 버튼이 inspector 영역에 겹침.

**원인**:
- 일반 `<ui:VisualElement class="be-toolbar">` 에 USS 로 `flex-direction: row` 적용
- 그러나 자식 `<uie:ToolbarSearchField>` 가 toolbar 컨텍스트 외부에서 자기 너비를 100% 로 가정 → row 안에서 인접 element 밀려서 다음 줄로 wrap
- `flex-direction: row` 만으로는 ToolbarSearchField 의 내부 layout 계산을 통제 못 함

**해결**: UXML 의 `<ui:VisualElement class="be-toolbar">` → `<uie:Toolbar class="be-toolbar">` 로 교체. Unity 표준 Toolbar 클래스가 자체 row layout + height + padding 처리. ToolbarSearchField/ToolbarButton 도 toolbar 자식으로 인식돼 사이즈 계산 깔끔.

USS 도 단순화: `.be-toolbar` 의 강제 `flex-direction: row`/`height`/`padding`/`background-color` 모두 제거 (Unity Toolbar 자체 스타일 신뢰), `flex-shrink: 0` 만 남김.

**향후 가이드라인**:
- toolbar 영역에 추가 element (CL-165 의 Export/Import 버튼 등) 추가 시 `<uie:ToolbarButton>` 또는 toolbar 호환 element 사용
- 일반 `<ui:VisualElement>` + custom CSS 로 toolbar 흉내내지 말 것 — Unity 표준 활용

---

## 5. 최종 산출물 트리

```text
LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/
  BalanceEditorWindow.cs                       (CL-162 작성, CL-163 대폭 수정)
  IBalanceCategoryProvider.cs                  (CL-163 신규)
  TreeNode.cs                                  (CL-163 신규)
  BalanceEditorAssetWatcher.cs                 (CL-163 신규)
  LostMemory.BalanceEditor.Editor.asmdef       (CL-162 Phase 2 작성, 변경 없음)
  Providers/
    RelicCategoryProvider.cs                   (CL-163 신규)
    BuildSetCategoryProvider.cs                (CL-163 신규)
  Resources/
    BalanceEditorWindow.uxml                   (CL-162 작성, CL-163 수정 + 핫픽스)
    BalanceEditorWindow.uss                    (CL-162 작성, CL-163 수정 + 핫픽스)
```

각 .cs/.uxml/.uss/.asmdef 에 .meta 파일 동반 (Unity 자동 생성).

문서:
```text
client/docs/khi/
  cl162_plan.md                                (김회인)
  cl162_phase_1_handoff_20260505.md            CL-162 Phase 1
  cl162_phase_2_handoff_20260505.md            CL-162 완료
  cl163_plan.md                                (김회인 원본 plan)
  cl163_implementation_plan_20260505.md        구현 계획서 (사용자 결정 반영)
  cl163_phase_1_handoff_20260505.md            본 문서
```

---

## 6. git 상태 (본 문서 작성 시점)

- 브랜치: `S14P31C201-401-cl-162-editorwindow-ui-toolkit`
- CL-162 commit 후 추가된 변경:
  - 신규 5개 .cs 파일 + .meta
  - 수정 3개 (BalanceEditorWindow.cs / .uxml / .uss)
  - Providers/ 폴더 + .meta
  - cl163_implementation_plan_20260505.md
  - cl163_phase_1_handoff_20260505.md (본 문서)
- **사용자가 직접 commit / push / MR 처리 예정** (메모리 가이드라인 따름)

→ 브랜치명이 CL-162 명칭이라 MR 시 ticket 번호 표기 보강 검토 (예: commit message 에 "CL-163" 명시).

---

## 7. 후속 ticket 진입점

| Ticket | 진입 준비도 | hook / 진입점 |
|---|---|---|
| **CL-164** (Dirty + Undo/Redo) | ✅ **즉시 진입 가능** | `EditorUtility.IsDirty(so)` + `Undo.RecordObject` + InspectorElement 위에 시각 추가 |
| CL-165 (JSON Import/Export) | CL-164 후 | toolbar 에 `<uie:ToolbarButton>` Export/Import 추가 + Auto-save 토글 |
| CL-166 (추가 카테고리) | CL-165 후 | `IBalanceCategoryProvider` 구현체 3~4개 (Weapon/Skill/Shop) 추가, string filter 패턴 동일 |
| CL-103 (디자이너 친화 폴리시) | MVP 후 | InspectorElement → Custom UXML per SO 타입 (legacy 필드 숨김, [Header] 그룹화 폴리시) |

자연스러운 다음 흐름: **CL-164** ([`cl164_plan.md`](cl164_plan.md))

---

## 8. Epic U 진행률 갱신

| Ticket | 상태 |
|---|---|
| CL-162 EditorWindow 셸 | ✅ |
| **CL-163 트리뷰 + 디테일 + 검색** | ✅ **완료** |
| CL-164 Dirty + Undo/Redo | ⏳ 다음 |
| CL-165 JSON Import/Export | ⏳ |
| CL-166 데이터 카테고리 연결 | ⏳ |

**Epic U: 2/5 완료**

---

## 9. 다음 핸드오프 작성 시점

- 다음 환경 전환 또는 장기간 휴지 시 *동일 양식*으로 신규 doc 작성
- CL-164 진입 + 환경 전환이라면: `client/docs/khi/cl164_phase_1_handoff_<YYYYMMDD>.md`
- 본 문서는 *그 시점의 스냅샷* 이므로 갱신하지 않고 *새 문서* 로 누적

---

## 10. 사용자 다음 행동 (체크리스트)

- [ ] `git status` 로 변경 파일 확인 (.cs / .meta / .uxml / .uss / 본 문서 + 구현 계획서)
- [ ] commit 메시지 작성 (CL-163 명시) + commit
- [ ] push
- [ ] (선택) MR 생성
- [ ] CL-164 plan ([`cl164_plan.md`](cl164_plan.md)) 확인 + 설계 진입

---

## 11. 결정 변경 시 영향 (참조용)

| 변경 시도 | 비용 | 영향 |
|---|---|---|
| Manual 18 다시 포함 | 1줄 (RelicCategoryProvider.SearchFolders) | 즉시 트리 95 → 95 (Generated + Manual 통합) |
| BuildSets 카테고리 제외 | 1줄 (`_providers` 리스트) | 즉시 BuildSets 미표시 |
| Provider typed 참조 복원 | Runtime asmdef 신설 (5+ 캐스케이드) | 큼 — CL-166 후로 미루기 권장 |
| InspectorElement → Custom UXML | 신규 ticket (CL-103 또는 별도) | 디자이너 친화 정렬 — MVP 후 |
| 카테고리 추가 (Weapons/Skills/Shop) | Provider 클래스 + `_providers` 등록 | CL-166 본체 |
