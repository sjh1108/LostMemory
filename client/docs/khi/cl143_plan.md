# CL-143 — 스킬 친화 3세트 효과 (쿨감/불 도트/바람 회오리)

## Context

Epic S Phase 3의 두 번째 ticket. **CL-142의 OnHit 인프라 활용 + 신규 효과 추가**.

스킬 친화 3세트:
- 쿨감 (Cooldown) — 2/4/6스택 → 스킬 쿨다운 -15%/-30%/-50%
- 불 (Burn DOT on-hit) — 1/2/4스택 → 도트 10/2초 / 20/4초 / 30/6초 (1초당 1틱, 중첩 X)
- 바람 (Wind AOE on-hit) — 1/2/4스택 → 광역 효과 (디테일 미정)

**CL-142 의존성**:
- ✅ OnHitEffectRegistry (재사용)
- ✅ EnemyStatusEffect (확장: Burn 추가)

→ **본 CL은 CL-142보다 훨씬 가벼움**. 인프라가 이미 있어서 case 추가 + 새 status 1종 + 바람 디자인.

---

## ⚠️ 결정 미정 (회의록 미해소)

### 바람 효과 정확한 메커니즘

회의록 원문: "바람 효과 +X%" 표현만 있고 구체 메커니즘 없음.

**3가지 옵션**:

#### 옵션 A: 광역 회오리 데미지 (추천) ⭐
- 적 맞췄을 때 victim 주변에 광역 데미지
- 반경 2 유닛 / 데미지 = 본인 공격력 × magnitude
- Vampire Survivors의 'Garlic' 같은 느낌
- 구현 단순, 검증 쉬움

#### 옵션 B: 넉백
- 적 맞췄을 때 victim을 살짝 밀어냄
- magnitude = 넉백 강도
- 단점: 효과가 미묘함 (시각적 임팩트 약함)

#### 옵션 C: 이속 가속 (Player)
- 평타 시 일시 이속 +%
- magnitude = 이속 증가량
- 단점: 평타 친화 (얼음/전기) 와 구분 모호

**채택 (Plan)**: **옵션 A** (광역 회오리 데미지). 사용자 검토 필요.

### 쿨감(Cooldown) 적용 대상

현재 게임에 **일반 스킬 시스템 없음**. 기존 쿨다운:
- DashCooldown (StatId.DashCooldown) — 이미 존재
- 스태프 스킬 (CL-160 미구현)

**3가지 옵션**:

#### 옵션 A: 별도 StatId.SkillCooldown 신설 (추천) ⭐
- 향후 스킬 시스템에 적용
- 본 CL에서는 enum + StatModifier hook만, 실제 효과는 후속 ticket에서 (CL-160 스태프 스킬)
- TODO 주석 명시

#### 옵션 B: DashCooldown에도 적용
- 쿨감 빌드 = 대시 쿨다운도 줄어듦
- 단점: 의도 안 맞음 (회의록은 "스킬 쿨다운")

#### 옵션 C: TODO만, 효과 없음
- 본 CL에서 적용 안 함
- 단점: 빌드 발동되지만 체감 효과 0 → 디버깅 헷갈림

**채택**: **옵션 A**. SkillCooldown StatId 추가 + LogWarning ("아직 적용할 스킬 없음, CL-160 후 적용").

---

## 결정사항

### 1. 본 CL 범위

| 작업 | 비고 |
|---|---|
| BurnOnHit 핸들러 추가 (OnHitEffectRegistry) | CL-142 인프라 활용 |
| EnemyStatusEffect.ApplyBurn 추가 | DOT 1틱/초, 중첩 없음 |
| WindAOE 핸들러 추가 (옵션 A 채택) | 신규 광역 데미지 |
| StatId.SkillCooldown enum 추가 | StatModifier hook만, 적용은 후속 |
| BuildSetData 3개 작성 (쿨감/불/바람) | 회의록 수치 그대로 |

### 2. Burn 도트 메커니즘

회의록:
- 티어 1: 데미지 10, 지속 2초
- 티어 2: 데미지 20, 지속 4초
- 티어 3: 데미지 30, 지속 6초
- 1초당 1틱
- 중첩 없음 (가장 최근 화상으로 갱신)

**구현**:
```csharp
// EnemyStatusEffect 확장
private float _burnEndsAt;
private float _burnDamagePerTick;
private float _nextBurnTickAt;

public void ApplyBurn(float damagePerTick, float duration)
{
    // 중첩 없음 = 갱신
    _burnDamagePerTick = damagePerTick;
    _burnEndsAt = Time.time + duration;
    _nextBurnTickAt = Time.time + 1f;
}

private void Update()
{
    if (Time.time >= _nextBurnTickAt && Time.time < _burnEndsAt)
    {
        _nextBurnTickAt = Time.time + 1f;
        _health.Damage(_burnDamagePerTick, ...);
    }
    // 슬로우/빙결 만료 처리도 여기서
}
```

**Magnitude 의미**: BuildSetData.SetTier.Magnitude = 데미지 (10/20/30), SetTier.Duration = 지속 (2/4/6).
→ EffectEntry에 Duration 필드 활용.

### 3. WindAOE 메커니즘 (옵션 A 채택)

**구현**:
- victim 주변 2 유닛 광역 검색
- victim 본인 제외 (이중 데미지 방지)
- 데미지 = 본인 공격력 × magnitude (10%/20%/30%)
- 즉시 적용 (지연 없음)

```csharp
// OnHitEffectRegistry.HandleHit case
case RelicEffectType.WindAOE:
    ApplyWindAOE(victim, effect.Magnitude);
    break;

private void ApplyWindAOE(Health victim, float magnitude)
{
    float radius = 2f;
    float playerAtk = combat.WeaponData.BaseDamage * statContainer.GetTotalMultiplier(StatId.AttackPower);
    float aoeDamage = playerAtk * magnitude;

    var hits = Physics2D.OverlapCircleAll(victim.transform.position, radius);
    foreach (var hit in hits)
    {
        var h = hit.GetComponent<Health>();
        if (h == null || h == victim) continue;
        h.Damage(aoeDamage, ...);
    }
}
```

**시각**: 회오리 이펙트 표시 (placeholder VFX, 후속 ticket에서 정식 VFX)

### 4. CooldownReductionPercent → SkillCooldown StatId

**StatId 추가**:
```csharp
public enum StatId
{
    // ... 기존
    SkillCooldown,    // CL-143 추가
}
```

**SetEffectApplicator 라우팅**:
```csharp
case RelicEffectType.CooldownReductionPercent:
    statContainer.AddPermanent(StatId.SkillCooldown, tier.Magnitude, source);
    Debug.LogWarning("[CL-143] SkillCooldown 적용됨, but no skill system yet (CL-160)");
    break;
```

**적용 hook**: 본 CL에서는 추가 안 함. CL-160 (스태프 스킬) 작업 시 스킬 발동 코드에서 `GetTotalMultiplier(StatId.SkillCooldown)` 조회하여 쿨다운 감소.

### 5. BuildSetData 작성

#### BuildSet_쿨감.asset
| 티어 | RequiredCount | EffectType | Magnitude |
|---|---|---|---|
| 1 | 2 | CooldownReductionPercent | 0.15 |
| 2 | 4 | CooldownReductionPercent | 0.30 |
| 3 | 6 | CooldownReductionPercent | 0.50 |

#### BuildSet_불.asset
| 티어 | RequiredCount | EffectType | Magnitude | Duration |
|---|---|---|---|---|
| 1 | 1 | BurnOnHit | 10 | 2 |
| 2 | 2 | BurnOnHit | 20 | 4 |
| 3 | 4 | BurnOnHit | 30 | 6 |

#### BuildSet_바람.asset
| 티어 | RequiredCount | EffectType | Magnitude |
|---|---|---|---|
| 1 | 1 | WindAOE | 0.10 |
| 2 | 2 | WindAOE | 0.20 |
| 3 | 4 | WindAOE | 0.30 |

---

## 핵심 파일

### 신규
없음 (CL-142 인프라 재사용)

### 수정

| 경로 | 변경 내용 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Combat/StatId.cs` | `SkillCooldown` enum 추가 |
| `Assets/_Project/Scripts/Runtime/Combat/OnHitEffectRegistry.cs` (CL-142) | `BurnOnHit`, `WindAOE` case 추가 |
| `Assets/_Project/Scripts/Runtime/Enemy/EnemyStatusEffect.cs` (CL-142) | `ApplyBurn(damage, duration)` 메서드 추가 |
| `Assets/_Project/Scripts/Runtime/Relics/SetEffectApplicator.cs` (CL-140) | `CooldownReductionPercent`, `BurnOnHit`, `WindAOE` case 채움 |

### 신규 SO

| 경로 | 내용 |
|---|---|
| `Assets/_Project/ScriptableObjects/BuildSets/BuildSet_쿨감.asset` | 위 표 |
| `Assets/_Project/ScriptableObjects/BuildSets/BuildSet_불.asset` | 위 표 |
| `Assets/_Project/ScriptableObjects/BuildSets/BuildSet_바람.asset` | 위 표 |

---

## 구현 단계

### 1단계: SkillCooldown StatId 추가 + 라우팅 (15분)

1. `StatId.cs` — `SkillCooldown` 추가
2. `SetEffectApplicator.cs` — `CooldownReductionPercent` case 채움 (`AddPermanent(StatId.SkillCooldown, ...)`)
3. LogWarning 추가 ("아직 적용할 스킬 없음, CL-160 후")

### 2단계: BurnOnHit 통합 (45분)

1. `EnemyStatusEffect.cs` — `ApplyBurn(damage, duration)` 메서드 추가:
   - 1초당 1틱 데미지
   - 중첩 없음 (갱신)
   - 만료 시 자동 정지
2. `OnHitEffectRegistry.HandleHit` — BurnOnHit case 추가:
   - victim의 EnemyStatusEffect.ApplyBurn 호출
3. `SetEffectApplicator` — BurnOnHit case 채움:
   - `onHitRegistry.Register(BurnOnHit, magnitude, duration, source)`
   - **OnHitEffectRegistry의 Register API에 duration 파라미터 추가 필요**

### 3단계: WindAOE 통합 (1시간)

1. `OnHitEffectRegistry.HandleHit` — WindAOE case 추가:
   - 위 §3 의사코드대로 광역 검색 + 데미지
2. `SetEffectApplicator` — WindAOE case 채움
3. Placeholder VFX:
   - victim 위치에 간단한 ParticleSystem (또는 SpriteRenderer pulse)
   - 정식 VFX는 후속 ticket

### 4단계: BuildSetData 3개 SO 생성 (15분)

각 SO 인스펙터에서 입력:
- BuildSet_쿨감 → 위 표
- BuildSet_불 → 위 표 (Duration 필드 활용)
- BuildSet_바람 → 위 표

### 5단계: 인게임 검증 (45분)

```
시나리오 1: 쿨감
- 쿨감 아이템 6개 도달 → SkillCooldown total = 1.50 (50% 감소)
- 콘솔에 LogWarning 출력 ("적용할 스킬 없음")
- 향후 CL-160에서 적용

시나리오 2: 불 도트
- 불 아이템 4개 → 적 평타 시 화상 30/6초
- 1초마다 30 데미지 6회 누적 검증

시나리오 3: 바람 광역
- 바람 아이템 4개 → 적 맞췄을 때 주변 적도 데미지
- 적 그룹에서 단일 타격 → 여러 마리 데미지

시나리오 4: 복합
- 불 + 바람 동시 → 화상 + 광역 데미지 동시 작동
- 얼음 + 불 → 빙결된 적도 화상 진행 (정상)
- 전기 + 바람 → 체인 + 광역 (둘 다 발동, 다른 적에 데미지)
```

---

## 위험 / 결정 미정

### 위험
1. **OnHitEffectRegistry.Register API 시그니처 변경**: CL-142에서 `(type, magnitude, source)` 였는데 BurnOnHit 위해 `duration` 추가 → CL-142 코드 수정 필요. 영향 적음 (Register 호출처는 SetEffectApplicator뿐).
2. **WindAOE 무한 루프**: AOE 데미지가 또 OnHit 발화? → AOE는 Health.Damage 직접 호출 (TargetHit 안 발화) → 루프 X.
3. **빙결 + 화상 동시**: 적이 정지 상태에서도 화상 도트 진행? → 정상 (분리된 효과). 디자인 의도.
4. **VFX 누락**: 회오리 시각 없으면 플레이어가 효과 못 인지. → Placeholder로 Particle 1줄 추가.
5. **바람 디자인 미확정**: 옵션 A 채택했지만 사용자 확정 필요. 다르게 가면 plan 재작성.

### 결정 미정
- [ ] 바람 효과 디자인 (A 광역 / B 넉백 / C 이속) — **A 추천**
- [ ] 쿨감 적용 대상 (A 신규 SkillCooldown / B DashCooldown 공유 / C TODO) — **A 추천**
- [ ] 화상 시각 (적 빨간색 변경 등) — 회의록에 "화상입으면 빨개지게" 언급 → MVP에서 SpriteRenderer.color 변경만
- [ ] WindAOE 반경 (2 유닛 vs 더 크게) — 2 유닛 시작, 밸런스 후 조정

---

## 후속 ticket 영향

| Ticket | CL-143과의 관계 |
|---|---|
| **CL-144 (미소녀 자동공격)** | 본 CL의 BurnOnHit/WindAOE 활용 가능 (미소녀가 화상 부여 등) |
| **CL-145 (미소녀 5합체)** | 합체 광역 공격 = WindAOE의 강화 버전 패턴 |
| **CL-160 (스태프 스킬)** | 본 CL의 SkillCooldown StatId를 실제 적용. 스킬 쿨다운에 GetTotalMultiplier(SkillCooldown) 호출 |
| **CL-153 (QA)** | 본 CL의 3세트 검증 시나리오 |

---

## 예상 시간

| 단계 | 시간 |
|---|---|
| 1단계 (SkillCooldown) | 15분 |
| 2단계 (BurnOnHit) | 45분 |
| 3단계 (WindAOE) | 1시간 |
| 4단계 (BuildSetData 3개) | 15분 |
| 5단계 (검증) | 45분 |
| **합계** | **3시간** (티켓 점수 5점이지만 실제 3점급) |

CL-142 인프라 덕에 가벼움. 5점 → 실제 3점.

---

## 결정 요청

| # | 질문 | 옵션 | 추천 |
|---|---|---|---|
| 1 | 바람 효과 메커니즘 | A 광역 데미지 / B 넉백 / C 이속 | **A** |
| 2 | 쿨감 적용 대상 | A SkillCooldown 신설 / B DashCooldown 공유 / C TODO | **A** |
| 3 | 화상 시각 (회의록 "빨개지게") | A 적 SpriteRenderer.color / B 별도 VFX | **A** (MVP 단순) |
| 4 | WindAOE 반경 | 1.5 / 2.0 / 3.0 유닛 | **2.0** |

전부 추천대로면 **A + A + A + 2.0 유닛**.

---

## 다음 plan

CL-143 OK 하시면 다음:
- **CL-144**: 자동 발동 미소녀 1~4스택 (3점)
- 또는 CL-146 (공통 7세트) — 단순한 stat 효과 묶음
- 또는 CL-147 (타로) — P2이지만 흥미로운 시스템

추천: **CL-144** (미소녀 핵심 메커니즘. 게임 정체성에 중요).
