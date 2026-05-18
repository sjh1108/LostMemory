# CL-157 — 검 트리 구현 (단검 / 방패검 / 대검 + Stage 2 강화)

## Context

Epic K 의 첫 콘텐츠 ticket. **CL-156 의 강화 시스템 첫 활용 사례**.

5점 P2, 클라1, CL-156 의존.

### 기존 상태 (확인 완료)

- ✅ `WeaponData` SO 구조 (CL-090) — 콤보 + 데미지 + hitbox + visual
- ✅ `Sword_Default.asset` 1개 존재 (CL-090 산출물) — 본 CL 의 **Stage 0 검** 으로 활용
- ✅ `WeaponElement` enum (CL-155) — Stage 2 속성 부여용
- ✅ `WeaponData._upgrades` / `_stage` / `_upgradeCost` (CL-156) — 트리 입력
- ✅ `PlayerWeaponLoadout.TryUpgrade()` (CL-156) — 강화 흐름
- ❌ 단검 / 방패검 / 대검 SO — **본 CL 신설**
- ❌ Stage 2 강화 무기 SO 3개 — **본 CL 신설**
- ❌ 정식 sprite — placeholder (Sword_Default sprite 재활용)

### 본 CL 책임 범위

1. **검 트리 SO 7개** (Stage 0 ×1 + Stage 1 ×3 + Stage 2 ×3)
2. 각 무기의 콤보 데이터 (Steps 1/2/3타, damage, hitbox, slashFrames)
3. **`_upgrades` 트리 입력** (검 → 단검/방패검/대검, 각자 Stage 2 후보)
4. `_element` 설정 (Stage 2 일부 속성 부여)
5. 검증: 강화 흐름 + 콤보 작동 + 속성 발동

→ **무기별 정식 sprite 디자인은 별도 ticket**. 본 CL 은 placeholder (Sword_Default frames 재활용).
→ **방패검의 방패 메커니즘** (블록/방어) 같은 신규 메커니즘은 별도 ticket. 본 CL 은 데이터 + 수치 차별만.

---

## 결정사항

### 1. 검 트리 구조 (확정)

```
검 (Sword_Default, Stage 0)         _element: None
 ├─ 단검 (Stage 1)                  _element: None  / 빠르고 짧음
 │   └─ 쌍단검 (Stage 2)            _element: None  / 더 빠르고 추가 타격
 ├─ 방패검 (Stage 1)                _element: None  / 균형, 약간 느림
 │   └─ 성검 (Stage 2)              _element: None  / 데미지 ↑, 사거리 ↑
 └─ 대검 (Stage 1)                  _element: None  / 느리지만 강타
     └─ 화염대검 (Stage 2)          _element: Fire  / 평타 BurnOnHit 자동
```

→ Stage 2 속성 부여는 **화염대검만 Fire**. 다른 둘 (쌍단검 / 성검)은 None + 수치 강화만 → MVP 단순화.

→ 디자이너가 다른 속성 원하면 §결정 미정.

### 2. 무기별 수치 차별화 (기본값)

> 모든 수치는 **검 (Stage 0) 베이스라인 = 100%** 기준.

| 무기 | 데미지 | 공격 속도 | 사거리 (hitboxSize) | 강화 비용 | 비고 |
|---|---|---|---|---|---|
| **검 (Stage 0)** | 10 | 100% (기존) | 1.0 | 0 | 베이스라인 |
| **단검** (Stage 1) | 7 (-30%) | 130% (1.3×) | 0.7 (-30%) | 100 | 빠르고 짧음 |
| **방패검** (Stage 1) | 11 (+10%) | 95% (-5%) | 1.0 (동일) | 100 | 균형, 살짝 느림 |
| **대검** (Stage 1) | 16 (+60%) | 70% (-30%) | 1.4 (+40%) | 100 | 느리지만 강타 |
| **쌍단검** (Stage 2) | 8 (-20%) | 160% (1.6×) | 0.7 | 300 | 단검 ++ |
| **성검** (Stage 2) | 14 (+40%) | 95% | 1.2 (+20%) | 300 | 방패검 균형 강화 |
| **화염대검** (Stage 2) | 18 (+80%) | 70% | 1.4 | 300 | 대검 + Fire 속성 |

**구현**:
- `BaseDamage` 직접 입력
- 공속 = `AttackStepData.startupDuration` / `recoveryDuration` 비율 조정 (Stage 0 의 % 배율)
- 사거리 = `AttackStepData.hitboxSize` 의 x 비례

### 3. 콤보 패턴 — 모두 3타 콤보 (검 트리 일관성)

**Stage 0 검**: 기존 Sword_Default 의 3타 그대로.

**Stage 1/2**: 베이스 = Sword_Default 의 Steps 복사 후 수치만 조정.

```
1타: damageMultiplier 1.0
2타: damageMultiplier 1.2
3타: damageMultiplier 1.5  (마무리)
```

→ 모든 검 트리 무기가 동일 패턴. 차별화는 BaseDamage / startup / hitboxSize 만.

**예외 — 쌍단검**: 4타 콤보 가능?
- (a) 3타 유지 (단순)
- (b) **4타** (이중 검 컨셉) ⭐
- 본 plan: (b) — 디자이너 선택 시 4타 가능. MVP 는 3타 (시간 절약).

### 4. 시각 — Sword_Default frames 재활용 (placeholder)

본 CL 모든 무기:
- `slashFrames`: Sword_Default 의 frames 그대로
- `slashTint`: 무기별 색 차별화
  - 단검: 흰색
  - 방패검: 파란빛 (#A0C4FF)
  - 대검: 회색 (#888888)
  - 쌍단검: 청록 (#80FFD0)
  - 성검: 금색 (#FFD700)
  - 화염대검: 빨간색 (#FF4040) — Fire 속성 시각

→ 정식 sprite per weapon 별도 ticket. 본 CL 은 tint 만으로 구분.

### 5. 방패검의 "방패" 메커니즘 — 본 CL 미포함

방패검 = 블록/패링 능력 추가? → **본 CL 미포함**, 별도 ticket. 본 CL 은 수치만 (균형 데미지).

→ 방패검을 "균형형" 으로 정의 (단검/대검 사이의 중간 수치).

### 6. SO 파일 명명 규칙

```
Assets/_Project/ScriptableObjects/Weapons/
  Sword_Default.asset           (기존, Stage 0 = 검)
  Sword_Dagger.asset            (Stage 1, 단검)
  Sword_ShieldSword.asset       (Stage 1, 방패검)
  Sword_Greatsword.asset        (Stage 1, 대검)
  Sword_TwinDagger.asset        (Stage 2, 쌍단검)
  Sword_HolySword.asset         (Stage 2, 성검)
  Sword_FireGreatsword.asset    (Stage 2, 화염대검)
```

→ 접두사 `Sword_` 통일. CL-158 (활) 은 `Bow_`, CL-159 (봉) 은 `Polearm_`, CL-160 (스태프) 은 `Staff_`.

### 7. _upgrades 트리 입력

```
Sword_Default._upgrades = [Sword_Dagger, Sword_ShieldSword, Sword_Greatsword]
Sword_Dagger._upgrades = [Sword_TwinDagger]
Sword_ShieldSword._upgrades = [Sword_HolySword]
Sword_Greatsword._upgrades = [Sword_FireGreatsword]

(Stage 2 모두 _upgrades = [])
```

→ 각 SO Inspector 에서 직접 드래그 입력.

### 8. 강화 비용 (CL-156 의 _upgradeCost)

- Stage 1 진입: **100 골드** (기본)
- Stage 2 진입: **300 골드** (기본)

→ CL-156 의 디폴트 정책 따름. 수치 조정 별도 ticket.

### 9. PlayerWeaponLoadout 시작 무기 — Sword_Default

CL-156 의 디폴트와 일치. Inspector 에서 `_currentWeapon = Sword_Default` 할당.

### 10. 화염대검의 Fire 속성 magnitude

CL-155 의 `_elementMagnitude` 디폴트 0.10 (Burn 1티어 수준).

**옵션**:
- (a) 디폴트 0.10
- (b) 무기 강화 단계라 0.20 (Stage 2 자체가 강화)

**채택: (a)**. 빌드 인챈트 (Fire 5세트) 와 비교했을 때 0.10 이 적절. Stage 2 의 강화 가치는 데미지 +80% 가 이미 큼.

---

## 핵심 파일

### 신규 (SO 6개)

| 경로 | 내용 |
|---|---|
| `Sword_Dagger.asset` | 단검 (Stage 1, None, 빠름) |
| `Sword_ShieldSword.asset` | 방패검 (Stage 1, None, 균형) |
| `Sword_Greatsword.asset` | 대검 (Stage 1, None, 느린 강타) |
| `Sword_TwinDagger.asset` | 쌍단검 (Stage 2, None) |
| `Sword_HolySword.asset` | 성검 (Stage 2, None) |
| `Sword_FireGreatsword.asset` | 화염대검 (Stage 2, Fire) |

### 수정

| 경로 | 변경 |
|---|---|
| `Sword_Default.asset` (기존) | `_stage = 0`, `_upgrades = [Dagger, ShieldSword, Greatsword]`, `_upgradeCost = 0` |
| `PlayerWeaponLoadout` (CL-156) | `_currentWeapon = Sword_Default` 확정 (Inspector) |

### 신규 문서

| 경로 | 내용 |
|---|---|
| `docs/khi/cl157_sword_tree_balance.md` | 7개 무기 수치 표 + 차별화 가이드 (CL-161 QA 인풋) |

---

## 구현 단계

### 1단계: Sword_Default 트리 입력 (15분)

기존 SO 의 `_stage` / `_upgrades` / `_upgradeCost` 입력.
- _stage = 0
- _upgrades = (3개 — Stage 1 SO 생성 후 드래그)
- _upgradeCost = 0

### 2단계: Stage 1 SO 3개 생성 (1시간 30분, 각 30분)

**Sword_Dagger** (단검):
1. 우클릭 → Create > LostMemory > Combat > WeaponData
2. _displayName = "단검"
3. _baseDamage = 7
4. _stage = 1
5. _upgradeCost = 100
6. _element = None
7. Steps:
   - 1타: damageMultiplier 1.0, startupDuration 0.04, recoveryDuration 0.06, hitboxSize (0.7, 0.5)
   - 2타: damageMultiplier 1.2, startupDuration 0.04, recoveryDuration 0.06, hitboxSize (0.7, 0.5)
   - 3타: damageMultiplier 1.5, startupDuration 0.04, recoveryDuration 0.10, hitboxSize (0.7, 0.5)
8. slashFrames = Sword_Default 의 frames 복사
9. slashTint = Color.white

**Sword_ShieldSword** (방패검):
- _baseDamage = 11, startupDuration 0.06, hitboxSize (1.0, 0.5)
- slashTint = #A0C4FF
- 나머지 검과 동일

**Sword_Greatsword** (대검):
- _baseDamage = 16, startupDuration 0.10, recoveryDuration 0.15, hitboxSize (1.4, 0.7)
- slashTint = #888888

### 3단계: Stage 2 SO 3개 생성 (1시간 30분)

**Sword_TwinDagger**:
- _baseDamage = 8
- _stage = 2
- _upgradeCost = 300
- startupDuration 0.03 (Dagger 보다 +) 
- (옵션 4타: comboStep 4 추가, damageMultiplier 1.0)
- slashTint = #80FFD0

**Sword_HolySword**:
- _baseDamage = 14
- _stage = 2
- _upgradeCost = 300
- hitboxSize (1.2, 0.5)
- slashTint = #FFD700

**Sword_FireGreatsword**:
- _baseDamage = 18
- _stage = 2
- _upgradeCost = 300
- _element = Fire
- _elementMagnitude = 0.10
- startupDuration 0.10, hitboxSize (1.4, 0.7) — 대검 동일
- slashTint = #FF4040

### 4단계: _upgrades 트리 입력 (15분)

각 SO 의 _upgrades 드래그:
- Sword_Default → [Dagger, ShieldSword, Greatsword]
- Sword_Dagger → [TwinDagger]
- Sword_ShieldSword → [HolySword]
- Sword_Greatsword → [FireGreatsword]

### 5단계: PlayerWeaponLoadout 시작 무기 확정 (5분)

플레이어 prefab 의 PlayerWeaponLoadout._currentWeapon 슬롯에 Sword_Default 드래그.

### 6단계: 수치/차별화 가이드 문서 (20분)

`cl157_sword_tree_balance.md` — §2 표 그대로 + 비고. CL-161 QA 인풋.

### 7단계: 검증 (1시간 30분)

```
시나리오 1: 시작 무기 = 검
- Play 시작 → KhiMeleeComboController.WeaponData == Sword_Default 확인
- 평타 3타 콤보 정상

시나리오 2: 검 → 단검 강화
- 디버그 골드 100+ 보장
- 상점 진입 → 강화 슬롯 1개 (Stage 1 후보 중 1개)
- 단검이 슬롯에 등장하면 구매
- 강화 후 _currentWeapon = Sword_Dagger
- 평타 → 데미지 7 / 빠른 속도 / 짧은 hitbox 확인

시나리오 3: 단검 → 쌍단검
- 강화 후 상점에 쌍단검 슬롯 등장
- 300 골드로 강화

시나리오 4: 검 → 방패검 → 성검
- 균형형 흐름 검증
- 성검 데미지 14 확인

시나리오 5: 검 → 대검 → 화염대검
- 화염대검 강화 후 평타 시 BurnOnHit 발동 (CL-155 hook)
- 적 HP 도트 감소 확인
- slashTint 빨강 확인

시나리오 6: 트리 분기 잘못
- 단검 보유 시 상점에 "방패검" 등장 X (트리 따라서)

시나리오 7: 최종 강화 후
- 쌍단검/성검/화염대검 보유 시 상점에 강화 슬롯 X (모두 _upgrades = [])

시나리오 8: 시각
- 무기별 slashTint 확인 (런타임 sprite 색상)

시나리오 9: 콤보 데이터
- 각 무기 1/2/3타 모두 정상 발동
- 3타 마무리 데미지 1.5×

시나리오 10: BaseDamage 라이브 튠
- Play 중 Sword_Dagger._baseDamage 슬라이더 변경 → 다음 평타 즉시 반영 (CL-090 흐름 유지)
```

---

## 위험 / 결정 미정

### 위험

1. **Sword_Default frames 재활용 시각 단조로움**: 7개 무기가 모두 같은 sprite + tint 차이만 → 시각 차별 약함. → 정식 per-weapon sprite 별도 ticket 명시.
2. **방패검의 "방패" 부재 혼란**: 이름은 방패검인데 메커니즘 X. → 디스플레이명 = "방패검", 문서에 "MVP 는 균형형" 명시. 별도 ticket 에서 블록 추가.
3. **쌍단검 4타 콤보 vs 3타**: KhiMeleeComboController 가 3타 가정 코드 가능. 4타 추가 시 input/animator 분기 필요. → MVP 3타 유지.
4. **CL-156 인프라 미완 시 검증 불가**: 강화 슬롯 / 상점 통합이 CL-156 에 의존. CL-156 완료 후 본 CL 시작.
5. **수치 차별화가 직관적이지 않을 가능성**: 대검 -30% 속도 가 너무 느려서 안 쓰일 가능. CL-161 QA 에서 조정.
6. **화염대검 + 빌드 Fire 5세트 OP**: BurnOnHit 두 번 발동 (각각, CL-155 MVP 정책). 합산 안 함. → CL-155 위험 명시.
7. **_upgrades 순환 참조**: SO 가 자기 자신 또는 부모를 가리키면 무한. CL-156 OnValidate 가 검사하면 안전.
8. **시작 무기 변경 시 디폴트 깨짐**: PlayerWeaponLoadout._currentWeapon 미할당 시 NRE. Awake 검증 권장.

### 결정 미정

- [ ] Stage 2 의 화염대검 외 속성 부여 — 본 plan: **화염대검만 Fire, 나머지 2개 None**
- [ ] 쌍단검 4타 — 본 plan: **MVP 3타** (4타는 별도)
- [ ] 방패검 블록 메커니즘 — 본 plan: **별도 ticket**
- [ ] 정식 sprite — 본 plan: **placeholder tint, 별도 ticket**
- [ ] 수치 디폴트 — 본 plan: **§2 표 그대로** (CL-161 QA 후 조정)
- [ ] 화염대검 _elementMagnitude — 본 plan: **0.10 (CL-155 디폴트)**
- [ ] Stage 2 후보 추가 (트리 확장) — 본 plan: **각 Stage 1 당 1개 만** (단순)

---

## 후속 ticket 영향

| Ticket | CL-157 과의 관계 |
|---|---|
| **CL-158 (활 트리)** | 동일 패턴, 7개 SO + _upgrades 트리. 본 CL 패턴 그대로 |
| **CL-159 (봉 트리)** | 동일 |
| **CL-160 (스태프 트리)** | 동일 + 스킬 2종 추가 |
| **CL-161 (무기 QA)** | 본 CL 의 7개 무기 밸런스 검증 |
| **별도 ticket: 정식 sprite per weapon** | placeholder tint → 정식 sprite |
| **별도 ticket: 방패검 블록/패링** | 블록 메커니즘 신설 |
| **별도 ticket: 4타 콤보 (쌍단검)** | KhiMeleeComboController 4타 지원 |
| **별도 ticket: Stage 2 트리 확장** | 각 Stage 1 당 2~3개 Stage 2 분기 |

---

## 예상 시간

| 단계 | 시간 |
|---|---|
| 1단계 (Sword_Default 트리 입력) | 15분 |
| 2단계 (Stage 1 SO 3개) | 1시간 30분 |
| 3단계 (Stage 2 SO 3개) | 1시간 30분 |
| 4단계 (_upgrades 트리 입력) | 15분 |
| 5단계 (시작 무기 확정) | 5분 |
| 6단계 (수치 가이드 문서) | 20분 |
| 7단계 (검증 10 시나리오) | 1시간 30분 |
| **합계** | **약 5시간 25분** |

→ 5점 ticket 에 부합 (콘텐츠 입력 비중).

---

## 결정 요청

| # | 질문 | 옵션 | 추천 |
|---|---|---|---|
| 1 | Stage 2 속성 | 화염대검만 Fire / 3개 모두 (성검=Light, 쌍단검=Lightning) / None만 | **화염대검만 Fire** |
| 2 | 쌍단검 콤보 | **3타** / 4타 | **3타** (MVP) |
| 3 | 방패검 블록 | 본 CL / **별도** | **별도** |
| 4 | sprite | placeholder tint / **정식 별도** | **placeholder** |
| 5 | 수치 표 §2 | 그대로 / 조정 | **그대로 + CL-161 후 조정** |
| 6 | Stage 2 분기 수 | **각 1개 (총 3)** / 각 2개 (총 6) | **각 1개** (단순) |
| 7 | 화염대검 magnitude | **0.10** / 0.20 | **0.10** |

전부 추천대로면 **Fire하나 + 3타 + 블록별도 + tint + §2그대로 + 1개 + 0.10**.

---

## Epic K 진행률 (CL-157 후)

| Ticket | Plan |
|---|---|
| CL-090 무기 SO | ✅ |
| CL-155 4속성 + 결합 | ✅ |
| CL-156 단계 강화 | ✅ |
| **CL-157 검 트리** | ✅ ← 방금 |
| CL-158 활 트리 | ⏳ |
| CL-159 봉 트리 | ⏳ |
| CL-160 스태프 트리 | ⏳ |
| CL-161 무기 QA | ⏳ |

**Epic K: 3/7** (인프라 ✅ + 콘텐츠 1/4)

---

## 다음 plan

| 옵션 | Ticket | 점수 | 비고 |
|---|---|---|---|
| **A** | CL-158 활 트리 | 5점 | 본 CL 패턴 반복 + 원거리 신규 |
| B | CL-159 봉 트리 | 5점 | 본 CL 패턴 반복 |
| C | CL-160 스태프 트리 | 5점 | + 스킬 2종 (가장 무거움) |
| D | Epic U 진입 (CL-162) | - | 디자이너 도구 (병렬) |

**추천: A (CL-158 활 트리)** — 검 다음 가장 정체성 큰 무기. 원거리 = ProjectileWeapon 처리 신규 (TopDown Engine 활용 가능). 봉/스태프는 그 후.

뭐로 갈까요?
