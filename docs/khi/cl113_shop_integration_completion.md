# CL-113 (확장) — Shop 방 통합 + 골드 시스템 + MVP1 6방 1런 — 완료

브랜치: `feat/S14P31C201-352/cl-113-상점방-da-flow-통합`

## Context

[`cl113_shop_integration_plan.md`](cl113_shop_integration_plan.md) 의 *씬 wiring + 통합 검증 단계* 를 본 doc 이 이어받는다. [`cl112_mvp1_prebuilt_completion.md`](cl112_mvp1_prebuilt_completion.md) 의 B 그룹 (Shop + Gold 베이스 코드 산출) 위에 다음 4 가지를 마무리:

1. **Shop 방 prefab + 씬 wiring** (plan §1, §2)
2. **ShopController 확장 — input 일괄 차단** (plan §8)
3. **MVP1_testkhi.unity 6방 일렬 배치 + entry chain 통합 검증** (plan §4, §5)
4. **인벤토리 단독 토글 부산물** (plan 미명시)

**후속 분리** (사용자 결정):
- plan §6 Boss 방 wiring 검증
- plan §검증 12 항목 중 11~12 (Boss 처치 → RunResult / OnRestart Reset → 씬 재로드)

→ Boss 검증 세션 (별도 ticket 또는 별도 작업) 에서 처리.

---

## A 그룹 — Shop 방 prefab + 씬 wiring

### A1. `ShopRoom_Sample.prefab` 신설

**파일**: [`ShopRoom_Sample.prefab`](../../client/LostMemory/Assets/_Project/Map/Modules/Shop/ShopRoom_Sample.prefab)

Combat module prefab 복제 후 변경:
- ❌ EnemyEncounterSpawner / SpawnPoints / Encounter 데이터 제거
- ✅ Shop NPC GameObject (placeholder sprite + Collider2D + `ShopNpcInteractable`)
- ✅ 출구 zone GameObject (Collider2D trigger + `ShopExitTrigger`)
- ✅ `RoomEntryRuntimeController.roomData` slot = `RoomData_Mvp_Shop_01.asset`

plan §1 명세 그대로. NPC sprite 는 placeholder 단계 (정식 NPC asset 은 후속 CL).

### A2. `ShopController` 인벤토리 동시 표시

**파일**: [`ShopController.cs:28-29, 94-99, 159-162`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopController.cs:28)

plan §2 의 4 슬롯 외에 `InventoryPanelView inventoryPanel` 슬롯 추가 (null 허용):
- `Open` 시 `inventoryPanel.gameObject.SetActive(true)` + `Init(playerRelicInventory, goldWallet.Current)` — Shop 열면 인벤토리도 같이 출현
- `HandleItemPurchased` 시 `inventoryPanel.Refresh(...)` — 구매한 유물이 즉시 인벤토리 슬롯에 반영
- `Close` 시 `inventoryPanel.gameObject.SetActive(false)` — Shop 닫으면 인벤토리도 같이 닫힘 (단독 토글은 B 그룹)

> 구매 → 슬롯 갱신을 *플레이 도중 즉시* 확인 가능하게 하기 위한 결정. 단독 토글 (`InventoryToggleController`) 과 같은 패널을 공유.

---

## B 그룹 — `ShopController` input 일괄 차단 (plan §8)

[`cl112_mvp1_prebuilt_completion.md` A4](cl112_mvp1_prebuilt_completion.md) 의 `RewardController.SetCombatInputsBlocked` 패턴을 그대로 차용.

**파일**: [`ShopController.cs:36-43, 188-195`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopController.cs:36)

추가된 4 슬롯 + 헬퍼 메서드:

| 슬롯 | 차단 효과 |
|---|---|
| `KhiWeaponPresenter playerWeaponPresenter` | 칼이 마우스 따라 회전하는 Update 차단 (`enabled = false` → 마지막 프레임 freeze) |
| `KhiMeleeComboController playerMeleeCombo` | 좌클릭 공격 input 차단 (`ExternalBlock = true`) |
| `KhiDashController playerDash` | Shift 대쉬 input 차단 (`PermitAbility(false)`) |
| `KhiParryController playerParry` | 우클릭 패링 input 차단 (`ExternalBlock = true`) |

호출 지점:
- `SuppressPlayerControls()` (Open 진입) → `SetCombatInputsBlocked(true)`
- `RestorePlayerControls()` (Close 진입 / OnDisable panic) → `SetCombatInputsBlocked(false)`

**기존 슬롯 보존**: `KhiPlayerAim playerAim` 의 `enabled` 토글은 사실상 no-op (KhiPlayerAim 자체엔 Update 가 없음 — 칼 회전은 KhiWeaponPresenter 가 담당). cl112 의 RewardController 와 동일 사유로 슬롯 + tooltip 보존 (향후 KhiPlayerAim 에 Update 추가 시 자동 동작).

> ⚠️ `ShopController.SetCombatInputsBlocked` ↔ `RewardController.SetCombatInputsBlocked` 코드 중복 — 후속 리팩토링 ticket 권장 (공통 헬퍼 또는 PlayerCombatInputGate 추출).

---

## C 그룹 — `InventoryToggleController.cs` (Shop 통합용 baseline)

**파일**: [`InventoryToggleController.cs`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Shop/InventoryToggleController.cs) (신설, 76 라인)

Shop 외부에서 인벤토리 패널을 단독 토글 (default `KeyCode.I`). `ShopController.inventoryPanel` 와 *같은 InventoryPanelView 슬롯 공유* — 한쪽이 열면 다른 쪽 IsOpen 도 true 가 됨. Shop 열림 중 I 키로 닫는 등의 충돌은 *현 단계 허용* (Shop 닫기 전엔 인벤토리만 닫혀도 다음 구매 시 ShopController 의 Refresh 가 자동 재오픈하지 않으므로 *시각적 desync 만 발생*).

> 🔖 **본 CL 의 책임 범위**: Shop 통합에 필요한 *baseline* 만 (Shop 열린 동안 인벤토리 동시 표시 + I 키 단독 토글 최소 동작). **본격적인 인벤토리 연동 정식화 — Shop ↔ InventoryToggle desync 정리 / ESC 키 토글 / 네임스페이스 정리 (`LostMemory.Shop` → `LostMemory.UI.Inventory`) — 는 CL-115 (골드 통화 최소) 에서 진행.** 김회인 본인의 *연동 작업* 영역. 노소연의 인벤토리 UI / asset 영역 (CL-194 / CL-342, 이미 closed) 과 짝이 되는 *연동 ticket* 으로 자리잡음.

---

## D 그룹 — MVP1_testkhi.unity 6방 일렬 배치 + entry chain (plan §4, §5)

### D1. 통합 검증 씬 신설

**파일**: `client/LostMemory/Assets/Scenes/MVP1/MVP1_testkhi.unity`

cl112 doc 의 `MVP1.unity` (5 Combat 만) 와 별도 씬으로 신설. 본 CL 의 정식 데모 씬.

**6 module 일렬 배치** (Boss 부분 제외 시 5 module 통합 검증 완료):
```
Combat_1 → Combat_2 → Shop → Combat_3 → Combat_4 → Boss(미검증)
```

**씬 GameObject** (plan §4 명세 그대로):
- 6 module prefab 인스턴스
- `RunManager` (필드 wiring: rewardController / playerRelicInventory / goldWallet / runResultPanelView)
- `RewardController` (CL-110 산출)
- `GoldWallet`
- `PlayerRelicInventory` (CL-109)
- `RunResultPanelView` prefab
- `InventoryPanelView` prefab (Shop / 단독 토글 모두 같은 인스턴스 참조)
- `InventoryToggleController` (I 키 토글)
- Player (TestKhi) — Combat_1 entry zone 위치
- CinemachineCamera

**module 별 설정**:
- Combat_1~4: `autoOpenExitsOnCleared = false` (RewardController 가 OpenExits 트리거)
- Shop: 출구 trigger 발화 = 클리어 시점 (이미 출구 통과로 출구 정책 무관)
- Boss: cl112 doc 그대로 — 본 CL 미검증

### D2. `DungeonRunBootstrap.usePrebuiltLayout = true` instance override

cl112 의 A1 결정 그대로. MVP1_testkhi.unity 도 동일 모드 — `dungeon.Build()` skip + `DungeonBuilt` 즉시 발화 → `RunManager.SubscribeAllRoomControllers` 가 씬에 미리 배치된 `RoomEntryRuntimeController[]` 를 발견.

### D3. RoomData 6 종

**파일**: `client/LostMemory/Assets/_Project/ScriptableObjects/Rooms/Mvp/RoomData_Mvp_*.asset`
- `RoomData_Mvp_Combat_01 ~ 04.asset`
- `RoomData_Mvp_Shop_01.asset` (`RoomType = Shop`, `RoomClearConditionType = Custom`)
- `RoomData_Mvp_Boss_01.asset` (Boss 검증 미수행 — 후속)

---

## E 그룹 — RunResult / Restart wiring 재참조 (plan §7)

cl112 doc B5 와 동일 — *재참조* 만:

| 위치 | 동작 |
|---|---|
| `RunManager.HandleRoomCleared` (line 305-308) | Combat 클리어 시 `goldWallet.Add(50)` |
| `RunManager.CloseResulting` (line 242-245) | `goldWallet.ResetToInitial()` — Run 한정 골드 초기화 |
| `RunManager.HandleRestartRequested` (line 440) | `CloseResulting()` + 현재 씬 재로드 |
| `RunManager.HandleLobbyRequested` (line 449-452) | CL-117 까지 `CloseResulting()` 만 (TODO 주석) |

---

## 검증 결과 (plan §검증 12 항목)

### 통과 (사용자 표명 + 코드 / wiring 확인)

**골드 / Combat 흐름**
1. Run 시작 골드 0
2. Combat_1 클리어 → 보상 → 카드 선택 → 골드 50 → 출구 → Combat_2
3. Combat_2 → 골드 100
4. Combat_3 → 골드 150
5. Combat_4 → 골드 200

**Shop 흐름**
6. Shop 방 진입 → 적 0 / NPC 1 / 출구 trigger 존재. 패널 자동 출현 X
7. NPC 근접 + F → ShopPanel + InventoryPanel 동시 활성. 플레이어 이동 / 조준 / 공격 / 대쉬 / 패링 / 칼 회전 일괄 봉쇄
8. 상품 구매 → 골드 차감 + RelicData PlayerRelicInventory 추가 + InventoryPanel 즉시 Refresh + 매진 표시
9. F 토글 → ShopPanel + InventoryPanel 동시 비활성. 플레이어 조작 복원
10. 출구 trigger 통과 → `[Controller] RoomCleared` 로그 → 다음 방 (Combat_3) 진입

### 후속 분리 (Boss 검증 세션에서 처리)

11. Boss 방 진입 → Bertha 자동 등장 → 처치 → `[BossDefeat] RoomCleared` 로그 → RunResult 패널 (success=true)
12. RunResult OnRestart → `CloseResulting` → 골드 0 Reset 로그 → 씬 재로드

> 12 는 코드 wiring 은 완료 (E 그룹 line 440, 244 참조) 이지만 Boss 통과를 전제로 RunResult 패널이 출현하므로 Boss 검증과 동반 분리.

---

## 후속 ticket / 권장 작업

| 항목 | 담당 ticket | 사유 |
|---|---|---|
| Boss 방 wiring 검증 (plan §6) | **CL-114 (보스방 DA flow 통합, 다른 작업자)** | `BossArea_Test.prefab` 정식 동작 확인. Boss 처치 → `BossDefeatRoomClearController.NotifyCustomRoomCleared` → RunManager Boss 분기 → `RunResultPanelView.Show(success=true)` 흐름. **본인 영역 X** |
| OnRestart Reset 검증 (plan §검증 12) | **CL-114 동반** | Boss 통과 전제 — Boss 검증과 묶임 |
| **인벤토리 연동 정식화** (Shop ↔ InventoryToggle desync 정리 / ESC 키 토글 / `LostMemory.Shop` → `LostMemory.UI.Inventory` 네임스페이스 분리) | **CL-115 (골드 통화 최소 — 전투 보상 → 상점 사용)** | CL-115 가 *상점 사용* UX scope 라 인벤토리 동시 표시 + 토글 UX 보강이 자연스럽게 포함. 김회인 본인의 *연동 작업* 영역. CL-194/CL-342 (노소연 UI 영역) 와 짝이 되는 연동 ticket |
| `ShopController` ↔ `RewardController` 의 `SetCombatInputsBlocked` 공통 추출 | 별도 리팩토링 ticket (TBD) | 두 곳 코드 복제. `PlayerCombatInputGate` 류 헬퍼 권장. 시연 차단요소 X — 우선순위 낮음 |
| 정식 NPC sprite asset 도입 | 별도 art ticket (TBD) | 현재 placeholder. 정식 art 후속 CL |
| **CL-115 (정식 골드 시스템 — 메타 누적)** | CL-115 (위와 같은 ticket) | 메타 누적 / 영구 골드 / 마을 환전. 본 CL 의 `GoldWallet` Run 한정 그대로 활용 |
| **CL-117 (마을 ↔ 던전 씬 전환)** | CL-117 | `HandleLobbyRequested` TODO 마무리 — `SceneManager.LoadScene(VillageScene)` |
| **CL-112 (DA layout)** | CL-112 | 본 CL 의 module prefab 그대로 재사용. DA 가 spawn 만 대신함 — *DA 막혀도 본 CL 의 MVP1_testkhi.unity 가 데모 fallback* |
