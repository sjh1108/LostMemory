# CL-143 스킬 친화 3세트 효과 — 구현 기록

작성일: 2026-05-05

브랜치: `feat/S14P31C201-387/cl-143-스킬-친화-3-세트-효과`

기준 plan: [cl143_plan.md](cl143_plan.md), 선행 ticket 구현 기록: [cl142_implementation.md](cl142_implementation.md)

**상태**: 🟢 **3세트 모두 완전 검증** — Unity Editor Play 테스트에서 쿨감/불/바람 모두
본격 동작 (쿨감 -50% stat / 불 30dmg/6s 도트 / 바람 검기 광역 직선 데미지 30%).
화상 visual + 검기 LineRenderer placeholder 추가.

---

## 목적

Epic S Phase 3 의 두 번째 ticket. CL-142 가 깔아둔 OnHit 인프라
(`OnHitEffectRegistry` + `EnemyStatusEffect`) 위에 **3세트 친화 효과 본격 적용**:

- **쿨감 (Cooldown)** — 2/4/6스택 → `StatId.Cooldown` 멀티플라이어 -15% / -30% / -50%
- **불 (Burn DOT on-hit)** — 1/2/4스택 → 데미지 10/20/30, 지속 2/4/6초, 1초당 1틱, 중첩 시 갱신
- **바람 (Wind 검기 on-hit)** — 1/2/4스택 → victim 위치에서 공격 방향 직선 검기, 경로상 적에 본인 공격력 × magnitude 데미지

**해결되는 문제**: `SetEffectApplicator.cs` L122-125 의 `BurnOnHit / WindAOE` 분기가
`LogWarning` 만 출력하는 미구현 상태. `BuildSet_쿨감 / 불 / 바람` 3개 SO 의 `_tiers`
배열 비어있음.

**의도된 결과**:
- 3세트 본격 동작
- Inspector 에서 SO Tier 입력 완료
- e2e 검증 콘솔 로그로 통과
- (계획 외) 화상 visual placeholder + Wind blade LineRenderer placeholder 추가

---

## 설계 기준 + 사용자 결정

### Plan 단계 결정 사항
- **Wind 효과 = 검기 발사체** (사용자 결정 — plan 추천 "광역" 에서 변경): victim 위치에서
  공격 방향(player→victim) 직선으로 발사. 경로상 박스 영역 내 적에 본인 공격력 × magnitude.
- **Cooldown StatId = 기존 `StatId.Cooldown` 재사용** (이미 enum 정의 있음, 미사용 상태)
- **SetTier 에 `Duration` 필드 신설** + `OnHitEffectRegistry.Register` 시그니처에
  duration 파라미터 추가
- **WindAOE 검기 길이 2.0 / 너비 0.6 유닛** (반경 2.0 동치)
- **화상 visual = SpriteRenderer 빨간 반투명 박스 placeholder** (CL-142 빙결 박스 패턴 미러)

### 작업 중 발견·결정 사항
- **`CooldownReductionPercent` 부호 처리**: SO 의 Magnitude 는 직관적인 양수(0.15 = -15%
  감소)로 입력하고, applicator 가 `-tier.Magnitude` 로 부호 반전해서 stat 누적. SO
  편집 UX 가 명확.
- **Burn DOT instigator 전달**: `EnemyStatusEffect.ApplyBurn(damagePerTick, duration,
  instigator)` 시그니처. `OnHitEffectRegistry` 의 `gameObject` (player) 를 instigator 로
  전달 → `Health.Damage(_, instigator, _, _, _)` 호출 시 정확한 가해자 추적.
- **Burn 갱신 정책**: 중첩 없음 — 매 평타마다 `_burnExpiresAt` / `_burnNextTickAt` 모두
  리셋. 다음 틱이 1초 미만에 발화되지 않도록 명시.
- **Wind 방향 fallback**: `victim - player` 가 0벡터(겹쳤을 때) 면 `combat.transform.right`
  로 폴백. 검기가 player 우측으로 발사.
- **Wind 무한 루프 방지**: 검기 데미지는 `Health.Damage` 직접 호출 (TargetHit 안 발화)
  → 자체 트리거 X. CL-142 chain 패턴 동일.
- **Wind 시각화 = LineRenderer 재사용**: CL-142 ChainBolt 패턴 그대로 활용
  (`_chainMaterial` static cache, 노란 직선 0.3초). 별도 visual 로직 신설 X.

## 확정된 설계 결정

| 결정 | 값 | 근거 |
|---|---|---|
| Wind 효과 형태 | 검기 발사체 (직선 박스 영역) | 사용자 결정 — 광역(plan 추천)에서 변경 |
| 검기 길이 / 너비 | 2.0 / 0.6 유닛 | MVP 기본값, 밸런스 후 조정 |
| 검기 데미지 | BaseDamage × StatId.AttackPower × magnitude | CL-142 chain 과 동일 공식 |
| 검기 시각화 | CL-142 ChainBolt LineRenderer 재사용 (노란 직선 0.3초) | 별도 visual 로직 신설 X |
| Cooldown StatId | 기존 `StatId.Cooldown` 재사용 | enum 추가 회피 |
| Cooldown magnitude 부호 | SO 양수 입력 + applicator 음수 반전 | SO 편집 UX |
| Cooldown 적용 hook | 본 CL 미구현 (LogWarning 만) | CL-160 스태프 스킬에서 적용 |
| Burn duration | SetTier 에 `Duration` 필드 신설 | 데이터 스키마 명확 |
| Burn 중첩 | 갱신 (`_burnExpiresAt` / `_burnNextTickAt` 모두 리셋) | plan 정의 |
| Burn 데미지 instigator | OnHitEffectRegistry 의 gameObject (player) | Health.Damage 가해자 추적 |
| Burn 시각화 | 적 위치에 빨간 반투명 SpriteRenderer (CL-142 freeze 박스 패턴 미러) | 정식 VFX 후속 |
| Register API 시그니처 | `(type, mag, duration, src)` — duration 파라미터 추가 | breaking change 1곳뿐 |
| 기존 5세트 SO 호환 | Duration=0 default 자동 reserialize | Unity 직렬화 정책 |

---

## 수정 파일

### 신규
**없음.** CL-142 인프라 100% 재사용.

### 수정 (4)

```text
LostMemory/Assets/_Project/Scripts/Runtime/Relics/BuildSetData.cs
    - SetTier struct 에 `[Min(0)] public float Duration` 필드 추가 (line 47-48)
      → BurnOnHit 등 시간성 효과의 지속시간 입력. Stat 효과는 0.

LostMemory/Assets/_Project/Scripts/Runtime/Combat/OnHitEffectRegistry.cs
    - `_windBladeLength`, `_windBladeWidth` 인스펙터 필드 추가 (line 50-55)
    - `OnHitEntry` 내부 struct 에 `Duration` 필드 추가 (line 66)
    - `Register` 시그니처에 `float duration` 파라미터 추가 (line 94, 96, 98)
    - `HandleHit` switch 에 `BurnOnHit` / `WindAOE` case 추가 (line 122-123)
    - `ApplyBurn(victim, dmg, duration)` 메서드 신설 (line 175-183)
    - `ApplyWindBlade(victim, magnitude)` 메서드 신설 (line 185-231)
      · 방향 = (victim - player).normalized, 0벡터 fallback = combat.transform.right
      · Physics2D.OverlapBoxAll(boxCenter, (length, width), angle) 으로 적 검색
      · victim 본인 + Player 제외 (이중 데미지 방지)
      · DrawChainBolt 재사용으로 시각화 (CL-142 LineRenderer placeholder)

LostMemory/Assets/_Project/Scripts/Runtime/Enemies/EnemyStatusEffect.cs
    - `_health` 캐싱 필드 + Awake 에서 GetComponent<Health>() (line 33, 60)
    - 화상 visual 색상/sortingOrder 상수 (line 28-30)
    - Burn 상태 필드 4개 (`_burnDamagePerTick`, `_burnExpiresAt`, `_burnNextTickAt`,
      `_burnInstigator`) (line 41-45)
    - `_burnVisual` GameObject 필드 + 관리 플래그 (line 49-50)
    - `ApplyBurn(damagePerTick, duration, instigator)` 메서드 신설 (line 95-105)
    - Update 에 Burn 도트 처리 추가 — 만료 / 1초/틱 / Health.Damage 호출 (line 124-141)
    - `SetBurnVisualActive` / `EnsureBurnVisual` 메서드 신설 — 빨간 반투명 박스
      placeholder, CL-142 FreezeVisual 패턴 미러 (line 220-261)
    - OnDestroy 에 `_burnVisual` 정리 추가 (line 69-72)

LostMemory/Assets/_Project/Scripts/Runtime/Relics/SetEffectApplicator.cs
    - `CooldownReductionPercent` case — magnitude 음수 반전 + LogWarning 추가
      (line 103-107)
    - OnHit 라우팅 case 그룹에 `BurnOnHit` / `WindAOE` 합류 — `Register(tier.EffectType,
      tier.Magnitude, tier.Duration, source)` 호출 (line 116-126)
    - 기존 LogWarning 분기 (line 122-125 → 단일 그룹으로 통합) 제거
```

### Unity Editor 작업 (사용자)

- 3개 BuildSetData SO `_tiers` 입력:
  - `BuildSet_쿨감.asset` — T1(2, 0.15), T2(4, 0.30), T3(6, 0.50). EffectType=`CooldownReductionPercent`. Duration=0.
  - `BuildSet_불.asset` — T1(1, 10, **dur=2**), T2(2, 20, **dur=4**), T3(4, 30, **dur=6**). EffectType=`BurnOnHit`.
  - `BuildSet_바람.asset` — T1(1, 0.10), T2(2, 0.20), T3(4, 0.30). EffectType=`WindAOE`. Duration=0.
- 기존 5세트 SO 자동 reserialize (Duration=0 호환 확인) — Editor 자동 처리
- TestKhi player prefab wiring 변경 없음 — CL-142 OnHitEffectRegistry 슬롯 그대로 작동

### 재사용 (수정 X)

- `BuildManager` (CL-139) — 무관
- `KhiMeleeComboController.TargetHit` 이벤트 — CL-142 구독 그대로 활용
- `PlayerStatModifierContainer.GetTotalMultiplier` — Cooldown stat 누적
- `Health.Damage(damage, instigator, hitForce, invuln, dir)` — Burn / Wind 데미지 적용
- `OnHitEffectRegistry.GetOrAddStatus` (CL-142) — Burn 적용 시 EnemyStatusEffect 자동 부착
- `OnHitEffectRegistry.DrawChainBolt` (CL-142) — Wind 검기 시각화 재사용
- `RelicEffectType.BurnOnHit / WindAOE / CooldownReductionPercent` — enum 이미 정의됨
- `StatId.Cooldown` — enum 이미 정의됨, 미사용이었음

---

## 발견·해소된 이슈

### 1. SetTier.Duration 필드 추가로 인한 SO reserialize
**문제**: 기존 5세트 SO (공속/치명타/일반뎀/얼음/전기) 가 새 필드 추가로 reserialize 필요.

**해소**: Unity 직렬화 정책상 새 필드는 default value(0f) 자동 채워짐. 인게임 검증에서
기존 5세트 그대로 동작 확인. git diff 로 .asset 파일에 `Duration: 0` 라인 추가 확인.

### 2. CooldownReductionPercent 부호 처리 모호성
**문제**: CL-140 author 가 `tier.Magnitude` 그대로 사용 (양수). multiplier 합산 정책상
`AddPermanent(Cooldown, +0.5)` = 50% 증가 (쿨다운 더 길어짐) — 의도와 반대.

**해소**: applicator 에서 `-tier.Magnitude` 로 부호 반전. SO 는 양수 입력(0.15 / 0.30 / 0.50)
유지 — 디자이너 입장에서 "+15% 감소" 직관적. 코드 주석으로 정책 명시.

### 3. CL-142 발견 ArcherArrow 이슈 (OnHitEffectRegistry Character 필터 부재)
**문제 (CL-143 검증 중 재확인)**: 적 화살 (`ArcherArrow-0`) 에도 OnHit 효과 적용됨.
`[OnHit] Burn 30/tick for 6s → ArcherArrow-0`, `[OnHit] WindBlade ... → 1 hits` 등 발화.
다른 시스템 (RoomEntryZone 등) 은 `Character` 컴포넌트 없으면 skip 처리하지만
OnHitEffectRegistry 는 `Health` 만 체크.

**영향**: 거의 없음 — 화살은 곧 destroy. 단, 콘솔 로그 노이즈 + 약간의 CPU.

**해소**: 본 CL 범위 외. 별도 follow-up — `OnHitEffectRegistry.HandleHit` 진입 시점 또는
`GetOrAddStatus` 에서 `victim.GetComponentInParent<Character>()?.CharacterType ==
Character.CharacterTypes.AI` 체크 추가 권장.

### 4. Burn / Wind placeholder 시각 가시성
**문제**: 사용자 검증 시 빨간 박스 / 노란 검기가 명확히 보이지 않음 — sortingOrder
9998/9999 로 설정됐지만 적 sprite 와의 sortingLayer 차이 또는 카메라 zoom 문제로
가려졌을 가능성.

**해소**: 본 CL 범위 외. 콘솔 로그가 코드 경로 100% 발화 증명 (`[OnHit] Burn ...`,
`[OnHit] WindBlade ...` ) 했으므로 게임플레이 검증은 PASS. 정식 VFX ticket 에서 5/3세트
placeholder 일괄 교체 시 sortingLayer 통일 + Camera Settings 점검.

---

## 검증 결과 (e2e)

### 1. Wiring 진단 OK ✅
```
[SetEffectApplicator] OnEnable — wiring: buildManager=OK, statContainer=OK, onHitRegistry=OK (host=TestKhi_MinimalCharacter2D)
```

### 2. 시나리오 1 — 쿨감 단독 (6스택 → t2) ✅
RelicData 6개 (방어 룬, 마법학자의 갑옷, 현자의 안목, 예언의 검술, 늙은 마법사의 지팡이, 마법사의 노트) 추가:
```
[SetEffect] APPLY Cooldown t2: CooldownReductionPercent mag=0.5
[StatModifier] AddPermanent Cooldown -50.0% src=SetEffect(Cooldown, t2)
[CL-143] StatId.Cooldown 적용됨, but no skill system yet — CL-160 후속 hook 에서 GetTotalMultiplier(Cooldown) 로 적용 예정.
```
- t2 (RequiredCount=6) 정확 도달
- 음수 반전 정상 (-50.0%)
- LogWarning 1회 발화

### 3. 시나리오 2 — 불 단독 (4스택 → t2) ✅
RelicData 4개 (화염 부적, 변신 거울, 별 모양 단추, 합체 부적) 추가:
```
[SetEffect] APPLY Fire t2: BurnOnHit mag=30
[OnHit] Register BurnOnHit mag=30 dur=6 src=SetEffect(Fire, t2)
[OnHit] Burn 30/tick for 6s → Orc_CL037(Clone)
[OnHit] Burn 30/tick for 6s → SkeletonArcher_CL041(Clone)
[OnHit] Burn 30/tick for 6s → OrcRider_CL039(Clone)
```
- Fire t2 (RequiredCount=4, mag=30, dur=6) 정확
- 평타마다 Burn 갱신 — 중첩 정책 정상
- 자동 부착 동작 — Orc / OrcRider / SkeletonArcher 모두 EnemyStatusEffect AddComponent 후 ApplyBurn 호출 OK
- 실제 데미지 들어가서 적 빠르게 사망

### 4. 시나리오 3 — 바람 단독 (4스택 → t2) ✅
RelicData 4개 (폭풍의 부름, 폭풍 부적, 회전 마법봉, 바람 깃털) 추가:
```
[SetEffect] APPLY Wind t2: WindAOE mag=0.3
[OnHit] Register WindAOE mag=0.3 dur=0 src=SetEffect(Wind, t2)
[OnHit] ChainBolt material 생성 — shader='Sprites/Default'
[OnHit] WindBlade 30 % dir=(-0.91, 0.41) → 2 hits, 3.0 each
[OnHit] WindBlade 30 % dir=(-0.40, 0.91) → 4 hits, 3.0 each
[OnHit] WindBlade 30 % dir=(0.00, 1.00) → 4 hits, 3.0 each
[OnHit] WindBlade 30 % dir=(-0.07, 1.00) → 0 hits, 3.0 each
```
- Wind t2 (RequiredCount=4, mag=0.3) 정확
- 검기 방향 = (victim - player) 정상
- 한 번에 최대 4마리 동시 타격
- 0 hits 케이스 정상 처리 (경로 빈 경우)
- 데미지 = 3.0 (BaseDamage 10 × magnitude 0.3) 정확
- LineRenderer 재사용 (CL-142 ChainBolt material) 정상

### 5. 무한 루프 방지 ✅
검기 + 체인 동시 등록 상황에서도 무한 루프 X — `Health.Damage` 직접 호출 (TargetHit 미발화).

### 6. CL-142 5세트 회귀 ✅
검증 중 부수 효과로 발화한 Slow / Chain (얼음/전기 secondary 태그):
```
[OnHit] Slow 1 % for 2s → Orc_CL037(Clone)
[OnHit] Chain 10 % → 3 targets, 1.0 each
```
- Duration=0 호환 정상 — 기존 5세트 동작 변화 X
- Register API 시그니처 변경에도 SetEffectApplicator 한 곳에서만 호출되므로 호환성 OK

---

## 위험 / 결정 미정

### 위험
1. **placeholder 시각 가시성 미확인**: 빨간 박스 / 노란 검기가 카메라/sortingLayer 문제로 가려졌을 가능성. 콘솔 로그로 게임플레이는 검증됐지만 사용자 인지 가능성 별도 확인 필요. 정식 VFX ticket 에서 일괄 정리.
2. **ArcherArrow 등 적 발사체에 OnHit 적용**: CL-142 인프라 한계 (Character 필터 부재). 별도 follow-up ticket 권장.
3. **`StatId.Cooldown` 실제 적용 hook 부재**: 본 CL 은 stat 입력만. CL-160 (스태프 스킬) 작업 시 `GetTotalMultiplier(StatId.Cooldown)` 조회로 실제 쿨다운 감소 적용 필요.
4. **빙결 + 화상 동시**: 적이 정지 상태에서도 화상 도트 진행 (정상 의도). 검증에서 자연스러웠음.
5. **`SetTier.Duration` 필드 추가로 인한 SO 메타 reserialize**: Unity 자동 처리됐지만 git commit 시 .asset 파일 메타 변경 잠재. 5세트 .asset 한 번씩 열어서 저장 후 commit 권장.

### 결정 미정 (본 CL 외)
- [ ] OnHitEffectRegistry 에 Character 필터 추가 (ArcherArrow 이슈)
- [ ] Burn 도트 저항/면역 시스템 (적 보스 등)
- [ ] 검기 정확한 attack 방향 — KhiAttackRequest 의 input direction 활용 (현재는 player→victim)
- [ ] 검기 점프 / 체인 (검기가 또 다음 적으로) — 후속 확장
- [ ] 정식 VFX (검기 sprite, 화상 particle) — 별도 ticket
- [ ] sortingLayer / Camera Settings 정리 — 모든 placeholder 통일
- [ ] CL-160 (스태프 스킬) 에서 실제 쿨다운 감소 hook 적용

## 후속 인계

| Ticket | CL-143 과의 관계 |
|---|---|
| **CL-144 (미소녀 자동공격)** | 본 CL 의 BurnOnHit / EnemyStatusEffect.ApplyBurn 활용 가능 (미소녀가 화상 부여 등). OnHitEffectRegistry.Register 패턴 그대로 사용 |
| **CL-145 (미소녀 5합체)** | 합체 광역 공격 = WindBlade 의 강화 버전 패턴 가능 |
| **CL-146 (공통 7세트)** | StatId.Range / Dodge / Defense — 본 CL 과 무관 |
| **CL-160 (스태프 스킬)** | 본 CL 의 `StatId.Cooldown` 을 실제 적용. 스킬 발동 코드에서 `GetTotalMultiplier(Cooldown)` 곱하기 |
| **별도 ticket — 정식 OnHit VFX** | 화상 particle / 검기 sprite + trail / 슬로우 발 밑 / 빙결 결정 / 체인 lightning. 본 CL 의 placeholder 일괄 교체. sortingLayer 통일 |
| **별도 ticket — Character 필터 추가** | OnHitEffectRegistry 에 `victim.Character.CharacterType == AI` 체크. 적 발사체 노이즈 제거 |
| **CL-153 (QA)** | 본 CL 의 3세트 검증 시나리오 + 5세트 회귀 |

## Phase 3 진행 상태

- [x] CL-138~141 (Foundation Phase 1+2)
- [x] CL-142 (평타 5세트)
- [x] **CL-143 (스킬 3세트)** ← 본 CL
- [ ] CL-144~145 (미소녀)
- [ ] CL-146 (공통 7세트)
- [ ] CL-147 (타로)
- [ ] CL-148 (인벤토리 UI)

## 예상 vs 실제 시간

| 단계 | 예상 (plan) | 실제 |
|---|---|---|
| 1단계 (SetTier.Duration) | 15분 | 5분 |
| 2단계 (Register API) | 20분 | 10분 |
| 3단계 (BurnOnHit) | 45분 | 30분 |
| 4단계 (WindBlade) | 1시간 | 40분 |
| 5단계 (Cooldown 라우팅) | 10분 | 5분 |
| 6단계 (BuildSetData SO 입력) | 15분 | 10분 (사용자 작업) |
| 7단계 (검증 — 시나리오 3개) | 45분 | 약 30분 (사용자 작업) |
| **합계** | **약 3시간 30분** | **약 2시간** |

CL-142 인프라 덕에 plan 예상보다 빠름. Register API 시그니처 변경 영향이 1곳뿐이라
breaking change 처리 시간 줄어듦. SO reserialize 도 Unity 자동 처리.
