# CL-144 자동 발동 미소녀 1~4스택 — 구현 기록

작성일: 2026-05-05

브랜치: `feat/S14P31C201-?/cl-144-자동-발동-미소녀-1-4스택`

기준 plan: [cl144_plan.md](cl144_plan.md), 선행 ticket 구현 기록: [cl143_implementation.md](cl143_implementation.md)

**상태**: 🟢 **검증 완료** — Unity Editor Play 테스트에서 1~4스택 모두 본격 동작
(원형 배치 / 1.5초 간격 자동 공격 / 데미지 30% / 속성 시각 4종 색상 전환 /
Character.AI 필터링).

---

## 목적

Epic S Phase 3 의 세 번째 ticket. **게임 정체성 핵심 시스템 — 미소녀 자동 공격**.
CL-140 의 `SetEffectApplicator` 가 `MagicalGirlSummon` / `MagicalGirlElementalAttack` /
`MagicalGirlElementalEnhanced` 케이스를 모두 LogWarning 으로만 두던 것을 본 CL 이 채우고,
floating 미소녀 spawner / 개별 AI 인프라 신설.

**해결되는 문제**: 미소녀 RelicData 13개 (분홍 리본, 별 모양 단추, 얼음 결정, 전기 안경,
바람의 깃털, 합체 부적 등)가 인벤토리에 들어가도 효과 0. BuildManager 카운트는 되지만
실제로 미소녀가 등장하지 않음.

**의도된 결과**:
- 미소녀 1~4스택 시 플레이어 주변에 floating 미소녀 1~4명 등장
- 1.5초마다 가장 가까운 적 자동 공격 (사거리 5 유닛)
- 데미지 = 본인 공격력 × 0.30
- 속성 미소녀 색상 변화 (마지막 획득 정책)
- 5합체는 별도 ticket (CL-145) — 본 CL 은 1~4까지만

---

## 설계 기준 + 사용자 결정

### Plan 단계 결정 사항
- **Prefab 신규 생성 X — 코드 절차적 sprite** (CL-142 FreezeVisual / CL-143 BurnVisual 패턴 재사용). AddComponent 로 SpriteRenderer 동적 부착, Texture2D.whiteTexture 로 원형 sprite. 사용자 Editor 작업 0.
- **위치 = 원형 배치** (반경 1.5 유닛). 1명: 0°, 2명: 0°/180°, 3명: 0°/120°/240°, 4명: 사각형
- **공격 간격 1.5초**, **사거리 5 유닛**, **데미지 비율 0.30 고정**
- **속성 시각 = 마지막 획득 정책** (모든 미소녀가 같은 색상 유지, 새 속성 미소녀 추가 시 일제히 갱신)
- **무적** (Health 컴포넌트 X, 적 공격에 영향 X)
- **치명타 적용 X** (MVP 단순)
- **카운트 매핑 = SetTier.RequiredCount 만 사용** (1/2/3/4). 합체 부적 magnitude 추가 처리는 CL-145 영역
- **Character.AI 필터 추가** — CL-143 발견 ArcherArrow 등 발사체에 OnHit 적용되던 이슈 자체 해결

### 작업 중 발견·결정 사항
- **`[AddComponentMenu]` 어트리뷰트 충돌**: 처음 `MagicalGirlSpawner` 클래스에 `[AddComponentMenu("Lost Memory/Magical Girl/Spawner")]` 부착했더니 Unity Add Component 메뉴 검색에 안 잡힘. 신규 메뉴 경로(`Magical Girl/`) 등록은 가끔 즉시 적용 X — 어트리뷰트 제거 후 기본 경로(`Scripts/LostMemory.MagicalGirl/Magical Girl Spawner`)로 등록. MagicalGirlAI 와 동일 처리 방식.
- **RemoveTierEffect 의 deactivation 누락 발견**: 미소녀 마지막 아이템 제거 시 newTier=-1 케이스에서 ApplyTierEffect 호출 X → 기존 미소녀 떠 있음. RemoveTierEffect 에 `magicalGirlSpawner.SetCount(0)` 추가로 해결 (tier 전환 시에도 0 으로 리셋되지만 ApplyTierEffect 가 즉시 newCount 로 재spawn). 약간의 비효율 (3→4 업그레이드 시 3 destroy + 4 spawn) 있지만 이벤트 빈도 낮아 허용.

## 확정된 설계 결정

| 결정 | 값 | 근거 |
|---|---|---|
| Prefab 방식 | 코드 절차적 SpriteRenderer | CL-142/143 패턴 일관성, 사용자 작업 0, 컨셉 변경에 유연 |
| 위치 배치 | 원형 (반경 1.5 유닛) | 회의록 + plan |
| 공격 간격 | 1.5초 | plan 추천 |
| 사거리 | 5 유닛 | plan 추천 |
| 데미지 비율 | 0.30 고정 | MVP 단순 |
| 속성 시각 정책 | 마지막 획득 (모든 girls 동일 색) | plan 추천. 후속에서 미소녀별 다른 색 가능 |
| 사망 가능 | NO (무적) | plan 추천 |
| 치명타 적용 | NO | MVP 단순 |
| 카운트 매핑 | SetTier.RequiredCount 만 | 합체 부적 magnitude 처리는 CL-145 영역 |
| Character.AI 필터 | FindClosestEnemy 에서 체크 | CL-143 ArcherArrow 이슈 본 CL 자체 해결 |
| sortingOrder | 100 (sprite) | 적/배경보다 위 |
| visual 정책 hook | PlayerRelicInventory.OnRelicAcquired 직접 구독 (Spawner 내부) | SetEffectApplicator 의 ElementalAttack/Enhanced case 는 set 효과로 의미 없음 — 직접 hook 이 더 깔끔 |
| Tier deactivation | RemoveTierEffect 에 SetCount(0) | newTier=-1 케이스 대응 |

---

## 수정 파일

### 신규 (3)

| 경로 | 내용 |
|---|---|
| `LostMemory/Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlVisual.cs` | enum 8종 (Default/Fire/Ice/Lightning/Wind/Light/Dark/Pink) + 색상 Dictionary 정적 헬퍼 + RelicTag→MagicalGirlVisual 매핑 함수 |
| `LostMemory/Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlAI.cs` | 개별 미소녀 자동 공격. Awake 에서 SpriteRenderer 절차적 생성. Update 에서 1.5초 간격 FindClosestEnemy + Damage. Character.AI 필터 포함. SetVisual API. 0.1초 sprite flash placeholder |
| `LostMemory/Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlSpawner.cs` | Player 에 부착. SetCount(N) / RegisterElementalVariant(tag) API. 원형 배치 RebalancePositions. PlayerRelicInventory.OnRelicAcquired 직접 구독해서 마지막 획득 정책 hook |

### 수정 (1)

```text
LostMemory/Assets/_Project/Scripts/Runtime/Relics/SetEffectApplicator.cs
    - using LostMemory.MagicalGirl 추가 (line 2)
    - [SerializeField] MagicalGirlSpawner magicalGirlSpawner 슬롯 추가 (line 35-36)
    - OnEnable 의 wiring 진단 로그에 magicalGirlSpawner 상태 추가 (line 60-63)
    - ApplyTierEffect 의 MagicalGirlSummon case → SetCount(tier.RequiredCount) (line 134-140)
    - MagicalGirlFusion 은 LogWarning 유지 (CL-145)
    - MagicalGirlElementalAttack/Enhanced 는 no-op (시각 처리는 Spawner 자체 hook)
    - RemoveTierEffect 에 MagicalGirlSummon 일 때 SetCount(0) 추가 — newTier=-1 케이스 대응 (line 188-190)
```

### Unity Editor 작업 (사용자)

- `BuildSet_미소녀.asset` `_tiers` 입력:
  - T1: RequiredCount=1, EffectType=`MagicalGirlSummon`, Magnitude=1, Duration=0
  - T2: 2, MagicalGirlSummon, 2, 0
  - T3: 3, MagicalGirlSummon, 3, 0
  - T4: 4, MagicalGirlSummon, 4, 0
- Player GameObject (TestKhi_MinimalCharacter2D) 에 `MagicalGirlSpawner` 컴포넌트 부착 (1회)
- `MagicalGirlSpawner` 인스펙터 wiring:
  - Anchor → 비워두면 self transform 자동
  - Player Stat / Player Combat / Inventory → 같은 GameObject 의 컴포넌트 드래그
  - Log Spawn ☑
- `SetEffectApplicator` 의 새 `Magical Girl Spawner` 슬롯에 위 컴포넌트 드래그

### 재사용 (수정 X)

- `BuildManager` (CL-139) — OnSetTierChanged 이벤트
- `KhiMeleeComboController.WeaponData.BaseDamage` — 데미지 스케일링
- `PlayerStatModifierContainer.GetTotalMultiplier(StatId.AttackPower)` — 공격력 multiplier 합산
- `PlayerRelicInventory.OnRelicAcquired` — 속성 시각 hook
- `RelicData.TagPrimary / TagSecondary` — 미소녀 + 속성 식별
- `Health.Damage(damage, instigator, ...)` — 적 데미지
- `Character.CharacterType` (TDE) — AI 만 타게팅 (발사체 제외)

---

## 발견·해소된 이슈

### 1. AddComponentMenu 어트리뷰트로 인한 컴포넌트 미발견
**문제**: `MagicalGirlSpawner` 에 `[AddComponentMenu("Lost Memory/Magical Girl/Spawner")]` 부착 → Unity Add Component 검색창에 "MagicalGirl" 검색해도 안 잡힘. `MagicalGirlAI` (어트리뷰트 X) 는 정상 발견됨.

**원인 추정**: Unity 컴포넌트 DB 가 신규 menu path 등록을 즉시 반영 X — Reimport 또는 Editor 재시작 필요.

**해소**: `[AddComponentMenu]` 어트리뷰트 제거 → 기본 경로 (`Scripts/LostMemory.MagicalGirl/Magical Girl Spawner`) 로 즉시 등록. 검색에서 발견 가능. `MagicalGirlAI` 와 동일 처리 방식.

**향후**: 정식 VFX ticket 등에서 메뉴 정리 시 어트리뷰트 다시 추가하고 Editor 재시작.

### 2. RemoveTierEffect 의 newTier=-1 deactivation 누락
**문제**: 미소녀 마지막 아이템 제거 시 BuildManager 가 OnSetTierChanged(MagicalGirl, oldTier=0, newTier=-1) 발화 → RemoveTierEffect 호출됨 (활성 tier=0 이었으므로) but ApplyTierEffect 호출 안 됨 (newTier<0). 결과: stat/onHit 효과는 RemoveBySource 로 정리되지만 미소녀 GameObject 는 그대로 떠 있음.

**해소**: RemoveTierEffect 에 EffectType==MagicalGirlSummon 분기 추가 → SetCount(0). Tier 전환 (3→4) 시에도 0 으로 리셋되지만 ApplyTierEffect 가 즉시 SetCount(4) 호출하므로 결과는 동일. Tier 변경이 자주 발생하지 않으므로 비효율 허용.

### 3. CL-143 발견 ArcherArrow 이슈 자체 해결
**문제 (CL-143 유산)**: OnHitEffectRegistry 가 Health 만 체크하고 Character 필터 없어 적 화살 (ArcherArrow-0 등) 에도 OnHit 효과 적용됨. 본 CL 의 미소녀 자동 공격이 같은 문제를 겪을 수 있었음.

**해소**: MagicalGirlAI.FindClosestEnemy 에서 `Character.CharacterType == AI` 체크 추가. 미소녀가 발사체 공격 X. CL-143 의 follow-up ticket 은 OnHitEffectRegistry 별도 patch 로 남김 (본 CL 과 무관).

---

## 검증 결과 (e2e)

### 1. Wiring 진단 OK ✅
```
[MagicalGirlSpawner] OnEnable — wiring: anchor=self, playerStat=OK, playerCombat=OK, inventory=OK
[SetEffectApplicator] OnEnable — wiring: ... onHitRegistry=OK, magicalGirlSpawner=OK (host=TestKhi_MinimalCharacter2D)
```

### 2. SetCount 1→2→3→4 진행 ✅
인벤토리에 미소녀 RelicData 추가하면서 tier 전환 자동 처리:
```
[SetEffect] APPLY MagicalGirl t0: MagicalGirlSummon mag=1
[MagicalGirl] SetCount → 1, visual=Default
[SetEffect] REMOVE MagicalGirl t0 → APPLY t1: MagicalGirlSummon mag=2
[MagicalGirl] SetCount → 0, visual=Fire   (RemoveTierEffect)
[MagicalGirl] SetCount → 2, visual=Fire   (ApplyTierEffect)
[SetEffect] APPLY MagicalGirl t2: MagicalGirlSummon mag=3
[MagicalGirl] SetCount → 3, visual=Ice
[SetEffect] APPLY MagicalGirl t3: MagicalGirlSummon mag=4
[MagicalGirl] SetCount → 4, visual=Lightning
```

### 3. 속성 시각 4종 전환 (마지막 획득 정책) ✅
- 분홍 리본 (S=Range) → Default (분홍, fallback)
- 별 모양 단추 (S=Fire) → Fire (빨강)
- 얼음 결정 (S=Ice) → Ice (파랑)
- 전기 안경 (S=Lightning) → Lightning (노랑)

스크린샷 확인: Player 주변에 4개 노란 사각형 floating (Lightning 시각).

### 4. 자동 공격이 적 처치 ✅
콘솔 RoomCleared 스택 트레이스:
```
[Controller] RoomCleared: roomId='room_combat_small_sample' on 'Module_Bridge 2'.
  ... AllEnemiesDefeatedTracker:HandleEnemyDeath
  ... Health:Kill / Health:Damage
  ← LostMemory.MagicalGirl.MagicalGirlAI:Attack
  ← LostMemory.MagicalGirl.MagicalGirlAI:Update
```
사용자가 KhiDown 으로 다운된 상태에서도 미소녀가 알아서 적 처치 → 자동 타게팅 + 1.5초 간격 발화 정상.

### 5. 무적 ✅
사용자 캐릭터는 KhiDown EnterDown 다중 발화 (적 공격에 다운) 했지만 미소녀들은 그대로 살아남아 공격 지속. Health 컴포넌트 없으므로 적 데미지 적용 X. Collider 도 없어서 적과 물리 충돌 X (적 통과).

### 6. CL-142/143 회귀 ✅
부수 효과 동시 발화 정상 — secondary 태그로 활성된 다른 set 들:
```
[OnHit] WindBlade 10 % dir=(...) → 7 hits, 1.0 each
[OnHit] Burn 10/tick for 2s → Orc_CL037(Clone)
[OnHit] Slow 1 % for 2s → Orc_CL037(Clone)
[OnHit] Chain 10 % → 3 targets, 1.0 each
```
- WindBlade / Burn / Slow / Chain 모두 정상 발화
- 무한 루프 X
- 미소녀 공격 데미지는 OnHit 효과 트리거 X (정상 — 미소녀는 KhiMeleeComboController.TargetHit 이벤트와 무관한 별도 데미지 소스)

### 7. Range 태그 fallback ✅
`[MagicalGirl] Elemental → Range (visual=Default)` — Range 는 MagicalGirlVisualPalette.FromTag switch 에서 default case 로 떨어져 Default(분홍) 시각 사용. 정상 동작이지만 향후 Range 미소녀에 별도 색상 주려면 enum + Dict + FromTag 3곳 1줄씩 추가 필요.

---

## 위험 / 결정 미정

### 위험
1. **Tier 전환 시 destroy + respawn 비효율**: 3→4 업그레이드 시 RemoveTierEffect 가 SetCount(0) 호출 (3 destroy) → 즉시 ApplyTierEffect 가 SetCount(4) (4 spawn). 7번 GameObject 조작. Tier 변경 빈도 낮아 허용. 최적화 필요 시 RemoveTierEffect 시그니처에 newTier 추가.
2. **OverlapCircleNonAlloc 매프레임 호출**: 4명 × 60fps = 240회/초. 검색 반경 5 유닛 작아서 미미. 성능 이슈 시 MagicalGirlAI.Update 에 0.1초 throttle.
3. **속성 시각 단순화 (마지막 획득)**: 모든 girls 같은 색 → 사용자 혼란 가능 ("왜 다 같은 색?"). 향후 미소녀별 다른 색 정책으로 확장하려면 Spawner._currentVisual 단일 → list 로 refactor. 본 CL 외.
4. **Range / Greed / Tarot 등 매핑 부재**: MagicalGirlVisualPalette.FromTag 에 정의 안 된 태그는 Default fallback. 분홍 리본의 Range secondary 가 그 케이스. 의도된 fallback이지만 다양화 시 매핑 추가.
5. **5합체 임계값 처리**: BuildSet_미소녀에 T5(RequiredCount=5)를 안 넣으면 5스택 도달 시 활성 tier 가 T3(RequiredCount=4)에 머무름 → 4명 그대로. CL-145에서 T5 추가 + spawner.SetFusionMode() 호출 패턴으로 전환. 본 CL 안전.
6. **합체 부적 magnitude=2 추가 미소녀**: 본 CL 에서 무시 (SetTier.RequiredCount 만 사용). magnitude 의 "추가 +2 미소녀"는 CL-145 영역.

### 결정 미정 (본 CL 외)
- [ ] OnHitEffectRegistry 에 Character.AI 필터 추가 (CL-143 follow-up — 미소녀 외 평타 자체에서도 발사체 제외)
- [ ] 미소녀가 치명타 가능?
- [ ] 미소녀 공격이 OnHit 효과 트리거? (예: Burn 빌드 시 미소녀 공격도 도트 부여)
- [ ] 정식 미소녀 sprite + 애니메이션 + 파티클 + 사운드 (별도 ticket)
- [ ] 5합체 메커니즘 (CL-145)
- [ ] 속성별 다른 효과 (Fire = Burn 부여 등) — 시각만 → 메커니즘 확장 ticket
- [ ] 미소녀별 다른 색상 정책 (현재는 모두 같은 색)
- [ ] 분홍 리본 등 Range secondary 의 visual 매핑

## 후속 인계

| Ticket | CL-144 와의 관계 |
|---|---|
| **CL-145 (미소녀 5합체)** | 본 CL 의 spawner.SetCount(5) 시 fusion 모드 전환. BuildSet_미소녀 T5 추가. 본 CL 인프라 활용 |
| **CL-146 (공통 7세트)** | 무관 |
| **CL-147 (타로)** | 무관 |
| **별도 ticket — Character 필터 (CL-143 유산)** | OnHitEffectRegistry 에도 Character.AI 체크 추가. 본 CL 의 MagicalGirlAI.FindClosestEnemy 패턴 참고 |
| **별도 ticket — 정식 미소녀 VFX** | placeholder sprite + 0.1초 flash → 정식 sprite/애니메이션/파티클/사운드. AddComponentMenu 어트리뷰트 다시 추가 |
| **별도 ticket — 속성별 다른 효과** | 현재는 시각만. Fire = Burn 부여, Ice = Slow 부여 등 메커니즘 확장 |
| **CL-153 (QA)** | 본 CL 의 1~4스택 검증 + 시각 4종 + 자동 공격 시나리오 |

## Phase 3 진행 상태

- [x] CL-138~141 (Foundation Phase 1+2)
- [x] CL-142 (평타 5세트)
- [x] CL-143 (스킬 3세트)
- [x] **CL-144 (자동 발동 미소녀 1~4)** ← 본 CL
- [ ] CL-145 (미소녀 5합체)
- [ ] CL-146 (공통 7세트)
- [ ] CL-147 (타로)
- [ ] CL-148 (인벤토리 UI)

## 예상 vs 실제 시간

| 단계 | 예상 (plan) | 실제 |
|---|---|---|
| 1단계 (MagicalGirlVisual + 색상) | 10분 | 5분 |
| 2단계 (MagicalGirlAI) | 1시간 | 30분 |
| 3단계 (MagicalGirlSpawner) | 1시간 | 30분 |
| 4단계 (SetEffectApplicator 통합) | 15분 | 15분 |
| 5단계 (속성 시각 hook) | 30분 | 0분 (Spawner 에 통합) |
| 6단계 (BuildSetData SO 입력) | 5분 | 5분 (사용자 작업) |
| 7단계 (검증) | 45분 | 30분 (사용자 작업) |
| **합계** | **약 3시간 45분** | **약 1시간 55분** |
| AddComponentMenu 디버깅 (계획 외) | - | **약 10분** (어트리뷰트 제거로 즉시 해소) |
| RemoveTierEffect deactivation 발견·수정 (계획 외) | - | **약 5분** |
| **총 합계** | **약 3시간 45분** | **약 2시간 10분** |

CL-142/143 인프라 패턴 재사용 + 코드 절차적 sprite 결정 덕에 plan 예상보다 약 1시간 30분
빠름. 5단계 (속성 시각 hook) 는 별도 단계 없이 Spawner 의 OnEnable 구독 1줄로 통합.
