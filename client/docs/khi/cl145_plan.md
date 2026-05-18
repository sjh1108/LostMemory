# CL-145 — 미소녀 5스택 합체 (레이저 + 전역 범위 공격)

## Context

Epic S Phase 3의 네 번째 ticket. **CL-144 미소녀 시스템 완성**.

회의록 결정사항:
- 미소녀 5개 완성 시 → **마법미소녀 등장** ("많이 쎄요")
- 5번 = **엑조디아** (게임체인저급 보상)
- **레이저 폼 / 범위기 두 가지 패턴**
- **닿으면 죽을딜** (매우 강력)
- 사용자 추가: 레이저 = 오버워치 모이라 궁 같은 느낌 / 전역 = 화면 전체 범위

CL-144 인프라:
- ✅ MagicalGirlSpawner (1~4 관리)
- ✅ MagicalGirlAI (개별 미소녀 행동)

본 CL 책임: **5스택 도달 시 합체 모드 전환** + **2가지 강력 공격 패턴 구현**.

---

## 결정사항

### 1. 합체 시 시각적 변화

**옵션**:
- (a) 4명 미소녀 → **사라지고 1개 큰 fusion 엔티티 등장** ⭐
- (b) 4명이 모여서 변형되는 애니메이션 → fusion
- (c) 4명 그대로 + 추가 fusion 효과 (4 + 1)

**채택: (a)**.
- MVP 단순
- 시각적으로 "변신했다"는 임팩트 강함
- Spawn/despawn 기존 인프라 그대로 활용

**MVP 구현**:
- 5스택 도달 → `MagicalGirlSpawner.SetCount(5)` 받음
- Spawner 내부:
  - 기존 1~4 미소녀 despawn
  - Fusion 엔티티 1개 spawn (별도 prefab)
- 5스택 미만 됨:
  - Fusion despawn
  - 1~4 미소녀 다시 spawn

### 2. Fusion 엔티티 설계

**컴포넌트**:
- `MagicalGirlFusion` (개별 AI와 별개 컴포넌트)
- 위치: 플레이어 머리 위 (단일, 원형 배치 X)
- 크기: 일반 미소녀의 1.5~2배

**공격 패턴 2가지**:
- **레이저** (Pattern A)
- **전역 범위** (Pattern B)

### 3. 패턴 A: 레이저 (모이라 궁 풍)

**메커니즘**:
- 플레이어 → 마우스 방향 (또는 가장 가까운 적 방향)으로 **연속 빔 발사**
- 빔 길이: **10 유닛** (화면 끝까지)
- 빔 두께: **1 유닛**
- 빔에 닿은 모든 적에게 **0.1초마다 데미지 틱**
- 틱당 데미지: **플레이어 공격력 × 0.5** (강력)
- 지속: **3초**
- 쿨다운: **5초**

**구현**:
```csharp
private void FireLaser()
{
    Vector2 dir = GetTargetDirection();  // mouse or closest enemy
    Vector2 end = (Vector2)transform.position + dir * 10f;
    
    // 매 0.1초마다 빔 라인캐스트 + 데미지
    StartCoroutine(LaserCoroutine(transform.position, end, duration: 3f));
}

private IEnumerator LaserCoroutine(Vector2 start, Vector2 end, float duration)
{
    float endsAt = Time.time + duration;
    while (Time.time < endsAt)
    {
        var hits = Physics2D.OverlapBoxAll(midpoint, new Vector2(10, 1), angle, enemyLayers);
        foreach (var hit in hits) hit.Damage(playerAtk * 0.5f, ...);
        // 시각: LineRenderer 갱신
        yield return new WaitForSeconds(0.1f);
    }
}
```

**시각**:
- LineRenderer 컴포넌트 + 빛나는 머티리얼 (placeholder는 단색)
- 정식 VFX는 후속 ticket

**사거리·각도 결정**:
- **자동 타게팅** (가장 가까운 적 방향) — MVP 단순
- 또는 **마우스 조준** — 플레이어 입력 의미 있음
- → **MVP: 자동 타게팅** (미소녀 자체가 자동 시스템 컨셉)

### 4. 패턴 B: 전역 범위

**메커니즘**:
- 화면 전체 적에게 **즉발 광역 데미지**
- 데미지: **플레이어 공격력 × 3.0** (한 번에 약한 적 즉사)
- 쿨다운: **8초**
- 시각: 화면 전체 플래시 + 적 위치에 폭발 이펙트

**구현**:
```csharp
private void FireGlobalAOE()
{
    Camera cam = Camera.main;
    Vector2 viewportMin = cam.ViewportToWorldPoint(Vector2.zero);
    Vector2 viewportMax = cam.ViewportToWorldPoint(Vector2.one);
    Vector2 size = viewportMax - viewportMin;
    Vector2 center = (viewportMin + viewportMax) * 0.5f;

    var hits = Physics2D.OverlapBoxAll(center, size, 0, enemyLayers);
    foreach (var hit in hits) hit.Damage(playerAtk * 3.0f, ...);

    // 시각: 화면 전체 플래시 (Image/Canvas 흰색 0.2초)
    StartCoroutine(ScreenFlash());
}
```

**시각**:
- 화면 전체 white flash (Canvas Image alpha 0.5 → 0)
- 적 사망 이펙트 (TDE 기본)

### 5. 패턴 선택 규칙

**옵션**:
- (a) 무작위 50/50
- (b) **번갈아** (레이저 → AOE → 레이저 → ...) ⭐
- (c) 상황별 (적 5+ → AOE / 그 외 → 레이저)
- (d) 둘 다 동시 (강력하지만 복잡)

**채택: (b) 번갈아**. 단순 + 예측 가능 + 각 패턴 매번 발동.

**전환 로직**:
```csharp
private bool _useLaserNext = true;

private void TryAttack()
{
    if (Time.time < _nextAttackAt) return;

    if (_useLaserNext) { FireLaser(); _nextAttackAt = Time.time + 5f; }
    else              { FireGlobalAOE(); _nextAttackAt = Time.time + 8f; }
    _useLaserNext = !_useLaserNext;
}
```

### 6. Fusion 활성/비활성 전환

**활성화** (count 5 도달):
1. Spawner: 기존 1~4 despawn
2. Spawner: MagicalGirlFusion prefab 1개 spawn (player anchor)
3. Fusion 자동 공격 시작 (Update에서)

**비활성화** (count 5 미만):
1. Spawner: Fusion despawn
2. Spawner: 새 count만큼 일반 미소녀 spawn (1~4)

**전환 시 cooldown 처리**:
- Fusion이 공격 진행 중에 비활성화? → 즉시 중단 (코루틴 stop)
- 진행 중 데미지는 적용된 만큼 유지

### 7. 데미지 스케일링

회의록 "닿으면 죽을딜":
- 일반 적 (HP 100~200) → 레이저 1틱에 죽음 (0.5 × 100 = 50, 2틱에 죽음 정도)
- 보스 (HP 5000+) → 시간 걸리지만 의미 있는 DPS
- AOE 1발에 약한 적 즉사

**조정 가능**:
- BuildSetData_미소녀.asset 5티어의 Magnitude로 데미지 비율 결정
- 레이저: magnitude 기본 0.5
- AOE: magnitude 기본 3.0
- 또는 별도 SetTier 필드 (LaserDamageRatio, AOEDamageRatio)

**MVP**: SetTier.Magnitude = 5티어 합체 표시 (값 자체는 의미 X), 실제 데미지는 코드 상수로.

### 8. EffectType 처리

CL-138에서 `MagicalGirlFusion` enum 추가됨.

```csharp
// SetEffectApplicator (CL-140)
case RelicEffectType.MagicalGirlFusion:
    magicalGirlSpawner.TriggerFusion();  // 새 메서드
    break;
```

`MagicalGirlSpawner.TriggerFusion()`:
```csharp
public void TriggerFusion()
{
    // 모든 일반 미소녀 despawn
    foreach (var g in _girls) Destroy(g.gameObject);
    _girls.Clear();

    // Fusion 엔티티 spawn
    _fusionInstance = Instantiate(fusionPrefab, anchor);
    _fusionInstance.GetComponent<MagicalGirlFusion>().Init(playerStat, playerCombat);
}

public void EndFusion()
{
    if (_fusionInstance != null)
    {
        Destroy(_fusionInstance);
        _fusionInstance = null;
    }
    // 이후 SetCount(N) 호출로 일반 미소녀 재소환
}
```

### 9. BuildSet_미소녀.asset 5티어 추가

CL-144에서 1~4티어만 작성. 본 CL에서 5티어 추가:

| 티어 | RequiredCount | EffectType | Magnitude |
|---|---|---|---|
| 1 | 1 | MagicalGirlSummon | 1 |
| 2 | 2 | MagicalGirlSummon | 2 |
| 3 | 3 | MagicalGirlSummon | 3 |
| 4 | 4 | MagicalGirlSummon | 4 |
| **5** | **5** | **MagicalGirlFusion** | **1** |

---

## 핵심 파일

### 신규

| 경로 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlFusion.cs` | Fusion AI (레이저 + AOE) |
| `Assets/_Project/Prefabs/MagicalGirl/MagicalGirlFusion.prefab` | Fusion placeholder |
| (선택) `Assets/_Project/UI/ScreenFlashCanvas.prefab` | 전역 AOE 시각용 |

### 수정

| 경로 | 변경 내용 |
|---|---|
| `Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlSpawner.cs` (CL-144) | `TriggerFusion()`, `EndFusion()` 메서드 추가 |
| `Assets/_Project/Scripts/Runtime/Relics/SetEffectApplicator.cs` (CL-140) | `MagicalGirlFusion` case 채움 |
| `Assets/_Project/ScriptableObjects/BuildSets/BuildSet_미소녀.asset` (CL-144) | 5티어 추가 |

---

## 구현 단계

### 1단계: MagicalGirlFusion.prefab (30분)

1. 빈 GameObject → `MagicalGirlFusion.prefab`
2. 컴포넌트:
   - SpriteRenderer (placeholder, 큰 분홍 별)
   - LineRenderer (레이저 시각용)
   - MagicalGirlFusion 스크립트
3. Layer = "MagicalGirl" (CL-144와 동일)

### 2단계: MagicalGirlFusion 컴포넌트 (1.5시간)

```csharp
public class MagicalGirlFusion : MonoBehaviour
{
    [SerializeField] private LineRenderer laserLine;
    [SerializeField] private float laserDuration = 3f;
    [SerializeField] private float laserCooldown = 5f;
    [SerializeField] private float laserDamageRatio = 0.5f;
    [SerializeField] private float aoeCooldown = 8f;
    [SerializeField] private float aoeDamageRatio = 3.0f;
    [SerializeField] private LayerMask enemyLayers;

    private bool _useLaserNext = true;
    private float _nextAttackAt;
    private PlayerStatModifierContainer _stat;
    private KhiMeleeComboController _combat;

    public void Init(PlayerStatModifierContainer stat, KhiMeleeComboController combat) { ... }

    private void Update()
    {
        if (Time.time < _nextAttackAt) return;
        if (_useLaserNext) FireLaser();
        else               FireGlobalAOE();
        _useLaserNext = !_useLaserNext;
    }

    private void FireLaser() { /* 위 §3 */ }
    private void FireGlobalAOE() { /* 위 §4 */ }
}
```

### 3단계: MagicalGirlSpawner 확장 (30분)

`TriggerFusion()`, `EndFusion()` 메서드 추가. SetCount(5)일 때 자동 fusion 모드:

```csharp
public void SetCount(int newCount)
{
    if (newCount >= 5)
    {
        if (_fusionInstance == null) TriggerFusion();
    }
    else
    {
        if (_fusionInstance != null) EndFusion();
        // 일반 미소녀 1~4 재소환
        while (_girls.Count < newCount) Spawn();
        while (_girls.Count > newCount) Despawn();
        RebalancePositions();
    }
}
```

### 4단계: SetEffectApplicator MagicalGirlFusion 처리 (15분)

```csharp
case RelicEffectType.MagicalGirlFusion:
    magicalGirlSpawner.SetCount(5);  // 5는 fusion 트리거
    break;
```

또는 별도 명시적 호출:
```csharp
case RelicEffectType.MagicalGirlFusion:
    magicalGirlSpawner.TriggerFusion();
    break;
```

후자가 명확. 선택.

### 5단계: BuildSet_미소녀.asset 5티어 (5분)

Inspector에서 5티어 추가:
- RequiredCount: 5
- EffectType: MagicalGirlFusion
- Magnitude: 1

### 6단계: 화면 플래시 (선택, 30분)

전역 AOE 시각:
- Canvas (Screen Space) + Image (white, alpha 0)
- AOE 발동 시 alpha 0.5 → 0 (DOTween 또는 Coroutine)

```csharp
private IEnumerator ScreenFlash()
{
    flashImage.color = new Color(1, 1, 1, 0.5f);
    float duration = 0.2f;
    float t = 0;
    while (t < duration)
    {
        t += Time.deltaTime;
        flashImage.color = new Color(1, 1, 1, Mathf.Lerp(0.5f, 0f, t / duration));
        yield return null;
    }
}
```

### 7단계: 인게임 검증 (45분)

```
시나리오 1: 합체 발동
- 미소녀 아이템 5개 모음 → 1~4명 사라짐 + Fusion 등장
- 큰 sprite, 머리 위 위치

시나리오 2: 레이저
- Fusion 활성 + 적 근처 → 레이저 발동
- 빔 따라 적들 데미지
- 3초 지속, 5초 쿨다운

시나리오 3: 전역 AOE
- 다음 공격 사이클 → 화면 플래시 + 모든 적 데미지
- 약한 적 즉사 확인

시나리오 4: 패턴 번갈아
- 레이저 → AOE → 레이저 → AOE 순환

시나리오 5: 합체 해제
- 미소녀 아이템 1개 제거 → 4명으로 → Fusion 사라지고 일반 미소녀 4명 재등장
- 다시 5개 모으면 Fusion 재등장

시나리오 6: 데미지 검증
- 보스 (HP 5000) 상대로 레이저 3초 → 약 1500 데미지 (0.5 × playerAtk × 30틱)
- AOE 1번 → 약 300 데미지 (3.0 × 100 baseAtk)
- 일반뎀 빌드 시 데미지 비례 증가
```

---

## 위험 / 결정 미정

### 위험
1. **레이저 무한 루프**: 코루틴이 SetCount(5 미만) 시 안 멈춤? → EndFusion()에서 모든 코루틴 StopAllCoroutines() 호출 필요.
2. **AOE 화면 범위 vs 절대 좌표**: Camera 기반이라 카메라 줌 변경 시 영향. → 카메라 고정 가정 (현재 게임 스타일).
3. **데미지 너무 강함**: 보스도 즉살 우려. → MVP 검증 후 조정.
4. **Fusion sprite 가림**: 너무 크면 플레이어 보임 안 됨. → 반투명 또는 sprite 크기 조절.
5. **합체 진입 컷씬 부재**: 5스택 도달이 게임적으로 임팩트 큰 순간인데 시각적 강조 없음. → 별도 ticket (시각 폴리싱).

### 결정 미정
- [ ] 패턴 선택 (랜덤 / **번갈아** / 상황별) — **번갈아 추천**
- [ ] 레이저 타게팅 (자동 / 마우스) — **자동 추천**
- [ ] AOE 데미지 비율 (2.0 / **3.0** / 5.0) — **3.0**
- [ ] Fusion 시각 크기 (1.5x / **2x** / 3x) — **2x**
- [ ] 합체 진입 컷씬 / 임팩트 효과 — 별도 ticket
- [ ] 합체 BGM 변화 — 별도 ticket

---

## 후속 ticket 영향

| Ticket | CL-145와의 관계 |
|---|---|
| **CL-146 (공통 7세트)** | 무관 |
| **CL-147 (타로)** | 무관 |
| **별도 ticket: 미소녀 도트** | Fusion sprite도 정식 도트 필요 |
| **별도 ticket: 합체 컷씬** | 5스택 도달 시 짧은 정지 + 이펙트 강조 |
| **CL-153 (QA)** | Fusion 검증 시나리오 |

---

## 예상 시간

| 단계 | 시간 |
|---|---|
| 1단계 (Fusion prefab) | 30분 |
| 2단계 (MagicalGirlFusion 컴포넌트) | 1.5시간 |
| 3단계 (Spawner 확장) | 30분 |
| 4단계 (SetEffectApplicator) | 15분 |
| 5단계 (BuildSet_미소녀 5티어) | 5분 |
| 6단계 (화면 플래시, 선택) | 30분 |
| 7단계 (검증) | 45분 |
| **합계** | **약 4시간** (티켓 점수 3점에 부합) |

---

## 결정 요청

| # | 질문 | 옵션 | 추천 |
|---|---|---|---|
| 1 | 합체 시각 변환 | (a) 4 사라지고 fusion 1 / (b) 변신 애니 / (c) 4+1 | **(a)** |
| 2 | 패턴 선택 | 랜덤 / **번갈아** / 상황별 | **번갈아** |
| 3 | 레이저 타게팅 | **자동 (가장 가까운 적)** / 마우스 | **자동** |
| 4 | 레이저 데미지 비율 | 0.3 / **0.5** / 0.8 | **0.5** |
| 5 | AOE 데미지 비율 | 2.0 / **3.0** / 5.0 | **3.0** |
| 6 | 화면 플래시 효과? | YES / NO | **YES** (시각 임팩트) |
| 7 | Fusion sprite 크기 | 1.5x / **2x** / 3x | **2x** |

전부 추천대로면 **단순 fusion + 번갈아 + 자동 타게팅 + 0.5/3.0 데미지 + 플래시 + 2x 크기**.

---

## Phase 3 진행률 (CL-145 후)

| Ticket | Plan |
|---|---|
| CL-142 평타 5세트 | ✅ |
| CL-143 스킬 3세트 | ✅ |
| CL-144 미소녀 1~4 | ✅ |
| **CL-145 미소녀 5합체** | ✅ ← 방금 |
| CL-146 공통 7세트 | ⏳ 다음 |
| CL-147 타로 | ⏳ P2 |

**Phase 3 진행률: 4/6 → 미소녀 시스템 완성**

---

## 다음 plan

| 옵션 | Ticket | 점수 | 특징 |
|---|---|---|---|
| **A** | CL-146 공통 7세트 | 3점 | 단순 stat 효과 묶음 (체력/방어/회피/범위/행운/탐욕/타로) — 가장 가벼움 |
| B | CL-147 타로 시스템 | 5점 P2 | 메이저 아르카나 8장. 별도 시스템 |
| C | Phase 4 진입 (CL-148~) | - | 인벤토리 UI 5×5 |

추천: **A** (CL-146). Phase 3 마무리 + 가장 단순. 그 다음 Phase 4 인벤토리 UI로.

뭐로 갈까요?
