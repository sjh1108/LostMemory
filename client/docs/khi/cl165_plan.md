# CL-165 — JSON Import/Export + Auto-save 토글

## Context

Epic U 의 데이터 교환 ticket. **외부 편집 / 백업 / 자동 저장**.

3점 P2, CL-164 의존.

### 본 CL 책임 범위

1. **JSON Export** — 선택 SO / 카테고리 / 전체 → `.json` 파일
2. **JSON Import** — `.json` 파일 → 기존 SO 덮어쓰기 (Undo 가능)
3. **Auto-save 토글** — 변경 후 N초 (디폴트 30s) 자동 SaveAll
4. **포맷 호환** — Unity `JsonUtility` 표준 + 디자이너 가독성

### 사용 시나리오

- **백업**: 전체 SO → JSON 폴더 (git 외부 보관 가능)
- **디자이너 외부 편집**: Excel/Sheets → CSV/JSON → Import
- **버전 비교**: binary `.asset` 보다 JSON diff 가 가독성 ↑
- **Auto-save**: 디자이너가 Save 잊어도 안전망

### 기존 상태 (CL-162~164)

- ✅ EditorWindow + 트리뷰 + 검색 + 디테일 (CL-162/163)
- ✅ Dirty 추적 + Save 버튼 + Ctrl+S + Undo (CL-164)
- ❌ JSON Import/Export — **본 CL 신설**
- ❌ Auto-save 토글 — **본 CL 신설**

---

## 결정사항

### 1. JSON 직렬화 — `JsonUtility` 표준 ⭐

**옵션**:
- (a) **`JsonUtility`** ⭐ — Unity 표준, ScriptableObject 호환, 빠름
- (b) `Newtonsoft.Json` — 더 풍부, 단 Dictionary/Polymorphism 가능
- (c) Custom 직렬화 — 디자이너 친화 포맷 (Excel-like)

**채택: (a)**.
- Unity 가 SO 직렬화 알고 있는 것 그대로
- 의존성 없음
- 라운드트립 (Export → Import) 안전

**한계**:
- `Dictionary<K,V>` 미지원 → 본 CL SO 들에 없음 (확인 §위험)
- `[SerializeReference]` 미지원 → 본 CL 영향 X
- enum 은 정수로 저장 (이름 X)

→ Newtonsoft 마이그레이션은 별도 ticket.

### 2. Export 단위 — 단일 SO / 카테고리 / 전체

**Export 메뉴 (toolbar)**:
- "Export Selected" — 트리뷰 선택 1개 SO → 단일 파일
- "Export Category" — 선택 노드의 카테고리 전체 → 폴더 (각 SO 1 파일)
- "Export All" — 전체 → 폴더 (카테고리별 하위 폴더)

```csharp
private void ExportSelected()
{
    var so = GetCurrentSelection();
    if (so == null) { ToastNotifier.Show("선택된 SO 가 없습니다"); return; }
    string path = EditorUtility.SaveFilePanel("Export JSON", "", $"{so.name}.json", "json");
    if (string.IsNullOrEmpty(path)) return;
    File.WriteAllText(path, JsonUtility.ToJson(so, prettyPrint: true));
    UpdateStatus($"Exported: {Path.GetFileName(path)}");
}

private void ExportAll()
{
    string root = EditorUtility.SaveFolderPanel("Export All to Folder", "", "BalanceExport");
    if (string.IsNullOrEmpty(root)) return;
    foreach (var provider in _providers)
    {
        var dir = Path.Combine(root, provider.CategoryName);
        Directory.CreateDirectory(dir);
        foreach (var so in provider.LoadAll())
        {
            string p = Path.Combine(dir, $"{so.name}.json");
            File.WriteAllText(p, JsonUtility.ToJson(so, prettyPrint: true));
        }
    }
    UpdateStatus($"Exported all to {root}");
}
```

→ `prettyPrint: true` 디자이너 가독성.

### 3. Import 단위 — 단일 SO 또는 폴더

**Import 메뉴**:
- "Import to Selected" — 선택 SO 에 JSON 덮어쓰기
- "Import Folder" — 폴더 → 이름 매칭으로 일괄 덮어쓰기

```csharp
private void ImportToSelected()
{
    var so = GetCurrentSelection();
    if (so == null) { ToastNotifier.Show("선택된 SO 가 없습니다"); return; }
    string path = EditorUtility.OpenFilePanel("Import JSON", "", "json");
    if (string.IsNullOrEmpty(path)) return;

    string json = File.ReadAllText(path);
    Undo.RecordObject(so, $"Import JSON: {Path.GetFileName(path)}");
    JsonUtility.FromJsonOverwrite(json, so);
    EditorUtility.SetDirty(so);
    UpdateStatus($"Imported: {Path.GetFileName(path)}");
    treeView.RefreshItems();
    UpdateTitle();
}

private void ImportFolder()
{
    string root = EditorUtility.OpenFolderPanel("Import Folder", "", "");
    if (string.IsNullOrEmpty(root)) return;

    int matched = 0, skipped = 0;
    var allSo = _providers.SelectMany(p => p.LoadAll()).ToDictionary(s => s.name);
    foreach (var file in Directory.GetFiles(root, "*.json", SearchOption.AllDirectories))
    {
        string name = Path.GetFileNameWithoutExtension(file);
        if (!allSo.TryGetValue(name, out var so)) { skipped++; continue; }

        Undo.RecordObject(so, $"Import Folder: {name}");
        JsonUtility.FromJsonOverwrite(File.ReadAllText(file), so);
        EditorUtility.SetDirty(so);
        matched++;
    }
    UpdateStatus($"Imported {matched} matched, {skipped} skipped (no matching SO)");
    treeView.RefreshItems();
    UpdateTitle();
}
```

→ Undo 통합 (CL-164 흐름 활용). Import 후 dirty 표시 → 명시적 SaveAll 필요.

### 4. Auto-save 토글

**구조**:
```csharp
[SerializeField] private bool _autoSaveEnabled = false;
[SerializeField, Min(5f)] private float _autoSaveDelaySeconds = 30f;

private float _nextAutoSaveAt;
private bool _hasUnsavedChanges;

private void OnEnable()
{
    EditorApplication.update += OnEditorUpdate;
}

private void OnEditorUpdate()
{
    if (!_autoSaveEnabled) return;
    if (!_hasUnsavedChanges) return;
    if (EditorApplication.timeSinceStartup < _nextAutoSaveAt) return;

    SaveAll();
    _hasUnsavedChanges = false;
    UpdateStatus($"Auto-saved at {DateTime.Now:HH:mm:ss}");
}

// CL-164 의 변경 이벤트 hook 에서:
private void OnPropertyChanged(SerializedPropertyChangeEvent _)
{
    treeView.RefreshItems();
    UpdateTitle();
    _hasUnsavedChanges = true;
    _nextAutoSaveAt = EditorApplication.timeSinceStartup + _autoSaveDelaySeconds;
}
```

→ 변경 후 N초간 추가 변경 없으면 SaveAll. 연속 변경 시 timer 재시작 (debounce).

**UI**:
- toolbar 에 토글: `[ ] Auto-save`
- 토글 옆 숫자 입력: `Delay: [30] s`

```csharp
var autoSaveToggle = new ToolbarToggle { text = "Auto-save", value = _autoSaveEnabled };
autoSaveToggle.RegisterValueChangedCallback(evt => _autoSaveEnabled = evt.newValue);
toolbar.Add(autoSaveToggle);

var delayField = new IntegerField("Delay (s)") { value = (int)_autoSaveDelaySeconds };
delayField.RegisterValueChangedCallback(evt => _autoSaveDelaySeconds = Math.Max(5, evt.newValue));
toolbar.Add(delayField);
```

→ 디폴트 OFF (디자이너가 명시적 ON).

### 5. Auto-save 영속성 — EditorPrefs

토글 / delay 값 다음 Unity 세션 복원:

```csharp
private const string AutoSaveKey = "BalanceEditor.AutoSave.Enabled";
private const string AutoSaveDelayKey = "BalanceEditor.AutoSave.DelaySeconds";

private void OnEnable()
{
    _autoSaveEnabled = EditorPrefs.GetBool(AutoSaveKey, false);
    _autoSaveDelaySeconds = EditorPrefs.GetFloat(AutoSaveDelayKey, 30f);
}

// 토글/delay 변경 시 EditorPrefs.SetBool / SetFloat
```

### 6. Import 시 외부 변경 vs Dirty 충돌

Import 시 SO dirty 가 이미 있으면 사용자 변경이 덮어씌워짐 → **확인 다이얼로그**:

```csharp
private void ImportToSelected()
{
    var so = GetCurrentSelection();
    if (EditorUtility.IsDirty(so))
    {
        bool proceed = EditorUtility.DisplayDialog(
            "Import 충돌",
            $"{so.name} 에 저장 안 한 변경이 있습니다. JSON 으로 덮어쓰시겠습니까?",
            "Overwrite", "Cancel");
        if (!proceed) return;
    }
    // ...
}
```

### 7. JSON 포맷 — Unity 표준 + meta 헤더

**옵션**:
- (a) **순수 JsonUtility 출력** ⭐ — Unity 가 그대로 ImportOverwrite
- (b) Custom wrapper (`{"_meta": {...}, "data": ...}`)

**채택: (a)**.
- 라운드트립 안전 (Unity 자체 호환)
- 디자이너 외부 도구 (Excel + script) 도 단순

→ 메타 정보 (생성 시각, 버전) 는 별도 `manifest.json` 옵션 (CL-166 이후).

### 8. Export 폴더 영속성 — EditorPrefs

마지막 Export 폴더 기억:
```csharp
EditorPrefs.SetString("BalanceEditor.LastExportFolder", path);
```

→ 다음 Export 시 디폴트로 활용.

### 9. 부분 Import 안전성

Import JSON 이 SO 의 일부 필드만 가지고 있으면 나머지는 디폴트 값으로 초기화 (`FromJsonOverwrite` 동작).

→ 디자이너가 일부 수정만 하고 Import 시 의도치 않은 reset. **MVP**: 위험 명시 + Full export 권장. **후속**: partial merge 옵션.

### 10. Auto-save 시 disk I/O 비용

30s 마다 SaveAssets = 디스크 쓰기. 일부 변경만 있어도 모든 dirty SO 저장. **저비용 OK** (75+16 SO 도 200ms 이내).

→ 변경 없으면 호출 X (`_hasUnsavedChanges` 가드).

---

## 핵심 파일

### 신규

| 경로 | 내용 |
|---|---|
| `Editor/BalanceEditor/JsonImportExport.cs` | Export/Import 메서드 모음 |
| `Editor/BalanceEditor/AutoSaveController.cs` | Auto-save 타이머 + EditorPrefs |

### 수정

| 경로 | 변경 |
|---|---|
| `BalanceEditorWindow.cs` (CL-162~164) | toolbar 에 Export / Import / Auto-save 토글 추가, OnEditorUpdate 등록 |
| `BalanceEditorWindow.uxml` (CL-162) | toolbar 에 Export/Import 버튼 + Auto-save 토글 + Delay 필드 |
| `BalanceEditorWindow.uss` (CL-162) | 토글 / 필드 스타일 |

---

## 구현 단계

### 1단계: JsonImportExport — Export 3종 (45분)

§2 코드:
- ExportSelected (단일 SO → 파일)
- ExportCategory (카테고리 전체 → 폴더)
- ExportAll (전체 → 카테고리 하위 폴더)
- EditorPrefs 마지막 Export 폴더 영속성

### 2단계: JsonImportExport — Import 2종 (45분)

§3 코드:
- ImportToSelected (단일 파일 → 선택 SO)
- ImportFolder (폴더 → 이름 매칭 일괄)
- Undo.RecordObject 통합
- Dirty 충돌 다이얼로그 (§6)

### 3단계: AutoSaveController (45분)

§4 + §5 코드:
- _autoSaveEnabled / _autoSaveDelaySeconds
- OnEditorUpdate timer
- _hasUnsavedChanges 추적
- EditorPrefs 영속성

### 4단계: toolbar UI 통합 (45분)

UXML toolbar 에 추가:
- Export 메뉴 버튼 (Drop-down: Selected / Category / All)
- Import 메뉴 버튼 (Drop-down: To Selected / Folder)
- Auto-save 토글
- Delay 입력 필드

```xml
<uie:ToolbarMenu name="ExportMenu" text="Export ▾" />
<uie:ToolbarMenu name="ImportMenu" text="Import ▾" />
<uie:ToolbarToggle name="AutoSaveToggle" label="Auto-save" />
<uie:IntegerField name="AutoSaveDelay" value="30" label="Delay (s)" />
```

코드에서 ToolbarMenu 항목 추가:
```csharp
var exportMenu = root.Q<ToolbarMenu>("ExportMenu");
exportMenu.menu.AppendAction("Selected", _ => ExportSelected());
exportMenu.menu.AppendAction("Category", _ => ExportCategory());
exportMenu.menu.AppendAction("All", _ => ExportAll());
```

### 5단계: 검증 (1시간)

```
시나리오 1: Export Selected
- "전사의 끈" 선택 → Export ▾ > Selected
- 파일 저장 다이얼로그 → "전사의 끈.json" 저장
- 파일 내용 확인: _displayName / _baseDamage / 등 모든 필드

시나리오 2: Export Category
- Export ▾ > Category
- 폴더 선택 → "Relics/" 하위에 75 .json 파일

시나리오 3: Export All
- Export ▾ > All
- 폴더 선택 → "Relics/" + "BuildSets/" 하위 폴더 + 각 SO 1 파일

시나리오 4: Import to Selected
- 외부에서 "전사의 끈.json" 의 _baseDamage 수정 (예: 10 → 50)
- 트리에서 "전사의 끈" 선택 → Import ▾ > To Selected
- 파일 선택 → SO 의 _baseDamage 50 으로 갱신
- 트리 dirty 표시
- Undo 가능 (Ctrl+Z 로 원복)

시나리오 5: Import Folder
- 외부에서 폴더 전체 수정
- Import ▾ > Folder → 폴더 선택
- 일괄 갱신 + 매치 카운트 표시

시나리오 6: Import 충돌
- "전사의 끈" 변경 (dirty 상태) → Import 시도
- 다이얼로그 "저장 안 한 변경이 있습니다..."
- "Cancel" → 변경 유지
- "Overwrite" → JSON 으로 덮어쓰기

시나리오 7: Auto-save 토글 OFF (디폴트)
- 변경 후 30s 대기 → 자동 저장 X
- Dirty 마커 유지

시나리오 8: Auto-save ON
- 토글 ON → Delay 30s
- 변경 후 30s → 자동 SaveAll
- status bar "Auto-saved at HH:mm:ss"
- Dirty 마커 사라짐

시나리오 9: Auto-save Delay 변경
- Delay 5s 로 변경
- 변경 후 5s → 자동 저장

시나리오 10: Auto-save debounce
- 변경 → 25s 후 또 변경 → 추가 30s 대기 (timer 재시작)

시나리오 11: 영속성
- Auto-save ON + Delay 60 설정
- 윈도우 닫고 Unity 재시작 → 다음 열기 시 ON + 60 유지

시나리오 12: 라운드트립
- Export Selected → 외부 편집 X (그대로) → Import → 변경 X
- Dirty 마커 X (또는 Unity 가 SetDirty 하지만 변경 없음)
```

---

## 위험 / 결정 미정

### 위험

1. **JsonUtility 의 SO 한계**: `Dictionary` / `[SerializeReference]` / Polymorphism 미지원. 본 CL SO 점검:
   - RelicData: `EffectEntry[]` (배열, OK), enum (정수 저장, OK)
   - BuildSetData: `SetTier[]` (배열, OK)
   - WeaponData: `AttackStepData[]` (배열, OK)
   - SkillData: enum (OK)
   → 본 CL 영향 없을 것. 단 `EffectEntry` 안의 polymorphism (없음) 점검.
2. **Import 시 SO ref 깨짐**: SO 가 다른 SO 를 ref 하는 경우 (예: BuildSetData 의 RelicTag) → JsonUtility 가 ref 도 직렬화하지만 GUID 가 아닌 instanceID 라 다른 환경에서 깨짐. → 본 CL SO 가 ref 거의 없음 (enum 위주). 위험 낮음. 단 RewardPool._allRewards (RelicData 배열 ref) 같은 케이스는 Import 시 깨짐. → **별도 ticket 으로 SO ref Import 처리**.
3. **Import 후 prefab/scene 안 갱신**: Inspector 에 열려 있던 SO 는 Import 후 재바인딩 필요. EditorUtility.SetDirty + AssetDatabase.Refresh 로 회피.
4. **Auto-save 도중 Unity Play 모드 진입**: SaveAssets 호출 중 도메인 리로드 → 잘못하면 데이터 손상. → Auto-save 가드: `EditorApplication.isPlaying || EditorApplication.isCompiling` 시 skip.
5. **부분 Import = 의도치 않은 reset**: §9 위험. MVP 는 명시적 경고만.
6. **JSON 파일 수 폭증**: 75+16 = 91 .json. Export All 시 100+ 파일 생성. git 커밋 부담. → 디자이너가 binary asset 과 JSON 둘 다 commit?  본 plan: **JSON 은 임시 / 외부 보관**, asset 이 source of truth.
7. **Newtonsoft 마이그레이션 필요 시점**: 향후 Dictionary 추가 / polymorphism 도입 시. 별도 ticket.
8. **Import 시 잘못된 JSON → 예외**: try/catch + 사용자에게 에러 메시지. 본 plan §3 코드 try/catch 추가 권장.

### 결정 미정

- [ ] JSON 라이브러리 — 본 plan: **JsonUtility (Unity 표준)**
- [ ] Export 단위 — 본 plan: **Selected / Category / All 3종**
- [ ] Import 충돌 처리 — 본 plan: **확인 다이얼로그**
- [ ] Auto-save 디폴트 — 본 plan: **OFF (명시적 ON)**
- [ ] Auto-save delay 디폴트 — 본 plan: **30s**
- [ ] 부분 Import 처리 — 본 plan: **MVP 경고만, 후속 partial merge**
- [ ] JSON 메타 헤더 — 본 plan: **순수 JsonUtility (메타 X)**
- [ ] Auto-save Play/컴파일 중 가드 — 본 plan: **skip**

---

## 후속 ticket 영향

| Ticket | CL-165 와의 관계 |
|---|---|
| **CL-166 (데이터 카테고리 연결)** | 추가 카테고리도 Export/Import 자동 적용 |
| **별도 ticket: SO ref Import 처리** | RewardPool 같은 ref 배열 |
| **별도 ticket: partial merge** | 일부 필드만 Import |
| **별도 ticket: Newtonsoft 마이그레이션** | Dictionary / polymorphism |
| **별도 ticket: CSV/Excel Import** | 디자이너 친화 포맷 |
| **별도 ticket: JSON manifest** | 메타 정보 (생성 시각, 버전) |

---

## 예상 시간

| 단계 | 시간 |
|---|---|
| 1단계 (Export 3종) | 45분 |
| 2단계 (Import 2종 + 충돌 다이얼로그) | 45분 |
| 3단계 (AutoSaveController + EditorPrefs) | 45분 |
| 4단계 (toolbar UI) | 45분 |
| 5단계 (검증 12 시나리오) | 1시간 |
| **합계** | **약 4시간** |

→ 3점 ticket 에 부합 (Export/Import 단순 + Auto-save).

---

## 결정 요청

| # | 질문 | 옵션 | 추천 |
|---|---|---|---|
| 1 | JSON 라이브러리 | **JsonUtility** / Newtonsoft | **JsonUtility** |
| 2 | Export 단위 | **Selected/Category/All** / Selected 만 | **3종** |
| 3 | Import 충돌 | **확인 다이얼로그** / 강제 덮어쓰기 | **다이얼로그** |
| 4 | Auto-save 디폴트 | OFF / ON | **OFF** (명시적) |
| 5 | Auto-save delay 디폴트 | 10s / **30s** / 60s | **30s** |
| 6 | JSON 메타 헤더 | 순수 / wrapper | **순수** |
| 7 | Play/컴파일 중 Auto-save | skip / 강제 | **skip** |

전부 추천대로면 **JsonUtility + 3종 + 다이얼로그 + OFF + 30s + 순수 + skip**.

---

## Epic U 진행률 (CL-165 후)

| Ticket | Plan |
|---|---|
| CL-162 EditorWindow 셸 | ✅ |
| CL-163 트리뷰 + 디테일 + 검색 | ✅ |
| CL-164 Dirty + Undo/Redo | ✅ |
| **CL-165 JSON Import/Export + Auto-save** | ✅ ← 방금 |
| CL-166 데이터 카테고리 연결 | ⏳ |

**Epic U: 4/5**

---

## 다음 plan

| 옵션 | Ticket | 점수 | 비고 |
|---|---|---|---|
| **A** | CL-166 데이터 카테고리 연결 | 2점 | Epic U 마지막. Provider 추가 (Weapon/Skill/Enemy/Shop) |

**추천: A (CL-166)** — Epic U 마무리. 본 CL 인프라 + CL-163 Provider 패턴 활용해서 카테고리 추가만.

뭐로 갈까요?
