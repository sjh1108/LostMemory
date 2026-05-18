# 인벤토리 세트효과 패널 추가 플랜

## Context

현재 `BuildManager` (CL-139) 가 16개 세트의 활성 티어를 정확히 계산해 보유하고 있고, `SetEffectApplicator` (CL-140) 가 효과까지 적용 중이다. 그러나 **플레이어가 자신이 어떤 세트를 발동시켰는지, 다음 티어까지 몇 개 남았는지 확인할 UI가 없다.** 인벤토리 슬롯 호버 시 `TooltipView` 가 단일 유물 정보만 보여줄 뿐이라, 빌드 결정이 "그냥 줍는" 수준에 머문다.

목표: 인벤토리 (`I` 키)를 열면 옆에 세트효과 패널이 같이 뜨고, 16개 세트 전부를 활성→비활성 순으로 정렬해 표시한다. 각 세트는 `이름 / 카운트(현재/다음 임계치) / 활성 티어 효과` 를 보여주어, 플레이어가 빌드를 한눈에 파악할 수 있게 한다.

## 결정 요약

| 항목 | 결정 |
| --- | --- |
| 표시 범위 | 16개 세트 전부 (비활성 포함, 시각적 디밍) |
| 배치 | 인벤토리 패널 사이드 (별도 자식 패널, 같이 토글) |
| 항목 정보 | 이름 + 카운트(예: `3/5`) + 다음 임계치 + 활성 티어 효과 |
| 1차 정렬 | 활성 티어 인덱스 내림차순 (활성 세트가 위) |
| 2차 정렬 | 카운트 내림차순 (같은 티어면 더 진행된 게 위) |
| 3차 정렬 (안정) | `BuildManager._setDatabase` 등록 순서 |
| 데이터 소스 | `BuildManager` 의 `AllActiveTiers` / `AllCounts` / `GetSetForTag()` / `OnSetTierChanged` |
| 갱신 트리거 | 패널 Open 시 1회 + `OnSetTierChanged` 이벤트 구독 |

## 변경/생성 파일

### 신규 스크립트

**`LostMemory/Assets/_Project/Scripts/Runtime/Shop/SetEffectPanelView.cs`**
- `BuildManager` 참조 + `SetEffectRowView` 풀 (16개 사전 생성)
- `Init(BuildManager)` 또는 `OnEnable` 시 구독, `OnDisable` 시 해제
- `Refresh()`: 16개 row 엔트리 만들고 정렬 → row.Bind() 호출
- 정렬 키 (3단 stable sort):
  1. activeTier desc (-1 은 가장 아래)
  2. count desc
  3. registration index asc (안정성)
- `_setDatabase` 등록 순서를 알아야 안정 정렬 가능 → `BuildManager` 에 `IReadOnlyList<BuildSetData> RegisteredSetsInOrder` getter 신규 추가하거나, 인스펙터에서 같은 SO 배열을 패널에도 넘겨받음. **권장: BuildManager 에 getter 추가** (단일 진실 원천 유지).

**`LostMemory/Assets/_Project/Scripts/Runtime/Shop/SetEffectRowView.cs`**
- 1개 세트 한 줄 표시 컴포넌트
- 필드: `_nameText` / `_countText` / `_effectText` / `_backgroundImage` (활성 강조용)
- `Bind(BuildSetData set, int count, int activeTier)`:
  - 이름: `set.DisplayName`
  - 카운트: `count` 와 다음 임계치 계산 → `"3/5"` 형식. 최고 티어 도달 시 `"3/MAX"` 또는 마지막 임계치 그대로
  - 효과: `activeTier >= 0 ? set.Tiers[activeTier].Description : set.Tiers[0].Description` (비활성은 디밍 + 0티어 미리보기). 최소 구현은 비활성 시 `"미발동"` 한 줄로 시작해도 충분.
  - 활성/비활성: `_backgroundImage.color` 알파 또는 텍스트 색으로 구분 (활성=불투명, 비활성=알파 0.4)

### 신규 Prefab

**`LostMemory/Assets/_Project/Prefabs/UI/SetEffectPanel.prefab`**
- 루트: RectTransform + `SetEffectPanelView` + 배경 Image
- 내부: ScrollRect + Vertical Layout Group + Content
- Content 자식으로 `SetEffectRow` 16개 사전 생성 (또는 런타임 인스턴스화)

**`LostMemory/Assets/_Project/Prefabs/UI/SetEffectRow.prefab`**
- 루트: Horizontal Layout Group
- 자식: `이름 (Flex 2) / 카운트 (Fixed) / 효과 텍스트 (Flex 5)`
- 배경 Image 1장 (활성 강조용)

### 수정

**`LostMemory/Assets/_Project/Scripts/Runtime/Shop/InventoryToggleController.cs`** (Shop/InventoryToggleController.cs:71-89)
- 필드 추가: `[SerializeField] private SetEffectPanelView setEffectPanel;` (null 허용)
- `Open()`: 기존 `panel.Init(...)` 직후 `if (setEffectPanel != null) { setEffectPanel.gameObject.SetActive(true); setEffectPanel.Init(buildManager); }`
- `Close()`: `if (setEffectPanel != null) setEffectPanel.gameObject.SetActive(false);`
- `Awake()` 의 `startHidden` 분기에도 동일하게 setEffectPanel 숨김 처리
- 새 Inspector 필드: `[SerializeField] private BuildManager buildManager;`

**`LostMemory/Assets/_Project/Scripts/Runtime/Relics/BuildManager.cs`** (Relics/BuildManager.cs:76-77 근처)
- 신규 getter: `public IReadOnlyList<BuildSetData> RegisteredSetsInOrder => _setDatabase;`
  - 배열 그대로 노출 (등록 순서 = Inspector 순서). null/중복은 패널에서 한 번 더 거른다.

### 씬 wiring (수동)

InventoryPanel 이 살아있는 씬마다 (`Assets/_Project/Scenes/...` 의 인벤토리 사용 씬) Hierarchy 에서:
1. SetEffectPanel.prefab 을 Canvas 아래 InventoryPanel 옆에 배치
2. RectTransform 으로 인벤토리 패널 우측에 정렬
3. InventoryToggleController 의 새 두 필드 (`setEffectPanel`, `buildManager`) 채우기
4. `startHidden` 동작 확인 (씬 시작 시 숨겨져야 함)

대상 씬 목록은 `InventoryToggleController` 가 배치된 모든 씬을 grep 으로 확인 (대부분 인벤토리 테스트 씬 + 본 게임 씬).

## 활용 가능한 기존 자산

- `BuildManager.AllActiveTiers` / `AllCounts` / `GetSetForTag()` — Relics/BuildManager.cs:49-77
- `BuildManager.OnSetTierChanged` 이벤트 — Relics/BuildManager.cs:46
- `BuildSetData.DisplayName` / `Tiers[].RequiredCount` / `Tiers[].Description` — Relics/BuildSetData.cs:26-28, 39-51
- `RelicTagLabels.ToKorean()` — Shop/TooltipView.cs:97 에서 사용. 필요 시 카운트 옆 보조 라벨에 활용 가능
- 기존 16개 BuildSet SO — `Assets/_Project/ScriptableObjects/BuildSets/BuildSet_*.asset`

## 정렬 구현 (의사코드)

```csharp
// SetEffectPanelView.Refresh
var entries = new List<(BuildSetData set, int count, int tier, int regIdx)>();
var registered = _buildManager.RegisteredSetsInOrder;
for (int i = 0; i < registered.Count; i++)
{
    var set = registered[i];
    if (set == null) continue;
    var tag = set.SetTag;
    entries.Add((set, _buildManager.GetTagCount(tag), _buildManager.GetActiveTier(tag), i));
}

entries.Sort((a, b) =>
{
    int t = b.tier.CompareTo(a.tier);            // 1차: 활성 티어 desc (-1 가장 아래)
    if (t != 0) return t;
    int c = b.count.CompareTo(a.count);          // 2차: 카운트 desc
    if (c != 0) return c;
    return a.regIdx.CompareTo(b.regIdx);          // 3차: 등록 순 asc
});
```

## 카운트 표시 규칙

- `set.Tiers` 가 오름차순 `RequiredCount` 라는 전제 (CL-138 데이터 규약)
- 다음 임계치 계산:
  - `activeTier == -1` → 다음 = `Tiers[0].RequiredCount` → 표시 `"{count}/{Tiers[0].RequiredCount}"`
  - `0 <= activeTier < Tiers.Count - 1` → 다음 = `Tiers[activeTier + 1].RequiredCount` → 표시 `"{count}/{next}"`
  - `activeTier == Tiers.Count - 1` (최고) → 표시 `"{count}/MAX"`

## 검증 (end-to-end)

1. **빈 인벤토리** — `I` 키 → 인벤토리 패널 + 세트효과 패널 동시 표시. 16개 row 모두 비활성 상태 (디밍, `0/{최저임계치}`, "미발동")
2. **공속 유물 1개 획득** (TagPrimary=AttackSpeed) → 세트효과 패널 즉시 갱신 (구독). 공속 row 가 카운트 1로 올라감. 등록 순 정렬 유지.
3. **공속 1티어 임계치까지 획득** → 공속 row 가 활성화 (티어 0), 정렬에 의해 가장 위로. Description 노출.
4. **여러 세트 동시 활성** → 활성 티어 큰 순 → 카운트 큰 순으로 정렬되는지 확인.
5. **유물 제거 (드래그 버리기)** → BuildManager 의 dirty flag → LateUpdate → `OnSetTierChanged` 발화 → row 다운그레이드/비활성화.
6. **인벤토리 토글** — `I` 두 번 눌러 닫고 다시 열어도 상태 일관. ESC 닫기도 동일.
7. **Shop / Reward 가드** — 상점/보상 패널이 떠 있을 때 `I` 무시 동작 영향 없는지 (기존 동작 유지).

씬: `cl-mvp 통합 데모 씬` (최근 커밋 `ce3c7d0a8`) + 단독 `Inventory Test` 씬이 있다면 그것까지.

## 범위 외 (다음 작업)

- 비활성 세트의 모든 티어 트리 펼쳐 보기 (현재는 "다음 한 단계"만)
- 세트 row 호버 시 상세 툴팁 (모든 티어 + 효과 수치)
- 세트별 아이콘
- 정렬 모드 토글 UI (티어순 / 가나다순)
