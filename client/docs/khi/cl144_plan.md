# CL-144 — 자동 발동 미소녀 1~4스택 (floating + 자동 타게팅 + 공격)

## Context

Epic S Phase 3의 세 번째 ticket. **게임 정체성 핵심 시스템 — 미소녀 자동 공격**.

회의록 결정사항 (재확인):
- 미소녀가 **floating 형태**로 캐릭터 주변에 떠다님
- **자동 공격 + 자동 타게팅**
- **공격 모션만 보여주고** 타게팅은 알아서
- **5합체는 별도 ticket (CL-145)**

본 CL 범위: **1~4스택만** (5합체 제외).

CL-140의 TODO 채우기:
```csharp
case RelicEffectType.MagicalGirlSummon:
    // CL-144: 미소녀 카운트 적용
    magicalGirlSpawner.SetCount(tier+1);
    break;
case RelicEffectType.MagicalGirlElementalAttack:
    // CL-144: 속성 미소녀 표시 (시각만, 메커니즘은 일반과 동일)
    magicalGirlSpawner.RegisterElementalVariant(secondaryTag);
    break;
```

---

## 결정사항

### 1. 미소녀 위치 — 플레이어 주변 원형 배치

**옵션**:
- (a) 일렬 (위에 가로로)
- (b) **원형 배치** ⭐
- (c) 무작위 위치

**채택: (b) 원형**.
- 1명: 머리 위 (0°)
- 2명: 위·아래 (0°, 180°)
- 3명: 삼각형 (0°, 120°, 240°)
- 4명: 사각형 (0°, 90°, 180°, 270°)
- 반경 1.5 유닛
- 플레이어가 움직이면 따라옴 (child Transform)

```csharp
// 원형 배치 계산
private Vector3 GetSlotOffset(int slotIndex, int totalSlots)
{
    float angle = (360f / totalSlots) * slotIndex;
    float rad = angle * Mathf.Deg2Rad;
    return new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * 1.5f;
}
```

### 2. 공격 메커니즘 — 자동 + 간격

**파라미터**:
- 자동 타게팅: 가장 가까운 적 (반경 5 유닛 내)
- 공격 간격: **1.5초**
- 데미지 = `플레이어 공격력 × 0.30` (각 미소녀 1명당)
- 사거리 외 적 → 대기 (공격 안 함)

**타게팅 로직**:
```csharp
private Health FindClosestEnemy()
{
    var hits = Physics2D.OverlapCircleAll(transform.position, 5f, enemyLayers);
    Health closest = null;
    float minDist = float.MaxValue;
    foreach (var hit in hits)
    {
        var h = hit.GetComponentInParent<Health>();
        if (h == null || h.CurrentHealth <= 0) continue;
        float dist = Vector2.Distance(transform.position, h.transform.position);
        if (dist < minDist) { minDist = dist; closest = h; }
    }
    return closest;
}
```

**공격 실행**:
- 1.5초마다 FindClosestEnemy → Damage(playerAttack × 0.30)
- 또는 투사체 발사 (시각 풍부, 단 복잡)
- **MVP: 즉시 데미지 적용** (투사체 X)

### 3. 데미지 스케일링

- 본인 공격력 = `combat.WeaponData.BaseDamage × statContainer.GetTotalMultiplier(StatId.AttackPower)`
- 미소녀 데미지 = 본인 공격력 × **0.30**
- 일반뎀 빌드와 시너지 (미소녀도 같이 강해짐)
- 치명타 적용? — **MVP는 X** (미소녀 공격은 치명타 안 됨, 단순)

**대안**: BuildSetData_미소녀.asset의 magnitude로 데미지 비율 조절 가능
- 티어 1 (1스택): 0.20 (20%)
- 티어 2 (2스택): 0.25
- 티어 3 (3스택): 0.30
- 티어 4 (4스택): 0.35
- → 스택 늘수록 개별 미소녀 강함

**채택**: 단순 0.30 고정 (티어 무관). 향후 밸런스 후 조정.

### 4. 속성 미소녀 처리 — MVP는 시각만

회의록 + 사용자 결정 (방향 A 시각만):
- [미소녀+불] = 별 모양 단추 → "화염 공격" → **시각만 빨간색**
- [미소녀+얼음] = 얼음 결정 → **시각만 파란색**
- [미소녀+전기] = 전기 안경 → **시각만 노란색**
- [미소녀+바람] = 바람의 깃털 → **시각만 초록색**
- [미소녀+체력] = 빛의 미소녀 → **시각만 흰색**
- [미소녀+치명타] = 어둠의 미소녀 → **시각만 검은색**
- [미소녀+범위] = 분홍 리본 → **시각 분홍 (기본)**

메커니즘은 모두 동일 (data-driven 색상만). 실제 속성 효과 (BurnOnHit 등)는 다른 set에서 트리거.

**구현**:
```csharp
public enum MagicalGirlVisual { Default, Fire, Ice, Lightning, Wind, Light, Dark }

private void AssignVisualByItems()
{
    // 인벤토리에서 미소녀 + 속성 아이템 우선순위로 색상 결정
    // 1. 미소녀+불 → Fire
    // 2. 미소녀+얼음 → Ice
    // ... (등급순 또는 획득 순)
    // 없으면 Default
}
```

**MVP 단순**: 마지막 획득한 속성 미소녀 아이템의 색상으로 모두 통일.

### 5. 카운트 변경 처리

- 0 → N: N명 spawn
- N → M (N < M): (M-N)명 추가 spawn, 위치 재배치
- N → M (N > M): (N-M)명 despawn, 위치 재배치
- N → 0: 모두 despawn

```csharp
public void SetCount(int newCount)
{
    while (_girls.Count < newCount) Spawn();
    while (_girls.Count > newCount) Despawn();
    RebalancePositions();
}
```

### 6. 시각 (Placeholder)

- **Sprite**: 단순 원형 또는 별 모양 (placeholder)
- **공격 모션**: 팔 흔들기 또는 빛나기 (placeholder)
- **사망**: despawn 시 fade-out 또는 즉시 사라짐
- 정식 미소녀 도트는 별도 ticket (회의록의 "미소녀 5개 도트 찍어주세요")

### 7. 미소녀가 밟는 충돌

- 미소녀는 **trigger only** (적과 물리 충돌 X)
- 적이 미소녀를 통과 가능
- 미소녀는 적에게 데미지만 입힘 (피격당하지 않음)

### 8. 무적 / 사망 처리

- 미소녀는 사망 안 함 (HP 무한)
- 적의 공격에 영향 X
- → 단순화. 향후 "미소녀 잃을 위험" 메커니즘 추가 가능 (별도 ticket)

---

## 핵심 파일

### 신규

| 경로 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlSpawner.cs` | spawner 컴포넌트 (Player에 부착) |
| `Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlAI.cs` | 개별 미소녀 AI (각 girl prefab에 부착) |
| `Assets/_Project/Prefabs/MagicalGirl/MagicalGirl.prefab` | placeholder 미소녀 prefab |
| `Assets/_Project/ScriptableObjects/BuildSets/BuildSet_미소녀.asset` | 미소녀 set 데이터 |

### 수정

| 경로 | 변경 내용 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Relics/SetEffectApplicator.cs` (CL-140) | `MagicalGirlSummon`, `MagicalGirlElementalAttack` case 채움 |

### 참조

| 경로 | 사용 |
|---|---|
| `BuildManager` (CL-139) | tier 변화 이벤트 (간접) |
| `KhiMeleeComboController` | 플레이어 공격력 multiplier 조회 |
| `PlayerStatModifierContainer` | StatId.AttackPower |
| `Health` (TDE) | 적 데미지 적용 |

---

## 구현 단계

### 1단계: MagicalGirl prefab 작성 (30분)

1. 빈 GameObject → `MagicalGirl.prefab`
2. 컴포넌트:
   - SpriteRenderer (placeholder sprite, 분홍 원형)
   - MagicalGirlAI 컴포넌트
   - (선택) ParticleSystem (공격 시 발광)
3. Layer = "MagicalGirl" 또는 "Player Allied" (적과 충돌 X 설정)

### 2단계: MagicalGirlAI 컴포넌트 (1시간)

```csharp
public class MagicalGirlAI : MonoBehaviour
{
    [SerializeField] private float attackInterval = 1.5f;
    [SerializeField] private float attackRange = 5f;
    [SerializeField] private float damageRatio = 0.30f;
    [SerializeField] private LayerMask enemyLayers;
    [SerializeField] private SpriteRenderer sprite;

    private float _nextAttackAt;
    private MagicalGirlVisual _visual = MagicalGirlVisual.Default;
    private PlayerStatModifierContainer _playerStat;
    private KhiMeleeComboController _playerCombat;

    public void Init(PlayerStatModifierContainer stat, KhiMeleeComboController combat)
    {
        _playerStat = stat;
        _playerCombat = combat;
    }

    public void SetVisual(MagicalGirlVisual visual)
    {
        _visual = visual;
        sprite.color = GetColorForVisual(visual);
    }

    private void Update()
    {
        if (Time.time < _nextAttackAt) return;
        Health target = FindClosestEnemy();
        if (target == null) return;

        Attack(target);
        _nextAttackAt = Time.time + attackInterval;
    }

    private void Attack(Health target)
    {
        float playerAtk = _playerCombat.WeaponData.BaseDamage
                        * _playerStat.GetTotalMultiplier(StatId.AttackPower);
        float damage = playerAtk * damageRatio;
        target.Damage(damage, gameObject, ...);
        // VFX: sprite flash 또는 ParticleSystem.Play
    }
}
```

### 3단계: MagicalGirlSpawner (1시간)

```csharp
public class MagicalGirlSpawner : MonoBehaviour
{
    [SerializeField] private GameObject magicalGirlPrefab;
    [SerializeField] private Transform anchor;  // 플레이어 transform
    [SerializeField] private PlayerStatModifierContainer playerStat;
    [SerializeField] private KhiMeleeComboController playerCombat;
    [SerializeField] private float ringRadius = 1.5f;

    private readonly List<MagicalGirlAI> _girls = new();
    private MagicalGirlVisual _currentVisual = MagicalGirlVisual.Default;

    public void SetCount(int newCount)
    {
        while (_girls.Count < newCount) Spawn();
        while (_girls.Count > newCount) Despawn();
        RebalancePositions();
    }

    public void RegisterElementalVariant(RelicTag elementTag)
    {
        var visual = ConvertTagToVisual(elementTag);
        _currentVisual = visual;
        // 모든 기존 girls 색상 갱신
        foreach (var g in _girls) g.SetVisual(visual);
    }

    private void Spawn()
    {
        var go = Instantiate(magicalGirlPrefab, anchor);
        var ai = go.GetComponent<MagicalGirlAI>();
        ai.Init(playerStat, playerCombat);
        ai.SetVisual(_currentVisual);
        _girls.Add(ai);
    }

    private void Despawn()
    {
        if (_girls.Count == 0) return;
        var last = _girls[^1];
        _girls.RemoveAt(_girls.Count - 1);
        Destroy(last.gameObject);
    }

    private void RebalancePositions()
    {
        for (int i = 0; i < _girls.Count; i++)
        {
            float angle = (360f / _girls.Count) * i * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * ringRadius;
            _girls[i].transform.localPosition = offset;
        }
    }
}
```

### 4단계: SetEffectApplicator 통합 (15분)

```csharp
case RelicEffectType.MagicalGirlSummon:
    int targetCount = tier.RequiredCount + (int)tier.Magnitude;
    // RequiredCount는 set tier의 필요 카운트 (1, 2, 3, 4)
    // Magnitude는 추가 미소녀 (예: 합체 부적이 +2면 +2)
    magicalGirlSpawner.SetCount(targetCount);
    break;
case RelicEffectType.MagicalGirlElementalAttack:
    magicalGirlSpawner.RegisterElementalVariant(/* secondary tag from RelicData */);
    break;
case RelicEffectType.MagicalGirlElementalEnhanced:
    magicalGirlSpawner.RegisterElementalVariant(/* secondary tag */);
    // 강화 표시는 후속 (CL-144 범위 외)
    break;
```

⚠️ **MagicalGirlSummon 의 magnitude 처리 주의**:
- 회의록: 1티어 1개, 2티어 2개, ... 4티어 4개
- BuildSetData.SetTier.RequiredCount = 1/2/3/4
- 추가로 합체 부적 (미소녀 +2) 처리는 어떻게? → `MagicalGirlSummon` magnitude로 추가 카운트
- BuildManager가 이 magnitude를 sum해서 SetEffectApplicator에 전달 필요

→ **CL-139 (BuildManager) 추가 보완 필요**: 미소녀 카운트는 보유 아이템들의 magnitude 합산.

### 5단계: BuildSet_미소녀.asset 작성 (15분)

| 티어 | RequiredCount | EffectType | Magnitude |
|---|---|---|---|
| 1 | 1 | MagicalGirlSummon | 1 |
| 2 | 2 | MagicalGirlSummon | 2 |
| 3 | 3 | MagicalGirlSummon | 3 |
| 4 | 4 | MagicalGirlSummon | 4 |
| 5 | 5 | MagicalGirlFusion | (CL-145에서) |

→ Magnitude = 미소녀 수. 단순.

### 6단계: 인게임 검증 (45분)

```
시나리오 1: 단순 카운트
- 미소녀 아이템 1개 추가 → 분홍 리본 1명 등장 (머리 위)
- 2개 → 2명 (양 옆)
- 3개 → 3명 (삼각형)
- 4개 → 4명 (사각형)

시나리오 2: 적 자동 공격
- 적 근처 → 1.5초마다 미소녀가 공격
- 적 멀어지면 공격 안 함

시나리오 3: 속성 시각
- 별 모양 단추 [미소녀+불] 추가 → 미소녀 빨간색
- 얼음 결정 [미소녀+얼음] 추가 → 미소녀 파란색 (가장 최근 색)

시나리오 4: 카운트 변화
- 4명 상태 → 아이템 1개 제거 → 3명 (재배치)
- 5합체 임계치 도달 → CL-145에서 처리 (본 CL은 4까지만)

시나리오 5: 데미지 스케일링
- 일반뎀 빌드 추가 (공격력 +30%) → 미소녀 데미지도 +30%
```

---

## 위험 / 결정 미정

### 위험
1. **미소녀 카운트 누적 처리**: 보유 아이템들의 미소녀 magnitude 합 계산 필요. CL-139 BuildManager가 단일 set tier의 RequiredCount만 알면 부족 → magnitude 합산 로직 추가.
2. **Layer 충돌**: 미소녀와 적 collider 설정 잘못되면 미소녀가 막힘. → Trigger 처리 + Layer 분리.
3. **성능**: 4명 미소녀 × Update 매프레임 → 미미. 단 OverlapCircleAll 매프레임 호출은 부담 → 0.1초마다 (10Hz) 체크로 최적화.
4. **미소녀 prefab 누락**: SerializeField 미할당 시 NRE → Awake 검증.
5. **속성 시각 단순화**: 모든 girls가 마지막 획득 색상 → 사용자 혼란 ("왜 다 같은 색?"). MVP 한계 명시.

### 결정 미정
- [ ] 데미지 스케일링 (0.30 vs 티어별 변동) — **0.30 고정** (MVP)
- [ ] 미소녀 색상 정책 (마지막 획득 vs 등급순 vs 첫 획득) — **마지막 획득** (단순)
- [ ] 공격 시각 (sprite flash vs ParticleSystem vs 투사체) — **sprite flash** (placeholder)
- [ ] 미소녀 사거리 (5 유닛) — 일단 5, 밸런스 후 조정
- [ ] 미소녀가 치명타 가능? — **MVP X**, 후속 검토

---

## 후속 ticket 영향

| Ticket | CL-144와의 관계 |
|---|---|
| **CL-145 (5합체)** | 본 CL의 spawner.SetCount(5) 호출 시 → fusion 모드로 전환. 본 CL 인프라 활용 |
| **CL-146 (공통 7세트)** | 무관 |
| **CL-147 (타로)** | 무관 |
| **별도 ticket: 미소녀 도트 디자인** | placeholder sprite를 정식 도트로 교체 |
| **별도 ticket: 속성 미소녀 메커니즘** | 시각만 → 실제 효과 (BurnOnHit 등 트리거) 확장 |

---

## 예상 시간

| 단계 | 시간 |
|---|---|
| 1단계 (prefab) | 30분 |
| 2단계 (MagicalGirlAI) | 1시간 |
| 3단계 (MagicalGirlSpawner) | 1시간 |
| 4단계 (SetEffectApplicator 통합) | 15분 |
| 5단계 (BuildSet_미소녀) | 15분 |
| 6단계 (검증) | 45분 |
| **합계** | **약 3시간 45분** (티켓 점수 3점에 부합) |

---

## 결정 요청

| # | 질문 | 옵션 | 추천 |
|---|---|---|---|
| 1 | 위치 | 일렬 / **원형** / 무작위 | **원형** |
| 2 | 공격 간격 | 1.0 / **1.5** / 2.0초 | **1.5** |
| 3 | 사거리 | 3 / **5** / 7 유닛 | **5** |
| 4 | 데미지 비율 | 0.20 / **0.30** / 0.50 | **0.30** |
| 5 | 속성 시각 | 마지막 획득 / 등급순 / 균등 분포 | **마지막 획득** |
| 6 | 사망 가능? | YES / **NO (무적)** | **NO** |
| 7 | 치명타 적용? | YES / **NO** | **NO** |

전부 추천대로면 **원형 + 1.5초 + 5유닛 + 0.30 + 마지막획득 + 무적 + 치명타X**.

---

## 다음 plan

CL-144 OK 하시면:
- **CL-145** (미소녀 5합체) — 본 CL 인프라 활용. 합체 시 4명 미소녀 → 1개 fusion 엔티티 (레이저/광역). 3점.

추천: **CL-145 바로 진행** (미소녀 시스템 완성).
