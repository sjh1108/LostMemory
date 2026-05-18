# CL-221 구현 요약 — 좌측 RelicData 트리 세트별 그룹화

**상태**: 검증 완료 (2026-05-09)
**관련 plan**: [cl221_relic_tree_set_grouping_plan.md](cl221_relic_tree_set_grouping_plan.md)

## 목적

`Inventory Test Window` 좌측 RelicData 트리가 모든 Permanent 유물을 한 그룹으로 묶어 16세트 빌드 구조 시각 확인 불가 → `RelicTag` 별 16개 세트 그룹으로 분리.

## Plan과 달라진 점

없음 — 결정 사항(옵션 A, enum 순서, BuildSetData.DisplayName 헤더, 빈 그룹 숨김) 그대로 구현.

## 변경 파일

[InventoryTestWindow.cs](../../LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/InventoryTestWindow.cs) — 5 위치 변경.

### 1. 신규 필드

```csharp
// CL-221: RelicTag → BuildSetData.DisplayName (한글 세트명) 매핑. 좌측 트리 그룹 헤더용.
private Dictionary<RelicTag, string> _setDisplayNames = new();
```

### 2. 신규 헬퍼 — `LoadSetDisplayNames()`

`AssetDatabase.FindAssets("t:BuildSetData")` 로 16종 SO 스캔 → `RelicTag → DisplayName` 매핑. 자산 누락 / DisplayName 미설정이면 enum 이름 fallback.

### 3. `BuildLeftTree` — 매핑 초기 로드

```csharp
_allRelics = LoadAllRelics();
_setDisplayNames = LoadSetDisplayNames();
```

### 4. `BuildTreeData` — Permanent 그룹화 로직 교체

```csharp
// 옛: roots.Add(BuildGroup(ref id, "Permanent", permanents));
// 새:
foreach (RelicTag tag in System.Enum.GetValues(typeof(RelicTag)))
{
    if (tag == RelicTag.None) continue;
    var members = permanents
        .Where(r => r.TagPrimary == tag || r.TagSecondary == tag)
        .ToList();
    if (members.Count == 0) continue;
    string label = _setDisplayNames.TryGetValue(tag, out var dn) ? dn : tag.ToString();
    roots.Add(BuildGroup(ref id, label, members));
}
```

- 듀얼 태그 정책 A: `TagPrimary == tag || TagSecondary == tag` — 양쪽 그룹에 중복 leaf
- 그룹 순서: `Enum.GetValues` (= enum 정의 순서)
- 빈 그룹 자동 숨김: `members.Count == 0` 가드
- 헤더: `BuildSetData.DisplayName` (한글). 매핑 누락 시 enum 이름

### 5. `OnRefreshClicked` — Refresh 시에도 매핑 갱신

```csharp
_allRelics = LoadAllRelics();
_setDisplayNames = LoadSetDisplayNames();
```

BuildSetData 자산을 Editor 에서 수정한 후 Refresh 누르면 헤더에 반영됨.

## 핵심 동작

```
열기 / Refresh
  ↓
LoadAllRelics()        → Generated 폴더 RelicData 스캔
LoadSetDisplayNames()  → 16개 BuildSetData 스캔, RelicTag → 한글명 매핑
  ↓
BuildTreeData(filter)
  ↓ permanents = !IsConsumable + 필터 매칭
  ↓ for tag in RelicTag (None 제외)
  ↓   members = permanents.Where(Primary == tag || Secondary == tag)
  ↓   if members.Empty continue
  ↓   roots.Add(BuildGroup(label = displayNames[tag], members))
  ↓ if consumables.Any: roots.Add("Consumable")
  ↓
TreeView 표시 + ExpandAll
```

검색 필터는 기존 `MatchesFilter` (4채널 OR — 이름 / 설명 / 태그 / EffectType) 그대로. 필터 적용 후 `members.Count == 0` 가드로 매칭 없는 세트 그룹은 자동 숨김.

## 검증 방법

1. Unity Editor → `LostMemory > Inventory Test Window` 열기
2. Play 모드 진입 → 좌측 트리 확인:
   - 한글 세트명 그룹들 (공속/치명타/...) — RelicData 가 있는 세트만
   - 그룹 순서가 RelicTag enum 순서(평타 5 → 스킬 3 → 자동 1 → 공통 7)
   - 각 그룹 펼치면 해당 RelicData 들이 leaf
   - 듀얼 태그 RelicData 는 양쪽 그룹에 모두 보임
   - 마지막에 Consumable 그룹
3. 검색:
   - `공속` 입력 — 공속 그룹과 매칭되는 RelicData 들만 남음
   - 부분 RelicData 이름 — 그 RelicData 가 속한 모든 세트 그룹에 표시
4. Refresh 버튼 — 트리 재빌드, `_lastFilter` 보존
5. Inventory Clear → 우측만 갱신, 좌측 정상

## 검증 결과 (2026-05-09)

Inventory Test Window 좌측 트리에서 RelicTag 별 그룹화 정상 동작 확인. 한글 세트명 헤더, enum 정의 순서, 듀얼 태그 양쪽 노출, 빈 그룹 자동 숨김, 검색 필터 호환, Refresh 갱신 모두 OK.

### 검증 단계 발견 / 처리

| # | 발견 | 영향 | 처리 |
|---|---|---|---|
| V1 | Window 첫 오픈 시 좌측 트리에 RelicData 일부만 (예: 2개) 보임 — 데이터·코드는 정상이나 `AssetDatabase` 인덱스 캐시가 미흡한 상태에서 `LoadAllRelics()` 호출됨 | 16세트 중 매칭 그룹이 거의 안 보임 | Refresh 버튼 / Window 재오픈 / 자산 Reimport 중 하나로 해소. 본 CL 코드 변경 외 운영 노트 |

V1 은 본 CL 의 코드 결함이 아니라 Editor 임포트 타이밍 이슈. 재현 시 Refresh 가 제일 빠른 해결.

## 알려진 이슈 / 보류

- **`RelicSearchFolders` 가 `Generated` 폴더만 스캔** — 본 CL 외. 부모 `Relics/` 폴더 RelicData (반격의표식, 붉은송곳니, 추적자의망토 등) 가 좌측 트리에 안 보임 (CL-203 검증 시 발견). 별도 티켓 권장.
- **TagPrimary == TagSecondary 인 데이터 오류** — `Where` 필터가 단일 매치라 자동 1번 노출. 데이터 정비는 별도.
- **Tag 둘 다 None 인 RelicData** — 16세트 그룹 어디에도 안 속해 좌측 트리 미노출. 의도대로 단일 None 케이스는 도구 검증 대상 외.
- **그룹 펼침 상태 유지 X** — `ExpandAll` 이라 매번 모두 펼쳐짐. UX 개선 필요 시 별도 티켓.
- **첫 오픈 시 자산 미인덱스 가능성** — 검증 단계 V1 참고. Refresh 버튼이 안전한 회피책. 자동 retry 로직은 코스트 대비 효익 낮아 보류.

## 변경 파일 정리

| 파일 | 변경 |
|---|---|
| [InventoryTestWindow.cs](../../LostMemory/Assets/_Project/Scripts/Editor/InventoryTest/InventoryTestWindow.cs) | 코드 — 필드 1 + 헬퍼 1 + BuildLeftTree/OnRefreshClicked 갱신 + BuildTreeData 그룹화 교체 |

UXML/USS / 데이터 자산 / 인스펙터 와이어링 변경 X.
