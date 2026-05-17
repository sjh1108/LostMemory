# CL-228 미스터리 구매 — 구현 기록 + Editor 핸드오프

작성일: 2026-05-15
기준 plan: [cl228_event_item_purchase_plan.md](cl228_event_item_purchase_plan.md)

**상태**: 🟡 **A 옵션 (Vending Machine) 진행 중** — Mystery 카드 시스템은 보존 (CL-227 등 후속에서 재사용 예정).

## 진행 분기

사용자 요청으로 *단순 vending machine* (1 슬롯 / 정체 공개 / 200G / 랜덤박스) 채택. Mystery 카드 시스템 코드는 그대로 보존:

| 시스템 | 상태 | 용도 |
|---|---|---|
| **Vending Machine** (`VendingMachine*.cs`) | ✅ 활성 — 본 CL 이 사용 | 1 아이템 단순 판매 |
| **Mystery Shop** (`Mystery*.cs`) | 💤 보존 — 미사용 | CL-227 카드 뽑기 등 후속에서 재사용 |

---

## 적용된 결정 (Phase A 기본값)

| # | 결정 | 적용값 |
|---|---|---|
| 1 | 슬롯 수 | **3** |
| 2 | 가격 결정 방식 | **(b)** 슬롯별 가격 → 가격대별 등급 가중치 추첨 |
| 3 | 가격대 | **80 / 200 / 400** |
| 4 | 가림 시 표시 정보 | **(a)** 가격만 |
| 5 | 건너뛰기 옵션 | **(c)** Skip 버튼 |
| 6 | 보상 풀 | 유물 + 소모품 (RewardPool 재사용 + fallbackConsumable) |
| 7 | 재방문 | sold-out 보존 (캐시된 ShopData 재사용) |
| 8 | 멀티 확장 | 후속 |
| Reveal | 즉시 swap (back→front 활성 토글) | **애니메이션은 후속 폴리시** |

> 본 plan 의 결정 4 (가격만 표시) 는 시각적 *유일한 차별 포인트*. Prefab 셋업 시 등급/이름이 *뒷면에 누설되지 않도록* 반드시 BackRoot 와 FrontRoot 분리.

---

## 작성된 스크립트 (6개)

| 파일 | 책임 |
|---|---|
| [MysteryShopConfig.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Events/MysteryShopConfig.cs) | 슬롯 가격 + 가격대별 등급 가중치 SO |
| [MysteryShopGenerator.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Events/MysteryShopGenerator.cs) | 가격→등급→유물 추첨 (`RewardPool.DrawOneRelicForShop` 활용) |
| [MysteryCardView.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Events/MysteryCardView.cs) | 카드 1장 (back/front root + 가격/구매 버튼) |
| [MysteryShopPanelView.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Events/MysteryShopPanelView.cs) | 패널 — N개 슬롯 + Skip 버튼 + 골드 HUD |
| [MysteryShopController.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Events/MysteryShopController.cs) | NPC trigger → Open/Close + GoldWallet sync + 봉쇄 + 출구 해제 |
| [MysteryShopNpcInteractable.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Events/MysteryShopNpcInteractable.cs) | F 키 trigger zone (캐시된 ShopData 재사용) |

**namespace**: `LostMemory.Events` (신규)

**기존 코드 재사용**:
- [`ShopData`](../../LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopData.cs), [`ShopItemData`](../../LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopItemData.cs) — 그대로 재사용 (수정 없음)
- [`RewardPool.DrawOneRelicForShop`](../../LostMemory/Assets/_Project/Scripts/Runtime/Rewards/RewardPool.cs) — 그대로 사용
- [`InventoryPanelView`](../../LostMemory/Assets/_Project/Scripts/Runtime/Shop/InventoryPanelView.cs), [`ShortcutBarView`](../../LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShortcutBarView.cs) — Shop 과 동일하게 동시 표시

---

## Procedural 스폰 지원 (CL-228 본 흐름)

코드가 *NPC 가 어떤 방에서 instantiate 돼도 자동 동작* 하도록 수정됨:

| 컴포넌트 | 어떻게 ref 찾나 |
|---|---|
| `VendingMachineInteractable.controller` | 인스펙터 비어있으면 첫 F 키 시 `FindFirstObjectByType<VendingMachineController>()` |
| `VendingMachineInteractable.roomController` | 인스펙터 비어있으면 Awake 에서 `GetComponentInParent<RoomEntryRuntimeController>()` |
| `VendingMachineController.Open(config, forRoom)` | NPC 가 자기 방 RoomEntryRuntimeController 를 인자로 넘김. `_activeRoomController` 매 Open 시 갱신 → 다른 방 NPC 도 정상 동작 |

### 결과적인 사용 패턴

**메인 던전 씬 셋업** (한 번만):
- Canvas 안에 `VendingMachinePanel.prefab` (인스턴스)
- 씬 어딘가에 `VendingMachineController` GameObject
- → 메뉴 `LostMemory > Vending Machine > Sync In Active Scene` 으로 자동

**방 prefab** (한 번만):
- `Room_Event_VendingMachine.prefab` 안에 `VendingMachineNpc.prefab` 자식으로 박음
- NPC 의 `config` 슬롯에 SO 미리 wire (예: `VendingMachineConfig_RandomBox200`)
- NPC 의 `controller` / `roomController` 슬롯은 **비워둠** — 런타임 자동 탐색

**Dungeon Architect 가 방 procedural 스폰**:
- Vending machine 방이 굴려질 때마다 → 방 prefab 인스턴스 → NPC 자동 등장
- F → NPC 가 Controller 자동 찾음 → 자기 방을 Controller 에 알려줌 → 출구 해제

### 다중 방 동시성

같은 던전에 vending machine 방이 N개 떠도 정상 동작:
- 모두 같은 Panel + Controller 공유 (한 번에 하나만 Open 가능)
- 각 NPC 의 `roomController` 는 *자기 방* 가리킴 → 클리어 시 *자기 방만* 해제
- `_hasResolvedRoomCleared` 는 매 Open() 마다 리셋 → 다른 방 NPC 도 한 번 클리어 가능

---

## 빠른 셋업 (A 옵션 — Vending Machine) ⭐ 권장

### 사용법
1. Unity 에서 셋업할 씬 열기 (예: `Dungeon_1F_Shop_testkhi.unity`)
2. 상단 메뉴 **`LostMemory → Vending Machine → Sync In Active Scene`** 클릭
3. 자동 생성:
   - `Canvas/VendingMachinePanel` — 아이콘 + 이름 + 설명 + 가격 + 구매/Skip 버튼 + 골드 표시
   - `VendingMachineController` GO — 기존 ShopController 의 player ref 자동 복사
   - `VendingMachineNpc` GO — Player 위치 + 우측 3 unit, BoxCollider2D trigger
   - 자산 자동 wire (`VendingMachineConfig_RandomBox200`)
4. **VendingMachineNpc 위치 조정**
5. Play → NPC 가까이 → F → 패널 표시 (랜덤박스 200G) → 구매 또는 Skip

### 메뉴가 안 보이면
- Unity 에서 **`Assets → Refresh`** (또는 Ctrl+R)
- Console 에 컴파일 에러 있는지 확인 — 있으면 그것부터 해결
- Console clear 후 메뉴 다시 확인

### 자산 변경
`VendingMachineConfig_RandomBox200.asset` 인스펙터에서:
- **Item**: 다른 RelicData 로 교체 가능 (포션, 유물 등)
- **Price**: 가격 조정
- **Fallback Label**: item null 일 때 표시할 라벨

---

## (참고) Mystery 카드 시스템 셋업 — 보류 중

> 💤 **CL-227 카드 뽑기 등에서 재사용 예정** — 본 CL 에선 사용 X.

`MysteryShopUIBuilder.cs` 가 ShopUIBuilder 패턴 그대로 — *없는 것만 추가, 기존 인스펙터 값 보존* 의 Sync 방식.

### 사용법
1. Unity 에서 셋업하고 싶은 씬 열기 (예: `Dungeon_1F_Shop_testkhi.unity`)
2. 상단 메뉴 **LostMemory → Mystery Shop → Sync In Active Scene** 클릭
3. 자동으로 생성됨:
   - **Canvas/MysteryShopPanel** (3 카드 슬롯 + Skip 버튼 + 골드 표시) — 평소 비활성
   - **MysteryShopController** GameObject — 기존 ShopController 의 player/wallet/inventory ref **자동 복사**
   - **MysteryNpc** GameObject — Player 위치 + 우측 3 unit 에 BoxCollider2D trigger + interactable
   - NPC 의 `mysteryShopConfig` / `rewardPool` 자산 자동 wire (`MysteryShopConfig_Default` + `RewardPool`)
4. **MysteryNpc 위치 조정** (씬 안에서 원하는 위치로 드래그)
5. Play → Player 가 NPC 가까이 → F 키

### 메뉴 옵션
- **Sync In Active Scene** — 풀 셋업 (없는 것만 추가)
- **Wire References Only** — 오브젝트는 안 만들고 ref만 다시 연결 (prefab 새로 꺼냈을 때 등)

### 자동 wire 되는 ref
| 컴포넌트 | wire 출처 |
|---|---|
| MysteryShopController.panel | 새로 만든 MysteryShopPanel |
| MysteryShopController.inventoryPanel/shortcutBar/playerRelicInventory/playerConsumableInventory/goldWallet/playerAim/playerMovement/playerWeaponPresenter/playerMeleeCombo/playerDash/playerParry | 씬의 기존 `ShopController` 에서 복사 |
| MysteryShopController.roomController | 씬의 첫 번째 `RoomEntryRuntimeController` (없으면 비움 — 단독 검증 OK) |
| MysteryShopNpcInteractable.mysteryShopController | 새 컨트롤러 |
| MysteryShopNpcInteractable.mysteryShopConfig | `MysteryShopConfig_Default.asset` |
| MysteryShopNpcInteractable.rewardPool | `RewardPool.asset` |
| MysteryShopNpcInteractable.playerRelicInventory | 씬의 첫 번째 |

### 주의
- 씬에 기존 `ShopController` 가 있어야 player ref 자동 복사 작동. 없으면 console warning + Inspector 에서 수동 wire 필요
- NPC sprite 는 placeholder (Unity built-in UISprite). 디자인 입히려면 SpriteRenderer.sprite 교체

---

## (수동) Editor 핸드오프 — 단계별 셋업

> ⚠️ 위 Sync 메뉴 사용 권장. 아래는 메뉴가 안 될 경우의 fallback.

### 1. SO 자산 생성

**(a) MysteryShopConfig 자산**
1. Unity Project 창에서 `Assets/_Project/ScriptableObjects/Events/` 폴더 생성 (없으면)
2. 우클릭 → **Create → LostMemory → Events → Mystery Shop Config** → 이름 `MysteryShopConfig_Default.asset`
3. 인스펙터 값:
   - **Slot Prices**: `[80, 200, 400]` (배열 길이 3)
   - **Tier1 Weights** (가격 ≤80): common=80, rare=20, unique=0, legendary=0
   - **Tier2 Weights** (≤250): common=30, rare=50, unique=20, legendary=0
   - **Tier3 Weights** (>250): common=0, rare=30, unique=50, legendary=20
   - **Tier1 Max Price**: 80, **Tier2 Max Price**: 250
   - **Fallback Consumable**: 큰 회복약 RelicData (예: `RelicData_LargeHealPotion.asset`) 드래그

**(b) RoomData 자산**
1. 우클릭 → **Create → LostMemory → Stage → RoomData** → 이름 `RoomData_Event_MysteryShop_01.asset` (`Assets/_Project/ScriptableObjects/Rooms/` 아래)
2. 인스펙터:
   - **Room Id**: `event_mystery_shop_01`
   - **Display Name**: 미스터리 상점
   - **Room Type**: `Event`
   - **Category**: `SmallRoom` (혹은 적당히)
   - **Layout Prefab**: 아래 Step 3 의 prefab 으로 후 연결
   - **Init Context**: `RoomInitContextSpec` 신규 또는 기존 활용
     - **Lock Exit Doors**: ☑ (반드시 — 안 잠그면 NPC 무시 통과 가능)
     - **Player Spawn Anchor Tag**: 빈 값 또는 디자인 따라
   - **Clear Condition**: `AllEnemiesDefeated` (빈 방이라 즉시 충족) 또는 `Custom`/`InteractionComplete` (후자면 controller 의 `NotifyCustomRoomCleared` 가 의미 있음)
   - **Encounter**: 빈 (적 없음)
   - **Reward Pool**: 빈 (이벤트 방은 RewardController 가 skip 함)

### 2. UI Prefab — `MysteryShopPanel.prefab`

**구조 (Hierarchy)**:
```
MysteryShopPanel (Canvas 자식, RectTransform)
├── Background  (Image, dim)
├── Title       (TextMeshProUGUI, "미스터리 상점")
├── GoldText    (TextMeshProUGUI) ── MysteryShopPanelView.goldText 슬롯
├── CardSlots   (HorizontalLayoutGroup)
│   ├── CardSlot1 (MysteryCardView)
│   ├── CardSlot2 (MysteryCardView)
│   └── CardSlot3 (MysteryCardView)
└── SkipButton  (Button)         ── MysteryShopPanelView.skipButton 슬롯
```

**MysteryShopPanel 루트의 `MysteryShopPanelView` 컴포넌트 인스펙터**:
- **Card Views**: 위 3개 CardSlot 드래그 (배열 길이 3)
- **Gold Text**: GoldText 드래그
- **Skip Button**: SkipButton 드래그

### 3. CardSlot Prefab — `MysteryCardSlot.prefab`

**구조 (각 CardSlot 의 자식)**:
```
CardSlot (RectTransform, MysteryCardView)
├── BackRoot       ── MysteryCardView.backRoot 슬롯
│   ├── BackImage  (Image — 카드 뒷면 sprite)
│   ├── BackPriceText (TextMeshProUGUI, "200 G")  ── backPriceText 슬롯
│   └── BuyButton  (Button)                        ── buyButton 슬롯
├── FrontRoot      ── frontRoot 슬롯 (기본 비활성)
│   ├── FrontIcon       (Image)                    ── frontIcon 슬롯
│   ├── FrontNameText   (TextMeshProUGUI)          ── frontNameText 슬롯
│   ├── FrontRarityTag  (TextMeshProUGUI)          ── frontRarityTagText 슬롯
│   └── FrontPriceText  (TextMeshProUGUI, 옵션)    ── frontPriceText 슬롯 (선택)
└── SoldOutOverlay (옵션, 기본 비활성)             ── soldOutOverlay 슬롯
```

**중요**: BackRoot 는 `SetActive(true)`, FrontRoot 는 `SetActive(false)` 가 prefab 기본 상태. Init 시 자동 적용되지만 prefab 도 그렇게 둬야 Editor 미리보기 자연.

### 4. 방 Prefab — `Room_Event_MysteryShop.prefab`

**구조**:
```
Room_Event_MysteryShop (RoomEntryRuntimeController 부착)
├── Floor          (배경)
├── Walls          (Tilemap)
├── ExitWalls      (RoomExitWall 부착, 기본 비활성)
├── EntryAnchors   (RoomEntryAnchor)
└── Npc_MysteryShop  (Sprite + Collider2D[isTrigger] + MysteryShopNpcInteractable)
    └── PromptCanvas (옵션, 비활성 기본)
        └── PromptText ("F")
```

**RoomEntryRuntimeController 인스펙터**:
- **Room Data**: `RoomData_Event_MysteryShop_01` 드래그
- **Auto Open Exits On Cleared**: ☑ (체크 — controller 의 NotifyCustomRoomCleared 가 fire → autoOpen 분기 안 타지만 OpenExits 직접 호출함. 둘 다 작동)

**Npc_MysteryShop > MysteryShopNpcInteractable 인스펙터**:
- **Mystery Shop Controller**: 씬의 `MysteryShopController` 드래그 (Step 5 에서 만들 예정)
- **Mystery Shop Config**: `MysteryShopConfig_Default.asset` 드래그
- **Reward Pool**: 기존 `RewardPool_Default.asset` (Shop 에서 쓰는 것과 동일) 드래그
- **Player Relic Inventory**: 씬의 PlayerRelicInventory 드래그
- **Interact Key**: F (기본)
- **Player Tag**: Player
- **Prompt Object**: 자식 PromptCanvas 드래그 (옵션)

### 5. 씬 GameObject — `MysteryShopController`

**Hierarchy 위치**: 씬의 매니저들 옆 (Shop 의 ShopController 옆이 자연). DDOL 부착 가능.

**MysteryShopController 인스펙터 슬롯**:
| 슬롯 | 값 |
|---|---|
| Panel | 씬의 `MysteryShopPanel` (위 Step 2) |
| Inventory Panel | 씬의 InventoryPanelView (Shop 과 동일) |
| Shortcut Bar | 씬의 ShortcutBarView (Shop 과 동일) |
| Player Relic Inventory | 씬의 PlayerRelicInventory |
| Player Consumable Inventory | 씬의 PlayerConsumableInventory |
| Gold Wallet | 씬의 GoldWallet |
| Player Aim | 씬의 KhiPlayerAim (Player GameObject 위) |
| Player Movement | 씬의 CharacterMovement (Player GameObject 위) |
| Player Weapon Presenter | 씬의 KhiWeaponPresenter |
| Player Melee Combo | 씬의 KhiMeleeComboController |
| Player Dash | 씬의 KhiDashController |
| Player Parry | 씬의 KhiParryController |
| Room Controller | 방 prefab 의 `RoomEntryRuntimeController` (이벤트 방 인스턴스) |

> **씬 전환 시 stale ref**: `ResolveSceneLocalRefs` 가 활성 씬에서 자동 재 wiring 하므로 DDOL 부착도 안전 (Shop 과 동일 패턴).

### 6. 검증 씬 (옵션) — `MAP_event_mysteryshop_test.unity`

빠른 단독 검증용. `Assets/_Project/Scenes/Tests/` 또는 적절한 위치에 신규 씬:
- Camera + Player + Player 컴포넌트 (Aim/Movement/Combo/Dash/Parry/WeaponPresenter)
- GoldWallet (initialGold = 500), PlayerRelicInventory, PlayerConsumableInventory
- Canvas + MysteryShopPanel (Step 2) + InventoryPanel + ShortcutBar
- MysteryShopController (Step 5)
- 방 prefab 1개 인스턴스 (Step 4)

---

## 검증 체크리스트

코드 컴파일 후 Editor 에서:

- [ ] **F-1 정상 구매**: GoldWallet=500, F → 패널 + 3장 (가격만 보임). 200G 슬롯 클릭 → -200G + 카드 reveal + 슬롯 sold-out + 첫 구매 시 출구 해제
- [ ] **F-2 골드 부족**: GoldWallet=50 → 모든 슬롯 어둡게 (afford=false). 클릭 무반응
- [ ] **F-3 Skip**: 구매 안 하고 Skip → 패널 닫힘 + 출구 해제
- [ ] **F-4 봉쇄**: 패널 떠있는 동안 WASD 안 먹힘, 마우스 클릭 시 칼 안 휘둘러짐
- [ ] **F-5 인벤/단축키바 동시 갱신**: Shop 처럼 구매 즉시 인벤토리 슬롯에 들어간 유물 visible
- [ ] **F-6 캐시 재사용**: F → 닫고 다시 F → 같은 카드 + 이전 sold-out 상태 보존
- [ ] **F-7 폴백 소모품**: RewardPool 의 미보유 유물을 일부러 다 보유한 상태로 진입 → 슬롯에 fallbackConsumable (포션) 등장
- [ ] **F-8 절차생성 통합**: `RoomData_Event_MysteryShop_01` 을 RouteSequenceConfig 한 칸에 배치 → 던전 진행 시 정상 등장 + 이전 Combat 방 RewardController 가 본 방 RoomCleared 무시 ([RewardController.cs:133](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/RewardController.cs))

---

## 알려진 한계 / 후속

- **Reveal 애니메이션 없음** — Phase A 는 즉시 swap. 카드 뒤집기 코루틴은 후속 폴리시 (CL-227 의 EventCardView 도 동일 구조 — 공통 베이스 추출 검토)
- **Skip 버튼이 너무 쉬워서 도박감 약함** — 결정 5 (d) "무료 1장 + 추가 유료" 가 게임성 ↑. 후속
- **가격 외 카테고리/등급 힌트 0** — 결정 4 (a) 채택. 등급 힌트는 후속 폴리시
- **Shop 과의 코드 중복** — Controller / NpcInteractable / PanelView 80% 패턴 동일. CL-227/229 까지 끝난 후 공통 베이스 추출 ticket
- **멀티 확장** — `MysteryShopGenerator.Generate` 는 호스트만 호출 + 결과 RPC broadcast 필요. 본 CL 외

---

## 다음 작업 흐름

1. 사용자: 위 Editor 핸드오프 1~6 단계 진행 (SO 자산 생성 → Prefab → 씬)
2. 사용자: 검증 체크리스트 F-1 ~ F-8 진행
3. 검증 통과 → CL 종료 → MR/PR 생성
4. 다음 CL: CL-229 슬롯머신 (이벤트 방 패턴 검증되면 더 빠름)
