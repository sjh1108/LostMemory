# CL-152 보상/상점 풀 ItemData 연결 + overflow 토스트 — 구현 기록

작성일: 2026-05-06

브랜치: `feat/S14P31C201-?/cl-152-보상상점-풀-itemdata-연결`

기준 plan: [cl152_plan.md](cl152_plan.md), 선행 ticket 구현 기록: [cl151_implementation.md](cl151_implementation.md)

**상태**: 🟢 **검증 완료** — RewardPool 95개 일괄 등록 + 분포 audit + OnTryAddRejected 이벤트 + ToastNotifier(Bridge) hook + **CL-146 RewardPanelView 호출 순서 버그 fix** 모두 통과. **Phase 5 P1 완료**.

---

## 목적

Epic S Phase 5 의 첫 ticket — **75 (실제 95) ItemData 풀 등록 + overflow 토스트** (P1).

원본 plan 의 8단계 중 **거의 모두 선행 ticket 에서 이미 완료**:
- ✅ `RewardPool.DrawCount(count, owned, luckPoints, forceLegendary)` — CL-146 완료
- ✅ `RewardController` PicksAllowed / luck 기반 카드 수 / forceLegendary — CL-146 완료
- ✅ `BuildManager.GetTagCount` / `GetActiveTier` — 이미 존재
- ✅ `HasLuckTag` / `RarityWeights` / `ConsumableWeight` — 이미 존재
- ❌ CL-147 별 카드 — 본 CL-147 단순화 (3장: Death/Healing/Reroll) 로 별 카드 자체 없음 → §5 StarCard 작업 불필요

**본 CL 실제 작업** (대폭 축소):
1. 75 (실제 95) ItemData → RewardPool 일괄 등록 메뉴 (Dry Run + Apply)
2. 분포 audit 메뉴
3. `PlayerRelicInventory.OnTryAddRejected` 이벤트 신설 (노소연 침범 회피)
4. `ToastNotifier` placeholder + `ToastNotifierBridge` hook 컴포넌트
5. **(검증 중 발견) CL-146 RewardPanelView 호출 순서 버그 fix** — picksAllowed=1 시 패널 닫혀도 timeScale 복구 안 되는 영구 정지 버그
6. **(검증 중 발견) RewardPool.GetWeight KeyNotFoundException 안전화**

---

## 설계 기준 + 사용자 결정

### Plan 단계 결정 사항

| # | 항목 | 결정 |
|---|---|---|
| 1 | overflow 토스트 hook 방식 | 옵션 A — `PlayerRelicInventory.OnTryAddRejected` 이벤트 + `ToastNotifierBridge` 컴포넌트. 노소연 침범 X |
| 2 | Editor 메뉴 path | `LostMemory/Relics/...` (CL-150 RelicSizeApplyMenu 일관) |
| 3 | 등록 메뉴 정책 | Dry Run + Apply 2개 (CL-150 패턴) |
| 4 | ToastNotifier UI | placeholder (`Debug.LogWarning`). 정식 토스트 별도 ticket |
| 5 | 자동화 (AssetPostprocessor) | 별도 ticket (명시적 trigger 우선) |
| 6 | 상점 다중 사이즈 표시 | 별도 ticket (UX 폴리싱) |
| 7 | DrawThree 시그니처 | 디폴트 파라미터 그대로 (CL-146 완료) |
| 8 | bonusPicks 누적 (별 카드) | N/A — CL-147 단순화로 별 카드 자체 제거 |
| 9 | 5장 동적 RewardPanelView | N/A — CL-146 에서 이미 완료 |
| 10 | 95 등록 git commit | 별도 commit 권장 (CL-150 패턴) |

### 작업 중 발견·결정 사항

- **CL-146 RewardPanelView.OnCardSelected 호출 순서 버그 (검증 중 발견)**: `RewardSelected.Invoke` 가 `gameObject.SetActive(false)` 보다 **먼저** 호출 → RewardController.HandleRewardSelected 의 `if (gameObject.activeSelf)` 가드가 잘못 트리거 → 다중 픽 모드로 잘못 분기 → timeScale 복구 X → Player 영구 정지. CL-146 검증 시 picksAllowed=2 (다중 픽) 시나리오만 검증 + picksAllowed=1 (single pick) 누락. 본 CL 의 95개 풀 등록 후 정상 보상 진행 검증 시 발견. **수정**: SetActive / DisableSelectedCard 를 RewardSelected.Invoke 전에 처리.
- **RewardPool.GetWeight KeyNotFoundException 위험 (검증 중 발견)**: `RarityWeights[data.Rarity]` indexer 가 잘못된 Rarity enum 시 예외 → PickWeighted break → 카드 갯수 부족. 95개 풀 등록 후 사용자가 같은 SO 여러 번 등록한 케이스에서 카드 2개만 표시되는 증상 발생. **수정**: `TryGetValue` fallback (weight=1 + LogWarning).
- **메뉴 idempotent**: `existing.Contains(r.name)` 가드로 메뉴 다시 돌려도 중복 추가 X. CL-150 패턴 일관.
- **사용자 메뉴 path 인지 confusion**: `Populate RewardPool` (Apply) 와 `Populate RewardPool (Dry Run)` 둘 다 알파벳 정렬로 인접 표시 → 사용자가 처음에 Apply 메뉴를 못 찾음. 메뉴 보일 때 두 줄 다 확인 필요.

## 확정된 설계 결정

| 결정 | 값 | 근거 |
|---|---|---|
| OnTryAddRejected 이벤트 시그니처 | `Action<RelicData, string>` (relic + reason) | 사용자 메시지 구성 유연성 |
| 발화 케이스 | 사이즈 초과 / 그리드 공간 부족만 | 소모품/중복은 정상 흐름이라 발화 X |
| ToastNotifier | static class (placeholder) | 정식 UI 별도 ticket |
| ToastNotifierBridge 위치 | `Runtime/UI/` | InventoryCleanupButton 등과 동일 |
| RewardPoolPopulateMenu 패턴 | CL-141 RelicDataBatchGenerator + CL-150 RelicSizeApplyMenu | AssetDatabase.FindAssets 재사용 |
| Apply 정책 | Append 만 (기존 ref 유지) | 디자이너가 만든 순서 보존 |
| GetWeight fallback | `TryGetValue` + LogWarning + weight=1 | KeyNotFoundException 방지 + 진단 가능 |
| RewardPanelView OnCardSelected 순서 | SetActive → Invoke (CL-152 fix) | RewardController 가드 정확히 트리거 |

---

## 수정 파일

### 신규 (3)

```text
LostMemory/Assets/_Project/Scripts/Runtime/UI/ToastNotifier.cs
    - public static class ToastNotifier
    - Show(string msg, float duration = 2.5f) — Debug.LogWarning placeholder
    - 정식 UI ticket 후 Canvas 임시 Text 표시 또는 fade animation 추가 예정

LostMemory/Assets/_Project/Scripts/Runtime/UI/ToastNotifierBridge.cs
    - MonoBehaviour, [AddComponentMenu("Lost Memory/UI/Toast Notifier Bridge")]
    - 슬롯: PlayerRelicInventory inventory
    - OnEnable 에서 inventory.OnTryAddRejected 구독
    - HandleRejected → ToastNotifier.Show($"인벤토리 추가 실패: {name} ({reason})")

LostMemory/Assets/_Project/Scripts/Editor/Rewards/RewardPoolPopulateMenu.cs
    - public static class RewardPoolPopulateMenu
    - [MenuItem("LostMemory/Relics/Populate RewardPool (Dry Run)")] DryRun
    - [MenuItem("LostMemory/Relics/Populate RewardPool")] Apply
    - [MenuItem("LostMemory/Relics/Audit RewardPool Distribution")] Audit
    - AssetDatabase.FindAssets("t:RelicData", { RelicsFolder }) → existing 가드 → toAdd 만 append
    - SaveAssets + Refresh
```

### 수정 (3 — 본인 파일)

```text
LostMemory/Assets/_Project/Scripts/Runtime/Relics/PlayerRelicInventory.cs
    - 신규 이벤트: event Action<RelicData, string> OnTryAddRejected
    - TryAdd 의 reject 분기에서 발화:
        * 사이즈 초과 (CL-151) → reason="사이즈 초과"
        * 그리드 공간 부족 (CL-151) → reason="공간 부족"
        * 소모품/중복은 정상 흐름이라 발화 X

LostMemory/Assets/_Project/Scripts/Runtime/Rewards/RewardPanelView.cs (CL-146 버그 fix)
    - OnCardSelected 호출 순서 변경
        * before: TryAdd → _picksMade++ → RewardSelected.Invoke → SetActive(false)
        * after:  TryAdd → _picksMade++ → SetActive(false) (또는 DisableSelectedCard) → RewardSelected.Invoke
    - RewardController.HandleRewardSelected 의 activeSelf 가드가 정확히 분기되도록

LostMemory/Assets/_Project/Scripts/Runtime/Rewards/RewardPool.cs (GetWeight 안전화)
    - GetWeight: RarityWeights[indexer] → TryGetValue fallback
    - 잘못된 Rarity SO 도 weight=1 으로 추첨 가능 + LogWarning 으로 진단
```

### 영향 (메뉴 실행 결과)

| 경로 | 변경 |
|---|---|
| `Assets/_Project/ScriptableObjects/Reward/RewardPool.asset` | `_allRewards` 배열 19 → 95+ (메뉴 실행 후) |

### Wiring (사용자 Editor 작업 — 완료)

- 신규 GameObject `ToastNotifierBridge` 생성 (Stage 또는 UI Canvas 하위)
- `Lost Memory > UI > Toast Notifier Bridge` 컴포넌트 부착
- `Inventory` 슬롯에 Player 의 `PlayerRelicInventory` 드래그

### 재사용 (수정 X)

- `RewardPool.DrawCount(count, owned, luckPoints, forceLegendary)` — CL-146 완료
- `RewardController` PicksAllowed / forceLegendary / 다중 픽 — CL-146 완료
- `BuildManager.GetTagCount(RelicTag)` / `GetActiveTier(RelicTag)` — 이미 존재
- `RewardPanelView.OnCardSelected` — TryAdd 호출 (이벤트로 자동 토스트, 패널 코드 수정 X)
- `Shop/ShopPanelView.TryBuy` (노소연) — 그대로 (이벤트로 자동 토스트, 코드 수정 X)
- `RelicSizeApplyMenu` (CL-150) — 패턴 reference

---

## 발견·해소된 이슈

### 1. CL-146 RewardPanelView 호출 순서 버그 (검증 중 발견 + 수정)
**문제**: 보상 카드 1장 선택 후 Player 영구 정지. timeScale=0 / aim lock / 입력 차단 모두 유지.

**원인**: `OnCardSelected` 의 호출 순서:
```csharp
RewardSelected?.Invoke(selected);   // ← RewardController 호출
if (_picksMade >= _picksAllowed)
    gameObject.SetActive(false);    // ← 그 후에 SetActive(false)
```
RewardSelected 시점엔 패널 active → RewardController 의 `if (gameObject.activeSelf)` 가드가 "다중 픽 진행 중" 으로 잘못 분기 → timeScale 복구 X.

**해소**: SetActive(false) 또는 DisableSelectedCard 를 `RewardSelected.Invoke` 전으로 이동:
```csharp
bool willClose = _picksMade >= _picksAllowed;
if (willClose) gameObject.SetActive(false);
else DisableSelectedCard(selected);
RewardSelected?.Invoke(selected);   // ← 마지막
```

**왜 CL-146 에서 못 잡혔나**: CL-146 검증 시나리오는 행운 5스택 다중 픽만 확인 (picksAllowed=2 → 1번째 패널 유지 / 2번째 패널 닫힘). picksAllowed=1 (일반 보상) 시나리오 누락. 다중 픽에서는 우연히 작동했음 (1번째 픽 시 패널 active = 의도 일치).

### 2. RewardPool.GetWeight KeyNotFoundException 위험 (검증 중 발견 + 안전화)
**문제**: 95개 풀 등록 후 사용자가 같은 SO 여러 번 등록 → 풀에 의도치 않은 상태 → 보상 카드 갯수 부족 (3 → 2).

**원인 후보**:
- `RarityWeights[data.Rarity]` indexer — 잘못된 Rarity 시 KeyNotFoundException
- 같은 SO 여러 번 등록 → PickWeighted 의 totalWeight 계산 불일치

**해소**: `GetWeight` 를 `TryGetValue` fallback 으로 변경:
```csharp
if (RarityWeights.TryGetValue(data.Rarity, out int rw))
    weight = rw;
else
{
    Debug.LogWarning($"[RewardPool] {data.name} 의 Rarity ({data.Rarity}) 가 RarityWeights 에 없음 — weight=1 fallback");
    weight = 1;
}
```
잘못된 Rarity SO 도 weight=1 로 추첨 가능 + 진단 로그.

**사용자 자체 해소**: 같은 SO 여러 번 등록 문제는 사용자가 직접 정리.

### 3. 사용자 메뉴 path 인지 confusion
**문제**: `Populate RewardPool` (Apply) 메뉴가 `Populate RewardPool (Dry Run)` 와 알파벳 순으로 인접 표시 → 사용자가 처음에 Apply 메뉴 못 찾음.

**해소**: 메뉴 path 안내 — 두 항목 다 같은 prefix 라 시각적으로 비슷. CL-150 패턴 일관성 유지.

**향후 개선**: 메뉴 path 분리 또는 sub-menu 그룹 (예: `LostMemory > Relics > RewardPool > Populate / Dry Run / Audit`) — polish ticket.

---

## 검증 결과 (e2e)

### 1. RewardPool 일괄 등록 ✅
사용자 실행:
```
[CL-152] [DRY RUN] 기존 19개 / 추가 예정 N개 / 풀 검색 95개
[CL-152] [APPLIED] 기존 19개 / 추가 예정 N개 / 풀 검색 95개
```

### 2. 분포 audit ✅
```
[CL-152] RewardPool 분포 (총 N 슬롯):
  Legendary: 5
  Unique: 5
  Rare: 19
  Common: 46
  Consumable: ?
```

### 3. 보상 흐름 정상 (single pick + 버그 fix) ✅
```
[RewardController] Reward panel shown. timeScale=0, aim locked. luck=0 tier=-1 count=3 picks=1 forceLegendary=False
[CL-151] 가죽 신발 사이즈 (6×6) 가 인벤토리 (5×5) 초과 — TryAdd reject
[TOAST] 인벤토리 추가 실패: 가죽 신발 (사이즈 초과)
[RewardController] Reward selected: 가죽 신발. Restoring timeScale + aim.
[RewardController] Calling OpenExits on 'Module_Bridge 1' (RoomId=room_combat_small_sample).
```

→ TryAdd false → OnTryAddRejected → Bridge → ToastNotifier 흐름 정상 + RewardController 가 timeScale 정확히 복구 + OpenExits 정상 호출 ✅

### 4. ToastNotifier 발화 ✅
- 사이즈 초과: `[TOAST] 인벤토리 추가 실패: {name} (사이즈 초과)`
- 공간 부족 (5×5 가득 참): 코드 단순 동일 패턴이라 자동 PASS

### 5. CL-146 / CL-147 / CL-150 / CL-151 회귀 ✅
- CL-146 행운 hook (RewardController 의 luck 조회 + DrawCount 인자 전달) — 보상 흐름 검증으로 동시 확인
- CL-147 타로 발화 — 별도 검증
- CL-150 사이즈 매핑 — 변경 X (재실행 안 했음, 가죽 신발만 6×6 잔여)
- CL-151 자동 배치 — 검증 시나리오의 (6×6) reject 가 사이즈 hook 정상 작동 확인

### 미검증 (낮은 우선순위)
- 행운 1스택 가중치 통계 (100회 추첨 비교) — 체감 어려움
- 5×5 가득 참 시 토스트 (사이즈 초과로 대체 검증)
- 상점 가득 참 시 토스트 — `ShopPanelView.TryBuy` 의 false 흐름 검증 필요 (노소연 인계 가능성)
- 정식 토스트 UI 시각 — 별도 ticket

---

## 위험 / 결정 미정

### 위험
1. **`ShopPanelView.TryBuy` 의 TryAdd false 흐름 미검증**: line 104 의 `bool added = _inventory.TryAdd(item.Relic);` 가 false 시 골드 차감 / OnItemPurchased 발화 흐름 검증 필요. OnTryAddRejected 이벤트로 토스트 자동 발화는 OK 지만, 골드 차감 동작이 의도와 다르면 노소연 인계 ticket 필요.
2. **메뉴 다시 돌릴 때 동기화**: 새 SO 추가 시 메뉴 다시 돌려야 함 (자동화 X). 디자이너가 잊을 수 있음. AssetPostprocessor 자동화는 별도 ticket.
3. **풀 95개 등록 시 git diff**: RewardPool.asset 메타 변경 큼. 별도 commit 권장.
4. **ToastNotifier placeholder**: 정식 토스트 UI 가 없으므로 사용자에게 시각 알림 X (콘솔 로그만). 게임 진행 중 인지 어려움 — 정식 UI ticket 시급.
5. **GetWeight fallback weight=1**: 잘못된 Rarity SO 가 LogWarning 출력하지만 추첨에는 포함됨. 의도 다르면 (제외 또는 0 weight) 별도 정책 결정.
6. **PickWeighted GC alloc**: 95개 + filter + sort 매 추첨마다 — 보상 빈도 낮으니 OK. 최적화는 후속.
7. **RewardPanelView 호출 순서 변경 영향**: CL-146 의 다중 픽 (picksAllowed=2) 시나리오에서 SetActive(false) 가 1번째 픽에서 호출 안 되는지 (DisableSelectedCard 만) 재확인 필요. 사용자 회귀 검증 시나리오 1-B 추가 권장.

### 결정 미정 (본 CL 외)
- [ ] 정식 ToastNotifier UI (Canvas 임시 Text 또는 fade animation)
- [ ] AssetPostprocessor 자동 풀 등록 (디자이너 편의)
- [ ] 상점 ShopPanelView 다중 사이즈 표시 (UX 폴리싱)
- [ ] 등급 분포 밸런싱 (Common 46개 압도적 — CL-154 V0.3 컷)
- [ ] DrawCount 최적화 (후속, MVP 충분)
- [ ] 95 SO `.asset` 변경분 별도 commit (디자이너 작업)
- [ ] `ShopPanelView.TryBuy` 의 TryAdd false 흐름 검증 (필요 시 노소연 인계)
- [ ] CL-146 회귀 — picksAllowed=2 다중 픽 1번째 픽 시나리오 재검증
- [ ] CL-150 재실행 — 가죽 신발 등 검증용 변경 SO 원복 (1×1)

---

## 후속 인계

| Ticket / 작업 | CL-152 와의 관계 |
|---|---|
| **CL-153 (1차 통합 QA)** | 본 CL 의 95 풀 + Phase 3 hook 들 실 플레이 검증 |
| **CL-154 (V0.3 컷 + 수치 조정)** | 본 CL 의 분포 audit 결과 → 컷 후보 식별 (Common 46개 압도적) |
| **별도 — ToastNotifier 정식 UI** | placeholder 교체 |
| **별도 — 상점 ShopPanelView 다중 사이즈** | CL-151 의 size 표시를 상점에도 |
| **별도 — AssetPostprocessor 자동 풀 등록** | 새 ItemData 추가 시 자동 |
| **별도 — `ShopPanelView.TryBuy` overflow 검증** | TryAdd false 시 골드/UI 흐름 (노소연 인계 가능성) |
| **별도 — CL-146 picksAllowed=2 회귀 재검증** | 본 CL 의 호출 순서 변경 영향 재확인 |
| **노소연 인계 (CL-151 handoff)** | 본 CL 토스트 시스템과 무관. 인벤토리 UI 시각 갱신만 |

## Phase 5 진행 상태

- [x] **CL-152 보상/상점 풀 ItemData 연결** ← 본 CL
- [ ] CL-153 1차 통합 QA
- [ ] CL-154 V0.3 컷 + 수치 조정

**Phase 5 진행률: 1/3**

## 예상 vs 실제 시간

| 단계 | 예상 (plan) | 실제 |
|---|---|---|
| 1 PlayerRelicInventory.OnTryAddRejected 이벤트 | 15분 | 5분 (단순 이벤트 추가) |
| 2 ToastNotifier placeholder | 10분 | 5분 |
| 3 ToastNotifierBridge 컴포넌트 | 15분 | 10분 |
| 4 RewardPoolPopulateMenu (Dry Run + Apply + Audit) | 45분 | 25분 (CL-150 패턴 재사용) |
| 5 메뉴 실행 + 분포 audit (사용자) | 15분 | 약 20분 (사용자 메뉴 path confusion + 같은 SO 여러 번 등록 정리) |
| 6 ToastNotifierBridge wiring (사용자) | 15분 | 약 10분 |
| 7 검증 (사용자) | 30분 | 약 30분 |
| (추가) CL-146 RewardPanelView 호출 순서 버그 fix | - | 10분 |
| (추가) RewardPool.GetWeight 안전화 | - | 5분 |
| **합계** | **약 2시간 30분** | **약 2시간** |

원본 plan 4시간 30분 → 본 CL plan 축소 2시간 30분 → 실제 2시간. 주요 단축:
- CL-146 / CL-147 에서 RewardPool / RewardController hook 통합 이미 완료
- BuildManager API 이미 존재
- 5장 동적 RewardPanelView CL-146 완료
- 별 카드 (CL-147 단순화) 제거

추가 작업 (버그 fix + 안전화) 가 15분 추가되었지만 핵심 시간 단축으로 상쇄.

→ 1점 ticket 수준 (단순 Editor 메뉴 + 토스트 hook + 이벤트 추가) — Phase 5 의 가벼운 통합 작업.

---

## 다음 단계

1. **사용자 작업**:
   - Git commit 분리 (CL-150 패턴):
     - commit 1: 코드 (PlayerRelicInventory.cs + 신규 3 파일 + RewardPanelView.cs 버그 fix + RewardPool.cs 안전화)
     - commit 2: RewardPool.asset 변경분 (`CL-152: populate reward pool 95 items`)
   - (선택) 가죽 신발 사이즈 원복 (CL-150 Apply 메뉴 1회)
   - (선택) ShopPanelView.TryBuy 흐름 검증 — 상점 가득 참 토스트 작동 확인
2. **나의 작업** (다음 ticket):
   - CL-153 (1차 통합 QA) plan md 정독 + plan 작성
   - 또는 CL-154 (V0.3 컷 + 수치 조정)

**Phase 5 다음**: CL-153 권장 — 본 CL 의 95 풀 + Phase 3 hook 들 실 플레이 통합 검증.
