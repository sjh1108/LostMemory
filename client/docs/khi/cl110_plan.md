# CL-110 — 보상 UI ↔ 방 클리어 연동 (RewardController 신설 + wiring)

## Context

Reward 인프라 (RewardPool, RewardCardView, RewardPanelView, PlayerRelicInventory) 는 모두 완성되어 있지만 **호출처가 0** 인 상태. RewardPanelView.Show() 를 부르는 코드가 어디에도 없음. CL-110 의 목적은 **방 클리어 → 보상 3택 UI → 선택 → 다음 방 진행** 흐름의 *오케스트레이터* `RewardController` 를 신설해서 인프라들을 연결하는 것.

본 CL 은 Epic P 의 다른 CL (106~109) 와 **완전 독립**. 효과 적용 시스템과 무관. RelicData 를 PlayerRelicInventory 에 추가하는 시점만 책임.

**현재 흐름** (RewardController 미존재):
```
RoomCleared → RoomEntryRuntimeController.SetExitWallsActive(false) → 문 자동 열림 → 플레이어 자유 진출
                                                                   (보상 UI 개입 0)
```

**CL-110 후 흐름** (옵션 A 채택):
```
RoomCleared → RewardController 가 가로챔 → RewardPanelView.Show()
                                          ↓
                              플레이어가 카드 1 장 선택
                                          ↓
                       PlayerRelicInventory.TryUseOrAdd(selected)
                                          ↓
                        RewardController 가 문 열기 (SetExitWallsActive(false))
                                          ↓
                             RoomEntryZone trigger → 다음 방
```

---

## 결정사항

1. **시간 순서 정책 = 옵션 A** — 보상 선택을 *문 열림의 강제 게이트* 로 둠. 이유:
   - 보상 선택을 게임 진행의 의미 있는 결정 지점으로 만듦 (옵션 B/C 는 보상 무시 가능)
   - 보스방을 제외하면 보상 카드 3 장 중 1 장은 *항상* 받게 됨 (단, 비-MVP 등 효과 미적용 유물도 카드에 포함될 수 있음)
2. **보상 대상 = Combat 타입만** — Boss 는 RunCleared 로 직행 (보상 X), Shop/Event 는 별도 정책 (본 CL 범위 외, 위험 항목에서 다룸)
3. **RoomEntryRuntimeController 의 자동 문 열림 무력화** — 현재 클리어 시 자동 `SetExitWallsActive(false)` 호출되는 위치 (L275 추정) 를 *옵트인* 으로 변경:
   ```csharp
   [SerializeField, Tooltip("true 면 클리어 시 자동으로 출구 벽 비활성화. RewardController 가 관리하는 방은 false 로 둘 것.")]
   private bool autoOpenExitsOnCleared = true;
   ```
   - 기본값 true (기존 동작 유지). RewardController 가 wiring 하는 Combat 방은 Inspector 에서 false 로
   - **대안**: RewardController 가 RoomCleared 이벤트 직후 SetExitWallsActive(true) *다시* 호출 (강제 잠금) 후 보상 선택 시 false. 코드는 더 단순하지만 *문이 열렸다가 즉시 잠기는* 깜빡임 발생 가능 → 채택 X
4. **RewardController 위치** = `Stage/RewardController.cs`. RunManager 와 같은 lifetime (DontDestroyOnLoad 가능, 또는 Run 단위). 일단 *Run 단위* 권장 — Singleton X, RunManager 가 Awake 에서 참조 보유 또는 별도 GameObject. lifetime 결정은 위험 항목 #1
5. **RoomCleared 구독자 = 두 명** — RunManager (Boss 분기 → RunCleared) + RewardController (Combat 분기 → 보상 UI). 두 구독자 모두 같은 이벤트 받음. 분기는 각자 RoomType 체크
6. **다중 방 동시 클리어 보호** — 두 방이 거의 동시 클리어되면 보상 패널이 두 번 뜰 수 있음. RewardController 에 `_isShowingReward` flag 추가 → 보상 표시 중이면 다른 RoomCleared 무시 (또는 큐에 적재)
7. **시작방 처리** — StageRoomType 에 *Start* 값 없음. 가능성:
   - (가) 시작방도 RoomType=Combat 인데 wave 가 빈 상태 → AllEnemiesDefeatedTracker 가 즉시 클리어 발화 → RewardController 가 보상 띄우려 함 (오작동)
   - (나) 시작방은 RoomType=Unknown 또는 별도 RoomData 설정 → RewardController 의 RoomType==Combat 필터에서 자동 제외
   - 본 plan: **(나) 가정**. 시작방의 RoomData.RoomType 이 Combat 이 아니거나, RoomEntryRuntimeController 의 `autoOpenExitsOnCleared = true` (기본값) 로 둬서 보상 우회. 만약 (가) 가 발견되면 RewardController 에 *wave count == 0 즉시 클리어된 방 무시* 로직 추가. cl045 plan 확인 필요
8. **보상방 처리** — StageRoomType 에 Reward 값 없음. 본 CL 범위 외. 추가가 필요하면 별도 ticket. 현재는 *Reward enum 없음 = 보상방 없음* 으로 진행
9. **카드 선택 강제** — 플레이어가 보상 패널을 닫으면 (esc?) 게임 진행 불가. RewardPanelView 에 *닫기 버튼 없음* 가정 (현재 코드 L43-47 의 OnCardSelected 만 닫기 트리거). 추가 닫기 경로 차단
10. **RewardController → RoomEntryRuntimeController 호출 인터페이스** — 문 열기를 외부에서 트리거하기 위해 `RoomEntryRuntimeController.OpenExits()` public 메서드 신설. 내부적으로 `SetExitWallsActive(false)` 호출 (private 유지)
11. **Inventory.TryUseOrAdd 호출 위치** — 현재 RewardPanelView.OnCardSelected (L43-47) 에서 inventory.TryAdd. CL-108 에서 TryUseOrAdd 로 바뀜. CL-110 은 *그대로 유지* (RewardController 는 OnCardSelected 호출 후의 *후속 처리* 만 책임). 즉 RewardPanelView 가 자체적으로 inventory 추가 → 콜백으로 RewardController 에게 "선택 완료" 알림. 신규 콜백 또는 RewardPanelView.OnCardSelected event 추가 필요
12. **이벤트 추가 = `RewardPanelView.RewardSelected`** — `event Action<RelicData> RewardSelected` 신설, OnCardSelected 끝에 발화. RewardController 가 구독해서 문 열기

---

## 핵심 파일

### 신규 (1 컴포넌트)

| 파일 | 역할 |
|---|---|
| `Stage/RewardController.cs` | RoomCleared (Combat 만) 구독 → RewardPanelView.Show() → RewardSelected 후 문 열기 |

### 수정

| 파일 | 변경 |
|---|---|
| `Rewards/RewardPanelView.cs` | `event Action<RelicData> RewardSelected` 추가, OnCardSelected (L46) 끝에 발화 |
| `Stage/RoomEntryRuntimeController.cs` | `[SerializeField] bool autoOpenExitsOnCleared = true` 추가. 클리어 시 자동 문 열기를 옵트인. `public void OpenExits()` 신설 (RewardController 가 호출) |
| `Stage/RunManager.cs` | (선택) RewardController 참조 추가 + lifetime 관리 위임. 또는 별도 GameObject 로 둠 |

### 참조 (수정 없음)

- `Stage/RoomEntryRuntimeController.cs` — `event Action<RoomClearedPayload> RoomCleared` (L49)
- `Stage/RoomClearedPayload` — roomId + RoomData
- `Stage/StageRoomType` — `{Unknown, Combat, Shop, Event, Boss}`. RewardController 는 Combat 만 처리
- `Rewards/RewardPanelView.Show(PlayerRelicInventory)` — 현재 시그니처 유지
- `Relics/PlayerRelicInventory.TryUseOrAdd` (CL-108 산출물) — RewardPanelView 가 호출 (CL-108 작업분)

---

## 작업 단계 (구현 순서)

### Step 1 — RewardPanelView.RewardSelected 이벤트 추가 (5분)
```csharp
public event Action<RelicData> RewardSelected;

private void OnCardSelected(RelicData selected)
{
    _inventory.TryUseOrAdd(selected);  // CL-108 산출물
    gameObject.SetActive(false);
    RewardSelected?.Invoke(selected);
}
```

### Step 2 — RoomEntryRuntimeController 옵트인 + OpenExits API (15분)
```csharp
[Header("Reward Integration (CL-110)")]
[SerializeField, Tooltip("true 면 클리어 시 자동으로 출구 벽 비활성화. RewardController 가 관리하는 방은 false 로.")]
private bool autoOpenExitsOnCleared = true;

// 기존 클리어 핸들러 (L275 추정):
private void HandleRoomCleared(/* args */)
{
    // ... 기존 로직 ...
    if (autoOpenExitsOnCleared)
    {
        SetExitWallsActive(false);
    }
    // false 일 때는 외부 (RewardController) 가 OpenExits() 호출
}

public void OpenExits()
{
    SetExitWallsActive(false);
}
```

### Step 3 — RewardController 신설 (40분)

`Stage/RewardController.cs`:
```csharp
using System.Collections.Generic;
using LostMemory.Relics;
using LostMemory.Rewards;
using UnityEngine;

namespace LostMemory.Stage
{
    /// <summary>
    /// CL-110 — 방 클리어 → 보상 3택 → 선택 → 문 열림 흐름의 오케스트레이터.
    /// Combat 타입 방만 처리. Boss 는 RunManager 가 RunCleared 로 직행.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Stage/Reward Controller")]
    public sealed class RewardController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private RewardPanelView rewardPanelView;
        [SerializeField] private PlayerRelicInventory playerRelicInventory;

        [Header("Debug")]
        [SerializeField] private bool logRewardFlow = false;

        private readonly HashSet<RoomEntryRuntimeController> _subscribed = new HashSet<RoomEntryRuntimeController>();
        private RoomEntryRuntimeController _pendingController;
        private bool _isShowingReward;

        private void OnEnable()
        {
            if (rewardPanelView != null)
            {
                rewardPanelView.RewardSelected += HandleRewardSelected;
            }
        }

        private void OnDisable()
        {
            UnsubscribeAllRoomControllers();
            if (rewardPanelView != null)
            {
                rewardPanelView.RewardSelected -= HandleRewardSelected;
            }
        }

        /// <summary>
        /// RunManager.SubscribeAllRoomControllers() 가 동작하는 시점에 같이 호출.
        /// RunManager.HandleDungeonBuilt 끝 또는 별도 wiring.
        /// </summary>
        public void SubscribeAllRoomControllers()
        {
            UnsubscribeAllRoomControllers();
            RoomEntryRuntimeController[] controllers = FindObjectsOfType<RoomEntryRuntimeController>();
            for (int i = 0; i < controllers.Length; i++)
            {
                RoomEntryRuntimeController c = controllers[i];
                if (c == null) continue;
                c.RoomCleared += HandleRoomCleared;
                _subscribed.Add(c);
            }
            if (logRewardFlow)
                Debug.Log($"[RewardController] Subscribed to {_subscribed.Count} room controllers.");
        }

        public void UnsubscribeAllRoomControllers()
        {
            foreach (RoomEntryRuntimeController c in _subscribed)
            {
                if (c != null) c.RoomCleared -= HandleRoomCleared;
            }
            _subscribed.Clear();
        }

        private void HandleRoomCleared(RoomClearedPayload payload)
        {
            if (payload.Data == null)
            {
                Debug.LogWarning("[RewardController] RoomCleared payload has null RoomData; ignoring.", this);
                return;
            }
            // Boss/Shop/Event 등은 보상 X
            if (payload.Data.RoomType != StageRoomType.Combat) return;

            // 동시 다중 클리어 보호
            if (_isShowingReward)
            {
                if (logRewardFlow) Debug.LogWarning($"[RewardController] Already showing reward; ignoring room {payload.RoomId}.");
                return;
            }

            // payload 에서 controller 참조 직접 못 얻으면, sender 컬렉션 검색
            _pendingController = ResolveControllerFromPayload(payload);
            if (_pendingController == null)
            {
                Debug.LogError("[RewardController] Could not resolve RoomEntryRuntimeController from payload.", this);
                return;
            }

            ShowReward();
        }

        private RoomEntryRuntimeController ResolveControllerFromPayload(RoomClearedPayload payload)
        {
            // RoomClearedPayload 가 controller ref 또는 roomId 만 들고있으면 _subscribed 에서 찾음
            foreach (RoomEntryRuntimeController c in _subscribed)
            {
                if (c != null && c.RoomId == payload.RoomId) return c;  // RoomId getter 가정
            }
            return null;
        }

        private void ShowReward()
        {
            if (rewardPanelView == null || playerRelicInventory == null)
            {
                Debug.LogError("[RewardController] rewardPanelView or playerRelicInventory is null; cannot show reward.", this);
                return;
            }
            _isShowingReward = true;
            rewardPanelView.Show(playerRelicInventory);
            if (logRewardFlow) Debug.Log("[RewardController] Reward panel shown.");
        }

        private void HandleRewardSelected(RelicData selected)
        {
            if (logRewardFlow) Debug.Log($"[RewardController] Reward selected: {selected?.DisplayName}. Opening exits.");
            _isShowingReward = false;
            if (_pendingController != null)
            {
                _pendingController.OpenExits();
                _pendingController = null;
            }
        }
    }
}
```

### Step 4 — RunManager 와의 wiring (10분)

RunManager 의 `HandleDungeonBuilt` 직후 RewardController.SubscribeAllRoomControllers() 호출:
```csharp
private void HandleDungeonBuilt()
{
    if (StateMachine.Current != RunState.Initializing) return;
    SubscribeAllRoomControllers();
    if (rewardController != null) rewardController.SubscribeAllRoomControllers();  // CL-110 추가
    StateMachine.TryTransition(RunState.InRun);
}
```

`[SerializeField] private RewardController rewardController;` 필드 추가.

### Step 5 — RoomClearedPayload 에 controller ref 또는 RoomId getter 확인 (10분)

탐색 보고서에 RoomClearedPayload = (roomId, RoomData) 라 명시. `RoomEntryRuntimeController.RoomId` public getter 있는지 확인. 없으면 추가:
```csharp
public string RoomId => _roomId;  // 또는 적절한 ID 필드
```

또는 RoomClearedPayload 에 controller ref 필드 추가 (더 단순):
```csharp
public readonly struct RoomClearedPayload
{
    public string RoomId { get; }
    public RoomData Data { get; }
    public RoomEntryRuntimeController Controller { get; }  // CL-110 추가
    // ...
}
```

### Step 6 — Inspector wiring (10분)

- 새 GameObject "RewardController" 또는 RunManager 의 자식에 RewardController 컴포넌트 부착
- rewardPanelView, playerRelicInventory 참조 드래그
- RunManager 의 rewardController 필드에 드래그
- 모든 Combat 방의 RoomEntryRuntimeController 의 `autoOpenExitsOnCleared` = false (Inspector)
- Boss 방은 true (또는 N/A — Boss 는 RunCleared 로 직행이라 어차피 이 흐름 안 탐)
- Shop/Event 방은 true (보상 X 라 자동 문 열림)
- 시작방은 true (자동)

---

## 검증 (PlayMode, 6 시나리오)

### 사전 준비
- RewardController logRewardFlow = true
- PlayerStatModifierContainer logModifierChanges = true (보상 적용 후 효과 발현 확인용)
- 디버그용 RoomData asset 1 종 (Combat) + Boss RoomData 1 종

### 시나리오 1 — 일반 Combat 방 보상 흐름 (메인)
1. 시작 → Combat 방 진입 → 적 전멸
2. **기대**:
   - `[RewardController] Reward panel shown.`
   - 보상 패널 표시, 카드 3 장 보임
   - 출구 벽 활성화 유지 (문 잠김)
3. 카드 1 장 선택
4. **기대**:
   - `[PlayerRelicInventory] 유물 획득: ...`
   - `[RewardController] Reward selected: ... Opening exits.`
   - 출구 벽 비활성화 (문 열림)
5. 출구로 이동 → 다음 방 진입

### 시나리오 2 — Boss 방 (보상 X)
1. Boss 방 진입 → 적 전멸 (보스 처치)
2. **기대**:
   - 보상 패널 표시 X
   - RunManager 가 RunCleared 전이 → 결과 화면

### 시나리오 3 — 시작방 (보상 X)
1. 게임 시작 → 시작방 (적 없음)
2. **기대**:
   - RoomCleared 가 즉시 발화되더라도 RoomType != Combat 이라 보상 패널 표시 X
   - 출구 자동 열림 (autoOpenExitsOnCleared = true 가정)

### 시나리오 4 — 회복약 보상 (소모품)
1. Combat 방 클리어 → 보상 패널에 회복약이 카드로 등장
2. 카드 선택
3. **기대**:
   - `_inventory.TryUseOrAdd(selected)` → IsConsumable 분기 → `[PlayerHealing] Heal ...` 로그
   - 인벤토리 미등록 (소모품)
   - 문 열림

### 시나리오 5 — 보상 후 효과 즉시 발현 (CL-107~109 통합)
1. Combat 방 클리어 → "전사의 끈" 카드 선택
2. **기대**:
   - 인벤토리 등록 + RelicEffectRegistry.HandleAcquired → AttackPower +5%
   - 다음 방 진입 후 검 1 타 데미지 = base × stepMul × 1.05

### 시나리오 6 — Run 종료 (정리) — **CL-109 cleanup 자연 검증 통합**
1. 보상 1 회 받은 후 보스 클리어 → RunCleared → 결과 화면 → CloseResulting
2. **기대**:
   - RunManager.CloseResulting 끝의 `playerRelicInventory.Clear()` 자동 호출 (**CL-109 구현 완료** — [cl109.md](cl109.md) 참조)
   - `PlayerRelicInventory.OnCleared` → `RelicEffectRegistry.HandleRunCleared` chain → `container.ClearAll` + `playerShield.ClearShield` + 3 List `Clear`
   - **기대 로그**:
     ```
     [StatModifier] Cleared all modifiers.
     [PlayerShield] Expired/Cleared
     ```
   - 다음 런 시작 후 *Add all assigned relics 누르기 전* 시점에 Log all totals → 7 stat 모두 baseline (1.000 / count=0) 복귀 (= 전 런의 효과 누수 없음)
   - RewardController.UnsubscribeAllRoomControllers (OnDisable 또는 RunManager 위임)
   - 다음 런 시작 시 보상 다시 정상 동작
3. **⚠️ 본 시나리오 = CL-109 시나리오 3 의 자연 검증 시점**. CL-109 commit 시 *현장 검증 미진행* (DungeonArchitect Build NRE 로 보스 방 진입 막힘 + 디버그 ContextMenu 추가/제거 비용 회피) → 본 CL-110 의 보상 흐름 검증 시 *전체 한 사이클* 자연 트리거로 동시 검증. 본 시나리오 통과 시 **CL-109 의 HandleRunCleared 4 줄 cleanup 도 동시에 검증 완료** 처리. 누수 발견 시 CL-109 fix ticket 별도 생성

---

## 완료 기준

- [ ] Unity 컴파일 통과
- [ ] 신규 RewardController.cs
- [ ] 수정 3 파일 (RewardPanelView, RoomEntryRuntimeController, RunManager)
- [ ] Inspector wiring (RewardController 부착 + 모든 Combat 방의 autoOpenExitsOnCleared=false)
- [ ] 시나리오 1 ~ 6 모두 통과
- [ ] CL-107 ~ 109 의 회귀 시나리오 통과 (보상 흐름이 효과 적용 시스템에 영향 없음 확인)

---

## 위험 / 결정 보류

1. **RewardController lifetime** — Run 단위 (Scene 객체) vs DontDestroyOnLoad (Singleton). 본 plan: Run 단위 권장. RunManager 가 참조 보유. Singleton 으로 가면 RunManager 처럼 Instance 패턴 추가 가능
2. **RoomClearedPayload 에 controller ref 추가 vs RoomId 검색** — Step 5 의 두 옵션. controller ref 추가가 더 단순하지만 payload 변경. RoomId 검색은 payload 무변경이지만 _subscribed 에 의존. 선택 필요
3. **시작방 처리** — RoomType=Combat 이면서 wave 가 빈 경우 RewardController 가 잘못 트리거. cl045 plan 확인 후 시작방의 실제 RoomType 확정. 만약 Combat 이면 RewardController 에 *wave count == 0 즉시 클리어 무시* 로직 추가
4. **Shop / Event 방 보상 정책** — 본 CL 범위 외. RewardController 는 Combat 만 처리 → Shop/Event 클리어 시 보상 X. 향후 정책 확정 시 별도 ticket
5. **다중 방 동시 클리어** — `_isShowingReward` flag 로 단순 차단. 두 번째 방 클리어가 무시되면 영구 손실. 큐로 적재할지 결정 필요. 현재는 *동시 클리어가 사실상 발생 X* (Player 가 한 방에 있을 때만 클리어 가능) 가정
6. **카드 닫기 경로 차단** — 현재 RewardPanelView 는 OnCardSelected 만 닫기 트리거. 만약 다른 닫기 경로 (esc, 외부 click) 가 있다면 추가 차단. 본 plan 은 *닫기 경로 없음* 가정
7. **autoOpenExitsOnCleared 기본값 = true 라 회귀 안전** — 기존에 만든 모든 Combat 방 prefab 의 RoomEntryRuntimeController 가 *기본값 true* 라 본 CL 적용 후에도 *자동 문 열림 + 보상 패널* 둘 다 발생. 즉 Combat 방의 prefab 을 모두 false 로 변경해야 보상 흐름이 정상. Inspector wiring 누락 시 *문 열림과 보상 패널이 동시 표시* 되는 어색한 UX. 회귀 검증 필수
8. **보상 패널이 떠있는 동안 Player 행동 차단** — 현재 RewardPanelView 가 모달인지 확인 필요. 만약 보상 패널 떠있어도 Player 가 이동/공격 가능하다면 기획 위반. CharacterStateAggregator 또는 별도 input lock 필요. 본 CL 범위 확장 가능성. 위험 항목 명시
9. **Boss 클리어 시 마지막 보상 정책** — 현재 plan: Boss 클리어 → 보상 X → RunCleared. 기획상 *보스 처치 후 마지막 보상* 원하면 별도 분기 필요. 본 CL 미포함

---

## 후속 CL 인터페이스

| CL | 본 CL 산출물 활용 |
|---|---|
| **Shop 방 구현 ticket** (미정) | Shop 진입 시 별도 ShopController 가 비슷한 패턴으로 RoomEntered 구독. RewardController 와 분리 |
| **Reward enum 추가 ticket** (미정) | StageRoomType.Reward 추가 시 RewardController 의 필터에 `|| RoomType.Reward` 추가 |
| **랜덤박스 메커니즘 ticket** (CL-109 위험 #5) | 보상 카드로 랜덤박스가 등장 → TryUseOrAdd → 별도 RandomBoxHandler 가 처리. RewardController 무관 |
| **보스 마지막 보상 ticket** (위험 #9) | RewardController.HandleRoomCleared 의 RoomType 분기에 Boss 별도 처리 추가. 또는 RunManager 가 RunCleared 직전에 1 회 강제 표시 |

---

## 참고 — explore 검증 결과

- `RoomEntryRuntimeController.RoomCleared` event (L49) ✅. payload = RoomClearedPayload (roomId + RoomData)
- `StageRoomType` = `{Unknown, Combat, Shop, Event, Boss}` ✅. Start/Reward 없음
- `RewardPanelView.Show(PlayerRelicInventory)` (L21) — 호출처 0 ✅. 본 CL 이 첫 호출자
- `RewardPanelView.OnCardSelected` (L43-47) — _inventory.TryAdd + Hide. CL-108 에서 TryUseOrAdd 로 변경됨
- `RoomEntryRuntimeController` 의 자동 문 열림 = 클리어 시 SetExitWallsActive(false) (L275 추정) ✅. 옵트인 변경 필요
- `RoomEntryZone.OnTriggerEnter2D` (L23-46) — 다음 방 진입 트리거. 1회 보장 (entryConsumed flag) ✅
- `RunManager.HandleRoomCleared` (L191) — 보스방만 RunCleared 로 분기. Combat 분기 없음 (RewardController 가 채울 자리)
- `AllEnemiesDefeatedTracker` (CL-035) — 적 전멸 클리어 판정. 빈 wave 시 즉시 클리어 (시작방 우려)
