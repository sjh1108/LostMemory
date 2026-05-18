# CL-162 Phase 1 핸드오프 (2026-05-05)

## 문서 목적

작업 환경을 전환하는 시점의 *체크포인트 보고서*. 이 문서를 읽으면 컨텍스트 0 인 상태에서도 CL-162 작업 재개 가능.

## 환경 전환 사유

물리적 작업 환경 이동.

---

## 1. 완료된 작업

### 1.1 Plan 작성 및 사용자 승인 ✅

원본 plan: [`docs/khi/cl162_plan.md`](cl162_plan.md) — 결정사항/위험/구현 단계 기재.

코드베이스 탐색 후 보정 plan: `C:\Users\AD\.claude\plans\cl-162-plan-md-http-plan-md-linked-kettle.md` (로컬, 새 환경에서는 이 파일 없음 — 필요시 본 핸드오프의 §3 결정사항 참조).

### 1.2 코드 작성 ✅ (3개 파일, 사용자 승인된 plan 따름)

```text
LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/
  BalanceEditorWindow.cs
  Resources/
    BalanceEditorWindow.uxml
    BalanceEditorWindow.uss
```

#### `BalanceEditorWindow.cs` 핵심
- 네임스페이스: `LostMemory.Editor.BalanceEditor` (기존 `LostMemory.Editor.<Domain>` 패턴 따름)
- 메뉴: `[MenuItem("LostMemory/Balance Editor")]` (**기존 컨벤션 — `Window/...` 아님**)
- `CreateGUI()` 에서 UXML/USS Resources.Load
- `protected virtual PopulateLeftPanel/RightPanel(VisualElement)` — CL-163 override hook

#### `BalanceEditorWindow.uxml` 핵심
- 구조: `be-root > [be-toolbar, be-body[be-left-panel, be-splitter, be-right-panel], be-statusbar]`
- `name="LeftPanel"` / `name="RightPanel"` / `name="StatusLabel"` 부여
- placeholder 텍스트 "(카테고리 트리 — CL-163)" / "(상세 편집 패널 — CL-163)"

#### `BalanceEditorWindow.uss` 핵심
- 다크 테마: 배경 rgb(56), 좌측 패널 rgb(48), toolbar/statusbar rgb(40)
- 좌측 패널 30% width, min-width 200px

---

## 2. 미완료 — 새 환경에서 이어할 작업

### 2.1 즉시 시작점

**Unity 에디터에서 asmdef 생성** — 사용자 작업.

### 2.2 절차

새 환경에서 다음 순서로 진행:

#### 단계 1: Git 동기화

```bash
git pull              # 이전 환경에서 push 한 분 가져오기
git status            # 깨끗한지 확인
```

→ 이전 환경에서 **반드시 push 후 종료**해야 함. 안 그러면 새 환경에서 재개 불가.

#### 단계 2: Unity 프로젝트 오픈

1. Unity Hub 에서 프로젝트 열기 (`client/LostMemory/`)
2. 패키지 import + 컴파일 대기
3. Console 빨간 에러 확인 — `BalanceEditorWindow.cs` 가 `using UnityEditor;` 사용하므로 Editor 컴파일 시점에만 컴파일 됨. 단, 폴더명이 `Editor` 아래라 Unity 가 자동 인식하므로 asmdef 없어도 일단 컴파일은 됨.

#### 단계 3: asmdef 생성 (Unity 에디터)

1. Project 창에서 `Assets/_Project/Scripts/Editor/BalanceEditor` 폴더 우클릭
2. `Create > Scripting > Assembly Definition` (Unity 6) 또는 `Create > Assembly Definition` (이전 버전)
   - 메뉴 못 찾으면 Create 메뉴 검색창에 `assembly` 입력
3. 파일명: `LostMemory.BalanceEditor.Editor`
4. 인스펙터 설정:
   - **Name**: `LostMemory.BalanceEditor.Editor`
   - **Auto Referenced**: ✅ ON (체크)
   - **Include Platforms**: `Editor` 만 체크 (나머지 모두 해제)
   - **Assembly Definition References**: 비워둠
   - **Apply**

> ⚠️ asmdef 안 만들고도 코드는 동작 (폴더명 `Editor` 관습으로 Unity 가 인식). 다만 plan 의 §위험 3 / 검증 시나리오 5 통과 안 함.

#### 단계 4: 검증 7 시나리오 (사용자, Unity 에디터)

| # | 시나리오 | 통과 기준 |
|---|---|---|
| 1 | 메뉴 등록 | Unity 메뉴 `LostMemory > Balance Editor` 노출. 클릭 시 윈도우 열림 |
| 2 | 레이아웃 | 좌 30% / 우 70%, 상단 "Balance Editor" 타이틀, 하단 "Ready" statusbar, placeholder 표시 |
| 3 | 리사이즈 | 800×500 미만 축소 불가, 도킹 가능 |
| 4 | 다크 테마 | 배경 rgb(56), 좌측 rgb(48), border 색상 적용 확인 |
| 5 | asmdef 분리 | 인스펙터에 Editor only platform 체크. Player 빌드 시 BalanceEditor 폴더 미포함 |
| 6 | 재오픈 / 도메인 리로드 | 윈도우 닫고 열기, 스크립트 수정 후 정상 동작 |
| 7 | hook 호출 | (선택) 임시로 `PopulateLeftPanel` 에 `Debug.Log("hook OK")` 넣어 호출 확인 (검증 후 제거) |

---

## 3. 결정사항 요약 (plan 에서 가져옴)

| # | 항목 | 채택 |
|---|---|---|
| 1 | UI 프레임워크 | UI Toolkit (UXML/USS) |
| 2 | 메뉴 경로 | **`LostMemory/Balance Editor`** (기존 컨벤션, `Window/...` 아님) |
| 3 | 폴더 위치 | `Assets/_Project/Scripts/Editor/BalanceEditor/` |
| 4 | 네임스페이스 | `LostMemory.Editor.BalanceEditor` |
| 5 | minSize | 800×500 |
| 6 | titleContent | "Balance Editor" |
| 7 | UXML/USS 위치 | Editor 폴더 하위 `Resources/` (Player 빌드 자동 제외) |
| 8 | hook 방식 | `protected virtual` 메서드 (CL-163 override 진입점) |
| 9 | asmdef References | **비워둠** (Auto Referenced 만으로 Assembly-CSharp 자동 참조) |

---

## 4. 알아야 할 컨텍스트 (Gotchas)

### 4.1 asmdef References 가 비어 있는 이유

프로젝트에 기존 asmdef 자체가 없음 — 모든 스크립트가 기본 `Assembly-CSharp` 에 들어 있음. Editor asmdef 가 Assembly-CSharp 의 RelicData 등을 참조하려면:

- ❌ Assembly-CSharp 은 GUID 참조 불가 (다른 일반 어셈블리와 다름)
- ✅ Auto Referenced ON 으로 자동 참조 동작

본 CL-162 는 RelicData 등 미참조이므로 영향 無. CL-163 부터 데이터 참조 시 검증 필요.

### 4.2 메뉴 경로가 원본 plan 과 다른 이유

원본 plan §2 는 `Window/LostMemory/Balance Editor`. 코드베이스 탐색 결과 기존 컨벤션이 `LostMemory/...` (예: `LostMemory/Relics/...`, `LostMemory/Shop/...`) 로 통일돼 있어 **`LostMemory/Balance Editor`** 로 채택. ExitPlanMode 에서 사용자 승인 받음.

### 4.3 .meta 파일 자동 생성

3개 파일 (.cs/.uxml/.uss) 작성 후 .meta 는 아직 없음. Unity 가 프로젝트 오픈 시 자동 생성. 이 .meta 들이 git 에 추가되는지 확인 필요 (Unity 가 자동 import).

### 4.4 Resources 폴더 빌드 포함 위험

`Resources/` 는 일반적으로 빌드 시 모두 포함되지만, **Editor asmdef 폴더 하위에 두면 자동 제외**. 본 plan 은 `Editor/BalanceEditor/Resources/` 위치라 안전.

### 4.5 UI Toolkit 첫 도입

프로젝트 코드 자체엔 UXML/USS 사용 흔적 없음 (ThirdParty 외). `using UnityEngine.UIElements` namespace 가 자동 컴파일되는지 첫 검증 필요.

### 4.6 CL-138 의존성

CL-138 (RelicData 듀얼 태그/사이즈/다중 효과) 완료 상태. 본 CL-162 는 인프라만 — CL-138 산출물 미참조. CL-163 부터 활용.

### 4.7 메모리 가이드라인

- `.unity/.prefab/.meta` 직접 편집 금지 → Unity Editor 작업은 사용자
- 브랜치 스코프 엄격히 → 본 브랜치 `S14P31C201-401-cl-162-editorwindow-ui-toolkit` 외 작업 금지
- 커밋/push/MR 은 사용자 직접

---

## 5. 관련 파일 빠른 참조

### 본 CL 산출물

```text
LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/
  BalanceEditorWindow.cs                   ← Claude 작성
  Resources/
    BalanceEditorWindow.uxml               ← Claude 작성
    BalanceEditorWindow.uss                ← Claude 작성
  LostMemory.BalanceEditor.Editor.asmdef   ← 사용자 작성 예정 (Unity 에디터)
```

### 문서

```text
client/docs/khi/
  cl162_plan.md                            원본 plan (사용자 작성)
  cl162_phase_1_handoff_20260505.md        본 문서
```

### 참고 — 기존 Editor 코드 (네임스페이스 패턴 / 메뉴 컨벤션 참조)

```text
LostMemory/Assets/_Project/Scripts/Editor/
  BossHealthBarPrefabBuilder.cs            [MenuItem("LostMemory/UI/...")]
  Relics/RelicDataBatchGenerator.cs        namespace LostMemory.Editor.Relics
  Shop/ShopUIBuilder.cs                    [MenuItem("LostMemory/Shop/...")]
```

---

## 6. 후속 ticket 영향

| Ticket | CL-162 와의 관계 |
|---|---|
| CL-163 (트리뷰+디테일+검색) | `PopulateLeftPanel/RightPanel` hook 활용 |
| CL-164 (Dirty + Undo/Redo) | EditorWindow 의 SerializedObject 활용 |
| CL-165 (JSON Import/Export) | toolbar 에 버튼 추가 |
| CL-166 (데이터 카테고리 연결) | 트리뷰에 카테고리 노드 추가 |

---

## 7. 새 환경 검증 시작점

1. `git pull` 로 최신 변경 (본 환경에서 push 한 3개 파일 + 본 핸드오프) 가져오기
2. Unity 프로젝트 오픈 → 패키지 import 대기 → Console 에러 확인
3. `Assets/_Project/Scripts/Editor/BalanceEditor/` 폴더에 3개 파일 + Resources/ 가 있는지 확인
4. **§2.2 단계 3 (asmdef 생성)** 부터 진행
5. §2.2 단계 4 (검증 7 시나리오) 통과 후 알림
6. CL-163 진입 또는 다음 ticket 결정

---

## 8. 다음 핸드오프 작성 시점

다음 환경 전환 또는 장기간 휴지 시점에 *동일 양식* 으로 신규 doc 작성.

```text
client/docs/khi/cl162_phase_2_handoff_<YYYYMMDD>.md
```

본 문서는 *그 시점의 스냅샷* 이므로 갱신하지 않고 *새 문서* 로 누적.
