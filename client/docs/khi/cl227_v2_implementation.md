# CL-227 V2 카드 뽑기 — 구현 기록

작성일: 2026-05-16
기준 plan: [cl227_card_draw_v2_plan.md](cl227_card_draw_v2_plan.md)
사용법: [cl227_v2_usage.md](cl227_v2_usage.md)

**상태**: 🟢 **Phase 1 구현 완료**

---

## 적용된 결정 (최종)

| # | 결정 | 적용값 |
|---|---|---|
| 1 | 보상 종류 | 골드 도박 (유지/증가/감소) |
| 2 | Mystery 코드 재사용 | 패턴 복제 (Mystery* 6개 untouched, 신규 컴포넌트) |
| 3 | 카드 수 / 픽 수 | 3장 / 1픽 |
| 4 | 입장료 | 100G (SO 에서 조정 가능) |
| 5 | 분포 | 3장 = 1유지 + 1증가 + 1감소 (Fisher-Yates 셔플) |
| 6 | 결과 | 유지=100G / 증가=250G / 감소=0G (회수액 기준) |
| 7 | 1회 가드 | NPC `_consumed` flag |
| 8 | 카드 sprite | Card A back + Blank Yellow front (2D Pixel Quest Vol.3) |
| 9 | Entrance flip | Flip04 (좁음) → Flip01 (정면) — 0.2s |
| 10 | Pick flip | back: 정면→좁음 + front: 좁음→정면 — 0.4s |
| 11 | 자동 닫힘 | **제거** — 플레이어가 X / ESC / 픽 외 클릭 시 닫음 |
| 12 | 닫기 트리거 | X 버튼 (우상단), ESC, 픽 전 Skip 버튼 |
| 13 | 골드 부족 시 | 카드 비활성 + Skip / X 만 활성 |
| 14 | 출구 해제 | 픽 또는 Skip / X 시 (idempotent) |
| 15 | NPC procedural | lazy resolve controller + `GetComponentInParent` room |
| 16 | Controller per-room | `Open(config, forRoom)` 오버로드 — 한 Controller 가 여러 방 처리 |

### Phase 2 후속 (보류)
- 보석 빛 (gem glow) 펄스 — 시도 후 롤백
- Shake (카드 흔들기)
- 사운드 / 카메라 셰이크
- 카드 sprite 별 자동 보석 위치 검출 (방법 A — 노란 픽셀 무게중심) — 코드 제거, 학습 가치만 남김

---

## 작성된 파일 (최종)

### Runtime (6개) — `Runtime/Events/`
| 파일 | 책임 |
|---|---|
| [CardDrawOutcome.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Events/CardDrawOutcome.cs) | enum `CardDrawOutcomeKind` + struct `CardDrawOutcomeEntry` |
| [CardDrawConfig.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Events/CardDrawConfig.cs) | SO (entryCost + 3 outcomes + GetAllOutcomes()) |
| [CardDrawCardView.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Events/CardDrawCardView.cs) | 카드 1장 (back/front sprite + 4프레임 flip + entrance flip) |
| [CardDrawPanelView.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Events/CardDrawPanelView.cs) | 패널 (3 카드 슬롯 셔플 + Skip + X 버튼 + 토스트) |
| [CardDrawController.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Events/CardDrawController.cs) | 봉쇄 + 골드 차감/입금 + 닫기 + 방 클리어 |
| [CardDrawNpcInteractable.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Events/CardDrawNpcInteractable.cs) | F 키 trigger + 1회 가드 + procedural 친화 |

### Editor (1개)
- [CardDrawUIBuilder.cs](../../LostMemory/Assets/_Project/Scripts/Editor/Events/CardDrawUIBuilder.cs) — Sync / Wire / SaveAsPrefabs 메뉴 3종

### 자산 (1개)
- [CardDrawConfig_Default.asset](../../LostMemory/Assets/_Project/ScriptableObjects/Events/CardDrawConfig_Default.asset) — 100G / 100·250·0

### 사용된 외부 자산 (수정 없이 참조)
- `Assets/2D Pixel Quest Vol.3 - The UI-GUI/Sprites PNG/Skill Cards- Flip Animations/Back Face Flip/Back Face Flip A/F_U_CardA_Back_Flip01~04.png`
- `Assets/2D Pixel Quest Vol.3 - The UI-GUI/Sprites PNG/Skill Cards- Flip Animations/Skills face flip - Blank/F_U_Blank Yellow Card_Flip01~04.png`
- `Assets/_Project/Art/Fonts/Galmuri9.asset`

### 기존 코드 변경
**없음** — Mystery / Vending Machine / Shop / Stage 코드 모두 untouched.

---

## 아키텍처 다이어그램

```
[CardDrawNpc] (per-방, prefab)
    │ Player tag trigger
    │ F 키 입력
    ↓
[CardDrawController] (씬 싱글톤)
    │ Open(config, forRoom)
    ↓
[CardDrawPanelView] (Canvas 자식, 씬 싱글톤)
    │ Init → 3 outcomes 셔플
    │ for each card: cardView.Init(entryCost, onClick)
    ↓
[CardDrawCardView] × 3 (panel 자식)
    │ Init → entrance flip (Flip04 → 01)
    │ idle: BackImage = Flip01 (정면)
    │ PickButton.onClick → onClickCallback(this)
    ↑
    │ panel.HandleCardClicked(index)
    │   → ✅ _picked = true (1회 가드)
    │   → cardView.RevealOutcome(picked outcome)
    │   → 다른 2장도 RevealOutcome (선택지 공개)
    │   → OnCardPicked event 발화
    ↓
[Controller.HandleCardPicked]
    │ GoldWallet.Spend(entryCost) + Add(outcome.ReturnGold)
    │ InventoryPanel / ShortcutBar refresh
    │ ResolveRoomCleared (idempotent)
    │ 패널 유지 (X / ESC / Skip 으로 수동 닫기)
    ↓
[Controller.HandleClosePressed] (X / ESC / Skip)
    │ Close panel + restore player controls
    │ ResolveRoomCleared
```

---

## 핵심 알고리즘

### 1. 결과 셔플 (Fisher-Yates)
```csharp
private static void ShuffleInPlace<T>(T[] arr)
{
    for (int i = arr.Length - 1; i > 0; i--)
    {
        int j = UnityEngine.Random.Range(0, i + 1);
        (arr[i], arr[j]) = (arr[j], arr[i]);
    }
}
```
→ 3 outcomes 의 위치만 랜덤. 결과 *분포는 보장* (정확히 1유지/1증가/1감소).

### 2. Sprite 인덱스 의미 (주의)

| 시리즈 | [0] 의미 | [3] 의미 | 기본 idle |
|---|---|---|---|
| **Card A back** | Flip01 = **정면** (자연스러운) | Flip04 = 좁음 (측면) | `[0]` |
| **Yellow front** | Flip01 = 좁음 (측면) | Flip04 = **정면** (자연스러운) | `[3]` |

⚠️ back 과 front 의 의미가 *반대*. 패키지 디자인 quirk.

### 3. Flip 애니메이션 순서

**Entrance flip** (패널 열림 시):
```
backFrames: [3] (좁음) → [2] → [1] → [0] (정면) — 0.2s
```

**Pick flip** (카드 클릭 시):
```
1. back closing:  backFrames [0] (정면) → [1] → [2] → [3] (좁음) — 0.2s
2. swap back→front
3. front opening: frontFrames [0] (좁음) → [1] → [2] → [3] (정면) — 0.2s
4. outcome 텍스트 reveal (마지막 프레임에서)
```

총 픽 후 ~0.4s.

### 4. NPC Procedural 친화 (lazy resolve)

```csharp
private void Awake()
{
    // 부모 방 자동 탐색
    if (roomController == null)
        roomController = GetComponentInParent<RoomEntryRuntimeController>();
}

private void Update()
{
    if (!_playerInRange || _consumed) return;
    if (!Input.GetKeyDown(interactKey)) return;

    // Controller lazy resolve — prefab 인스턴스 시 ref 비어있어도 동작
    if (controller == null)
        controller = Object.FindFirstObjectByType<CardDrawController>();

    controller.Toggle(config, roomController);
}
```

→ NPC prefab 이 어떤 방에서 instantiate 되어도 자동 작동.

### 5. Per-Call Room (다중 방 동시성)

```csharp
public void Open(VendingMachineConfig config, RoomEntryRuntimeController forRoom)
{
    _activeRoomController = forRoom != null ? forRoom : roomController;
    _hasResolvedRoomCleared = false;  // 매 Open 시 reset
    // ...
}
```

→ 한 던전에 카드 뽑기 방 N개 떠도 정상 (각자 자기 방만 클리어).

---

## 봉쇄 / 복원 패턴 (Shop / Mystery / Vending 과 동일)

```csharp
private void SuppressPlayerControls()
{
    playerMovement.MovementForbidden = true;
    playerAim.enabled = false;
    playerMeleeCombo.ExternalBlock = true;
    playerDash.PermitAbility(false);
    playerParry.ExternalBlock = true;
    playerWeaponPresenter.enabled = false; // 칼 회전 freeze
}
```

`OnDisable` 에서 panic restore — 패널 떠있는 채 controller disable 시 잠금 방지.

---

## 씬 ref Stale 처리

`ResolveSceneLocalRefs` — Controller 가 DDOL 부착 시 활성 씬에서 ref 자동 재 wiring (Shop 패턴).

```csharp
panel = ResolveInActiveScene(panel, active);
goldWallet = ResolveInActiveScene(goldWallet, active);
// ... 모든 슬롯 동일 패턴
```

---

## 변경 히스토리

| 날짜 | 변경 |
|---|---|
| 2026-05-16 (초기) | Runtime 6 + Editor 1 + SO 1 + meta 8 작성. 기본 placeholder 패널 |
| 2026-05-16 | 카드 sprite 도입 (Card A back + Yellow front 4프레임씩). preserveAspect=true |
| 2026-05-16 | Entrance flip 추가 (Flip01→04 잘못된 방향) |
| 2026-05-16 | Flip 인덱스 방향 fix — Flip01=정면, Flip04=좁음 (역방향이었음) |
| 2026-05-16 | Yellow front 인덱스 fix — back 과 반대 ([3]=정면) |
| 2026-05-16 | 자동 닫힘 제거. X 버튼 + ESC + Skip 으로 수동 닫기 |
| 2026-05-16 | Save As Prefabs 메뉴 추가 (`PrefabUtility.SaveAsPrefabAssetAndConnect`) |
| 2026-05-16 | Gem glow 시도 (Image + scale + alpha pulse + 방법 A 검출 도구) — 롤백 |

---

## 알려진 한계

| 항목 | 비고 |
|---|---|
| 결과 분포 고정 | 항상 1/3 each. 행운 효과 / 가중치 없음 |
| 결과 종류 3개 고정 | Keep / Increase / Decrease 만. 추가 결과 (대박/꽝/특수) 는 후속 |
| 사운드 / 파티클 없음 | 시각 외 피드백 X |
| Reveal 시 모든 카드 동시 flip | 시각적으로 약간 정신없을 수 있음 |
| 정확한 카드 비율 | preserveAspect=true 라 sprite 비율 유지. UI 카드 슬롯 (180x280) 와 sprite 비율 다르면 빈 영역 |
| Multi-player 미지원 | 호스트/클라 권위 분리 X. 솔로 가정 |
| 통계 / 어쳐브먼트 X | 누적 플레이 횟수, 잭팟 횟수 등 추적 X |

---

## 후속 작업 후보

| 우선순위 | 항목 | 비고 |
|---|---|---|
| 🟢 높음 | 사운드 (카드 등장, 픽, 잭팟) | 게임 임팩트 ↑ |
| 🟢 높음 | 잭팟 (Increase) 시 특별 연출 (색 강조, 파티클) | 결과 차별화 |
| 🟡 중간 | Shake (카드 흔들기) — pick 직전 | 사용자가 원했던 연출 (보류) |
| 🟡 중간 | 결과별 front 색 차별 (Keep=Blue, Increase=Yellow, Decrease=Red) | sprite 6장으로 교체 |
| 🟡 중간 | 가변 입장료 (베팅 슬라이더) | 100G / 500G / 1000G 등 |
| 🔵 낮음 | 행운 효과 (Luck set 영향) | 분포 가중치 조정 |
| 🔵 낮음 | 통계 추적 | 플레이 횟수, 누적 net |
| 🔵 낮음 | Multi-player 동기화 | 호스트 셔플 + RPC broadcast |

---

## Mystery / Vending Machine 와의 관계

| 시스템 | 상태 | 특징 |
|---|---|---|
| **Mystery Shop** (Mystery*.cs) | 💤 dormant | 3슬롯, 정체 가림, 가격 공개. 카드 reveal UI 최초 prototype. 본 CL 의 패턴 영감 |
| **Vending Machine** (VendingMachine*.cs) | 🟢 활성 | 1슬롯 단순 vendor. CL-228 의 본 흐름. NPC procedural 패턴 검증 |
| **Card Draw** (CardDraw*.cs) | 🟢 활성 (본 CL) | 3슬롯 도박. 카드 sprite + flip 애니메이션 + 수동 닫기 |

→ 3 시스템 모두 80% 패턴 공유. 후속 리팩토링에서 `EventControllerBase` / `EventCardViewBase` / `EventNpcInteractableBase` 공통 추출 가능.

---

## 검증 체크리스트

- [ ] 컴파일 에러 없음 (Console 빨간 없음)
- [ ] Sync 메뉴 정상 실행 + GameObject 자동 생성
- [ ] F → 패널 entrance flip (Flip04→01)
- [ ] 카드 클릭 → pick flip (정면→좁음→정면) + outcome reveal
- [ ] 100회 시도 시 결과 ≈ 33% 각 (1/3 보장)
- [ ] 골드 부족 시 카드 비활성 + Skip / X 만 활성
- [ ] X 버튼 클릭 → Close
- [ ] ESC 키 → Close
- [ ] 닫기 시 출구 해제 (RoomEntryRuntimeController.NotifyCustomRoomCleared)
- [ ] 픽 후 NPC 재진입 시 무반응 (`_consumed` 가드)
- [ ] 패널 떠있는 동안 player 이동/공격 차단
- [ ] Prefab Save 후 prefab 인스턴스 정상 작동
- [ ] Procedural 방 안에 NPC prefab 박아도 정상 (auto-find room)
