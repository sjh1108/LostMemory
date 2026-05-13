# Item Icon 자동 할당 Editor 메뉴

## Context

- `Art/UI/Icon/` 에 PNG 56개가 추가됐고 (git status `?? LostMemory/Assets/_Project/Art/UI/Icon/`), 곧 96개 `RelicData` SO의 `_icon` 필드에 일괄 할당해야 함.
- 현재 대부분 SO의 `_icon: {fileID: 0}` — 비어있음.
- Inspector에서 96개를 일일이 드래그하는 것은 비효율. 게다가 한 번이 아니라 PNG 추가될 때마다 반복될 작업.
- **걸림돌**: SO 이름과 PNG 파일명이 정확히 매칭되지 않음.
  - `RelicData_가죽 신발.asset` ↔ `가죽신발.png` (공백 차이)
  - `RelicData_거북이 등딱지.asset` ↔ `거북이 등껍질.png` (동의어 — 이건 사람이 봐야 함)
- 목표: **이름 정규화 + fuzzy 매칭으로 70~80% 자동 할당**, 나머지는 Dry Run 콘솔 리포트를 보고 수동 처리.

## 접근

[`RelicSizeApplyMenu.cs`](../../LostMemory/Assets/_Project/Scripts/Editor/Relics/RelicSizeApplyMenu.cs) 패턴을 그대로 따라간다 — `AssetDatabase.FindAssets("t:RelicData")` + `SerializedObject` + Dry Run 분리 + 콘솔 분류 리포트. 새 코드 한 파일만 추가.

### 매칭 알고리즘

1. **SO 키 추출**: `so.name` 에서 `RelicData_` prefix 제거. 비어있으면 skip. `DisplayName` 은 사용 안 함(대부분 비어있을 가능성).
2. **PNG 키 추출**: 파일명에서 확장자 제거.
3. **정규화** (양쪽 모두 동일하게):
   - 공백/언더스코어/하이픈 제거 (`Regex.Replace(@"[\s_\-]+", "")`)
   - 끝의 숫자 suffix 제거 (`Regex.Replace(@"\d+$", "")`) — `갑옷2` ↔ `갑옷`, `RelicData_가죽 신발 1` ↔ `가죽신발` 매칭 위해
4. **매칭 우선순위**:
   - (a) 정규화 후 정확히 일치
   - (b) SO 키가 PNG 키를 포함 (또는 그 반대) — longest match 우선
   - (c) 둘 다 실패 → `[NoMatch]` 로 분류
5. **모호 케이스** (여러 PNG가 같은 SO에 매칭): 첫 번째 사용 + `[Ambiguous]` 경고 로그.
6. **불필요 PNG 제외**: `ChatGPT Image` 로 시작하는 파일은 매칭 후보에서 자동 제외.

### 보호 정책

- 이미 `_icon` 이 할당된 SO 는 **건드리지 않음** (덮어쓰기 방지). `[AlreadyAssigned]` 카운트만 보고.
- 강제 재할당이 필요하면 별도 메뉴 `Reassign All Icons (Force)` 분리 — 일단 V1에서는 생략.

### 출력 분류 (콘솔)

```
[CL-XXX] [DRY RUN] / [APPLIED]
- Assigned: N개
- AlreadyAssigned (skip): N개
- NoMatch (수동 처리 필요): N개
    RelicData_거북이 등딱지
    RelicData_강철 손목보호대
    ...
- Ambiguous (첫 번째 사용): N개
    RelicData_가죽 신발 → [가죽신발.png, 고급가죽신발.png] → 가죽신발.png 채택
- UnusedIcons (어느 SO에도 매칭 안 됨): N개
    ChatGPT Image ...png  ← 삭제 후보
    ...
```

## 추가/수정 파일

| 파일 | 작업 |
|---|---|
| `LostMemory/Assets/_Project/Scripts/Editor/Relics/RelicIconAutoAssignMenu.cs` | **신규**. `LostMemory/Relics/Assign Icons by Name (Dry Run)` + `Assign Icons by Name (Apply)` 두 MenuItem. 기존 `RelicSizeApplyMenu.cs` 의 구조 그대로 차용. |

기존 파일 수정 없음. 데이터(.asset)는 Apply 단계에서만 `SerializedObject` 통해 변경.

## 핵심 참고 파일 (재사용)

- [`RelicSizeApplyMenu.cs`](../../LostMemory/Assets/_Project/Scripts/Editor/Relics/RelicSizeApplyMenu.cs) — Dry Run / Apply 분리, `FindAssets("t:RelicData")` 순회, `SerializedObject` + `SetDirty` + `SaveAssets` 패턴 그대로.
- [`RelicData.cs:51`](../../LostMemory/Assets/_Project/Scripts/Runtime/Relics/RelicData.cs) — `_icon` 필드명 (SerializedProperty 경로).
- [`RelicDataBatchGenerator.cs`](../../LostMemory/Assets/_Project/Scripts/Editor/Relics/RelicDataBatchGenerator.cs) — Editor 어셈블리 위치 / 네임스페이스 컨벤션 (`LostMemory.Editor.Relics`).
- 아이콘 스캔 경로: `Assets/_Project/Art/UI/Icon` — `AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/_Project/Art/UI/Icon" })`.

## 검증 (Verification)

1. **Dry Run 실행** — Unity 에디터 메뉴 `LostMemory/Relics/Assign Icons by Name (Dry Run)`. 콘솔에서:
   - `Assigned` 카운트가 60~80개 사이인지 (96개 중).
   - `NoMatch` 목록을 시각적으로 검토 — 동의어 케이스(등딱지↔등껍질 등) 정리 후 PNG 파일명 rename 으로 해결할지, 수동으로 Inspector에서 할당할지 결정.
   - `UnusedIcons` 의 `ChatGPT Image*.png` 3개는 삭제 후보로 확인.
2. **Apply 실행** — Dry Run 결과가 만족스러우면 `Assign Icons by Name (Apply)`. 콘솔 `[APPLIED]` 확인.
3. **시각 확인**:
   - `Dungeon_1F_Shop` 씬을 열고 상점에서 아이콘이 보이는지 확인 — 바인딩 지점 [`ShopItemView.cs`](../../LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopItemView.cs) (`_iconImage.sprite = data.Relic.Icon`).
   - 인벤토리 UI에서도 확인 — [`InventorySlotView.cs`](../../LostMemory/Assets/_Project/Scripts/Runtime/UI/Inventory/InventorySlotView.cs).
4. **Git diff** — `ScriptableObjects/Relics/**/*.asset` 의 `_icon: {fileID:...}` 변경 라인 수가 Assigned 카운트와 일치하는지 확인.

## V1 범위 제외 (후속 작업 후보)

- CSV 오버라이드 매핑 (`icon_overrides.csv`) — fuzzy 로 안 잡히는 동의어 케이스. 첫 Dry Run 결과 보고 필요하면 V2.
- AssetPostprocessor (Texture Import Settings 자동화) — 56개는 수동 일괄 설정이 빠를 수 있음. 추후 PNG 추가가 잦으면 추가.
- 강제 재할당 메뉴 — 현재는 `_icon` 이 비어있을 때만 채움.
- `ChatGPT Image*.png` 자동 삭제 — 일단 리포트만, 사용자가 보고 직접 삭제.
