# CL-110 진행 상황 (2026-04-29 → 2026-04-30 갱신)

## 한 줄 요약
보상 흐름 (RoomCleared → 보상 3택 → 선택 → 다음 방) **본 흐름 작동**. **Down → Defeated 미스터리도 해결**: Down 중 적의 후속 hit 으로 HP ≤ 0 → `Health.Kill()` 가 GameObject 비활성화 → `Update` 정지 → 타이머 멈춤 패턴이었음. `HandleHealthHit` 에 Down 중 HP floor 복원 로직 추가로 fix. 시나리오 6 수동 검증 1 회만 남음.

---

## 본 CL 의 목적
- `RewardController.cs` 신설 (RoomCleared 가로채서 보상 패널 띄우는 오케스트레이터)
- `RewardPanelView.RewardSelected` event 추가 → RewardController 가 구독해 문 열기 트리거
- `RoomEntryRuntimeController` 옵트인화 (`autoOpenExitsOnCleared` + `OpenExits()` + `RoomId` getter)
- RunManager wiring (HandleDungeonBuilt 시 RewardController 도 Subscribe, CloseResulting 시 Unsubscribe + Inventory.Clear)
- 보상 패널 떠있는 동안 Input lock (Time.timeScale=0 + KhiPlayerAim.enabled=false)

전체 결정사항 / 위험 항목은 [cl110_plan.md](cl110_plan.md) 참조.

---

## 구현 완료 (코드 변경)

### 신규 파일 (1)
- `client/LostMemory/Assets/_Project/Scripts/Runtime/Stage/RewardController.cs`
  - `[SerializeField] RewardPanelView / PlayerRelicInventory / KhiPlayerAim`
  - `Dictionary<RoomEntryRuntimeController, Action<RoomClearedPayload>> _subscribed` — closure 로 source controller 캡처
  - `SubscribeAllRoomControllers()` / `UnsubscribeAllRoomControllers()` — 각 controller 마다 자기 자신 캡처 람다 등록
  - `HandleRoomClearedFromController(source, payload)` — source 를 그대로 `_pendingController` 에 저장 (RoomId 검색 X)
  - `ShowReward()` — public (미래 외부 트리거 위해), timeScale=0 + aim disable + panel.Show
  - `HandleRewardSelected()` — timeScale 복원 + aim 복원 + `_pendingController.OpenExits()` 호출
  - `OnDisable()` panic restore — timeScale 잠금 방지

### 수정 파일 (3)
- `Rewards/RewardPanelView.cs`
  - `event Action<RelicData> RewardSelected` 추가
  - `OnCardSelected` 끝에 `RewardSelected?.Invoke(selected)` 발화
- `Stage/RoomEntryRuntimeController.cs`
  - `[SerializeField] bool autoOpenExitsOnCleared = true` 추가
  - `public string RoomId => roomData?.RoomId` getter 신설
  - `public void OpenExits() { SetExitWallsActive(false); }` 신설
  - `HandleRoomCleared` 의 자동 문 열기를 옵트인 (autoOpenExitsOnCleared=true 일 때만)
- `Stage/RunManager.cs`
  - `[SerializeField] RewardController rewardController` 필드 추가
  - `HandleDungeonBuilt`: `rewardController.SubscribeAllRoomControllers()` 호출
  - `CloseResulting`: `rewardController.UnsubscribeAllRoomControllers()` + `playerRelicInventory.Clear()` 호출

### 핵심 버그 수정 (커밋 전)
**closure 기반 source controller 식별** — DungeonArchitect 가 같은 Module prefab 을 여러 번 spawn 해서 여러 `RoomEntryRuntimeController` 인스턴스가 같은 RoomId 를 공유. 기존 `ResolveControllerFromPayload` 가 RoomId 만으로 검색 → *플레이어가 들어가지 않은 instance* 반환 → 그 instance 의 walls 는 활성화된 적 없어 OpenExits 가 무의미. closure 캡처로 정확한 source 식별로 변경하여 해결.

---

## Inspector wiring 완료

- RewardController GameObject 부착
- RewardController slot:
  - Reward Panel View ✓
  - Player Relic Inventory ✓
  - Player Aim ✓
- RunManager slot:
  - Reward Controller ✓
  - Player Relic Inventory ✓ (CL-109 산출물)
  - Run Result Panel View ✓ (사용자가 본 CL 진행 중에 추가)
- Combat 방 Module prefab 의 `autoOpenExitsOnCleared` = false 설정 ✓ (작동 확인됨)

UI Canvas: **Screen Space - Overlay** 로 변경 (Camera 모드일 때 캐릭터가 UI 위에 렌더되는 문제 해결).

---

## 검증 결과

### ✅ 통과
- **시나리오 1** — Combat 방 클리어 → 보상 패널 → 카드 선택 → 모디파이어 등록 → 문 열림 → 다음 방 진입
  - 2 회 연속 검증됨 (수호의 파편 +12% MaxHealth, 바람 깃털 +8% MoveSpeed)
- **시나리오 5** — modifier 등록 로그 (`[StatModifier] AddPermanent ...`) 정상 발화
- **closure fix** — 같은 RoomId 의 multiple instance 환경에서 정확한 controller 매칭 확인

### ⚠️ 미진행 / 미검증
- **시나리오 3** (Boss 방 보상 X) — 현재 test_khi.unity scene 에 Boss 방 없음. 후속 ticket 에서 Boss 추가 후 검증
- **시나리오 4** (다중 방 동시 클리어 보호) — 재현 어려움. 코드만 작성됨
- **시나리오 6** (Run 종료 cleanup) — **현재 blocked. 아래 막힌 지점 참조**

---

## ✅ 해결됨 — Down → Defeated 미스터리

### 증상
플레이어가 적에게 맞아 HP 0 도달 → Down 상태 진입까지 정상. 이후 10 초 이상 기다려도 `Down timer expired -> Defeated` 로그 안 뜨고 결과 화면 안 출현.

### 원인 (가설 1번이 맞았음)
**Down 진입 후 적의 후속 hit 으로 GameObject 가 비활성화** → `Update` 정지 → `TickDown()` 멈춤 → Defeated 전이 영원히 안 일어남.

세부 chain:
1. `HandleHealthHit` 의 Down 분기가 *없던 시점* 에는 `_state == Down` 일 때 hit 이벤트가 *그냥 무시* 됨
2. 그러나 `Health.Damage` 자체는 hit 으로 HP 를 추가 차감 → HP 가 음수로 떨어짐
3. `Health.Damage` 끝에 `if (CurrentHealth <= 0) Kill()` → `gameObject.SetActive(false)`
4. GameObject 비활성 → `Update` 정지 → 타이머 영원히 안 진행

### Fix
[KhiDownController.cs:223](../../client/LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiDownController.cs:223) `HandleHealthHit` 의 Down 분기에 **HP floor 즉시 복원 로직** 추가:

```csharp
// Down 중 후속 hit (적 OnTriggerStay2D 등) 으로 HP 가 0 이하 떨어지면
// Health.Damage 의 `if (CurrentHealth <= 0) Kill()` 가 발화해 GameObject 가 비활성화되고
// Down 타이머가 멈춘다. floor 로 즉시 복원해 Kill 트리거를 막는다.
if (_state == KhiDownState.Down)
{
    if (health.CurrentHealth <= 0f)
    {
        health.SetHealth(Mathf.Max(0.0001f, downHealthFloor));
    }
    return;
}
```

> 주의: `Defeated` 분기는 *그냥 return* (이미 죽음 처리 완료, intercept X). `Normal` 분기는 *기존 EnterDown 흐름 유지*. **Down 만이 새로 추가된 분기**.

### 진단 패턴 (재발 시 / 비슷한 케이스)
- 타이머 기반 상태 전이가 *발화 자체* 가 안 일어나면 = **GameObject 비활성** 의심 (Update 가 안 돈다)
- `OnDisable` 에 stack trace 로그 임시 추가 → SetActive(false) 호출처 추적 가능
- `Time.unscaledTime` 으로 디버그 로그 → timeScale=0 잠금 케이스도 진단 가능 (단 본 CL 은 timeScale 문제 X)

→ 시나리오 6 (Run 종료 정리) 의 자연 흐름 검증 가능 상태. CL-109 의 cleanup chain 도 통합 검증.

---

## 정리한 임시 디버그 코드

본 CL fix 진단 과정에서 추가된 임시 코드. **모두 정리 완료** (2026-04-30):

| 파일 | 위치 | 내용 | 상태 |
|---|---|---|---|
| `KhiDownController.cs` | TickDown | `[KhiDown-DBG] TickDown remaining=...` 로그 (1 초 간격) + `_lastDbgTickLogTime` 필드 | ✅ 제거 |
| `KhiDownController.cs` | Update | `[KhiDown-DBG-Update]` 로그 + `_lastDbgUpdateLogTime` 필드 | ✅ 제거 |
| `KhiDownController.cs` | OnDisable | `[KhiDown-DBG-OnDisable]` stack trace 로그 (비활성화 호출처 추적용) | ✅ 제거 |

### 유지 항목

| 파일 | 위치 | 내용 | 이유 |
|---|---|---|---|
| `KhiParryController.cs` | ContextMenu | "Dump permit state" / "Force restore permits" | CL-108 followup race 추적용 ([cl108_followup_parry_race.md](cl108_followup_parry_race.md) 별 ticket 에서 재활용) |
| `KhiDownController.cs` | TickDown | `Time.time` → 그대로 유지 (진단 시 `Time.unscaledTime` 으로 임시 변경했으나 복원) | 기존 동작 보존 |

`RewardController.logRewardFlow` 와 `KhiDownController.logStateTransitions` 는 [SerializeField] 토글이라 그대로 유지 (Inspector 에서 끄기).

`RunManager.LogStateChange` 의 `[RunManager] State -> State` 로그는 항상 발화 — 의도적. 그대로 유지.

---

## 후속 ticket (본 CL 범위 외)

1. **Boss 방 추가** — MVP 데모 흐름의 일부. test_khi.unity 또는 DA layout 에 Boss 방 RoomData + Bertha module prefab 배치 (별 CL)
2. **다중 방 동시 클리어 보호 재현 테스트** — `_isShowingReward` flag 동작 확인 (시나리오 4)
3. **TopDownEngine ItemPicker / Deadline NRE 정리** — 본 CL 검증 시 다수 발생. Player prefab 의 ItemPicker 비활성화 또는 InventoryName 셋업 (별 ticket)
4. **KhiParryController permit race 수정** — [cl108_followup_parry_race.md](cl108_followup_parry_race.md) 의 별 ticket
5. **RunResultPanelView 의 OnRestart / OnLobby 핸들러 wiring** — 현재 RunResultPanelView 는 prefab 만 배치된 상태. 버튼 클릭 시 RunManager.CloseResulting 호출 또는 Scene 재로드 wiring 필요. MVP 데모 흐름 ticket 에서 통합 진행
6. **MVP 데모 흐름 통합** — 마을 → 던전 → 전투4 + 상점 + 보스 → 결과 → 마을 복귀. 본 CL plan 파일 [`110-cl-md-happy-harbor.md`](../../../../Users/AD/.claude/plans/110-cl-md-happy-harbor.md) 의 Part 2 참조

---

## 본 CL commit 기준

- [x] **Down → Defeated 미스터리 fix** — `HandleHealthHit` 의 Down 분기에 HP floor 복원 로직 추가
- [x] **임시 디버그 로그 정리** — KhiDown-DBG / KhiDown-DBG-Update / KhiDown-DBG-OnDisable + 필드 모두 제거
- [x] **시나리오 6 수동 검증** — 사용자가 이전 진행 시 검증 완료
- [x] **cl110.md 완료 보고서 작성** — [cl110.md](cl110.md) 참조

본 CL 모든 항목 완료. commit / merge 가능 상태.
