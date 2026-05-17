# CL-228: 이벤트 방 — 미스터리 아이템 구매 (Mystery Item Purchase)

**Epic**: I. 이벤트 방 / 메타 보상 루프
**상태**: 📋 **plan only** — 미구현
**선행**: CL-113 (Shop 시스템 — `ShopController` / `ShopPanelView` / `ShopGenerator`), CL-227 (이벤트 방 NPC 패턴 + 봉쇄 패턴)
**관련 후속**: CL-229 (이벤트 — 슬롯머신)

> 이벤트 방 3종 시리즈 두 번째. **기존 Shop 과 명확히 구별** 되는 *미스터리 구매* 컨셉. 단순 vendor 가 아니라 *가격은 보이지만 아이템 정체가 가려진 슬롯* 을 구매하는 도박적 요소를 가진 방.

---

## Context

### 왜 그냥 Shop 을 또 쓰지 않는가

기존 [ShopController.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopController.cs) 와 [ShopGenerator.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopGenerator.cs) 는 *이미 잘 만들어진* 다중 슬롯 vendor — 유물 3 + 포션 1 + 랜덤박스 (확률) 구성. 본 CL 의 이벤트 방 컨셉은:

> "이벤트 = *놀라움*". 그냥 가격표 붙은 vendor 면 이벤트가 아니라 그냥 추가 상점.

따라서 본 CL 은 **3 슬롯 모두 정체 가려진 카드 형태** + 슬롯별 가격만 보임. 플레이어가 "200골드 짜리 슬롯 하나" 를 사면 → 카드 뒤집힘 → 무엇이 나올지 그때 알게 됨. 가격만으로 등급 추정은 가능하지만 *실제 아이템* 은 운.

### 기존 Shop 과의 차이 요약

| 항목 | 기존 Shop (CL-113) | 본 CL — 미스터리 구매 |
|---|---|---|
| 슬롯 수 | 4~5 (유물 3 + 포션 1 + 랜덤박스 0~1) | **3** 고정 |
| 아이템 정체 | 모두 공개 (이름/아이콘/등급/효과) | **모두 가려짐** — 카드 뒷면 |
| 가격 | 등급별 고정 | **슬롯별 다른 가격대** (등급 힌트가 됨) |
| 구매 횟수 | 슬롯당 1회 (sold-out 추적) | 동일 |
| 닫기 | F 토글 또는 ESC | 동일 |
| 방 진입 시 출구 | 잠기지 않음 | **잠김** — 1 슬롯 이상 구매 시 해제 (또는 NPC 와 다시 상호작용으로 *건너뛰기* 가능) |
| 주체 NPC | 기존 Shop NPC | 별도 — *미스터리 상인* (다른 sprite) |

### 결정 사항: "지나가기" 옵션 필요한가

플레이어가 골드 부족하거나 사기 싫을 때 출구 못 나가면 frustration. 두 가지 선택:
- **(a) 즉시 통과** — 출구 안 잠김 (Shop 과 동일). 이벤트라는 의미 약해짐
- **(b) 1슬롯 강제 구매 후 해제** — 골드 0 일 때 막힘 → 비싼 강제. 짜증
- **(c) "건너뛰기" 옵션** — NPC 가 "구매 OR 건너뛰기" 선택지. 건너뛰면 즉시 통과
- **(d) 무료 1장 제공 + 추가 구매 옵션** — 무료로 1장 가져가면 자동 통과, 더 사고 싶으면 구매

**추천: (d) 무료 1장 + 추가 유료** — 이벤트답게 *반드시 무언가 줌*, 추가 구매는 도박. 단 구현 난이도 ↑.
**대안 추천: (c) 건너뛰기** — 가장 단순, 짜증 없음, 이벤트 무시 가능 (나쁜 옵션이지만 합리적 자유).

본 plan 의 기본은 **(c) 건너뛰기** 채택, (d) 는 후속 옵션으로 명시.

---

## 결정해야 할 사항 (사용자 확인 필요)

| # | 결정 | 후보 | 추천 |
|---|---|---|---|
| 1 | 슬롯 수 | 2 / 3 / 4 | **3** — Shop 과 시각적 구별 + 선택 부담 적당 |
| 2 | 가격 결정 방식 | (a) 등급 추첨 후 등급별 고정 가격, (b) 슬롯별 가격 미리 정해두고 가격대에 맞는 등급 추첨 | **(b)** — *낮은 가격 = 낮은 등급 가능성 ↑* 의 메타 정보가 직관적. ShopGenerator 와 정반대 흐름 |
| 3 | 가격대 (3슬롯) | 50/150/300, 100/200/400, 등 | **80 / 200 / 400** — Shop 평균가보다 낮음 (이벤트 보너스). 후속 ShopConfig 와 통일성 검토 |
| 4 | 정체 가림 시 표시 정보 | (a) 가격만, (b) 가격 + 카테고리 (유물/소모품/골드), (c) 가격 + 등급 | **(a)** — 가장 도박. 등급은 가격대로 *추측만* 가능 |
| 5 | "건너뛰기" 옵션 | (a)/(b)/(c)/(d) 위 | **(c)** Phase A. (d) 후속 |
| 6 | 보상 종류 풀 | 유물만 / 유물+소모품 / 유물+소모품+골드 | **유물 + 소모품** — Shop 과 같음. 골드는 슬롯머신 (CL-229) 차별화 |
| 7 | 동일 던전 재방문 | 같은 슬롯 재구매 가능? | **불가능** — Shop 과 같이 sold-out 추적 |
| 8 | 멀티 확장 | 같은 슬롯을 여러 플레이어가 살 수 있나 | 후속 — Phase A 솔로 |

---

## 시스템 사실

### Shop 시스템 재사용 가능 부분

| Shop 컴포넌트 | 본 CL 재사용 | 변경 |
|---|---|---|
| `ShopData` ([cs:10](../../LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopData.cs)) | **재사용** — 단순 ShopItemData[] 컨테이너 | 그대로 `MysteryShopData` (또는 ShopData 재사용 + 다른 generator) |
| `ShopItemData` | **재사용** — Relic + Price 구조체 | 그대로 |
| `ShopConfig` ([cs:11](../../LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopConfig.cs)) | **재사용 X** — 본 CL 은 별도 `MysteryShopConfig` (가격대 / 슬롯별 가중치) | 새 SO 권장 |
| `ShopGenerator.Generate` ([cs:29](../../LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopGenerator.cs)) | **재사용 X** — 본 CL 은 *가격 우선* 추첨 (결정 2 (b)) | 새 generator |
| `ShopController` ([cs:24](../../LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopController.cs)) | **재사용 후보** — 거의 그대로 사용 가능. 봉쇄 + 봉쇄 해제 + sceneLocalRefs + Open/Close/Toggle | 본 CL 은 *별도 controller* (`MysteryShopController`) 권장 — 인스펙터 슬롯이 다르고 (다른 panel/data ref) DDOL 충돌 방지 |
| `ShopPanelView` ([cs:15](../../LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopPanelView.cs)) | **재사용 X** — 카드 뒷면 + 가격 표시 + reveal 흐름이 다름. 새 `MysteryShopPanelView` | |
| `ShopItemView` | **재사용 X** — 카드 뒷면 + 가격만 표시. 새 `MysteryShopItemView` | |
| `ShopNpcInteractable` ([cs:26](../../LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopNpcInteractable.cs)) | **패턴 복제** — `MysteryShopNpcInteractable`. ResolveShopData 는 단순화 (정적 ref + 동적 generation 분기) | |

### 카드 reveal 패턴 (CL-227 재사용)

CL-227 에서 만들 `EventCardView` 의 *뒷면 / 앞면 / 뒤집힘 코루틴* 로직은 본 CL 의 슬롯과 **거의 동일** 한 비주얼. **CL-227 의 `EventCardView` 를 재사용** 하거나 **공통 베이스 `EventCardVisual`** 를 추출. 본 CL 의 차이점:
- 카드 위에 *가격 텍스트* 추가 (구매 전 표시)
- 클릭 시 *구매 시도* (가격 차감 + reveal) — CL-227 은 즉시 reveal
- "구매 가능 (afford)" 시각적 상태 — 가격 못 내면 어둡게

→ **별도 컴포넌트 `MysteryCardView` 가 깔끔**. CL-227 의 `EventCardView` 와 코드 중복 일부 발생하나 책임 분리 명확.

### 출구 잠금 흐름 (CL-227 동일)
- RoomData.InitContext.LockExitDoors = true
- "건너뛰기" 또는 "1 슬롯 구매" → controller 가 `roomController.OpenExits()` 호출
- `autoOpenExitsOnCleared=true` 면 RoomCleared 발화로도 해제 가능

### Shop 의 ShortcutBar / InventoryPanel 동시 표시 ([ShopController.cs:115-126](../../LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopController.cs))
- Shop 열림 시 인벤토리 패널 + 단축키바 함께 표시 (구매 즉시 슬롯 변화 확인)
- **본 CL 도 동일 적용 권장** — 미스터리 카드 reveal 후 인벤 슬롯에 들어간 게 즉시 보임

---

## 작업 범위

### Phase A — 데이터 모델

- [ ] `MysteryShopConfig.cs` (신규 SO) — `Assets/_Project/Scripts/Runtime/Events/`
  - 슬롯별 가격 (3개) `int[] slotPrices` — 결정 3
  - 가격대별 등급 가중치 테이블 (예: 가격 ≤100 → Common 70 / Rare 30 / Unique 0 / Legendary 0)
  - 소모품 풀 (포션 등) — 가격 ≤100 슬롯에 등장 가능
- [ ] `MysteryShopGenerator.cs` (신규 static class) — 가격 우선 추첨
  - `Generate(MysteryShopConfig config, RewardPool rewardPool, PlayerRelicInventory inventory)` → `ShopData` (재사용)
  - 각 슬롯에 대해: 가격 → 가격대별 등급 가중치 → 등급에 맞는 미보유 유물 추첨 (소모품 fallback)
- [ ] `ShopData` 재사용 ([cs:10](../../LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopData.cs))

### Phase B — UI

- [ ] `MysteryShopPanelView.cs` (신규)
  - `Init(ShopData data, PlayerRelicInventory inventory, GoldWallet wallet, int gold)`
  - 슬롯 N개 (씬 prefab 에 배치된 `MysteryCardView[]`) 각각에 `MysteryCardView.Init(data, price, onClick)` 호출
  - `OnSlotPurchased` 이벤트 — controller 가 구독해 GoldWallet sync
  - `RefreshAffordability(int gold)` — 가격 못 내는 슬롯 어둡게 (Shop `RefreshAffordability` 패턴)
  - 모든 슬롯 sold-out 시 **자동 닫힘 + roomCleared 신호** (또는 controller 가 감지)
- [ ] `MysteryCardView.cs` (신규)
  - 뒷면 sprite + 가격 텍스트 (앞면) — 항상 보임
  - 앞면 (구매 후 reveal): 아이콘, 이름, 등급
  - `Init(ShopItemData data, Action<ShopItemData> onClick)` + `SetAffordable(bool)` + `SetSoldOut(bool)`
  - 클릭 → onClick 콜백 (controller 가 가격 검증 + 구매 처리 + reveal 트리거)
  - `Reveal(bool animate)` — 1초 뒤집기 코루틴
- [ ] `MysteryShopPanel.prefab` (신규) — `Prefabs/UI/`
  - 슬롯 3장 가로 배치, "건너뛰기" 버튼 (결정 5 (c))

### Phase C — 컨트롤러

- [ ] `MysteryShopController.cs` (신규) — [ShopController.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopController.cs) 의 80% 복제 + 차이점 적용
  - 인스펙터 슬롯: panel(`MysteryShopPanelView`), inventoryPanel, shortcutBar, playerRelicInventory, playerConsumableInventory, goldWallet, [봉쇄 6종], **roomController**
  - `Open(MysteryShopData data)` — Shop 과 동일하게 봉쇄 + 패널 활성
  - `Close()` — 동일
  - `Toggle(MysteryShopData data)` — 동일
  - `HandleSlotPurchased(ShopItemData item)`:
    1. `goldWallet.Spend(item.Price)` — 부족 시 false 처리 (PanelView 가 사전 검증하지만 desync 방지)
    2. `playerRelicInventory.TryAdd(item.Relic)`
    3. PanelView 의 해당 슬롯 reveal 트리거
    4. inventory/shortcutBar Refresh
    5. 첫 구매 시 `roomController.OpenExits()` 호출 — 출구 해제 (or 모든 슬롯 sold-out 후?)
  - `HandleSkipPressed()` — "건너뛰기" 버튼 콜백 → `Close()` + `roomController.OpenExits()` + `NotifyCustomRoomCleared()`
  - `ResolveSceneLocalRefs()` — Shop 과 동일 패턴
  - `SuppressPlayerControls / RestorePlayerControls / SetCombatInputsBlocked` — Shop 과 동일
- [ ] `MysteryShopNpcInteractable.cs` (신규) — [ShopNpcInteractable.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopNpcInteractable.cs) 복제 + 단순화
  - shopController → mysteryShopController, ResolveShopData → 캐시된 동적 generation (Shop 과 동일 패턴)
  - 1회 가드 X — 다중 슬롯 구매를 위해 F 토글 자유롭게

### Phase D — 데이터 자산

- [ ] `MysteryShopConfig_Default.asset` 생성 — `ScriptableObjects/Events/`
  - slotPrices = [80, 200, 400]
  - 가격대 80 → Common 80 / Rare 20 (소모품 weight 추가)
  - 가격대 200 → Common 30 / Rare 50 / Unique 20
  - 가격대 400 → Rare 30 / Unique 50 / Legendary 20
- [ ] `RoomData_Event_MysteryShop_01.asset` — `ScriptableObjects/Rooms/`
  - RoomType = Event
  - InitContext.LockExitDoors = true
- [ ] `Room_Event_MysteryShop.prefab` — 방 레이아웃 (NPC + collider trigger 포함)

### Phase E — Editor / 씬 셋업

- [ ] `MAP_event_mysteryshop_test.unity` (단독 검증 씬)
- [ ] `MysteryShopPanel.prefab` 의 슬롯 3개 배치 + 건너뛰기 버튼 배치
- [ ] NPC sprite 결정 (다른 컬러 / 다른 모델로 Shop NPC 와 구별)

### Phase F — 검증

- [ ] **단일 방 — 정상 흐름**
  - F → 패널 + 슬롯 3장 (뒷면 + 가격)
  - 골드 부족 슬롯 어둡게
  - 200G 슬롯 클릭 → -200G + 슬롯 reveal (예: "Rare 유물 X") + 슬롯 sold-out
  - **첫 구매 후 출구 해제** (결정에 따라)
  - F 다시 → 패널 닫힘
- [ ] **건너뛰기 흐름**
  - 골드 0 으로 진입 → 모든 슬롯 어둡게
  - 건너뛰기 버튼 → 즉시 출구 해제 + 패널 닫힘
- [ ] **봉쇄 검증** — 패널 떠있는 동안 이동/조준/공격 차단 (CL-227 동일)
- [ ] **인벤토리/단축키바 동시 갱신** — Shop 과 동일하게 구매 즉시 슬롯에 추가
- [ ] **절차생성 던전 통합** — `RoomData_Event_MysteryShop_01` 을 시퀀스 한 칸에 배치
- [ ] **씬 전환 stale ref** — DDOL 부착 시 ResolveSceneLocalRefs 작동
- [ ] **Reroll/캐시** — 같은 방 재방문은 없지만 (1 run 1 visit), 패널 재오픈 시 같은 ShopData 인스턴스 재사용 (sold-out 보존, [ShopPanelView.cs:54](../../LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopPanelView.cs) 패턴)

### 작업 외 (Out of scope)

- (d) 무료 1장 + 추가 유료 옵션 (결정 5 의 후속)
- 슬롯에 가격 외 힌트 표시 (결정 4 (b)/(c) 의 후속)
- 골드 보상 슬롯 (결정 6 — 슬롯머신과 차별화)
- 멀티 확장 (호스트 권위 추첨 + 결과 sync)
- 같은 던전 재방문 (1 run 1 visit 가정)

---

## 변경 파일

### 신규 (스크립트)
| 파일 | 책임 |
|---|---|
| `Runtime/Events/MysteryShopConfig.cs` | 슬롯 가격 + 가격대별 등급 가중치 SO |
| `Runtime/Events/MysteryShopGenerator.cs` | 가격 → 등급 → 유물 추첨 로직 |
| `Runtime/Events/MysteryShopPanelView.cs` | 패널 뷰 + 슬롯 N개 관리 + Skip 버튼 |
| `Runtime/Events/MysteryCardView.cs` | 카드 1장 (뒷면 + 가격, 클릭 시 reveal) |
| `Runtime/Events/MysteryShopController.cs` | NPC trigger → 패널 open/close + 봉쇄 + GoldWallet/Inventory sync + 방 클리어 |
| `Runtime/Events/MysteryShopNpcInteractable.cs` | F 키 trigger zone (1회 가드 X — 다중 슬롯 자유) |

### 신규 (자산)
| 자산 | 위치 |
|---|---|
| `MysteryShopConfig_Default.asset` | `ScriptableObjects/Events/` |
| `RoomData_Event_MysteryShop_01.asset` | `ScriptableObjects/Rooms/` |
| `MysteryShopPanel.prefab` | `Prefabs/UI/` |
| `MysteryCardView_Slot.prefab` | `Prefabs/UI/` |
| `Room_Event_MysteryShop.prefab` | `Prefabs/Rooms/` |
| `MAP_event_mysteryshop_test.unity` | `Scenes/Tests/` |

### 수정
- 없음 (신규 모듈만 추가).
- 단, 기존 [ShopData.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopData.cs) 와 [ShopItemData.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopItemData.cs) 를 그대로 재사용 (수정 없이 참조)

---

## 코드 spec (핵심 부분 요약)

### `MysteryShopGenerator.Generate` (핵심 차이점)

```csharp
public static ShopData Generate(
    MysteryShopConfig config,
    RewardPool        rewardPool,
    PlayerRelicInventory inventory)
{
    var shopData = ScriptableObject.CreateInstance<ShopData>();
    var items    = new List<ShopItemData>();
    var excluded = new HashSet<string>(inventory.GetOwnedNames());

    for (int i = 0; i < config.SlotPrices.Length; i++)
    {
        int price = config.SlotPrices[i];
        // 가격대별 가중치 테이블 lookup → Dictionary<RelicRarity, int>
        var rarityWeights = config.GetRarityWeightsForPrice(price);

        var relic = rewardPool.DrawOneRelicForShop(excluded, rarityWeights);
        if (relic == null)
        {
            // 폴백 — 소모품 (포션) 또는 환불용 골드 카드
            relic = config.FallbackConsumable;
        }
        items.Add(new ShopItemData { Relic = relic, Price = price });
        excluded.Add(relic.name);
    }

    shopData.Items = items.ToArray();
    return shopData;
}
```

### `MysteryShopController.HandleSlotPurchased` (출구 해제 로직)

```csharp
private void HandleSlotPurchased(ShopItemData item)
{
    // 골드 차감 (PanelView 가 이미 사전 검증)
    if (goldWallet != null) goldWallet.Spend(item.Price);

    // PanelView 가 reveal + sold-out 처리
    if (panel != null) panel.UpdateGold(goldWallet.Current);
    if (inventoryPanel != null && inventoryPanel.gameObject.activeSelf)
        inventoryPanel.Refresh(playerRelicInventory, goldWallet.Current);
    if (shortcutBar != null && shortcutBar.gameObject.activeSelf)
        shortcutBar.Refresh();

    // 첫 구매 시 출구 해제 — Phase A 정책
    if (!_hasPurchasedAtLeastOnce && roomController != null)
    {
        _hasPurchasedAtLeastOnce = true;
        roomController.OpenExits();
        roomController.NotifyCustomRoomCleared();   // RoomCleared 이벤트 발화 (메타 추적)
    }
}

private void HandleSkipPressed()
{
    Close();
    if (roomController != null)
    {
        roomController.OpenExits();
        roomController.NotifyCustomRoomCleared();
    }
}
```

### `MysteryCardView.Reveal` (뒤집기 코루틴 — CL-227 EventCardView 와 동일 패턴)

```csharp
private IEnumerator RevealCoroutine(bool animate)
{
    if (!animate)
    {
        _backRoot.SetActive(false);
        _frontRoot.SetActive(true);
        yield break;
    }

    float duration = 0.5f;
    float elapsed = 0f;
    Transform root = transform;

    // 뒷면: 0 → 90도 (X 축 회전)
    while (elapsed < duration / 2f)
    {
        float t = elapsed / (duration / 2f);
        root.localEulerAngles = new Vector3(t * 90f, 0, 0);
        elapsed += Time.unscaledDeltaTime;
        yield return null;
    }

    _backRoot.SetActive(false);
    _frontRoot.SetActive(true);

    // 앞면: 90 → 0도
    elapsed = 0f;
    while (elapsed < duration / 2f)
    {
        float t = elapsed / (duration / 2f);
        root.localEulerAngles = new Vector3(90f - t * 90f, 0, 0);
        elapsed += Time.unscaledDeltaTime;
        yield return null;
    }
    root.localEulerAngles = Vector3.zero;
}
```

> `Time.unscaledDeltaTime` — Shop 처럼 timeScale 변경 안 하지만 안전 차원. 적용 후 검증 시 Shop 같이 timeScale 정지가 발생하는지 확인 후 결정.

---

## 검증 시나리오 (Phase F 상세)

### F-1. 단일 방 — 정상 구매 흐름
1. `MAP_event_mysteryshop_test.unity` 시작 시 GoldWallet=500
2. 방 진입 → 출구 잠김
3. NPC F 키 → 패널 + 슬롯 3장 (뒷면 + "80G" / "200G" / "400G")
4. 80G 슬롯 클릭 → 뒤집힘 → 예: "Common 유물 ABC" + 인벤에 추가됨
5. GoldWallet 420 으로 갱신, 80G 슬롯 sold-out 표시
6. **첫 구매 후 출구 자동 해제** (결정에 따라)
7. 200G 슬롯 클릭 → 추가 구매 가능
8. ESC → 패널 닫힘 → 출구 통과 가능

### F-2. 건너뛰기 흐름 (골드 0)
9. GoldWallet=0 으로 시작
10. F → 패널 + 모든 슬롯 어둡게 (afford=false)
11. 슬롯 클릭 → 무반응 (또는 "골드 부족" 토스트)
12. 건너뛰기 버튼 → 패널 닫힘 + 출구 해제

### F-3. 가격대 → 등급 매핑 검증
13. 100회 generation 시뮬레이션 (또는 generator 단위 테스트)
14. 80G 슬롯 → Common 80% / Rare 20% (config 와 일치)
15. 400G 슬롯 → Legendary 20%, Common 0%

### F-4. 봉쇄 + UI 동시 표시 (Shop 과 동일)
16. 패널 떠있는 동안 KhiMeleeComboController.ExternalBlock=true
17. 인벤토리 패널 + 단축키바 동시 표시 — 구매 즉시 슬롯에 들어간 유물 visible

### F-5. 캐시 동작 (Shop 패턴)
18. 패널 닫고 같은 방에서 다시 F → 같은 ShopData 인스턴스 재사용 → 같은 카드 + sold-out 상태 보존

### F-6. 절차생성 + Combat 방과 분리
19. RoomData_Event_MysteryShop_01 을 시퀀스 한 칸에 배치
20. Combat 방 → 이전 보상 → MysteryShop → 정상
21. RewardController 가 Event 방 RoomCleared 무시 ([cs:133](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/RewardController.cs))

### F-7. 같은 던전 재방문 가정 X
22. (현재 시스템에서 같은 RoomId 재방문 흐름 자체가 없음 — 검증 시 *진입 1회만* 확인)

---

## 알려진 이슈 / 고려

- **Shop 과의 코드 중복** — Controller / NpcInteractable / PanelView 3개 모두 80% 비슷. 본 CL 후 *공통 베이스 추출* 리팩토링 ticket 후속 (예: `ShopLikeControllerBase` / `NpcInteractableBase`). 본 CL 에선 명시적 복제로 출시
- **출구 해제 타이밍 결정 (위 코드 spec 의 "첫 구매 시 해제")** — 모든 슬롯 sold-out 후 해제로 바꿀 수도 있음. **첫 구매 시** 가 자유도 ↑ 추천
- **카드 reveal 도중 ESC 입력** — Coroutine StopAllCoroutines 또는 reveal 완료까진 ESC 무시. Phase A 단순화로 reveal 도중 ESC 무시
- **ShopData 인스턴스 재사용 (sold-out 보존)** — [ShopPanelView.cs:54-63](../../LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopPanelView.cs) 패턴 그대로 적용. `ReferenceEquals(_shopData, shopData)` 비교
- **PlayerConsumableInventory 가득** — 소모품 reveal 됐는데 TryAdd 실패 가능. 본 CL 은 console warning + 골드 환불 (price 만큼) 정도가 합리. Phase A: warning 만, 손실 허용
- **Affordability 갱신 시점** — 구매 후 반드시 `RefreshAffordability(goldWallet.Current)` 호출. Shop [PanelView.cs:128](../../LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopPanelView.cs) 와 동일
- **무료 1장 옵션 (결정 5 (d))** 후속 시 — `_freeUsed` flag + 무료 슬롯 별도 표시. 본 CL 은 미포함

---

## 후속 CL 후보

| CL | 내용 |
|---|---|
| CL-229 | 이벤트 방 — 슬롯머신 (본 CL 의 NPC + 봉쇄 + GoldWallet 패턴 재사용) |
| 리팩토링 | Shop / MysteryShop 공통 베이스 추출 (ShopLikeControllerBase, NpcInteractableBase) |
| 후속 폴리시 | 무료 1장 옵션 (결정 5 (d)), 가격 외 카테고리 힌트 (결정 4 (b)), 가격대 추가 (5슬롯) |
| 멀티 확장 | 호스트 권위 generation + 결과 broadcast, 여러 플레이어가 다른 슬롯 동시 구매 |
