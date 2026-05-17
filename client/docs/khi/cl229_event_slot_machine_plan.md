# CL-229: 이벤트 방 — 슬롯머신 (Slot Machine)

**Epic**: I. 이벤트 방 / 메타 보상 루프
**상태**: 📋 **plan only** — 미구현
**선행**: CL-227 (카드 뽑기 — NPC + 봉쇄 패턴), CL-228 (미스터리 구매 — GoldWallet 차감 + 출구 해제 패턴), CL-113 (Shop 패턴)

> 이벤트 방 3종 시리즈 마지막. **3종 중 가장 *연출 무거운*** — 릴 회전 + 정지 + 매칭 판정 + 보상. 솔로에서도 임팩트 강하고 멀티 확장 시 관전 재미 최강 (마지막 릴 멈출 때 친구가 "777!" 외치는 순간).

---

## Context

### 슬롯머신 컨셉

플레이어가 방에 들어가 슬롯머신 오브젝트와 상호작용 → "스핀 비용 100G" 표시 → F 키 → 100G 차감 + 릴 3개 회전 시작 → 1.5초 후 릴 1, 0.3초 후 릴 2, 0.3초 후 릴 3 순차 정지 → 매칭 판정 → 보상 토스트.

**보상 = 골드** (이벤트 방 3종 차별화: 카드=다양 / 구매=유물 / 슬롯=골드 도박).

### 매칭 판정 (Phase A — 단순)

| 조합 | 보상 |
|---|---|
| 7 7 7 | 비용 × 10 = 1000G |
| 같은 심볼 3 (7 외) | 비용 × 5 = 500G |
| 같은 심볼 2 + 다른 1 | 비용 × 1.5 = 150G (본전 약간 ↑) |
| 모두 다름 | 0G (꽝) |

심볼 종류: 5개 (7, 체리, 종, 별, 다이아) — 가중치 균등하면 *모두 다름* 확률 = 5×4×3 / 5³ = 48%.

### 왜 본 CL 이 가장 마지막인가

- CL-227 의 카드 reveal 코루틴 패턴 재사용 (릴 정지 코루틴)
- CL-228 의 GoldWallet 차감 + 부족 시 거부 패턴 재사용
- 둘 다 검증된 후 본 CL 이 *연출 강화* 에 집중 가능

### 기존 시스템과의 관계

| 시스템 | 재사용 | 변경 |
|---|---|---|
| `GoldWallet.Spend / Add` ([cs:80](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/GoldWallet.cs)) | **재사용** | 그대로 |
| `ShopNpcInteractable` 패턴 ([cs:26](../../LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopNpcInteractable.cs)) | **패턴 복제** — `SlotMachineInteractable` (F 키 trigger) | 1회 가드 X — 다회 스핀 자유 (단 골드 부족하면 자연 막힘) |
| `ShopController.SuppressPlayerControls` ([cs:252](../../LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopController.cs)) | **패턴 복제** — 패널 떠있는 동안 봉쇄 | 동일 |
| `ResolveSceneLocalRefs` 패턴 ([ShopController.cs:169](../../LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopController.cs)) | **재사용** | 동일 |
| `RoomEntryRuntimeController.OpenExits / NotifyCustomRoomCleared` ([cs:139,349](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/RoomEntryRuntimeController.cs)) | **재사용** | 1회 스핀 또는 "그만하기" 후 해제 |
| `EventCardView.Reveal` 패턴 (CL-227) | **참조** — 릴 회전/정지 코루틴이 비슷한 unscaledDeltaTime 기반 | 릴 별도 컴포넌트 |
| RewardPanel / RewardController | **재사용 X** — 본 CL 은 보상이 골드 전용 (RelicData 무관) | |

---

## 결정해야 할 사항 (사용자 확인 필요)

| # | 결정 | 후보 | 추천 |
|---|---|---|---|
| 1 | 릴 개수 | 2 / **3** / 5 | **3** — 정통 슬롯, 매칭 판정 단순 |
| 2 | 심볼 개수 | 3 / **5** / 8 | **5** (7, 체리, 종, 별, 다이아) — 꽝 확률 적당 (~48%) |
| 3 | 스핀 비용 | 50G / **100G** / 가변 (베팅 슬라이더) | **100G** Phase A. 베팅 슬라이더는 후속 |
| 4 | 보상 배율 | 위 표 (×10/×5/×1.5) / 다른 비율 | **위 표** — 기댓값 약 1.0 (본전). 7×10 으로 *대박* 임팩트 |
| 5 | 심볼 가중치 | 균등 / 7만 낮게 (희귀) / 매번 가중치 다름 | **균등** Phase A. *7 가중치 ↓* 후속 (대박 희소성 ↑) |
| 6 | 스핀 횟수 제한 | 무제한 (골드 한계) / 1회만 / N회 | **무제한** — 골드 부족 시 자연 종료 |
| 7 | 릴 정지 입력 | 자동 정지 / Skill-stop (스페이스로 강제 정지) | **자동 정지** Phase A. Skill-stop 후속 폴리시 |
| 8 | 보상 종류 | 골드만 / 골드 + 유물 (777=Legendary 유물) | **골드만** Phase A — 단순. 후속 7×3 = 유물 보상 |
| 9 | 출구 해제 | 1회 스핀 후 / "그만하기" 버튼 / 스핀 안 하고 떠나기 자유 | **"그만하기" 버튼** + 스핀 안 해도 떠나기 자유 |
| 10 | 멀티 확장 | 호스트 권위 추첨 + 결과 sync / 각 클라 독립 | **호스트 권위** — 부정 방지. Phase A 솔로 — 본 CL 외 |

---

## 시스템 사실

### 패널 떠있는 동안 봉쇄 — Shop 과 동일 ([ShopController.cs:252-276](../../LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopController.cs))
- timeScale 변경 X (적 없음)
- KhiPlayerAim.enabled = false
- CharacterMovement.MovementForbidden = true
- KhiMeleeComboController.ExternalBlock = true
- KhiDashController.PermitAbility(false)
- KhiParryController.ExternalBlock = true
- KhiWeaponPresenter.enabled = false (칼 회전 freeze)

### 릴 회전 코루틴 — 가능한 구현 패턴
- 릴 = sprite 9장 (또는 vertical scrolling sprite atlas) 가 빠르게 위→아래 흐름
- `Time.unscaledDeltaTime` 기반 (timeScale 변경 안 하지만 일관성)
- 정지 시 정확히 *결정된 심볼* 위치에 멈춤 (smooth easing — Lerp + 마지막 0.3초 OutBack 등)
- 결과는 코루틴 시작 *전* 이미 결정 (Random.Range) — 회전은 연출만

### "F 키 multi-press" 가드
- 릴 회전 중 F 입력 무시 (`_isSpinning` flag)
- 릴 모두 정지 후 0.5초 후 다시 스핀 가능 (보상 토스트 표시 시간)

### 출구 잠금
- RoomData.InitContext.LockExitDoors = true (이벤트 방 일관)
- "그만하기" 버튼 → controller 가 `OpenExits + NotifyCustomRoomCleared`
- 또는 골드 0 으로 더 못 굴리면 자동 "그만하기" 안내

### 멀티 권위 (Phase A 외)
- 호스트만 `Random.Range` 호출 → 결과 RPC broadcast
- 각 클라는 결과 받은 후 동일한 릴 회전 연출 → 같은 심볼에 정지
- 본 CL 솔로에선 `HostAuthority.IsHost = true` 라 분기 없이 동작

---

## 작업 범위

### Phase A — 데이터 모델

- [ ] `SlotMachineConfig.cs` (신규 SO) — `Assets/_Project/Scripts/Runtime/Events/`
  - `int SpinCost` (기본 100)
  - `Sprite[] Symbols` (5개)
  - 심볼 가중치 `int[] SymbolWeights` (균등이면 모두 1)
  - 보상 배율 `float JackpotMultiplier` (10), `float TripleMultiplier` (5), `float DoubleMultiplier` (1.5)
  - "잭팟 심볼 인덱스" `int JackpotSymbolIndex` (예: 0 = 7)
- [ ] `SlotMachineSpinResult.cs` (신규 struct)
  - `int[] SymbolIndices` (3개)
  - `int RewardGold` (계산된 보상)
  - `SlotMachineRewardKind Kind` (Jackpot / Triple / Double / Miss)

### Phase B — UI

- [ ] `SlotMachinePanelView.cs` (신규)
  - `Init(SlotMachineConfig config, GoldWallet wallet)`
  - 릴 3개 (`SlotReelView[]`) + 스핀 버튼 + 그만하기 버튼 + 보상 토스트 텍스트 + 골드 표시
  - `OnSpinPressed` → controller 가 구독 (골드 차감 + 결과 결정 + 릴 회전 트리거)
  - `OnQuitPressed` → controller 가 구독 (Close + 방 클리어)
  - `PlaySpin(SlotMachineSpinResult result)` — 릴 3개 회전 + 순차 정지 + 결과 표시 코루틴
  - `RefreshAffordability(int gold)` — 스핀 버튼 어둡게 (골드 부족 시)
- [ ] `SlotReelView.cs` (신규) — 릴 1개 컴포넌트
  - `Init(Sprite[] symbols)`
  - `Spin(int targetIndex, float duration)` — duration 동안 스크롤 → 마지막 0.3초 easing → targetIndex 에 정지
  - 내부: vertical Image scrolling 또는 sprite swap loop
- [ ] `SlotMachinePanel.prefab` (신규) — `Prefabs/UI/`
  - 릴 3개 가로, 스핀 버튼, 그만하기 버튼, 골드 텍스트, 결과 토스트

### Phase C — 컨트롤러

- [ ] `SlotMachineController.cs` (신규)
  - 인스펙터 슬롯: panel, config(SlotMachineConfig), goldWallet, [봉쇄 6종], **roomController**
  - `Open()` — 패널 활성 + Init + 봉쇄
  - `Close()` — 패널 비활성 + 복원
  - `Toggle()` — F 키 콜백
  - `HandleSpinPressed()`:
    1. 골드 부족 시 토스트 "골드 부족" + return
    2. `goldWallet.Spend(config.SpinCost)`
    3. 심볼 3개 추첨 (가중치 기반 `WeightedRandomSymbol()` 3회)
    4. 매칭 판정 → SlotMachineSpinResult 생성
    5. `panel.PlaySpin(result)` 호출
    6. 코루틴 끝나면 `goldWallet.Add(result.RewardGold)` (RewardGold > 0 시)
  - `HandleQuitPressed()`:
    1. `Close()`
    2. `roomController.OpenExits()` + `NotifyCustomRoomCleared()`
  - `_isSpinning` flag — 회전 중 추가 스핀 차단
  - `ResolveSceneLocalRefs()` — Shop 패턴
  - `SuppressPlayerControls / RestorePlayerControls / SetCombatInputsBlocked` — Shop 동일
- [ ] `SlotMachineInteractable.cs` (신규) — [ShopNpcInteractable.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Shop/ShopNpcInteractable.cs) 복제 + 단순화
  - `slotController.Toggle()` 호출 (인자 없음 — controller 가 config 보유)
  - Trigger zone + F 키 + promptObject + playerTag

### Phase D — 데이터 자산

- [ ] `SlotMachineConfig_Default.asset` 생성
  - SpinCost = 100
  - Symbols = [7, 체리, 종, 별, 다이아] (placeholder sprite)
  - SymbolWeights = [1, 1, 1, 1, 1]
  - 배율 = (10, 5, 1.5)
  - JackpotSymbolIndex = 0
- [ ] `RoomData_Event_SlotMachine_01.asset`
  - RoomType = Event
  - InitContext.LockExitDoors = true (그만하기로만 나갈 수 있음)
- [ ] `Room_Event_SlotMachine.prefab` — 방 레이아웃 (슬롯머신 오브젝트 + Trigger collider)

### Phase E — Editor / 씬 셋업

- [ ] `MAP_event_slotmachine_test.unity`
- [ ] `SlotMachinePanel.prefab` 의 릴/버튼 레이아웃
- [ ] 슬롯머신 오브젝트 sprite (placeholder OK)
- [ ] 심볼 sprite 5개 (placeholder — 임시 도형도 OK)

### Phase F — 검증

- [ ] **단일 방 — 정상 스핀 흐름**
  - GoldWallet=500 시작
  - 방 진입 → 출구 잠김
  - F → 패널 + 릴 3개 (정지 상태) + 스핀 버튼 + 그만하기
  - 스핀 → -100G + 릴 3개 회전 → 1.5초 후 릴1 정지 → 0.3초 릴2 → 0.3초 릴3
  - 결과 토스트 (예: "꽝!" / "+500G!" / "JACKPOT +1000G!")
  - 보상 골드 자동 입금 (GoldWallet 갱신)
- [ ] **multi-spin**
  - 0.5초 토스트 후 다시 스핀 가능
  - 골드 부족 시 스핀 버튼 어둡게 + 클릭 무반응
- [ ] **그만하기 흐름**
  - 그만하기 버튼 → 패널 닫힘 + 출구 해제 → 통과 가능
  - 그만하기 안 하고 ESC → Close + 출구 해제 (선택 — 결정 9 변종)
- [ ] **봉쇄 검증** — Shop/CL-227 동일 패턴
- [ ] **회전 중 추가 스핀 차단** — `_isSpinning=true` 동안 클릭 무반응
- [ ] **잭팟 시각 강조** — 잭팟 시 토스트 색 / 사운드 / 카메라 셰이크 (후속, Phase A 는 텍스트 색만)
- [ ] **결과 결정의 재현성** — 같은 seed 로 동일 결과 (시드 고정 디버그 옵션, 후속)
- [ ] **확률 분포 검증** — 1000회 스핀 시뮬레이션 → 잭팟 0.8% (1/125), 트리플 4% (5/125 - 1/125), 더블 ~50%, 꽝 ~48%
- [ ] **절차생성 던전 통합** — RoomData_Event_SlotMachine_01 시퀀스 배치
- [ ] **씬 전환 stale ref** — DDOL 부착 시 ResolveSceneLocalRefs 작동

### 작업 외 (Out of scope)

- 가변 베팅 슬라이더 (결정 3 후속)
- 7 가중치 ↓ (결정 5 후속)
- Skill-stop (결정 7 후속) — 스페이스로 릴 강제 정지
- 777 = 유물 보상 (결정 8 후속)
- 사운드 / 카메라 셰이크 / 잭팟 파티클 (Phase A 후 폴리시)
- 멀티 권위 호스트 추첨 + RPC broadcast
- 누적 잭팟 (jackpot pool 이 매 스핀마다 누적)
- 역사 기록 (이번 런 중 누적 수익률 표시)

---

## 변경 파일

### 신규 (스크립트)
| 파일 | 책임 |
|---|---|
| `Runtime/Events/SlotMachineConfig.cs` | 비용/심볼/가중치/배율 SO |
| `Runtime/Events/SlotMachineSpinResult.cs` | 1회 스핀 결과 구조체 |
| `Runtime/Events/SlotMachinePanelView.cs` | 패널 뷰 + 릴 3개 + 버튼 + PlaySpin 코루틴 |
| `Runtime/Events/SlotReelView.cs` | 릴 1개 (Spin → targetIndex 정지) |
| `Runtime/Events/SlotMachineController.cs` | NPC trigger → 패널 + 골드 차감/입금 + 결과 결정 + 봉쇄 |
| `Runtime/Events/SlotMachineInteractable.cs` | F 키 trigger zone (Shop 패턴 복제) |

### 신규 (자산)
| 자산 | 위치 |
|---|---|
| `SlotMachineConfig_Default.asset` | `ScriptableObjects/Events/` |
| `RoomData_Event_SlotMachine_01.asset` | `ScriptableObjects/Rooms/` |
| `SlotMachinePanel.prefab` | `Prefabs/UI/` |
| `SlotReelView_Slot.prefab` | `Prefabs/UI/` |
| `Room_Event_SlotMachine.prefab` | `Prefabs/Rooms/` |
| `MAP_event_slotmachine_test.unity` | `Scenes/Tests/` |
| (placeholder) 심볼 sprite 5개 | `Sprites/Events/SlotMachine/` |

### 수정
- 없음 (신규 모듈만 추가).

---

## 코드 spec (핵심 부분 요약)

### `SlotMachineSpinResult` (구조)

```csharp
namespace LostMemory.Events
{
    public enum SlotMachineRewardKind
    {
        Miss = 0,
        Double = 1,        // 2 같은 심볼
        Triple = 2,        // 3 같은 심볼 (잭팟 외)
        Jackpot = 3        // 777
    }

    [System.Serializable]
    public struct SlotMachineSpinResult
    {
        public int[] SymbolIndices;  // length = 3
        public int RewardGold;
        public SlotMachineRewardKind Kind;
    }
}
```

### `SlotMachineController.HandleSpinPressed` (핵심)

```csharp
private void HandleSpinPressed()
{
    if (_isSpinning) return;
    if (config == null || goldWallet == null) return;

    if (goldWallet.Current < config.SpinCost)
    {
        if (panel != null) panel.ShowToast("골드 부족");
        return;
    }

    _isSpinning = true;
    goldWallet.Spend(config.SpinCost);
    if (panel != null) panel.UpdateGold(goldWallet.Current);

    // 결과 *먼저* 결정 (회전은 연출만)
    int s1 = WeightedRandomSymbol();
    int s2 = WeightedRandomSymbol();
    int s3 = WeightedRandomSymbol();
    SlotMachineSpinResult result = JudgeResult(s1, s2, s3, config);

    panel.PlaySpin(result, OnSpinAnimationComplete);
}

private void OnSpinAnimationComplete(SlotMachineSpinResult result)
{
    if (result.RewardGold > 0)
    {
        goldWallet.Add(result.RewardGold);
        if (panel != null) panel.UpdateGold(goldWallet.Current);
    }
    if (panel != null) panel.ShowToast(BuildResultMessage(result));
    _isSpinning = false;
}

private int WeightedRandomSymbol()
{
    int total = 0;
    for (int i = 0; i < config.SymbolWeights.Length; i++) total += config.SymbolWeights[i];
    int roll = Random.Range(0, total);
    int cum = 0;
    for (int i = 0; i < config.SymbolWeights.Length; i++)
    {
        cum += config.SymbolWeights[i];
        if (roll < cum) return i;
    }
    return 0;
}

private static SlotMachineSpinResult JudgeResult(int s1, int s2, int s3, SlotMachineConfig config)
{
    var result = new SlotMachineSpinResult { SymbolIndices = new[] { s1, s2, s3 } };
    bool allSame = (s1 == s2 && s2 == s3);
    bool isJackpot = allSame && s1 == config.JackpotSymbolIndex;

    if (isJackpot)
    {
        result.Kind = SlotMachineRewardKind.Jackpot;
        result.RewardGold = Mathf.RoundToInt(config.SpinCost * config.JackpotMultiplier);
    }
    else if (allSame)
    {
        result.Kind = SlotMachineRewardKind.Triple;
        result.RewardGold = Mathf.RoundToInt(config.SpinCost * config.TripleMultiplier);
    }
    else if (s1 == s2 || s2 == s3 || s1 == s3)
    {
        result.Kind = SlotMachineRewardKind.Double;
        result.RewardGold = Mathf.RoundToInt(config.SpinCost * config.DoubleMultiplier);
    }
    else
    {
        result.Kind = SlotMachineRewardKind.Miss;
        result.RewardGold = 0;
    }
    return result;
}
```

### `SlotMachinePanelView.PlaySpin` (코루틴)

```csharp
public void PlaySpin(SlotMachineSpinResult result, Action<SlotMachineSpinResult> onComplete)
{
    StartCoroutine(SpinCoroutine(result, onComplete));
}

private IEnumerator SpinCoroutine(SlotMachineSpinResult result, Action<SlotMachineSpinResult> onComplete)
{
    // 모든 릴 동시 회전 시작
    for (int i = 0; i < _reels.Length; i++)
    {
        _reels[i].StartSpinning();
    }

    // 릴 1 정지 (1.5초 후)
    yield return new WaitForSecondsRealtime(1.5f);
    _reels[0].StopAt(result.SymbolIndices[0], 0.3f);

    yield return new WaitForSecondsRealtime(0.3f + 0.3f);  // 정지 easing 0.3 + delay 0.3
    _reels[1].StopAt(result.SymbolIndices[1], 0.3f);

    yield return new WaitForSecondsRealtime(0.3f + 0.3f);
    _reels[2].StopAt(result.SymbolIndices[2], 0.3f);

    yield return new WaitForSecondsRealtime(0.5f);
    onComplete?.Invoke(result);
}
```

### `SlotReelView.StopAt` (핵심)

```csharp
public void StopAt(int targetIndex, float duration)
{
    StartCoroutine(StopCoroutine(targetIndex, duration));
}

private IEnumerator StopCoroutine(int targetIndex, float duration)
{
    // 회전 속도 점진 감소 + 마지막은 OutBack easing 으로 targetIndex sprite 에 정확히 정지
    float elapsed = 0f;
    while (elapsed < duration)
    {
        float t = elapsed / duration;
        float eased = 1f - Mathf.Pow(1f - t, 3f); // EaseOutCubic
        // _currentScrollOffset 을 targetIndex 위치로 보간
        // sprite swap 또는 vertical Image.fillAmount / RectTransform anchored Y 변경
        elapsed += Time.unscaledDeltaTime;
        yield return null;
    }
    _spinning = false;
    SetSymbolDirect(targetIndex);
}
```

---

## 검증 시나리오 (Phase F 상세)

### F-1. 단일 방 — 정상 스핀
1. `MAP_event_slotmachine_test.unity` Play, GoldWallet=500
2. 방 진입 → 출구 잠김
3. NPC F → 패널 (릴 3개 + 스핀/그만하기 버튼 + 골드 500 표시)
4. 스핀 → 골드 -100 (즉시 400 으로 갱신) + 릴 3개 회전
5. 1.5초 후 릴1 정지, 0.6초 후 릴2, 0.6초 후 릴3
6. 토스트 표시 (꽝/더블/트리플/잭팟 중 하나) + 보상 골드 입금
7. 0.5초 후 다시 스핀 가능

### F-2. 잭팟 강제 (디버그 옵션)
8. config.JackpotSymbolIndex 외 weight 모두 0 → 100% 잭팟
9. 스핀 → 7 7 7 → "JACKPOT +1000G!" + 골드 +1000

### F-3. 골드 부족
10. 골드 50 (< SpinCost 100) → 스핀 버튼 어둡게
11. 클릭 → 토스트 "골드 부족" + 실제 굴리지 X

### F-4. 회전 중 추가 스핀 차단
12. 스핀 중 스핀 버튼 다시 클릭 → 무반응 (`_isSpinning=true` 가드)

### F-5. 그만하기 흐름
13. 그만하기 → 패널 닫힘 + 출구 해제 + 통과 가능
14. 스핀 안 하고 그만하기 가능 — 100% 자유

### F-6. 봉쇄 검증
15. 패널 떠있는 동안 이동/조준/공격 차단 (Shop/CL-227 동일)

### F-7. 확률 분포 검증 (1000회 자동 스핀)
16. 디버그 옵션: 자동 스핀 1000회 + 결과 카운트 console
17. 잭팟 ≈ 1/125 (5심볼 균등 시) — 0.8%
18. 트리플 (잭팟 외) ≈ 4/125 — 3.2%
19. 더블 ≈ 약 48% (실제 분포 확인)
20. 꽝 ≈ 약 48%

### F-8. 절차생성 통합
21. RoomData_Event_SlotMachine_01 을 RouteSequenceConfig 한 칸 배치
22. 던전 진행 시 슬롯머신 방 등장, 위 흐름 동일

### F-9. 씬 전환 stale ref
23. SlotMachineController DDOL 부착 시 ResolveSceneLocalRefs 자동 wiring (Shop 동일)

---

## 알려진 이슈 / 고려

- **결과 사전 결정 vs 회전 중 결정** — *결과 먼저* 가 표준 (회전은 연출). 멀티 확장 시 호스트 결정 후 broadcast 하기 쉬움
- **릴 정지 시 위치 정확도** — sprite swap 방식이 가장 안전 (마지막 frame 에 `SetSymbolDirect(targetIndex)`). vertical scrolling 은 작은 오차 발생 가능 → 마지막 snap 필요
- **토스트 텍스트 다국어** — "JACKPOT" / "꽝" — `RelicTagLabels.ToKorean` 패턴 참조해 `SlotMachineLabels.ToKorean(kind)` 같이 분리
- **회전 도중 패널 외부 ESC** — 회전 중단 + 결과 적용 후 close? 또는 회전 끝까지 기다리기? **Phase A: 회전 끝까지 기다림** (StopAllCoroutines 호출 안 함). ESC 안전성 보장
- **연속 잭팟의 비현실성** — 1/125 가 연속 발생하면 플레이어 의심. 시드 검증 / RNG 정상성 console 로그 옵션
- **golden 골드 입금 타이밍** — 회전 끝난 후 입금이 자연 (스릴). 스핀 즉시 입금하면 의미 X
- **사운드 (Phase A 외)** — 회전음 / 정지 클릭음 / 매칭 음 / 잭팟 음. 후속 폴리시 필수
- **멀티 확장 시 호스트 권위** — Random.Range 호스트만 호출 → result 를 RPC 로 broadcast. 각 클라가 같은 result 로 PlaySpin 호출. UI 동기화 자연. 본 CL 외
- **누적 잭팟 (Progressive jackpot)** — 매 miss 마다 jackpot pool +N 누적, 잭팟 시 pool 전액 지급. 매우 후속

---

## 후속 CL 후보

| CL | 내용 |
|---|---|
| 후속 폴리시 | 사운드 / 카메라 셰이크 / 잭팟 파티클 / Skill-stop 입력 |
| 보상 강화 | 777 = Legendary 유물 (결정 8) |
| 가변 베팅 | 베팅 슬라이더 + 배율 비례 보상 (결정 3) |
| 누적 잭팟 | Progressive jackpot pool — 미스마다 +N 누적 |
| 멀티 확장 | 호스트 권위 결과 결정 + RPC broadcast, 친구 옆에서 함께 보기 |
| 리팩토링 | 이벤트 방 3종 (CL-227/228/229) 의 controller 공통 베이스 추출 (`EventRoomControllerBase` — 봉쇄/복원/sceneLocalRefs/roomController 호출) |
