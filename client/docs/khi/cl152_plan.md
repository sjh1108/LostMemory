# CL-152 — 보상/상점 풀 ItemData 연결 + 후속 hook 통합

## Context

Epic S Phase 5의 첫 ticket. **75 ItemData 일괄 등록 + Phase 3/4의 후속 hook 마무리**.

### 기존 상태 (확인 완료)

- ✅ `RewardPool` SO — `_allRewards: RelicData[]` 단일 배열, `DrawThree`/`DrawOneRelicForShop` 두 진입점 (소스: `Rewards/RewardPool.cs`)
- ✅ `ShopGenerator.Generate(inventory, rewardPool, config)` — 동적 ShopData 생성, 유물 3슬롯 + 비유물 1슬롯
- ✅ **보상/상점 풀 이미 통합** — 같은 `RewardPool._allRewards` 공유. Reward는 `DrawThree(owned)`, Shop은 `DrawOneRelicForShop(excluded, weights)`
- ❌ 75 ItemData (CL-141 산출물) 가 풀에 모두 등록되어 있는지 — **본 CL 핵심**
- ❌ CL-146 / CL-147 / CL-151 plan 에 명시된 hook 들 마무리 — **본 CL 통합**

### 본 CL 책임 범위 (3축)

1. **데이터 연결**: CL-141 의 75개 ItemData SO → `RewardPool._allRewards` 일괄 등록
2. **Phase 3/4 후속 hook 마무리**:
   - CL-146: `LuckPoints` 가중치 / `ForceLegendary` flag / `PicksAllowed` 다중 선택
   - CL-147: 별 카드의 `AddBonusPick(1)` (다음 보상 +1장)
   - CL-151: overflow 토스트 (인벤토리 가득 참 시 보상 거부 알림)
3. **검증**: 보상/상점 흐름이 75 풀에서 정상 작동, 등급 분포 검증

→ **2점 ticket**: 데이터 연결 자체는 단순. Hook 통합 + 검증이 비중 큼.

---

## 결정사항

### 1. 75 ItemData 등록 방식 — Editor 일괄 메뉴

**옵션**:
- (a) Inspector 에서 수동 드래그 75개 (오류 위험)
- (b) **Editor 메뉴 자동 수집** ⭐ — 폴더 스캔 + 자동 등록
- (c) 런타임 `Resources.LoadAll<RelicData>()` (런타임 비용 + Resources 폴더 의존)

**채택: (b)**.

```csharp
[MenuItem("Tools/LostMemory/Populate RewardPool from Folder")]
public static void Populate()
{
    var pool = AssetDatabase.LoadAssetAtPath<RewardPool>(
        "Assets/_Project/ScriptableObjects/Rewards/RewardPool.asset");

    var guids = AssetDatabase.FindAssets("t:RelicData",
        new[] { "Assets/_Project/ScriptableObjects/Relics" });
    var relics = guids
        .Select(g => AssetDatabase.LoadAssetAtPath<RelicData>(AssetDatabase.GUIDToAssetPath(g)))
        .Where(r => r != null)
        .ToArray();

    var sObj = new SerializedObject(pool);
    var arr = sObj.FindProperty("_allRewards");
    arr.arraySize = relics.Length;
    for (int i = 0; i < relics.Length; i++)
        arr.GetArrayElementAtIndex(i).objectReferenceValue = relics[i];
    sObj.ApplyModifiedProperties();
    EditorUtility.SetDirty(pool);
    AssetDatabase.SaveAssets();
    Debug.Log($"[CL-152] RewardPool 에 {relics.Length}개 ItemData 등록");
}
```

→ CL-150 의 size 적용 메뉴와 동일 패턴. 디자이너가 새 ItemData 추가 시 메뉴 한 번 더 클릭.

### 2. CL-146 LuckPoints 가중치 통합

CL-146 plan 의 행운 1스택 hook (RewardPool 가중치 +행운 태그 보너스):

```csharp
public List<RelicData> DrawThree(IEnumerable<string> ownedRelicNames, int luckPoints = 0)
{
    var owned = new HashSet<string>(ownedRelicNames);
    var available = _allRewards
        .Where(r => r.IsConsumable || !owned.Contains(r.name))
        .ToList();
    return PickWeighted(available, 3, luckPoints);
}

private List<RelicData> PickWeighted(List<RelicData> pool, int count, int luckPoints)
{
    // 기존 로직 + GetWeight(item, luckPoints)
}

private static int GetWeight(RelicData data, int luckPoints)
{
    int baseW = data.IsConsumable ? ConsumableWeight : RarityWeights[data.Rarity];
    if (luckPoints > 0 && HasLuckTag(data))
        baseW += luckPoints * 2;   // 행운 1당 +2 가중치 (CL-146 §5-1)
    return baseW;
}

private static bool HasLuckTag(RelicData r) =>
    r.TagPrimary == RelicTag.Luck || r.TagSecondary == RelicTag.Luck;
```

**호출 측 (RewardController)**:
```csharp
int luck = buildManager.GetSetCount(RelicTag.Luck);
var draws = rewardPool.DrawThree(inventory.GetOwnedNames(), luck);
```

`buildManager.GetSetCount(tag)` 가 없으면 신설 필요 (CL-139 후속).

### 3. CL-146 ForceLegendary flag

```csharp
public bool ForceLegendary { get; set; }

public List<RelicData> DrawThree(IEnumerable<string> ownedRelicNames, int luckPoints = 0)
{
    var owned = new HashSet<string>(ownedRelicNames);
    var available = _allRewards
        .Where(r => r.IsConsumable || !owned.Contains(r.name))
        .ToList();

    if (ForceLegendary)
        available = available.Where(r => r.Rarity == RelicRarity.Legendary).ToList();

    return PickWeighted(available, 3, luckPoints);
}
```

**호출 측**:
```csharp
rewardPool.ForceLegendary = (luck >= 7);
var draws = rewardPool.DrawThree(...);
rewardPool.ForceLegendary = false;   // 1회용
```

→ flag 가 SO 에 있어서 1회용 토글 위험. **개선**: 메서드 파라미터로 전달 (`DrawThree(owned, luck, forceLegendary)`).

```csharp
public List<RelicData> DrawThree(IEnumerable<string> ownedRelicNames,
                                 int luckPoints = 0,
                                 bool forceLegendary = false)
```

→ **채택: 파라미터 방식** (안전).

### 4. CL-146 PicksAllowed (행운 5스택 다중 선택)

`RewardController.cs` 에 `PicksAllowed` 신설:

```csharp
public class RewardController : MonoBehaviour
{
    public int PicksAllowed { get; private set; } = 1;
    private int _picksMade;

    public void StartReward()
    {
        _picksMade = 0;
        int luck = buildManager.GetSetCount(RelicTag.Luck);
        PicksAllowed = ResolvePicksAllowed(luck);
        // 카드 표시
        int cardCount = (luck >= 5) ? 5 : 3;
        var draws = rewardPool.DrawThree(...);  // 또는 DrawN(cardCount, ...)
        // 5장 표시는 DrawThree 가 3개만 반환하므로 별도 DrawN 필요
    }

    private static int ResolvePicksAllowed(int luck)
    {
        if (luck >= 5) return 2;
        return 1;
    }

    private void OnCardSelected(RelicData picked)
    {
        inventory.TryAdd(picked);
        _picksMade++;
        if (_picksMade >= PicksAllowed) ClosePanel();
    }
}
```

**RewardPool 추가 메서드**:
```csharp
public List<RelicData> DrawN(IEnumerable<string> owned, int n, int luckPoints = 0, bool forceLegendary = false)
{
    // DrawThree 일반화: count = n
}
```

→ DrawThree 는 `DrawN(owned, 3, ...)` 의 wrapper 로 유지.

### 5. CL-147 별 카드 — AddBonusPick

별 카드 효과 = "다음 보상 카드 +1장". CL-147 plan 에서는 `RewardPool.AddBonusPick(1)` 또는 `RewardController.PicksAllowed +1` hook 재사용 명시.

**채택: PicksAllowed +1 재사용**. 별도 메서드 X.

```csharp
// StarCard.Activate(ctx)
public void Activate(TarotContext ctx)
{
    rewardController.QueueBonusPick(1);  // 다음 보상에 +1
}

// RewardController
private int _bonusPicks;
public void QueueBonusPick(int amount) => _bonusPicks += amount;

private static int ResolvePicksAllowed(int luck) { ... }   // luck 기반 base

public void StartReward()
{
    int basePicks = ResolvePicksAllowed(luck);
    PicksAllowed = basePicks + _bonusPicks;
    _bonusPicks = 0;   // 1회 소모
}
```

### 6. CL-151 overflow 토스트

`RewardController.OnCardSelected` 에서 `inventory.TryAdd(picked)` false 시 토스트:

```csharp
private void OnCardSelected(RelicData picked)
{
    if (!inventory.TryAdd(picked))
    {
        ToastNotifier.Show("인벤토리가 가득 찼습니다 — 정리 후 다시 선택해주세요");
        return;
    }
    _picksMade++;
    if (_picksMade >= PicksAllowed) ClosePanel();
}
```

`ToastNotifier` 가 없으면 placeholder (`Debug.LogWarning` + 임시 UI). 정식 토스트 컴포넌트는 별도 ticket.

상점 측도 동일:
```csharp
// ShopController.OnPurchase
if (!inventory.TryAdd(item.Relic))
{
    ToastNotifier.Show("인벤토리 가득 참");
    return;   // 골드 차감 안 됨
}
```

### 7. ShopPanelView 다중 사이즈 표시 — 본 CL 범위 외

CL-151 의 후속에서 명시된 항목. 본 plan 결정:
- 상점은 1×1 표시 유지 (구매 후 인벤토리에서만 사이즈 표시)
- 다중 사이즈 표시는 **별도 ticket** (UX 폴리싱)

→ 본 CL 은 **데이터 + 토스트 + Phase3 hook 통합** 만.

### 8. 상점 풀 분리 옵션 — 본 CL 결정 X

회의록 / items_draft 에 "상점만 등장하는 아이템" 같은 구분 없음. 현재 통합 풀 그대로 유지. 향후 분리 필요 시 별도 ticket.

### 9. 75 ItemData 등급 분포 검증

CL-141 산출물 등급 분포 (예상):
- Common: 약 46개
- Rare: 약 19개
- Unique: 약 5개
- Legendary: 약 5개
- (총 75)

`RarityWeights` 60/25/10/5 와 분포 일치 → 등급 가중치가 ItemData 수와 결합되어 자연스러운 추첨 분포 형성. 등록 후 통계 디버그 메뉴로 검증.

```csharp
[MenuItem("Tools/LostMemory/Audit RewardPool Distribution")]
public static void Audit()
{
    var pool = AssetDatabase.LoadAssetAtPath<RewardPool>("...");
    var counts = pool._allRewards.GroupBy(r => r.Rarity).ToDictionary(g => g.Key, g => g.Count());
    foreach (var (k, v) in counts)
        Debug.Log($"  {k}: {v}");
}
```

---

## 핵심 파일

### 신규

| 경로 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Editor/Rewards/RewardPoolPopulateMenu.cs` | 75 ItemData 일괄 등록 메뉴 + 분포 audit |
| `Assets/_Project/Scripts/Runtime/UI/ToastNotifier.cs` | placeholder 토스트 (정식은 후속) |

### 수정

| 경로 | 변경 |
|---|---|
| `RewardPool.cs` | `DrawThree(owned, luckPoints, forceLegendary)` / `DrawN(owned, n, ...)` 시그니처 확장, `LuckPoints` 가중치, `HasLuckTag` 헬퍼 |
| `RewardController.cs` | `PicksAllowed` / `_bonusPicks` / `QueueBonusPick(int)`, luck 기반 카드 수 결정 (3 vs 5), 다중 선택 흐름, overflow 토스트 |
| `BuildManager.cs` (CL-139) | `GetSetCount(RelicTag tag)` public 메서드 신설 (없으면) |
| `ShopController.cs` | 구매 시 `inventory.TryAdd` 결과 검증 + overflow 토스트 |
| `Rewards/RewardPool.asset` | 75개 ItemData 등록 (메뉴 실행 결과) |

### 영향 (메뉴 실행 결과)

| 경로 | 변경 |
|---|---|
| `RewardPool.asset` | _allRewards 배열에 75개 ref 추가 |

---

## 구현 단계

### 1단계: RewardPoolPopulateMenu (30분)

§1 코드 작성. 메뉴 실행 → 75개 등록 + 분포 로그.

### 2단계: RewardPool 확장 (30분)

1. `DrawThree(owned, luckPoints=0, forceLegendary=false)` 시그니처 변경
2. 기존 호출 측 컴파일 안전 (디폴트 파라미터)
3. `DrawN(owned, n, luckPoints, forceLegendary)` 신설, DrawThree 는 `DrawN(owned, 3, ...)` 호출
4. `HasLuckTag` private 헬퍼
5. `PickWeighted` 가중치 계산에 luckPoints 반영

### 3단계: BuildManager.GetSetCount (15분)

```csharp
public int GetSetCount(RelicTag tag)
{
    return _setCounts.TryGetValue(tag, out var c) ? c : 0;
}
```

이미 존재하면 skip.

### 4단계: RewardController 통합 (1시간)

1. `PicksAllowed` / `_bonusPicks` / `_picksMade` 필드
2. `StartReward`:
   - luck 조회 → 카드 수 (3 또는 5)
   - forceLegendary 결정 (luck ≥ 7)
   - DrawN 호출
   - PicksAllowed = base + _bonusPicks (별 카드 보너스 합산)
   - _bonusPicks = 0 (1회 소모)
3. `OnCardSelected`:
   - TryAdd 검증 → 실패 시 토스트
   - _picksMade++ → ≥ PicksAllowed 시 ClosePanel
4. `QueueBonusPick(int)` public 메서드 (StarCard 가 호출)

### 5단계: ToastNotifier placeholder (15분)

```csharp
public static class ToastNotifier
{
    public static void Show(string msg, float duration = 2.5f)
    {
        Debug.LogWarning($"[TOAST] {msg}");
        // TODO: 정식 UI ticket 후 교체
    }
}
```

→ 정식 UI는 별도 ticket. 본 CL 은 hook 만.

### 6단계: ShopController overflow 처리 (15분)

§6 코드 그대로. 구매 흐름에서 TryAdd 결과 확인 + 골드 환불 X (애초 차감 X).

### 7단계: 메뉴 실행 + 분포 audit (15분)

1. `Tools > LostMemory > Populate RewardPool from Folder` 클릭
2. 콘솔: "75개 등록"
3. `Tools > LostMemory > Audit RewardPool Distribution` 클릭
4. 콘솔에 등급 분포 출력 → CL-141 산출물과 일치 확인

### 8단계: 검증 (1.5시간)

```
시나리오 1: 보상 흐름 정상
- 방 클리어 → RewardPanel 표시
- 3장 카드 표시, 모두 다른 ItemData
- 1장 선택 → inventory 추가, 패널 닫힘

시나리오 2: 상점 흐름 정상
- 상점 진입 → 유물 3장 + 비유물 1장 표시
- 보유 중인 유물은 등장 X

시나리오 3: 행운 5스택
- luck = 5 디버그 강제
- 보상 패널에 5장 표시
- 1장 선택 → 1장 disable, 4장 중 1장 더 선택 가능
- 2장 선택 후 패널 닫힘

시나리오 4: 행운 7스택
- luck = 7 디버그 강제
- 보상 3장 모두 Legendary

시나리오 5: 행운 1스택 가중치
- luck = 1 디버그
- 100회 추첨 디버그 메뉴로 LuckTag 아이템 등장 빈도 측정
- luck 0 vs 1 비교 → LuckTag 아이템 빈도 증가 확인

시나리오 6: 별 카드 보너스
- StarCard 강제 발동 → QueueBonusPick(1)
- 다음 보상 → 4장 표시 (3 + 1)

시나리오 7: 인벤토리 가득 참
- 5×5 모두 채움
- 보상 1장 선택 → TryAdd false → 토스트 표시 + 패널 유지
- 정리 후 재선택 가능

시나리오 8: 상점 가득 참
- 5×5 모두 채움
- 상점 구매 → TryAdd false → 토스트 + 골드 차감 X

시나리오 9: 분포 audit
- 메뉴 실행 → 등급 분포 콘솔 출력
- CL-141 산출물과 일치 (Common 46 / Rare 19 / Unique 5 / Legendary 5)
```

---

## 위험 / 결정 미정

### 위험

1. **CL-146 / CL-147 의 hook 들이 본 CL 에 몰림**: CL-146/147 plan 에서 "RewardPool/RewardController 수정" 명시했지만 실제 구현은 본 CL 에서. → CL-146/147 작업 시 hook 만들고 placeholder 처리, 본 CL 에서 통합 마무리. **plan 간 책임 경계 명확화**.
2. **`buildManager.GetSetCount(RelicTag)` 메서드 미존재 가능**: CL-139 BuildManager 가 OnSetTierChanged 이벤트만 노출하고 카운트 직접 조회 안 될 수 있음. → 본 CL 에서 신설.
3. **DrawThree 시그니처 변경 → 호출 측 컴파일 영향**: 디폴트 파라미터로 안전. 단 모든 호출 위치 확인 필요 (Grep 으로 DrawThree 호출 확인).
4. **75개 등록 메뉴 실행 시 git diff**: RewardPool.asset 메타 변경. 별도 commit 권장 (CL-150 패턴과 동일).
5. **새 ItemData 추가 시 메뉴 재실행 잊음**: 자동화 옵션 — 디자이너가 잊을 수 있음. 본 plan 은 명시적 메뉴 권장 + AssetPostprocessor 자동 등록 옵션은 후속.
6. **5장 표시 UI**: RewardPanelView 가 3장 고정으로 되어 있을 가능성. 5장 동적 표시 필요. → 본 plan 은 RewardCardView 인스턴스 동적 생성 가정.
7. **bonusPicks 누적 정책**: 별 카드 → 다음 보상 +1. 보상 수령 안 한 채로 별 카드 또 발동 시 +2 누적? 1회만? **본 plan: 누적 OK + 1회 보상 시 일괄 소모**.
8. **소모품 (포션/랜덤박스) 풀 등록 정책**: ItemData 폴더에 포션이 같이 있으면 메뉴가 모두 등록 → 보상에서 포션이 유물 슬롯에 등장. → **메뉴 필터: `IsConsumable` 도 등록**. 기존 `DrawThree` 가 소모품 가중치 처리 (ConsumableWeight=5).

### 결정 미정

- [ ] DrawThree 호출 측 일괄 갱신 — 본 plan: **디폴트 파라미터** (호환 유지)
- [ ] 메뉴 자동화 (AssetPostprocessor) — 본 plan: **수동 메뉴만** (명시적)
- [ ] ToastNotifier 정식 UI — 본 plan: **placeholder + 별도 ticket**
- [ ] 상점 다중 사이즈 표시 — 본 plan: **별도 ticket**
- [ ] bonusPicks 누적 정책 — 본 plan: **누적 + 1회 일괄 소모**
- [ ] 5장 동적 RewardPanelView — 본 plan: **본 CL 포함** (CL-146 의 5장중2장 핵심)
- [ ] 상점 풀 분리 — 본 plan: **현재 통합 유지** (별도 ticket 안 함)

---

## 후속 ticket 영향

| Ticket | CL-152 와의 관계 |
|---|---|
| **CL-153 (1차 통합 QA)** | 본 CL 의 75 풀 + Phase 3 hook 들 실 플레이 검증 |
| **CL-154 (V0.3 컷 + 수치 조정)** | 본 CL 의 분포 audit 결과 → 컷 후보 식별 (items_draft V0.4 §컷 후보) |
| **별도 ticket: ToastNotifier 정식 UI** | placeholder 교체 |
| **별도 ticket: 상점 ShopPanelView 다중 사이즈** | CL-151 의 size 표시를 상점에도 |
| **별도 ticket: AssetPostprocessor 자동 풀 등록** | 새 ItemData 추가 시 자동 |

---

## 예상 시간

| 단계 | 시간 |
|---|---|
| 1단계 (RewardPoolPopulateMenu) | 30분 |
| 2단계 (RewardPool 확장) | 30분 |
| 3단계 (BuildManager.GetSetCount) | 15분 |
| 4단계 (RewardController 통합) | 1시간 |
| 5단계 (ToastNotifier placeholder) | 15분 |
| 6단계 (ShopController overflow) | 15분 |
| 7단계 (메뉴 실행 + audit) | 15분 |
| 8단계 (검증 9 시나리오) | 1시간 30분 |
| **합계** | **약 4시간 30분** |

→ 2점 ticket 표 보다 큼 (Phase 3 hook 통합이 비중 큼). 실제로 **3점급**. 단순 데이터 연결만이면 1시간이지만 hook 통합 포함이라 길어짐.

→ **분리 옵션**: 본 CL 을 **A (데이터 + 토스트, 1.5h)** + **B (Phase 3 hook 통합, 3h)** 로 plan 단위 분할. ticket 은 단일 유지.

---

## 작업 단위 가이드 (Plan 분할)

### 작업 단위 A — 데이터 + overflow (1.5시간)
- RewardPoolPopulateMenu
- 메뉴 실행 + audit
- ToastNotifier placeholder
- RewardController / ShopController overflow 토스트 hook
- 75 등록 git commit 분리

### 작업 단위 B — Phase 3 hook 통합 (3시간)
- RewardPool.DrawN / luckPoints / forceLegendary
- BuildManager.GetSetCount
- RewardController PicksAllowed / luck 기반 카드 수 / bonusPicks
- StarCard.QueueBonusPick 호출 검증

### 검증 (1시간)
- 9 시나리오

---

## 결정 요청

| # | 질문 | 옵션 | 추천 |
|---|---|---|---|
| 1 | 메뉴 자동화 (AssetPostprocessor) | 본 CL / **별도** | **별도** (명시적 trigger) |
| 2 | ToastNotifier 정식 UI | 본 CL / **별도** | **별도** (placeholder 충분) |
| 3 | 상점 다중 사이즈 표시 | 본 CL / **별도** | **별도** |
| 4 | DrawThree 시그니처 정책 | 디폴트 파라미터 / 별도 메서드 | **디폴트 파라미터** (호환) |
| 5 | bonusPicks 누적 | 누적 / 1회 덮어쓰기 | **누적** |
| 6 | 5장 동적 RewardPanelView | **본 CL** / 별도 | **본 CL** (CL-146 핵심) |
| 7 | 75 등록 commit 분리 | 분리 / 합침 | **분리** (CL-150 패턴) |

전부 추천대로면 **자동화별도 + 토스트별도 + 상점별도 + 디폴트파라미터 + 누적 + 5장본CL + 분리커밋**.

---

## Phase 5 진행률 (CL-152 후)

| Ticket | Plan |
|---|---|
| **CL-152 보상/상점 풀 연결** | ✅ ← 방금 |
| CL-153 1차 통합 QA | ⏳ |
| CL-154 V0.3 컷 + 수치 조정 | ⏳ |

**Phase 5: 1/3**

---

## 다음 plan

| 옵션 | Ticket | 점수 | 비고 |
|---|---|---|---|
| **A** | CL-153 1차 통합 QA | 5점 | plan 보다 실 플레이 비중 |
| B | CL-154 V0.3 컷 + 수치 조정 | 3점 | CL-153 결과 기반 (선후관계 있음) |
| C | Epic K 무기 확장 진입 (CL-155) | - | Epic S 나간 후 다른 Epic |

**추천: A (CL-153)** — 본 CL 통합 직후 통합 QA 자연스러움. CL-154 는 CL-153 결과 의존이라 그 다음.

뭐로 갈까요?
