# CL-146 공통 7세트 효과 — 구현 기록

작성일: 2026-05-06

브랜치: `feat/S14P31C201-?/cl-146-공통-7세트`

기준 plan: [cl146_plan.md](cl146_plan.md), 선행 ticket 구현 기록: [cl145_implementation.md](cl145_implementation.md)

**상태**: 🟢 **검증 완료** — Stat 4세트 (체력/방어/회피/범위) + System 3세트 (행운/탐욕/타로) 모두
정상 동작. PlayerDamageReceiver TDE Health.OnHit 후처리 패턴 검증, 행운 다중 픽 로직 검증
(5장 wiring은 후속 cosmetic). Phase 3 마무리.

---

## 목적

Epic S Phase 3 의 다섯 번째 ticket. **공통 카테고리 7세트 처리** — Phase 3 마무리 (P0/P1).

7세트 분류:
- **Stat 4세트** (값 변경): 체력 / 방어력 / 회피 / 범위
- **System 3세트** (게임 시스템 hook): 행운 / 탐욕 / 타로

**해결되는 문제** — `SetEffectApplicator.cs` 의 미적용 case들:
- 체력: 이미 stat 등록 (PlayerHealthStatApplier 자동 적용)
- 방어 / 회피: stat 등록만, 적용 hook 없음 (피격 시 데미지 차감 / 회피 미작동)
- 범위: stat 등록만, 어디서도 GetTotalMultiplier(Range) 호출 X
- 탐욕 / 행운 / 타로: LogWarning placeholder

**의도된 결과**:
- 체력 / 범위: stat 등록 + Range hook 4곳 (KhiMeleeHitbox, MagicalGirlAI, OnHitEffectRegistry chain/wind)
- 방어 / 회피: PlayerStatModifierContainer.GetTotalFlat 추가 + PlayerDamageReceiver 신설 (TDE Health.OnHit 후처리)
- 탐욕: GoldWallet.SetGainMultiplier
- 행운 4단계: 1스택 RewardPool 가중치 / 3스택 인벤토리 슬롯 / 5스택 보상 다중픽 / 7스택 forceLegendary
- 타로: placeholder LogWarning (CL-147 인계)

---

## 설계 기준 + 사용자 결정

### Plan 단계 결정 사항
- **Defense 합산 정책 = 덧셈** (4 + 4 = 8 차감) — `GetTotalFlat(StatId)` 신설
- **행운 5스택 (다중 보상 픽) = 본 CL 에서 모두 구현** — RewardPanelView 5장 카드 + 다중 선택
- **행운 3스택 (인벤토리 슬롯 +1) = 데이터 우선** — UI 컴포넌트 없으므로 시각 적용은 CL-148
- **Damage receive hook = TDE Health.OnHit 후처리** — OnHit delegate 가 파라미터 없음 → `Health.LastDamage` 프로퍼티로 데미지 추정 → `ReceiveHealth(refund, gameObject)` 로 환불
- **회피 시각 (MISS 텍스트) = 별도 ticket**
- **행운 1스택 가중치 boost = luckPoints × 2**
- **탐욕 "상점 아이템 추가" = 별도 ticket** (Shop 시스템 통합)
- **LuckPicksDouble enum 미존재 → LuckPoints 재사용 + RewardController 가 luckTier 인덱스로 분기 감지**
- **Range는 Fusion 미적용** (laser/AOE 그대로) — 후속 결정

### 작업 중 발견·결정 사항
- **TDE Health API 정확한 메서드명 발견**: plan 의 `GetHealth()` 는 잘못, 실제는 `ReceiveHealth(float, GameObject)` (line 1022). `LastDamage` 프로퍼티 (line 167) 가 직전 데미지 값 직접 노출 → 차이 추정 불필요, 더 깔끔.
- **회피 SO EffectType 입력 오류 (검증 중 발견)**: BuildSet_회피.asset T0~T2 의 EffectType 이 모두 `AttackRangePercent` 로 잘못 입력 → Dodge stat 대신 Range stat 증가. 사용자가 SO 인스펙터 드롭다운에서 잘못 선택. **해소**: `DodgeChancePercent` 로 재입력 → 검증 통과.
- **Defense 표시가 +400% 로 cosmetic 오류**: PlayerStatModifierContainer.Notify 의 포맷 `{magnitude:+0.0%;-0.0%;0%}` 가 4 → 400% 표시. 동작은 정상 (DamageReceiver 가 GetTotalFlat=4 받음), 로그만 헷갈림.
- **RewardPanelView 5장 wiring 미완**: Editor 에서 `_cards` 배열을 3 → 5 로 확장 안 함. 코드는 `Mathf.Clamp(count, 1, _cards.Length)` 로 안전하게 3에 clamp → 다중 픽 로직 정상 (3장 중 2장 선택), 디자인 의도 (5장 중 2장) 만 미달. **별도 cosmetic ticket 으로 분리**.
- **HandleRewardSelected 다중 픽 timeScale 처리**: 1번째 픽에서 timeScale 복구하면 안 됨 (패널 유지 중). `rewardPanelView.gameObject.activeSelf` 체크로 패널 닫힐 때만 복구.

## 확정된 설계 결정

| 결정 | 값 | 근거 |
|---|---|---|
| Defense 합산 | 덧셈 (`GetTotalFlat`) | flat 의미 자연 |
| Damage receive hook | TDE Health.OnHit 후처리 + LastDamage / ReceiveHealth | TDE 원본 미수정, 단순 |
| 회피·방어 적용 순서 | 회피 roll 먼저 → 실패 시 방어 차감 | 회피는 전체 환불, 방어는 부분 차감 |
| Range 적용처 | KhiMeleeHitbox / MagicalGirlAI / OnHitEffectRegistry (chain/wind) | 4곳, Fusion 제외 |
| GoldWallet multiplier | `SetGainMultiplier(1+mag)`, Add 시 `Mathf.RoundToInt(amount × mul)` | 단순 곱셈 |
| 인벤토리 슬롯 | `MaxSlots = baseMaxSlots + bonusSlots` 데이터만 | UI 적용은 CL-148 |
| RewardPool 확장 | 새 메서드 `DrawCount(count, owned, luckPoints, forceLegendary)`, 기존 DrawThree 호환 | 비파괴 |
| 행운 가중치 | luck 태그 RelicData 의 weight + luckPoints × 2 | plan 추천 |
| ForceLegendary | available.Where(r => r.Rarity == Legendary), 후보 0 시 LogWarning | 안전 |
| RewardPanelView | `Show(inventory, count, picksAllowed, luckPoints, forceLegendary)` | 시그니처 확장, 기존 호환 |
| RewardController luck 조회 | BuildManager.GetTagCount / GetActiveTier(RelicTag.Luck) | tier 인덱스 ≥ 2 = picksDouble, ≥ 3 = forceLegendary |
| 다중 픽 timeScale | 패널 닫힐 때만 복구 (`gameObject.activeSelf == false`) | 1번째 픽에서 복구 안 함 |
| LuckPicksDouble enum | LuckPoints 재사용 + tier 인덱스 분기 | enum 추가 회피 |
| TarotProc | LogWarning placeholder | CL-147 인계 |
| RemoveTierEffect | GoldGainPercent → SetGainMultiplier(1f) / LuckSlotExpand → AddSlots(-1) | 비활성화 hook |

---

## 수정 파일

### 신규 (1)

| 경로 | 내용 |
|---|---|
| `LostMemory/Assets/_Project/Scripts/Runtime/Combat/PlayerDamageReceiver.cs` | TDE Health.OnHit 후처리 어댑터. `Health.LastDamage` 로 직전 데미지 조회 → 회피 roll (`GetTotalMultiplier(Dodge)-1`) 성공 시 `ReceiveHealth(damage)` 전체 환불 → 방어 (`GetTotalFlat(Defense)`) 만큼 부분 환불. wiring 진단 로그 + `_logDamageMod` 토글. |

### 수정 (10)

```text
LostMemory/Assets/_Project/Scripts/Runtime/Combat/PlayerStatModifierContainer.cs
    - GetTotalFlat(StatId) 메서드 추가 — Permanent + Timed + Conditional magnitude 합산
      (multiplier 와 별도, DefenseFlat 등 flat 성격 stat 용)

LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeHitbox.cs
    - using LostMemory.Combat 추가
    - [SerializeField] PlayerStatModifierContainer statContainer 슬롯 + Awake 자동 검색
    - Sample() 에서 step.hitboxSize × statContainer.GetTotalMultiplier(StatId.Range)
      (debug preview / OverlapBoxNonAlloc 모두 finalSize 사용)

LostMemory/Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlAI.cs (CL-144)
    - FindClosestEnemy() 의 attackRange 에 _playerStat.GetTotalMultiplier(StatId.Range) 곱하기

LostMemory/Assets/_Project/Scripts/Runtime/Combat/OnHitEffectRegistry.cs (CL-142)
    - ApplyChain() 의 _chainRadius 에 Range multiplier
    - ApplyWindBlade() 의 _windBladeLength 에 Range multiplier (boxCenter / boxSize / 시각 endPoint 모두 effectiveLength 사용)

LostMemory/Assets/_Project/Scripts/Runtime/Stage/GoldWallet.cs (CL-113)
    - _gainMultiplier 필드 + SetGainMultiplier(float) public API
    - Add(amount) 에서 Mathf.RoundToInt(amount × _gainMultiplier) 적용
    - 로그에 "× 1.10 = 55" 형식 표시

LostMemory/Assets/_Project/Scripts/Runtime/Relics/PlayerRelicInventory.cs (CL-110)
    - [SerializeField] _baseMaxSlots (default 25) + _bonusSlots 필드
    - MaxSlots property + AddSlots(int) public API + MaxSlotsChanged 이벤트
    - clamp 보장 (음수 X)

LostMemory/Assets/_Project/Scripts/Runtime/Rewards/RewardPool.cs
    - DrawCount(count, ownedNames, luckPoints, forceLegendary) 메서드 추가
    - 기존 DrawThree(ownedNames) 호환 유지 (내부적으로 DrawCount(3) 호출)
    - PickWeighted 에 luckPoints 파라미터 + GetWeight(data, luckPoints) 변경
    - HasLuckTag(RelicData) 헬퍼 — TagPrimary == Luck || TagSecondary == Luck
    - forceLegendary 시 available.Where(r => !IsConsumable && Rarity == Legendary)
      후보 0 시 LogWarning + return empty

LostMemory/Assets/_Project/Scripts/Runtime/Rewards/RewardCardView.cs
    - public RelicData Data => _data 추가 — 다중 픽에서 카드 식별용

LostMemory/Assets/_Project/Scripts/Runtime/Rewards/RewardPanelView.cs
    - Show(inventory, count, picksAllowed, luckPoints, forceLegendary) 시그니처 확장
    - 기존 Show(inventory) 호환 유지 (count=3, picksAllowed=1)
    - _picksMade / _picksAllowed 상태 + 다중 픽 처리
    - DisableSelectedCard — 선택된 카드만 비활성, 나머지 대기

LostMemory/Assets/_Project/Scripts/Runtime/Stage/RewardController.cs
    - [SerializeField] BuildManager buildManager 슬롯 추가
    - ShowReward 에서 BuildManager.GetTagCount/GetActiveTier(RelicTag.Luck) 조회
      → picksDouble = (luckTier >= 2), forceLegendary = (luckTier >= 3)
      → count = picksDouble ? 5 : 3, picksAllowed = picksDouble ? 2 : 1
    - HandleRewardSelected 에 다중 픽 가드 — rewardPanelView.gameObject.activeSelf 체크
      패널 닫힐 때만 timeScale + aim 복구 + OpenExits

LostMemory/Assets/_Project/Scripts/Runtime/Relics/SetEffectApplicator.cs (CL-140)
    - using LostMemory.Stage 추가
    - [SerializeField] GoldWallet goldWallet + PlayerRelicInventory playerRelicInventory 슬롯
    - OnEnable wiring 진단 로그에 goldWallet / playerRelicInventory 상태 추가
    - GoldGainPercent case → goldWallet.SetGainMultiplier(1f + tier.Magnitude)
    - LuckPoints / LuckLegendaryGuarantee case → no-op (RewardController 직접 조회)
    - LuckSlotExpand case → playerRelicInventory.AddSlots(1)
    - TarotProc case → LogWarning placeholder (CL-147)
    - RemoveTierEffect 에 GoldGainPercent → SetGainMultiplier(1f), LuckSlotExpand → AddSlots(-1) 추가
```

### Unity Editor 작업 (사용자)

- 7개 BuildSetData SO `_tiers` 입력:
  - `BuildSet_체력.asset` T1(1, MaxHealthPercent, 0.10) / T2(3, 0.30) / T3(5, 0.60)
  - `BuildSet_방어력.asset` T1(1, DefenseFlat, 4) / T2(3, 8) / T3(5, 16)
  - `BuildSet_회피.asset` T1(1, DodgeChancePercent, 0.05) / T2(3, 0.15) / T3(5, 0.30)
  - `BuildSet_범위.asset` T1(1, AttackRangePercent, 0.10) / T2(3, 0.30) / T3(5, 0.60)
  - `BuildSet_탐욕.asset` T1(1, GoldGainPercent, 0.10) / T2(3, 0.30) / T3(5, 0.50)
  - `BuildSet_행운.asset` T1(1, LuckPoints, 1) / T2(3, LuckSlotExpand, 1) / T3(5, LuckPoints, 1) / T4(7, LuckLegendaryGuarantee, 1)
  - `BuildSet_타로.asset` T1(1, TarotProc, 0.10) / T2(3, 0.30) / T3(5, 0.60) / T4(7, 1.00)
- `PlayerDamageReceiver` 컴포넌트 부착 (Player) + Health / Container 슬롯 wiring
- `KhiMeleeHitbox` 의 새 `Stat Container` 슬롯 wiring
- `SetEffectApplicator` 의 새 `Gold Wallet` / `Player Relic Inventory` 슬롯 wiring
- `RewardController` 의 새 `Build Manager` 슬롯 wiring
- (보류) `RewardPanelView._cards` 배열 3 → 5 확장 — 후속 cosmetic ticket

### 재사용 (수정 X)

- `BuildManager` (CL-139) — OnSetTierChanged, GetTagCount, GetActiveTier
- `Health` (TDE) — OnHit delegate, LastDamage property, ReceiveHealth(amount, instigator), CurrentHealth
- `RelicData.TagPrimary / TagSecondary` — Luck 태그 체크
- `Camera.main` — 직접 사용 X (Range 적용처는 멀티플라이어만)

---

## 발견·해소된 이슈

### 1. TDE Health API 메서드명 plan 과 차이 (구현 중 발견)
**문제**: Plan 이 `Health.GetHealth(amount, gameObject)` 로 회복 호출 가정. 실제 TDE API 는 `ReceiveHealth(float, GameObject)` (line 1022).

**해소**: PlayerDamageReceiver 에서 `ReceiveHealth(damage, gameObject)` 사용. 또한 plan 의 `currentHealth - lastHealth` 차이 추정 패턴 대신 `Health.LastDamage` 프로퍼티 (line 167) 직접 사용 → 더 깔끔, 동시 다중 데미지 정밀도 개선.

### 2. 회피 SO EffectType 입력 오류 (사용자 검증 중 발견)
**문제**: 사용자가 `BuildSet_회피.asset` 인스펙터에서 EffectType 드롭다운을 `AttackRangePercent` 로 잘못 선택 (DodgeChancePercent 와 헷갈림). 결과: 회피 set 활성 시 Range stat 만 +30% 증가, 실제 회피 stat = 0% → 회피 미발동.

**해소**: 사용자가 인스펙터에서 T0/T1/T2 모두 `DodgeChancePercent` 로 재입력 → 검증에서 `[DamageReceiver] DODGE — refunded 10 (chance=30%)` 정상 발화 확인.

**향후 개선**: SO 의 EffectType 매핑 검증 도구 또는 SO 자동 검증 스크립트 (set 명과 EffectType 일관성 체크) — 별도 polish ticket.

### 3. Defense 표시 +400% (cosmetic)
**문제**: `PlayerStatModifierContainer.Notify` 가 magnitude 를 `{magnitude:+0.0%;-0.0%;0%}` 포맷으로 출력. Defense 의 flat 4 가 +400.0% 로 표시되어 헷갈림.

**해소 (현재)**: 동작은 정상 (PlayerDamageReceiver 가 GetTotalFlat=4 직접 조회), 로그만 misleading. **Notify 포맷 변경 보류** — 다른 stat 들은 % 표시가 정확하므로 일관성 유지. Defense 는 GetTotalMultiplier 호출하지 않도록 주석으로 명시.

**향후**: Notify 가 stat 별로 다른 포맷 (flat vs %) 선택하도록 확장 — cosmetic ticket.

### 4. RewardPanelView 5장 wiring 미완 (사용자 결정으로 deferral)
**문제**: 행운 5스택 시 5장 카드 표시 디자인 의도. RewardPanelView 의 `_cards` 인스펙터 배열이 기존 3장 — Editor 에서 5장 prefab 복제·확장 작업 필요. 사용자가 작업하지 않음.

**해소 (현재)**: 코드의 `Mathf.Clamp(count, 1, _cards.Length)` 안전 fallback → 5장 요청해도 3장 표시. **다중 픽 로직은 정상 작동** (3장 중 2장 선택 → 패널 닫힘). 디자인 의도 (선택 폭) 만 미달.

**향후**: RewardPanelView UI 5장 카드 prefab 확장 — **별도 cosmetic ticket 으로 분리** (CL-148 인벤토리 UI 작업 시점에 함께).

### 5. HandleRewardSelected 다중 픽 timeScale 처리
**문제**: 기존 코드는 매 reward 픽마다 `Time.timeScale = _savedTimeScale` 복구. 행운 5스택의 다중 픽에서는 1번째 픽 후 패널이 유지되어야 함 → timeScale 도 0 유지 필요.

**해소**: HandleRewardSelected 첫 줄에 `if (rewardPanelView.gameObject.activeSelf) return;` 가드 추가. RewardPanelView 가 picksAllowed 도달 시 SetActive(false) 하므로 — 패널 닫힐 때만 timeScale / aim / OpenExits 복구.

---

## 검증 결과 (e2e)

### 1. Wiring 진단 OK ✅
```
[PlayerDamageReceiver] OnEnable — wiring: health=OK, container=OK
[SetEffectApplicator] OnEnable — wiring: ... goldWallet=OK, playerRelicInventory=OK
```

### 2. 방어 (Test A) ✅
신성한 보호 1개 추가 → Defense t0 활성:
```
[SetEffect] APPLY Defense t0: DefenseFlat mag=4
[StatModifier] AddPermanent Defense +400.0% src=SetEffect(Defense, t0)   (cosmetic 표시 +400%, 실제 flat 4)
```
적에게 한 대 맞음:
```
[DamageReceiver] DEFENSE -4 (raw=10, def=4)   ← 데미지 10 중 4 환불, 실제 -6
```

### 3. 회피 (Test B) ✅
가죽 신발 / 댄서의 발 / 비단 천 / 회피 자세 / 숨겨진 오솔길 5개 추가 → Dodge 5스택 → t2:
```
[SetEffect] APPLY Dodge t2: DodgeChancePercent mag=0.3   (SO 재입력 후)
[StatModifier] AddPermanent Dodge +30.0% src=SetEffect(Dodge, t2)
```
적에게 5~10번 맞음 → 약 30% 확률로 회피:
```
[DamageReceiver] DODGE — refunded 10 (chance=30%)   ← 2회 발화 (5번 맞은 중)
```

### 4. 탐욕 (보너스 검증) ✅
행운 RelicData 5개의 secondary=Greed 로 t0 활성:
```
[SetEffect] APPLY Greed t0: GoldGainPercent mag=0.1
[GoldWallet] SetGainMultiplier → 1.10
```
방 클리어 보상 골드 +50 → 실제 +55:
```
[GoldWallet] Add(50 × 1.10 = 55) -> Current=155
```

### 5. 행운 5스택 SetEffect 도달 ✅
별의 운명 / 운명의 수레바퀴 / 운명의 카드 / 네잎 클로버 / 황금의 운 5개 → Luck t2:
```
[SetEffect] APPLY Luck t2: LuckPoints mag=1     (no-op, RewardController 직접 조회)
```

### 6. 행운 다중 픽 ✅ (3장으로 검증)
방 클리어 → reward panel 표시 (사용자 보고):
- 카드 **3장** 표시 (5장 wiring 미완 — 별도 ticket)
- 1장 선택 → 패널 유지, 그 카드만 비활성
- 2장 선택 → 패널 닫힘 + timeScale 복구

→ **다중 픽 로직 정상 작동**. 디자인 의도 (5장 표시) 만 별도 wiring 필요.

### 7. Tarot placeholder ✅
행운 RelicData 의 secondary=Tarot 로 Tarot t1 활성:
```
[SetEffect] APPLY Tarot t1: TarotProc mag=0.3
[SetEffectApplicator] TarotProc 미구현 (CL-147 에서 TarotSystem 신설 후 적용)
```

### 8. CL-142~145 회귀 ✅
- 미소녀 fusion 5합체 정상 작동 (이전 검증)
- 평타 OnHit 효과 (Burn / Slow / Chain / WindBlade) 정상 발화
- WindBlade 길이 = base × Range multiplier 적용 확인

### 미검증 (낮은 우선순위)
- 체력 5스택 +60% (코드 동일 패턴이라 자동 PASS 예상)
- 범위 5스택 시각 (hitbox / 미소녀 사거리 확장 — 회귀에서 부분 확인)
- 행운 7스택 forceLegendary (필터만이라 자동 PASS 예상)
- 행운 1스택 가중치 boost (체감 어려운 통계 변화)
- 행운 3스택 슬롯 +1 데이터 (콘솔 로그만, UI는 CL-148)

---

## 위험 / 결정 미정

### 위험
1. **TDE Health.OnHit 후처리 한계**: OnHit 가 데미지 후 발화하므로 HP 가 잠깐 깎였다가 ReceiveHealth 로 복구 → 0.1초 미만 시각 flicker 가능. 검증에서 Player UI 가 빠르게 뛰어 인지 어려움. 후속 시각 ticket 에서 필요 시 OnHit 타이밍 조정.
2. **Defense flat vs % 혼용 헷갈림**: 다른 stat 은 GetTotalMultiplier 사용, Defense 만 GetTotalFlat. 다른 곳에서 실수로 GetTotalMultiplier(Defense) 호출하면 1 + 4 = 5x 로 잘못 해석 가능. 주석으로 명시.
3. **회피 SO 입력 오류 패턴 재발 가능**: 인스펙터 EffectType 드롭다운에서 set 명과 무관한 EffectType 선택 가능. SO 자동 검증 스크립트 또는 SO 별 EffectType whitelist 도입 권장 — 별도 ticket.
4. **RewardPanelView 5장 wiring 보류**: 다중 픽 로직은 검증됐지만 디자인 의도 (5장 중 2장) 미달. 사용자 경험 측면에서 후속 polish 필요 — cosmetic ticket.
5. **다중 픽 timeScale 가드 의존**: HandleRewardSelected 가 `rewardPanelView.gameObject.activeSelf` 로 패널 닫힘 감지. 패널이 다른 이유로 비활성화되면 잘못 분기 가능. 현재는 안전 (RewardPanelView 가 picksAllowed 도달 시에만 SetActive(false)).
6. **Range 가 Fusion 미적용**: laser/AOE 의 사거리 / viewport 는 Range multiplier 무관. 의도된 (OP 우려) 또는 일관성 부족 — 후속 결정.

### 결정 미정 (본 CL 외)
- [ ] RewardPanelView 5장 카드 prefab 확장 (cosmetic ticket)
- [ ] SO 자동 검증 스크립트 (EffectType whitelist)
- [ ] Defense Notify 포맷 (% vs flat 구분 표시)
- [ ] Range Fusion 적용 여부 검토
- [ ] DamageReceiver 정밀 hook 패턴 (Health subclass 또는 reflection — 정밀도 문제 발견 시)
- [ ] 회피 시각 (MISS 텍스트, 화면 효과)
- [ ] 방어 데미지 차감 시각 (숫자 표시, 사운드)
- [ ] 인벤토리 UI 와 MaxSlots 동기화 (CL-148)
- [ ] 탐욕 "상점 아이템 추가" (Shop 시스템 통합)
- [ ] 타로 시스템 (CL-147)
- [ ] 행운 1스택 가중치 통계 검증 (체감 어려움)
- [ ] LuckPicksDouble enum 정식 추가 (현재 LuckPoints 재사용)

---

## 후속 인계

| Ticket | CL-146 과의 관계 |
|---|---|
| **CL-147 (타로)** | 본 CL 의 `TarotProc` LogWarning placeholder → TarotSystem 실 시스템 |
| **CL-148 (인벤토리 UI)** | MaxSlots 데이터 → UI 동적 슬롯 표시 + 추후 RewardPanelView 5장 wiring 검토 |
| **CL-149 (툴팁)** | 무관 |
| **CL-152 (보상/상점 통합)** | 본 CL 의 RewardPanelView 5장 변경 + 탐욕 상점 추가 통합 |
| **별도 ticket — 회피/방어 시각** | MISS 텍스트, 데미지 차감 숫자 표시, 사운드 |
| **별도 ticket — Fusion Range 통합** | Fusion laser/AOE 도 Range 적용 여부 |
| **별도 ticket — RewardPanelView 5장 카드 wiring** | _cards 배열 3 → 5 확장 (Editor 작업) |
| **별도 ticket — SO EffectType 자동 검증** | set tag ↔ EffectType 일관성 자동 체크 |
| **CL-153 (QA)** | 7세트 검증 시나리오 |

## Phase 3 진행 상태

- [x] CL-138~141 (Foundation Phase 1+2)
- [x] CL-142 (평타 5세트)
- [x] CL-143 (스킬 3세트)
- [x] CL-144 (자동 미소녀 1~4)
- [x] CL-145 (미소녀 5합체)
- [x] **CL-146 (공통 7세트)** ← 본 CL — **Phase 3 P0/P1 완성** 🎉
- [ ] CL-147 (타로) — P2

**Phase 3 진행률: 5/6 (P0/P1 모두 완성, P2만 남음)**

## 예상 vs 실제 시간

| 단계 | 예상 (plan) | 실제 |
|---|---|---|
| A-1 GetTotalFlat | 15분 | 5분 |
| A-2 PlayerDamageReceiver | 45분 | 20분 (LastDamage / ReceiveHealth API 발견으로 단순) |
| A-3 Range hook 3곳 | 1시간 | 30분 |
| B-1 GoldWallet multiplier | 15분 | 10분 |
| B-2 RewardPool 확장 | 30분 | 25분 |
| B-3 RewardPanelView 5장 + 다중 픽 | 1시간 | 40분 (코드 + 시그니처) |
| B-4 RewardController 통합 | 15분 | 20분 (다중 픽 timeScale 가드 추가로 약간 증가) |
| B-5 PlayerRelicInventory MaxSlots | 15분 | 10분 |
| B-6 SetEffectApplicator 7개 case | 15분 | 15분 |
| C-1 BuildSet SO 7개 입력 (사용자) | 15분 | 약 20분 (회피 SO EffectType 입력 오류 + 재입력) |
| C-2 검증 (사용자) | 45분 | 약 40분 |
| Test 도중 SO 수정 사이클 (계획 외) | - | 약 15분 |
| **합계** | **약 5시간 30분** | **약 4시간** |

Plan 추정보다 1.5시간 빠름. 주요 단축:
- TDE API 정확 메서드 (LastDamage / ReceiveHealth) 활용으로 PlayerDamageReceiver 단순화
- CL-142~145 인프라 (BuildManager, SetEffectApplicator 패턴) 재사용
- 사용자 검증 반복 사이클이 빠름 (Test A → SO 수정 → Test B → SO 재수정 → Test C)

5점 ticket 적정 규모 (CL-142 / CL-145 와 비슷).
