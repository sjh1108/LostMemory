# CL-142 — 평타 친화 5세트 효과 구현 (분할 B)

> 파일명은 139 이지만 내용은 CL-142 (plan mode 단일 plan 파일 제약).

## Context

**왜**: Epic S Phase 3 의 첫 ticket. CL-138~141 Foundation 위에 **5세트 효과를 본격
적용**. CL-140 의 SetEffectApplicator 가 OnHit 3 case (Slow/Freeze/Chain) 를 LogWarning
으로만 두고 있는데, 본 CL 이 그 자리를 채우고 OnHitEffectRegistry + EnemyStatusEffect
인프라를 신설.

**해결되는 문제**: 현재 CL-141 이 만든 Generated/ SO 들 중 평타 친화 5세트 (공속·치명타·
일반뎀·얼음·전기) 의 효과가 **부분 적용 또는 미적용**:
- 공속/일반뎀: 이미 적용됨 ✅ (검증만)
- 치명타: SetEffectApplicator case 있지만 KhiMeleeComboController 의 데미지 hook 없음
- 얼음/전기: SetEffectApplicator 가 LogWarning, 적용 시스템 0

**의도된 결과**:
- 5세트 모두 본격 동작 (공속 ×2 / 치명타 100% / 일반뎀 +30% / 얼음 슬로우+빙결 / 전기 체인 3마리)
- OnHit 인프라 (CL-143 burn/wind 가 그대로 재활용 가능)
- 5 BuildSetData SO Tiers 본격 입력

## 사용자 결정 사항 (이미 합의)

- ✅ **분할 B**: 같은 ticket 단일 branch, plan 만 작업 단위 a/b 로 분리. 커밋 단위별 분리
- ✅ **치명타 배율 ×2 고정**
- ✅ **체인 3마리** (plan 추천 1마리 → 변경): 첫 hit 위치 기준 반경 내 가장 가까운
  최대 3마리 **동시 데미지** (점프 메커니즘 X — MVP 단순)
- ✅ **빙결 중첩 = 갱신** (이미 빙결 중에 새 빙결 오면 시간만 교체)
- ✅ **EnemyStatusEffect 자동 부착** (Awake 에서 GetComponent, 없으면 AddComponent)

## 사전 의존

- ✅ CL-138/139/140/141 완료
- ✅ SetEffectApplicator 의 Critical/AttackSpeed/AttackPower case 이미 구현됨 (검증만)
- ✅ KhiMeleeComboController.TargetHit 이벤트 존재 (line 46), 구독만 하면 됨
- ⚠️ 검증 시 BuildSetData_공속/치명타/일반뎀/얼음/전기 5 SO 의 Tiers 본격 입력 필요 (본 CL 작업의 일부)

---

## 작업 단위 A — Stat 효과 (1.5~2시간)

### A-1: SetEffectApplicator case 검증 (5분)

이미 작성됨 ([SetEffectApplicator.cs:85,87,93](LostMemory/Assets/_Project/Scripts/Runtime/Relics/SetEffectApplicator.cs#L85)):
- `AttackPowerPercent` → `StatId.AttackPower` ✅
- `AttackSpeedPercent` → `StatId.AttackSpeed` ✅
- `CriticalChancePercent` → `StatId.Critical` ✅

작업 없음. 검증만.

### A-2: KhiMeleeComboController 에 치명타 적용 (15분)

**파일**: [KhiMeleeComboController.cs:220](LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeComboController.cs#L220)

기존 finalDamage 계산 직후 추가:

```csharp
float finalDamage = weaponData.BaseDamage * step.damageMultiplier * attackMul * finisherMul;

// CL-142: 치명타 처리 — Magnitude 가 critChance (0.25 = 25%), 적중 시 ×2
float critChance = statContainer != null
    ? Mathf.Max(0f, statContainer.GetTotalMultiplier(StatId.Critical) - 1f)
    : 0f;
bool isCritical = critChance > 0f && UnityEngine.Random.value < critChance;
if (isCritical) finalDamage *= 2f;
```

`GetTotalMultiplier - 1f` 패턴: container 가 `1 + Σ(percents)` 반환하므로 -1 하면
순수 stack 합산. 시각 피드백(데미지 텍스트 색상)은 별도 ticket.

### A-3: BuildSetData 3 SO 작성 (Unity Editor, 30~45분)

| SO | Tiers (RequiredCount → EffectType, Magnitude) |
|---|---|
| `BuildSet_공속.asset` | 1 → AttackSpeedPercent, 0.10 / 3 → 0.50 / 5 → 1.0 |
| `BuildSet_치명타.asset` | 2 → CriticalChancePercent, 0.25 / 4 → 0.50 / 6 → 1.0 |
| `BuildSet_일반뎀.asset` | 1 → AttackPowerPercent, 0.10 / 3 → 0.30 |

cl142_plan.md 의 회의록 표 그대로. CL-138 BuildSets 폴더의 기존 SO Inspector 에서 직접 입력.

### A-4: 단위 A 검증 (15분)

`Debug — Add` 로 평타 친화 SO 추가:
- 공속 5스택: 콤보 속도 ×2 (육안 확인)
- 일반뎀 3스택: `[StatModifier] AddPermanent AttackPower +30%` 로그 + 데미지 +30%
- 치명타 6스택: 모든 공격 ×2 데미지 (확률 100%)

Console:
```
[BuildManager] AttackSpeed: tier -1 → 2 (count=5)
[SetEffect] APPLY AttackSpeed t2: AttackSpeedPercent mag=1
[StatModifier] AddPermanent AttackSpeed +100% src=SetEffect(AttackSpeed, t2)
```

---

## 작업 단위 B — OnHit 시스템 (3~4시간)

### B-1: OnHitEffectRegistry 신설 (1시간)

**경로**: `LostMemory/Assets/_Project/Scripts/Runtime/Combat/OnHitEffectRegistry.cs`

Player GameObject 부착. KhiMeleeComboController.TargetHit 구독 → 등록된 OnHit 효과들
순회 적용.

```csharp
[DisallowMultipleComponent]
[AddComponentMenu("Lost Memory/Combat/On Hit Effect Registry")]
public sealed class OnHitEffectRegistry : MonoBehaviour
{
    [SerializeField] private KhiMeleeComboController combat;
    [SerializeField] private PlayerStatModifierContainer statContainer;
    [Tooltip("체인 검색 반경 (유닛). MVP: 3.")]
    [SerializeField] private float _chainRadius = 3f;
    [Tooltip("체인 동시 타격 최대 마리수. MVP: 3.")]
    [SerializeField] private int _chainMaxTargets = 3;
    [Tooltip("체인 발동 후 글로벌 쿨다운 (초). MVP: 0.5.")]
    [SerializeField] private float _chainCooldown = 0.5f;
    [Tooltip("얼음 슬로우 지속시간 (초). MVP: 2.")]
    [SerializeField] private float _slowDuration = 2f;

    private readonly IRelicEffectAuthority _authority = new NetworkRelicEffectAuthority();

    private struct OnHitEntry
    {
        public RelicEffectType Type;
        public float Magnitude;
        public object Source;
    }
    private readonly List<OnHitEntry> _entries = new();
    private float _nextChainAllowedAt;

    private void OnEnable()
    {
        if (combat == null) { Debug.LogError("[OnHitEffectRegistry] combat null"); return; }
        combat.TargetHit += HandleHit;
    }

    private void OnDisable()
    {
        if (combat == null) return;
        combat.TargetHit -= HandleHit;
    }

    public void Register(RelicEffectType type, float magnitude, object source)
    {
        _entries.Add(new OnHitEntry { Type = type, Magnitude = magnitude, Source = source });
    }

    public void UnregisterBySource(object source)
    {
        _entries.RemoveAll(e => Equals(e.Source, source));
    }

    private void HandleHit(KhiAttackRequest req, AttackStepData step, Health victim)
    {
        if (!_authority.IsAuthority) return;
        if (victim == null) return;

        foreach (OnHitEntry e in _entries)
        {
            switch (e.Type)
            {
                case RelicEffectType.SlowOnHit:    ApplySlow(victim, e.Magnitude); break;
                case RelicEffectType.FreezeOnHit:  ApplyFreeze(victim, e.Magnitude); break;
                case RelicEffectType.ChainOnHit:   ApplyChain(req, step, victim, e.Magnitude); break;
                // CL-143 에서 추가: BurnOnHit, WindAOE
            }
        }
    }

    private void ApplySlow(Health victim, float magnitude)
    {
        EnemyStatusEffect status = GetOrAddStatus(victim);
        status?.ApplySlow(magnitude, _slowDuration);
    }

    private void ApplyFreeze(Health victim, float magnitudeSeconds)
    {
        EnemyStatusEffect status = GetOrAddStatus(victim);
        status?.ApplyFreeze(magnitudeSeconds);   // magnitude 가 초 단위
    }

    private void ApplyChain(KhiAttackRequest req, AttackStepData step, Health victim, float magnitude)
    {
        if (Time.time < _nextChainAllowedAt) return;
        _nextChainAllowedAt = Time.time + _chainCooldown;

        // 본인 공격력 (StatModifier 합산 적용)
        float playerAttack = combat.WeaponData != null ? combat.WeaponData.BaseDamage : 0f;
        if (statContainer != null) playerAttack *= statContainer.GetTotalMultiplier(StatId.AttackPower);
        float chainDamage = playerAttack * magnitude;

        // victim 위치 기준 반경 내 가장 가까운 N 마리 동시 데미지 (victim 자신 제외)
        List<Health> targets = FindNearbyEnemies(victim.transform.position, victim, _chainRadius, _chainMaxTargets);
        foreach (Health t in targets)
        {
            t.Damage(chainDamage, gameObject, 0f, 0f, Vector3.zero);
        }
    }

    private static EnemyStatusEffect GetOrAddStatus(Health victim)
    {
        if (victim == null) return null;
        EnemyStatusEffect status = victim.GetComponent<EnemyStatusEffect>();
        if (status == null) status = victim.gameObject.AddComponent<EnemyStatusEffect>();
        return status;
    }

    private static readonly Collider2D[] _chainBuf = new Collider2D[16];

    private static List<Health> FindNearbyEnemies(Vector3 origin, Health exclude, float radius, int maxCount)
    {
        var result = new List<(Health h, float distSq)>();
        int hits = Physics2D.OverlapCircleNonAlloc(origin, radius, _chainBuf);
        for (int i = 0; i < hits; i++)
        {
            Collider2D col = _chainBuf[i];
            if (col == null) continue;
            Health h = col.GetComponentInParent<Health>();
            if (h == null || h == exclude) continue;
            if (h.CurrentHealth <= 0f) continue;
            float dSq = (h.transform.position - origin).sqrMagnitude;
            result.Add((h, dSq));
        }
        result.Sort((a, b) => a.distSq.CompareTo(b.distSq));
        return result.Take(maxCount).Select(t => t.h).ToList();
    }
}
```

체인 무한 루프 방지: `combat.TargetHit` 만 구독, 체인은 `Health.Damage` 직접 호출 →
TargetHit 안 발화 → 체인이 또 다른 체인 트리거 X.

### B-2: EnemyStatusEffect 신설 (45분)

**경로**: `LostMemory/Assets/_Project/Scripts/Runtime/Enemies/EnemyStatusEffect.cs`

```csharp
[DisallowMultipleComponent]
public sealed class EnemyStatusEffect : MonoBehaviour
{
    private CharacterMovement _movement;   // TDE
    private float _slowExpiresAt;
    private float _slowMagnitude;          // 0.05 = -5% 이속
    private float _freezeExpiresAt;
    private float _baseSpeedMultiplier = 1f;

    private void Awake()
    {
        _movement = GetComponentInParent<CharacterMovement>();
    }

    public void ApplySlow(float magnitude, float duration)
    {
        // 더 강한 슬로우 만 유지 (가장 큰 magnitude)
        if (magnitude > _slowMagnitude || Time.time >= _slowExpiresAt)
        {
            _slowMagnitude = magnitude;
        }
        _slowExpiresAt = Mathf.Max(_slowExpiresAt, Time.time + duration);
        ApplyMovementMultiplier();
    }

    public void ApplyFreeze(float durationSeconds)
    {
        // 갱신 정책: 항상 새 시간으로 (사용자 결정)
        _freezeExpiresAt = Time.time + durationSeconds;
        ApplyMovementMultiplier();
    }

    private void Update()
    {
        bool changed = false;
        if (_slowMagnitude > 0f && Time.time >= _slowExpiresAt)
        {
            _slowMagnitude = 0f;
            changed = true;
        }
        if (_freezeExpiresAt > 0f && Time.time >= _freezeExpiresAt)
        {
            _freezeExpiresAt = 0f;
            changed = true;
        }
        if (changed) ApplyMovementMultiplier();
    }

    private void ApplyMovementMultiplier()
    {
        if (_movement == null) return;

        bool isFrozen = Time.time < _freezeExpiresAt;
        float slowFactor = (_slowMagnitude > 0f && Time.time < _slowExpiresAt)
            ? Mathf.Clamp(1f - _slowMagnitude, 0f, 1f)
            : 1f;
        float effectiveMul = isFrozen ? 0f : (_baseSpeedMultiplier * slowFactor);
        _movement.MovementSpeedMultiplier = effectiveMul;
    }
}
```

**자동 부착**: OnHitEffectRegistry.GetOrAddStatus 가 적 처음 hit 시점에 GetComponent →
없으면 AddComponent. 적 prefab 수정 불필요. (단점: 첫 hit 시 1프레임 지연 가능 → MVP 허용)

**TDE CharacterMovement 통합**: `MovementSpeedMultiplier` 사용 (Player StatModifier 와
동일 패턴). 빙결 시 0 으로 멈춤.

### B-3: SetEffectApplicator 의 OnHit 3 case 교체 (15분)

[SetEffectApplicator.cs:106-112](LostMemory/Assets/_Project/Scripts/Runtime/Relics/SetEffectApplicator.cs#L106) — 기존 LogWarning 골격을 본격 라우팅으로:

```csharp
// before:
case RelicEffectType.BurnOnHit:
case RelicEffectType.SlowOnHit:
case RelicEffectType.FreezeOnHit:
case RelicEffectType.ChainOnHit:
case RelicEffectType.WindAOE:
    Debug.LogWarning($"[SetEffectApplicator] {tier.EffectType} OnHit 라우팅 미구현 (CL-142/143)");
    break;

// after:
case RelicEffectType.SlowOnHit:
case RelicEffectType.FreezeOnHit:
case RelicEffectType.ChainOnHit:
    onHitRegistry.Register(tier.EffectType, tier.Magnitude, source);
    break;
case RelicEffectType.BurnOnHit:
case RelicEffectType.WindAOE:
    Debug.LogWarning($"[SetEffectApplicator] {tier.EffectType} OnHit 라우팅 미구현 (CL-143)");
    break;
```

`RemoveTierEffect` 도 OnHit case 추가:
```csharp
private void RemoveTierEffect(BuildSetData set, int tierIndex, SetTier tier)
{
    if (_logEffectDispatch) Debug.Log($"[SetEffect] REMOVE ...");
    var source = new SetEffectSource(set, tierIndex);
    statContainer.RemoveBySource(source);
    if (onHitRegistry != null) onHitRegistry.UnregisterBySource(source);
}
```

`SerializeField OnHitEffectRegistry onHitRegistry` 추가 + Inspector wiring.

### B-4: BuildSetData 2 SO 작성 (Unity Editor, 30분)

**SO**: `BuildSet_얼음.asset`, `BuildSet_전기.asset`

`BuildSet_얼음` Tiers:
- 1 → SlowOnHit, magnitude 0.01 (1% 슬로우)
- 2 → SlowOnHit, magnitude 0.02 (2%)
- 3 → SlowOnHit, magnitude 0.05 (5%) **+ 별도 effect 필요**

문제: `SetTier` struct 는 단일 EffectType 만 보유. 티어 3 의 "Slow + Freeze 동시" 표현 불가.

**해결**: 회의록 결정 ("3티어 = SlowOnHit + FreezeOnHit 둘 다 등록") 을 **티어 3 에
FreezeOnHit 만 입력** + Slow 효과는 별도 RelicData (얼음 아이템) 의 effects[] 에서 이미
적용되도록 하는 방식. 또는 SetTier 를 array 로 확장.

→ **MVP 결정**: Tier 3 = FreezeOnHit, magnitude 1.0 (1초). 슬로우는 티어 2 의 0.02 가
계속 활성 (티어 2 = 누적 X, 최고 티어만이라 슬로우 사라짐). 회의록 의도 100% 구현은
SetTier 구조 확장 필요 → 본 CL 범위 외, CL-146 또는 별도 ticket 에서 구조 결정.

→ **본 CL 단순 채택**: 각 티어 1 효과만:
- 1 → SlowOnHit, 0.01 (1%)
- 2 → SlowOnHit, 0.02 (2%)
- 3 → FreezeOnHit, 1.0 (1초 빙결, 슬로우 효과는 일시 X — 회의록 100% 구현 X. 후속 ticket)

`BuildSet_전기` Tiers:
- 1 → ChainOnHit, 0.10
- 2 → ChainOnHit, 0.20
- 3 → ChainOnHit, 0.30

### B-5: Player GameObject wiring (10분)

`TestKhi_MinimalCharacter2D.prefab` 에 OnHitEffectRegistry 컴포넌트 부착 + 슬롯 wiring:
- `combat` ← KhiMeleeComboController
- `statContainer` ← PlayerStatModifierContainer
- 기본값 (`_chainRadius=3, _chainMaxTargets=3, _chainCooldown=0.5, _slowDuration=2`) 유지

SetEffectApplicator Inspector 에 `onHitRegistry` 슬롯 추가 → 같은 GameObject 의 OnHitEffectRegistry 드래그.

### B-6: 단위 B 검증 (30분)

시나리오:
1. 얼음 아이템 4개 (Tier 3 도달) → 평타 시 적 슬로우 → 4스택 도달 시 빙결 추가
2. 전기 아이템 4개 → 평타 시 가까운 적 3마리 동시 데미지 ([StatModifier] 로그 X — Health.Damage 직접)
3. 얼음+전기 동시 활성 → 두 효과 모두 발동

Console:
```
[SetEffect] APPLY Ice t2: SlowOnHit mag=0.02
[SetEffect] APPLY Lightning t1: ChainOnHit mag=0.10
... (평타 시점)
(EnemyStatusEffect 의 디버그 로그 추가 시) [Status] Slow applied to Orc_CL037 (mag=0.02, expires in 2s)
```

---

## 핵심 파일

### 신규 (2)
- `LostMemory/Assets/_Project/Scripts/Runtime/Combat/OnHitEffectRegistry.cs` — OnHit 라우터
- `LostMemory/Assets/_Project/Scripts/Runtime/Enemies/EnemyStatusEffect.cs` — 적 측 Status

### 수정 (2)
- [`KhiMeleeComboController.cs:220`](LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeComboController.cs#L220) — 치명타 데미지 ×2 처리 (5줄 추가)
- [`SetEffectApplicator.cs:106-112`](LostMemory/Assets/_Project/Scripts/Runtime/Relics/SetEffectApplicator.cs#L106) — OnHit 3 case 본격 라우팅 + onHitRegistry SerializeField + RemoveTierEffect 에 unregister + Inspector wiring

### Unity Editor 작업
- 5개 BuildSetData SO Tiers 입력: 공속/치명타/일반뎀/얼음/전기
- TestKhi_MinimalCharacter2D.prefab 에 OnHitEffectRegistry 부착 + 슬롯 2개 wiring
- SetEffectApplicator 의 onHitRegistry 슬롯 wiring

### 재사용 (수정 X)
- BuildManager (CL-139) — 무관
- KhiMeleeComboController.TargetHit 이벤트 — 구독만
- PlayerStatModifierContainer — Critical multiplier 조회용

---

## 결정사항

| 결정 | 값 | 근거 |
|---|---|---|
| 작업 분할 | B (a/b 단위, 같은 ticket) | plan 권장, 검증 2회로 위험 분산 |
| 치명타 배율 | ×2 고정 | MVP 단순 (StatId.CriticalDamage 별도 ticket) |
| 체인 마릿수 | **3 (사용자 변경)** | plan 추천 1마리 → 3마리. 동시 데미지, 점프 X |
| 체인 발동 정책 | 첫 hit 위치 기준 반경 내 가장 가까운 3마리 동시 | MVP 단순. 점프 메커니즘은 후속 |
| 빙결 중첩 | 갱신 (이미 빙결 중 새 빙결 = 시간 교체) | 단순 |
| 슬로우 중첩 | 가장 강한 magnitude 유지 + 시간 max | 디자이너 의도에 가까움 |
| EnemyStatusEffect 부착 | OnHitEffectRegistry 가 첫 hit 시 자동 (GetOrAdd) | 적 prefab 수정 불필요. 1프레임 지연 허용 |
| OnHit 효과 적층 | 같은 TargetHit 이벤트에 모든 등록된 효과 순회 | 순서 무관, 단순 |
| 체인 무한루프 방지 | combat.TargetHit 만 구독, 체인은 Health.Damage 직접 | 자체 트리거 X |
| 얼음 Tier 3 = Slow+Freeze 동시 | 본 CL 미구현 (Tier 3 = FreezeOnHit 만) | SetTier 단일 EffectType 제약. 별도 ticket 에서 구조 확장 결정 |

## 검증 (e2e)

### 단위 A 검증
1. 공속 5스택 → `[SetEffect] APPLY AttackSpeed t2: AttackSpeedPercent mag=1` + 콤보 속도 ×2
2. 일반뎀 3스택 → `AddPermanent AttackPower +30%` + 데미지 +30%
3. 치명타 6스택 → 모든 공격 ×2 (확률 100%)

### 단위 B 검증
4. 얼음 4스택 → 평타 시 적 슬로우 + 빙결
5. 전기 4스택 → 평타 시 가까운 적 3마리 동시 데미지
6. 얼음+전기 동시 → 두 효과 모두 발동
7. Run Clear → 모든 OnHit 효과 unregister 확인

### 통합 검증
8. 공속+치명타+일반뎀+얼음+전기 5세트 동시 활성 → 게임 안 멈춤 + 모든 효과 정상

## 위험 / 결정 미정

### 위험
1. **EnemyStatusEffect 가 적이 죽기 전에 동작**: Health 가 victim 의 root 또는 하위에
   있을 때 GetComponentInParent 로 Movement 찾기. 적 prefab 구조에 따라 _movement 가
   null 일 수 있음 → null 체크 필수.
2. **체인이 Player 자신을 hit**: FindNearbyEnemies 가 player Collider 잡을 위험. Health
   필터로 Player 제외하거나 Layer 기반 필터 필요. → 본 plan 의 `h == exclude` 만으로는
   부족할 수 있음. 검증 시 Player 데미지 받는지 확인.
3. **체인 데미지 = 본인 공격력 × magnitude** 인데 본인 공격력 계산 시 step.damageMultiplier 무시.
   순수 BaseDamage × StatId.AttackPower multiplier 로 단순화. 디자이너 검수 필요.
4. **얼음 Tier 3 미구현 정책**: 회의록 의도 (Slow+Freeze 동시) 100% 구현 X. SetTier
   array 화 또는 새 EffectType 신설 (예: SlowFreezeOnHit) 결정 별도 ticket.
5. **빙결 시 이속 0**: TDE CharacterMovement 기준 MovementSpeedMultiplier=0 이 정상
   동작? 적 AI 가 이동 시도하면서 추격 못 하는 형태. 검증 시 적 어색하지 않은지 확인.

### 결정 미정 (본 CL 외)
- [ ] 치명타 시각 피드백 (데미지 텍스트 색상, 사운드) — 별도 ticket
- [ ] StatId.CriticalDamage (×2 고정 외 가변) — 별도 ticket
- [ ] 체인 점프 메커니즘 (체인 → 그 적 기준 다음 체인) — 후속 확장
- [ ] SetTier 다중 효과 구조 (얼음 Tier 3 Slow+Freeze 동시) — 별도 ticket

## 후속 인계

| Ticket | CL-142 와의 관계 |
|---|---|
| **CL-143 (스킬 3세트)** | 본 CL 의 OnHitEffectRegistry 재사용. BurnOnHit/WindAOE case 만 추가 |
| **CL-144 (미소녀)** | 본 CL 의 EnemyStatusEffect 활용 가능 (미소녀가 슬로우 부여 등) |
| **CL-146 (공통 7세트)** | StatId.Range/Dodge/Defense 적용 hook 위치 결정 (별도 작업) + DefenseFlat % 충돌 정책 결정 (CL-140 인계) |
| **별도 ticket — SetTier 다중 효과 구조** | 얼음 Tier 3 Slow+Freeze 동시 구현 위해 SetTier 배열화 또는 enum 신설 |
| **별도 ticket — 치명타 시각 피드백** | 데미지 텍스트 색상, 사운드, particle |
| **CL-153 (QA)** | 본 CL 의 5세트 검증 시나리오 |

## Phase 3 진행 상태

- [x] CL-138~141 (Foundation Phase 1+2)
- [ ] **CL-142 (평타 5세트)** ← 본 CL
- [ ] CL-143 (스킬 3세트)
- [ ] CL-144~145 (미소녀)
- [ ] CL-146 (공통 7세트)
- [ ] CL-147 (타로)
- [ ] CL-148 (인벤토리 UI)

## 예상 시간

| 단위 | 단계 | 시간 |
|---|---|---|
| A | A-1 SetEffectApplicator 검증 | 5분 |
| A | A-2 KhiMeleeComboController 치명타 | 15분 |
| A | A-3 BuildSet 3 SO Tiers (Editor) | 30~45분 |
| A | A-4 단위 A 검증 | 15분 |
| **A 합계** | | **약 1.5시간** |
| B | B-1 OnHitEffectRegistry | 1시간 |
| B | B-2 EnemyStatusEffect | 45분 |
| B | B-3 SetEffectApplicator OnHit case 교체 | 15분 |
| B | B-4 BuildSet 2 SO Tiers (Editor) | 30분 |
| B | B-5 Player wiring | 10분 |
| B | B-6 단위 B 검증 | 30분 |
| **B 합계** | | **약 3.5시간** |
| **총 합계** | | **약 5시간** |

작업 순서: **A 완료 → A 검증 → A 커밋 → B 진행 → B 검증 → B 커밋**.
