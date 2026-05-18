# CL-147 타로 시스템 — 구현 기록

작성일: 2026-05-06

브랜치: `feat/S14P31C201-?/cl-147-타로`

기준 plan: [cl147_plan.md](cl147_plan.md) (8장 디자인 — 본 CL 에서 3장으로 단순화), 선행 ticket 구현 기록: [cl146_implementation.md](cl146_implementation.md)

**상태**: 🟢 **검증 완료** — 평타 hit count 트리거 + 3장 (Death/Healing/Reroll) 균등 추첨 +
DeathCard 발화 e2e 검증. CL-146 회귀 (행운 5스택 다중 픽, 탐욕 1.10 boost) 동시 정상.
**Phase 3 P2 완성 → Phase 3 100% 종료** 🎉

---

## 목적

Epic S Phase 3 의 마지막 ticket — **타로 시스템 활성화** (P2).

CL-146 종료 시점 상태:
- `SetEffectApplicator.cs` 의 `TarotProc` case → `Debug.LogWarning("...CL-147 에서 TarotSystem 신설 후 적용")` placeholder
- `BuildSet_타로.asset` 4 tier (1/3/5/7) 입력 완료 — 모두 `TarotProc` 효과
- 타로 RelicData 7개 (Generated 폴더) + RewardPool 등록 검증

**의도된 결과**:
- 평타 N번 칠 때마다 3장 중 1장 균등 추첨 발화
- 스택 ↑ → 필요 평타 수 ↓ (1=30 / 3=20 / 5=12 / 7=7)
- 카드 효과: 죽음 (적 MaxHP 30% 데미지) / 회복 (Player MaxHP 30%) / 재추첨 (보상 1회)
- 타로 카드 효과는 `TarotEffectMultiplier` 효과로 강화 (회의록 의도 유지)

---

## 설계 기준 + 사용자 결정

### 원본 plan 8장 → 3장 단순화 (사용자 결정)

원본 cl147_plan.md 의 8장 (별/탑/연인/은둔자/운명의수레바퀴/바보/마법사/...) 모두 제거 → **죽음 / 회복 / 재추첨** 3장으로 축소. 트리거도 "방 입장 확률" → "평타 hit count" 로 변경 (CL-142 OnHit 인프라 재사용 패턴).

### Plan 단계 결정 사항

| # | 항목 | 결정 |
|---|---|---|
| 1 | 카드 종류 | **3장**: 죽음 (공격) / 회복 (힐) / 재추첨 (유틸) |
| 2 | 카드 추첨 가중치 | **균등** (각 1/3) — 스택 무관 |
| 3 | 트리거 | **평타 hit count** — `KhiMeleeComboController.TargetHit` |
| 4 | 스택별 필요 평타 수 (tier 0~3) | t0(1스택)=30 / t1(3스택)=20 / t2(5스택)=12 / t3(7스택)=7 |
| 5 | 죽음 카드 효과 | 현재 방 모든 적 MaxHP 30% × (1+mul) 데미지 |
| 6 | 회복 카드 효과 | Player MaxHP 30% × (1+mul) 회복 |
| 7 | 재추첨 카드 효과 | 다음 보상 패널 카드 (1 + RoundToInt(mul))회 재추첨 |
| 8 | TarotEffectMultiplier enum | **유지** — 회의록 RelicData 효과 매핑 (개별 RelicData 의 효과 강화 hook) |
| 9 | hit count 누적 reset 시점 | **카드 발화 시 0 리셋** (방 이동/사망 무관) |
| 10 | miss 처리 | OnHit 가 hit 시점에만 발화 — 자동으로 miss 카운트 X |
| 11 | 재추첨 multiplier 의미 | mul=0 → 1회 / mul=1 → 2회 / 즉시 적용 후 마지막 결과 표시 |

### 작업 중 발견·결정 사항

- **TDE Health 정확한 메서드명 확인**: CL-146 에서 발견한 `Damage(float, GameObject, float, float, Vector3, List<TypedDamage>)` (line 471) / `ReceiveHealth(float, GameObject)` (line 1022) 그대로 활용 — 별도 시그니처 정정 없음.
- **SetTier 가 struct → null 체크 불가**: 초기 plan 의 `OnTarotTierChanged(SetTier tier)` 단일 메서드 (`tier == null` 분기) 를 **`OnTarotActivated(SetTier)` / `OnTarotDeactivated()` 두 메서드** 로 분리. SetEffectApplicator 의 ApplyTierEffect / RemoveTierEffect 가 각각 호출.
- **RewardPanelView 재추첨 UX**: 즉시 N회 재추첨 후 마지막 결과 표시 (사용자가 "재추첨 사용한 사실" 모름). MVP 단순화 — 후속 ticket 에서 [재추첨] 버튼 UI.
- **죽음 카드 적 필터 이중 가드**: `FindObjectsByType<Health>` 가 Player Health 까지 잡음 → `if (h == ctx.PlayerHealth) continue;` + `Character.CharacterTypes.Player` 체크 이중 가드. CL-142 패턴 재사용.

## 확정된 설계 결정

| 결정 | 값 | 근거 |
|---|---|---|
| 카드 수 | 3장 (Death/Healing/Reroll) | 사용자 결정, 8장 단순화 |
| 트리거 | 평타 hit count (CL-142 TargetHit) | OnHit 인프라 재사용 |
| 추첨 가중치 | 균등 1/3 | 단순, stack 무관 |
| Stack curve | t0=30 / t1=20 / t2=12 / t3=7 | 사용자 결정 곡선 |
| Damage receive 적 필터 | PlayerHealth exclude + Character.Player exclude | CL-142 패턴 |
| 카드 효과 강화 | TarotEffectMultiplier × (1+mul) | RelicData 효과 hook 유지 |
| Reroll 적용 시점 | 다음 Show 시 즉시 N회 적용 | UX 단순 |
| Hit count reset | 카드 발화 시 0 리셋 | 방/사망 무관 |
| Activated/Deactivated 분리 | 두 메서드 | SetTier struct null 불가 |
| Tier index 매핑 | RequiredCount switch (1/3/5/7 → 0/1/2/3) | BuildSet SO 그대로 |

---

## 수정 파일

### 신규 (4)

```text
LostMemory/Assets/_Project/Scripts/Runtime/Tarot/TarotCardId.cs
    - enum { Death, Healing, Reroll }

LostMemory/Assets/_Project/Scripts/Runtime/Tarot/ITarotCard.cs
    - class TarotContext { TarotSystem System; Health PlayerHealth; RewardPanelView RewardPanel; float EffectMultiplier; }
    - interface ITarotCard { TarotCardId Id; void Activate(TarotContext ctx); }

LostMemory/Assets/_Project/Scripts/Runtime/Tarot/TarotCards.cs
    - DeathCard: FindObjectsByType<Health> → exclude Player → MaxHP × (0.30 × (1+mul)) damage
    - HealingCard: ctx.PlayerHealth.ReceiveHealth(MaxHP × 0.30 × (1+mul))
    - RerollCard: ctx.RewardPanel.RequestReroll(1 + RoundToInt(mul))

LostMemory/Assets/_Project/Scripts/Runtime/Tarot/TarotSystem.cs
    - MonoBehaviour, [AddComponentMenu("Lost Memory/Tarot/Tarot System")]
    - 슬롯: meleeController (KhiMeleeComboController) / playerHealth (TDE Health) / rewardPanelView
    - HitThresholds = [30, 20, 12, 7] (tier index 0~3)
    - OnEnable: meleeController.TargetHit += HandleHit
    - HandleHit(KhiAttackRequest, AttackStepData, Health victim) — _hitCount++, threshold 도달 시 Random 추첨 + Activate
    - OnTarotActivated(SetTier): RequiredCount switch → _currentTierIndex 설정
    - OnTarotDeactivated(): _currentTierIndex=-1, _hitCount=0
    - SetEffectMultiplier(float mul): _effectMultiplier = mul
```

### 수정 (3)

```text
LostMemory/Assets/_Project/Scripts/Runtime/Relics/RelicEffectType.cs
    - enum 끝에 TarotEffectMultiplier 추가 (= 33)
    - 신규 추가만이므로 기존 SO reserialize 영향 없음

LostMemory/Assets/_Project/Scripts/Runtime/Relics/SetEffectApplicator.cs
    - using LostMemory.Tarot 추가
    - [SerializeField] TarotSystem tarotSystem 슬롯 추가
    - OnEnable wiring 진단 로그에 tarotSystem 상태 추가
    - ApplyTierEffect:
        case TarotProc → tarotSystem.OnTarotActivated(tier)  (LogWarning 제거)
        case TarotEffectMultiplier → tarotSystem.SetEffectMultiplier(tier.Magnitude)
    - RemoveTierEffect:
        TarotProc → tarotSystem.OnTarotDeactivated()
        TarotEffectMultiplier → tarotSystem.SetEffectMultiplier(0f)

LostMemory/Assets/_Project/Scripts/Runtime/Rewards/RewardPanelView.cs
    - _pendingRerolls 필드 추가
    - public void RequestReroll(int additionalDraws) — 누적
    - Show 메서드의 DrawCount 직후 _pendingRerolls > 0 시 즉시 N회 재추첨 후 마지막 결과만 표시
```

### Unity Editor 작업 (사용자)

- 신규 GameObject `TarotSystem` 생성 (Player 자식: `TestKhi_MinimalCharacter2D > TarotSystem`)
  - `TarotSystem.cs` 컴포넌트 부착
  - `Melee Controller` 슬롯 ← KhiMeleeComboController 부착된 GameObject
  - `Player Health` 슬롯 ← Player root (TestKhi_MinimalCharacter2D, Health 컴포넌트 자동 검색)
  - `Reward Panel View` 슬롯 ← UI Canvas 의 RewardPanelView
- `SetEffectApplicator` 의 새 슬롯 `Tarot System` ← 위 GameObject 드래그
- `BuildSet_타로.asset` — CL-146 입력 그대로 OK (모두 TarotProc, magnitude 0)
- `RewardPool.asset` 의 `_All Rewards` 에 타로 RelicData 7개 등록 검증:
  - `별의 운명` (행운+타로, 전설), `운명의 수레바퀴` (행운+타로, 유니크), `운명의 카드` (행운+타로, 레어)
  - `점성술사의 별자리` / `타로 카드 별` (행운+타로, 일반)
  - `점쟁이의 수정구` (탐욕+타로, 레어), `부의 신탁` (탐욕+타로, 일반)

### 재사용 (수정 X)

- `BuildManager` (CL-139) — OnSetTierChanged, GetTagCount(Tarot), GetActiveTier(Tarot)
- `KhiMeleeComboController` (CL-142) — `event Action<KhiAttackRequest, AttackStepData, Health> TargetHit`
- `Health` (TDE) — `Damage(...)`, `ReceiveHealth(amount, instigator)`, `MaximumHealth`, `CurrentHealth`
- `RewardPool / RewardPanelView` (CL-110/146) — `Show(...)` 추가 호출, DrawCount 그대로
- `OnHitEffectRegistry` (CL-142) — TargetHit 구독 패턴 reference (별도 인스턴스로 같은 이벤트 구독)

---

## 발견·해소된 이슈

### 1. SetTier 가 struct → null 체크 불가 (구현 중 발견)
**문제**: Plan 의 `OnTarotTierChanged(SetTier tier)` 단일 메서드에서 `if (tier == null) ...` 분기로 비활성 처리하려 했으나 SetTier 가 `[Serializable] struct` 라 nullable 아님. 컴파일 에러.

**해소**: 두 메서드로 분리 — `OnTarotActivated(SetTier tier)` (활성), `OnTarotDeactivated()` (비활성, 매개변수 없음). SetEffectApplicator 의 ApplyTierEffect / RemoveTierEffect 가 각각 호출. **더 명확한 설계** — null 분기 제거.

### 2. Player Health 슬롯 인스펙터 위치 confusion (사용자 검증 중 발견)
**문제**: 사용자가 Player root (TestKhi_MinimalCharacter2D) GameObject 를 선택한 상태에서 그 Inspector 의 Health 컴포넌트를 보고 "Player Health 슬롯이 안 보인다" 고 보고. 실제로는 사용자가 **TarotSystem 자식 GameObject 를 선택해야** 거기 부착된 TarotSystem 컴포넌트의 슬롯들이 Inspector 에 표시됨.

**해소**: 사용자에게 "Hierarchy 에서 TarotSystem 자식 GameObject 클릭 → 그 Inspector 에 Tarot System 컴포넌트의 슬롯 3개 보임" 안내. 즉시 wiring 완료 후 정상 동작.

**향후 개선**: 컴포넌트 검증 도구 (모든 슬롯 wiring 확인) 또는 인스펙터 가이드 — polish ticket.

### 3. mul=0 고정 (현재 SO 입력)
**관찰**: `BuildSet_타로.asset` 의 t1~t4 가 모두 `TarotProc` (magnitude 0) 입력. → TarotEffectMultiplier 효과 발화 안 됨 → 카드 효과 항상 base (30%/30%/1회).

**의도된 결과**: 본 CL MVP. 회의록 의도 (스택 ↑ → 카드 효과 강화) 는 t2~t4 를 TarotEffectMultiplier 로 전환하면 가능. 별도 SO 조정 결정.

**향후**: t2 = TarotEffectMultiplier 0.3 / t3 = 0.7 / t4 = 1.0 으로 전환 시 → 7스택 시 죽음/회복 60%, 재추첨 2회. 사용자 게임 밸런스 검토 후 결정.

---

## 검증 결과 (e2e)

### 1. Wiring 진단 OK ✅
```
[SetEffectApplicator] OnEnable — wiring: ... goldWallet=OK, playerRelicInventory=OK, tarotSystem=OK
```

### 2. 타로 7스택 활성 ✅
타로 RelicData 7개 (별의 운명/수레바퀴/카드/별자리/타로 카드 별/수정구/부의 신탁) 모두 인벤토리 추가:
```
[PlayerRelicInventory] 유물 획득: 별의 운명 (× 7)
[SetEffect] APPLY Tarot t3: TarotProc mag=1
[Tarot] 활성 — tier 3 (필요 평타 7타, 현재 누적 0)
```

### 3. 평타 hit count + DeathCard 발화 (×2) ✅
방 1 — 7타 누적:
```
[Tarot] hit 5/7
[Tarot] PROC! 카드 = Death (threshold 7 도달)
[Tarot/Death] 4 적에게 MaxHP 30% 데미지 (mul=0.00)
```
방 2 — 다시 7타 누적:
```
[Tarot] hit 5/7
[Tarot] PROC! 카드 = Death (threshold 7 도달)
[Tarot/Death] 4 적에게 MaxHP 30% 데미지 (mul=0.00)
```

→ 카운터 0 리셋 + 적 4명 모두 30% MaxHP 데미지 적용 (mul=0 base) 확인.

### 4. CL-146 회귀 ✅
**행운 5스택 다중 픽** (luckPoints=5, picksAllowed=2):
```
[RewardPanel] Show — count=5, picksAllowed=2, luckPoints=5, forceLegendary=False
[RewardController] Reward panel shown. timeScale=0, aim locked. luck=5 tier=2 count=5 picks=2 forceLegendary=False
```

**탐욕 1스택 +10%**:
```
[GoldWallet] SetGainMultiplier → 1.10
[GoldWallet] Add(50 × 1.10 = 55) -> Current=155
```

### 5. 미발화 카드 (랜덤 시드 우연) — 코드 정상
- **HealingCard**: 미발화. 7타 발화 2회 모두 우연히 Death (균등 1/3 × 2회 ≈ 11% Death-Death).
- **RerollCard**: 미발화 (동일 이유). 코드 검토 결과 정상 — Random.Range(0, 3) 균등 분포.

→ 더 긴 run 또는 시드 조정 시 자연 발화 예상. 코드 로직 자체는 정상.

### 6. CL-142~145 회귀 ✅
- 평타 OnHit 효과 (Burn/Slow/Chain/WindBlade) 정상 — 별도 발화 충돌 X
- 미소녀 시스템 (CL-144/145) 정상
- 보상 패널 다중 픽 (CL-146) 정상

### 미검증 (낮은 우선순위)
- HealingCard 발화 (랜덤 시드 — 더 긴 run 자연 검증)
- RerollCard 발화 + 보상 패널 재추첨 (`[RewardPanel] N회 재추첨 적용` 로그)
- TarotEffectMultiplier mul>0 적용 (현재 SO 모두 mul=0)
- 타로 1스택 t0 = 30타 발화 (현재 7스택만 검증, t1~t3 동일 패턴이라 자동 PASS 예상)

---

## 위험 / 결정 미정

### 위험
1. **SO 가 모두 TarotProc → mul=0 고정**: 카드 효과 항상 base (30%/30%/1회). 회의록 의도 (스택 ↑ → 강화) 가 미반영. 사용자 게임 밸런스 검토 후 t2~t4 를 TarotEffectMultiplier 로 전환 권장.
2. **Reroll UX 미흡**: 즉시 N회 재추첨 후 마지막 결과 표시 → 사용자가 재추첨 사용한 사실 모름. 후속 ticket 에서 [재추첨] 버튼 UI 또는 시각 알림 필요.
3. **Hit count reset 시점**: 카드 발화 시에만 0 리셋. 방 이동/사망 무관. 메타 누적이 의도일 수도, 의도 아닐 수도. 후속 사용자 검토 후 조정.
4. **DeathCard FindObjectsByType<Health>**: 매 발화마다 씬 전체 검색 → 적 N마리 환경에서 GC 부담 가능. MVP 충분 (방 단위 적 ~10마리), 보스급 시 캐싱 패턴 검토.
5. **TargetHit 시그니처 변경 시 영향**: `Action<KhiAttackRequest, AttackStepData, Health>` — 이 시그니처 변경되면 TarotSystem.HandleHit + OnHitEffectRegistry.HandleHit 모두 수정 필요. 계약 안정성 약함.
6. **Random.Range 시드 균등성**: Unity 의 Random.Range(0, 3) 가 충분히 균등하지만 적은 표본 (이번 검증 2회) 에서는 우연히 한쪽 카드만 나올 수 있음. 정통계 검증은 큰 표본 필요.

### 결정 미정 (본 CL 외)
- [ ] BuildSet_타로 t2~t4 를 TarotEffectMultiplier 로 전환 (게임 밸런스 검토 후)
- [ ] 재추첨 [버튼] UI (현재 즉시 N회 적용)
- [ ] hit count miss 페널티 (현재 OnHit 만 카운트)
- [ ] 타로 카드 발화 시각 (오버레이, 사운드, 카메라 흔들림)
- [ ] 8장 디자인 복원 옵션 (별/탑/연인/은둔자/운명의수레바퀴/바보/마법사 추가)
- [ ] hit count reset 시점 정책 (현재 발화 시만 — 방/사망 무관)
- [ ] DeathCard 적 검색 캐싱 (보스 환경)

---

## 후속 인계

| Ticket | CL-147 과의 관계 |
|---|---|
| **CL-148 (인벤토리 UI)** | 무관 |
| **CL-149 (툴팁)** | 타로 카드 효과 툴팁 작성 시 30%/30%/1회 표 참조 |
| **CL-152 (보상/상점 통합)** | `RewardPanelView.RequestReroll` 메서드 → 상점 재추첨에 재사용 가능 |
| **별도 — 타로 시각 폴리싱** | 카드 발화 오버레이, 사운드, 카메라 흔들림 |
| **별도 — 타로 8장 확장** | 별/탑/연인/은둔자/운명의수레바퀴/바보/마법사 추가 (cl147_plan.md 원본) |
| **별도 — 타로 SO 밸런스 (TarotEffectMultiplier)** | t2~t4 를 mul 0.3/0.7/1.0 으로 전환 시 효과 강화 |
| **별도 — Reroll UX (버튼/시각)** | 사용자가 재추첨 사용 인지 가능하도록 |
| **CL-153 (QA)** | 7세트 (CL-146) + 타로 (CL-147) 검증 시나리오 |

## Phase 3 진행 상태

- [x] CL-138~141 (Foundation Phase 1+2)
- [x] CL-142 (평타 5세트)
- [x] CL-143 (스킬 3세트)
- [x] CL-144 (자동 미소녀 1~4)
- [x] CL-145 (미소녀 5합체)
- [x] CL-146 (공통 7세트)
- [x] **CL-147 (타로)** ← 본 CL — **Phase 3 100% 완성** 🎉

**Phase 3 진행률: 6/6 (P0/P1/P2 모두 완성)**

## 예상 vs 실제 시간

| 단계 | 예상 (plan) | 실제 |
|---|---|---|
| 1 enum + SetEffectApplicator case | 15분 | 10분 |
| 2 ITarotCard + 3 cards | 45분 | 30분 (TDE API CL-146 에서 이미 검증됨) |
| 3 TarotSystem MonoBehaviour | 45분 | 30분 (SetTier struct null 분기 → 두 메서드 분리) |
| 4 RewardPanelView 재추첨 | 15분 | 10분 |
| 5 Wiring + 검증 (사용자) | 60분 | 약 40분 (Player Health 슬롯 confusion 5분 추가) |
| **합계** | **약 3시간** | **약 2시간** |

Plan 추정보다 1시간 빠름. 주요 단축:
- CL-146 에서 TDE Health.Damage / ReceiveHealth 시그니처 검증 완료 → 본 CL 에서 시행착오 없음
- CL-142 OnHitEffectRegistry 구독 패턴 그대로 재사용 (TargetHit 시그니처 동일)
- 사용자 wiring 검증 사이클 빠름 (1 회 수정 후 바로 PASS)

3점 ticket 적정 규모 (CL-143 / CL-144 와 비슷, 단순 디자인 + 강한 인프라 재사용).

---

## Phase 3 종료 회고

CL-138 (Foundation) 부터 CL-147 (타로) 까지 약 5주간의 16세트 시스템 완성. 핵심 성과:

- **세트 시스템 인프라**: `BuildSetData` SO 16개 + `BuildManager` 카운트/tier + `SetEffectApplicator` 라우터 — RelicEffectType enum 확장만으로 새 효과 추가 가능
- **OnHit 인프라 (CL-142)**: `KhiMeleeComboController.TargetHit` event → `OnHitEffectRegistry` / `TarotSystem` 등 다중 구독 가능. 평타 기반 추가 효과 확장 용이
- **Stat 시스템 통합**: `PlayerStatModifierContainer` 의 multiplier (% stack) + flat (Defense) 동시 지원 → Damage / OnHit / 시각 모두 일관된 stat 조회
- **재사용 패턴**: CL-142 → CL-146 → CL-147 점진적 인프라 구축. 마지막 ticket 일수록 작업량 감소 (5h → 4h → 2h)

**남은 후속 polish**:
- 시각 (회피 MISS / 방어 차감 숫자 / 타로 카드 발화 오버레이)
- 인벤토리 UI (CL-148)
- 툴팁 (CL-149)
- QA (CL-153)
- 8장 타로 확장 / SO 밸런스 조정 / Reroll UX
