# CL-187 — Quick Add 프리셋 (테스터 저장/로드)

## Context

Epic V (인벤토리 테스트 도구) 의 **편의 기능 ticket**. 디자이너/테스터가 자주 쓰는 인벤토리 조합 (예: "특정 빌드 검증용 5개 Relic 세트") 을 프리셋으로 저장 → 한 번 클릭으로 로드. 매번 트리에서 5번 더블클릭 / 드래그 반복 회피.

master plan epic_uv §2 ticket 시트 line 73: "**Quick Add 프리셋 (테스터 저장/로드)** — 현재 인벤토리 상태를 프리셋으로 저장(EditorPrefs 또는 PresetSO) → 한 번 클릭으로 로드. 추천 빌드 즉석 세팅 — 의존 CL-176 — 3점 P2".

CL-176 (`41ba59d73`) 머지 완료 → 진입 가능. **신규 가치 명확** — Epic U Inventory MVP 에 없는 영역. CL-186 와 함께 Epic V 폴리시 마무리 ticket.

### 본 CL 책임 범위

| 포함 | 제외 |
|---|---|
| `InventoryTestPreset` 직렬화 클래스 (name + Permanent GUID[] + Consumable GUID[4]) | 게임 보상 흐름의 프리셋 (본 도구 = 테스트용) |
| `InventoryPresetStore` 정적 매니저 (Save / Load / Delete / List, EditorPrefs 영속) | 멀티 사용자 공유 (사용자별 EditorPrefs — 본인 단독 작업 OK) |
| Toolbar 에 `ToolbarMenu name="PresetMenu"` 추가 — Save/Load/Delete 액션 | UI: 프리셋 비교 / diff / 미리보기 (폴리시 후속) |
| Save 시 이름 입력 다이얼로그 (`EditorUtility.DisplayDialog` 또는 `EditorWindow` popup) | 프리셋 import/export (별도 ticket — JSON 파일 공유 시) |
| Load 시 기존 인벤토리 ⚠ Clear 후 적용 (확정 다이얼로그) | 프리셋 적용 시 일부만 추가 (현재 위에 덮음 모드) |
| RelicData GUID 식별 — asset rename/move 안전 | RelicData 가 삭제됐을 때 자동 마이그레이션 (경고 + skip) |
| 자동 갱신 — `TryAdd` / `TryAddAt` 호출이 CL-175 의 이벤트 발화로 자동 (CL-186 일관) | 프리셋의 게임 효과 미리 적용 (BuildManager 가 자동) |

---

## 현황 (탐색)

### 1.1 본 프로젝트의 직렬화 패턴 — EditorPrefs + JSON 이중 채택

| 도구 | 위치 | 용도 |
|---|---|---|
| `BalanceEditor/AutoSaveController.cs:32-48` | EditorPrefs | UI 상태 (auto-save on/off, debounce delay) |
| `BalanceEditor/JsonImportExport.cs:16-31` | EditorPrefs (`LastExportFolder` key) | 마지막 export/import 폴더 경로 기억 |
| `BalanceEditor/JsonImportExport.cs:30, 179` | `JsonUtility.ToJson` / `FromJsonOverwrite` | SO 직렬화 (CL-165 머지) |

→ **편의 패턴 일관**: EditorPrefs 로 메타 / JSON 으로 데이터. 본 CL 도 동일 채택.

### 1.2 RelicData 식별 — GUID 표준

기존 InventoryTestWindow / BalanceEditor 모두 `AssetDatabase.FindAssets` + `GUIDToAssetPath` + `LoadAssetAtPath` 패턴.

| 식별 후보 | 평가 |
|---|---|
| `RelicData.name` (asset 파일명) | rename 시 깨짐 ❌ |
| **`AssetDatabase.AssetPathToGUID`** | 파일 이동/rename 안전 ✅ — 본 CL 채택 |
| `RelicData.DisplayName` | 디자이너 변경 가능 ❌ |

GUID 예시: `123e4567-e89b-12d3-a456-426614174000` (32자 hex).

### 1.3 인벤토리 상태 캡처 API

| API | 위치 | 본 CL 사용 |
|---|---|---|
| `PlayerRelicInventory.OwnedRelics` (`IReadOnlyList<RelicData>`) | `Runtime/Relics/PlayerRelicInventory.cs:36` | Save 시 GUID 배열로 변환 |
| `PlayerRelicInventory.MaxSlots` (`int`) | `:39` | preset 메타에 저장 (Load 시 검증) |
| `PlayerConsumableInventory.Slots` (`IReadOnlyList<RelicData>`, 길이 4) | `Runtime/Relics/PlayerConsumableInventory.cs:18` | Save 시 길이 4 GUID 배열 (null 허용) |
| `PlayerRelicInventory.TryAdd(RelicData)` | `:88` | Load 시 Permanent 자동 배치 |
| `PlayerConsumableInventory.TryAddAt(int, RelicData)` | `:51` | Load 시 Consumable 슬롯 지정 |
| `PlayerRelicInventory.Clear()` | `:101` | Load 시 기존 인벤토리 비움 |
| `PlayerConsumableInventory.Clear()` | (해당 메서드 존재 가정 — CL-176/177 머지) | 동상 |

### 1.4 BalanceEditor ToolbarMenu 패턴 (CL-165 — 차용 대상)

[BalanceEditorWindow.cs:167-194](../../client/LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/BalanceEditorWindow.cs):

```csharp
var exportMenu = root.Q<ToolbarMenu>("ExportMenu");
if (exportMenu != null)
{
    exportMenu.menu.AppendAction("Selected SO",
        _ => ExportSelected(),
        _ => _currentlyShownSo != null
            ? DropdownMenuAction.Status.Normal
            : DropdownMenuAction.Status.Disabled);
    exportMenu.menu.AppendAction("Current Category", _ => ExportCurrentCategory());
    exportMenu.menu.AppendAction("All Categories", _ => ExportAll());
}
```

→ `ToolbarMenu` 의 `menu.AppendAction(label, action, statusCallback)` — disabled 상태도 지원. 본 CL 의 Save/Load/Delete 메뉴에 그대로 적용.

### 1.5 현재 toolbar UXML 구조

[InventoryTestWindow.uxml:3-9](../../client/LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/Resources/InventoryTestWindow.uxml):

```xml
<uie:Toolbar class="iv-toolbar">
    <ui:Label text="Inventory Test" class="iv-title" />
    <uie:ToolbarSearchField name="SearchField" class="iv-search-field" />  <!-- CL-183 머지 후 -->
    <ui:VisualElement class="iv-toolbar-spacer" />
    <uie:ToolbarButton name="ClearPermanentButton" ... />
    <uie:ToolbarButton name="ClearConsumableButton" ... />
    <uie:ToolbarButton name="RefreshButton" ... />
</uie:Toolbar>
```

→ Spacer 직후 (Clear 버튼들 좌측) 또는 Refresh 직후 (가장 우측) — 정책 결정 필요.

### 1.6 본 CL 에서 손대는 코드의 작성자

| 파일 | 작성자 |
|---|---|
| `InventoryTestWindow.cs` | **김회인 본인** — 신규 메서드 + CreateGUI 확장 |
| `InventoryTestWindow.uxml` | 본인 — `<ToolbarMenu name="PresetMenu" />` 1줄 추가 |
| `InventoryTestWindow.uss` | 본인 — `.iv-preset-menu` 1 셀렉터 추가 (선택) |
| `InventoryTestPreset.cs` | **신규** (Claude) |
| `InventoryPresetStore.cs` | **신규** (Claude) |

→ 협업 0 / 침범 0.

---

## 설계 결정

### 1. 직렬화 = JSON + EditorPrefs

- **JSON 형식**: BalanceEditor CL-165 패턴 일관. `JsonUtility.ToJson(preset, prettyPrint: true)` / `JsonUtility.FromJson<>`
- **저장 위치**: EditorPrefs 1개 키에 전체 preset 컬렉션 직렬화 (단순, 별도 파일 X)
  - Key: `"LostMemory.InventoryTest.Presets"`
  - Value: JSON `{ "presets": [{name, permanentGuids, consumableGuids}, ...] }`

근거:
- BalanceEditor 의 JSON Export/Import 는 **공유용** (디자이너 간 데이터 전송)
- 본 CL 의 Quick Add 는 **개인 테스트 편의** — 사용자 PC 단위 저장 OK (본인 단독 작업, Jira 미등록 정책 동상)
- 향후 공유 필요 시 별도 폴리시 ticket — `Library/` 또는 `Editor/Presets/` 폴더 + asset 으로 이전

### 2. 식별자 = GUID

§1.2 — asset rename / move 안전. 디자이너가 RelicData 파일명 변경해도 preset 동작 유지.

```csharp
[Serializable]
public class InventoryTestPreset
{
    public string name;
    public string[] permanentGuids;       // 길이 가변 (0 ~ MaxSlots)
    public string[] consumableGuids;      // 길이 4, null 허용
    public string savedAt;                // ISO 8601, 메타 정보
    public int permanentMaxSlotsAtSave;   // Load 시 슬롯 수 비교 (경고용)
}
```

### 3. UI 진입점 = ToolbarMenu (BalanceEditor 패턴)

- `PresetMenu` 라벨 = `"Preset"`
- 위치: Toolbar 의 spacer 와 Clear 버튼 사이 (좌측 grouping = view 액션 — search/preset / 우측 grouping = state 액션 — clear/refresh)
- 메뉴 액션:
  - **`Save Current as Preset...`** — 다이얼로그로 이름 입력 → 현재 인벤토리 상태를 새 preset 으로 저장
  - **`Load`** > 서브메뉴 (저장된 preset 이름들) — 클릭 시 확정 다이얼로그 → Load 실행
  - **`Delete`** > 서브메뉴 (저장된 preset 이름들) — 클릭 시 확정 다이얼로그 → 삭제
  - **`Clear All Presets`** — 전체 삭제 (이중 확정)

### 4. Save 동작

```
1. 현재 _relicInv.OwnedRelics + _consumeInv.Slots 캡처
2. 각 RelicData 를 AssetDatabase.GetAssetPath + AssetPathToGUID 로 변환
3. 사용자에게 이름 입력 다이얼로그
   - 빈 문자열 / 중복 이름 → reject
   - 중복 이름은 사용자 확인 후 덮어쓰기 옵션
4. EditorPrefs 의 컬렉션에 추가 + JSON 재직렬화 + EditorPrefs.SetString
5. PresetMenu 재구축 (메뉴 항목 갱신)
```

### 5. Load 동작

```
1. 사용자에게 확정 다이얼로그 — "기존 인벤토리를 비우고 '<preset name>' 으로 교체합니다. 계속할까요?"
2. _relicInv.Clear() + _consumeInv.Clear()
3. preset.permanentGuids 순회 — GUIDToAssetPath + LoadAssetAtPath<RelicData> → _relicInv.TryAdd(relic)
   - null (asset 삭제됨) → Console 경고 + skip
   - IsConsumable=true 잘못된 데이터 → 경고 + skip (preset 저장 시점과 정합성 깨짐)
4. preset.consumableGuids 4개 순회 — GUIDToAssetPath + Load → _consumeInv.TryAddAt(i, relic)
   - null (해당 슬롯 빈 채로 저장됐음) → skip
   - asset 삭제됨 → Console 경고 + skip
5. preset.permanentMaxSlotsAtSave > _relicInv.MaxSlots 면 Console 경고 ("저장 시점 25칸, 현재 20칸 — 일부 미적용")
```

### 6. Delete 동작

```
1. 사용자에게 확정 다이얼로그 — "'<preset name>' 을 삭제합니다."
2. EditorPrefs 컬렉션에서 제거 + 재직렬화
3. PresetMenu 재구축
```

### 7. Clear All 동작

```
1. 이중 확정 다이얼로그 — "모든 preset (N개) 을 삭제합니다. 복구 불가."
2. EditorPrefs.DeleteKey("LostMemory.InventoryTest.Presets")
3. PresetMenu 재구축
```

### 8. PresetMenu 동적 재구축 — 변경 시점

다음 시점에 `RebuildPresetMenu()` 호출:
- CreateGUI (초기)
- Save / Delete / Clear All 직후
- (선택) 도메인 리로드 후 (`OnEnable` — 이미 CreateGUI 가 호출됨)

### 9. asset 삭제 / GUID 변경 시 정책

- **asset 삭제됨** (LoadAssetAtPath 결과 null): Load 시 Console 경고 + skip. preset 자체는 유지 — 사용자가 명시적 삭제할 때까지
- **GUID 변경 가능성** (Unity 가 자동 변경 안 함, 수동 .meta 삭제 시만): asset 식별 실패 → 경고. 본 CL 외 (사용자 메타 관리 영역)

### 10. namespace / 파일 위치

| 파일 | 경로 | namespace |
|---|---|---|
| `InventoryTestPreset.cs` | `Editor/InventoryTest/InventoryTestPreset.cs` | `LostMemory.Editor.InventoryTest` |
| `InventoryPresetStore.cs` | `Editor/InventoryTest/InventoryPresetStore.cs` | `LostMemory.Editor.InventoryTest` |

→ 기존 `InventoryTreeNode.cs` 와 동일 위치 / 동일 ns. Editor 폴더 자동 분리 (asmdef 없음 정책 일관).

### 11. CL-186 / CL-183 / CL-184 무관

- CL-186 drag/drop, CL-183 검색, CL-184 (CL-175/176 흡수) 와 **직교** — preset 은 toolbar 액션 layer, 트리/슬롯/검색과 별개

---

## 신규 / 수정 파일 — 코드

### A. `InventoryTestPreset.cs` (신규)

```csharp
using System;

namespace LostMemory.Editor.InventoryTest
{
    [Serializable]
    public class InventoryTestPreset
    {
        public string name;
        public string[] permanentGuids;
        public string[] consumableGuids;  // 항상 길이 4, null 허용
        public string savedAt;
        public int permanentMaxSlotsAtSave;
    }

    [Serializable]
    public class InventoryTestPresetCollection
    {
        public InventoryTestPreset[] presets;
    }
}
```

> JSON 직렬화 호환을 위해 `[Serializable]` + 평면 필드. `List<>` 대신 `[]` (JsonUtility 가 List 도 지원하지만 일관성 위해 배열).

### B. `InventoryPresetStore.cs` (신규)

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LostMemory.Editor.InventoryTest
{
    /// <summary>
    /// CL-187: InventoryTestWindow 의 Quick Add 프리셋 저장소.
    /// EditorPrefs 1개 키에 모든 preset 의 JSON 직렬화 컬렉션 저장 (사용자 PC 단위).
    /// </summary>
    public static class InventoryPresetStore
    {
        private const string EditorPrefsKey = "LostMemory.InventoryTest.Presets";

        public static List<InventoryTestPreset> LoadAll()
        {
            var json = EditorPrefs.GetString(EditorPrefsKey, "");
            if (string.IsNullOrEmpty(json)) return new List<InventoryTestPreset>();
            try
            {
                var collection = JsonUtility.FromJson<InventoryTestPresetCollection>(json);
                return collection?.presets?.ToList() ?? new List<InventoryTestPreset>();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[InventoryPresetStore] EditorPrefs JSON 파싱 실패: {e.Message}");
                return new List<InventoryTestPreset>();
            }
        }

        public static void SaveAll(List<InventoryTestPreset> presets)
        {
            var collection = new InventoryTestPresetCollection
            {
                presets = presets.ToArray()
            };
            var json = JsonUtility.ToJson(collection, prettyPrint: true);
            EditorPrefs.SetString(EditorPrefsKey, json);
        }

        public static void DeleteAll() => EditorPrefs.DeleteKey(EditorPrefsKey);

        public static InventoryTestPreset Find(string name) =>
            LoadAll().FirstOrDefault(p => p.name == name);

        public static bool Exists(string name) => Find(name) != null;
    }
}
```

### C. `InventoryTestWindow.cs` — 추가/변경

```csharp
// 신규 멤버
private ToolbarMenu _presetMenu;

// CreateGUI 의 toolbar 와이어링 부분에 추가
_presetMenu = root.Q<ToolbarMenu>("PresetMenu");
RebuildPresetMenu();

// 신규 메서드
private void RebuildPresetMenu()
{
    if (_presetMenu == null) return;

    // 메뉴 비우기 (재구축)
    _presetMenu.menu.ClearItems();

    var presets = InventoryPresetStore.LoadAll();

    _presetMenu.menu.AppendAction("Save Current as Preset...",
        _ => OnSavePresetClicked(),
        _ => (_relicInv != null && _consumeInv != null)
            ? DropdownMenuAction.Status.Normal
            : DropdownMenuAction.Status.Disabled);

    if (presets.Count == 0)
    {
        _presetMenu.menu.AppendAction("Load (no presets)",
            _ => { },
            _ => DropdownMenuAction.Status.Disabled);
        _presetMenu.menu.AppendAction("Delete (no presets)",
            _ => { },
            _ => DropdownMenuAction.Status.Disabled);
    }
    else
    {
        foreach (var p in presets)
        {
            var capturedName = p.name;
            _presetMenu.menu.AppendAction($"Load/{capturedName}",
                _ => OnLoadPresetClicked(capturedName),
                _ => (_relicInv != null && _consumeInv != null)
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
            _presetMenu.menu.AppendAction($"Delete/{capturedName}",
                _ => OnDeletePresetClicked(capturedName));
        }
        _presetMenu.menu.AppendSeparator();
        _presetMenu.menu.AppendAction("Clear All Presets",
            _ => OnClearAllPresetsClicked());
    }
}

private void OnSavePresetClicked()
{
    if (_relicInv == null || _consumeInv == null) return;

    string name = "";
    bool nameOk = false;
    while (!nameOk)
    {
        name = EditorInputDialog.Show("Save Preset", "프리셋 이름:", name);
        if (string.IsNullOrEmpty(name)) return;  // 취소

        if (InventoryPresetStore.Exists(name))
        {
            if (EditorUtility.DisplayDialog(
                "프리셋 덮어쓰기",
                $"'{name}' 프리셋이 이미 있습니다. 덮어쓸까요?",
                "덮어쓰기", "다른 이름"))
            {
                nameOk = true;
            }
            // else 다른 이름 입력 루프
        }
        else
        {
            nameOk = true;
        }
    }

    var preset = new InventoryTestPreset
    {
        name = name,
        permanentGuids = _relicInv.OwnedRelics
            .Where(r => r != null)
            .Select(r => GuidOf(r))
            .Where(g => !string.IsNullOrEmpty(g))
            .ToArray(),
        consumableGuids = _consumeInv.Slots
            .Select(r => r != null ? GuidOf(r) : null)
            .ToArray(),
        savedAt = DateTime.UtcNow.ToString("o"),
        permanentMaxSlotsAtSave = _relicInv.MaxSlots
    };

    var presets = InventoryPresetStore.LoadAll();
    presets.RemoveAll(p => p.name == name);  // 덮어쓰기
    presets.Add(preset);
    InventoryPresetStore.SaveAll(presets);

    Debug.Log($"[InventoryTest] Preset saved: '{name}' ({preset.permanentGuids.Length} permanent + " +
              $"{preset.consumableGuids.Count(g => !string.IsNullOrEmpty(g))} consumable)");

    RebuildPresetMenu();
}

private void OnLoadPresetClicked(string name)
{
    var preset = InventoryPresetStore.Find(name);
    if (preset == null) return;
    if (_relicInv == null || _consumeInv == null) return;

    if (!EditorUtility.DisplayDialog(
        "Load Preset",
        $"기존 인벤토리를 비우고 '{name}' 으로 교체합니다.\n계속할까요?",
        "Load", "Cancel")) return;

    _relicInv.Clear();
    _consumeInv.Clear();

    int permanentApplied = 0, permanentSkipped = 0;
    foreach (var guid in preset.permanentGuids ?? Array.Empty<string>())
    {
        var relic = LoadRelicByGuid(guid);
        if (relic == null) { permanentSkipped++; continue; }
        if (relic.IsConsumable)
        {
            Debug.LogWarning($"[InventoryTest] Preset '{name}' 의 permanent GUID '{guid}' 가 IsConsumable=true. skip");
            permanentSkipped++;
            continue;
        }
        _relicInv.TryAdd(relic);
        permanentApplied++;
    }

    int consumableApplied = 0, consumableSkipped = 0;
    var slots = preset.consumableGuids ?? new string[4];
    for (int i = 0; i < Math.Min(4, slots.Length); i++)
    {
        if (string.IsNullOrEmpty(slots[i])) continue;
        var relic = LoadRelicByGuid(slots[i]);
        if (relic == null) { consumableSkipped++; continue; }
        if (!relic.IsConsumable)
        {
            Debug.LogWarning($"[InventoryTest] Preset '{name}' 의 consumable[{i}] GUID '{slots[i]}' 가 IsConsumable=false. skip");
            consumableSkipped++;
            continue;
        }
        _consumeInv.TryAddAt(i, relic);
        consumableApplied++;
    }

    if (preset.permanentMaxSlotsAtSave > _relicInv.MaxSlots)
    {
        Debug.LogWarning($"[InventoryTest] Preset '{name}' saved with MaxSlots={preset.permanentMaxSlotsAtSave}, " +
                         $"current MaxSlots={_relicInv.MaxSlots} — 일부 미적용 가능");
    }

    Debug.Log($"[InventoryTest] Preset loaded: '{name}' " +
              $"(permanent {permanentApplied}/{permanentApplied + permanentSkipped}, " +
              $"consumable {consumableApplied}/{consumableApplied + consumableSkipped})");

    OnConsumableChanged();  // Consumable 이벤트 부재 → 명시 갱신
}

private void OnDeletePresetClicked(string name)
{
    if (!EditorUtility.DisplayDialog(
        "Delete Preset",
        $"프리셋 '{name}' 을 삭제합니다.",
        "Delete", "Cancel")) return;

    var presets = InventoryPresetStore.LoadAll();
    presets.RemoveAll(p => p.name == name);
    InventoryPresetStore.SaveAll(presets);

    Debug.Log($"[InventoryTest] Preset deleted: '{name}'");
    RebuildPresetMenu();
}

private void OnClearAllPresetsClicked()
{
    var presets = InventoryPresetStore.LoadAll();
    if (presets.Count == 0) return;

    if (!EditorUtility.DisplayDialog(
        "Clear All Presets",
        $"모든 프리셋 ({presets.Count}개) 을 삭제합니다. 복구 불가.",
        "Clear All", "Cancel")) return;

    if (!EditorUtility.DisplayDialog(
        "한번 더 확인",
        $"정말 {presets.Count}개 프리셋 모두 삭제할까요?",
        "Yes, Delete All", "Cancel")) return;

    InventoryPresetStore.DeleteAll();
    Debug.Log($"[InventoryTest] All {presets.Count} presets cleared");
    RebuildPresetMenu();
}

private static string GuidOf(RelicData relic)
{
    if (relic == null) return null;
    var path = AssetDatabase.GetAssetPath(relic);
    return string.IsNullOrEmpty(path) ? null : AssetDatabase.AssetPathToGUID(path);
}

private static RelicData LoadRelicByGuid(string guid)
{
    if (string.IsNullOrEmpty(guid)) return null;
    var path = AssetDatabase.GUIDToAssetPath(guid);
    if (string.IsNullOrEmpty(path)) return null;
    return AssetDatabase.LoadAssetAtPath<RelicData>(path);
}
```

### D. `EditorInputDialog.cs` (신규 — 이름 입력 다이얼로그)

Unity Editor 표준 `EditorUtility.DisplayDialog` 는 텍스트 입력 미지원. 단순 popup window 직접 구현 필요:

```csharp
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LostMemory.Editor.InventoryTest
{
    /// <summary>
    /// CL-187: 텍스트 입력 다이얼로그. EditorUtility.DisplayDialog 가 미지원이라 별도 EditorWindow 구현.
    /// </summary>
    public class EditorInputDialog : EditorWindow
    {
        private string _input = "";
        private string _label = "";
        private string _result = null;
        private bool _confirmed = false;

        public static string Show(string title, string label, string defaultValue = "")
        {
            var window = CreateInstance<EditorInputDialog>();
            window.titleContent = new GUIContent(title);
            window._label = label;
            window._input = defaultValue;
            window.minSize = new Vector2(320, 100);
            window.maxSize = new Vector2(320, 100);
            window.ShowModal();
            return window._confirmed ? window._result : null;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(_label);
            _input = EditorGUILayout.TextField(_input);
            EditorGUILayout.Space(8);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("OK", GUILayout.Width(80)))
                {
                    _result = _input?.Trim();
                    _confirmed = !string.IsNullOrEmpty(_result);
                    Close();
                }
                if (GUILayout.Button("Cancel", GUILayout.Width(80)))
                {
                    _confirmed = false;
                    Close();
                }
            }
        }
    }
}
```

> IMGUI 로 작성 (UI Toolkit 보다 modal popup 에 표준적). Enter / Escape 키 핸들링은 폴리시 후속.

### E. `InventoryTestWindow.uxml` — 1줄 추가

기존 toolbar 의 spacer 직후 (Clear 버튼들 좌측):

```xml
<uie:Toolbar class="iv-toolbar">
    <ui:Label text="Inventory Test" class="iv-title" />
    <uie:ToolbarSearchField name="SearchField" class="iv-search-field" />  <!-- CL-183 -->
    <uie:ToolbarMenu name="PresetMenu" text="Preset" class="iv-preset-menu" />  <!-- 신규 -->
    <ui:VisualElement class="iv-toolbar-spacer" />
    <uie:ToolbarButton name="ClearPermanentButton" ... />
    <uie:ToolbarButton name="ClearConsumableButton" ... />
    <uie:ToolbarButton name="RefreshButton" ... />
</uie:Toolbar>
```

### F. `InventoryTestWindow.uss` — 1 셀렉터 추가 (선택)

```css
.iv-preset-menu {
    flex-shrink: 0;
    flex-grow: 0;
    margin-right: 4px;
}
```

---

## 위험 / 알려진 상황

| # | 위험 | 대응 |
|---|---|---|
| 1 | **EditorPrefs 사용자 PC 단위 저장 → 백업 어려움** | 본인 단독 작업 OK. 향후 공유 필요 시 Library/ 또는 Editor/Presets/ 폴더로 이전 (별도 ticket) |
| 2 | **EditorPrefs 키 충돌** | `"LostMemory.InventoryTest.Presets"` 네임스페이스 prefix — 본 프로젝트 다른 도구와 충돌 0 |
| 3 | **JSON 파싱 실패** | `LoadAll` 의 try/catch — 빈 리스트 반환. 사용자 손실 가능 (기존 preset 무시) — 폴리시: 백업 키 (`Presets.backup`) 보존 검토 |
| 4 | **asset 삭제 후 Load 시 GUID 미발견** | LogWarning + skip, preset 자체는 유지. 사용자 명시 정리 |
| 5 | **MaxSlots 변화 (preset 저장 시점 25, 현재 20)** | LogWarning 하되 적용 시도 — TryAdd 가 빈 슬롯 자동 결정 (25개 중 20개 까지만) |
| 6 | **Consumable preset 중복 슬롯** | preset 직렬화 시 슬롯 N 에 1개 — 직접 충돌 없음. 단 preset 저장 시점에 슬롯이 차 있으면 그대로 GUID 캡처 |
| 7 | **RelicData IsConsumable 변경 (저장 후 재정의)** | Load 시 `IsConsumable` 재검증 — Permanent 에 Consumable 들어가면 skip + 경고 |
| 8 | **EditorInputDialog 의 modal blocking** | `ShowModal()` 가 Unity Editor 메인 스레드 block — 사용자 입력 대기. 일반적 패턴 OK |
| 9 | **Unicode preset 이름** | EditorPrefs / JSON 모두 UTF-8. 한글 이름 OK (`"내 빌드 1"`) |
| 10 | **빈 인벤토리 preset 저장** | 가능 — `permanentGuids = []`, `consumableGuids = [null,null,null,null]`. Load 시 Clear 후 빈 채 유지 (정상) |
| 11 | **CL-186 drag/drop 과의 상호작용** | 무관 — preset Load 가 `TryAdd` / `TryAddAt` 호출 → drag 와 동일 path → 자동 갱신 일관 |
| 12 | **CL-183 검색 필터링과의 상호작용** | 무관 — preset 은 좌측 트리 / 우측 슬롯 layer. 검색은 트리 표시 layer |
| 13 | **PlayerConsumableInventory.Clear 메서드 존재 가정** | CL-176/177 머지 시 추가됐는지 확인 필요. 없으면 [InventoryTestWindow.cs:322-333](../../client/LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/InventoryTestWindow.cs) 의 `OnClearConsumableClicked` 가 어떻게 동작하는지 — `_consumeInv.Clear()` 호출 코드 확인 필요. 없으면 수동 슬롯 0~3 Remove 루프 |
| 14 | **JsonUtility 의 List<> 직접 직렬화 미지원** | InventoryTestPresetCollection 의 `presets` 를 배열로. `LoadAll` 에서 `.ToList()` 변환 |
| 15 | **이름 검증 — 빈 / 공백 / 매우 긴** | `Trim()` + `IsNullOrEmpty` 검증. 길이 제한은 폴리시 후속 |

---

## 검증 시나리오

### CS 빌드 검증 (Claude)

```
1. InventoryTestPreset.cs / InventoryPresetStore.cs / EditorInputDialog.cs 컴파일 OK
2. InventoryTestWindow.cs 의 ToolbarMenu / DropdownMenuAction 타입 import 확인 (UnityEditor.UIElements)
3. JsonUtility.ToJson / FromJson 시그니처 정합
4. Grep "InventoryPresetStore" 매치 = 신규 / window.cs 호출
5. PlayerConsumableInventory 의 Clear 메서드 존재 여부 확인 (Read)
```

### Unity Editor 검증 (사용자)

```
6. Inventory Test Window 열기 → Toolbar 에 "Preset" 드롭다운 표시
7. Edit 모드 — Preset 메뉴는 항상 표시되나 Save/Load 는 disabled (Player 인스턴스 없음)
8. Play 모드 진입 → 좌측 트리 / 우측 슬롯 정상
9. 트리에서 RelicData 5개 더블클릭으로 Permanent 채움 + Consumable 1~2개 채움
10. Toolbar Preset > Save Current as Preset... → 다이얼로그 → "테스트1" 입력 → OK
    → Console "[InventoryTest] Preset saved: '테스트1' (5 permanent + 2 consumable)"
11. Toolbar Preset 다시 클릭 → "Save Current as Preset..." / "Load > 테스트1" / "Delete > 테스트1" / "Clear All Presets" 표시 확인
12. Clear Permanent + Clear Consumable 버튼 → 인벤토리 비우기
13. Toolbar Preset > Load > 테스트1 → 확정 다이얼로그 → Load
    → 인벤토리 복원 (5 permanent + 2 consumable, 슬롯 위치 정확)
    → Console "[InventoryTest] Preset loaded: '테스트1' (permanent 5/5, consumable 2/2)"
14. Save 시 같은 이름 입력 → 덮어쓰기 다이얼로그 → 덮어쓰기 → 정상
15. Save 시 빈 문자열 / 공백만 입력 → 다이얼로그 reject 또는 취소 처리
16. Toolbar Preset > Delete > 테스트1 → 확정 → Console 메시지 + 메뉴에서 사라짐
17. preset 0개일 때 Toolbar Preset > Load (no presets) / Delete (no presets) — disabled 표시
18. preset 3개 저장 → Toolbar Preset > Clear All Presets → 이중 확정 → 모두 삭제
19. asset 삭제 시나리오 — 사용자가 Generated/RelicData_X.asset 삭제 → preset Load 시 해당 GUID skip + 경고 로그
20. (선택) RelicData 의 IsConsumable 변경 후 preset Load — type mismatch 경고 + skip
```

---

## 핵심 파일

### 신규 (Claude — 3)

| 경로 | 내용 |
|---|---|
| `LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/InventoryTestPreset.cs` | `InventoryTestPreset` + `InventoryTestPresetCollection` (Serializable POCO) |
| `LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/InventoryPresetStore.cs` | EditorPrefs + JSON 영속 매니저 (LoadAll/SaveAll/DeleteAll/Find/Exists) |
| `LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/EditorInputDialog.cs` | 이름 입력 modal popup (IMGUI) |

### 수정 (Claude — 3)

| 경로 | 변경 |
|---|---|
| `Editor/InventoryTest/InventoryTestWindow.cs` | `_presetMenu` 멤버 + CreateGUI 와이어링 + `RebuildPresetMenu` / `OnSavePresetClicked` / `OnLoadPresetClicked` / `OnDeletePresetClicked` / `OnClearAllPresetsClicked` 메서드 + `GuidOf` / `LoadRelicByGuid` 헬퍼 |
| `Editor/InventoryTest/Resources/InventoryTestWindow.uxml` | `<uie:ToolbarMenu name="PresetMenu" text="Preset" />` 1줄 추가 |
| `Editor/InventoryTest/Resources/InventoryTestWindow.uss` | `.iv-preset-menu` 1 셀렉터 (선택) |

### 무수정

- `Runtime/Relics/RelicData.cs` / `PlayerRelicInventory.cs` / `PlayerConsumableInventory.cs` — 호출만
- `InventoryTreeNode.cs` — 무관
- BalanceEditor 전체 — 패턴 차용만, 코드 수정 X

---

## 후속 ticket 영향

| Ticket | 본 CL 와의 관계 |
|---|---|
| **공유 preset (별도 ticket 후보)** | 사용자 PC 단위 EditorPrefs → 디자이너 간 공유 시 JSON 파일 export/import 또는 Library/Presets/ 폴더 + asset 으로 이전 |
| **preset 미리보기 / diff** (폴리시) | Save 전 또는 Load 전에 슬롯 시각화 — 별도 ticket |
| **Default presets** (게임 빌드 가이드) | 게임 디자인 빌드 가이드를 미리 등록해둔 default preset — 별도 ticket |
| **PlayerStatsData preset** (CL-179 SO 와 연동) | Khi_Stats 와 인벤토리 조합 — 본 CL 외 |
| **CL-186 drag/drop** | 무관 — preset 의 TryAdd / TryAddAt 호출이 drag 와 동일 path |
| **CL-183 검색** | 무관 — preset 적용 후에도 검색 동작 |
| **PlayerConsumableInventory 이벤트 추가** | 본 CL 의 `OnConsumableChanged()` 명시 호출 우회. 이벤트 추가 시 자동 갱신으로 단순화 |
| **CL-185 자동 배치** | preset Load 의 Permanent 는 이미 `TryAdd` (자동 첫 빈) 사용. CL-185 결정 영향 X |

---

## 작업 순서

| 순서 | 담당 | 작업 |
|---|---|---|
| 1 | (선결) | CL-176 (`41ba59d73`) + CL-177 (`db82245f0`) 머지 확인 ✅ |
| 2 | (사전 검증) | `PlayerConsumableInventory.Clear()` 메서드 존재 확인 (없으면 본 CL 에서 수동 Remove 루프로 우회) |
| 3 | Claude | `InventoryTestPreset.cs` 작성 |
| 4 | Claude | `InventoryPresetStore.cs` 작성 |
| 5 | Claude | `EditorInputDialog.cs` 작성 |
| 6 | Claude | `InventoryTestWindow.uxml` 1줄 추가 |
| 7 | Claude | `InventoryTestWindow.uss` 1 셀렉터 추가 (선택) |
| 8 | Claude | `InventoryTestWindow.cs` 수정 — 멤버 + 메서드 + CreateGUI 와이어링 |
| 9 | Claude | 컴파일 / Grep 검증 |
| 10 | 사용자 | Unity Editor 검증 (§검증 6-20) |
| 11 | 사용자 | MR 생성 |
| 12 | — | Epic V 트랙 마무리 — 다음은 (보류) CL-185 정책 결정 또는 다른 Epic 진입 |

---

## 예상 시간

| 단계 | 담당 | 시간 |
|---|---|---|
| InventoryTestPreset.cs 작성 | Claude | 5분 |
| InventoryPresetStore.cs 작성 | Claude | 10분 |
| EditorInputDialog.cs 작성 | Claude | 10분 |
| UXML / USS 수정 | Claude | 5분 |
| InventoryTestWindow.cs 메서드 추가 (Save/Load/Delete/RebuildMenu + 헬퍼) | Claude | 30분 |
| 컴파일 / Grep / PlayerConsumableInventory.Clear 확인 | Claude | 10분 |
| Unity Editor 검증 (사용자) — 15개 시나리오 | 사용자 | 30분 |
| **합계** | | **약 100분** |

→ 3점 P2 ticket. CL-186 (2점, 55분 예상) 보다 김 — 신규 클래스 3개 + 다이얼로그 + 5개 메뉴 액션. epic_uv ticket 시트의 "3점" 적정.

---

## 메모

- **본 CL 은 EditorPrefs 채택** — 사용자 PC 단위. 본인 단독 작업 + Jira 미등록 정책 (cl178 / cl182 / cl184 일관) 으로 공유 불요
- **Library/Editor/Presets/ 폴더 + asset 으로 이전** 가능성 — 향후 디자이너 간 공유 / git tracked 가 필요해지면 별도 ticket. EditorPrefs ↔ asset 마이그레이션 코드 추가
- **JSON 직렬화 호환** — `JsonUtility` 가 단순 POCO 직렬화에 한정. List<> 미지원 → 배열 사용. Newtonsoft.Json 도입 미고려 (본 프로젝트에 없음)
- **GUID 식별자** — RelicData asset rename / move 안전. asset 삭제 시만 자동 마이그레이션 X (LogWarning + skip)
- **CL-185 / CL-184 와 직교** — preset 은 toolbar 액션 layer, drag/검색/더블클릭과 별개
- **Epic V 폴리시 마무리 ticket** — CL-187 머지 후 Epic V (CL-182~187) 전체 정리:
  - CL-182 ~~취소~~ (CL-174 흡수)
  - CL-183 plan 작성됨, 진입 대기
  - CL-184 ~~취소~~ (CL-175/176 흡수)
  - CL-185 정책 결정 필요 (CL-177 충돌)
  - CL-186 plan 작성됨, 진입 대기
  - CL-187 plan 작성됨 (본 doc), 진입 대기
- master plan epic_uv ticket 시트 갱신은 사용자 영역 (cl173 / cl181 / cl182 / cl184 §메모 정책 동일)
- **PlayerConsumableInventory.Clear 의존** — CL-176/177 머지 시 추가됐는지 사전 확인 필요. 없으면 수동 Remove 루프 (`for i 0..3: Remove(i)`) 또는 본 CL 의존 ticket 으로 추가
- **EditorInputDialog 의 IMGUI 채택** — UI Toolkit modal popup 보다 표준적. 단 키보드 (Enter/Escape) 폴리시 후속
