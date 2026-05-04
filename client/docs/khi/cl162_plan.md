# CL-162 — EditorWindow 셸 + UI Toolkit (UXML/USS) 구조

## Context

Epic U (밸런스 에디터)의 첫 ticket. **디자이너 친화 단일 EditorWindow 의 인프라 셸**.

3점 P1, CL-138 의존.

회의록 (tickets-master §Epic U):
> 포폴 어필용. UI Toolkit 기반 단일 EditorWindow + 좌측 카테고리 트리 + 우측 디테일.

### Epic U 전체 구조 (참고)

| Ticket | 책임 |
|---|---|
| **CL-162 (본 CL)** | EditorWindow 셸 + UXML/USS 구조 (빈 레이아웃) |
| CL-163 | 좌측 트리뷰 + 우측 디테일 + 검색 (실제 데이터 표시) |
| CL-164 | Dirty 마커 + Undo/Redo |
| CL-165 | JSON Import/Export + Auto-save |
| CL-166 | 데이터 카테고리 연결 (빌드/무기/적/상점 SO 통합) |

→ **본 CL 은 인프라만**. 실제 데이터 표시 / 편집은 CL-163 부터.

### 본 CL 책임 범위

1. **EditorWindow 클래스** — 메뉴 등록 + 윈도우 띄우기
2. **UXML 레이아웃** — 빈 좌측/우측 패널 구조 (placeholder)
3. **USS 스타일시트** — 기본 색상/폰트/spacing
4. **에셈블리 정의** — Editor 전용 폴더 / asmdef
5. **빌드 안전성** — Player 빌드에 포함 X 보장

→ 코드 자체는 작지만 **UI Toolkit 학습 곡선** + asmdef 분리가 핵심 도전.

---

## 결정사항

### 1. UI 프레임워크 — UI Toolkit (UXML/USS) ⭐

**옵션**:
- (a) IMGUI (구식 GUILayout) — 빠르지만 UX 떨어짐
- (b) **UI Toolkit (UXML/USS)** ⭐ — Unity 권장 + 회의록 명시
- (c) Custom Inspector 만 — 단일 윈도우 X

**채택: (b)**.
- 회의록 명시
- Unity 6 정식 지원 + 디자이너 친화
- UXML/USS 분리로 레이아웃과 로직 분리
- Editor / Runtime 모두 지원 (향후 인게임 디버그 메뉴 재활용 가능)

### 2. 윈도우 메뉴 위치

```
Window > LostMemory > Balance Editor
```

Unity 표준 패턴 (`Window/...` 메뉴).

### 3. 윈도우 도킹 정책

- 도킹 가능 (다른 Inspector 옆)
- 최소 사이즈: 800 × 500
- 디폴트 사이즈: 1200 × 700
- 윈도우 타이틀: "Balance Editor"

### 4. 폴더 구조 + asmdef

```
Assets/_Project/Scripts/Editor/BalanceEditor/
  LostMemory.BalanceEditor.Editor.asmdef        ← Editor 전용 asmdef
  BalanceEditorWindow.cs                         ← EditorWindow 클래스
  Resources/
    BalanceEditorWindow.uxml                    ← 레이아웃
    BalanceEditorWindow.uss                     ← 스타일
```

`asmdef` 설정:
- `includePlatforms`: Editor 만
- `references`: LostMemory.Runtime (RelicData / WeaponData / etc 참조)
- `autoReferenced`: true

→ Player 빌드 자동 제외. asmdef 누락 시 빌드 에러.

### 5. UXML 레이아웃 — 좌측 / 우측 2 패널

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement class="be-root">
        <ui:VisualElement class="be-toolbar">
            <ui:Label text="Balance Editor" class="be-title" />
            <!-- CL-163: 검색 박스 등 -->
        </ui:VisualElement>

        <ui:VisualElement class="be-body">
            <ui:VisualElement class="be-left-panel" name="LeftPanel">
                <ui:Label text="(카테고리 트리 — CL-163)" class="be-placeholder" />
            </ui:VisualElement>

            <ui:VisualElement class="be-splitter" />

            <ui:VisualElement class="be-right-panel" name="RightPanel">
                <ui:Label text="(상세 편집 패널 — CL-163)" class="be-placeholder" />
            </ui:VisualElement>
        </ui:VisualElement>

        <ui:VisualElement class="be-statusbar">
            <ui:Label text="Ready" name="StatusLabel" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

- `LeftPanel` / `RightPanel` 이름 attribute 부여 — CL-163 에서 코드로 접근.
- 좌측 너비 30% (CL-163 에서 splitter 동작 추가).

### 6. USS 스타일 — 기본만

```css
.be-root {
    flex-grow: 1;
    flex-direction: column;
    background-color: rgb(56, 56, 56);
}

.be-toolbar {
    flex-direction: row;
    align-items: center;
    height: 30px;
    background-color: rgb(40, 40, 40);
    border-bottom-width: 1px;
    border-bottom-color: rgb(20, 20, 20);
    padding-left: 8px;
}

.be-title {
    color: white;
    font-size: 14px;
    -unity-font-style: bold;
}

.be-body {
    flex-grow: 1;
    flex-direction: row;
}

.be-left-panel {
    width: 30%;
    min-width: 200px;
    background-color: rgb(48, 48, 48);
    border-right-width: 1px;
    border-right-color: rgb(20, 20, 20);
    padding: 8px;
}

.be-right-panel {
    flex-grow: 1;
    background-color: rgb(56, 56, 56);
    padding: 8px;
}

.be-splitter {
    width: 1px;
    background-color: rgb(30, 30, 30);
}

.be-statusbar {
    height: 22px;
    flex-direction: row;
    align-items: center;
    padding-left: 8px;
    background-color: rgb(40, 40, 40);
    border-top-width: 1px;
    border-top-color: rgb(20, 20, 20);
}

.be-placeholder {
    color: rgb(150, 150, 150);
    -unity-font-style: italic;
}
```

→ Unity 표준 다크 테마 색상.

### 7. EditorWindow 클래스 — 최소 골격

```csharp
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LostMemory.BalanceEditor.Editor
{
    public class BalanceEditorWindow : EditorWindow
    {
        [MenuItem("Window/LostMemory/Balance Editor")]
        public static void Open()
        {
            var window = GetWindow<BalanceEditorWindow>();
            window.titleContent = new GUIContent("Balance Editor");
            window.minSize = new Vector2(800, 500);
            window.Show();
        }

        private const string UxmlPath = "BalanceEditorWindow";   // Resources/ 하위
        private const string UssPath = "BalanceEditorWindow";

        public void CreateGUI()
        {
            var root = rootVisualElement;

            var uxml = Resources.Load<VisualTreeAsset>(UxmlPath);
            if (uxml == null)
            {
                Debug.LogError($"[CL-162] UXML 미발견: {UxmlPath}");
                return;
            }
            uxml.CloneTree(root);

            var uss = Resources.Load<StyleSheet>(UssPath);
            if (uss != null) root.styleSheets.Add(uss);
        }
    }
}
```

→ `CreateGUI` 가 UI Toolkit 진입점 (UnityEditor 5+).

### 8. 리소스 위치 — Editor/Resources 하위

UXML/USS 를 `Editor/BalanceEditor/Resources/` 에 두면 Editor 전용 리소스로 처리. Player 빌드 자동 제외.

대안: `EditorGUIUtility.Load(...)` 사용. 본 plan: **Resources.Load** (단순, 호환).

→ 단, `Resources/` 폴더는 빌드 시 모두 포함되는 위험 존재. 본 CL 은 Editor asmdef 폴더 안에 두어 안전.

### 9. 다국어 — 미적용 (MVP 한국어 + 영어 혼용)

본 CL 텍스트:
- 메뉴: 영어 "Balance Editor"
- 윈도우 타이틀: 영어
- placeholder: 한국어 "카테고리 트리"
- 향후 다국어 지원 별도 ticket.

### 10. 향후 확장 hook (CL-163~166 입구)

`BalanceEditorWindow` 에 protected 가상 메서드:
```csharp
// CL-163 에서 override
protected virtual void PopulateLeftPanel(VisualElement panel) { }
protected virtual void PopulateRightPanel(VisualElement panel) { }
```

→ CL-163 이 자식 클래스 또는 partial 로 확장. 본 CL 은 메서드 호출만 (CreateGUI 마지막에).

```csharp
public void CreateGUI()
{
    // ... uxml 로드 ...
    PopulateLeftPanel(root.Q<VisualElement>("LeftPanel"));
    PopulateRightPanel(root.Q<VisualElement>("RightPanel"));
}
```

---

## 핵심 파일

### 신규

| 경로 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Editor/BalanceEditor/LostMemory.BalanceEditor.Editor.asmdef` | Editor 전용 asmdef |
| `Assets/_Project/Scripts/Editor/BalanceEditor/BalanceEditorWindow.cs` | EditorWindow 클래스 |
| `Assets/_Project/Scripts/Editor/BalanceEditor/Resources/BalanceEditorWindow.uxml` | UXML 레이아웃 |
| `Assets/_Project/Scripts/Editor/BalanceEditor/Resources/BalanceEditorWindow.uss` | USS 스타일 |

### 수정

(없음 — 본 CL 은 신규 생성만)

---

## 구현 단계

### 1단계: asmdef 생성 (15분)

1. 폴더 생성: `Assets/_Project/Scripts/Editor/BalanceEditor/`
2. `Create > Assembly Definition` → `LostMemory.BalanceEditor.Editor.asmdef`
3. 설정:
   - Name: `LostMemory.BalanceEditor.Editor`
   - Auto Referenced: ON
   - Include Platforms: **Editor 만 체크**
   - Assembly Definition References: **LostMemory.Runtime** (또는 RelicData 어셈블리명)
4. Resources 하위 폴더 생성

### 2단계: BalanceEditorWindow.cs (45분)

§7 코드 그대로:
- MenuItem `Window/LostMemory/Balance Editor`
- minSize 설정
- CreateGUI 에서 UXML/USS 로드
- PopulateLeftPanel / PopulateRightPanel 가상 메서드 (CL-163 hook)

### 3단계: BalanceEditorWindow.uxml (30분)

§5 코드. VisualElement 계층:
- be-root
  - be-toolbar (title + 향후 검색)
  - be-body (좌측 + splitter + 우측)
  - be-statusbar

`name="LeftPanel"` / `name="RightPanel"` 명시 (코드 접근용).

### 4단계: BalanceEditorWindow.uss (45분)

§6 코드. 다크 테마 색상 + spacing.

### 5단계: 검증 (45분)

```
시나리오 1: 메뉴 등록
- Unity 메뉴 Window > LostMemory > Balance Editor 노출
- 클릭 → 윈도우 열림

시나리오 2: 윈도우 레이아웃
- 좌측 패널 30%, 우측 70%
- 좌측에 placeholder 텍스트 "카테고리 트리"
- 우측에 placeholder "상세 편집 패널"
- 상단 "Balance Editor" 타이틀
- 하단 "Ready" status bar

시나리오 3: 윈도우 리사이즈
- 작게 줄여도 minSize (800×500) 유지
- 다른 윈도우 옆 도킹 가능

시나리오 4: 다크 테마
- 배경 색상 다크 (rgb 56)
- 좌측 패널 살짝 진함 (rgb 48)

시나리오 5: 어셈블리 분리
- Player 빌드 시 BalanceEditor 폴더 미포함 확인 (Build Report 또는 빌드 로그)
- Editor 만 컴파일

시나리오 6: 윈도우 재오픈
- 닫고 다시 열어도 정상 작동
- 도메인 리로드 후 자동 재생성

시나리오 7: 향후 hook 검증
- PopulateLeftPanel / PopulateRightPanel 호출 로그 (Debug.Log 임시)
- CL-163 이 override 가능한지 확인
```

---

## 위험 / 결정 미정

### 위험

1. **UI Toolkit 학습 곡선**: 팀이 IMGUI 익숙하면 UXML/USS 학습 부담. → 본 plan 의 코드를 그대로 시작점으로.
2. **Resources 폴더 빌드 포함 위험**: `Resources/` 는 빌드 시 모두 포함. Editor 폴더 하위에 두면 자동 제외 — 본 plan §8 명시. 잘못된 위치 두면 Player 빌드에 UXML 포함됨.
3. **asmdef 참조 누락**: RelicData / WeaponData 참조 못하면 컴파일 에러. asmdef 의 References 에 LostMemory.Runtime (또는 정확한 어셈블리명) 추가 필수. → 기존 Runtime asmdef 명 확인 필요.
4. **Unity 6 UI Toolkit API 변경 가능성**: `CreateGUI` 가 권장 진입점이지만 기존 OnEnable + rootVisualElement 도 작동. → CreateGUI 권장 (Unity 5.0+).
5. **MenuItem 충돌**: 다른 ticket 의 메뉴와 경로 충돌? → `Window/LostMemory/...` 로 namespace 화. 안전.
6. **윈도우 도킹 상태 영속성**: 사용자가 도킹한 위치가 다음 Unity 세션에서 복원? → EditorWindow 기본 동작이 처리. 추가 작업 X.
7. **CL-103 (디자이너 툴 폴리시)와 중복 가능**: tickets-master 에 명시된 위험. CL-103 이 별도로 Custom Inspector 작업. 본 CL 은 단일 EditorWindow 라 책임 분리. → CL-103 작업 시 통합 검토.

### 결정 미정

- [ ] UI 프레임워크 — 본 plan: **UI Toolkit (회의록 명시)**
- [ ] 메뉴 위치 — 본 plan: **Window/LostMemory/Balance Editor**
- [ ] 다국어 — 본 plan: **MVP 한국어+영어 혼용, 별도 ticket**
- [ ] CL-103 통합 — 본 plan: **별도 ticket 으로 통합 검토**
- [ ] CreateGUI vs OnEnable — 본 plan: **CreateGUI** (권장)

---

## 후속 ticket 영향

| Ticket | CL-162 와의 관계 |
|---|---|
| **CL-163 (트리뷰 + 디테일)** | PopulateLeftPanel/RightPanel hook 활용. 본 CL 의 빈 패널 채움 |
| **CL-164 (Dirty + Undo)** | EditorWindow 의 SerializedObject 활용 |
| **CL-165 (JSON Import/Export)** | toolbar 에 버튼 추가 |
| **CL-166 (데이터 카테고리 연결)** | 트리뷰에 카테고리 노드 추가 |
| **별도 ticket: CL-103 통합 검토** | Custom Inspector vs 단일 윈도우 책임 분리 |
| **별도 ticket: 다국어** | UXML 텍스트 다국어화 |
| **별도 ticket: 인게임 디버그 메뉴 재활용** | UI Toolkit 코드 Player 에서도 사용 |

---

## 예상 시간

| 단계 | 시간 |
|---|---|
| 1단계 (asmdef) | 15분 |
| 2단계 (BalanceEditorWindow.cs) | 45분 |
| 3단계 (UXML) | 30분 |
| 4단계 (USS) | 45분 |
| 5단계 (검증 7 시나리오) | 45분 |
| **합계** | **약 3시간** |

→ 3점 ticket 에 부합.

---

## 결정 요청

| # | 질문 | 옵션 | 추천 |
|---|---|---|---|
| 1 | UI 프레임워크 | **UI Toolkit** / IMGUI | **UI Toolkit** (회의록) |
| 2 | 메뉴 위치 | **Window/LostMemory/...** / Tools/... | **Window** (표준) |
| 3 | 디폴트 사이즈 | **1200×700** / 1024×600 | **1200×700** |
| 4 | UXML/USS 위치 | **Editor/Resources** / 별도 | **Editor/Resources** (안전) |
| 5 | CL-163 hook 방식 | **virtual 메서드** / 이벤트 | **virtual** (단순) |
| 6 | CL-103 통합 | 본 CL 통합 / **별도 ticket** | **별도** |

전부 추천대로면 **UI Toolkit + Window + 1200×700 + Editor/Resources + virtual + 별도**.

---

## Epic U 진행률 (CL-162 후)

| Ticket | Plan |
|---|---|
| **CL-162 EditorWindow 셸** | ✅ ← 방금 |
| CL-163 트리뷰 + 디테일 + 검색 | ⏳ |
| CL-164 Dirty + Undo/Redo | ⏳ |
| CL-165 JSON Import/Export | ⏳ |
| CL-166 데이터 카테고리 연결 | ⏳ |

**Epic U: 1/5**

---

## 다음 plan

| 옵션 | Ticket | 점수 | 비고 |
|---|---|---|---|
| **A** | CL-163 트리뷰 + 디테일 + 검색 | 5점 | Epic U 핵심 본체. 본 CL hook 활용 |
| B | Epic 외 ticket | - | - |

**추천: A (CL-163)** — Epic U 의 본체. 본 CL 의 빈 패널을 실제 데이터 표시로 채움. 5점이라 가장 무거운 Epic U ticket.

뭐로 갈까요?
