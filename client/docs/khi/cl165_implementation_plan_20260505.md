# CL-165 구현 계획서 (2026-05-05)

원본 plan: [`cl165_plan.md`](cl165_plan.md) (김회인 작성)
본 문서: 사용자와의 결정 합의 후 **실행 가능한 구현 계획**.

---

## Context

Epic U 의 **데이터 교환 ticket**. CL-164 의 Dirty/Save/Undo 위에 **JSON Import/Export + Auto-save 토글** 추가. 디자이너 외부 편집 / 백업 / 강제 종료 안전망.

3점 P2, CL-164 의존.

### 분담
- **본 CL-165**: JsonUtility 기반 Export 3종 + Import 2종 + Auto-save debounce 토글 + EditorPrefs 영속성
- CL-166: 추가 카테고리 (Weapon/Skill/Shop) — JSON Import/Export 자동 적용
- 별도 ticket: SO ref Import 처리 / partial merge / Newtonsoft 마이그레이션 / CSV/Excel / enum-name 직렬화 / debounce + 강제 주기 하이브리드

---

## 1. 현황 (Phase 1)

### 1.1 CL-164 산출물 — 진입점 식별

[BalanceEditorWindow.cs](LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/BalanceEditorWindow.cs):
- `_currentlyShownSo` — Import 후 디테일 보존 활용
- `_inRebuild` flag — RebuildTree 중 ClearDetail 차단
- `SuppressAssetWatcher` static 플래그 — Import 도중 watcher 캐스케이드 차단에 재사용
- `OnInspectorChanged()` — Auto-save timer 재시작 hook
- `CountAllDirty()` — Auto-save 발화 조건 (별도 `_hasUnsavedChanges` flag 불필요)
- `SaveAll()` — Auto-save 가 호출
- `_treeView?.RefreshItems()` — Import 후 라벨 갱신
- `UpdateTitle()` — Import / Auto-save 후 호출

[BalanceEditorWindow.uxml](LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/Resources/BalanceEditorWindow.uxml):
- 현재 toolbar: `[Title][Search 240px][Spacer][Refresh 80px][Save All 80px]`
- 본 CL 추가: Export 메뉴 / Import 메뉴 / Auto-save 토글 / Delay 필드

[BalanceEditorWindow.uss](LostMemory/Assets/_Project/Scripts/Editor/BalanceEditor/Resources/BalanceEditorWindow.uss):
- `.be-search` width 240 → 180px (toolbar 폭 줄이기)
- 신규: `.be-export-menu`, `.be-import-menu`, `.be-autosave-toggle`, `.be-autosave-delay`

### 1.2 CL-164 패턴 재사용 매핑

| 위험 | CL-164 의 해결 패턴 | CL-165 적용 |
|---|---|---|
| watcher 캐스케이드 | `SuppressAssetWatcher` flag | Import 시 try-finally |
| 디테일 손실 | `_currentlyShownSo` + `_inRebuild` + `RefreshItems()` | Import 후 RefreshItems 만 (RebuildTree X) |
| Inspector 즉시 발화 | RefreshItems 만 | 이미 적용됨 |
| 종료 시 다이얼로그 멈춤 | `_isQuitting` 가드 | Auto-save 도 동일 가드 |

---

## 2. 결정사항 (사용자 합의)

| # | 항목 | 결정 |
|---|---|---|
| 1 | JSON 라이브러리 | **JsonUtility (Unity 표준)** |
| 2 | Export 단위 | **3종**: Selected / Category / All |
| 3 | Import 단위 | 2종: To Selected / Folder |
| 4 | Import 충돌 처리 | 확인 다이얼로그 (Overwrite / Cancel) |
| 5 | Auto-save 디폴트 | OFF (명시적 ON) |
| 6 | Auto-save delay 디폴트 | **60초 (1분)** — 범위 30s ~ 600s |
| 7 | Auto-save 모델 | **debounce** (N초 변경 없으면 저장) |
| 8 | JSON 포맷 | 순수 (메타 wrapper 없음) |
| 9 | Play/컴파일 중 Auto-save | skip |
| 10 | 영속성 | EditorPrefs (Auto-save 토글 / delay / 마지막 Export 폴더) |
| 11 | Toolbar 레이아웃 | 한 줄 — Search width 240→180px |

---

## 3. 핵심 파일

### 3.1 신규 (2개)

| 경로 | 책임 |
|---|---|
| `Editor/BalanceEditor/JsonImportExport.cs` | Export 3종 + Import 2종 + 충돌 다이얼로그 (정적 헬퍼 또는 인스턴스 클래스) |
| `Editor/BalanceEditor/AutoSaveController.cs` | debounce 타이머 + EditorPrefs + Play/컴파일 가드 |

### 3.2 수정 (3개)

| 경로 | 변경 |
|---|---|
| `BalanceEditorWindow.cs` | toolbar wire-up (Export/Import 메뉴, Auto-save 토글), AutoSaveController 라이프사이클, Import 후 RefreshItems |
| `Resources/BalanceEditorWindow.uxml` | toolbar 에 ToolbarMenu × 2 + ToolbarToggle + IntegerField 추가 |
| `Resources/BalanceEditorWindow.uss` | `.be-search` width 180 으로 축소, 신규 element 스타일 |

---

## 4. 구현 단계

### 4.1 단계 1: JsonImportExport — Export 3종 (45분)

```csharp
// Editor/BalanceEditor/JsonImportExport.cs
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LostMemory.Editor.BalanceEditor
{
    public static class JsonImportExport
    {
        private const string LastFolderKey = "BalanceEditor.LastExportFolder";

        public static void ExportSingle(ScriptableObject so)
        {
            if (so == null) return;
            string lastFolder = EditorPrefs.GetString(LastFolderKey, "");
            string path = EditorUtility.SaveFilePanel(
                "Export JSON", lastFolder, $"{so.name}.json", "json");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, JsonUtility.ToJson(so, prettyPrint: true));
            EditorPrefs.SetString(LastFolderKey, Path.GetDirectoryName(path));
        }

        public static int ExportCategory(IBalanceCategoryProvider provider)
        {
            string lastFolder = EditorPrefs.GetString(LastFolderKey, "");
            string root = EditorUtility.SaveFolderPanel(
                $"Export {provider.CategoryName} to Folder", lastFolder, provider.CategoryName);
            if (string.IsNullOrEmpty(root)) return 0;

            int count = 0;
            foreach (var so in provider.LoadAll())
            {
                if (so == null) continue;
                string p = Path.Combine(root, $"{so.name}.json");
                File.WriteAllText(p, JsonUtility.ToJson(so, prettyPrint: true));
                count++;
            }
            EditorPrefs.SetString(LastFolderKey, root);
            return count;
        }

        public static int ExportAll(IEnumerable<IBalanceCategoryProvider> providers)
        {
            string lastFolder = EditorPrefs.GetString(LastFolderKey, "");
            string root = EditorUtility.SaveFolderPanel(
                "Export All to Folder", lastFolder, "BalanceExport");
            if (string.IsNullOrEmpty(root)) return 0;

            int total = 0;
            foreach (var provider in providers)
            {
                var dir = Path.Combine(root, provider.CategoryName);
                Directory.CreateDirectory(dir);
                foreach (var so in provider.LoadAll())
                {
                    if (so == null) continue;
                    string p = Path.Combine(dir, $"{so.name}.json");
                    File.WriteAllText(p, JsonUtility.ToJson(so, prettyPrint: true));
                    total++;
                }
            }
            EditorPrefs.SetString(LastFolderKey, root);
            return total;
        }
    }
}
```

### 4.2 단계 2: JsonImportExport — Import 2종 + 충돌 다이얼로그 (45분)

```csharp
public static bool ImportToSingle(ScriptableObject so)
{
    if (so == null) return false;

    if (EditorUtility.IsDirty(so))
    {
        bool proceed = EditorUtility.DisplayDialog(
            "Import 충돌",
            $"{so.name} 에 저장 안 한 변경이 있습니다.\nJSON 으로 덮어쓰시겠습니까?",
            "Overwrite", "Cancel");
        if (!proceed) return false;
    }

    string lastFolder = EditorPrefs.GetString(LastFolderKey, "");
    string path = EditorUtility.OpenFilePanel("Import JSON", lastFolder, "json");
    if (string.IsNullOrEmpty(path)) return false;

    try
    {
        string json = File.ReadAllText(path);
        Undo.RecordObject(so, $"Import JSON: {Path.GetFileName(path)}");
        JsonUtility.FromJsonOverwrite(json, so);
        EditorUtility.SetDirty(so);
        EditorPrefs.SetString(LastFolderKey, Path.GetDirectoryName(path));
        return true;
    }
    catch (System.Exception e)
    {
        EditorUtility.DisplayDialog("Import 실패",
            $"JSON 파싱 실패:\n{e.Message}", "OK");
        return false;
    }
}

public static (int matched, int skipped) ImportFolder(
    IEnumerable<IBalanceCategoryProvider> providers)
{
    string lastFolder = EditorPrefs.GetString(LastFolderKey, "");
    string root = EditorUtility.OpenFolderPanel("Import Folder", lastFolder, "");
    if (string.IsNullOrEmpty(root)) return (0, 0);

    var allSo = new Dictionary<string, ScriptableObject>();
    foreach (var p in providers)
    {
        foreach (var s in p.LoadAll())
        {
            if (s != null) allSo[s.name] = s;
        }
    }

    int matched = 0, skipped = 0;
    foreach (var file in Directory.GetFiles(root, "*.json", SearchOption.AllDirectories))
    {
        string name = Path.GetFileNameWithoutExtension(file);
        if (!allSo.TryGetValue(name, out var so)) { skipped++; continue; }
        try
        {
            Undo.RecordObject(so, $"Import Folder: {name}");
            JsonUtility.FromJsonOverwrite(File.ReadAllText(file), so);
            EditorUtility.SetDirty(so);
            matched++;
        }
        catch
        {
            skipped++;
        }
    }
    EditorPrefs.SetString(LastFolderKey, root);
    return (matched, skipped);
}
```

`BalanceEditorWindow.cs` 의 wire-up:
```csharp
private void OnImportToSelected()
{
    BalanceEditorWindow.SuppressAssetWatcher = true;
    try
    {
        if (JsonImportExport.ImportToSingle(_currentlyShownSo))
        {
            UpdateTitle();
            _treeView?.RefreshItems();
            UpdateStatus($"Imported to {_currentlyShownSo.name}");
        }
    }
    finally
    {
        BalanceEditorWindow.SuppressAssetWatcher = false;
    }
}

private void OnImportFolder()
{
    BalanceEditorWindow.SuppressAssetWatcher = true;
    try
    {
        var (matched, skipped) = JsonImportExport.ImportFolder(_providers);
        UpdateTitle();
        _treeView?.RefreshItems();
        UpdateStatus($"Imported {matched} matched, {skipped} skipped");
    }
    finally
    {
        BalanceEditorWindow.SuppressAssetWatcher = false;
    }
}
```

> ⚠️ Import 후 **`RebuildTree` 안 부르고 `RefreshItems()` 만** — CL-164 패턴 따라 디테일 패널 보존.

### 4.3 단계 3: AutoSaveController — debounce + EditorPrefs (45분)

```csharp
// Editor/BalanceEditor/AutoSaveController.cs
using System;
using UnityEditor;

namespace LostMemory.Editor.BalanceEditor
{
    public class AutoSaveController
    {
        private const string EnabledKey = "BalanceEditor.AutoSave.Enabled";
        private const string DelayKey = "BalanceEditor.AutoSave.DelaySeconds";

        public const float MinDelay = 30f;
        public const float MaxDelay = 600f;
        public const float DefaultDelay = 60f;   // 1분

        public bool Enabled { get; private set; }
        public float DelaySeconds { get; private set; }

        private double _nextSaveAt;
        private bool _hasPendingChange;
        private readonly Func<int> _getDirtyCount;
        private readonly Action _saveAction;

        public AutoSaveController(Func<int> getDirtyCount, Action saveAction)
        {
            _getDirtyCount = getDirtyCount;
            _saveAction = saveAction;
            Enabled = EditorPrefs.GetBool(EnabledKey, false);
            DelaySeconds = Math.Clamp(
                EditorPrefs.GetFloat(DelayKey, DefaultDelay), MinDelay, MaxDelay);
        }

        public void SetEnabled(bool enabled)
        {
            Enabled = enabled;
            EditorPrefs.SetBool(EnabledKey, enabled);
            if (enabled) ResetTimer();
        }

        public void SetDelay(float seconds)
        {
            DelaySeconds = Math.Clamp(seconds, MinDelay, MaxDelay);
            EditorPrefs.SetFloat(DelayKey, DelaySeconds);
        }

        public void NotifyChange()
        {
            if (!Enabled) return;
            _hasPendingChange = true;
            ResetTimer();
        }

        private void ResetTimer()
        {
            _nextSaveAt = EditorApplication.timeSinceStartup + DelaySeconds;
        }

        public void Tick()
        {
            if (!Enabled) return;
            if (!_hasPendingChange) return;
            if (EditorApplication.timeSinceStartup < _nextSaveAt) return;
            // Play/컴파일 중 가드
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling) return;
            if (EditorApplication.isUpdating) return;
            // Dirty 없으면 호출 안 함
            if (_getDirtyCount() == 0) { _hasPendingChange = false; return; }

            _saveAction();
            _hasPendingChange = false;
        }
    }
}
```

`BalanceEditorWindow.cs` 라이프사이클:
```csharp
private AutoSaveController _autoSave;

private void OnEnable()
{
    _instance = this;
    Undo.undoRedoPerformed += OnUndoRedo;
    EditorApplication.quitting += OnEditorQuitting;
    EditorApplication.update += OnEditorUpdate;

    _autoSave = new AutoSaveController(CountAllDirty, SaveAll);
}

private void OnDisable()
{
    EditorApplication.update -= OnEditorUpdate;
    // ... 기존 ...
}

private void OnEditorUpdate()
{
    _autoSave?.Tick();
}

private void OnInspectorChanged()
{
    UpdateTitle();
    _treeView?.RefreshItems();
    _autoSave?.NotifyChange();   // Auto-save timer 재시작
}
```

### 4.4 단계 4: toolbar UI 통합 + Search width 조정 (45분)

UXML:
```xml
<uie:Toolbar class="be-toolbar">
    <ui:Label text="Balance Editor" class="be-title" />
    <uie:ToolbarSearchField name="SearchField" class="be-search" />
    <uie:ToolbarMenu name="ExportMenu" text="Export ▾" class="be-export-menu" />
    <uie:ToolbarMenu name="ImportMenu" text="Import ▾" class="be-import-menu" />
    <ui:VisualElement class="be-toolbar-spacer" />
    <uie:ToolbarToggle name="AutoSaveToggle" label="Auto-save" class="be-autosave-toggle" />
    <uie:IntegerField name="AutoSaveDelay" value="60" class="be-autosave-delay" />
    <uie:ToolbarButton name="RefreshButton" text="Refresh" class="be-refresh-btn" />
    <uie:ToolbarButton name="SaveButton" text="Save All" class="be-save-btn" />
</uie:Toolbar>
```

> 메뉴 좌측 (Export/Import) + 우측 (Auto-save/Delay/Refresh/Save) 분리. spacer 가 가운데에서 밀어냄.

CreateGUI wire-up:
```csharp
var exportMenu = root.Q<ToolbarMenu>("ExportMenu");
exportMenu.menu.AppendAction("Selected SO",
    _ => ExportSelectedFromMenu(),
    a => _currentlyShownSo != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
exportMenu.menu.AppendAction("Current Category", _ => ExportCategoryFromMenu());
exportMenu.menu.AppendAction("All Categories", _ => ExportAllFromMenu());

var importMenu = root.Q<ToolbarMenu>("ImportMenu");
importMenu.menu.AppendAction("To Selected SO",
    _ => OnImportToSelected(),
    a => _currentlyShownSo != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
importMenu.menu.AppendAction("Folder...", _ => OnImportFolder());

var autoSaveToggle = root.Q<ToolbarToggle>("AutoSaveToggle");
autoSaveToggle.value = _autoSave.Enabled;
autoSaveToggle.RegisterValueChangedCallback(evt => _autoSave.SetEnabled(evt.newValue));

var delayField = root.Q<IntegerField>("AutoSaveDelay");
delayField.value = (int)_autoSave.DelaySeconds;
delayField.RegisterValueChangedCallback(evt =>
{
    int clamped = (int)Math.Clamp(evt.newValue, AutoSaveController.MinDelay, AutoSaveController.MaxDelay);
    if (clamped != evt.newValue) delayField.SetValueWithoutNotify(clamped);
    _autoSave.SetDelay(clamped);
});
```

USS 조정:
```css
.be-search {
    width: 180px;   /* 240 → 180 */
}
.be-export-menu, .be-import-menu {
    flex-shrink: 0;
    margin-right: 4px;
}
.be-autosave-toggle {
    flex-shrink: 0;
    margin-right: 4px;
}
.be-autosave-delay {
    flex-shrink: 0;
    width: 60px;
    margin-right: 8px;
}
```

### 4.5 단계 5: 검증 (1시간) — §6 시나리오

---

## 5. 위험 / Gotchas

### 5.1 RelicTag enum 정수 직렬화 위험 (신규, 별도 ticket)

JsonUtility 가 enum 을 정수로 저장:
- Export 시점: `_tagPrimary = AttackSpeed (=1)` → JSON `"_tagPrimary": 1`
- 향후 RelicTag 순서 변경 (예: AttackSpeed → 2 위치로 이동) 후 Import → `1` 이 다른 enum 으로 매핑 → **데이터 손상**

**완화**:
- CL-138 후 17개 RelicTag 가 정착. 가까운 미래에 변경 가능성 낮음.
- 향후 변경 시 마이그레이션 ticket 필요.
- 본 plan 의 §위험 섹션에 명시 + git 커밋 메시지 가이드: "RelicTag 순서 절대 변경 금지" (또는 명시적 enum value 부여).

**별도 ticket 후보**: enum-name 직렬화 (Newtonsoft 또는 custom), enum value 명시화 (`= 0`, `= 1`, ...).

### 5.2 Auto-save debounce 함정 (사용자 가이드 필요)

debounce 의미: "**N초간 변경 없으면 저장**". 즉:
- 활성 편집 중에는 절대 발화 안 함
- 30분 연속 편집 → 0번 저장 → 크래시 시 30분 잃음

**완화**:
- statusbar 또는 tooltip 으로 의미 명시: "Auto-save: 1분 멈추면 저장"
- 향후 별도 ticket: debounce + 강제 주기 저장 하이브리드 (예: "1분 멈추면 또는 5분마다")

### 5.3 Import 시 watcher 캐스케이드 (CL-164 패턴 재사용)

`JsonUtility.FromJsonOverwrite + EditorUtility.SetDirty` → SaveAssets 호출 안 하지만 후속 RefreshItems 에서 트리 갱신. **다행히 SetDirty 자체는 AssetPostprocessor 발화 안 함**.

단 사용자가 직후 Save All 하면 watcher 발화 → `SuppressAssetWatcher` 로 차단 (CL-164 §4.2 와 동일).

본 CL Import 메서드의 try-finally 패턴 적용:
```csharp
SuppressAssetWatcher = true;
try { /* Import */ }
finally { SuppressAssetWatcher = false; }
```

### 5.4 CL-164 호환 — Import 후 RefreshItems 만

`RebuildTree` 호출하면 SetRootItems → ID 재생성 → CL-164 의 선택 보존 패턴이 발동하지만 **불필요한 비용**. Import 는 SO 자체 추가/삭제 없으므로 `RefreshItems()` 만으로 충분. 디테일 패널 자동 보존 (CL-164 패턴).

### 5.5 부분 Import = 의도치 않은 reset (plan §위험 5)

`JsonUtility.FromJsonOverwrite` 가 JSON 에 없는 필드를 디폴트값으로 초기화. 디자이너가 일부만 수정한 JSON 으로 Import 시 나머지 필드 초기화 위험.

**완화**: 다이얼로그 메시지에 "전체 필드 덮어쓰기" 명시. 별도 ticket 으로 partial merge.

### 5.6 SO ref Import 깨짐 (plan §위험 2)

JsonUtility 가 SO ref 를 instanceID 로 직렬화 → 다른 환경에서 깨짐. 본 CL 대상 SO 는 ref 거의 없음 (RelicData/BuildSetData/WeaponData/SkillData/ShopConfig 모두 enum 위주).

**향후 위험**: RewardPool 같은 SO ref 배열. CL-166 진입 시 점검.

### 5.7 Auto-save 가 Play 모드 진입 도중 발화 (plan §위험 4)

`AutoSaveController.Tick()` 의 가드:
```csharp
if (EditorApplication.isPlayingOrWillChangePlaymode) return;
if (EditorApplication.isCompiling) return;
if (EditorApplication.isUpdating) return;
```

Play 모드 진입 시 Unity 가 자동 SaveAssets 호출하므로 우리 Auto-save 가 추가로 호출 안 해도 안전.

### 5.8 EditorPrefs 키 충돌

키 이름:
- `BalanceEditor.AutoSave.Enabled`
- `BalanceEditor.AutoSave.DelaySeconds`
- `BalanceEditor.LastExportFolder`

`BalanceEditor.` prefix 로 namespacing. 향후 ticket (CL-166 등) 도 동일 prefix 권장.

### 5.9 toolbar 폭주

8개 element 한 줄 배치. Search width 240→180px 줄여 공간 확보. Auto-save 토글/Delay 가 추가로 ~120px 차지.

**최소 윈도우 width 800px** (CL-162 minSize) 에서:
```
Title(~120) + Search(180) + Export(60) + Import(60) + Spacer(가변) + Toggle(80) + Delay(60) + Refresh(80) + Save(80) ≈ 720px + spacer
```
800px 윈도우에서 spacer 80px 정도. 충분.

만약 부족 시 Search width 추가 축소 또는 Title 제거 (Unity 탭에 이미 표시됨).

---

## 6. 검증 시나리오 (12 + 환경 보강)

### 원본 12개

| # | 시나리오 | 통과 기준 |
|---|---|---|
| 1 | Export Selected | 트리 SO 선택 → Export ▾ > Selected SO → 다이얼로그 → 파일 생성 + 모든 필드 포함 (prettyPrint) |
| 2 | Export Category | Export ▾ > Current Category → 폴더 선택 → 카테고리 SO 수만큼 .json 파일 |
| 3 | Export All | Export ▾ > All Categories → 폴더 → Relics/ + BuildSets/ 하위 폴더 + 각 SO 1 파일 |
| 4 | Import to Selected | 선택 SO → Import ▾ > To Selected → JSON 파일 → 필드 갱신 + 트리 dirty 표시 + Ctrl+Z 로 원복 가능 |
| 5 | Import Folder | Import ▾ > Folder → 폴더 선택 → 매치 카운트 / skip 카운트 statusbar 표시 |
| 6 | Import 충돌 | dirty SO 에 Import 시도 → 다이얼로그 → Cancel 시 변경 유지 / Overwrite 시 덮어쓰기 |
| 7 | Auto-save 토글 OFF | 변경 후 1분 대기 → SaveAssets 호출 X, dirty 마커 유지 |
| 8 | Auto-save ON | 토글 ON → 변경 후 1분 → 자동 SaveAll, statusbar `Auto-saved at HH:mm:ss`, dirty 사라짐 |
| 9 | Auto-save Delay 변경 | Delay 30 으로 변경 → 변경 후 30s → 자동 저장 |
| 10 | Auto-save debounce | 변경 → 50s 후 또 변경 → 추가 1분 대기 → 저장 (timer 재시작 확인) |
| 11 | 영속성 | Auto-save ON + Delay 90 설정 → 윈도우 닫고 재오픈 → ON + 90 유지 |
| 12 | 라운드트립 | Export Selected → 외부 편집 X → Import → 변경 X (또는 SetDirty 만, 실제 값 동일) |

### 환경 보강 (CL-164 패턴 검증)

| # | 시나리오 | 통과 기준 |
|---|---|---|
| A | Import 후 디테일 보존 | SO 선택 → Import → InspectorElement 그대로 (값만 갱신) |
| B | watcher 폭주 차단 | Import 도중 Console 에 RebuildTree 로그 없음 (또는 임시 Debug.Log 로 확인) |
| C | Auto-save Play 가드 | Auto-save ON + dirty + Play 모드 진입 → Auto-save 발화 X (Unity 자체가 Save) |
| D | Auto-save 컴파일 가드 | Auto-save ON + dirty + 스크립트 수정 (재컴파일 트리거) → Auto-save 발화 X |
| E | EditorPrefs 영속 | 윈도우 닫고 Unity 재시작 → Auto-save 토글 / Delay / 마지막 Export 폴더 유지 |
| F | Delay 범위 클램프 | Delay 0 입력 → 30 으로 자동 클램프 / Delay 9999 → 600 으로 클램프 |

---

## 7. 후속 ticket 영향

| Ticket | CL-165 와의 관계 |
|---|---|
| **CL-166** (추가 카테고리) | WeaponCategoryProvider 등 추가 → JSON Export/Import 자동 적용 |
| 별도: SO ref Import 처리 | RewardPool 같은 ref 배열 |
| 별도: partial merge | JSON 일부 필드만 적용 |
| 별도: Newtonsoft 마이그레이션 | Dictionary / polymorphism 지원 |
| 별도: CSV/Excel Import | 디자이너 친화 포맷 |
| 별도: enum-name 직렬화 | RelicTag 순서 변경 안전 |
| 별도: debounce + interval 하이브리드 | 활성 편집 중에도 N분마다 강제 저장 |
| 별도: JSON manifest | 메타 정보 (생성 시각, 버전) |

---

## 8. 예상 시간

| 단계 | 시간 |
|---|---|
| 1단계 (Export 3종) | 45분 |
| 2단계 (Import 2종 + 다이얼로그 + SuppressAssetWatcher) | 45분 |
| 3단계 (AutoSaveController + EditorPrefs + Play/컴파일 가드) | 50분 |
| 4단계 (toolbar UI + Search width 조정) | 50분 |
| 5단계 (검증 12 + 환경 6 시나리오) | 1시간 10분 |
| **합계** | **약 4시간 20분** |

→ 3점 ticket 부합. 원본 4시간 + (+15분 toolbar 조정 + +5분 debounce 가이드).

---

## 9. 작업 순서 (사용자 + Claude 분담)

### Claude 작성 (코드)
- 단계 1~4 의 .cs / .uxml / .uss 변경

### 사용자 작업 (Unity 에디터)
- Unity 컴파일 모니터링 (Console 빨간 에러)
- §6 검증 12 + 환경 보강 6 시나리오 실행
- Auto-save delay 실측 (30s, 60s 시계 확인)
- Import 후 디테일 보존 검증 (CL-164 패턴 정상 동작)
- commit / push / MR

### 환경 전환 시
- 핸드오프 양식 (`cl165_phase_1_handoff_<YYYYMMDD>.md`) 누적 기록

---

## 10. 결정사항 변경 시 영향

| 변경 시도 | 영향 |
|---|---|
| Auto-save 디폴트 ON | EditorPrefs 디폴트 변경 1줄 — 디자이너에게 자동 저장 부담 (의도치 않은 commit 가능) |
| Delay 디폴트 변경 (30/120/300 등) | EditorPrefs 디폴트 변경 1줄 |
| debounce → interval 하이브리드 | AutoSaveController 로직 추가 (별도 ticket) |
| JsonUtility → Newtonsoft | 의존성 추가 + JsonImportExport 전면 재작성 (별도 ticket) |
| Import partial merge | FromJsonOverwrite → 사전 파싱 + 필드별 SetValue (별도 ticket) |
| Export 단위 변경 | 메뉴 항목 추가/제거 |
| Toolbar 2줄 분리 | UXML/USS 구조 변경 — 본 CL 에서 가능하나 권장 X |
| RelicTag 순서 변경 (위험!) | Export 한 모든 JSON 무효화 — 마이그레이션 ticket 필수 |

---

## 11. 핵심 인사이트 — CL-164 패턴 재활용

본 CL 은 **CL-164 의 패턴을 그대로 재사용**:

1. **`SuppressAssetWatcher`** — Import 도중 watcher 차단 (Save All 과 동일)
2. **`_currentlyShownSo` + `_inRebuild`** — Import 후 디테일 보존
3. **`_treeView?.RefreshItems()`** — Import 후 라벨만 갱신 (RebuildTree 안 함)
4. **`CountAllDirty()`** — Auto-save 발화 조건 (중복 flag 회피)

→ **새 패턴 도입 없이 기존 인프라로 모든 기능 구현**. CL-164 의 §4 Gotchas 참조하여 즉시 해결.
