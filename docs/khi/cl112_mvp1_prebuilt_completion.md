# CL-112 (실현형) — MVP1 수동 prebuilt 통합 완료

브랜치: `feat/S14P31C201-351/da-던전-레이아웃-정의-전투-4-상점-1`

## Context

CL-112 의 원본 scope = *DA(DungeonArchitect) 절차적 레이아웃*. 그러나 [`cl113_shop_integration_plan.md`](cl113_shop_integration_plan.md) 의 결정에 따라 **DA 는 후순위로 미루고, 6방을 수동 .unity 씬에 직접 배치하는 prebuilt 통합** 으로 pivot. 본 doc 은 그 pivot 의 산출물을 정리한다.

문서 scope = 이 브랜치에서 진행된 두 그룹 작업의 통합 정리:

| 그룹 | 내용 |
|---|---|
| **A** | MVP1 수동 prebuilt 통합 + 부수 정리 + 보상 흐름 폴리시 |
| **B** | CL-113 (Shop + Gold) 의 코드 베이스 prep — 본 브랜치에서 같이 진행 |

> CL-113 의 *씬 wiring + 검증 단계* 는 후속 doc (`cl113_*_completion.md`) 에서 다룸. 본 doc 은 *코드 산출물까지* 만 기록.

---

## A 그룹 — MVP1 수동 prebuilt 통합

### A1. `DungeonRunBootstrap` — `usePrebuiltLayout` 모드 도입

**파일**: [`DungeonRunBootstrap.cs:50-52, 100-109`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/DungeonRunBootstrap.cs:50)

**문제**: MVP1.unity 처럼 방이 씬에 직접 배치된 케이스에서 `dungeon.Build()` 가 LayoutGraph 미설정으로 NRE → `OnSpawnedManagedObjects` 미호출 → `DungeonBuilt` 이벤트 미발화 → `RunManager.HandleDungeonBuilt` 미실행 → `RewardController.SubscribeAllRoomControllers()` *한 번도 안 됨* → RoomCleared 이벤트는 정상 발화하지만 듣는 사람이 없어 보상 UI 안 뜸 + 출구도 안 열림.

**해결**: `usePrebuiltLayout` SerializeField 추가. true 일 경우 `dungeon.Build()` skip + `DungeonBuilt` 즉시 발화. `RunManager` 와 `RewardController` 의 `SubscribeAllRoomControllers` 가 `FindObjectsOfType<RoomEntryRuntimeController>()` 로 씬에 미리 배치된 방을 그대로 발견.

**MVP1.unity wiring**: [`MVP1.unity:2838-2842`](../../client/LostMemory/Assets/Scenes/MVP1/MVP1.unity:2838) — prefab instance modification 으로 `usePrebuiltLayout: 1` override (test_khi.unity 영향 없음 — 같은 MapMaker_LM prefab 공유하지만 instance override 만).

### A2. TopDownEngine 데모 잔여물 NRE 정리

**파일**:
- [`OrcMeleeWeapon.prefab`](../../client/LostMemory/Assets/_Project/Prefabs/Weapons/OrcMeleeWeapon.prefab)
- [`OrcMeleeWeapon4Dir.prefab`](../../client/LostMemory/Assets/_Project/Prefabs/Weapons/OrcMeleeWeapon4Dir.prefab)

**제거된 컴포넌트** (두 prefab 모두 동일):

| 컴포넌트 | script GUID | 증상 |
|---|---|---|
| `ItemPicker` (InventoryEngine) | `86fd837a0abc28d4d91c55590bd2c090` | `FindInventory` NRE — 존재하지 않는 inventory 이름 검색 |
| `DeadlineCollectible` (TopDownEngine 데모) | `117209d925eeb13429ff554fe3bec186` | `DeadlineProgressManager.LoadSavedProgress` NRE — Deadline 데모 의존 |
| `PickableItem` (TopDownEngine) | `1feff4042ae759c49a290443b9bfdd71` | 오크 무기 collider 가 플레이어 collider 와 닿을 때마다 *픽업 트리거* → MMFeedbacks 체인 발화 → `DeadlineCollectionExplosion(Clone)` 흰색 puff 스폰 + 의도치 않은 카메라 쉐이크 |
| `MMF_Events` 의 `PlayEvents.m_Calls` | — | `Collect` 메서드 호출 콜백 (DeadlineCollectible 의존) 정리 |

**남은 잔여물 (의도적 보존)**:
- 자식 GameObject `PickedMMFeedbacks` (MMFeedbacks 본체) — 더 이상 *참조되지 않아* 런타임 inert. prefab 트리에 남지만 무해. 향후 Unity Editor 에서 cleanup 가능.

### A3. 보상 패널 0.5초 표시 지연

**파일**: [`RewardController.cs:25-27, 130-139`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/RewardController.cs:25)

**문제**: 마지막 적 사망 → 즉시 `Time.timeScale = 0` + 패널 등장 → 플레이어 공격 모션 도중에 패널이 떠 *공격 입력이 끊기는* UX 이슈.

**해결**:
- `[SerializeField] float rewardShowDelay = 0.5f` 추가 (Min 0)
- `HandleRoomClearedFromController` 가 `ShowReward()` 직접 호출 → `StartCoroutine(DelayedShowReward())` 로 변경
- `DelayedShowReward` 코루틴 = `WaitForSecondsRealtime(rewardShowDelay)` 후 `ShowReward()` 호출
  - `WaitForSecondsRealtime` = timeScale 무관 (안전 차원)
  - `rewardShowDelay > 0` 가드 — 0 설정 시 즉시 호출 (디버그 편의)
- `_isShowingReward = true` 가드를 *delay 시작 시점* 으로 선점 → delay 도중 다른 방 클리어가 끼어들어 reward 가 큐잉되는 이슈 방지

### A4. 보상 패널 표시 동안 input + 칼 회전 차단

**문제**: `Time.timeScale = 0` 만으론 차단되지 않는 동작들이 있음 — Update 는 timeScale 무관하게 실행되므로 input 읽기와 일부 매 프레임 갱신은 그대로 동작. 이로 인해 보상 패널 떠있는 중에도:
- 마우스 좌클릭으로 공격 swing 가능 (KhiMeleeComboController)
- Shift 로 대쉬 가능 (KhiDashController)
- 우클릭으로 패링 가능 (KhiParryController)
- 칼이 마우스 따라 계속 회전 (KhiWeaponPresenter)

**해결**:

[`KhiParryController.cs:67, 138-141`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiParryController.cs:67)
- `public bool ExternalBlock { get; set; }` 추가 (`KhiMeleeComboController` 와 동일 패턴)
- `HandleParryInput()` 첫 줄에 `if (ExternalBlock) return;` 가드

[`RewardController.cs:24-31, 199-207`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/RewardController.cs:24)
- 4개 SerializeField 추가:
  - `KhiWeaponPresenter playerWeaponPresenter` — 칼 회전 Update 차단
  - `KhiMeleeComboController playerMeleeCombo` — 공격 input
  - `KhiDashController playerDash` — 대쉬 input
  - `KhiParryController playerParry` — 패링 input
- `SetCombatInputsBlocked(bool block)` 헬퍼 메서드 신설:
  - melee/parry: `ExternalBlock = block`
  - dash: `PermitAbility(!block)` (TopDownEngine 표준)
  - weaponPresenter: `enabled = !block` (Update 차단 → 칼 마지막 프레임 위치 그대로 freeze)
- 호출 지점:
  - `ShowReward()` 안에서 `SetCombatInputsBlocked(true)`
  - `HandleRewardSelected()` 안에서 `SetCombatInputsBlocked(false)`
  - `OnDisable()` panic restore 안에서 `SetCombatInputsBlocked(false)` — 영구 잠금 방지

**의도적으로 차단 안 한 구간**: 0.5초 delay 동안 (A3) 은 input 차단 미적용. 사용자가 마지막 공격 모션을 자연 종료할 수 있도록 *의도된 자유 시간*. 차단은 패널이 실제로 뜬 순간 (`ShowReward` 안) 부터.

**참고 — 기존 `playerAim.enabled = false` 코드 잔존**: KhiPlayerAim 자체엔 Update 가 없어 enabled 토글은 사실상 no-op. 향후 KhiPlayerAim 에 Update 가 추가될 가능성 대비해 코드와 SerializeField 는 보존. tooltip 에 명시.

### A5. (사이드) — `KhiPlayerAim` no-op 토글의 문서화

[`RewardController.cs:22`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/RewardController.cs:22) 의 `playerAim` 필드 tooltip 에 "사실상 no-op — 실제 칼 회전 차단은 playerWeaponPresenter 슬롯" 명시. *시간 흘러도 후속 개발자가 잘못 짚지 않도록* 문서화.

---

## B 그룹 — CL-113 prep (Shop + Gold 베이스 코드)

> 본 그룹은 *코드 산출물 단계까지*. 씬 wiring (Shop room prefab, MVP1 6방 배치, Gold UI 등) 은 후속.

### B1. `GoldWallet.cs` — Run 한정 골드 시스템

**파일**: [`GoldWallet.cs`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/GoldWallet.cs) (신설, 96 라인)

`Run` 단위로 골드를 보유하는 `MonoBehaviour`. `Add(int)`, `TrySpend(int)`, `ResetToInitial()` API 노출. Run 종료 시 `RunManager.CloseResulting()` 이 `ResetToInitial()` 호출 → 다음 Run 은 0 부터 시작.

### B2. `ShopController.cs` — Shop 방 오케스트레이터

**파일**: [`ShopController.cs`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopController.cs) (신설, 157 라인)

Shop 방 진입 시 `ShopPanelView` 표시 + `PlayerRelicInventory` / `GoldWallet` / `KhiPlayerAim` / `CharacterMovement` wiring. 보상 흐름의 `RewardController` 와 같은 *외부 input 차단* 패턴 (timeScale + aim + movement) 적용.

### B3. `ShopNpcInteractable.cs` — F 키 상호작용

**파일**: [`ShopNpcInteractable.cs`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopNpcInteractable.cs) (신설, 85 라인)

플레이어가 NPC 근처에서 F 키 누르면 `ShopController.OpenShop()` 호출. cl113 plan 의 결정 #4 ("Shop 진입 = NPC F 키").

### B4. `ShopExitTrigger.cs` — 출구 trigger

**파일**: [`ShopExitTrigger.cs`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopExitTrigger.cs) (신설, 54 라인)

플레이어가 Shop 방 출구 trigger 진입 → `RoomEntryRuntimeController.NotifyCustomRoomCleared()` 호출 → RoomCleared 이벤트 발화 → 다음 방 진입 가능. cl113 plan 결정 #2 ("Shop 클리어 = 출구 trigger").

### B5. `RunManager` — 골드 wiring + 결과 패널 버튼 핸들러

**파일**: [`RunManager.cs`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/RunManager.cs)

추가된 변경:

| 위치 | 변경 |
|---|---|
| line 38-39 | `[SerializeField] GoldWallet goldWallet` 슬롯 |
| `OnEnable` (line 86-89) | `runResultPanelView.OnRestart` / `OnLobby` 핸들러 구독 |
| `OnDisable` (line 109-113) | 동일 unsub |
| `CloseResulting` (line 175-179) | `goldWallet.ResetToInitial()` — Run 한정 골드 초기화 |
| `HandleRoomCleared` (line 238-242) | Combat 방 클리어 시 `goldWallet.Add(50)` (cl113 plan 결정 Q3) |
| `HandleRestartRequested` (line 320-326) | `CloseResulting()` + `SceneManager.LoadScene(activeScene.buildIndex)` |
| `HandleLobbyRequested` (line 329-333) | TODO 주석 (CL-117 마을 씬 도입 전 까지 `CloseResulting()` 만) |

---

## 검증 결과

### 정적

- 컴파일 에러 0
- `using` 문 보강: `RewardController.cs` 에 `System.Collections` (코루틴), `RunManager.cs` 에 `UnityEngine.SceneManagement` (LoadScene)

### 런타임 (MVP1.unity)

플레이 로그로 확인된 흐름 (4방 연속):
```
[RunManager] None -> Initializing
[DungeonRunBootstrap] usePrebuiltLayout=true. Skipping DA Build; firing DungeonBuilt directly.
[RunManager] Subscribed to 5 room controllers.
[RewardController] Subscribed to 5 room controllers.
[RunManager] Initializing -> InRun
─ Module_Bridge 1 클리어 → 보상 (질풍 장화) → 다음 방 ─
─ Module_Bridge 2 클리어 → 보상 (붉은 송곳니) → 다음 방 ─
─ Module_Bridge 3 클리어 → 보상 (반격의 표식) → 다음 방 ─
─ Module_Bridge 4 클리어 → 보상 (분쇄의 팔찌) ─
```

### 부작용 검증

| 이전 NRE | 결과 |
|---|---|
| `InventoryEngine.Inventory.FindInventory` | ✅ 사라짐 |
| `DeadlineProgressManager.LoadSavedProgress` | ✅ 사라짐 |
| `ItemPicker.Pickable` (PickableItem 경유) | ✅ 사라짐 |
| `DungeonArchitect.Dungeon.Build` | ✅ 사라짐 (`usePrebuiltLayout` 분기로 회피) |
| `DeadlineCollectionExplosion(Clone)` 스폰 | ✅ 사라짐 (PickableItem 제거로 발화 chain 끊김) |

### 알려진 보존 잔여물 (의도)

- 에디터 인스펙터 오류 `MissingReferenceException ... GameObjectInspector / TransformInspector` — 런타임 무관, Inspector 캐시 cosmetic.
- `OrcMeleeWeapon*.prefab` 의 `PickedMMFeedbacks` 자식 GameObject — 더 이상 참조 없음, inert. 향후 Unity Editor 에서 cleanup 가능.
- `RewardController.playerAim` 슬롯 토글 코드 (no-op) — 향후 KhiPlayerAim 의 Update 추가 가능성 대비 보존.

---

## 미검증 영역

| 항목 | 상태 |
|---|---|
| Module_Bridge 5 / Boss 방 클리어 → `RunCleared` → `RunResultPanel` | 다른 작업자 진행 (사용자 지시) |
| Shop 방 실제 통합 (씬에 ShopRoom prefab 배치 + ShopController wiring) | CL-113 본체 작업 |
| 6방 1런 완주 → Restart / Lobby 버튼 동작 | 후속 |

---

## 핵심 파일 인덱스

### 변경
- [`DungeonRunBootstrap.cs`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/DungeonRunBootstrap.cs)
- [`RewardController.cs`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/RewardController.cs)
- [`RunManager.cs`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/RunManager.cs)
- [`KhiParryController.cs`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiParryController.cs)
- [`MVP1.unity`](../../client/LostMemory/Assets/Scenes/MVP1/MVP1.unity)
- [`OrcMeleeWeapon.prefab`](../../client/LostMemory/Assets/_Project/Prefabs/Weapons/OrcMeleeWeapon.prefab) / [`OrcMeleeWeapon4Dir.prefab`](../../client/LostMemory/Assets/_Project/Prefabs/Weapons/OrcMeleeWeapon4Dir.prefab)

### 신설 (B 그룹)
- [`GoldWallet.cs`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/GoldWallet.cs)
- [`ShopController.cs`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopController.cs)
- [`ShopNpcInteractable.cs`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopNpcInteractable.cs)
- [`ShopExitTrigger.cs`](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopExitTrigger.cs)
