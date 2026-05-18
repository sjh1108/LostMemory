# CL-174 — InventoryTestWindow 셸 + Play 모드 가드

## Context

Epic U 의 인벤토리 테스트 도구 트랙 첫 ticket. 디자이너/테스터가 **던전 클리어 → 보상 추첨 흐름을 우회**해서 즉시 RelicData 를 추가/제거하고 효과/세트/스택을 검증할 별도 EditorWindow 를 신설.

본 CL = **셸 + 메뉴 등록 + Play 모드 가드 + Player 자동 검색 hook** 만. 트리/슬롯/동작은 후속 (CL-175 ~ CL-177).

**2점 P1, CL-166 의존.**

> ★ master plan epic_uv_20260506.md 에는 Epic U (CL-174~177 MVP) + Epic V (CL-182~187 확장) 둘 다 정식 ticket 으로 등록. CL-174 = 셸 + Play 모드 가드 (Epic U), CL-182 = 셸 + Player 자동 검색 (Epic V). 사용자 결정에 따라 본 plan (CL-174) 은 CL-182 정의를 흡수하여 **Player 자동 검색까지 셸에 포함** (§설계 3 참조). master plan 갱신 완료 (2026-05-06).

### 본 CL 책임 범위

| 포함 | 제외 |
|---|---|
| `InventoryTestWindow.cs` (EditorWindow 셸) | 좌측 RelicData 트리 (→ CL-175) |
| `InventoryTestWindow.uxml` / `.uss` | 우측 인벤토리 슬롯 표시 (→ CL-175) |
| 폴더 신설 (`Editor/InventoryTest/`) | 더블클릭 / 우클릭 / Clear (→ CL-176) |
| `[MenuItem("Tools/LostMemory/Inventory Test Window")]` | Consumable 4슬롯 영역 (→ CL-177) |
| Play 모드 가드 (Edit 모드 안내 문구) | 검색 / 드래그앤드롭 / Quick Add 등 확장 (→ Epic V 후속) |
| Player 자동 검색 hook (`FindAnyObjectByType`) | |

---

## 현황

### 1.1 BalanceEditor 셸 패턴 (CL-162) — 본 ticket 의 모방 대상

```
LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/
├─ BalanceEditorWindow.cs        (EditorWindow + [MenuItem] + CreateGUI)
├─ Resources/
│  ├─ BalanceEditorWindow.uxml   (Toolbar + Body[Left/Right] + StatusBar)
│  └─ BalanceEditorWindow.uss    (.be- 접두 클래스)
└─ LostMemory.BalanceEditor.Editor.asmdef  (references=[])
```

UXML 핵심 구조 (`BalanceEditorWindow.uxml`):
```xml
<ui:VisualElement class="be-root">
    <uie:Toolbar class="be-toolbar"> ... </uie:Toolbar>
    <ui:VisualElement class="be-body">
        <ui:VisualElement name="LeftPanel" />
        <ui:VisualElement name="RightPanel" />
    </ui:VisualElement>
    <ui:VisualElement class="be-statusbar"> ... </ui:VisualElement>
</ui:VisualElement>
```

로드: `Resources.Load<VisualTreeAsset>("BalanceEditorWindow")` ([BalanceEditorWindow.cs:107](../../LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/BalanceEditorWindow.cs:107))

### 1.2 인벤토리 Runtime 코드

| 파일 | namespace | 핵심 멤버 |
|---|---|---|
| `Runtime/Relics/PlayerRelicInventory.cs` | `LostMemory.Relics` | MonoBehaviour. `TryAdd(RelicData)` / `Remove(RelicData)` / `Clear()` / `MaxSlots` / `OwnedRelics` / 이벤트 `OnRelicAcquired` / `OnRelicRemoved` / `OnCleared` |
| `Runtime/Relics/PlayerConsumableInventory.cs` | `LostMemory.Relics` | MonoBehaviour. `SlotCount=4` / `TryAdd(RelicData)` / `Remove(int slot)` / `Slots` (IReadOnlyList) |
| `Runtime/Relics/RelicData.cs` | (RelicData SO) | `IsConsumable` 플래그로 유물/소모품 구분 |

### 1.3 ★ Runtime asmdef 미존재 — 결정적 제약

```
glob: LostMemory/Assets/_Project/Scripts/Runtime/**/*.asmdef → No files found
```

→ **Runtime 코드 전부 Assembly-CSharp 에 컴파일됨**. asmdef 가 있는 Editor 어셈블리는 Assembly-CSharp 직접 참조 불가 (Unity 제약). BalanceEditor 가 string filter `"t:..."` 로 우회한 이유.

본 ticket 은 `PlayerRelicInventory.TryAdd()` 같은 메서드를 직접 호출해야 함 → string filter 우회 불가능 → **asmdef 정책 결정 필요** (§설계 1).

### 1.4 상위 보상 흐름 (참조용 — 본 ticket 무관, 후속 ticket 에서 검증)

```
RewardController.cs:78-120 → RewardPanelView.cs:49 → _inventory.TryAdd(selected)
                                                     → BuildManager.cs:114 가 효과 적용
```

→ 본 도구도 같은 path (`TryAdd`) 호출 → BuildManager 효과 자동 적용. 테스트 정확도 보장.

---

## 설계 결정

### 1. ★ asmdef 정책 — 별도 asmdef vs 무 asmdef

master plan §11 권장은 별도 asmdef (`LostMemory.InventoryTest.Editor`). 하지만 §1.3 제약 (Runtime asmdef 미존재) 으로 재검토 필요.

| 안 | 장점 | 단점 |
|---|---|---|
| (a) **asmdef 없음 — Editor 폴더 규칙으로 자동 Assembly-CSharp-Editor** | Runtime 클래스 자유 참조, 추가 구성 0 | asmdef 분리 깔끔함 X |
| (b) Runtime 에 `LostMemory.Relics` asmdef 신설 + Editor asmdef 가 references | 깔끔한 분리 | 본 ticket 범위 외 영향 (Runtime asmdef 신설은 다른 도메인 코드 컴파일 영향 검증 필요. 회귀 위험) |
| (c) Reflection 으로 메서드 호출 | asmdef 분리 + 직접 참조 X | 코드 복잡, 런타임 오류 위험 |

→ **(a) 채택**. Unity 의 Editor 폴더 규칙으로 자동 분리 (게임 빌드 미포함). master plan §11 의 별도 asmdef 권장은 Runtime 어셈블리가 분리된 환경 전제. 현 환경에선 (a) 가 합리적. 추후 Runtime asmdef 가 도입되면 본 도구도 별도 asmdef 로 옮길 여지 남김 (namespace 는 미리 분리해 두면 이전 비용 작음).

### 2. Play 모드 가드 패턴

| 모드 | 동작 |
|---|---|
| Edit 모드 | 안내 문구 ("Play 모드 진입 시 인벤토리 테스트가 활성화됩니다.") 표시, BodyContainer.SetEnabled(false) |
| Play 모드 | 안내 문구 숨김, BodyContainer 활성화, `RefreshPlayerInstances()` 호출 |

전환: `EditorApplication.playModeStateChanged` 구독 → `EnteredPlayMode` / `EnteredEditMode` 시 `UpdateModeView()`.

### 3. Player 자동 검색 — 셸 단계 포함 결정

사용자 시트는 명시 안 함. master plan §8 마지막 권장: `FindAnyObjectByType<PlayerRelicInventory>()`.

→ **CL-174 셸에서 hook 까지 포함**. CL-175 가 트리/슬롯 표시 시작할 때 즉시 `_relicInv` / `_consumeInv` 사용 가능. Play 진입 시 자동 갱신. 단일 Player 가정 (멀티 디버깅 필요 시 후속 ticket 에서 드롭다운).

### 4. 폴더 / namespace

| 파일 | 경로 | namespace |
|---|---|---|
| `InventoryTestWindow.cs` | `LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/` | `LostMemory.Editor.InventoryTest` |
| `InventoryTestWindow.uxml` | `LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/Resources/` | — |
| `InventoryTestWindow.uss` | `LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/Resources/` | — |

> 폴더에 "Editor" 라는 이름이 들어가야 Unity Editor 폴더 규칙으로 인식됨. 상위 `Editor/` 가 이미 있으므로 그 안의 `InventoryTest/` 는 자동 Assembly-CSharp-Editor 에 컴파일.

### 5. UI 클래스 접두

BalanceEditor 가 `.be-` 사용. 본 도구는 **`.iv-` 접두** (Inventory test). 충돌 회피.

### 6. 미니멀 셸 — 후속 ticket 의 진입점만 마련

`BodyContainer` / `LeftPanel` / `RightPanel` 의 element name 안정. CL-175 이후 `root.Q<VisualElement>("LeftPanel")` 로 진입.

---

## 신규 파일 — 코드

### InventoryTestWindow.cs

```csharp
using LostMemory.Relics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LostMemory.Editor.InventoryTest
{
    /// <summary>
    /// Epic U 인벤토리 테스트 도구. Play 모드 전제 별도 EditorWindow.
    /// CL-174: 셸 + Play 모드 가드 + Player 자동 검색.
    /// CL-175~177: 트리/슬롯/동작 추가 예정.
    /// </summary>
    public class InventoryTestWindow : EditorWindow
    {
        private const string UxmlPath = "InventoryTestWindow";
        private const string UssPath  = "InventoryTestWindow";

        // CL-175 이후 사용 — 셸 단계에서 hook 만 마련
        private PlayerRelicInventory _relicInv;
        private PlayerConsumableInventory _consumeInv;

        private VisualElement _bodyContainer;
        private Label _modeHint;

        [MenuItem("Tools/LostMemory/Inventory Test Window")]
        public static void Open()
        {
            var window = GetWindow<InventoryTestWindow>();
            window.titleContent = new GUIContent("Inventory Test");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;

            var uxml = Resources.Load<VisualTreeAsset>(UxmlPath);
            if (uxml == null)
            {
                Debug.LogError($"[CL-174] UXML 미발견: Resources/{UxmlPath}");
                return;
            }
            uxml.CloneTree(root);

            var uss = Resources.Load<StyleSheet>(UssPath);
            if (uss != null) root.styleSheets.Add(uss);

            _bodyContainer = root.Q<VisualElement>("BodyContainer");
            _modeHint      = root.Q<Label>("ModeHint");

            UpdateModeView();
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode ||
                change == PlayModeStateChange.EnteredEditMode)
            {
                UpdateModeView();
            }
        }

        private void UpdateModeView()
        {
            bool playMode = EditorApplication.isPlaying;
            if (_modeHint != null)
                _modeHint.style.display = playMode ? DisplayStyle.None : DisplayStyle.Flex;
            if (_bodyContainer != null)
                _bodyContainer.SetEnabled(playMode);

            if (playMode) RefreshPlayerInstances();
        }

        private void RefreshPlayerInstances()
        {
            // 단일 Player 가정 (MVP). 멀티 디버깅 필요 시 후속 ticket 에서 드롭다운 추가.
            _relicInv   = Object.FindAnyObjectByType<PlayerRelicInventory>();
            _consumeInv = Object.FindAnyObjectByType<PlayerConsumableInventory>();
            // CL-175 이후: _relicInv / _consumeInv 사용해서 슬롯 표시
        }
    }
}
```

### InventoryTestWindow.uxml

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements">
    <ui:VisualElement class="iv-root">
        <uie:Toolbar class="iv-toolbar">
            <ui:Label text="Inventory Test" class="iv-title" />
        </uie:Toolbar>

        <ui:Label name="ModeHint"
                  text="Play 모드 진입 시 인벤토리 테스트가 활성화됩니다."
                  class="iv-mode-hint" />

        <ui:VisualElement name="BodyContainer" class="iv-body">
            <ui:VisualElement name="LeftPanel"  class="iv-left-panel" />
            <ui:VisualElement name="RightPanel" class="iv-right-panel" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

### InventoryTestWindow.uss

```css
.iv-root {
    flex-grow: 1;
    flex-direction: column;
    background-color: rgb(56, 56, 56);
}

.iv-toolbar { flex-shrink: 0; }

.iv-title {
    color: rgb(220, 220, 220);
    font-size: 13px;
    -unity-font-style: bold;
    margin-left: 4px;
    margin-right: 12px;
    -unity-text-align: middle-left;
}

.iv-mode-hint {
    color: rgb(200, 180, 100);
    font-size: 12px;
    -unity-font-style: italic;
    padding: 8px;
    flex-shrink: 0;
}

.iv-body {
    flex-grow: 1;
    flex-direction: row;
}

.iv-left-panel {
    min-width: 240px;
    background-color: rgb(48, 48, 48);
    border-right-width: 1px;
    border-right-color: rgb(20, 20, 20);
    padding: 4px;
}

.iv-right-panel {
    flex-grow: 1;
    background-color: rgb(56, 56, 56);
    padding: 8px;
}
```

---

## 위험 / 알려진 상황

| # | 위험 | 대응 |
|---|---|---|
| 1 | **Runtime 클래스 직접 참조** — InventoryTestWindow 가 PlayerRelicInventory 직접 사용. Runtime API 변경 시 영향 | asmdef 분리 X 결정 (§설계 1) 의 trade-off. 컴파일 오류 시 즉시 발견 |
| 2 | **단일 Player 가정** — `FindAnyObjectByType` 1개만 반환 | MVP. 멀티플레이/2명 디버깅 필요해지면 Player 드롭다운 후속 ticket |
| 3 | **Play 모드 도메인 리로드** — Play 진입 시 Editor 객체 재생성, `_relicInv` 참조 stale 위험 | `OnPlayModeChanged` → `RefreshPlayerInstances()` 매번 재검색. 단일 Player 가정이라 비용 무시 가능 |
| 4 | **Window 가 닫힌 채 Play 모드 진입** | Window 가 닫혀있으면 OnPlayModeChanged 구독 X (OnEnable 이 구독, OnDisable 이 해제). 다음 Open 시 CreateGUI → UpdateModeView 가 현 모드 반영. 정상 |
| 5 | **CL-175~177 후속 ticket 의 가정** — UXML element name (`BodyContainer` / `LeftPanel` / `RightPanel` / `ModeHint`) 안정성 | 본 ticket 컨벤션. 후속에서 변경 시 name 재확인 필요 |
| 6 | **CL-174 ↔ CL-182 ticket 정의 유사** — Epic U MVP 와 Epic V 정식. CL-174 가 Player 자동 검색을 흡수하여 차이 0 으로 통합 | master plan 에 두 ticket 모두 정식 등록. Epic V (CL-183~187) 의 의존 hub 역할 유지 |

---

## 검증 시나리오

### CS 빌드 검증 (Claude)

```
1. InventoryTestWindow.cs 컴파일 오류 없음
2. UXML/USS Resources.Load 경로 일치 확인
3. Editor 폴더 규칙으로 Assembly-CSharp-Editor 에 자동 분리됨 확인 (게임 빌드 미포함)
4. 다른 코드 영향 0 (PlayerRelicInventory 등 Runtime 무수정)
```

### Unity Editor 검증 (사용자)

```
5. Tools > LostMemory > Inventory Test Window 메뉴 표시
6. 메뉴 클릭 → 빈 EditorWindow 열림
7. Edit 모드 — 안내 문구 ("Play 모드 진입 시...") 표시 + BodyContainer 비활성 (회색)
8. Play 모드 진입 → 안내 문구 사라지고 BodyContainer 활성 (빈 Left/Right Panel)
9. Play 종료 → 안내 문구 재표시, BodyContainer 비활성
10. Window 가 닫힌 상태에서 Play 모드 진입 → 메뉴로 다시 열어도 정상 동작
11. (선택) Console 에 PlayerRelicInventory / PlayerConsumableInventory FindAnyObjectByType 결과 — null 이면 Player 가 씬에 없는 것 (정상; CL-175 부터 안내 표시 추가)
```

---

## 핵심 파일

### 신규 (Claude 작성)

| 경로 | 내용 |
|---|---|
| `LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/InventoryTestWindow.cs` | 셸 + Play 모드 가드 + Player 자동 검색 |
| `LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/Resources/InventoryTestWindow.uxml` | UI 구조 |
| `LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/Resources/InventoryTestWindow.uss` | 스타일 |

### 무수정 (참고)

| 파일 | 이유 |
|---|---|
| `Runtime/Relics/PlayerRelicInventory.cs` | Editor 도구가 호출만, 무수정 |
| `Runtime/Relics/PlayerConsumableInventory.cs` | 동일 |
| `Runtime/Relics/RelicData.cs` | 동일 |
| `BalanceEditorWindow.cs` 등 BalanceEditor 전체 | 별개 Window, 본 CL 무관 |

> 다른 사람 코드 침범 없음. 모든 신규 파일은 Claude 가 새로 작성. Runtime 코드는 호출만 하고 손대지 않음.

---

## 후속 ticket 영향

| Ticket | 관계 |
|---|---|
| **CL-175** 좌측 RelicData 트리 + 우측 슬롯 표시 | LeftPanel 에 트리, RightPanel 에 슬롯 시각화. 본 CL 의 `_relicInv` / `_consumeInv` 사용 |
| **CL-176** 더블클릭 / 우클릭 / Clear | `_relicInv.TryAdd` / `Remove` / `Clear` 호출. `OnRelicAcquired` 이벤트 구독으로 자동 갱신 |
| **CL-177** Consumable 4슬롯 영역 + 슬롯 지정 | `_consumeInv.TryAdd` / `Remove(int slot)` 호출 |
| **(미정 후속)** 검색 / 드래그앤드롭 / Quick Add 프리셋 | master plan epic_uv §8 의 확장 기능 (CL-186/187 옛 번호). 새 시트에서는 별도 ticket 또는 보류 |

---

## 작업 순서

| 순서 | 담당 | 내용 |
|---|---|---|
| 1 | (선결) | CL-173 머지 완료 확인 |
| 2 | Claude | 폴더 신설 (`Editor/InventoryTest/`, `Editor/InventoryTest/Resources/`) |
| 3 | Claude | `InventoryTestWindow.uxml` 작성 |
| 4 | Claude | `InventoryTestWindow.uss` 작성 |
| 5 | Claude | `InventoryTestWindow.cs` 작성 |
| 6 | Claude | 컴파일 오류 없음 확인 (Grep / Read) |
| 7 | 사용자 | Unity Editor 에서 §검증 5-11 실행 |
| 8 | 사용자 | MR 생성 (커밋/push 사용자 직접) |
| 9 | — | CL-175 (트리 + 슬롯 표시) 진입 |

---

## 예상 시간

| 단계 | 시간 |
|---|---|
| UXML 작성 | 10분 |
| USS 작성 | 10분 |
| InventoryTestWindow.cs 작성 | 25분 |
| 컴파일 / 검증 (Claude) | 10분 |
| Unity Editor 검증 (사용자) | 10분 |
| **합계** | **약 65분** |

→ 2점 ticket 표준 분량. Editor 인프라 신설 (폴더+UXML+USS+CS 4종 산출물) 이라 cl173 (1점, 35분) 보다 시간 약간 더 소요.

---

## 메모

- master plan epic_uv §1.3 / §11 의 "별도 asmdef" 권장과 본 plan §설계 1 의 "asmdef 없음" 결정이 다름 — 근거: Runtime asmdef 미존재 (§1.3). 마스터 plan 갱신 시 본 결정 반영 권장
- master plan epic_uv ticket 시트는 Epic U (CL-174~177) + Epic V (CL-182~187) 모두 정식 등록 완료 (2026-05-06). 다른 ticket 옛 번호 (CL-180→CL-178, CL-188~190→CL-179~181) 본문 일괄 갱신 완료
- Runtime asmdef 신설은 별도 큰 ticket. 본 plan 범위 외
