# CL-150 — 아이템 사이즈 시스템 (등급별 매핑 + 일괄 적용)

## Context

Epic S Phase 4의 두 번째 ticket. **CL-138에서 미뤄둔 등급-사이즈 자동 매핑 helper 구현 + 75개 SO 일괄 적용**.

### 기존 상태 (확인 완료)

- ✅ `RelicData._size` (Vector2Int) — CL-138에서 필드 추가, 디폴트 `(1,1)`
- ✅ `RelicRarity` enum — Common / Rare / Unique / Legendary (CL-138 부록 참조)
- ✅ 75+ 개 RelicData SO — CL-141에서 일괄 생성됨 (모두 _size = (1,1) 상태로 추정)
- ❌ 등급 → 사이즈 자동 매핑 helper — **본 CL에서 신설**
- ❌ 인벤토리 그리드의 다중 칸 점유 표현 — **CL-151로 미룸**

### 회의록 매핑

| 등급 | 사이즈 | 비고 |
|---|---|---|
| Common | 1×1 | 일반 |
| Rare | 1×2 (또는 2×1) | 레어 |
| Unique | 2×2 | 유니크 |
| Legendary | 2×2 | 전설 |

→ 본 CL: **(1,1) / (2,1) / (2,2) / (2,2)** 로 확정 (Vector2Int x=가로, y=세로 표기. 가로 우선 1×2 = (2,1))

### 본 CL 책임 범위

1. `RelicSizeMapping` 유틸 (등급 → Vector2Int)
2. RelicData 에 사이즈 helper 프로퍼티 (`Width`, `Height`, `OccupiedCells`)
3. **Editor 일괄 적용 메뉴** — 75개 SO 한 번에 갱신
4. 디자이너 오버라이드 허용 (Lock 플래그)
5. 인벤토리 5×5 안에 들어가는지 검증 (런타임 assert)

→ **자동 배치 알고리즘 / 그리드 시각화는 CL-151**.

---

## 결정사항

### 1. 매핑 정책 — 등급 1:1 매핑

| 등급 | Vector2Int | 점유 칸 |
|---|---|---|
| Common | (1, 1) | 1 |
| Rare | (2, 1) | 2 |
| Unique | (2, 2) | 4 |
| Legendary | (2, 2) | 4 |

**Rare 가로/세로 결정**: 가로 1×2 = `(2, 1)`. 인벤토리가 5칸 가로이므로 가로 우선이 자연스러움. 세로 1×2 = `(1, 2)` 가 필요한 케이스가 생기면 Lock 플래그로 수동 오버라이드.

**Unique vs Legendary 차별 X**: 둘 다 2×2. 회의록 그대로.

### 2. 적용 시점 — Editor 일괄 메뉴 (수동 trigger)

**옵션**:
- (a) **Editor menu 일괄 적용** ⭐ — 디자이너가 명시적 trigger
- (b) OnValidate 자동 적용 — 매번 Inspector 열 때마다 덮어씀 (디자이너 오버라이드 깨짐)
- (c) AssetPostprocessor 자동 적용 — 새 SO 생성 시 자동, 단 기존 SO 일괄 X

**채택: (a)**.
- 메뉴: `Tools > LostMemory > Apply Rarity-based Sizes`
- 옵션: "Lock 된 항목 건너뛰기" 체크박스
- 결과 로그: "75개 SO 중 73개 갱신, 2개 Lock 건너뜀"

### 3. Lock 플래그 — 디자이너 수동 오버라이드 보호

```csharp
[Tooltip("체크 시 등급 자동 매핑에서 제외 (수동 사이즈 유지).")]
[SerializeField] private bool _sizeLocked;
```

회의록에 없는 변형 사이즈 (예: 검 무기가 1×3) 가 향후 생길 수 있어 보호장치.

### 4. RelicData 헬퍼 프로퍼티

```csharp
public int Width  => _size.x;
public int Height => _size.y;
public int OccupiedCells => _size.x * _size.y;
public bool IsSizeLocked => _sizeLocked;
```

→ CL-151 의 자동 배치 알고리즘이 직접 사용.

### 5. 런타임 검증 — 인벤토리 max 사이즈 초과 방지

`PlayerRelicInventory.TryAdd` 시 사이즈 검증:
- `Width > MaxColumns` 또는 `Height > MaxRows` → reject + 로그
- 5×5 인벤토리에 (3, 1) 사이즈 → OK (가로 3, 세로 1)
- 5×5 인벤토리에 (6, 1) → reject

본 CL에서는 **검증 hook 만 추가** (TryAdd 안에 if 한 줄). 실제 배치는 CL-151.

```csharp
// PlayerRelicInventory.TryAdd 수정
public bool TryAdd(RelicData relic)
{
    if (relic.Width > MaxColumns || relic.Height > MaxRows)
    {
        Debug.LogWarning($"[CL-150] {relic.name} 사이즈 ({relic.Width}×{relic.Height}) 가 인벤토리 ({MaxColumns}×{MaxRows}) 초과");
        return false;
    }
    // 기존 로직
}
```

### 6. 매핑 유틸 — `RelicSizeMapping` 정적 클래스

```csharp
public static class RelicSizeMapping
{
    public static Vector2Int GetDefaultSize(RelicRarity rarity) => rarity switch
    {
        RelicRarity.Common    => new Vector2Int(1, 1),
        RelicRarity.Rare      => new Vector2Int(2, 1),
        RelicRarity.Unique    => new Vector2Int(2, 2),
        RelicRarity.Legendary => new Vector2Int(2, 2),
        _ => new Vector2Int(1, 1),
    };
}
```

→ CL-138 의 부록 매핑표와 1:1.

---

## 핵심 파일

### 신규

| 경로 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Relics/RelicSizeMapping.cs` | 등급 → Vector2Int 정적 매핑 |
| `Assets/_Project/Scripts/Editor/Relics/RelicSizeApplyMenu.cs` | Editor 일괄 적용 메뉴 |

### 수정

| 경로 | 변경 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Relics/RelicData.cs` | `_sizeLocked` 필드 + `Width`/`Height`/`OccupiedCells`/`IsSizeLocked` 프로퍼티 |
| `Assets/_Project/Scripts/Runtime/Relics/PlayerRelicInventory.cs` | `TryAdd` 사이즈 검증 (2줄) |

### 영향 (메뉴 실행 후 75+ 개 .asset 파일 변경)

| 경로 | 변경 |
|---|---|
| `Assets/_Project/ScriptableObjects/Relics/*.asset` | _size 필드값 등급에 따라 갱신 |

---

## 구현 단계

### 1단계: RelicSizeMapping 유틸 (10분)

`RelicSizeMapping.cs` 작성. switch expression 4 케이스만.

### 2단계: RelicData 헬퍼 + Lock 필드 (15분)

```csharp
[Tooltip("체크 시 등급 자동 매핑에서 제외 (수동 사이즈 유지).")]
[SerializeField] private bool _sizeLocked;

public int Width  => _size.x;
public int Height => _size.y;
public int OccupiedCells => _size.x * _size.y;
public bool IsSizeLocked => _sizeLocked;
```

### 3단계: Editor 일괄 적용 메뉴 (30분)

```csharp
public static class RelicSizeApplyMenu
{
    [MenuItem("Tools/LostMemory/Apply Rarity-based Sizes")]
    public static void ApplyAll()
    {
        var guids = AssetDatabase.FindAssets("t:RelicData");
        int updated = 0, locked = 0;
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<RelicData>(path);
            if (so.IsSizeLocked) { locked++; continue; }

            var newSize = RelicSizeMapping.GetDefaultSize(so.Rarity);
            // SerializedObject 로 _size 필드 직접 갱신
            var sObj = new SerializedObject(so);
            sObj.FindProperty("_size").vector2IntValue = newSize;
            sObj.ApplyModifiedProperties();
            EditorUtility.SetDirty(so);
            updated++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[CL-150] {updated}개 SO 갱신, {locked}개 Lock 건너뜀");
    }
}
```

### 4단계: PlayerRelicInventory.TryAdd 검증 hook (5분)

§5 코드 그대로. `MaxColumns` / `MaxRows` 가 아직 없으면 상수 5/5 로 임시.

### 5단계: 메뉴 실행 + 검증 (30분)

1. Unity 열고 `Tools > LostMemory > Apply Rarity-based Sizes` 클릭
2. Console: "75개 SO 갱신, 0개 Lock 건너뜀" 확인
3. RelicData 몇 개 샘플로 Inspector 확인:
   - 일반 등급: _size = (1, 1)
   - 레어 등급: _size = (2, 1)
   - 유니크/전설: _size = (2, 2)
4. Lock 테스트:
   - 한 SO에 _sizeLocked = true 체크 + _size 수동 변경 (3, 1)
   - 메뉴 다시 실행
   - 해당 SO 의 _size 가 (3, 1) 유지되는지 확인
   - Console: "1개 Lock 건너뜀"

### 6단계: 런타임 검증 (15분)

```
시나리오 1: 정상 추가
- (2, 2) 사이즈 유물 → TryAdd 성공

시나리오 2: 초과 사이즈
- (6, 1) 임시 SO → TryAdd false + LogWarning

시나리오 3: 5칸 짜리 (경계)
- (5, 1) → 가로 정확히 채움 → 성공
```

---

## 위험 / 결정 미정

### 위험

1. **75개 SO 갱신 git diff 폭증**: meta 변경 + asset 변경. PR review 부담. → **별도 commit 으로 분리** ("CL-150: apply rarity sizes" 단독).
2. **Lock 잊은 디자이너 의도 파괴**: 수동 사이즈 작업한 SO 가 _sizeLocked=false 상태이면 메뉴 1번에 덮어씌워짐. → **메뉴 실행 전 dry-run 옵션** 추가? 또는 GitLog 으로 복구. 본 plan 은 dry-run 미포함 (디자이너에게 Lock 사용 안내).
3. **Vector2Int 직렬화 호환**: CL-138 에서 이미 Vector2Int 추가했으므로 호환. 단 CL-138 후 SO 가 적용되었다면 디폴트 (1,1) 인 채로 75개 모두 있을 가능성.
4. **MaxColumns/MaxRows 가 PlayerRelicInventory 에 없을 가능성**: CL-110 시점에 없을 수 있음. → 임시 상수 5×5 + 후속 ticket 에서 도입 (또는 본 CL 에서 추가).
5. **Vector2Int x=가로, y=세로 컨벤션 일관성**: CL-151 의 자동 배치 알고리즘과 동일 컨벤션 확인 필요. 본 plan: **x=가로(width), y=세로(height)**.

### 결정 미정

- [ ] Rare 사이즈 (2,1) vs (1,2) — 본 plan: **(2,1) 가로 우선**
- [ ] Unique/Legendary 차별 — 본 plan: **둘 다 (2,2)** (회의록)
- [ ] 메뉴 실행 시 dry-run — 본 plan: **미포함** (Git 복구 의존)
- [ ] OnValidate 자동 적용 추가 — 본 plan: **미포함** (Lock 보호 + 명시적 trigger 우선)
- [ ] PlayerRelicInventory 에 MaxColumns/MaxRows 도입 — 본 plan: **임시 상수 5×5 + TODO**

---

## 후속 ticket 영향

| Ticket | CL-150 과의 관계 |
|---|---|
| **CL-151 (자동 배치)** | 본 CL 의 `Width`/`Height`/`OccupiedCells` 를 직접 사용. Top-left first fit 알고리즘이 다중 칸 점유 처리 |
| **CL-148 (HUD 인벤토리)** | 본 CL 의 사이즈가 HUD 슬롯 점유에 영향. 단 CL-148 은 단순 1×1 가정으로 만들어졌으므로 **CL-151 완성 후 HUD 갱신** 필요 |
| **CL-153 (QA)** | 다양 사이즈 인벤토리 동작 검증 |

---

## 예상 시간

| 단계 | 시간 |
|---|---|
| 1단계 (RelicSizeMapping) | 10분 |
| 2단계 (RelicData 헬퍼) | 15분 |
| 3단계 (Editor 메뉴) | 30분 |
| 4단계 (TryAdd 검증) | 5분 |
| 5단계 (메뉴 실행 + Inspector 확인) | 30분 |
| 6단계 (런타임 검증) | 15분 |
| **합계** | **약 1시간 45분** |

→ 2점 ticket 에 부합 (단순 helper + 일괄 적용).

---

## 결정 요청

| # | 질문 | 옵션 | 추천 |
|---|---|---|---|
| 1 | Rare 사이즈 방향 | (2,1) 가로 / (1,2) 세로 | **(2,1)** |
| 2 | 메뉴 실행 commit 분리 | 본 작업과 합침 / 별도 commit | **별도** (review 편의) |
| 3 | OnValidate 자동 적용 추가 | 추가 / 미추가 | **미추가** (Lock 우선) |
| 4 | PlayerRelicInventory MaxColumns/MaxRows 신설 | 본 CL / 별도 ticket | **본 CL** (임시 상수 5/5) |
| 5 | dry-run 옵션 | 추가 / 미추가 | **미추가** (Git 복구 의존) |

전부 추천대로면 **(2,1) + 별도 commit + OnValidate 미추가 + Max 상수 + dry-run 미추가**.

---

## Phase 4 진행률 (CL-150 후)

| Ticket | Plan |
|---|---|
| CL-148 인벤토리 + 진척 UI | ✅ |
| CL-149 아이템 툴팁 | ⏳ |
| **CL-150 사이즈 시스템** | ✅ ← 방금 |
| CL-151 자동 배치 + 정리 | ⏳ |

**Phase 4: 2/4**

---

## 다음 plan

| 옵션 | Ticket | 점수 | 비고 |
|---|---|---|---|
| **A** | CL-151 자동 배치 | 4점 | 본 CL 직접 후속. Top-left first fit |
| B | CL-149 툴팁 | 2점 | UX 개선, CL-148 의 hover 활용 |
| C | CL-152 보상/상점 통합 | 2점 | Phase 5 |

**추천: A (CL-151)**. 본 CL 의 사이즈 정의가 CL-151 의 직접 입력. 흐름상 자연스러움.

뭐로 갈까요?
