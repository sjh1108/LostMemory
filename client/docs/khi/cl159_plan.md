# CL-159 — 봉 트리 구현 (여의봉 / 창 / 망치 + Stage 2 강화)

## Context

Epic K 세 번째 콘텐츠 ticket. **CL-157 검 트리 패턴 반복** (모두 근접 무기).

5점 P2, 클라1, CL-156 의존.

### 검 vs 봉 — 핵심 차이 (작음)

| 항목 | 검 (CL-157) | 봉 (본 CL) |
|---|---|---|
| 공격 방식 | Melee 평타 | **Melee 평타 (동일)** |
| 컨트롤러 | KhiMeleeComboController | **재활용 (변경 X)** |
| 데이터 | WeaponData.Steps[] | **재활용 (수치만 차별)** |
| 차별화 | 데미지/속도/가로 hitbox | **데미지/속도/긴 hitbox (찌르기) + 넉백** |

→ **인프라 작업 거의 없음**. 콘텐츠 (SO 7개 + 수치) 위주.
→ CL-157 보다 단순 (활 인프라 없이 검 패턴 그대로).

### 기존 상태 (확인 완료)

- ✅ `WeaponData` (CL-090/158) — Melee 가정 동작 + AttackKind enum 분기 (CL-158 추가)
- ✅ `KhiMeleeComboController` — 검과 동일하게 사용
- ✅ CL-155/156/157 인프라 모두 적용
- ❌ 봉 트리 7개 SO — **본 CL 신설**
- ⏳ **넉백 메커니즘** — 본 CL 신설 (망치용)

### 본 CL 책임 범위

1. **봉 트리 7개 SO** (Stage 0 ×1 + Stage 1 ×3 + Stage 2 ×3)
2. 각 무기 콤보 데이터 (Steps, hitbox, 수치)
3. _upgrades 트리 입력
4. _element 설정 (Stage 2 일부)
5. **넉백 메커니즘 신설** (망치/대망치 전용 — 본 CL 첫 도입)
6. 검증

→ **활 인프라 없음**. 모두 Melee.

---

## 결정사항

### 1. 봉 트리 구조 (확정)

```
봉 (Polearm_Default, Stage 0)              _element: None     / 균형 폴암
 ├─ 여의봉 (Stage 1)                       _element: None     / 긴 사거리 + 빠름
 │   └─ 폭풍여의봉 (Stage 2)               _element: Wind     / 광역 회오리 + 사거리
 ├─ 창 (Stage 1)                           _element: None     / 찌르기 (직선 좁음)
 │   └─ 삼지창 (Stage 2)                   _element: None     / 다중 찌르기
 └─ 망치 (Stage 1)                         _element: None     / 강타 + 넉백
     └─ 천둥망치 (Stage 2)                 _element: Lightning/ 강타 + Chain
```

→ Stage 2 속성 부여:
- 폭풍여의봉 = **Wind** (회의록의 "바람 광역 회오리" 시각 컨셉)
- 천둥망치 = **Lightning** (회의록 "전기 체인" 컨셉)
- 삼지창은 None (다중 찌르기 자체가 강화)

### 2. 무기별 수치 차별화 (디폴트)

> 모든 수치는 **봉 (Stage 0) = 100% 베이스라인**.

| 무기 | Stage | Element | BaseDmg | 공격속도 | hitboxSize | 넉백 | UpgradeCost |
|---|---|---|---|---|---|---|---|
| **봉** | 0 | None | 12 | 100% | (1.2, 0.4) | 0 | 0 |
| **여의봉** | 1 | None | 10 (-17%) | 110% | (1.6, 0.4) | 0 | 100 |
| **창** | 1 | None | 14 (+17%) | 90% | (1.8, 0.3) | 0 | 100 |
| **망치** | 1 | None | 20 (+67%) | 60% | (1.2, 0.7) | 3 | 100 |
| **폭풍여의봉** | 2 | Wind | 12 (+20%) | 110% | (1.8, 0.6) | 0 | 300 |
| **삼지창** | 2 | None | 14 | 100% | (1.8, 0.4) ×3 | 0 | 300 |
| **천둥망치** | 2 | Lightning | 22 (+10%) | 60% | (1.4, 0.8) | 4 | 300 |

**차별 포인트**:
- 봉 = 검과 비슷하지만 살짝 더 긴 hitbox (가로 1.2 vs 검 1.0)
- 여의봉 = 가장 긴 사거리 (가로 1.6, +33% vs 봉)
- 창 = 가장 좁은 hitbox (세로 0.3, 직선 찌르기 느낌)
- 망치 = 큰 hitbox + 넉백 + 강 데미지 + 느림
- Stage 2 모두 강화

### 3. 넉백 메커니즘 — 신규 도입

**옵션**:
- (a) **AttackStepData 에 knockbackForce 필드 추가** ⭐
- (b) WeaponData 무기 단위
- (c) 별도 KnockbackOnHit 컴포넌트

**채택: (a)**.
- step 단위 (1타/2타/3타 마무리만 큰 넉백 가능)
- 데이터 주도 (BuildSetData 변경처럼 Inspector 입력)
- KhiMeleeHitbox 가 적중 시 적의 Rigidbody2D.AddForce 호출

```csharp
// AttackStepData.cs 추가
[Header("Knockback (CL-159)")]
[Tooltip("적 적중 시 가하는 넉백 힘. 0 이면 넉백 X. 망치/대망치 전용.")]
[Min(0f)] public float knockbackForce;
```

**KhiMeleeHitbox 적중 처리**:
```csharp
// 기존 적중 시
if (step.knockbackForce > 0)
{
    var rb = target.GetComponent<Rigidbody2D>();
    if (rb != null)
    {
        Vector2 dir = (target.transform.position - transform.position).normalized;
        rb.AddForce(dir * step.knockbackForce, ForceMode2D.Impulse);
    }
}
```

→ 적이 Rigidbody2D 가지고 있어야 작동. TDE Character 기본 가짐.

### 4. 콤보 패턴 — 검과 동일 3타

모든 봉 무기:
- 1타: damageMultiplier 1.0
- 2타: damageMultiplier 1.2
- 3타: damageMultiplier 1.5 (마무리)

망치류는 **3타 마무리에만 큰 넉백** (1/2타는 작은 넉백):
- 망치 1/2타: knockbackForce = 2
- 망치 3타: knockbackForce = 5
- 천둥망치 1/2타: 3
- 천둥망치 3타: 7

여의봉/창/삼지창/봉: knockbackForce = 0 (모든 step).

### 5. 삼지창의 "다중 찌르기" 구현

**옵션**:
- (a) hitbox 3번 동시 발동 (3개 좌표) — 신규 메커니즘
- (b) **hitbox 1개로 표현** (긴 가로) — 단순화 ⭐
- (c) 콤보 step 추가 (3타 → 6타?)

**채택: (b)**.
- hitboxSize (1.8, 0.4) 그대로 + 데미지 1.2× 추가 (다중 찌르기 보상)
- 시각적으로 "다중 찌르기" 효과는 별도 sprite/파티클 (별도 ticket)
- (a) 의 다중 hitbox 는 메커니즘 신설 부담 → 후속

→ MVP 는 단순 강화 hitbox. "다중 찌르기" 는 시각적 표현만.

### 6. 폭풍여의봉의 Wind 속성

CL-155 의 `WeaponElement.Wind` → `WindAOE` OnHit 트리거.
- 평타 적중 시 광역 회오리 발동 (반경 2 유닛, 데미지 = elementMagnitude × playerAtk)
- _elementMagnitude = 0.10

→ 여의봉 자체 사거리 + 광역 회오리 → 광역 무기 정체성.

### 7. 천둥망치의 Lightning 속성

`WeaponElement.Lightning` → `ChainOnHit` 트리거.
- 평타 적중 시 인접 적 1마리 추가 데미지 (체인)
- _elementMagnitude = 0.10

→ 망치 강타 + 체인 = 다수 적 군집 격멸.

### 8. SO 파일 명명

```
Assets/_Project/ScriptableObjects/Weapons/
  Polearm_Default.asset           (Stage 0, 봉)
  Polearm_RuyiBang.asset          (Stage 1, 여의봉)
  Polearm_Spear.asset             (Stage 1, 창)
  Polearm_Hammer.asset            (Stage 1, 망치)
  Polearm_StormRuyiBang.asset     (Stage 2, 폭풍여의봉)
  Polearm_Trident.asset           (Stage 2, 삼지창)
  Polearm_ThunderHammer.asset     (Stage 2, 천둥망치)
```

### 9. _upgrades 트리

```
Polearm_Default → [RuyiBang, Spear, Hammer]
Polearm_RuyiBang → [StormRuyiBang]
Polearm_Spear → [Trident]
Polearm_Hammer → [ThunderHammer]
```

### 10. 시각 — Sword_Default frames 재활용 (placeholder)

검 트리와 동일 정책:
- slashFrames = Sword_Default 재활용
- slashTint 로 무기 차별화:
  - 봉: 갈색 (#A0734C)
  - 여의봉: 금색 (#FFD27D)
  - 창: 은색 (#C0C0C0)
  - 망치: 회색-갈색 (#7A5230)
  - 폭풍여의봉: 청록 (#80FFC0) — Wind 시각
  - 삼지창: 청회색 (#5A8FA0)
  - 천둥망치: 노란색 (#FFEC40) — Lightning 시각

→ 정식 sprite 별도 ticket.

---

## 핵심 파일

### 신규 (SO 7개)

| 경로 | 내용 |
|---|---|
| `Polearm_Default.asset` | 봉 (Stage 0) |
| `Polearm_RuyiBang.asset` | 여의봉 (Stage 1) |
| `Polearm_Spear.asset` | 창 (Stage 1) |
| `Polearm_Hammer.asset` | 망치 (Stage 1) |
| `Polearm_StormRuyiBang.asset` | 폭풍여의봉 (Stage 2, Wind) |
| `Polearm_Trident.asset` | 삼지창 (Stage 2) |
| `Polearm_ThunderHammer.asset` | 천둥망치 (Stage 2, Lightning) |

### 신규 문서

| 경로 | 내용 |
|---|---|
| `docs/khi/cl159_polearm_tree_balance.md` | 수치 표 + 차별화 가이드 |

### 수정

| 경로 | 변경 |
|---|---|
| `AttackStepData.cs` | `knockbackForce` 필드 추가 (1줄) |
| `KhiMeleeHitbox.cs` | 적중 처리에 넉백 적용 (10줄) |

---

## 구현 단계

### 1단계: AttackStepData.knockbackForce + KhiMeleeHitbox 적용 (45분)

§3 코드:
1. `AttackStepData.knockbackForce` 필드 추가
2. `KhiMeleeHitbox` 적중 처리에 Rigidbody2D.AddForce 추가
3. 기존 검 트리 SO 들의 step.knockbackForce = 0 (디폴트, 영향 X)

### 2단계: Stage 0 + Stage 1 SO 4개 (1시간 30분)

**Polearm_Default** (봉):
- _baseDamage 12, hitboxSize (1.2, 0.4), startupDuration 0.06
- _stage = 0, _upgradeCost = 0
- _attackKind = Melee
- slashTint = #A0734C
- Steps 1/2/3타 (검 패턴 그대로 + size 조정)

**Polearm_RuyiBang** (여의봉):
- _baseDamage 10, hitboxSize (1.6, 0.4), startupDuration 0.05
- slashTint = #FFD27D
- _upgrades = [StormRuyiBang]

**Polearm_Spear** (창):
- _baseDamage 14, hitboxSize (1.8, 0.3), startupDuration 0.07
- slashTint = #C0C0C0

**Polearm_Hammer** (망치):
- _baseDamage 20, hitboxSize (1.2, 0.7), startupDuration 0.10, recoveryDuration 0.15
- knockbackForce: 1/2타 = 2, 3타 = 5
- slashTint = #7A5230

### 3단계: Stage 2 SO 3개 (1시간 30분)

**Polearm_StormRuyiBang** (폭풍여의봉):
- _baseDamage 12, hitboxSize (1.8, 0.6)
- _stage = 2, _upgradeCost = 300
- _element = Wind, _elementMagnitude = 0.10
- slashTint = #80FFC0

**Polearm_Trident** (삼지창):
- _baseDamage 14 (베이스 동일), damageMultiplier per step + 0.2 (다중 찌르기 보너스)
  - 1타 1.2, 2타 1.4, 3타 1.7
- hitboxSize (1.8, 0.4)
- slashTint = #5A8FA0

**Polearm_ThunderHammer** (천둥망치):
- _baseDamage 22, hitboxSize (1.4, 0.8)
- knockbackForce: 1/2타 = 3, 3타 = 7
- _element = Lightning, _elementMagnitude = 0.10
- slashTint = #FFEC40

### 4단계: _upgrades 트리 입력 (15분)

§9 입력. Inspector 드래그.

### 5단계: 수치 가이드 문서 (20분)

`cl159_polearm_tree_balance.md` — §2 표 + 차별화 가이드 + 넉백 정책.

### 6단계: 검증 (1시간 30분)

```
시나리오 1: 봉 시작 (디버그 swap)
- Polearm_Default 로 swap
- 평타 3타 콤보 정상
- hitbox 가로 1.2 (검보다 살짝 김)

시나리오 2: 봉 → 여의봉
- 강화 후 hitbox 가로 1.6 (긴 사거리)
- 데미지 10
- 빠른 공격속도

시나리오 3: 봉 → 창
- hitbox (1.8, 0.3) — 좁고 김
- 정면 적만 적중 (양옆 적 안 맞음)

시나리오 4: 봉 → 망치
- 데미지 20 (큰)
- 느린 속도
- 적 적중 시 뒤로 밀림 (knockbackForce 시각 확인)
- 1/2타 작은 넉백, 3타 큰 넉백

시나리오 5: 여의봉 → 폭풍여의봉
- Wind 속성 → 적 적중 시 광역 회오리 (WindAOE)
- 광역 데미지 확인

시나리오 6: 창 → 삼지창
- damageMultiplier 1.2/1.4/1.7 → 더 강한 데미지
- hitbox 동일 (1.8, 0.4)

시나리오 7: 망치 → 천둥망치
- 더 큰 데미지 + 큰 넉백 + Lightning Chain (인접 적 추가 데미지)

시나리오 8: 시각
- 무기별 slashTint 확인

시나리오 9: 검 ↔ 봉 swap
- 검 → 봉 swap 후 콤보 정상
- WeaponData.Steps 변경 즉시 반영

시나리오 10: 빌드 인챈트와 결합
- 폭풍여의봉 + 빌드 Wind 5세트 → WindAOE 두 번 발동 (CL-155 MVP 정책)
```

---

## 위험 / 결정 미정

### 위험

1. **넉백 메커니즘 신설 — 적 Rigidbody2D 의존**: 적이 Rigidbody2D 없으면 넉백 X. TDE Character 는 기본 가짐, 커스텀 적은 점검 필요.
2. **창 좁은 hitbox 적중률 낮음**: 세로 0.3 → 좌우 적 안 맞음. 플레이 어색 가능. → CL-161 QA 후 조정.
3. **망치 느린 속도 어색함**: startupDuration 0.10 + recoveryDuration 0.15 → 적 다가오기 전 못 휘두름. 넉백으로 보상하지만 체감 X 가능.
4. **삼지창 단순화 (hitbox 1개)**: 진짜 다중 찌르기 X 라 디자이너 의도와 차이. → 후속 ticket 에서 다중 hitbox 지원.
5. **폭풍여의봉 OP 우려**: Wind 광역 + 긴 사거리 + 빠른 속도 = 모든 면 강함. → CL-161 QA 후 조정.
6. **봉 / 여의봉 차별 약함**: 둘 다 길고 단순. 여의봉은 사거리 +33% 만 차이. → 디자이너 정체성 보강 (별도 ticket).
7. **knockbackForce 디폴트 0 — 검 트리 영향 X**: 기존 검 트리 SO 의 step 들이 자동 0 → 검 동작 그대로. 안전.
8. **Force.Impulse 단위 조정**: 2 ~ 7 의 force 값이 적절한지 실험 필요. Rigidbody2D.mass 따라 다름.

### 결정 미정

- [ ] Stage 2 속성 — 본 plan: **폭풍여의봉=Wind, 천둥망치=Lightning, 삼지창=None**
- [ ] 삼지창 다중 hitbox — 본 plan: **hitbox 1개로 단순화**
- [ ] 넉백 단위 (2~7) — 본 plan: **디폴트 안, CL-161 후 조정**
- [ ] 망치 속도 (0.10 / 0.15) — 본 plan: **느림 유지 + 넉백 보상**
- [ ] Stage 2 분기 수 — 본 plan: **각 1개 (단순)**

---

## 후속 ticket 영향

| Ticket | CL-159 와의 관계 |
|---|---|
| **CL-160 (스태프 트리)** | 활 인프라 (Projectile) 활용 + 스킬 2종. 봉과 무관 |
| **CL-161 (무기 QA)** | 4 트리 모두 합쳐 28개 무기 밸런스 검증 |
| **별도 ticket: 다중 hitbox (삼지창)** | hitbox 3개 동시 |
| **별도 ticket: 넉백 시각 / 사운드** | 망치 시각 폴리시 |
| **별도 ticket: 정식 sprite per weapon** | placeholder tint → 정식 |

---

## 예상 시간

| 단계 | 시간 |
|---|---|
| 1단계 (knockbackForce 메커니즘) | 45분 |
| 2단계 (Stage 0+1 SO 4개) | 1시간 30분 |
| 3단계 (Stage 2 SO 3개) | 1시간 30분 |
| 4단계 (_upgrades 입력) | 15분 |
| 5단계 (수치 가이드 문서) | 20분 |
| 6단계 (검증 10 시나리오) | 1시간 30분 |
| **합계** | **약 5시간 50분** |

→ 5점 ticket 적정. 검 트리 (CL-157) 와 비슷한 규모 + 넉백 메커니즘.

---

## 결정 요청

| # | 질문 | 옵션 | 추천 |
|---|---|---|---|
| 1 | Stage 2 속성 | 본 plan §1 / 모두 None / 다른 조합 | **본 plan** (Wind/Lightning) |
| 2 | 삼지창 다중 hitbox | 본 CL / **별도** | **별도** (단순화) |
| 3 | 넉백 메커니즘 위치 | **AttackStepData** / WeaponData / 별도 컴포넌트 | **AttackStepData** |
| 4 | 망치 속도 | **느림 유지 (0.10/0.15)** / 중간 (0.07/0.10) | **느림** (정체성) |
| 5 | sprite | placeholder tint / 정식 별도 | **placeholder** |
| 6 | Stage 2 분기 수 | **각 1개** / 각 2개 | **각 1개** |

전부 추천대로면 **본plan + 삼지창단순 + AttackStep + 느림 + placeholder + 1개**.

---

## Epic K 진행률 (CL-159 후)

| Ticket | Plan |
|---|---|
| CL-090 무기 SO | ✅ |
| CL-155 4속성 + 결합 | ✅ |
| CL-156 단계 강화 | ✅ |
| CL-157 검 트리 | ✅ |
| CL-158 활 트리 | ✅ |
| **CL-159 봉 트리** | ✅ ← 방금 |
| CL-160 스태프 트리 | ⏳ |
| CL-161 무기 QA | ⏳ |

**Epic K: 5/7**

---

## 다음 plan

| 옵션 | Ticket | 점수 | 비고 |
|---|---|---|---|
| **A** | CL-160 스태프 트리 + 스킬 2종 | 5점 | 활 인프라 활용 + 스킬 신설 (가장 무거움) |
| B | CL-161 무기 QA | 2점 | CL-160 후 일괄 검증 |
| C | Epic U 진입 (CL-162) | - | 디자이너 도구 (병렬) |

**추천: A (CL-160 스태프 트리)** — Epic K 콘텐츠 마무리. 스태프는 마법 = 스킬 2종 추가 필요. 가장 도전적인 트리.

뭐로 갈까요?
