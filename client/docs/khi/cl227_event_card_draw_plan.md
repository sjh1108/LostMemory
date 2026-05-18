# CL-227: 이벤트 방 — 카드 뽑기 (Card Draw)

**Epic**: I. 이벤트 방 / 메타 보상 루프
**상태**: 📋 **plan only** — 미구현
**선행**: CL-110 (`RewardPanelView` / `RewardController`), CL-113 (`ShopNpcInteractable` 패턴), CL-032 (`StageRoomType.Event` enum 확정)
**관련 후속**: CL-228 (이벤트 — 아이템 구매), CL-229 (이벤트 — 슬롯머신)

> 이벤트 방 3종 시리즈 첫 번째. 솔로 우선, 후속 멀티 친구방 확장 고려. **3종 모두 같은 NPC 상호작용 + 패널 봉쇄 + 방 클리어 패턴을 공유** 하므로 본 plan 의 공통 베이스를 CL-228 / CL-229 가 재사용.

---

## Context

이벤트 방은 *전투 없이 랜덤 보상* 을 주는 방. `StageRoomType.Event` 는 [StageRoomType.cs:8](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/StageRoomType.cs) 에 이미 등록돼 있고, [RewardController.cs:133](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/RewardController.cs) 의 분기에서 *Combat 만* 자동 보상 — 즉 Event 방은 자체 흐름이 필요.

### 카드 뽑기의 의도

플레이어가 방에 들어가면 NPC (또는 제단/카드 더미 오브젝트) 가 있고, F 키로 상호작용 → **3장의 뒷면 카드** 표시 → 1장 선택 → 카드 뒤집힘 + 보상 획득. 이미 `RewardPanelView` 가 *앞면* 카드 3택을 잘 처리하고 있으므로, 본 CL 의 핵심 차별점은:

1. **카드가 뒷면으로 시작** — 선택 후 뒤집힘 애니메이션 → 보상 reveal
2. **보상 풀이 일반 RewardPool 보다 다양** — 유물, 소모품, **골드** 까지 포함 (RewardPool 은 RelicData 만)
3. **방 진입 시 자동 트리거 X** — 플레이어가 NPC 와 상호작용해야 시작 (한 번만 가능)
4. **뽑은 후 출구 열림** — 방 클리어 = 카드 1장 선택 완료

### 기존 시스템과의 관계

| 시스템 | 재사용 여부 | 비고 |
|---|---|---|
| `RewardPanelView` ([cs:19](../../LostMemory/Assets/_Project/Scripts/Runtime/Rewards/RewardPanelView.cs)) | **재사용 X** — 앞면 카드 + RelicData 한정. 본 CL 은 별도 `EventCardDrawPanelView` | 패턴은 참조 (Init/Show/Selected 이벤트 형태) |
| `RewardCardView` ([Glob](../../LostMemory/Assets/_Project/Scripts/Runtime/Rewards/RewardCardView.cs)) | **재사용 X** — 등급/유물 표시 전용. 본 CL 은 `EventCardView` (뒷면 + 뒤집힘 + 보상 종류별 표시) | |
| `RewardController` ([cs:19](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/RewardController.cs)) | **재사용 X** — RoomCleared 자동 구독 + Combat 한정. 본 CL 은 `EventCardDrawController` (NPC trigger 기반) | 봉쇄/복원 패턴은 그대로 가져옴 |
| `ShopNpcInteractable` ([cs:26](../../LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopNpcInteractable.cs)) | **패턴 복제** — `EventCardDrawNpcInteractable` (F 키 trigger zone) | playerTag/promptObject 슬롯 그대로 |
| `GoldWallet.Add` ([cs:59](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/GoldWallet.cs)) | **재사용** — 골드 보상 시 호출 | |
| `PlayerRelicInventory.TryAdd` ([cs:48 영역](../../LostMemory/Assets/_Project/Scripts/Runtime/Relics/PlayerRelicInventory.cs)) | **재사용** — 유물/소모품 보상 시 호출 | 소모품 자동 라우팅 |
| `RoomEntryRuntimeController.NotifyCustomRoomCleared` ([cs:139](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeController.cs)) | **재사용** — 카드 픽 후 호출 → 출구 벽 비활성 | `autoOpenExitsOnCleared=true` 면 자동 |
| `ResolveSceneLocalRefs` 패턴 ([ShopController.cs:169](../../LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopController.cs)) | **재사용** — DontDestroyOnLoad 부착 시 stale ref 방지 | controller 가 DDOL 이면 필수 |

---

## 결정해야 할 사항 (사용자 확인 필요)

| # | 결정 | 후보 | 추천 |
|---|---|---|---|
| 1 | 카드 수 | 3 / 5 / 보상 풀 크기에 따라 가변 | **3** — 선택 부담 최소, 패널 좁아도 OK |
| 2 | 뒤집힘 애니메이션 | (a) 즉시 reveal, (b) 1초 카드 뒤집기 코루틴, (c) 카드 위로 떴다가 뒤집힘 | **(b)** — 가장 간단하면서 임팩트. (c) 는 후속 폴리시 |
| 3 | 보상 종류 | 유물 / 소모품 / 골드 / **체력 회복** / **기억(메타) 조각** | **유물 + 소모품 + 골드** 3종 (Phase A). 메타/체력은 후속 |
| 4 | 보상 풀 정의 | (a) 새 SO `EventCardRewardPool`, (b) 기존 `RewardPool` 재사용 + 골드 옵션 추가 | **(a)** — 골드/소모품/유물 가중치를 독립 튜닝 가능. RewardPool 은 RelicData 전용으로 유지 |
| 5 | 픽 횟수 | 1픽 (선택 즉시 종료) / N픽 / **유료 추가 뽑기** | **1픽 고정** Phase A. 후속에서 골드로 추가 픽 |
| 6 | 카드 비주얼 — 뒷면 | 단일 디자인 / 등급/종류별 다른 뒷면 (단, 정보 누설 X) | **단일 디자인** — 정보 누설 방지 |
| 7 | 방 클리어 시점 | 카드 픽 직후 / 패널 닫은 후 / NPC 다시 상호작용으로 명시적 종료 | **픽 직후** — 자동으로 출구 열림. 플레이어는 보상 토스트 보고 자유 이동 |
| 8 | 멀티 확장 시 권위 | 호스트 추첨 + 결과 broadcast / 각 클라 독립 추첨 | **호스트 추첨** (CL-229 슬롯머신과 동일 정책으로 통일). Phase A 솔로 — 본 CL 외 |

> ⚠️ **결정 4 가 가장 중요**. (a) 채택 시 본 plan 이 새 SO 정의 포함, (b) 채택 시 `RewardPool.cs` 에 골드 옵션 추가 필요. **(a) 추천**.

---

## 시스템 사실

### `StageRoomType.Event` 활용 현황
- enum 자체는 [StageRoomType.cs:8](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/StageRoomType.cs) 에 등록
- [RewardController.cs:133](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/RewardController.cs) 가 `payload.Data.RoomType != StageRoomType.Combat` 인 방을 *보상 skip* — 즉 자체 흐름 필요
- 현재 Event 방 RoomData asset 은 (확인 필요 — `Assets/_Project/ScriptableObjects/Rooms/` 에서 `RoomType=Event` 인 게 있는지 grep 후 결정)

### 보상 패널의 timeScale=0 패턴 ([RewardController.cs:194-200](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/RewardController.cs))
- 보상 떠있는 동안 `Time.timeScale=0` + 마우스 조준 비활성 + 공격/대쉬/패링 input 차단
- 본 CL 은 *이벤트 방엔 적이 없음* → timeScale 정지 불필요. **Shop 방식 ([ShopController.cs:19](../../LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopController.cs)) 채택** — 이동/조준만 봉쇄, timeScale 그대로

### NPC 상호작용 패턴 ([ShopNpcInteractable.cs:64-86](../../LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopNpcInteractable.cs))
- BoxCollider2D trigger → `_playerInRange` 토글
- F 키 입력 → `controller.Toggle(data)` 호출
- `promptObject` 슬롯 — in-range 시 활성, 이탈 시 비활성 ("F 누르세요" 표시)

### 출구 잠금 / 해제 ([RoomEntryRuntimeController.cs:355](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeController.cs))
- `SetExitWallsActive(active)` — 모든 RoomExitWall 일괄 토글
- 진입 시 `RoomInitContextSpec.LockExitDoors=true` 면 출구 잠김 ([cs:191-195](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeController.cs))
- 클리어 시 `autoOpenExitsOnCleared=true` 면 자동 해제 ([cs:321-323](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeController.cs))
- 외부에서 `OpenExits()` ([cs:349](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeController.cs)) 또는 `NotifyCustomRoomCleared()` ([cs:139](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeController.cs)) 호출 가능

### 멀티 권위 ([RoomEntryRuntimeController.cs:63](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeController.cs))
- `IsAuthority => HostAuthority.IsHost` — 솔로에선 항상 true
- 멀티 확장 시 카드 추첨은 호스트 권위, 결과 broadcast 필요. **본 CL 외**

---

## 작업 범위

### Phase A — 데이터 모델

- [ ] `EventCardRewardPool.cs` (신규 SO) — `Assets/_Project/Scripts/Runtime/Events/`
  - 후보 항목: 유물 (RelicData[]), 소모품 (RelicData[]), 골드 보상 (`EventGoldReward[]`)
  - 등급별 가중치 + 카테고리별 가중치 (유물 vs 소모품 vs 골드)
  - `DrawCount(int n, IEnumerable<string> ownedRelicNames)` → `List<EventCardRewardEntry>` (RewardPool.DrawCount 와 유사 인터페이스)
- [ ] `EventGoldReward.cs` (신규 struct/class) — 골드 보상 정의 (min/max 범위)
- [ ] `EventCardRewardEntry.cs` (신규 struct) — 카드 1장 = `{ kind: Relic|Consumable|Gold, relic: RelicData?, goldAmount: int }`
  - Discriminated union 패턴 (kind enum + nullable fields)

### Phase B — UI

- [ ] `EventCardDrawPanelView.cs` (신규) — `Assets/_Project/Scripts/Runtime/Events/`
  - `Init(EventCardRewardPool pool, PlayerRelicInventory inventory, GoldWallet wallet)`
  - `Show(int count)` — pool.DrawCount() 호출 후 N개 카드를 *뒷면* 으로 표시
  - 카드 클릭 시 → 뒤집힘 코루틴 → 보상 적용 (Inventory.TryAdd / GoldWallet.Add) → `OnCardSelected` 이벤트 발화
  - 다른 카드들은 클릭 후 자동으로 reveal (선택지를 봤었음을 보여주기 위해)
- [ ] `EventCardView.cs` (신규) — 카드 1장 컴포넌트
  - 뒷면 sprite / 앞면 root (icon, name, rarity, gold amount text)
  - `Init(EventCardRewardEntry entry, Action<EventCardRewardEntry> onClick)`
  - `Reveal(bool animate)` — 코루틴 1초 X-axis 0→90→0 회전 + 중간에 sprite 교체
  - `SetSelectable(bool)` — 클릭 가능 여부 토글
- [ ] `EventCardDrawPanel.prefab` (신규) — `Assets/_Project/Prefabs/UI/`
  - Canvas 자식. RewardPanel.prefab 구조 참고
  - 카드 슬롯 3개 (가로 배치), 닫기 버튼 (또는 자동 닫힘), 보상 토스트 텍스트

### Phase C — 컨트롤러

- [ ] `EventCardDrawController.cs` (신규)
  - `Open(EventCardRewardPool pool)` / `Close()` — 패널 활성/비활성 + 봉쇄
  - `HandleCardSelected(EventCardRewardEntry entry)` — Inventory/GoldWallet 동기화 + 토스트 표시 + 1.5초 후 Close + `roomController.NotifyCustomRoomCleared()`
  - `SuppressPlayerControls()` / `RestorePlayerControls()` — [ShopController.cs:252-276](../../LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopController.cs) 와 동일
  - `ResolveSceneLocalRefs()` — DDOL 부착 시 활성 씬에서 ref 재 wiring
  - 인스펙터 슬롯: panel, playerRelicInventory, goldWallet, playerAim/Movement/WeaponPresenter/MeleeCombo/Dash/Parry, **roomController** (현재 방의 RoomEntryRuntimeController)
- [ ] `EventCardDrawNpcInteractable.cs` (신규) — [ShopNpcInteractable.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopNpcInteractable.cs) 복제 후 변경
  - shopController → eventController, shopData → eventCardRewardPool
  - **1회 가드** — `_consumed` flag, F 키 누른 후 두 번째 진입 방지 (카드 1번만 뽑게)
  - playerTag / promptObject / interactKey 슬롯 그대로

### Phase D — 데이터 자산

- [ ] `EventCardRewardPool_Default.asset` 생성 — `Assets/_Project/ScriptableObjects/Events/`
  - 유물 풀: 기존 `RewardPool` 의 유물 일부 (Common/Rare 위주)
  - 소모품: SmallHealPotion, LargeHealPotion
  - 골드 보상 3종: small (50G), medium (100G), large (200G)
  - 카테고리 가중치 예: 유물 40 / 소모품 30 / 골드 30
- [ ] `RoomData_Event_CardDraw_01.asset` 생성 — `Assets/_Project/ScriptableObjects/Rooms/`
  - RoomType = Event
  - LayoutPrefab → 신규 `Room_Event_CardDraw.prefab`
  - InitContext.LockExitDoors = true (카드 뽑기 전 못 나감)
  - autoOpenExitsOnCleared = false (controller 가 명시적으로 OpenExits 호출 X — `NotifyCustomRoomCleared` 가 자체 분기로 호출하도록 변경 필요? **결정 7 재확인**)

### Phase E — Editor / 씬 셋업

- [ ] `Room_Event_CardDraw.prefab` (신규) — 방 레이아웃
  - Floor / Walls / RoomEntryAnchor / RoomExitWall (1~2개)
  - NPC GameObject (sprite + Collider2D trigger + EventCardDrawNpcInteractable)
  - PromptCanvas (F 누르세요 텍스트, 비활성 기본)
- [ ] 검증 씬: `MAP_event_carddraw_test.unity` (신규) — 단독 검증
  - DungeonRunBootstrap stub 또는 단일 방 직접 배치
  - Player + GoldWallet + PlayerRelicInventory + EventCardDrawController + EventCardDrawPanel canvas

### Phase F — 검증

- [ ] 단일 방 검증 (MAP_event_carddraw_test):
  - F 키 → 패널 열림 + 카드 3장 뒷면
  - 1장 클릭 → 뒤집힘 + 보상 적용 (인벤토리 / GoldWallet 변화 console + UI 확인)
  - 패널 닫힘 → 출구 벽 비활성 (통과 가능)
  - F 다시 누름 → `_consumed` 가드로 무반응 (또는 "이미 카드를 뽑았다" 토스트)
- [ ] Combat 방 ↔ Event 방 전환 검증 (DungeonArchitect 절차생성):
  - `RoomData_Event_CardDraw_01` 을 던전 시퀀스 한 칸에 배치
  - 보상 흐름 정상 (전투 방 클리어 → 보상 패널 → Event 방 진입 → 카드 뽑기)
- [ ] 봉쇄 검증:
  - 패널 떠있는 동안 마우스 조준 / 공격 / 대쉬 / 패링 모두 차단
  - 패널 닫은 후 즉시 복원
- [ ] 씬 전환 stale ref 검증:
  - EventCardDrawController 를 DDOL 에 부착 시 새 씬에서 ResolveSceneLocalRefs 가 자동 wiring

### 작업 외 (Out of scope)

- 유료 추가 뽑기 (결정 5)
- 다중 픽 (결정 5)
- 카드 호버 시 미리보기 (정보 누설이라 의도적으로 안 함)
- 멀티 권위 broadcast (CL-22X 멀티 확장 CL 에서)
- 보상 풀에 메타 조각 / 체력 회복 추가 (결정 3)

---

## 변경 파일

### 신규 (스크립트)
| 파일 | 책임 |
|---|---|
| `Runtime/Events/EventCardRewardPool.cs` | 카드 보상 풀 SO + DrawCount |
| `Runtime/Events/EventCardRewardEntry.cs` | 카드 1장 데이터 (discriminated union) |
| `Runtime/Events/EventGoldReward.cs` | 골드 보상 정의 |
| `Runtime/Events/EventCardDrawPanelView.cs` | 패널 뷰 + Init/Show + 카드 픽 흐름 |
| `Runtime/Events/EventCardView.cs` | 카드 1장 컴포넌트 (뒷면/앞면/뒤집힘) |
| `Runtime/Events/EventCardDrawController.cs` | NPC trigger → 패널 open/close + 봉쇄 + 방 클리어 |
| `Runtime/Events/EventCardDrawNpcInteractable.cs` | F 키 trigger zone (1회 가드) |

### 신규 (자산)
| 자산 | 위치 |
|---|---|
| `EventCardRewardPool_Default.asset` | `ScriptableObjects/Events/` |
| `RoomData_Event_CardDraw_01.asset` | `ScriptableObjects/Rooms/` |
| `EventCardDrawPanel.prefab` | `Prefabs/UI/` |
| `EventCardView_Slot.prefab` | `Prefabs/UI/` (3장 인스턴스용) |
| `Room_Event_CardDraw.prefab` | `Prefabs/Rooms/` (또는 기존 룸 prefab 폴더) |
| `MAP_event_carddraw_test.unity` | `Scenes/Tests/` |

### 수정
없음 — 본 CL 은 기존 코드 변경 없이 신규 모듈로만 추가.

> 단, 결정 7 에서 "NotifyCustomRoomCleared 가 출구 자동 해제" 를 채택할 경우 [RoomEntryRuntimeController.cs:139-152](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeController.cs) 가 `SetExitWallsActive(false)` 도 호출하도록 작은 수정 필요. **현재 코드는 RoomCleared 이벤트만 발화하고 출구는 `autoOpenExitsOnCleared=true` 일 때만 해제** ([cs:321](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeController.cs)) — 본 CL 의 RoomData 는 `autoOpenExitsOnCleared=true` 로 두면 자연 호환. **이 옵션 사용 추천**.

---

## 코드 spec (핵심 부분 요약)

### `EventCardRewardEntry` (구조)

```csharp
namespace LostMemory.Events
{
    public enum EventCardRewardKind
    {
        Relic = 0,
        Consumable = 1,
        Gold = 2
    }

    [System.Serializable]
    public struct EventCardRewardEntry
    {
        public EventCardRewardKind Kind;
        public RelicData Relic;       // Kind=Relic|Consumable 일 때
        public int GoldAmount;        // Kind=Gold 일 때
    }
}
```

### `EventCardRewardPool.DrawCount` (시그니처)

```csharp
public List<EventCardRewardEntry> DrawCount(int count, IEnumerable<string> ownedRelicNames)
{
    // 1. 카테고리 가중치로 N개 카테고리 추첨 (중복 허용)
    // 2. 각 카테고리에서 항목 추첨 (Relic 은 보유 제외, Consumable/Gold 는 중복 허용)
    // 3. EventCardRewardEntry 로 wrap
}
```

### `EventCardDrawPanelView.OnCardSelected` (흐름)

```csharp
private void OnCardSelected(EventCardRewardEntry entry)
{
    // 1. 다른 카드들 reveal 처리 (선택지 공개)
    // 2. 선택된 카드 reveal 코루틴
    // 3. 보상 적용:
    //    - Relic/Consumable → _inventory.TryAdd(entry.Relic)
    //    - Gold → _wallet.Add(entry.GoldAmount)
    // 4. 1.5초 후 OnCardSelected 이벤트 발화 (controller 가 Close + 방 클리어 호출)
}
```

### `EventCardDrawController.HandleCardSelected` (방 클리어 호출)

```csharp
private void HandleCardSelected(EventCardRewardEntry entry)
{
    Close();
    if (roomController != null)
    {
        roomController.NotifyCustomRoomCleared();
    }
}
```

---

## 검증 시나리오 (Phase F 상세)

### F-1. 단일 방 — 정상 흐름
1. `MAP_event_carddraw_test.unity` Play
2. Player 가 방에 진입 → 출구 벽 활성 (`LockExitDoors=true`)
3. NPC 가까이 가면 promptObject ("F") 활성
4. F 키 → 패널 열림 + 카드 3장 뒷면
5. 1장 클릭 → 뒤집힘 애니메이션 (1초) → 보상 reveal (예: "골드 +100")
6. **GoldWallet.Current** 가 +100 (또는 인벤토리 변화) — DungeonCurrencyHUDPresenter 즉시 갱신
7. 1.5초 후 패널 자동 닫힘 → 출구 벽 비활성 → 통과 가능

### F-2. 1회 가드
8. 카드 픽 후 NPC 다시 가까이 → F 키 → 무반응 (또는 "이미 뽑음" 로그)

### F-3. 봉쇄 검증
9. 패널 떠있는 동안 마우스 클릭 → 칼 휘두르지 않음 (KhiMeleeComboController.ExternalBlock)
10. WASD → 캐릭터 안 움직임 (CharacterMovement.MovementForbidden)
11. 마우스 이동 → 칼 회전 안 됨 (KhiWeaponPresenter.enabled=false)

### F-4. 보상 종류별 검증
12. 보상 풀의 카테고리 가중치를 일시적으로 100/0/0 (유물만) → 카드 3장 모두 유물
13. 0/100/0 (소모품만) → 단축키바에 즉시 추가 확인
14. 0/0/100 (골드만) → GoldWallet 즉시 갱신

### F-5. 절차생성 던전 통합
15. `RoomData_Event_CardDraw_01` 을 `RouteSequenceConfig` 한 칸에 배치
16. `Dungeon.unity` Play → 던전 진행 시 Event 방 등장 → 위 흐름 동일하게 작동
17. **이전 Combat 방의 RewardController** 가 Event 방 RoomCleared 무시 ([RewardController.cs:133](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/RewardController.cs)) — 보상 패널 안 떠야 정상

### F-6. 씬 전환 stale ref
18. EventCardDrawController 가 DDOL 부착된 셋업 → 다음 던전 시 `ResolveSceneLocalRefs` 가 새 씬의 panel/inventory/wallet 등을 자동 wiring

---

## 알려진 이슈 / 고려

- **카드 클릭 후 Reveal 코루틴 도중 패널 닫기 시 잔여 코루틴**: `gameObject.SetActive(false)` 시 코루틴 자동 중단되지만, 보상 적용은 클릭 직후 즉시 수행하도록 순서 보장 (코루틴 안에서 적용 X)
- **소모품 인벤토리 풀**: `PlayerConsumableInventory` 가 가득 찬 상태에서 소모품 카드 픽 시 → `PlayerRelicInventory.TryAdd` 가 라우팅 후 false 반환 가능. 이때 보상 reveal 은 표시하되 **"인벤 가득"** 메시지로 폴백 (또는 골드로 자동 환전)
  - Phase A: 단순화 — 그냥 시도 후 false 면 console warning, 보상은 손실. 후속 폴리시
- **NPC 인터랙션 후 promptObject 처리**: `_consumed=true` 후엔 promptObject 도 재활성 X. `OnTriggerExit2D` 시 무조건 비활성, `OnTriggerEnter2D` 시 `if (!_consumed)` 가드
- **Event 방의 LockExitDoors 가드 누락 시**: 플레이어가 NPC 무시하고 그냥 통과 가능. **RoomData.InitContext 에서 LockExitDoors=true 필수**. 검증 시 항상 첫 번째로 확인할 항목
- **`autoOpenExitsOnCleared=true` 인 채로 두면**: `NotifyCustomRoomCleared()` → `FireRoomCleared` → `HandleRoomCleared` 분기 안 탐 (직접 발화) → 출구 안 열림. **올바른 흐름은 `OpenExits()` 직접 호출 또는 `autoOpenExitsOnCleared=true` + 다른 트리거**. 본 CL 은 controller 가 `roomController.OpenExits()` 직접 호출하는 방식이 안전
  - **재결정**: HandleCardSelected 가 `NotifyCustomRoomCleared()` + `OpenExits()` 둘 다 호출 (idempotent)
- **멀티 확장 시**: 카드 추첨 결과를 호스트가 결정 후 RPC 로 broadcast. Phase A 솔로 — 본 CL 외

---

## 후속 CL 후보

| CL | 내용 |
|---|---|
| CL-228 | 이벤트 방 — 아이템 구매 (본 CL 의 NPC 패턴 + 봉쇄 패턴 재사용) |
| CL-229 | 이벤트 방 — 슬롯머신 (본 CL 의 NPC 패턴 + 봉쇄 패턴 + 골드 입력 추가) |
| 후속 폴리시 | 유료 추가 뽑기, 호버 시 보상 종류 힌트, 뒤집힘 애니메이션 (a)/(c), 보상 풀에 메타 조각 / 체력 회복 추가 |
| 멀티 확장 | 호스트 권위 추첨 + 결과 broadcast, 친구방에서 *상대방이 뽑은 카드 함께 보기* |
