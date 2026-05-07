# CL-202: OnHit VFX 정식 교체 — Freeze / Burn / Slow

## Context
상태이상 3종 시각화 정비:
- **Freeze** ([EnemyStatusEffect.cs:188](../../LostMemory/Assets/_Project/Scripts/Runtime/Enemies/EnemyStatusEffect.cs)): 현재 `Texture2D.whiteTexture` 청색 틴트 SpriteRenderer placeholder
- **Burn** ([EnemyStatusEffect.cs:236](../../LostMemory/Assets/_Project/Scripts/Runtime/Enemies/EnemyStatusEffect.cs)): 현재 동일 방식 주황 틴트 placeholder (코드 주석에 "CL-143 placeholder" 명시)
- **Slow**: 현재 시각 피드백 0 — 적이 느려졌는지 인지 불가

`EnemyStatusEffect`의 visual 메서드들을 정식 ParticleSystem + Sprite 혼합 VFX로 교체하고, Slow 시각화를 신규 추가한다. 동시 다중 상태이상 시 VFX 1개만 표시(가장 최근, 정책 D)하고 종료 시 페이드아웃으로 자연스럽게 정리한다.

## 결정 사항 (사용자 확정 + 권장안)
- **표현 방식**: C 혼합 — 정적 Sprite 베이스 + ParticleSystem 보조 ✓ 사용자 확정
- **Slow 위치**: A+B+C 통합 — 발 밑 안개 + 적 위 입자 + 발 밑 원판 ✓ 사용자 확정
- **종료 정리**: 0.2~0.3초 페이드아웃 ✓ 사용자 확정
- **색상**:
  - Freeze: 청 `(0.2, 0.5, 1.0)`
  - Burn: 빨강 `(1, 0.2, 0.1)`
  - Slow: 청록 `(0.5, 0.8, 1.0)` (권장 / 청회 원하시면 알려주세요)
- **동시 적용 정책**: **D — 효과는 모두 적용 / VFX는 가장 최근 1개만 표시** (사용자 의견 반영)
- **SFX / 히트스톱**: SFX 포함 / 히트스톱 제외 (권장 — OnHit Chain/Wind에서 이미 처리되므로 중복 회피)

## 작업 범위
- [ ] `FreezeStatusVFX.prefab` 제작 (IceBlock 정적 Sprite + Star 청색 입자)
- [ ] `BurnStatusVFX.prefab` 제작 (Fire 애니메이션 + Smoke + Light 입자)
- [ ] `SlowStatusVFX.prefab` 제작 (발 밑 안개 + 적 위 입자 + 발 밑 원판 통합)
- [ ] `EnemyStatusEffect.EnsureFreezeVisual()` 정식 VFX 프리팹 인스턴스화로 교체
- [ ] `EnemyStatusEffect.EnsureBurnVisual()` 정식 VFX 프리팹 인스턴스화로 교체
- [ ] `EnemyStatusEffect.EnsureSlowVisual()` 신규 추가
- [ ] `ApplySlow()` 호출 시점에 SetSlowVisualActive(true) 트리거
- [ ] 동시 적용 정책 D 구현 — 활성 visual 1개 관리 (`_activeVisualType` enum)
- [ ] 페이드아웃 구현 (코루틴 또는 VFXAutoDestroy 헬퍼)
- [ ] SFX 통합 (Freeze/Burn 적용 시점 1회)
- [ ] 인스펙터 와이어링 + 인게임 검증

## 신규 프리팹

### FreezeStatusVFX.prefab
경로: `_Project/Prefabs/VFX/FreezeStatusVFX.prefab`

구조:
```
FreezeStatusVFX (root, 적에게 SpawnAttached)
├── IceBlockBack (SpriteRenderer)
│   - Sprite: IceBlockBack.png
│   - Color: (0.2, 0.5, 1.0, 0.6) 청색
│   - SortingOrder: 8000 (적 뒤)
├── IceBlockFront (SpriteRenderer)
│   - Sprite: IceBlockFront.png
│   - Color: (0.4, 0.7, 1.0, 0.7) 밝은 청색
│   - SortingOrder: 9999 (적 앞)
└── CrystalParticles (ParticleSystem)
    - Texture: Star.png
    - Color: 청록 (0.4, 0.8, 1.0, 0.9)
    - Shape: Sphere, 적 주변 결정 입자 부유
    - Loop, 만료 시 외부에서 Stop() 호출
```

### BurnStatusVFX.prefab
경로: `_Project/Prefabs/VFX/BurnStatusVFX.prefab`

구조:
```
BurnStatusVFX (root, 적에게 SpawnAttached)
├── FireSprite (SpriteRenderer + Animator)
│   - Sprite Sheet: Fire.png (4프레임)
│   - Animator: 0.15초/프레임 루프
│   - Color: (1, 0.4, 0.2, 0.85) 빨강 톤
│   - SortingOrder: 9998
├── SmokeParticles (ParticleSystem)
│   - Texture: Smoke.png
│   - Color: 진한 회색 (0.3, 0.3, 0.3, 0.7)
│   - 위로 떠오름, 중력 -0.5
│   - Loop
└── LightFlicker (ParticleSystem)
    - Texture: Light.png
    - Color: 빨강 (1, 0.3, 0.1, 0.6)
    - 점멸하는 화염 빛, Burst Rate 30/s
    - Loop
```

### SlowStatusVFX.prefab (신규 — 통합 A+B+C)
경로: `_Project/Prefabs/VFX/SlowStatusVFX.prefab`

구조:
```
SlowStatusVFX (root, 적에게 SpawnAttached)
├── GroundDust (ParticleSystem) — A: 발 밑 안개
│   - Texture: Dust.png
│   - Color: 청록 (0.5, 0.8, 1.0, 0.4)
│   - Shape: Box (수평), 적 발 밑 (Y offset = -0.5)
│   - 부드러운 안개, Loop
├── HeadParticles (ParticleSystem) — B: 적 위 입자
│   - Texture: Star.png 작은 사이즈
│   - Color: 청록 (0.5, 0.9, 1.0, 0.6)
│   - 적 머리 위에서 천천히 하강 (속도 0.3)
│   - Loop
└── GroundCircle (ParticleSystem 또는 SpriteRenderer) — C: 발 밑 원판
    - Texture: Light.png
    - Color: 청록 (0.4, 0.8, 1.0, 0.3)
    - 적 발 밑 평면 회전 원판 (지속)
    - Loop
```

## 수정 파일

### `LostMemory/Assets/_Project/Scripts/Runtime/Enemies/EnemyStatusEffect.cs`

#### 추가할 필드 (Inspector wired)
```csharp
[Header("VFX Prefabs (CL-202)")]
[SerializeField] private GameObject _freezeVFXPrefab;
[SerializeField] private GameObject _burnVFXPrefab;
[SerializeField] private GameObject _slowVFXPrefab;

[Header("SFX (CL-202)")]
[SerializeField] private AudioClip _freezeApplySfx;
[SerializeField] private AudioClip _burnApplySfx;
[SerializeField, Range(0f, 1f)] private float _sfxVolume = 0.6f;

[Header("Fade Out")]
[SerializeField, Min(0f)] private float _vfxFadeOutSeconds = 0.25f;
```

#### 신규: 활성 VFX 관리 (정책 D 구현)
```csharp
private enum StatusVisualType { None, Freeze, Burn, Slow }
private StatusVisualType _activeVisualType = StatusVisualType.None;
private GameObject _activeVisualInstance;
private Coroutine _fadeCoroutine;
```

#### 정책 D 핵심 메서드 신규 추가
```csharp
// 가장 최근 VFX 1개만 표시. 기존 활성 VFX는 페이드아웃 후 정리.
private void SetActiveVisual(StatusVisualType type, GameObject prefab)
{
    if (_activeVisualType == type && _activeVisualInstance != null) return;

    // 기존 visual 페이드아웃
    if (_activeVisualInstance != null)
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeOutAndDestroy(_activeVisualInstance, _vfxFadeOutSeconds));
    }

    _activeVisualType = type;
    _activeVisualInstance = (type == StatusVisualType.None || prefab == null)
        ? null
        : VFXSpawner.SpawnAttached(prefab, transform);
}

// 효과 만료 시 다른 활성 효과 있으면 그쪽으로 전환, 없으면 None
private void RefreshActiveVisual()
{
    bool freezeActive = Time.time < _freezeExpiresAt;
    bool burnActive = _burnExpiresAt > 0f && Time.time < _burnExpiresAt;
    bool slowActive = _slowMagnitude > 0f && Time.time < _slowExpiresAt;

    // 우선순위: Freeze > Burn > Slow (단일 visual 선택)
    if (freezeActive && _activeVisualType != StatusVisualType.Freeze)
        SetActiveVisual(StatusVisualType.Freeze, _freezeVFXPrefab);
    else if (!freezeActive && burnActive && _activeVisualType != StatusVisualType.Burn)
        SetActiveVisual(StatusVisualType.Burn, _burnVFXPrefab);
    else if (!freezeActive && !burnActive && slowActive && _activeVisualType != StatusVisualType.Slow)
        SetActiveVisual(StatusVisualType.Slow, _slowVFXPrefab);
    else if (!freezeActive && !burnActive && !slowActive)
        SetActiveVisual(StatusVisualType.None, null);
}

private IEnumerator FadeOutAndDestroy(GameObject vfx, float duration)
{
    float t = 0f;
    var renderers = vfx.GetComponentsInChildren<SpriteRenderer>();
    var particleSystems = vfx.GetComponentsInChildren<ParticleSystem>();

    // ParticleSystem 정지 (자연스럽게 흩어짐)
    foreach (var ps in particleSystems) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

    // SpriteRenderer 알파 페이드
    Color[] startColors = new Color[renderers.Length];
    for (int i = 0; i < renderers.Length; i++) startColors[i] = renderers[i].color;

    while (t < duration)
    {
        t += Time.deltaTime;
        float k = 1f - (t / duration);
        for (int i = 0; i < renderers.Length; i++)
        {
            Color c = startColors[i];
            renderers[i].color = new Color(c.r, c.g, c.b, c.a * k);
        }
        yield return null;
    }
    if (vfx != null) Destroy(vfx);
}
```

#### Line 77~86 `ApplySlow()` 변경
**기존 끝에 추가**:
```csharp
public void ApplySlow(float magnitude, float duration)
{
    // ... 기존 magnitude/duration 갱신 로직 ...
    ApplyMovementMultiplier();
    RefreshActiveVisual();  // 신규: 정책 D 호출
}
```

#### Line 88~93 `ApplyFreeze()` 변경
**기존 끝에 추가**:
```csharp
public void ApplyFreeze(float durationSeconds)
{
    _freezeExpiresAt = Time.time + durationSeconds;
    ApplyMovementMultiplier();
    RefreshActiveVisual();  // 신규
    if (_freezeApplySfx != null)
        AudioSource.PlayClipAtPoint(_freezeApplySfx, transform.position, _sfxVolume);
}
```

#### Line 95~105 `ApplyBurn()` 변경
**기존 `SetBurnVisualActive(true)` 호출 제거** → `RefreshActiveVisual()` 로 교체:
```csharp
public void ApplyBurn(float damagePerTick, float duration, GameObject instigator)
{
    if (damagePerTick <= 0f || duration <= 0f) return;
    _burnDamagePerTick = damagePerTick;
    _burnExpiresAt = Time.time + duration;
    _burnNextTickAt = Time.time + 1f;
    _burnInstigator = instigator;
    RefreshActiveVisual();  // 신규
    if (_burnApplySfx != null)
        AudioSource.PlayClipAtPoint(_burnApplySfx, transform.position, _sfxVolume);
}
```

#### Line 109~142 `Update()` 변경
**효과 만료 시 RefreshActiveVisual 호출 추가**:
- Slow 만료 시 (line 112-116): 만료 후 `RefreshActiveVisual()`
- Freeze 만료 시 (line 117-121): 만료 후 `RefreshActiveVisual()`
- Burn 만료 시 (line 127-132): `SetBurnVisualActive(false)` → `RefreshActiveVisual()`

#### 삭제 대상 (placeholder 코드 전체)
- Line 24-30 정적 색 상수 (`FreezeVisualColor`, `FreezeVisualSortingOrder`, `BurnVisualColor`, `BurnVisualSortingOrder`)
- Line 47-50 placeholder GameObject 필드 (`_freezeVisual`, `_freezeVisualActive`, `_burnVisual`, `_burnVisualActive`)
- Line 65-72 OnDestroy 내 placeholder 정리 (`_activeVisualInstance` 정리로 대체)
- Line 167 `SetFreezeVisualActive(isFrozen)` 호출 (`RefreshActiveVisual` 로 일원화됨)
- Line 172-216 `SetFreezeVisualActive`, `EnsureFreezeVisual` 메서드 전체
- Line 220-261 `SetBurnVisualActive`, `EnsureBurnVisual` 메서드 전체
- Line 14-15 placeholder 관련 주석

#### `OnDestroy()` 정리 추가
```csharp
private void OnDestroy()
{
    if (_activeVisualInstance != null) Destroy(_activeVisualInstance);
    if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
}
```

## 검증 방법
1. **Freeze 단독**: 얼음 세트로 적 빙결 → IceBlock 청색 + 결정 입자 표시, 정지 효과 정상
2. **Burn 단독**: 불 세트로 적 화상 → Fire 4프레임 애니메이션 + 연기 + 점멸 빛, DoT 데미지 정상
3. **Slow 단독**: 둔화 효과로 적 슬로우 → 발 밑 안개 + 적 위 입자 + 발 밑 원판 표시 (이전엔 시각 0)
4. **동시 적용 (정책 D 검증)**:
   - Freeze + Burn + Slow 동시 적용 → **VFX는 Freeze 1개만**, 효과는 모두 적용됨 (이속 0, DoT 들어감, 슬로우는 빙결 풀린 후 표시)
   - Freeze 풀림 → Burn으로 전환 (페이드아웃 0.25초 후 BurnVFX 등장)
   - Burn 풀림 → Slow로 전환
   - Slow 풀림 → VFX 사라짐 (페이드아웃)
5. **적 이동 시**: SpawnAttached 동작으로 VFX 따라옴
6. **적 사망 시**: OnDestroy 정리로 VFX 잔상 없음
7. **SFX**: Freeze/Burn 적용 시점에 1회 재생, Slow는 SFX 없음

## 추정 시간
1.5일 (프리팹 3종 + EnemyStatusEffect 리팩토링 + 정책 D 구현 + 페이드아웃 코루틴 + SFX + 검증)

## 의존성
- 선행: **CL-200** (VFXSpawner, MMVFX 텍스처 이전 필요)
- 병렬 가능: CL-201, CL-203 (다른 파일 수정)

## 주의사항
- **정책 D 우선순위**(Freeze > Burn > Slow)는 임의로 정한 것. 다른 우선순위 원하시면 알려주세요. 또는 "발동 시점 기준 가장 최근"으로 변경 가능 (각 효과의 ApplyXxx 호출 시점 timestamp 비교)
- **Burn DoT 데미지 정상**: VFX 정책 변경에도 `_burnDamagePerTick` / `_burnExpiresAt` 로직은 그대로 유지 → DoT 데미지 그대로 들어감
- **Slow 시각 신규 추가로 게임 인지 변화**: 이전엔 적이 느려진 걸 모르고 지나갔지만 이제 명확히 보임 → 기획팀에 시각 도입 알림 필요
- **페이드아웃 중 새 VFX 발동 시**: `_fadeCoroutine` 참조로 기존 페이드 중단 후 새 VFX 즉시 표시 (잔상 없음)
- **SpawnAttached 부착 위치**: 적 root에 부착되므로 적의 SpriteRenderer가 따로 있는 경우 정렬 문제 가능 → 프리팹의 SortingOrder로 보정
- **적 사망 시 Burn 잔상**: `OnDestroy`에서 `_activeVisualInstance` 즉시 Destroy로 처리 (페이드아웃 없이 즉시 정리)
- **AudioSource.PlayClipAtPoint 풀링 미적용**: 빈번 호출 시 GC 우려. CL-201 동일 이슈 — 추후 풀링 통합 검토
