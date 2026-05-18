# CL-162 Phase 2 완료 보고서 (2026-05-05)

## 문서 목적

Phase 1 핸드오프 ([`cl162_phase_1_handoff_20260505.md`](cl162_phase_1_handoff_20260505.md)) 후 새 환경에서 마무리한 작업의 *체크포인트 보고서*. 이 문서를 읽으면 컨텍스트 0 인 상태에서도 CL-162 종결 상태와 후속 ticket 진입 정보를 파악 가능.

본 문서로 **CL-162 종결 처리**.

---

## 1. Phase 1 ↔ Phase 2 연결

### Phase 1 종료 시점 (이전 환경)

- 코드 3개 파일 작성 완료 + remote push 됨
  - `BalanceEditorWindow.cs`
  - `Resources/BalanceEditorWindow.uxml`
  - `Resources/BalanceEditorWindow.uss`
- **asmdef 미생성 상태** — 새 환경에서 사용자 작업 예정
- 환경 전환 사유: 물리적 작업 환경 이동

### Phase 2 시작 시점 (새 환경)

- `git pull` 로 Phase 1 의 3개 파일 + Phase 1 핸드오프 문서 동기화
- Unity 프로젝트 (`client/LostMemory/`) 오픈 → 컴파일 빨간 에러 없음
- Phase 1 §2.2 단계 3 (asmdef 생성) ~ 단계 4 (검증 7 시나리오) 진행

---

## 2. 이번 환경에서 한 작업

### 2.1 Unity 프로젝트 오픈 + 컴파일 확인

- 패키지 import + 컴파일 통과
- `Assets/_Project/Scripts/Editor/BalanceEditor/` 에 Phase 1 산출물 3개 파일 + Resources/ 폴더 정상 동기화 확인
- `BalanceEditorWindow.cs` 가 Editor 폴더 컨벤션으로 자동 컴파일됨 (asmdef 없는 상태)
- Unity 메뉴 `LostMemory > Balance Editor` 즉시 동작 확인 (검증 1, 2 자동 통과)

### 2.2 asmdef 생성

`LostMemory.BalanceEditor.Editor.asmdef`

#### 최초 실수 → 수정

- 사용자가 Project 창에서 **Resources 폴더가 선택된 상태**로 우클릭 → asmdef 가 `BalanceEditor/Resources/` 하위에 생성됨
- 이렇게 두면 asmdef 가 Resources/ 하위만 관할 → `BalanceEditorWindow.cs` 가 새 어셈블리에 포함되지 않음
- **해결**: Project 창 좌측 트리에서 asmdef 파일을 `Editor > BalanceEditor` 폴더 위로 **드래그 이동**
- 이동 후 Unity 자동 재컴파일 → Console 깨끗

#### 최종 인스펙터 설정

| 항목 | 값 |
|---|---|
| Name | `LostMemory.BalanceEditor.Editor` |
| Allow 'unsafe' Code | ☐ |
| **Auto Referenced** | ☑ |
| No Engine References | ☐ |
| Override References | ☐ |
| Root Namespace | (비움) |
| Assembly Definition References | (비움 — Phase 1 §3 결정사항 9) |
| Platforms | **Include Platforms = Editor only** |

### 2.3 검증 7 시나리오 결과 — §3 표 참조

---

## 3. 검증 결과 표

| # | 시나리오 | 통과 여부 | 메모 |
|---|---|---|---|
| 1 | 메뉴 등록 | ✅ | `LostMemory > Balance Editor` 노출 + 클릭 시 윈도우 열림 |
| 2 | 레이아웃 | ✅ | 좌 30 / 우 70, "Balance Editor" 타이틀, "Ready" statusbar, placeholder 표시 |
| 3 | 리사이즈 | ✅ | 800×500 minSize 유지, 도킹 가능 |
| 4 | 다크 테마 | ✅ | 배경/패널 색 정상 |
| 5 | asmdef 분리 | ✅ | 인스펙터에서 Editor only platform 체크 확인 (Build Report 검증은 시간 사유로 생략) |
| 6 | 재오픈 / 도메인 리로드 | ✅ | 재오픈 OK, 스크립트 수정 후 윈도우 정상 유지 |
| 7 | hook 호출 | ⏭ skip | 선택 시나리오, 1~6 통과로 hook 도 정상 호출 중이라 판단 |

→ **CL-162 검증 통과**.

---

## 4. Gotchas — 이번 환경에서 새로 발견

### 4.1 asmdef 위치 함정 (중요)

Phase 1 §2.2 단계 3 절차는 "BalanceEditor 폴더 우클릭 → Create > Assembly Definition" 으로 명시. 그러나:

- Project 창에서 **현재 활성 폴더 (breadcrumb 마지막 항목)** 에 새 asset 이 생성됨
- 사용자가 직전에 Resources/ 폴더를 클릭한 상태였다면 BalanceEditor/ 우클릭 메뉴를 써도 새 파일이 Resources/ 하위로 생김 (Unity 6 의 동작)
- → **항상 좌측 트리에서 BalanceEditor 를 단일 클릭으로 활성화한 직후** Create 메뉴 사용해야 안전

**해결책 (사용한 방법)**:
- 잘못된 위치의 asmdef 를 좌측 트리의 BalanceEditor 폴더 위로 드래그 → "Move?" 확인 → 이동
- .meta 파일도 같이 이동됨 (별도 조작 불필요)

**향후 가이드라인**:
> CL-163 ~ 166 에서 추가 asmdef 만들 일 있으면, **반드시 만든 직후 위치 확인** 후 진행. asmdef 가 잘못 놓이면 **자기 폴더 + 하위 폴더만 관할** 하므로 의도한 cs 파일이 어셈블리에 누락될 수 있음.

### 4.2 드래그 이동 시 .meta 자동 동행

Unity 가 `.cs/.asmdef` 와 `.meta` 짝을 인지하므로 드래그 이동에서 .meta 도 함께 따라감. 수동 작업 불필요.

### 4.3 이동 직후 자동 재컴파일 신호

- Unity 우측 하단에 진행 바 잠깐 표시
- 이때 `BalanceEditorWindow.cs` 가 비로소 `LostMemory.BalanceEditor.Editor` 어셈블리에 포함
- Console 빨간 에러 없으면 정상 — 본 환경에서 통과 확인

### 4.4 Phase 1 §4.1 의 Auto Referenced 가정 유효함

- asmdef References 비워뒀어도 컴파일 에러 없음 → Auto Referenced ON 으로 Assembly-CSharp 자동 참조 동작 확인됨
- 단, 본 CL 은 Assembly-CSharp 의 RelicData 등을 미참조하므로 실제 참조 동작은 CL-163 진입 후 검증 필요

---

## 5. 최종 산출물 트리

```text
LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/
  BalanceEditorWindow.cs                         (Phase 1 작성, Claude)
  LostMemory.BalanceEditor.Editor.asmdef         (Phase 2 작성, 사용자)
  Resources/
    BalanceEditorWindow.uxml                     (Phase 1 작성, Claude)
    BalanceEditorWindow.uss                      (Phase 1 작성, Claude)
```

각각 `.meta` 파일 동반 (Unity 자동 생성).

문서:
```text
client/docs/khi/
  cl162_plan.md                                  원본 plan (사용자 작성)
  cl162_phase_1_handoff_20260505.md              Phase 1 핸드오프
  cl162_phase_2_handoff_20260505.md              본 문서 (Phase 2 완료)
```

---

## 6. git 상태 (본 문서 작성 시점)

- 브랜치: `S14P31C201-401-cl-162-editorwindow-ui-toolkit`
- Phase 1 push 후 추가된 변경:
  - `LostMemory.BalanceEditor.Editor.asmdef` + `.meta`
  - 신규 `.meta` 파일들 (Resources/, BalanceEditorWindow.cs.meta 등 — Unity 가 새 환경에서 생성)
  - `cl162_phase_2_handoff_20260505.md` (본 문서)
- **사용자가 직접 commit / push / MR 처리 예정** (Phase 1 §4.7 메모리 가이드라인 따름)

---

## 7. 후속 ticket 진입점

| Ticket | 진입 준비도 | hook / 진입점 |
|---|---|---|
| **CL-163** (트리뷰 + 디테일 + 검색) | ✅ **즉시 진입 가능** | `BalanceEditorWindow.PopulateLeftPanel/RightPanel` 가상 메서드 |
| CL-164 (Dirty + Undo/Redo) | CL-163 후 | `EditorWindow.SerializedObject` |
| CL-165 (JSON Import/Export) | CL-163 후 | toolbar 영역 (be-toolbar 클래스) |
| CL-166 (데이터 카테고리 연결) | CL-163 후 | 트리뷰 카테고리 노드 |

자연스러운 다음 흐름: **CL-163** ([`cl163_plan.md`](cl163_plan.md))

---

## 8. Epic U 진행률 갱신

| Ticket | 상태 |
|---|---|
| **CL-162 EditorWindow 셸** | ✅ **완료 (Phase 2)** |
| CL-163 트리뷰 + 디테일 + 검색 | ⏳ 다음 |
| CL-164 Dirty + Undo/Redo | ⏳ |
| CL-165 JSON Import/Export | ⏳ |
| CL-166 데이터 카테고리 연결 | ⏳ |

**Epic U: 1/5 완료**

---

## 9. 다음 핸드오프 작성 시점

Phase 1 문서 §8 의 누적 원칙 유지:

- 다음 환경 전환 또는 장기간 휴지 시 *동일 양식*으로 신규 doc 작성
- CL-163 진입 시점이라면: `client/docs/khi/cl163_phase_1_handoff_<YYYYMMDD>.md`
- 본 문서는 *그 시점의 스냅샷* 이므로 갱신하지 않고 *새 문서* 로 누적

---

## 10. 사용자 다음 행동 (체크리스트)

- [ ] `git status` 로 변경 파일 확인 (asmdef + .meta 들 + 본 문서)
- [ ] commit 메시지 작성 + commit
- [ ] push
- [ ] (선택) MR 생성
- [ ] CL-163 plan ([`cl163_plan.md`](cl163_plan.md)) 확인 + 설계 진입
