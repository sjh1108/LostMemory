# CL-227 V2: 이벤트 방 — 카드 뽑기 (골드 도박 버전)

**Epic**: I. 이벤트 방 / 메타 보상 루프
**상태**: 📋 **plan only** — 미구현
**선행**: CL-228 (Vending Machine — NPC procedural 패턴 + 봉쇄/복원/Editor UIBuilder), CL-113 (Shop 패턴)
**관련**: CL-227 V1 ([cl227_event_card_draw_plan.md](cl227_event_card_draw_plan.md)) — *다양 보상* 버전 (보류)

> 사용자 결정으로 V1 의 *유물/소모품/골드 다양 보상* 컨셉 → V2 의 *순수 골드 도박* 으로 전환. V1 plan 은 아카이브, V2 가 실제 구현.

---

## Context

**컨셉**: 100G 입장료를 내고 3장의 카드 중 1장을 픽 → 결과는 *유지 / 증가 / 감소* 셋 중 하나. 순수 도박 미니게임.

| 결과 | 의미 | 기본값 |
|---|---|---|
| **유지** | 입장료만큼 그대로 돌려받음 | +100G (net 0) |
| **증가** | 입장료보다 많이 돌려받음 (잭팟) | +250G (net +150) |
| **감소** | 입장료 일부 또는 전액 손실 | +0G (net -100) |

**확률**: 3장 = 정확히 *1장 유지 / 1장 증가 / 1장 감소* 가 랜덤 위치에 셔플. 매 픽마다 1/3 확률 보장.

기댓값: `(0 + 150 + (-100)) / 3 = +16.67G` → 살짝 player favorable. 디자이너가 SO 에서 자유 튜닝.

### Mystery 시스템과의 관계

Mystery* 코드 (CL-228 작업 중 만들어진 dormant 시스템) 는 *카드 뒤집기 + 가격 표시 + reveal* UI 패턴을 가지고 있음. **본 CL 은 Mystery 패턴 *복제* — 같은 폴더에 별도 컴포넌트로 신규 작성**:

- Mystery 코드는 그대로 보존 (사용자 정책)
- 새 컴포넌트가 같은 시각/구조 (back/front roots, reveal 토글) 를 *재구현*
- 추후 두 시스템 안정화되면 공통 베이스 추출 리팩토링 ticket

### 기존 시스템과의 관계

| 시스템 | 재사용? | 비고 |
|---|---|---|
| `MysteryCardView` ([Events/MysteryCardView.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Events/MysteryCardView.cs)) | **패턴만 복제** | back/front root 구조 + reveal — 새 `CardDrawCardView` 가 미러 |
| `MysteryShopPanelView` | **패턴만 복제** | 3 카드 슬롯 + Skip — 새 `CardDrawPanelView` |
| `MysteryShopController` | **패턴만 복제** | 봉쇄/복원/Open/Close — 새 `CardDrawController` |
| `VendingMachineInteractable` ([Events/VendingMachineInteractable.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Events/VendingMachineInteractable.cs)) | **패턴 복제** — procedural 친화 (lazy resolve + GetComponentInParent) | NPC interaction 패턴 (CL-228 에서 검증) |
| `GoldWallet.Add / Spend` ([Stage/GoldWallet.cs:59,80](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/GoldWallet.cs)) | **재사용** | 입장료 차감 + 결과 골드 입금 |
| `RoomEntryRuntimeController.OpenExits + NotifyCustomRoomCleared` | **재사용** | 첫 픽 또는 Skip 시 출구 해제 |
| `MysteryShopUIBuilder` 패턴 | **복제** | 새 `CardDrawUIBuilder` Editor 메뉴 |

---

## 결정 사항

### 사용자 확정
| # | 결정 | 값 |
|---|---|---|
| 1 | 보상 종류 | **유지 / 증가 / 감소** 골드 도박 |
| 2 | Mystery 재사용 | **패턴 복제** (코드는 별도 신규) |
| 3 | 카드 수 / 픽 수 | **3장 / 1픽** |

### 본 plan 기본값 (사용자 redirect 가능)
| # | 결정 | 적용값 | 근거 |
|---|---|---|---|
| 4 | 입장료 | **100G** | 사용자 명시 |
| 5 | 결과 분포 방식 | **3장 = 1유지 + 1증가 + 1감소 (셔플)** | "셋중 하나" 표현과 일치, 1/3 보장 |
| 6 | 결과 보상 (입장료 포함 회수액) | **유지=100G / 증가=250G / 감소=0G** | EV ≈ +16.67G, 살짝 player favorable |
| 7 | 1방 1회 가드 | **1번만 픽** | 카드 뽑기 = 1회성 이벤트 |
| 8 | reveal 애니메이션 | **즉시 swap** (back→front 활성 토글) | Phase A 단순. 후속 폴리시 |
| 9 | 입장료 부족 시 | **모든 카드 비활성 + Skip 만 가능** | 골드 0 으로 진입해도 frustration X |
| 10 | 출구 해제 | **첫 픽 또는 Skip 시** | Vending Machine 과 동일 정책 |
| 11 | NPC procedural 친화 | **lazy resolve + GetComponentInParent** | CL-228 검증 패턴 |

---

## 시스템 사실

### Vending Machine 의 procedural 패턴 ([VendingMachineInteractable.cs:43-87](../../LostMemory/Assets/_Project/Scripts/Runtime/Events/VendingMachineInteractable.cs))
- NPC `controller` ref null 이면 첫 F 키 시 `FindFirstObjectByType` lazy resolve
- NPC `roomController` ref null 이면 Awake 에서 `GetComponentInParent` 자동 탐색
- → NPC prefab 만들어두면 어떤 방에서 instantiate 되어도 자동 작동
- 본 CL 도 동일 패턴 적용

### Vending Machine 의 Controller per-call room ([VendingMachineController.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Events/VendingMachineController.cs))
- `Open(config, forRoom)` 오버로드 — NPC 가 자기 방을 매번 인자로 전달
- `_activeRoomController` 매 Open 시 갱신 → 한 Controller 가 여러 방 NPC 호출 처리 가능
- 본 CL 도 동일 패턴

### MysteryCardView 의 back/front 구조 ([MysteryCardView.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Events/MysteryCardView.cs))
- `backRoot` / `frontRoot` 두 GameObject — 토글로 reveal
- `RevealAndMarkSoldOut()` — back 비활성, front 활성, 데이터 전개
- 본 CL 의 `CardDrawCardView` 도 동일 구조 (단 데이터 = `CardDrawOutcome`)

---

## 작업 범위

### Phase A — 데이터 모델

- [ ] `CardDrawOutcome.cs` (신규 enum + struct) — `Runtime/Events/`
  - `enum CardDrawOutcomeKind { Keep, Increase, Decrease }`
  - `[Serializable] struct CardDrawOutcomeEntry { kind, returnGold, displayLabel, color }`
- [ ] `CardDrawConfig.cs` (신규 SO)
  - `int entryCost = 100`
  - `CardDrawOutcomeEntry keepOutcome` (returnGold=100, label="유지")
  - `CardDrawOutcomeEntry increaseOutcome` (returnGold=250, label="증가!")
  - `CardDrawOutcomeEntry decreaseOutcome` (returnGold=0, label="감소...")
  - 향후 확장: `outcomes[]` 배열로 일반화 (현재는 3개 고정)

### Phase B — UI

- [ ] `CardDrawCardView.cs` (신규)
  - `MysteryCardView` 와 동일 구조 (backRoot / frontRoot / reveal)
  - 데이터: `CardDrawOutcomeEntry`
  - `Init(int entryCost, Action<CardDrawCardView> onClick)` — back 에 entryCost 표시
  - `RevealOutcome(CardDrawOutcomeEntry outcome)` — front 활성, label/color 표시
  - 호버/affordability 등은 패널에서 통제
- [ ] `CardDrawPanelView.cs` (신규)
  - `Init(CardDrawConfig config, GoldWallet wallet)`
  - 3 카드 슬롯에 entryCost 표시 + 셔플된 outcomes 내부 배정
  - `OnCardPicked(CardDrawCardView card)` — 카드 클릭 시 발화
  - `OnSkipPressed` — 건너뛰기
  - 골드 부족 시 모든 카드 비활성 (Skip 만 가능)
  - 결과 토스트 표시 후 1.5초 자동 닫힘

### Phase C — 컨트롤러

- [ ] `CardDrawController.cs` (신규)
  - `Open(CardDrawConfig config, RoomEntryRuntimeController forRoom)` — Vending 과 동일 패턴
  - `HandleCardPicked(CardDrawOutcomeEntry outcome)`:
    1. `goldWallet.Spend(config.entryCost)` (이미 PanelView 에서 사전 검증)
    2. `goldWallet.Add(outcome.returnGold)` — 결과 보상
    3. PanelView 가 reveal + 토스트
    4. 1.5초 후 Close + ResolveRoomCleared
  - `HandleSkipPressed()` — Close + ResolveRoomCleared
  - 봉쇄/복원/sceneLocalRefs — Vending Machine 과 동일
- [ ] `CardDrawNpcInteractable.cs` (신규)
  - `VendingMachineInteractable` 패턴 복제 + **1회 가드**
  - `_consumed` flag — 한 번 카드 픽 후엔 F 키 무반응
  - lazy resolve controller, auto-find roomController

### Phase D — 데이터 자산

- [ ] `CardDrawConfig_Default.asset` 생성 — `ScriptableObjects/Events/`
  - entryCost = 100
  - keep = 100G return
  - increase = 250G return
  - decrease = 0G return

### Phase E — Editor Setup

- [ ] `CardDrawUIBuilder.cs` (신규 Editor) — `Scripts/Editor/Events/`
  - `MysteryShopUIBuilder` / `VendingMachineUIBuilder` 패턴 복제
  - 메뉴: `LostMemory/Card Draw/Sync In Active Scene`
  - 자동 생성: `Canvas/CardDrawPanel` + `CardDrawController` + `CardDrawNpc`
  - 기존 ShopController 의 player ref 자동 복사

### Phase F — 검증

- [ ] **단일 방 — 정상 흐름**
  - 1000G 시작
  - F → 패널 + 3장 (모두 100G 라벨)
  - 카드 1장 클릭 → -100G + reveal (예: "감소... +0G") + 결과 적용 + 1.5초 후 자동 닫힘
  - 출구 해제 → 통과 가능
- [ ] **유지 / 증가 / 감소 분포 검증** (수동 N번 시도)
  - 100회 시도 시 각 결과 ≈ 33% (1/3 보장)
- [ ] **골드 부족 케이스**
  - 50G < 100G → 모든 카드 비활성 + Skip 만 활성
  - Skip → 출구 해제, 골드 변화 X
- [ ] **1회 가드**
  - 카드 픽 후 다시 F → 무반응 또는 "이미 뽑음" 토스트
- [ ] **봉쇄 검증** — Vending 과 동일
- [ ] **Procedural 검증** (메인 던전에 방 prefab 배치 후)
  - DA 가 카드 뽑기 방 spawn → NPC 자동 등장 → 정상 작동
  - 같은 던전에 카드 뽑기 + vending machine 방 동시 존재 가능

### 작업 외 (Out of scope)

- Reveal 애니메이션 (즉시 swap, 후속 폴리시)
- 가변 입장료 (베팅 슬라이더 — 후속)
- 4가지 이상 결과 (현재 3개 고정 — outcomes[] 배열 일반화는 후속)
- 사운드 / 카메라 셰이크
- 멀티 권위 호스트 추첨

---

## 변경 파일

### 신규 (Runtime 스크립트)
| 파일 | 책임 |
|---|---|
| `Runtime/Events/CardDrawOutcome.cs` | enum + struct (결과 정의) |
| `Runtime/Events/CardDrawConfig.cs` | SO (entryCost + 3 outcomes) |
| `Runtime/Events/CardDrawCardView.cs` | 카드 1장 (back/front + reveal) |
| `Runtime/Events/CardDrawPanelView.cs` | 패널 (3 카드 + Skip + 토스트) |
| `Runtime/Events/CardDrawController.cs` | NPC trigger → 봉쇄 + 골드 차감/입금 + 방 클리어 |
| `Runtime/Events/CardDrawNpcInteractable.cs` | F 키 trigger (1회 가드 + procedural 친화) |

### 신규 (Editor)
| 파일 | 책임 |
|---|---|
| `Scripts/Editor/Events/CardDrawUIBuilder.cs` | Editor 메뉴 자동 셋업 |

### 신규 (자산)
| 자산 | 위치 |
|---|---|
| `CardDrawConfig_Default.asset` | `ScriptableObjects/Events/` |

### 수정
없음 — Mystery 코드 untouched, Vending 코드 untouched.

---

## 코드 spec (핵심)

### `CardDrawOutcome` 구조

```csharp
namespace LostMemory.Events
{
    public enum CardDrawOutcomeKind
    {
        Keep = 0,
        Increase = 1,
        Decrease = 2
    }

    [System.Serializable]
    public struct CardDrawOutcomeEntry
    {
        public CardDrawOutcomeKind Kind;
        public int ReturnGold;        // 입장료 포함 *총 회수액*. 100 = 본전, 0 = 전액 손실
        public string DisplayLabel;   // "유지" / "증가!" / "감소..."
        public Color LabelColor;      // UI 강조용
    }
}
```

### `CardDrawPanelView.HandleCardClicked` (핵심 흐름)

```csharp
private CardDrawOutcomeEntry[] _shuffledOutcomes;  // 매 Init 시 셔플

public void Init(CardDrawConfig config, int gold)
{
    _config = config;
    _gold = gold;

    // 3 outcomes 고정 + 위치만 셔플
    _shuffledOutcomes = new[] { config.KeepOutcome, config.IncreaseOutcome, config.DecreaseOutcome };
    ShuffleInPlace(_shuffledOutcomes);

    for (int i = 0; i < cardViews.Length; i++)
    {
        int captured = i;
        cardViews[i].Init(config.EntryCost, card => HandleCardClicked(captured));
    }
    RefreshAffordability();
}

private void HandleCardClicked(int index)
{
    if (_picked) return;   // 1회 가드
    if (_gold < _config.EntryCost) return;

    _picked = true;
    CardDrawOutcomeEntry outcome = _shuffledOutcomes[index];

    cardViews[index].RevealOutcome(outcome);
    OnCardPicked?.Invoke(outcome);   // controller 가 GoldWallet 처리

    // 다른 카드들도 reveal (선택지 공개)
    for (int i = 0; i < cardViews.Length; i++)
        if (i != index) cardViews[i].RevealOutcome(_shuffledOutcomes[i]);
}
```

### `CardDrawController.HandleCardPicked` (골드 처리)

```csharp
private void HandleCardPicked(CardDrawOutcomeEntry outcome)
{
    // 입장료 차감 (PanelView 사전 검증 후)
    bool ok = goldWallet.Spend(_currentConfig.EntryCost);
    if (!ok) Debug.LogError("[CardDraw] Spend 실패 — desync");

    // 결과 보상 입금
    if (outcome.ReturnGold > 0)
        goldWallet.Add(outcome.ReturnGold);

    if (panel != null) panel.UpdateGold(goldWallet.Current);

    int net = outcome.ReturnGold - _currentConfig.EntryCost;
    if (logFlow) Debug.Log($"[CardDraw] {outcome.Kind} — net {(net >= 0 ? "+" : "")}{net}G. Current={goldWallet.Current}");

    StartCoroutine(CloseAfterDelay(1.5f));
}

private IEnumerator CloseAfterDelay(float seconds)
{
    yield return new WaitForSecondsRealtime(seconds);
    Close();
    ResolveRoomCleared();
}
```

### `CardDrawNpcInteractable.Update` (1회 가드)

```csharp
private bool _consumed;

private void Update()
{
    if (!_playerInRange || _consumed) return;
    if (!Input.GetKeyDown(interactKey)) return;

    EnsureControllerResolved();
    if (controller == null || config == null) return;

    // 1회 가드 — controller.IsOpen 도 체크해서 토글 시 두 번째 진입 방지
    if (controller.IsOpen) { controller.Close(); return; }

    _consumed = true;   // 여는 순간 가드 활성. 닫혀도 다시 못 엶.
    if (logInteraction) Debug.Log($"[CardDrawNpc] '{interactKey}' → Open.");
    controller.Open(config, roomController);
}
```

> ⚠️ Controller 가 `_consumed` 후 닫혀도 NPC 는 다시 안 열림. 이 NPC 1회성. 단, 같은 씬 내 다른 CardDraw NPC 는 영향 X (각자 자기 `_consumed`).

---

## 검증 시나리오 (Phase F 상세)

### F-1. 단일 방 — 정상 흐름
1. GoldWallet=1000 시작
2. 방 진입 → 출구 잠김 (LockExitDoors)
3. NPC F → 패널 + 3장 (각 카드에 "100 G" 표시) + 보유 골드 1000 표시
4. 카드 1장 클릭 → -100G (즉시 900) + 그 카드 reveal (예: "감소... +0G")
5. 다른 2장도 reveal (어떤 결과였는지 보여줌)
6. 1.5초 후 패널 자동 닫힘 + 출구 해제

### F-2. 결과 분포 검증
7. 100회 반복 (수동 또는 디버그 자동) → 유지/증가/감소 각 ≈ 33%

### F-3. 골드 부족
8. GoldWallet=50 (< 100) 으로 진입
9. F → 패널 + 카드 모두 어둡게 (afford=false), Skip 만 활성
10. Skip → 패널 닫힘 + 출구 해제 + 골드 변화 X

### F-4. 1회 가드
11. 카드 픽 후 NPC 다시 가까이 → F → 무반응 또는 prompt 안 뜸
12. (디버그: 로그 "[CardDrawNpc] _consumed=true, ignore" 확인)

### F-5. 봉쇄 검증 — Vending 동일

### F-6. Procedural 검증
13. `Room_Event_CardDraw.prefab` 만들고 `RouteSequenceConfig` 한 칸 배치
14. 메인 던전 진행 시 카드 뽑기 방 자동 등장 → 정상 작동
15. 같은 던전에 vending machine 방도 함께 → 두 시스템 모두 정상

---

## 알려진 이슈 / 고려

- **Mystery 와 Card Draw 코드 80% 패턴 동일** — 후속 리팩토링 ticket 으로 공통 베이스 (`EventCardViewBase`, `EventNpcInteractableBase`) 추출
- **`_consumed` 가드 vs `controller.IsOpen` 체크** — NPC 가 자기 *_consumed* 만 보면 다른 NPC 와 독립. 한 NPC 1회성 + 다른 방 NPC 무영향
- **카드 picked 후 reveal 도중 ESC** — Phase A 는 ESC 시 reveal 중단 + 즉시 닫힘. 결과 보상은 *이미 적용됨* (HandleCardPicked 가 즉시 Spend/Add) — 정상
- **PlayerConsumableInventory 가득** — 본 CL 은 골드만 다루므로 무관
- **셔플 알고리즘** — Fisher-Yates 표준 (`Random.Range` 기반)
- **음수 returnGold** — 0으로 clamp (감소 = 0G 회수, 음수 손실은 entryCost spend 로 충분)
- **EntryCost > 보유 골드인데 첫 pick 클릭 가능?** — PanelView 의 `RefreshAffordability` 가 카드 클릭 비활성화. 방어 코드도 controller 에 있음

---

## 후속 CL / 폴리시

| 항목 | 내용 |
|---|---|
| Reveal 애니메이션 | 카드 뒤집기 코루틴 (Mystery 시스템도 후속에서 추가 예정) |
| 가변 입장료 (베팅) | 슬라이더로 입장료 50~500G 조정 + 결과 비례 보상 |
| 4가지 이상 결과 | outcomes[] 배열 일반화 (예: 대박/중박/유지/소박/꽝) |
| 사운드 / 연출 | 결과 별 SE + 카메라 셰이크 (대박 시) |
| 리팩토링 | Mystery / Card Draw / Slot Machine 공통 베이스 추출 |
| 멀티 확장 | 호스트 권위 셔플 + 결과 RPC broadcast |
