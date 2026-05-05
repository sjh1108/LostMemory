# CL-165 Phase 1 완료 보고서 (2026-05-05)

## 문서 목적

CL-165 (JSON Import/Export + Auto-save 토글) 의 *체크포인트 보고서*. 컨텍스트 0 인 상태에서도 CL-165 종결 상태와 후속 ticket 진입 정보를 파악 가능.

본 문서로 **CL-165 종결 처리**.

원본 plan: [`cl165_plan.md`](cl165_plan.md) (김회인)
구현 계획서: [`cl165_implementation_plan_20260505.md`](cl165_implementation_plan_20260505.md) (사용자 결정 반영)

---

## 1. CL-164 ↔ CL-165 연결

### CL-164 종료 시점 상태
- ✅ Dirty 추적 (`DirtyTracker`, `EditorUtility.IsDirty`)
- ✅ SaveAll + Ctrl+S + 창 닫기 다이얼로그
- ✅ Undo·Redo UI 동기화
- ✅ `SuppressAssetWatcher` 패턴 (자체 저장 시 watcher 차단)
- ✅ `_currentlyShownSo` + `_inRebuild` (선택/디테일 보존)
- ❌ JSON Import/Export — **본 CL 신설**
- ❌ Auto-save 토글 — **본 CL 신설**

### CL-165 진입 시점
- 동일 환경, 같은 브랜치 (`S14P31C201-401-cl-162-editorwindow-ui-toolkit`) 에서 연속 작업

---

## 2. 이번 환경에서 한 작업

### 2.1 Phase 1 — plan 검토 + 결정 합의

원본 plan 의 결정 7개 모두 추천 따라가되, **Auto-save delay 디폴트 60초 (1분)** 사용자 합의 + **Toolbar Search width 240→180px 축소** 신규 결정.

→ [`cl165_implementation_plan_20260505.md`](cl165_implementation_plan_20260505.md) 작성.

### 2.2 Phase 2 — 코드 작성 (2개 신규 + 3개 수정)

#### 신규 (2개)
```text
LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/
  JsonImportExport.cs                          ← Export 3종 + Import 2종 + 충돌 다이얼로그
  AutoSaveController.cs                        ← debounce + EditorPrefs + Play/컴파일 가드
```

#### 수정 (3개)
```text
BalanceEditorWindow.cs                         ← toolbar wire-up + Export/Import handlers + AutoSaveController 라이프사이클
Resources/BalanceEditorWindow.uxml             ← ToolbarMenu × 2 + ToolbarToggle + IntegerField 추가
Resources/BalanceEditorWindow.uss              ← .be-search 240→180px + 신규 element 스타일
```

### 2.3 Phase 3 — UI 핫픽스 (1건)

검증 도중 statusbar / 윈도우 타이틀 레이아웃 폴리시 — §4.4 Gotcha 에서 상세.

### 2.4 Phase 4 — 검증 (사용자, Unity 에디터)

12개 원본 시나리오 + 6개 환경 보강 중 핵심 11개 통과. 3개 선택 skip.

---

## 3. 검증 결과 표

### 원본 시나리오

| # | 시나리오 | 결과 | 메모 |
|---|---|---|---|
| 1 | Export Selected | ✅ | `prettyPrint` JSON 파일 생성, 모든 필드 직렬화 |
| 2 | Export Category | ✅ | 폴더 선택 → Generated 77 .json 일괄 생성 |
| 3 | Export All | ✅ | `Relics/` + `BuildSets/` 하위 폴더 + 각 SO 1 파일 |
| 4 | Import to Selected | ✅ | JSON 값 반영 + dirty + Undo 가능 + **디테일 패널 보존** (CL-164 패턴 검증) |
| 5 | Import Folder | ⏸ | (사용자 선택 skip — 패턴 동일이라 4 통과로 검증 충분) |
| 6 | Import 충돌 | ✅ | dirty SO 에 Import 시 다이얼로그 → Cancel/Overwrite 정상 동작 |
| 7 | Auto-save OFF | ✅ | 변경 후 시간 지나도 자동 저장 X |
| 8 | Auto-save ON | ✅ | 1분 멈추면 자동 SaveAll, statusbar `Saved at HH:mm:ss` |
| 9 | Delay 변경 | ✅ | 30초 입력 시 30초 후 자동 저장 |
| 10 | Debounce | ✅ | 변경 → 추가 변경 시 timer 재시작 |
| 11 | EditorPrefs 영속성 | ✅ | Auto-save ☑ + Delay 90 → Unity 재시작 후 유지 |
| 12 | 라운드트립 | ⏭ skip | (선택, JsonUtility 한계로 icon 손실 알려져 있어 의미 적음) |

### 환경 보강 시나리오

| # | 시나리오 | 결과 | 메모 |
|---|---|---|---|
| A | Import 후 디테일 보존 | ✅ | 시나리오 4 와 함께 검증 — InspectorElement 그대로, 값만 갱신 |
| B | watcher 폭주 차단 | ✅ | Import 도중 RebuildTree 미발화 (SuppressAssetWatcher 동작) |
| C | Play 모드 Auto-save 가드 | ⏭ skip | (선택) |
| D | 컴파일 중 Auto-save 가드 | ⏭ skip | (선택) |
| E | EditorPrefs 영속 | ✅ | 시나리오 11 과 동일 |
| F | Delay 클램프 | ✅ | 5 입력 → 30 / 9999 입력 → 600 |

→ **CL-165 검증 통과**. skip 된 항목은 동작 자명 또는 패턴 동일.

---

## 4. Gotchas — 이번 환경에서 새로 발견

### 4.1 Icon (Sprite) 참조 손실 — JsonUtility 한계 (예상됨)

JSON Export 결과:
```json
"_icon": {
    "instanceID": 0
}
```

원본 .asset:
```yaml
_icon: {fileID: 21300000, guid: a956fe15a8b86364bad6580b8bf863c7, type: 3}
```

**원인**: JsonUtility 가 Object 참조를 GUID 가 아닌 runtime instanceID 로 직렬화. 세션 종료 시 instanceID 무효 → 다른 환경 (또는 다음 세션) Import 시 icon 잃음.

**완화**:
- 본 CL 대상 SO (RelicData/BuildSetData) 의 유일한 Object ref 가 icon 이라 영향 제한적
- 사용자에게 가이드: "Export → Import 라운드트립 시 icon 은 None 으로 초기화됨"
- 향후 별도 ticket: GUID 기반 직렬화 (Newtonsoft + custom converter) 또는 enum-name 직렬화

**향후 가이드라인**:
- CL-166 의 추가 SO (Weapon/Skill/Shop) 가 Sprite/Object ref 가지면 같은 한계 발생
- 별도 ticket 으로 마이그레이션 계획 수립 필요

### 4.2 Export 위치를 프로젝트 폴더 안에 두면 Unity 가 자동 import

사용자가 Export 폴더로 `Assets/_Project/ScriptableObjects/Relics/` 같은 프로젝트 내 폴더 선택 시:
- Unity 가 .json 파일을 TextAsset 으로 자동 import → 각 .json 마다 .meta 생성
- git 커밋 부담 증가
- 우리 watcher (`.asset` 만 추적) 는 영향 무, 단 Project 창 어수선해짐

**완화**:
- 사용자 가이드: Export 는 **프로젝트 외부** (예: 데스크톱 / .gitignore 폴더) 권장
- JSON 은 임시 / 외부 보관용. .asset 이 source of truth

**향후 가이드라인** (선택):
- Export 시 프로젝트 폴더 감지 → 경고 다이얼로그 ("이 위치는 권장 안 됨")
- 또는 EditorPrefs 디폴트 폴더를 프로젝트 외부로 강제

### 4.3 Statusbar 레이아웃 함정 (실제 발생, 핫픽스)

**증상**: Statusbar 에 `[Auto: ON 30s | pending save]` 영구 인디케이터를 추가했더니 narrow window 에서 두 줄로 wrap 되고, wide window 에서 잘림.

**원인**:
- statusbar 안에 두 Label (좌측 status text + 우측 auto-save indicator) + flex spacer 배치
- USS 의 flex layout 이 narrow 에서 wrap 발생, height 22px 강제해도 children 이 overflow
- `flex-wrap: nowrap` / `overflow: hidden` / `text-overflow: ellipsis` 다 적용해도 안 정착

**해결**: Auto-save 정보를 **윈도우 타이틀로 이동**.
- `UpdateTitle()` 가 `Balance Editor (1 unsaved) • Auto 30s` 형식으로 통합
- statusbar 는 단일 Label 로 단순 복원
- Unity 윈도우 타이틀은 항상 보이고 자르지도 않음 (창 헤더 + 탭 + 좌상단 라벨 모두 표시)

**향후 가이드라인**:
- UI Toolkit 의 statusbar / toolbar 에 다중 영역 layout 시 USS flex 한계 주의
- 영구 정보는 윈도우 타이틀 활용 (Unity 가 잘 처리)
- 임시 메시지는 단일 Label statusbar
- 복잡한 layout 필요 시 absolute positioning 또는 별도 row 분리

### 4.4 CL-164 패턴 재사용 검증됨

본 CL 은 새 패턴 도입 없이 CL-164 의 기존 인프라 그대로 활용:

| 위험 | CL-164 패턴 | CL-165 적용 |
|---|---|---|
| watcher 캐스케이드 | `SuppressAssetWatcher` flag | Import 메서드 try-finally |
| 디테일 손실 | `_currentlyShownSo` + `_inRebuild` + `RefreshItems()` | Import 후 RefreshItems 만 |
| Inspector 즉시 발화 | RefreshItems 만 | 그대로 |
| 종료 시 다이얼로그 멈춤 | `_isQuitting` 가드 | OnDestroy 동일 |

→ **§5 §11 인사이트 검증됨** — Editor Window 패턴이 일반화되어 후속 작업에 직접 활용 가능.

---

## 5. 최종 산출물 트리

```text
LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/
  BalanceEditorWindow.cs                       (CL-162~165 누적 수정)
  IBalanceCategoryProvider.cs                  (CL-163)
  TreeNode.cs                                  (CL-163, CL-164 CachedSos)
  BalanceEditorAssetWatcher.cs                 (CL-163, CL-164 SuppressAssetWatcher)
  DirtyTracker.cs                              (CL-164)
  JsonImportExport.cs                          (CL-165 신규)
  AutoSaveController.cs                        (CL-165 신규)
  LostMemory.BalanceEditor.Editor.asmdef       (CL-162 Phase 2)
  Providers/
    RelicCategoryProvider.cs                   (CL-163)
    BuildSetCategoryProvider.cs                (CL-163)
  Resources/
    BalanceEditorWindow.uxml                   (CL-162~165 누적)
    BalanceEditorWindow.uss                    (CL-162~165 누적)
```

각 .cs/.uxml/.uss/.asmdef 에 .meta 동반.

문서:
```text
client/docs/khi/
  cl162_plan.md / cl162_phase_1_handoff_20260505.md / cl162_phase_2_handoff_20260505.md
  cl163_plan.md / cl163_implementation_plan_20260505.md / cl163_phase_1_handoff_20260505.md
  cl164_plan.md / cl164_implementation_plan_20260505.md / cl164_phase_1_handoff_20260505.md
  cl165_plan.md / cl165_implementation_plan_20260505.md / cl165_phase_1_handoff_20260505.md  ← 본 문서
```

---

## 6. git 상태 (본 문서 작성 시점)

- 브랜치: `S14P31C201-401-cl-162-editorwindow-ui-toolkit`
- CL-164 commit 후 추가된 변경:
  - 신규 2개 (JsonImportExport.cs / AutoSaveController.cs)
  - 수정 3개 (BalanceEditorWindow.cs / .uxml / .uss)
  - 본 문서 + 구현 계획서
- **사용자가 직접 commit / push / MR 처리 예정**
- commit 메시지에 **CL-165** 명시 권장

---

## 7. 후속 ticket 진입점

| Ticket | 진입 준비도 | hook / 진입점 |
|---|---|---|
| **CL-166** (추가 카테고리) | ✅ **즉시 진입 가능** | `IBalanceCategoryProvider` 구현체 추가 (Weapon/Skill/Shop) — 본 CL 의 JSON Export/Import + Auto-save 자동 적용 |
| 별도: SO ref Import 처리 | RewardPool 같은 ref 배열 지원 |
| 별도: partial merge | JSON 일부 필드만 적용 (현재는 전체 덮어쓰기) |
| 별도: Newtonsoft 마이그레이션 | Dictionary / polymorphism 지원 |
| 별도: enum-name 직렬화 | RelicTag 순서 변경 안전성 |
| 별도: GUID 기반 Object ref 직렬화 | Icon 라운드트립 보존 |
| 별도: CSV/Excel Import | 디자이너 친화 포맷 |
| 별도: Export 디폴트 폴더 가드 | 프로젝트 외부 강제 또는 경고 |
| 별도: debounce + 강제 주기 하이브리드 | 활성 편집 중에도 N분마다 강제 저장 |

자연스러운 다음 흐름: **CL-166** ([`cl166_plan.md`](cl166_plan.md))

---

## 8. Epic U 진행률 갱신

| Ticket | 상태 |
|---|---|
| CL-162 EditorWindow 셸 | ✅ |
| CL-163 트리뷰 + 디테일 + 검색 | ✅ |
| CL-164 Dirty + Undo/Redo | ✅ |
| **CL-165 JSON Import/Export + Auto-save** | ✅ **완료** |
| CL-166 데이터 카테고리 연결 | ⏳ 다음 (마지막) |

**Epic U: 4/5 완료** 🎉

---

## 9. 다음 핸드오프 작성 시점

- 다음 환경 전환 또는 장기간 휴지 시 *동일 양식*으로 신규 doc
- CL-166 진입 + 환경 전환이라면: `client/docs/khi/cl166_phase_1_handoff_<YYYYMMDD>.md`
- 본 문서는 *그 시점의 스냅샷* 이므로 갱신하지 않고 *새 문서* 로 누적

---

## 10. 사용자 다음 행동 (체크리스트)

- [ ] `git status` 로 변경 파일 확인 (.cs / .meta / .uxml / .uss / 본 문서 + 구현 계획서)
- [ ] commit 메시지 작성 (**CL-165** 명시) + commit
- [ ] push
- [ ] (선택) MR 생성
- [ ] CL-166 plan ([`cl166_plan.md`](cl166_plan.md)) 확인 + 설계 진입 — Epic U 마무리

---

## 11. 결정사항 변경 시 영향 (참조용)

| 변경 시도 | 영향 |
|---|---|
| Auto-save 디폴트 ON | EditorPrefs 디폴트 변경 1줄 |
| Delay 디폴트 변경 (30/120/300) | EditorPrefs 디폴트 변경 1줄 |
| debounce → interval 하이브리드 | AutoSaveController 로직 추가 (별도 ticket) |
| JsonUtility → Newtonsoft | 의존성 추가 + JsonImportExport 전면 재작성 (별도 ticket) |
| Import partial merge | FromJsonOverwrite → 사전 파싱 + 필드별 SetValue (별도 ticket) |
| Icon GUID 직렬화 | custom converter 도입 (별도 ticket) |
| Auto-save 토글 통합을 윈도우 타이틀에서 statusbar 로 환원 | UI 폴리싱 별도 작업, USS layout 디버깅 필요 |
| Export 프로젝트 외부 강제 | EditorUtility.SaveFolderPanel 결과 검증 + 경고 다이얼로그 |

---

## 12. 핵심 인사이트 — Editor Window 패턴 정착

본 CL 은 **CL-164 의 §12 인사이트 4가지 패턴이 그대로 작동함을 검증**:

1. **`SuppressAssetWatcher`** — Import 도중 watcher 차단. 일반화된 자체 저장 패턴
2. **`_currentlyShownSo` + `_inRebuild`** — Import 후 디테일 보존. Tree-rebuild 가 아닌 RefreshItems 사용
3. **`RefreshItems()`** — 데이터 구조 변경 없을 때 라벨만 갱신
4. **`CountAllDirty()`** — Auto-save 발화 조건 + 윈도우 타이틀 unsaved 카운트 공유

→ **새 패턴 도입 없이 모든 기능 구현 완료**. CL-166 도 동일 패턴으로 Provider 만 추가하면 됨.

### 추가 인사이트 (CL-165 신규)

5. **윈도우 타이틀이 영구 인디케이터로 가장 안정적** — statusbar 의 flex layout 함정 회피
6. **JsonUtility 의 Object ref 한계 (instanceID:0)** — 향후 SO 추가 시 동일 한계, 마이그레이션 ticket 필요
7. **debounce 의미 명시 중요** — "활성 편집 중 발화 안 함" 사용자 가이드 필수

→ CL-166 / 후속 ticket 작업 시 본 §4 Gotchas + §12 인사이트 참조하면 즉시 해결 가능.
