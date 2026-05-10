# CL-221: 좌측 RelicData 트리 세트별 그룹화

## Context

`Inventory Test Window` 의 좌측 RelicData 트리가 모든 Permanent 유물을 한 그룹("Permanent (count)")으로 묶어 16세트 빌드 구조가 시각적으로 안 보임. 세트 빌드 검증이 도구의 핵심 가치인데 현재는 이름순 평면 리스트라 검증성이 낮음.

본 작업: 좌측 트리의 Permanent 그룹을 **`RelicTag` 별 16개 하위 그룹**으로 분리해 세트 빌드 검증성 향상.

## 시스템 사실

- **`RelicTag`** ([RelicTag.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Relics/RelicTag.cs)): None + 16세트 (AttackSpeed/Critical/AttackPower/Ice/Lightning/Cooldown/Fire/Wind/MagicalGirl/Health/Defense/Dodge/Range/Luck/Greed/Tarot)
- **`RelicData`** 듀얼 태그 ([RelicData.cs:37,40,84,85](../../LostMemory/Assets/_Project/Scripts/Runtime/Relics/RelicData.cs)): `TagPrimary` + `TagSecondary` (CL-138). `TagSecondary == None` = 단일 태그
- **`BuildSetData`** 자산 16종: `_Project/ScriptableObjects/BuildSets/BuildSet_*.asset` — `SetTag` (RelicTag) + `DisplayName` (한글) 보유
- **현재 그룹 빌더**: [InventoryTestWindow.cs:202](../../LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/InventoryTestWindow.cs) `BuildTreeData` 의 `roots.Add(BuildGroup(ref id, "Permanent", permanents))` 한 줄
- **검색 필터** (CL-183, [:209–227](../../LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/InventoryTestWindow.cs)): 4채널 OR 매칭. 빈 그룹 자동 숨김.
- **Refresh 버튼** ([:477](../../LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/InventoryTestWindow.cs)): `_allRelics` 재로드 + 트리 재빌드.

## 결정 사항 (사용자 확정)

- **듀얼 태그 노출 정책**: **A — Primary/Secondary 양쪽 그룹에 중복 leaf 노출** ✓
  - `TagPrimary == X || TagSecondary == X` 인 모든 RelicData 가 그룹 X 에 포함
  - `TagSecondary == None` 이면 Primary 그룹에만
- **그룹 표시 순서**: `RelicTag` enum 정의 순서 (평타 친화 5 → 스킬 친화 3 → 자동 발동 1 → 공통 7)
- **그룹 헤더 텍스트**: `BuildSetData.DisplayName` (한글 "공속" 등). 매핑 누락 시 enum 이름 fallback.
- **빈 세트 그룹**: 자동 숨김 (필터 적용 후 빈 그룹도 동일)

## 작업 범위

- [x] BuildSetData 16종 로드 헬퍼 — `LoadSetDisplayNames()` (`AssetDatabase.FindAssets("t:BuildSetData")`)
- [x] `_setDisplayNames` 필드 추가 — `BuildLeftTree` / `OnRefreshClicked` 시점에 갱신
- [x] `BuildTreeData` 의 Permanent 그룹화를 RelicTag 별 분기로 교체
- [x] 듀얼 태그 정책 A 적용 (`TagPrimary == tag || TagSecondary == tag`)
- [x] 빈 세트 그룹 자동 숨김 (`if (members.Count == 0) continue;`)
- [x] 그룹 표시 순서: `Enum.GetValues(typeof(RelicTag))` 순서
- [ ] Editor 검증 (사용자)

## 작업 외 (Out of scope)

- 우측 인벤토리 슬롯 / Consumable 영역 변경 X
- 검색 필터 채널 추가 X (현재 4채널 유지)
- BuildSetData / RelicData 자산 데이터 변경 X (정비는 별도 티켓)
- 우측 슬롯의 세트 진행 표시 (현재 티어 등) X — 별도 후속 CL
- `LoadAllRelics` 의 `RelicSearchFolders` 가 `Generated` 폴더만 스캔 — 부모 `Relics/` 의 RelicData (반격의표식, 붉은송곳니 등) 미노출. 본 CL 외, 별도 이슈로 분리.

## 변경 파일

| 파일 | 변경 |
|---|---|
| [InventoryTestWindow.cs](../../LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/InventoryTestWindow.cs) | `_setDisplayNames` 필드 + `LoadSetDisplayNames` 헬퍼 + `BuildLeftTree`/`OnRefreshClicked` 갱신 + `BuildTreeData` 그룹화 로직 교체 |

UXML/USS (`Resources/InventoryTestWindow`) 변경 X — 트리 데이터만 다름.

## 검증 방법

1. Unity Editor → `LostMemory > Inventory Test Window` 열기
2. Play 모드 진입 → Player 자동 검색 후 트리 갱신
3. 좌측 트리 확인:
   - 한글 세트명 그룹들 (공속/치명타/...) 만 표시 (RelicData 없는 세트 자동 숨김)
   - 그룹 순서 = enum 정의 순서
   - 각 그룹 펼치면 해당 세트 RelicData 들이 leaf
   - 듀얼 태그 RelicData 가 양쪽 그룹에 중복 노출 (예: TagPrimary=AttackSpeed, TagSecondary=Luck → "공속" + "행운" 양쪽)
   - 마지막에 Consumable 그룹
4. 검색 필터 입력:
   - `공속` / `치명타` 등 — 매칭 그룹만 남음 (RelicData 매칭으로 빈 그룹 자동 숨김)
   - 부분 RelicData 이름 검색 — 그 RelicData 가 속한 모든 세트 그룹에서 보임
5. Refresh 버튼 — 트리 재빌드, 필터 보존
6. Inventory Clear → 우측만 갱신, 좌측 트리 정상 유지

## 알려진 이슈 / 보류

- **`Generated` 폴더 한정 스캔** — 본 CL 외. 부모 `Relics/` 폴더 RelicData 가 좌측 트리에 안 보임 (CL-203 검증 시 발견). 별도 티켓: `RelicSearchFolders` 확장 또는 자산 위치 통일.
- **TagSecondary != None 인데 동일한 Primary 와 같은 태그인 케이스** — 데이터 오류 가능. 본 CL 의 그룹화 로직은 `TagPrimary == X || TagSecondary == X` 이라 자동으로 1번만 노출 (LINQ Where 단일 매치). 데이터 정비는 별도.
- **RelicData 가 16세트 어디에도 안 속함 (Tag=None,None)** — 트리에 노출 안 됨. 의도대로 단일 태그 == None 인 케이스는 본 도구에서 검증 대상 외.
