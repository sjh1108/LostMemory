# CL-142 — 평타 친화 5세트 효과 (공속/치명타/일반뎀 + 얼음 슬로우/전기 체인 on-hit)

## Context

Epic S Phase 3의 첫 ticket. **CL-140의 TODO 5개를 채우는 작업**.

평타 친화 5세트:
- 공속 (AttackSpeed) — 1/3/5스택 → 공격 속도 +10%/50%/100%
- 치명타 (Critical) — 2/4/6스택 → 치명타 확률 25%/50%/100%
- 일반뎀 (AttackPower) — 1/3스택 → 평타 데미지 +10%/30%
- 얼음 (Slow/Freeze on-hit) — 1/2/4스택 → 슬로우 1%/2%/5% + 빙결 1초
- 전기 (Chain on-hit) — 1/2/4스택 → 체인 데미지 본인 공격력 비례 10%/20%/30%

기존 시스템 분석 결과:

| 세트 | StatId 존재? | 적용 코드 존재? | CL-142 작업 |
|---|---|---|---|
| 공속 | ✅ | ✅ KhiMeleeComboController L167 | 검증만 |
| 일반뎀 | ✅ AttackPower | ✅ L217 | 검증만 |
| 치명타 | ⏳ CL-140에서 추가 | ❌ 없음 | **신규 구현** |
| 얼음 | - | ❌ 없음 | **신규 OnHit 시스템** |
| 전기 | - | ❌ 없음 | **신규 OnHit 시스템** |

→ **공속/일반뎀은 CL-140 라우터에 case 추가만 하면 작동**. 치명타·얼음·전기가 진짜 작업.

⚠️ **본 ticket은 5점이지만 실제로는 7~8점급**. 쪼개기 검토 필요 (§쪼개기 옵션 참조).

---

## 결정사항

### 1. 치명타 시스템 디자인

**메커니즘**:
- 데미지 적용 시점에 확률 roll
- 확률 = `GetTotalMultiplier(StatId.Critical) - 1.0` (예: 1.25 = 25% 확률)
- Roll 성공 시 데미지 ×2 (또는 별도 multiplier)

**Hook 위치**: `KhiMeleeComboController` L220 finalDamage 계산 직후

```csharp
float finalDamage = weaponData.BaseDamage * step.damageMultiplier * attackMul * finisherMul;

// CL-142: 치명타 처리
float critChance = statContainer != null ? statContainer.GetTotalMultiplier(StatId.Critical) - 1f : 0f;
bool isCritical = Random.value < critChance;
if (isCritical) finalDamage *= 2f;
```

**치명타 배율**:
- 옵션 A: 고정 ×2
- 옵션 B: 별도 StatId.CriticalDamage (×3 등 강화 가능)
- **채택: A (고정 ×2)**. MVP 단순. 후속에서 B 확장 검토.

**시각 피드백**:
- 치명타 시 별도 데미지 텍스트 색상 / 사운드
- → CL-142 범위 외 (시각/사운드는 별도 ticket)

### 2. OnHitEffectRegistry 신설

새 컴포넌트. 등록된 on-hit 효과들을 KhiMeleeComboController.TargetHit 이벤트에서 일괄 적용.

**위치**: Player GameObject (BuildManager·SetEffectApplicator와 같은 위치)

```csharp
public class OnHitEffectRegistry : MonoBehaviour
{
    [SerializeField] private KhiMeleeComboController combat;

    private struct OnHitEffect { public RelicEffectType Type; public float Magnitude; public object Source; }
    private readonly List<OnHitEffect> _activeEffects = new();

    private void OnEnable() => combat.TargetHit += HandleHit;
    private void OnDisable() => combat.TargetHit -= HandleHit;

    public void Register(RelicEffectType type, float magnitude, object source) { ... }
    public void Unregister(object source) { ... }

    private void HandleHit(KhiAttackRequest req, AttackStepData step, Health victim)
    {
        foreach (var effect in _activeEffects)
        {
            switch (effect.Type)
            {
                case RelicEffectType.SlowOnHit:    ApplySlow(victim, effect.Magnitude); break;
                case RelicEffectType.FreezeOnHit:  ApplyFreeze(victim, effect.Magnitude); break;
                case RelicEffectType.ChainOnHit:   ApplyChain(req, step, victim, effect.Magnitude); break;
                // CL-143에서 추가: BurnOnHit, WindAOE
            }
        }
    }
}
```

**SetEffectApplicator (CL-140) 통합**:
- DispatchEffect의 OnHit case → `onHitRegistry.Register(type, magnitude, source)`
- 이전 티어 효과 제거 → `onHitRegistry.Unregister(source)`

### 3. 적 측 Status Effect 시스템

적이 슬로우·빙결·체인 데미지를 받을 수 있어야 함.

**옵션**:
- (a) Enemy 컴포넌트에 직접 효과 적용 (Health.TakeDamage 호출 + MoveSpeed multiplier 변경)
- (b) **EnemyStatusEffect 컴포넌트 신설** ⭐ — 효과 모음 관리
- (c) TDE의 기존 status effect 시스템 활용 (있으면)

**채택: (b)**.

```csharp
public class EnemyStatusEffect : MonoBehaviour
{
    public void ApplySlow(float percent, float duration);     // 이속 N% 감소
    public void ApplyFreeze(float duration);                  // N초 정지
    public void ApplyBurn(float dps, float duration);         // 도트 (CL-143)

    private void Update() { /* 만료 처리 */ }
}
```

각 적 prefab에 부착 (또는 Awake에서 자동 추가).

### 4. 얼음 효과 (Slow + Freeze)

**Slow 적용** (티어 1/2):
- `EnemyStatusEffect.ApplySlow(magnitude, duration=2sec)`
- 중복 적용 시 갱신 (가장 강한 슬로우 유지)

**Freeze 적용** (티어 3, 5%):
- 회의록: "이속 5% 슬로우 + 빙결 (1초 정지)"
- → magnitude 0.05면 슬로우 + 빙결 동시
- 또는 magnitude > 0.04 시 빙결도 발동
- **채택: 티어 3 도달 시 SlowOnHit + FreezeOnHit 둘 다 등록** (BuildSetData에 두 효과)

### 5. 전기 효과 (Chain)

**메커니즘**:
- Player 평타 적중 시 → 0.5초 쿨다운 후 가까운 적에게 체인 데미지
- 체인 데미지 = 본인 공격력 × magnitude (10% / 20% / 30%)
- 한 번에 N마리 체인? — **MVP는 1마리만** (단순). N+ 후속 확장.

**구현**:
- ApplyChain(원본 hit) → 0.5초 후 가까운 적 검색 → Health.TakeDamage(chainDamage)
- 코루틴 또는 Time.time 비교

```csharp
private float _nextChainAllowedAt;

private void ApplyChain(KhiAttackRequest req, AttackStepData step, Health hit, float magnitude)
{
    if (Time.time < _nextChainAllowedAt) return;
    _nextChainAllowedAt = Time.time + 0.5f;

    Health target = FindNearbyEnemy(hit.transform.position, hit, range: 3f);
    if (target == null) return;

    float playerAttack = combat.WeaponData.BaseDamage * statContainer.GetTotalMultiplier(StatId.AttackPower);
    float chainDamage = playerAttack * magnitude;
    target.Damage(chainDamage, ...);
}
```

### 6. 공속·일반뎀·치명타 라우팅 (CL-140 case 채우기)

CL-140 plan에 TODO로 남긴 case들을 본 CL에서 채움:

```csharp
// SetEffectApplicator.DispatchEffect (CL-140)
case RelicEffectType.AttackSpeedPercent:
    statContainer.AddPermanent(StatId.AttackSpeed, tier.Magnitude, source);
    break;
case RelicEffectType.AttackPowerPercent:
    statContainer.AddPermanent(StatId.AttackPower, tier.Magnitude, source);
    break;
case RelicEffectType.CriticalChancePercent:
    statContainer.AddPermanent(StatId.Critical, tier.Magnitude, source);
    break;
case RelicEffectType.SlowOnHit:
    onHitRegistry.Register(RelicEffectType.SlowOnHit, tier.Magnitude, source);
    break;
case RelicEffectType.FreezeOnHit:
    onHitRegistry.Register(RelicEffectType.FreezeOnHit, tier.Magnitude, source);
    break;
case RelicEffectType.ChainOnHit:
    onHitRegistry.Register(RelicEffectType.ChainOnHit, tier.Magnitude, source);
    break;
```

### 7. StatId 확장 — Critical 적용 hook

CL-140에서 enum만 추가됨. 본 CL에서 KhiMeleeComboController에 적용 코드 작성.

```csharp
// KhiMeleeComboController L220 직후 (수정)
float finalDamage = weaponData.BaseDamage * step.damageMultiplier * attackMul * finisherMul;

// CL-142 추가
float critChance = statContainer != null
    ? Mathf.Max(0f, statContainer.GetTotalMultiplier(StatId.Critical) - 1f)
    : 0f;
bool isCritical = Random.value < critChance;
if (isCritical) finalDamage *= 2f;
```

---

## 핵심 파일

### 신규

| 경로 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Combat/OnHitEffectRegistry.cs` | OnHit 효과 라우터 |
| `Assets/_Project/Scripts/Runtime/Enemy/EnemyStatusEffect.cs` | 적 측 status (slow/freeze/burn) |
| `Assets/_Project/Scripts/Runtime/Enemy/EnemyMoveSpeedSlow.cs` (선택) | 적 이속 적용 어댑터 |

### 수정

| 경로 | 변경 내용 |
|---|---|
| `Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeComboController.cs` | 치명타 데미지 배율 추가 (L220 부근) |
| `Assets/_Project/Scripts/Runtime/Relics/SetEffectApplicator.cs` (CL-140) | 5개 case 채움 (AttackSpeed/AttackPower/Critical/SlowOnHit/FreezeOnHit/ChainOnHit) |
| 적 prefab들 | `EnemyStatusEffect` 컴포넌트 추가 (Awake 자동 추가 또는 수동) |

### 참조

| 경로 | 사용 |
|---|---|
| `BuildManager` (CL-139) | 본 CL과 무관 (CL-140 통해 간접) |
| `BuildSetData_*` SO들 | 각 세트의 EffectType + Magnitude 참고 |
| `Health` (TDE) | 데미지 적용 |

---

## 구현 단계

### 1단계: 공속/일반뎀 검증 (15분)

이미 작동. CL-140 SetEffectApplicator에 case 추가 후:
- BuildSetData_공속.asset 16~17개 RelicData 추가 → 5스택 도달 → 공격력 multiplier 200% 검증
- 마찬가지로 일반뎀

### 2단계: 치명타 구현 (1시간)

1. `KhiMeleeComboController` 수정 (L220 부근):
   - critChance 계산
   - Random.value 비교
   - 데미지 ×2
2. SetEffectApplicator에 CriticalChancePercent case 추가
3. BuildSetData_치명타.asset 작성 (2/4/6 → 0.25/0.50/1.00)
4. 인게임 검증: 치명타 6스택 = 100% 확률 → 모든 공격 ×2 데미지

### 3단계: OnHitEffectRegistry 골격 (1시간)

1. `OnHitEffectRegistry.cs` 작성
2. KhiMeleeComboController.TargetHit 구독
3. SerializedField로 KhiMeleeComboController 참조
4. Register/Unregister API
5. SetEffectApplicator의 OnHit case → onHitRegistry.Register 호출

### 4단계: EnemyStatusEffect 컴포넌트 (1시간)

1. 컴포넌트 작성:
   - `ApplySlow(percent, duration)` — Movement 컴포넌트의 speed multiplier 적용
   - `ApplyFreeze(duration)` — Movement 일시 정지 + IsFrozen flag
   - 만료 시 자동 복원
2. 기존 적 prefab들 (Skeleton, Goblin 등)에 컴포넌트 추가
3. 적 Movement 컴포넌트 (TDE CharacterMovement?) 와의 통합

### 5단계: 얼음 효과 통합 (45분)

1. OnHitEffectRegistry.HandleHit에 SlowOnHit/FreezeOnHit case 추가:
   - victim의 EnemyStatusEffect 가져옴
   - ApplySlow / ApplyFreeze 호출
2. BuildSetData_얼음.asset 작성:
   - 티어 1: SlowOnHit, magnitude 0.01
   - 티어 2: SlowOnHit, magnitude 0.02
   - 티어 3: SlowOnHit, magnitude 0.05 + FreezeOnHit, magnitude 1.0 (1초)
3. 검증: 얼음 4스택 도달 → 평타 시 적 슬로우 + 빙결

### 6단계: 전기 체인 효과 (1시간)

1. OnHitEffectRegistry.HandleHit에 ChainOnHit case 추가:
   - 0.5초 쿨다운 체크
   - FindNearbyEnemy(victim 위치, range 3f)
   - target.Damage(playerAttack × magnitude)
2. FindNearbyEnemy 구현 (Physics2D.OverlapCircleAll + Health 컴포넌트 필터)
3. BuildSetData_전기.asset 작성:
   - 티어 1: ChainOnHit, magnitude 0.10
   - 티어 2: 0.20
   - 티어 3: 0.30
4. 검증: 전기 4스택 → 체인 발동 → 가까운 적 데미지

### 7단계: 통합 검증 (30분)

여러 세트 동시 활성화 시나리오:
- 공속 5스택 + 치명타 6스택 + 얼음 4스택 + 전기 4스택 동시
- 적 처치 속도 / 슬로우 / 체인 모두 정상

---

## 검증 방법 (e2e)

```
시나리오 1: 단일 세트 검증
1. 공속 아이템 5개 추가 → 콤보 속도 2배 확인
2. 일반뎀 아이템 3개 → 데미지 +30% 확인
3. 치명타 아이템 6개 → 모든 공격 ×2 (확률 100%)
4. 얼음 아이템 4개 → 적 슬로우 + 빙결 확인
5. 전기 아이템 4개 → 체인 데미지 확인 (가까운 적 추가 데미지)

시나리오 2: 복합
- 공속+치명타+일반뎀 빌드 = DPS 폭딜
- 얼음+전기 빌드 = 군중 제어 + 광역
- 둘 다 활성 시 게임 안 멈춤 + 효과 정상

시나리오 3: 티어 변화
- 얼음 1→2→3스택 진행 시 슬로우 강화 확인
- 빙결은 3스택부터만 발동 (티어 임계치 검증)
```

---

## 위험 / 결정 미정

### 위험
1. **OnHit 효과 적층**: 얼음 + 전기 + 불 (CL-143) 동시 활성 시 정상 작동? → 같은 TargetHit 이벤트 한 번 발화에 모든 등록된 효과 순회 → 순서 무관하게 작동
2. **EnemyStatusEffect 누락**: 적 prefab에 컴포넌트 안 붙으면 Slow/Freeze 무시. → Awake에서 자동 추가 (또는 IComponent.Get<>) 권장
3. **TDE CharacterMovement 호환**: 적의 이속 multiplier가 TDE 시스템과 충돌? → 적 측은 CharacterMovement.MovementSpeedMultiplier 사용 (Player와 동일 패턴)
4. **체인 무한 루프**: 체인이 또 다른 체인 트리거? → Player.TargetHit 만 구독. 체인 자체는 Health.Damage 직접 호출 (TargetHit 안 발화) → 무한 루프 X
5. **치명타 RNG**: 게임마다 결과 달라짐 → 시연용 시드 고정 활용 (CL-119 패턴)

### 결정 미정
- [ ] 치명타 배율 (×2 vs StatId.CriticalDamage 별도) — MVP는 ×2
- [ ] 빙결 중첩 정책 (이미 빙결 중일 때 새 빙결 무시 vs 갱신) — 갱신 권장 (단순)
- [ ] 체인 거리 (3유닛 vs 사거리 빌드 영향) — 고정 3유닛 → 후속 검토
- [ ] 체인이 1마리 vs 여러 마리 — MVP는 1마리
- [ ] EnemyStatusEffect는 모든 적에 자동 부착 vs 수동 — Awake에서 GetComponent 후 없으면 AddComponent (자동)

---

## 작업 단위 가이드 (Plan 분할, Ticket 단일)

> **사용자 결정**: ticket은 **CL-142 단일 유지**, plan에서만 작업 단위로 분할 표시. 같은 branch에서 작업.

### 작업 단위 A (1.5~2시간) — Stat 효과
- 단계 1+2+7
- SetEffectApplicator case 채우기 (AttackSpeed/AttackPower/Critical)
- KhiMeleeComboController 치명타 구현
- BuildSetData_공속/치명타/일반뎀 작성

### 작업 단위 B (3~4시간) — OnHit 시스템
- 단계 3+4+5+6+7
- OnHitEffectRegistry 신설
- EnemyStatusEffect 신설
- 얼음 (Slow/Freeze) 통합
- 전기 (Chain) 통합
- BuildSetData_얼음/전기 작성

### 작업 순서
A → B 순서 권장:
1. A 완료 후 인게임 기본 검증 (공속/치명타/평타 데미지)
2. B 진행 (OnHit 인프라 구축)
3. 통합 검증 (단계 7)

같은 branch에서 작업하고, 커밋만 단위별로 분리 (예: `feat(cl142a): 공속/치명타/일반뎀 적용`, `feat(cl142b): OnHit + 얼음/전기`).

---

## 후속 ticket 영향

| Ticket | CL-142와의 관계 |
|---|---|
| **CL-143 (스킬 3세트: 쿨감/불/바람)** | 본 CL의 OnHitEffectRegistry 재사용. BurnOnHit/WindAOE case만 추가 |
| **CL-144 (미소녀 자동공격)** | 본 CL의 EnemyStatusEffect 활용 가능 (미소녀가 슬로우 부여 등) |
| **CL-146 (공통 7세트)** | 본 CL의 StatId.Range/Dodge/Defense 적용 hook 위치 결정 (별도 작업) |
| **CL-153 (QA)** | 본 CL의 5세트 검증 시나리오 |

---

## 예상 시간

### 옵션 A (단일)
| 단계 | 시간 |
|---|---|
| 1단계 (공속/일반뎀 검증) | 15분 |
| 2단계 (치명타) | 1시간 |
| 3단계 (OnHitEffectRegistry) | 1시간 |
| 4단계 (EnemyStatusEffect) | 1시간 |
| 5단계 (얼음 통합) | 45분 |
| 6단계 (전기 체인) | 1시간 |
| 7단계 (통합 검증) | 30분 |
| **합계** | **5~6시간** |

### 옵션 B (분할)
- CL-142a: 1.5~2시간 (3점 적정)
- CL-142b: 3~4시간 (5점 적정)

---

## 결정 요청

진행 전 결정:

| # | 질문 | 옵션 | 추천 |
|---|---|---|---|
| 1 | 본 ticket 분할? | A 단일 / **B 둘로** / C 셋으로 | **B** |
| 2 | 치명타 배율 | ×2 고정 / StatId.CriticalDamage 별도 | ×2 고정 |
| 3 | 체인 1마리 vs N마리 | 1마리 / N마리 | 1마리 (MVP) |
| 4 | 빙결 중첩 | 무시 / 갱신 | 갱신 |
| 5 | EnemyStatusEffect 자동 부착 | 수동 / 자동 | 자동 (Awake) |

전부 추천대로면 **B 분할 + ×2 고정 + 1마리 체인 + 빙결 갱신 + 자동 부착**.

이대로 진행하시겠어요?

만약 분할 결정 (B)이면:
- 본 plan 그대로 (참조용)
- CL-142a / CL-142b 별도 plan 작성 필요? 또는 본 plan §단계 분할 표시로 충분?
