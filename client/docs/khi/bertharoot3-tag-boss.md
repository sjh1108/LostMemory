---
name: BertharRoot3 Boss 태그 + 본체 sprite 식별 개선
status: planned
related: EnemyStatusEffect, MagicalGirlAOE, BerthaRoot3, OnHitEffectRegistry
---

# Context

미소녀(Magical Girl) AOE 공격이 적에 명중하면 [`EnemyStatusEffect`](../../LostMemory/Assets/_Project/Scripts/Runtime/Enemies/EnemyStatusEffect.cs)가 적의 본체 SpriteRenderer 색을 푸르게(슬로우) 또는 붉게(화상) 칠한다. 본체 sprite 를 식별하는 현재 로직 [`TryCacheEnemySprite()`](../../LostMemory/Assets/_Project/Scripts/Runtime/Enemies/EnemyStatusEffect.cs:303)는:

1. 자식 SpriteRenderer 전부 수집
2. 이름에 `shadow / vfx / effect / status / iceblock` 들어간 것 제외
3. 남은 것 중 **bounds 면적이 가장 큰 것** 선택

이 휴리스틱이 깨지기 쉬움 — 새 보조 sprite 가 들어올 때마다 블랙리스트 추가 필요, sprite 프레임에 따라 bounds 가 변해서 캐싱 시점에 잘못된 sprite 가 선택될 수 있음, 의도가 코드에 안 드러남.

추가로, **보스(Bertha)는 상태이상 자체가 적용되지 않아야 함** — 게임 디자인상 보스가 슬로우/빙결/화상 면역이어야 함. 현재는 일반 적과 동일하게 영향 받음.

**목표:**
- 본체 sprite 식별을 **Animator 가 붙은 GameObject 의 SpriteRenderer** 기준으로 변경 (현 휴리스틱은 fallback)
- **`Boss` 태그 검사**로 보스에 모든 상태이상(slow/freeze/burn) 적용 차단
- BerthaRoot3.prefab 의 root GameObject 는 이미 사용자가 `Boss` 태그로 설정 완료

# 핵심 사실 (탐색 결과)

- **`EnemyStatusEffect` 부착 위치**: [`OnHitEffectRegistry.GetOrAddStatus`](../../LostMemory/Assets/_Project/Scripts/Runtime/Combat/OnHitEffectRegistry.cs:454)와 [`MagicalGirlAOE.cs:104`](../../LostMemory/Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlAOE.cs:104) 모두 `victim.gameObject` (= `Health` 컴포넌트가 붙은 GO) 에 `AddComponent` 한다.
- **BerthaRoot3 hierarchy**:
  - `BerthaRoot3` (root, **태그 = Boss**) — `Health`, `AIBrain`, `Character` 부착
  - `Visual / BerthaSprite` — `Animator` + `SpriteRenderer` 동일 GO 에 부착 (Animator 기반 식별 정상 작동)
- **결론**: EnemyStatusEffect 는 BerthaRoot3 에 직접 붙으므로 `transform.CompareTag("Boss")` 한 번이면 충분. 부모 체인 walk 불필요.
- 일반 적([Orc_CL037.prefab](../../LostMemory/Assets/_Project/Prefabs/Enemies/Orc_CL037.prefab) 등)도 동일 컨벤션: Animator + SpriteRenderer 가 같은 GO (`OrcModel`) 에 있음.

# 변경 대상

## 1. `Boss` 태그 추가 (Unity Editor 작업, 사용자 몫)

- **Project Settings → Tags and Layers** 에 `Boss` 태그 추가
- BerthaRoot3.prefab 은 이미 적용됨 (사용자 확인 완료) — 추가 prefab 작업 없음
- 향후 다른 보스 추가 시 root GO 에 `Boss` 태그만 붙이면 자동으로 면역 처리됨

## 2. [`EnemyStatusEffect.cs`](../../LostMemory/Assets/_Project/Scripts/Runtime/Enemies/EnemyStatusEffect.cs) 수정

### 2-1. Boss 면역 처리

```csharp
// 클래스 필드 추가
private bool _isBoss;
private const string BossTag = "Boss";

// Awake() 끝부분에 추가
_isBoss = IsBossInParents();

private bool IsBossInParents()
{
    for (Transform t = transform; t != null; t = t.parent)
    {
        if (t.CompareTag(BossTag)) return true;
    }
    return false;
}
```

- 부모 체인 walk: BerthaRoot3 는 0단계라 지금 시점엔 오버킬이지만, 향후 보스 prefab 이 wrapper GO 안에 들어가는 경우 안전. 보스 prefab depth 는 얕아서 비용 무시 가능.

`ApplySlow` / `ApplyFreeze` / `ApplyBurn` 진입부에 early-return 추가:

```csharp
public void ApplySlow(float magnitude, float duration)
{
    if (_isBoss) return;
    // ...기존 로직
}

public void ApplyFreeze(float durationSeconds)
{
    if (_isBoss) return;
    // ...기존 로직
}

public void ApplyBurn(float damagePerTick, float duration, GameObject instigator)
{
    if (_isBoss) return;
    // ...기존 로직
}
```

- 효과: 슬로우(이속 감속) / 빙결(정지) / 화상(도트 데미지) / sprite tint / VFX 부착 전부 안 일어남

### 2-2. 본체 SpriteRenderer 식별 로직 변경

[`TryCacheEnemySprite()`](../../LostMemory/Assets/_Project/Scripts/Runtime/Enemies/EnemyStatusEffect.cs:303) 교체:

```csharp
private void TryCacheEnemySprite()
{
    if (_enemyColorCached) return;
    if (_isBoss) return;  // 보스는 캐싱 자체 불필요

    // 1순위: Animator 가 붙은 GameObject 의 SpriteRenderer
    Animator animator = GetComponentInChildren<Animator>(includeInactive: true);
    SpriteRenderer best = null;
    if (animator != null)
    {
        best = animator.GetComponent<SpriteRenderer>();
        // Animator GO 에 SpriteRenderer 가 직접 없으면 같은 GO + 자식만 추가 검색 (보수적 fallback)
        if (best == null)
            best = animator.GetComponentInChildren<SpriteRenderer>(includeInactive: true);
    }

    // 2순위 fallback: 기존 largest-bounds 휴리스틱
    if (best == null)
    {
        SpriteRenderer[] candidates = GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
        float bestArea = 0f;
        foreach (SpriteRenderer sr in candidates)
        {
            if (sr == null || sr.sprite == null) continue;
            string n = sr.gameObject.name;
            if (n.IndexOf("shadow", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (n.IndexOf("vfx", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (n.IndexOf("effect", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (n.IndexOf("status", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (n.IndexOf("iceblock", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;

            Bounds b = sr.bounds;
            float area = b.size.x * b.size.y;
            if (area > bestArea)
            {
                bestArea = area;
                best = sr;
            }
        }
    }

    if (best != null)
    {
        _enemyMainSprite = best;
        _enemyOriginalColor = best.color;
        _enemyColorCached = true;
    }
    else
    {
        Debug.LogWarning($"[EnemyStatusEffect] tint 적용할 SpriteRenderer 0개 (Animator 기반 + fallback 모두 실패) host={gameObject.name}");
    }
}
```

- Animator 우선이지만 못 찾으면 기존 로직 그대로 작동 → 기존 적 prefab 호환성 무손실
- `_isBoss` 인 경우 캐싱 스킵 (작은 최적화 + early-return 으로 어차피 안 쓰임)

# Verification

1. **빌드 확인**: Unity Editor 에서 컴파일 에러 없음 확인
2. **기존 적 회귀 테스트** (Orc_CL037 등):
   - 미소녀 AOE 로 일반 적 명중 → sprite 가 푸르게 변함 (slow tint)
   - 화염 데미지 → sprite 가 붉게 변함 (burn tint)
   - 빙결 → 적 정지 + Freeze VFX 부착
   - 보조 sprite (그림자, status VFX 등) 는 색 안 변함
3. **보스 면역 테스트** (Bertha 보스룸 진입):
   - 미소녀 AOE 로 Bertha 명중 → sprite 색 변화 없음, 이속 감소 없음
   - 화염 데미지 → 도트 데미지 안 들어감, sprite 색 변화 없음
   - 빙결 공격 → Bertha 정지 안 됨, Freeze VFX 안 붙음
   - 일반 데미지 (`Health.Damage`) 는 정상 적용 — 보스가 면역되는 건 *상태이상* 만
4. **로그 확인**: Console 에 `[EnemyStatusEffect] tint 적용할 SpriteRenderer 0개` 경고 발생 안 함

# Out of scope

- 새 보스 prefab 추가/태그 작업 (BerthaRoot3 외)
- 보스에 별도 면역 시스템 (해독/디버프 무효화 등) 도입 — 현재는 단순 차단만
- `EnemyStatusEffect` 의 다른 컴포넌트(VFX prefab 주입, tint 색 push) 변경
