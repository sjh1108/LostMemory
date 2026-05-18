# CL-183 — 좌측 RelicData 트리 + 우측 슬롯 표시 + 검색 (이름/태그/효과)

## Context

Epic V (인벤토리 테스트 도구) 의 **첫 실질 코드 ticket**. ticket 시트의 정의는 "좌측 RelicData 트리 + 우측 슬롯 표시 + 검색 (이름/태그/효과)" 이지만, **트리/슬롯 부분은 CL-175 가 이미 머지** (`b0c40ee57`, Closes S14P31C201-410). 따라서 본 CL 의 신규 책임 = **검색 (이름/태그/효과)** 만.

CL-182 가 CL-174 흡수로 패스됐듯, CL-183 도 시트상 책임 중 일부 (트리/슬롯) 가 Epic U Inventory MVP 에서 이미 머지된 상태. 차이는 CL-182 가 100% 흡수였던 데 반해, CL-183 은 **검색 부분만 신규**.

3점 P1. 시트상 의존 = CL-174, **실질 의존 = CL-175 머지 완료**.

### 본 CL 책임 범위

| 포함 | 제외 |
|---|---|
| Toolbar `ToolbarSearchField` 신규 (UXML/USS) | 트리 / 슬롯 시각 변경 (CL-175 결과 그대로) |
| 검색 콜백 + 필터 메서드 (`BuildTreeData(filter)`) | 우측 슬롯 변경 / Player 인스턴스 갱신 흐름 |
| 매칭 채널 4개: DisplayName / EffectDescription / TagPrimary·TagSecondary / Effects[].Type | Quick Add 프리셋 (CL-187) |
| 빈 필터 = 전체 표시 + 카테고리 펼침 유지 | 드래그앤드롭 (CL-186) |
| 빈 카테고리 (매칭 0) 자동 숨김 | 검색 결과 하이라이트 / 강조 |
| 검색어 클리어 (`ToolbarSearchField` 기본 X 버튼) | 다중 토큰 / 정규식 / 대소문자 옵션 (정책 후속) |

---

## 현황 (탐색)

### 1.1 BalanceEditor FilterTree 패턴 (선례)

[BalanceEditorWindow.cs:148-152](../../client/LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/BalanceEditorWindow.cs) 에서 SearchField 등록:

```csharp
_searchField = root.Q<ToolbarSearchField>("SearchField");
if (_searchField != null)
{
    _searchField.RegisterValueChangedCallback(evt => RebuildTree(evt.newValue));
}
```

[BalanceEditorWindow.cs:508-537](../../client/LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/BalanceEditorWindow.cs) 의 `FilterTree`:
- `filter.ToLowerInvariant()` + `Contains` 단순 부분 매칭
- 카테고리(루트)별 children 순회 → 매칭 leaf 만 새 `TreeViewItemData<TreeNode>(newId++, ...)` 으로 복사
- `matched.Count == 0` 이면 카테고리 자체 skip (빈 카테고리 숨김)
- newId 100000 부터 — 원본 id 와 충돌 방지

### 1.2 InventoryTestWindow 현재 구조 (CL-174~177 위)

[InventoryTestWindow.cs](../../client/LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/InventoryTestWindow.cs)

| 멤버/메서드 | 역할 |
|---|---|
| `_allRelics` (line 38) | OrderBy(DisplayName) 정렬 캐시. BuildTreeData 에서 재사용 |
| `_treeView` (line 33) | TreeView 인스턴스 |
| `BuildLeftTree(leftPanel)` (line 129-153) | TreeView 생성 + makeItem/bindItem + selection/itemsChosen + SetRootItems(BuildTreeData()) + ExpandAll |
| `BuildTreeData()` (line 166-176) | Permanent / Consumable 그룹 생성 → `BuildGroup` 호출 |
| `BuildGroup(ref id, name, sos)` (line 178-195) | 그룹 leaf 생성 + 카운트 라벨 |
| `OnRefreshClicked()` (line 363-370) | `_allRelics = LoadAllRelics()` + tree rebuild — 본 CL 은 여기에 filter 인자 추가 필요 |
| `RefreshPlayerInstances()` (line 118-125) | Player 인스턴스 재검색 — 검색과 무관, 무수정 |

### 1.3 현재 toolbar UXML 구조

[InventoryTestWindow.uxml:3-9](../../client/LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/Resources/InventoryTestWindow.uxml)

```xml
<uie:Toolbar class="iv-toolbar">
    <ui:Label text="Inventory Test" class="iv-title" />
    <ui:VisualElement class="iv-toolbar-spacer" />
    <uie:ToolbarButton name="ClearPermanentButton"  ... />
    <uie:ToolbarButton name="ClearConsumableButton" ... />
    <uie:ToolbarButton name="RefreshButton" ... />
</uie:Toolbar>
```

→ Title 직후 / Spacer 직전에 SearchField 삽입 시 좌측 정렬 (검색은 primary 액션 강조). Spacer 가 우측 버튼 군을 그대로 우측으로 밀어줌.

### 1.4 RelicData 검색 가능 필드

[RelicData.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Relics/RelicData.cs)

| 필드 / 프로퍼티 | line | 검색 매핑 |
|---|---|---|
| `DisplayName` (string) | 21, 71 | "이름" — 디자이너 입력 표시명 |
| `EffectDescription` (string, TextArea) | 49, 79 | "효과" — 사용자 표시 텍스트 |
| `TagPrimary` (RelicTag enum) | 37, 84 | "태그" 1차 |
| `TagSecondary` (RelicTag enum) | 40, 85 | "태그" 2차 |
| `Effects` (`IReadOnlyList<EffectEntry>`) | 55, 87 | "효과" — `EffectEntry.Type` (RelicEffectType enum) |

⚠️ **`_effects` 가 null 가능성**: CL-138 legacy 단일 효과 fallback (`_effectTypeLegacy`) 으로 운영되는 SO 가 있을 수 있음. `Effects?.Any(...)` 또는 `Effects != null && Effects.Count > 0` 가드 필수.

⚠️ **`Effects[i].Type` 외 Magnitude/Duration/Threshold 는 검색 제외**: 숫자 검색은 디자이너 직관성 떨어짐 + 매핑 모호.

⚠️ **Legacy `_effectTypeLegacy` 는 검색 X**: `[Obsolete]` 표기 API. `Effects[]` 가 비어있으면 fallback 으로 매칭하지 않음 — 디자이너에게 "Effects 배열 채워" 압박 효과. 향후 마이그레이션 후 legacy 필드 제거 시 본 결정 영향 X.

### 1.5 본 CL 에서 손대는 코드의 작성자

| 파일 | 작성자 / 작업 |
|---|---|
| `InventoryTestWindow.cs` | **김회인 본인** (CL-174 / CL-175 / CL-176 WIP). 메서드 추가 + BuildTreeData 시그니처 변경 |
| `InventoryTestWindow.uxml` | 본인 (CL-174). 1줄 추가 |
| `InventoryTestWindow.uss` | 본인 (CL-174). 1 셀렉터 추가 |
| `RelicData.cs` | 본인 (CL-138). **무수정** — 읽기만 |

→ 협업 0 / 침범 0.

---

## 설계 결정

### 1. 검색 매칭 채널 = 4개 OR 매칭

```
filter.ToLowerInvariant() 가 다음 중 하나라도 Contains 매칭:
  ① relic.DisplayName
  ② relic.EffectDescription
  ③ relic.TagPrimary.ToString() / TagSecondary.ToString()
  ④ relic.Effects?[*].Type.ToString()
```

- 이름 / 효과설명 / 1차·2차태그 / 효과타입 — 4개 채널 OR
- BalanceEditor 의 단일 채널 (DisplayName 만) 보다 풍부 — 디자이너 요청 (epic_uv §9 결정 #1)

### 2. 비교 정규화 = `ToLowerInvariant().Contains`

BalanceEditor 일관. 다중 토큰 / 정규식 / Whitespace 분리 X — 단순성 우선. 향후 폴리시 ticket 으로 분리 가능.

### 3. Tag 검색 = enum 이름 문자열

`_tagPrimary.ToString()` 이 enum 이름 ("Strength", "None", ...) 반환. 디자이너가 "strength" 입력 → 매칭. 부수효과: "none" 입력 시 None 태그 항목 노출 — 비용 < 가치, 별도 스킵 로직 추가 X.

### 4. Effects null 가드

```csharp
relic.Effects != null && relic.Effects.Count > 0
    && relic.Effects.Any(e => e.Type.ToString().ToLowerInvariant().Contains(lower))
```

`IReadOnlyList<EffectEntry>` 라 LINQ Any 가능. legacy fallback (`_effectTypeLegacy`) 매칭 X (§1.4 정책).

### 5. 빈 필터 = 전체 표시

`string.IsNullOrEmpty(filter)` 면 필터 우회 → 기존 BuildTreeData 로직 그대로 (Permanent / Consumable 카테고리 모두 표시).

### 6. 빈 카테고리 자동 숨김

BalanceEditor 패턴. 매칭 leaf 가 0 인 카테고리는 root 에서 제외. Permanent/Consumable 한쪽만 매칭이면 한쪽만 표시.

### 7. SearchField UXML 위치 = Title 직후, Spacer 직전

```xml
<ui:Label text="Inventory Test" class="iv-title" />
<uie:ToolbarSearchField name="SearchField" class="iv-search-field" />  <!-- 신규 -->
<ui:VisualElement class="iv-toolbar-spacer" />
<uie:ToolbarButton name="ClearPermanentButton" ... />
...
```

→ Spacer 가 그대로 우측 버튼 군을 우측 정렬. SearchField 가 좌측에서 검색 = primary 액션 강조.

### 8. `_allRelics` 캐시 재사용

`LoadAllRelics()` 가 이미 OrderBy 정렬한 캐시 (line 155-164). BuildTreeData(filter) 가 매 키 입력마다 LINQ 필터링 — 77개 SO 기준 부담 X. 별도 인덱스 / Trie 불요.

### 9. 트리 자동 펼침 유지

`_treeView.ExpandAll()` 그대로. 필터 적용 후에도 매칭 leaf 즉시 노출 — BalanceEditor 일관.

### 10. 검색어 클리어

`ToolbarSearchField` 의 기본 X 버튼이 자동 제공. 클릭 시 `evt.newValue == ""` → 빈 필터 분기로 전체 복원. 추가 코드 X.

### 11. Refresh 버튼과의 상호작용

`OnRefreshClicked()` 가 `_allRelics` 재로드 + 트리 재구축. 본 CL 에서 **현재 검색 필터 보존** — `OnRefreshClicked` 가 `_lastFilter` 멤버를 사용하도록 확장 (BalanceEditor `_lastFilter` 패턴 동일).

### 12. namespace / 파일 위치

기존 그대로:
- `LostMemory.Editor.InventoryTest` namespace
- `Editor/InventoryTest/` + `Resources/`

신규 클래스 X (모두 `InventoryTestWindow` 멤버/메서드 추가).

---

## 신규 / 수정 파일 — 코드

### A. `InventoryTestWindow.cs` 추가/변경

```csharp
using UnityEditor.UIElements;  // ToolbarSearchField (이미 다른 using 으로 들어왔다면 무수정)

public class InventoryTestWindow : EditorWindow
{
    // 신규 멤버
    private ToolbarSearchField _searchField;
    private string _lastFilter = string.Empty;

    public void CreateGUI()
    {
        // ... 기존 코드 그대로 ...

        // 신규: SearchField 등록 (BuildLeftTree 호출 직전)
        _searchField = root.Q<ToolbarSearchField>("SearchField");
        if (_searchField != null)
        {
            _searchField.RegisterValueChangedCallback(evt =>
            {
                _lastFilter = evt.newValue ?? string.Empty;
                RebuildTreeWithFilter();
            });
        }

        BuildLeftTree(leftPanel);
        UpdateModeView();
    }

    // 변경: BuildTreeData 시그니처 + filter 분기
    private List<TreeViewItemData<InventoryTreeNode>> BuildTreeData()
    {
        return BuildTreeData(_lastFilter);
    }

    private List<TreeViewItemData<InventoryTreeNode>> BuildTreeData(string filter)
    {
        var permanents  = _allRelics.Where(r => !r.IsConsumable).ToList();
        var consumables = _allRelics.Where(r =>  r.IsConsumable).ToList();

        if (!string.IsNullOrEmpty(filter))
        {
            var lower = filter.ToLowerInvariant();
            permanents  = permanents.Where(r => MatchesFilter(r, lower)).ToList();
            consumables = consumables.Where(r => MatchesFilter(r, lower)).ToList();
        }

        int id = 0;
        var roots = new List<TreeViewItemData<InventoryTreeNode>>();
        if (permanents.Count > 0)  roots.Add(BuildGroup(ref id, "Permanent", permanents));
        if (consumables.Count > 0) roots.Add(BuildGroup(ref id, "Consumable", consumables));
        return roots;
    }

    // 신규: 4개 채널 OR 매칭
    private static bool MatchesFilter(RelicData r, string lower)
    {
        if ((r.DisplayName ?? r.name).ToLowerInvariant().Contains(lower)) return true;
        if (!string.IsNullOrEmpty(r.EffectDescription)
            && r.EffectDescription.ToLowerInvariant().Contains(lower)) return true;
        if (r.TagPrimary.ToString().ToLowerInvariant().Contains(lower))   return true;
        if (r.TagSecondary.ToString().ToLowerInvariant().Contains(lower)) return true;
        if (r.Effects != null && r.Effects.Count > 0)
        {
            foreach (var e in r.Effects)
                if (e.Type.ToString().ToLowerInvariant().Contains(lower)) return true;
        }
        return false;
    }

    // 신규: 필터 적용 트리 재구축
    private void RebuildTreeWithFilter()
    {
        if (_treeView == null) return;
        _treeView.SetRootItems(BuildTreeData(_lastFilter));
        _treeView.Rebuild();
        _treeView.ExpandAll();
    }

    // 변경: OnRefreshClicked — _lastFilter 보존
    private void OnRefreshClicked()
    {
        _allRelics = LoadAllRelics();
        RebuildTreeWithFilter();
        RefreshInventoryView();
    }
}
```

> `BuildLeftTree` 의 `_treeView.SetRootItems(BuildTreeData())` 줄은 그대로 — `BuildTreeData()` (인자 없는 버전) 가 `_lastFilter` 를 전달하는 wrapper.

### B. `InventoryTestWindow.uxml` 추가 (1줄)

기존 (line 3-9) →

```xml
<uie:Toolbar class="iv-toolbar">
    <ui:Label text="Inventory Test" class="iv-title" />
    <uie:ToolbarSearchField name="SearchField" class="iv-search-field" />  <!-- 신규 -->
    <ui:VisualElement class="iv-toolbar-spacer" />
    <uie:ToolbarButton name="ClearPermanentButton"  ... />
    <uie:ToolbarButton name="ClearConsumableButton" ... />
    <uie:ToolbarButton name="RefreshButton" ... />
</uie:Toolbar>
```

### C. `InventoryTestWindow.uss` 추가 (1 셀렉터)

```css
.iv-search-field {
    flex-shrink: 0;
    flex-grow: 0;
    width: 200px;
    margin-right: 8px;
}
```

→ 너비 고정 (200px), Spacer 가 우측 버튼 군을 그대로 우측 정렬.

---

## 위험 / 알려진 상황

| # | 위험 | 대응 |
|---|---|---|
| 1 | **`Effects` null SO 존재 가능성** (CL-138 legacy fallback) | `r.Effects != null && r.Effects.Count > 0` 가드. legacy 매칭은 정책상 X (§1.4) |
| 2 | **`DisplayName` null 인 SO** | `r.DisplayName ?? r.name` fallback. Generated 폴더 SO 는 모두 DisplayName 채워져 있으나 안전망 |
| 3 | **`EffectDescription` null** | `string.IsNullOrEmpty` 가드 |
| 4 | **Tag enum 이름 검색의 부수효과** ("none" 입력 시 None 태그 항목 노출) | 정책상 수용 (§3). 디자이너에게 안내 필요 시 검증 단계에서 결정 |
| 5 | **77개 SO × 4채널 매 키 입력 LINQ** | 부담 < 1ms. Trie / 인덱스 불요. 200개 이상으로 늘어나면 폴리시 후속 |
| 6 | **`_lastFilter` 와 도메인 리로드** | `OnEnable` / `CreateGUI` 재호출 시 `_lastFilter` 도 초기화됨 (default = string.Empty). SearchField 의 value 도 초기화됨 — 동기. 별도 영속 X |
| 7 | **`OnRefreshClicked` 의 필터 보존** (현재 코드는 보존 X) | 본 CL 에서 `RebuildTreeWithFilter()` 호출로 변경. 디자이너가 검색 중 Refresh 눌러도 필터 유지 |
| 8 | **`ToolbarSearchField` import** | `using UnityEditor.UIElements;` 추가 필요. 기존 파일은 `UnityEngine.UIElements` 만 import — 신규 using 1줄 |
| 9 | **빈 카테고리 숨김의 디자이너 학습** | 검색 결과 0 매칭 시 트리가 빈 채로 표시 — "검색 매치 없음" 라벨 표시는 폴리시 후속. 본 CL 범위 외 |
| 10 | **검색 시 PingObject 동작** | `OnTreeSelectionChanged` 가 selected leaf 를 ping — 필터링된 트리에서도 정상. 무수정 |
| 11 | **CL-176 / CL-177 머지 상태와 무관** | 본 CL 의 검색은 트리 표시 layer. 더블클릭/Clear/Consumable 슬롯 지정 등 동작 layer 와 직교. 머지 순서 영향 X |
| 12 | **CL-184 (이벤트 자동 갱신, CL-176 위) 와의 의존** | 본 CL 무관. CL-184 진입 시점에 본 검색 코드 위에 _treeView 갱신 흐름이 _lastFilter 보존하도록 검증 권장 |

---

## 검증 시나리오

### CS 빌드 검증 (Claude)

```
1. InventoryTestWindow.cs 컴파일 OK (using UnityEditor.UIElements 추가)
2. InventoryTestWindow.uxml 1줄 삽입 — UXML 스키마 검증 OK
3. InventoryTestWindow.uss .iv-search-field 셀렉터 추가 — 파싱 OK
4. Grep 검증: ToolbarSearchField 매칭 = InventoryTestWindow.cs / .uxml + BalanceEditor 파일들 (cross-reference)
5. RelicData.Effects null 안전 가드 코드 경로 확인
```

### Unity Editor 검증 (사용자)

```
6. Tools > LostMemory > Inventory Test Window 열기 → Toolbar 좌측에 SearchField 표시
7. Play 모드 진입 → 좌측 트리 정상 (Permanent (77) / Consumable (0))
8. SearchField 에 "khi" 입력 → DisplayName 매칭 항목만 노출 (없으면 빈 트리 또는 빈 카테고리)
9. SearchField 에 "공격" 또는 "attack" → EffectDescription 매칭 확인 (디자이너 표시 텍스트)
10. SearchField 에 "strength" → TagPrimary/Secondary 매칭 확인 (Tag enum 이름)
11. SearchField 에 "AttackPowerPercent" → Effects[].Type 매칭 확인 (RelicEffectType enum)
12. SearchField X 버튼 클릭 → 필터 클리어 + 전체 트리 복원
13. 검색 중 Refresh 버튼 클릭 → 검색어 + 매칭 결과 유지 (CL-183 의 _lastFilter 보존 정책)
14. 검색 중 leaf 선택 → 우측 PingObject 동작 확인 (CL-175 동작 유지)
15. 검색 중 더블클릭 → AddRelicToCorrectInventory 동작 확인 (CL-176 머지 후 검증)
16. Edit 모드 복귀 → SearchField 도 비활성 (Body SetEnabled(false) 의 자식이라 자동)
```

---

## 핵심 파일

### 수정 (Claude 작성)

| 경로 | 변경 |
|---|---|
| `LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/InventoryTestWindow.cs` | `_searchField` / `_lastFilter` 멤버 + CreateGUI 등록 + `BuildTreeData(string)` 오버로드 + `MatchesFilter` / `RebuildTreeWithFilter` 신규 + `OnRefreshClicked` 변경 + `using UnityEditor.UIElements;` 추가 |
| `.../Resources/InventoryTestWindow.uxml` | Toolbar 에 `<uie:ToolbarSearchField name="SearchField" class="iv-search-field" />` 1줄 삽입 |
| `.../Resources/InventoryTestWindow.uss` | `.iv-search-field` 1 셀렉터 추가 |

### 무수정 (참고)

- `Runtime/Relics/RelicData.cs` — 읽기만 (DisplayName / EffectDescription / TagPrimary / TagSecondary / Effects)
- `Runtime/Relics/PlayerRelicInventory.cs` / `PlayerConsumableInventory.cs` — 검색과 무관
- `Editor/BalanceEditor/BalanceEditorWindow.cs` — 패턴 차용만, 코드 변경 X
- `Editor/InventoryTest/InventoryTreeNode.cs` — 검색은 표시 layer, 노드 데이터 변경 X

---

## 후속 ticket 영향

| Ticket | 관계 |
|---|---|
| **CL-184 (Epic V 자동 갱신 강화, CL-176 위)** | 인벤토리 이벤트 발화 시 트리/슬롯 갱신할 때 `_lastFilter` 보존. CL-184 plan 에서 본 정책 명시 |
| **CL-185 (Consumable 자동 배치)** | 본 CL 무관. 검색은 좌측 트리 layer, 자동 배치는 우측 슬롯 layer |
| **CL-186 (드래그앤드롭)** | 검색 결과 leaf 도 드래그 가능해야 함 — Manipulator 부착 시점이 BuildLeftTree 의 makeItem/bindItem 이라 본 CL 코드 그대로 호환 |
| **CL-187 (Quick Add 프리셋)** | 본 CL 무관 |
| **검색 폴리시 후속** | 다중 토큰 / 정규식 / 대소문자 옵션 / 검색 결과 하이라이트 / "0 매칭" 라벨 / 검색 히스토리 — 별도 ticket 검토 |
| **legacy `_effectTypeLegacy` 마이그레이션 후** | `_effects[]` 만 검색 — 본 CL 정책 그대로. 마이그레이션 ticket 진입 시 본 코드 영향 X |

---

## 작업 순서

| 순서 | 담당 | 작업 |
|---|---|---|
| 1 | (선결) | CL-175 머지 확인 ✅ (`b0c40ee57`). CL-176 / CL-177 머지 여부와 무관 |
| 2 | Claude | `InventoryTestWindow.uxml` 에 `ToolbarSearchField` 1줄 삽입 |
| 3 | Claude | `InventoryTestWindow.uss` 에 `.iv-search-field` 1 셀렉터 추가 |
| 4 | Claude | `InventoryTestWindow.cs` 수정 — `using UnityEditor.UIElements;` + 멤버/메서드 추가 + `OnRefreshClicked` 변경 |
| 5 | Claude | 컴파일 / Grep 검증 (1-5) |
| 6 | 사용자 | Unity Editor 검증 (6-16) — Play 모드 진입 / 4채널 검색 키워드 시도 |
| 7 | 사용자 | MR 생성 (커밋/push 사용자 직접) |
| 8 | — | Epic V 다음은 **CL-184** (자동 갱신 강화, CL-176 머지 후 진입). 또는 CL-186 (드래그앤드롭, CL-176 위). 시트 의존 따름 |

---

## 예상 시간

| 단계 | 담당 | 시간 |
|---|---|---|
| UXML / USS 수정 | Claude | 5분 |
| InventoryTestWindow.cs 메서드 추가 + OnRefreshClicked 변경 | Claude | 15분 |
| 컴파일 / Grep 검증 (Claude) | Claude | 5분 |
| Unity Editor 검증 (사용자) — 4채널 키워드 | 사용자 | 15분 |
| **합계** | | **약 40분** |

→ 3점 ticket. cl175 (2점, 28분) 보다 약간 깁. 4채널 매칭 + `_lastFilter` 보존 정책 추가분 반영.

---

## 메모

- **CL-183 의 신규 책임 = 검색만**. 트리 / 슬롯은 CL-175 머지로 충족됨. CL-182 와 유사하게 시트 정의가 Epic U / Epic V 사이에 중복 — 본 CL 은 "검색만 신규" 로 차이 명확
- **BalanceEditor `FilterTree` 패턴 4채널 확장** — BalanceEditor 는 DisplayName 만, 본 CL 은 4채널 OR. 디자이너 요청 (epic_uv §9 결정 #1: "검색 범위 = 이름 + 태그 + 효과 → CL-183 에 통합")
- **legacy 효과 필드 검색 X** 정책 — `_effectTypeLegacy` (CL-138 deprecated) 매칭 안 함. 마이그레이션 후 본 CL 코드 영향 0
- **`_lastFilter` 보존 정책** — Refresh 버튼 / CL-184 자동 갱신 시점에 검색어 보존. 디자이너가 검색 중에도 인벤토리 변경 즉시 반영 가능
- 본 CL 완료 후 [client1_tasks_master_plan.md](client1_tasks_master_plan.md) / [epic_uv_master_plan_20260506.md](epic_uv_master_plan_20260506.md) §0 진행 상태 업데이트는 사용자 영역 (cl173 / cl181 §메모 정책 동일)
- master plan epic_uv ticket 시트 line 173 의 의존 표기 = "CL-174" 이지만 실질 의존 = CL-175 — 시트 갱신 권장 (사용자 영역, 본 CL 검증 단계에서 발견)
