# CL-113 (확장) — MVP1 수동 통합 + Shop 방 + 골드 시스템 — 계획

## 진행 현황 (Plan 작성 후 업데이트)

> Plan 의 인벤토리 표 / Step 1 위험 평가는 **작성 시점 스냅샷** 으로 보존. 실제 진행 상태는 본 섹션 참조.

### 완료
- [x] **§3 Combat +50 hook + GoldWallet Reset** — `RunManager.cs` 의 `HandleRoomCleared` (Combat 분기) + `CloseResulting` (Reset) 구현
- [x] **§2 신규 코드 4 파일 — 코드 산출물 단계** — `GoldWallet.cs`, `ShopController.cs`, `ShopNpcInteractable.cs`, `ShopExitTrigger.cs` 모두 작성됨. *씬 wiring + 통합 검증은 미진행*
- [x] **§7 RunResult 최소 wiring** — `RunManager.HandleRestartRequested` (CloseResulting + LoadScene), `HandleLobbyRequested` (CL-117 까지 CloseResulting 만)
- [x] **Step 1 spike 위험 소거** — `Assets/Scenes/MVP1/MVP1.unity` 의 prebuilt 5방 환경에서 `RoomEntryRuntimeController.BeginRoomEntry` → 적 처치 → `RoomCleared` 이벤트 → `RewardController` 보상 패널 → `OpenExits` → 다음 방 자연 진입 chain 이 *수동 배치 module* 에서도 정상 동작함을 4 방 연속 클리어로 확인. `ManualRoomChainController` fallback 불필요. 단 이를 위해 `DungeonRunBootstrap.usePrebuiltLayout` 모드를 신설 (DA Build skip + DungeonBuilt 즉시 발화) — [cl112_mvp1_prebuilt_completion.md](cl112_mvp1_prebuilt_completion.md) 참조
- [x] **수동 6방 씬 결정** — Plan §4 의 `MVP1_Dungeon.unity` 가정 대신 `Assets/Scenes/MVP1/MVP1.unity` 신설로 채택 (현재 5방 Combat 만 배치, Shop / Boss 미배치)

### 진행 중 / 미착수
- [ ] §1 `ShopRoom_Sample.prefab` 신설
- [ ] §4 6방 일렬 배치 완성 (현재 5 Combat → Shop 1 + Combat 4 + Boss 1 로 재구성 필요)
- [ ] §5 방→방 entry chain 통합 검증 (Shop / Boss 포함 6방)
- [ ] §6 Boss 방 wiring 검증 (`BossArea_Test.prefab` 또는 후속)
- [ ] §8 ShopController 의 플레이어 조작 봉쇄 — CL-112 의 `SetCombatInputsBlocked` 패턴 (공격 / 대쉬 / 패링 / 칼 회전 일괄 토글) 재사용 권장

### 연관 산출물
- [cl112_mvp1_prebuilt_completion.md](cl112_mvp1_prebuilt_completion.md) — 본 plan 의 *prebuilt 채택* 결정의 실현 doc + 본 브랜치 코드 산출물 (A 그룹: usePrebuiltLayout, NRE 정리, 보상 폴리시, input 차단 / B 그룹: GoldWallet, Shop scripts, RunManager 골드 hook) 통합 정리

---

## Context

CL-113 원본 scope = *Shop 방 통합 + 골드 시스템 최소* 만. 그러나 MVP 데모 마감일의 critical path 는 *DA 의 절차적 sequencing* 이 아니라 **방 클리어 → 보상/골드 → 다음 방 → 상점 → 보스 → 결과** 의 전체 통합 흐름. 따라서 본 CL 의 scope 를 *수동 6방 1런 환경에서의 통합 동작* 까지 확장.

**핵심 결정**: DA (CL-112) 는 후순위로 미루고, 수동 .unity 씬에 6 module 을 일렬 배치하여 통합 검증. DA 도입은 후속 CL — *DA 가 막혀도 본 CL 산출물이 그대로 데모 fallback*.

기존 ticket (CL-113) 안에서 진행. Jira 분리/재조정은 사용자 별도 처리.

- **상위 계획**: MVP 데모 흐름 통합 (`마을 → 던전 6방 → 결과 → 마을`)
- **선행**: CL-110 (보상 UI ↔ 방 클리어 연동), CL-054 (보스 처치 → RoomCleared)
- **후속**: CL-112 (DA layout, 후순위), CL-115 (골드 시스템 정식), CL-117 (마을 ↔ 던전 씬 전환)

## 사용자 의도 (확장 후 확정)

- (Q1) **DA 후순위** — 본 CL 은 DA 무관. 6방을 *수동 .unity 씬* 에 직접 배치 + 출구 → 다음 방 entry chain
- (Q2) **Shop 클리어 = 출구 trigger** (CL-113 원본 그대로)
- (Q3) **골드 = Run 한정, Combat 클리어 시 +50** (CL-113 원본 그대로)
- (Q4) **Shop 진입 = NPC F 키** (CL-113 원본 그대로)
- (Q5) **Combat / Boss 방 = 기존 동작 활용** — `BossDefeatRoomClearController` (CL-054) + `RewardController` (CL-110) 이미 동작. 본 CL 은 *수동 씬 안에서 wiring + 검증*

## 현 상태 인벤토리

| 영역 | 상태 |
|---|---|
| `RewardController` (Combat 클리어 → 보상 패널) | ✅ CL-110 완료 |
| `BossDefeatRoomClearController` (Boss 처치 → RoomCleared) | ✅ CL-054 완료 |
| `RoomEntryRuntimeController.NotifyCustomRoomCleared()` / `OpenExits()` | ✅ public, payload 자동 |
| `ShopPanelView` 단독 동작 | ✅ Test_Shop.unity 검증 |
| `GoldWallet` | ❌ 신설 (본 CL) |
| Shop 통합 (NPC F 키 / 출구 trigger / 외부 controller) | ❌ 신설 (본 CL) |
| Combat module prefab (`CombatRoom_Sample_Small.prefab`) | ✅ 동작 |
| Boss module prefab | ⚠️ `BossArea_Test.prefab` 짐작 — 본 CL 에서 검증 |
| 수동 6방 1런 .unity 씬 | ❌ 신설 (본 CL) |
| 방→방 entry chain (DA 없이) | ❌ 신설 (본 CL) |
| RunResult 의 OnRestart wiring | ⚠️ prefab 만 — 본 CL 최소 wiring 추가 |
| DA / FlowGraph / Module Database | ⏭️ 후속 CL |

## 결정 사항

### 1. Shop module prefab 신규 (CL-113 원본 그대로)

위치: `client/LostMemory/Assets/_Project/Map/Modules/Shop/ShopRoom_Sample.prefab`

**구조 = Combat module 복제 + 변경**:
- ❌ EnemyEncounterSpawner / SpawnPoints / Encounter 데이터 *제거*
- ✅ Shop NPC GameObject (placeholder sprite + Collider2D + `ShopNpcInteractable`)
- ✅ 출구 zone GameObject (Collider2D trigger + `ShopExitTrigger`)
- ✅ `RoomEntryRuntimeController.roomData` slot = (수동 씬용) RoomData_MVP_Shop_01.asset 임시
- ✅ `RoomEntryRuntimeController.autoOpenExitsOnCleared` 정책은 작업자 결정 (Shop 은 보상 X — 출구 trigger 발화 시점 = 이미 출구 통과로 의미 없음)

> Module Database 의 `shop_room` 슬롯 교체는 본 CL 에서 빠짐 — DA 후순위.

### 2. 신규 코드 4 파일 (CL-113 원본 그대로)

위치: `client/LostMemory/Assets/_Project/Scripts/Runtime/Shop/` (Shop scripts 3) + `Stage/` (GoldWallet)

#### `ShopController.cs`
- `[SerializeField]` 슬롯: `ShopPanelView` / `PlayerRelicInventory` / `GoldWallet` / `KhiPlayerAim` / `CharacterMovement`
- `Open(ShopData)` — `panel.SetActive(true)` + `panel.Init(shopData, inventory, gold.Current)` + 플레이어 조작 봉쇄
- `Close()` — `panel.SetActive(false)` + 플레이어 조작 복원
- `HandleItemPurchased(ShopItemData)` — `panel.OnItemPurchased` 구독. GoldWallet 와 sync (`gold.Spend(item.Price)` + `panel.UpdateGold(gold.Current)`)
- 닫기 키 = F 토글 default (작업자 결정)

#### `ShopNpcInteractable.cs`
- `[SerializeField]` 슬롯: `ShopController` / `ShopData` / `KeyCode interactKey = KeyCode.F`
- 플레이어 trigger zone in/out flag (`OnTriggerEnter2D` / `OnTriggerExit2D`)
- in-range + F 키 입력 → `shopController.Open(shopData)`
- 화면에 *F 누르세요* placeholder 표시 (시간 부족 시 생략)

#### `ShopExitTrigger.cs`
- `[SerializeField] RoomEntryRuntimeController roomController`
- `OnTriggerEnter2D(Collider2D other)` — Player 검증 → `roomController.NotifyCustomRoomCleared()`
- 한 번 발화 후 self-disable

#### `GoldWallet.cs`
위치: `client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/GoldWallet.cs`
- `int Current { get; private set; }`
- `event Action<int> Changed`
- `void Add(int amount)` (음수 거부 + Changed 발화)
- `bool Spend(int amount)` (부족 시 false 반환 + Changed 발화)
- `void Reset()` (initialGold 로 복원 + Changed 발화)
- `[SerializeField] int initialGold = 0`
- Run 한정 — `RunManager.CloseResulting` 에서 `Reset()` 호출

### 3. Combat 클리어 +50 골드 hook (CL-113 원본 그대로)

`RunManager.HandleRoomCleared(payload)` 의 Combat 분기에 추가:

```csharp
if (payload.Data.RoomType == StageRoomType.Combat)
{
    if (goldWallet != null) goldWallet.Add(50); // CL-113: Combat 클리어 보상 골드
}
```

`RunManager` 에 `[SerializeField] GoldWallet goldWallet` 필드 추가. `CloseResulting` 끝에 `goldWallet?.Reset()`.

### 4. **신규**: 수동 6방 1런 .unity 씬

위치: `client/LostMemory/Assets/_Project/Scenes/MVP1_Dungeon.unity` (신설) — 또는 *기존 `test_khi.unity` 확장* (작업자 결정. 신설 권장 — test_khi 는 Khi 캐릭터 단위 테스트용)

**구조**: 6 module 일렬 배치 (X 축, 50 unit 간격)
```
Combat_1 → Combat_2 → Shop → Combat_3 → Combat_4 → Boss
```

**씬 GameObject**:
- 6 module prefab 인스턴스 (각자 자기 `RoomEntryRuntimeController` 보유 — closure 캡처 패턴 CL-110)
- `RunManager` (필드 wiring: rewardController / playerRelicInventory / goldWallet / runResultPanelView)
- `RewardController` (CL-110 산출 — Inspector wiring 그대로)
- `GoldWallet` (본 CL 산출)
- `PlayerRelicInventory` (CL-109 산출)
- `RunResultPanelView` prefab (CL-110 progress 후속 5 — 최소 wiring)
- Player (TestKhi) — Combat_1 entry zone 위치
- CinemachineCamera — Player 추적

**module 별 설정**:
- Combat_1~4: `autoOpenExitsOnCleared = false` (RewardController 가 OpenExits 트리거)
- Shop: 작업자 결정 (위 `1.` 참조)
- Boss: `autoOpenExitsOnCleared = true` (보상 X — 자동 OK) + `BossDefeatRoomClearController` 부착

### 5. **신규**: 방 → 방 entry chain (DA 없이)

DA 가 없으므로 *각 방 출구 너머에 다음 방의 entry trigger* 가 자연스럽게 위치. 흐름:

```
플레이어 출구 trigger 진입
→ NotifyCustomRoomCleared
→ RoomEntryRuntimeController 의 RoomCleared event
→ (Combat) RewardController 가 보상 패널 → 카드 선택 → OpenExits
  (Shop) ShopExitTrigger 가 즉시 NotifyCustomRoomCleared (이미 출구 진입) — autoOpenExitsOnCleared 결과
  (Boss) BossDefeatRoomClearController 가 NotifyCustomRoomCleared → RunResult
→ 출구 벽 비활성
→ 플레이어가 다음 방 RoomEntryRuntimeController 의 entry trigger 진입
→ 다음 방 자연 활성화 (적 spawn / 카메라 confiner / 등)
```

**전제 (Step 1 spike 로 검증)**: `RoomEntryRuntimeController.BeginRoomEntry` 가 *씬에 미리 배치된 instance* 에서도 DA 환경과 동일 동작. 코드가 DA spawn 시점 가정으로만 짜여있으면 수동 chain manager 필요.

### 6. Boss 방 wiring 검증 (기존 동작 활용)

`BossDefeatRoomClearController.cs` (CL-054) 가 이미 Boss 처치 → `NotifyCustomRoomCleared` 발화. RewardController 는 `RoomType==Boss` 시 보상 패널 안 띄움 (CL-110 동작 검증됨). 본 CL 은 *수동 .unity 씬* Boss module 에:
- BossDefeatRoomClearController 부착 검증
- `roomData.RoomType=Boss` 검증
- Boss 처치 → RoomCleared → RunManager Boss 분기 → `RunResultPanelView.Show(success=true)` 흐름 검증

### 7. RunResult 최소 wiring

CL-110 progress 의 후속 ticket 5 (RunResult OnRestart / OnLobby) 는 본 CL 검증 시 부딪히므로 *최소* 추가:
- `OnRestart` → `RunManager.CloseResulting()` + `SceneManager.LoadScene(currentScene)`
- `OnLobby` → 보류 (CL-117 마을 씬 로드에서 정식 처리)

### 8. 플레이어 조작 봉쇄 (CL-113 원본 그대로)

ShopController.Open / Close 시:
- 이동: `playerMovement.MovementForbidden = true / false`
- 조준 / 공격: `playerAim.enabled = false / true`
- timeScale: 변경 X (적 없는 방)

## 본 CL 범위 외 (후속 CL)

| 후속 CL | 내용 |
|---|---|
| **CL-112 (DA layout)** | RoomData asset 6 개 + FlowGraph 사본 (`SnapFlowGraph2D_MVP.asset`) + Module Database 사본 (`SnapModuleDatabase_MVP.asset`) + `shop_room` 카테고리 + DungeonRunBootstrap 시드 고정. **본 CL 의 module prefab 그대로 재사용** — DA 가 spawn 만 대신함 |
| **CL-117 (마을 ↔ 던전 씬 전환)** | 마을 씬에서 본 CL 의 `MVP1_Dungeon.unity` LoadScene |
| **CL-115 (골드 시스템 정식)** | 메타 누적 / 영구 골드 / 마을 NPC 환전 |

## 구현 작업 순서 (예상 4~5 시간)

1. **30 분 spike — 수동 배치 module entry 동작 확인** — `RoomEntryRuntimeController` 1 개 씬 직접 배치 + Player 진입 → BeginRoomEntry 자연 발화 확인. *실패 시 plan 갱신 (수동 chain manager 추가)*
2. `GoldWallet.cs` + `RunManager` wiring + Combat +50 hook (15 분)
3. Shop scripts 3 + `ShopRoom_Sample.prefab` (60 분)
4. 수동 6방 .unity 씬 작성 — module 일렬 배치 + entry chain 검증 (60 분)
5. Boss 방 wiring + RunResult 최소 wiring (30 분)
6. 통합 검증 — Run 시작 → 6방 → 결과 (60 분)
7. `cl113.md` 완료 보고서 (30 분)

## 위험 / 의사결정

### 1. **신규**: 수동 배치 module 의 entry 자연 동작 (가장 큰 위험)

`RoomEntryRuntimeController.BeginRoomEntry` 가 DA 환경 가정으로 코드되어 있을 가능성. *씬에 미리 배치된 instance* 에서 동일 동작 안 하면 수동 chain wiring 필요.

- **spike 절차** (Step 1, 30 분): `RoomEntryRuntimeController` 1 개 씬 직접 배치 + Player entry trigger 진입 → BeginRoomEntry / spawn / OnRoomEntered 자연 발화 확인
- **성공 시**: 본 plan 그대로 진행
- **실패 시 fallback**: `ManualRoomChainController` 신설 — *현재 활성 방 trace + entry 신호 수신 시 다음 방 enable*. 작업 60~90 분 추가

### 2. ShopPanel 닫기 메커니즘 (CL-113 원본)

F 키 토글 default. 또는 ESC. 작업자 판단.

### 3. NPC *F 누르세요* 표시 (CL-113 원본)

placeholder = world space text 또는 sprite. 시간 부족 시 생략.

### 4. 골드 영구 누적 vs Run 한정 (CL-113 원본)

Run 한정 — `CloseResulting` 시 Reset. 시연 용이성 ↑.

### 5. .unity 씬 throwaway 가능성

DA 도입 (CL-112 후속) 시 수동 .unity 씬은 throwaway 가능. 단:
- module prefab / Shop scripts / GoldWallet / RewardController / RunManager wiring 모두 재사용
- 수동 씬 자체도 *DA 막힐 시 데모 fallback* 으로 보존 권장

### 6. Boss module prefab 미확인

`BossArea_Test.prefab` 의 정식 동작 미검증. Step 5 에서 부딪히면 별 ticket spinoff 또는 Combat module 임시 활용.

## 핵심 파일

### 신규
- `client/LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopController.cs`
- `client/LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopNpcInteractable.cs`
- `client/LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopExitTrigger.cs`
- `client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/GoldWallet.cs`
- `client/LostMemory/Assets/_Project/Map/Modules/Shop/ShopRoom_Sample.prefab`
- `client/LostMemory/Assets/_Project/Scenes/MVP1_Dungeon.unity` (신설 또는 test_khi.unity 확장)
- (조건부) `client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/ManualRoomChainController.cs` — Step 1 spike 실패 시

### 수정
- `client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/RunManager.cs` — `goldWallet` 필드 + Combat +50 + CloseResulting Reset + RunResult OnRestart wiring
- (조건부) `client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeController.cs` — Step 1 spike 결과에 따라 수동 배치 분기 추가 가능

### 참조 (읽기 전용)
- [ShopPanelView.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopPanelView.cs)
- [ShopData.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopData.cs)
- [BossDefeatRoomClearController.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/BossDefeatRoomClearController.cs)
- [RoomEntryRuntimeController.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeController.cs)
- [RewardController.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/RewardController.cs)
- [RunManager.cs](../../client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/RunManager.cs)
- [cl110_progress.md](cl110_progress.md) — closure 캡처 패턴 / 후속 ticket 5

## 검증 방법

### 본 CL 통과 기준 (12 항목)

**골드 / Combat 흐름**
1. Run 시작 → 골드 = 0 console 로그
2. Combat_1 클리어 → 보상 패널 → 카드 선택 → 골드 50 → 출구 열림 → 다음 방
3. Combat_2 클리어 → 동일 → 골드 100
4. Combat_3 클리어 → 동일 → 골드 150
5. Combat_4 클리어 → 동일 → 골드 200

**Shop 흐름**
6. Shop 방 진입 → 적 0 / NPC 1 / 출구 trigger 존재. 패널 자동 출현 X
7. NPC 근접 + F → ShopPanel 활성. 플레이어 이동/조준 봉쇄
8. 상품 구매 → 골드 차감 + RelicData PlayerRelicInventory 추가 + 매진 표시
9. F (또는 ESC) → ShopPanel 비활성. 플레이어 조작 복원
10. 출구 trigger 통과 → `[Controller] RoomCleared` 로그 → 다음 방 (Combat_3) 진입

**Boss / 종료 흐름**
11. Boss 방 진입 → Bertha 자동 등장 → 처치 → `[BossDefeat] RoomCleared` 로그 → RunResult 패널 (success=true)
12. RunResult OnRestart → `CloseResulting` → 골드 0 Reset 로그 → 씬 재로드

## 작업자 메모 (TODO)

- [ ] **Step 1 spike 결과** — 통과/실패 기록 + 실패 시 plan 갱신 (`ManualRoomChainController` 신설)
- [ ] 수동 6방 씬 = `test_khi.unity` 확장 vs `MVP1_Dungeon.unity` 신설 결정 — 신설 권장
- [ ] NPC sprite — placeholder (TestKhi sprite 또는 빨간 박스)
- [ ] Shop 방의 `autoOpenExitsOnCleared` 정책 결정
- [ ] ShopPanel 닫기 키 결정 (F 토글 / ESC / 둘 다)
- [ ] Boss module prefab 정식 동작 확인 — 안 되면 별 ticket spinoff
- [ ] `cl113.md` 완료 보고서 작성
