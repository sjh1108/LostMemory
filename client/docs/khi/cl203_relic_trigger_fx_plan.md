# CL-203: 유물 트리거 피드백 VFX 4종

## Context
개별 유물 효과가 발동하는 4가지 트리거 시점 — **패링 성공 시 보호막 / 적 처치 시 공속 / 대시 종료 시 이속 / 회복약 사용** — 에 시각 피드백이 전혀 없어 플레이어가 "무엇이 일어났는지" 인지 불가. 4종 VFX를 추가하여 효과 발동을 직관적으로 보여준다.

수정 대상:
- [RelicEffectRegistry.cs:148](../../LostMemory/Assets/_Project/Scripts/Runtime/Relics/RelicEffectRegistry.cs) `HandleParrySuccess()` (ShieldOnParry)
- [RelicEffectRegistry.cs:137](../../LostMemory/Assets/_Project/Scripts/Runtime/Relics/RelicEffectRegistry.cs) `HandleEnemyKilled()` (AttackSpeedOnKillTimed)
- [RelicEffectRegistry.cs:160](../../LostMemory/Assets/_Project/Scripts/Runtime/Relics/RelicEffectRegistry.cs) `HandleDashEnded()` (MoveSpeedAfterDashTimed)
- [PlayerHealing.cs:49](../../LostMemory/Assets/_Project/Scripts/Runtime/Combat/PlayerHealing.cs) `UseConsumable()` (HealConsumablePercent)

## 결정 사항 (사용자 확정 + 권장안)
- **보호막 표현/색**: B — 청록 에너지 막 `(0.4, 0.9, 1.0)` ✓
- **공속 오라 색**: 빨강 `(1, 0.3, 0.3, 1)` ✓
- **이속 오라 색**: 파랑 `(0.3, 0.6, 1, 1)` ✓
- **회복 색**: 녹색 `(0.3, 1, 0.4)` ✓
- **버프 갱신 정책**: B — duration만 갱신, 오라 인스턴스 유지 (자연스러움) ✓
- **SFX 포함**: ✓ (CL-201/202 일관성)
- **Authority 게이트**: 기존 `_authority.IsAuthority` 안에서 처리 (host-only). Multiplayer 환경 VFX 동기화는 별도 티켓

## 작업 범위
- [ ] `ShieldVFX.prefab` 제작 (청록 에너지 막 + 발동 순간 별빛)
- [ ] `BuffAuraVFX.prefab` 제작 (공통 베이스, 인스턴스화 시 색 코드 지정)
- [ ] `HealVFX.prefab` 제작 (녹색 위로 떠오르는 입자, 1회성)
- [ ] `RelicEffectRegistry.HandleParrySuccess()` Shield VFX 호출 + 활성 인스턴스 관리
- [ ] `RelicEffectRegistry.HandleEnemyKilled()` 빨강 BuffAura 호출 + duration 갱신 정책
- [ ] `RelicEffectRegistry.HandleDashEnded()` 파랑 BuffAura 호출 + duration 갱신 정책
- [ ] `PlayerHealing.UseConsumable()` Heal VFX 호출
- [ ] 만료 시 페이드아웃 정리 (Update tick)
- [ ] `HandleRunCleared()`에서 활성 VFX 일괄 정리
- [ ] SFX 통합 (각 트리거별 1회 재생)
- [ ] 인스펙터 와이어링 + 인게임 검증

## 신규 프리팹

### ShieldVFX.prefab
경로: `_Project/Prefabs/VFX/ShieldVFX.prefab`

구조:
```
ShieldVFX (root, 플레이어에게 SpawnAttached)
├── EnergyShell (SpriteRenderer)
│   - Sprite: Light.png (구체 형태)
│   - Color: (0.4, 0.9, 1.0, 0.5) 청록 반투명
│   - Scale: 1.5 (플레이어 감싸는 크기)
│   - SortingOrder: 9999
├── EnergyParticles (ParticleSystem) — 막 표면 입자
│   - Texture: Star.png 작은 사이즈
│   - Color: 청록 (0.4, 0.9, 1.0, 0.7)
│   - Shape: Sphere edge, 막 표면 따라 부유
│   - Loop, 듀레이션 동안 유지
└── GrantBurst (ParticleSystem) — 발동 순간 별빛
    - Texture: Star.png 큰 사이즈
    - Burst: 12 particles (1회)
    - Color: 청록 + 백색 그라데이션
    - 0.4초 페이드아웃, Stop Action: Destroy on Finish
```

### BuffAuraVFX.prefab (공통 베이스)
경로: `_Project/Prefabs/VFX/BuffAuraVFX.prefab`

구조:
```
BuffAuraVFX (root, 플레이어 발 밑 SpawnAttached, localOffset.y = -0.5)
├── GroundLight (ParticleSystem)
│   - Texture: Light.png
│   - Color: (1, 1, 1, 0.6) ← 인스턴스화 시 코드에서 지정
│   - Shape: Circle (수평), 반경 0.6
│   - 적 발 밑 평면 회전 발광 원판
│   - Loop
└── Sparkles (ParticleSystem)
    - Texture: Star.png 작은 사이즈
    - Color: 인스턴스화 시 코드에서 지정
    - Burst Rate: 8/s, 위로 약간 떠오름
    - Loop
```

색은 인스턴스화 후 `ParticleSystem.MainModule.startColor` 변경:
- 공속: `(1, 0.3, 0.3, 1)`
- 이속: `(0.3, 0.6, 1, 1)`

### HealVFX.prefab
경로: `_Project/Prefabs/VFX/HealVFX.prefab`

구조:
```
HealVFX (root, 1회성 Spawn at player position, autoDestroy 1.5s)
├── GreenLight (ParticleSystem)
│   - Texture: Light.png
│   - Color: 녹색 (0.3, 1, 0.4, 0.8)
│   - Shape: Sphere, 플레이어 중심 발광
│   - Burst: 1 (시작 시), 듀레이션 0.6s
└── RisingSparks (ParticleSystem)
    - Texture: Star.png
    - Color: 녹색 (0.3, 1, 0.4, 0.9)
    - Shape: Cone (위 방향), 위로 떠오름 (속도 1.5)
    - Burst: 15 particles, 1.2초 동안 흩어짐
```

## 수정 파일

### `LostMemory/Assets/_Project/Scripts/Runtime/Relics/RelicEffectRegistry.cs`

#### 추가할 필드
```csharp
[Header("VFX Prefabs (CL-203)")]
[SerializeField] private GameObject _shieldVFXPrefab;
[SerializeField] private GameObject _buffAuraVFXPrefab;

[Header("Buff Aura Colors (CL-203)")]
[SerializeField] private Color _attackSpeedAuraColor = new Color(1f, 0.3f, 0.3f, 1f);
[SerializeField] private Color _moveSpeedAuraColor = new Color(0.3f, 0.6f, 1f, 1f);

[Header("SFX (CL-203)")]
[SerializeField] private AudioClip _shieldGrantedSfx;
[SerializeField] private AudioClip _killBuffSfx;
[SerializeField] private AudioClip _dashBuffSfx;
[SerializeField, Range(0f, 1f)] private float _sfxVolume = 0.6f;

[Header("VFX Fade Out")]
[SerializeField, Min(0f)] private float _vfxFadeOutSeconds = 0.25f;

// 활성 VFX 관리 (정책 B: duration 갱신, 인스턴스 유지)
private GameObject _activeShieldVFX;
private float _shieldVFXExpiresAt;

private GameObject _activeAttackSpeedAura;
private float _attackSpeedAuraExpiresAt;

private GameObject _activeMoveSpeedAura;
private float _moveSpeedAuraExpiresAt;

private Transform _playerTransform;  // SpawnAttached 대상 (플레이어 root)
```

#### `Awake()` 또는 `OnEnable()`에서 playerTransform 캐싱
```csharp
private void Awake()
{
    // 보호막/오라가 부착될 플레이어 transform.
    // playerHealth 가 플레이어에 있으니 그쪽을 기준으로 사용.
    _playerTransform = playerHealth != null ? playerHealth.transform : transform;
}
```

#### `Update()` 신규 추가 (만료 처리)
```csharp
private void Update()
{
    if (!_authority.IsAuthority) return;

    // ShieldVFX 만료
    if (_activeShieldVFX != null && Time.time >= _shieldVFXExpiresAt)
    {
        StartCoroutine(FadeOutAndDestroy(_activeShieldVFX, _vfxFadeOutSeconds));
        _activeShieldVFX = null;
    }
    // AttackSpeed Aura 만료
    if (_activeAttackSpeedAura != null && Time.time >= _attackSpeedAuraExpiresAt)
    {
        StartCoroutine(FadeOutAndDestroy(_activeAttackSpeedAura, _vfxFadeOutSeconds));
        _activeAttackSpeedAura = null;
    }
    // MoveSpeed Aura 만료
    if (_activeMoveSpeedAura != null && Time.time >= _moveSpeedAuraExpiresAt)
    {
        StartCoroutine(FadeOutAndDestroy(_activeMoveSpeedAura, _vfxFadeOutSeconds));
        _activeMoveSpeedAura = null;
    }
}
```

#### Line 148~158 `HandleParrySuccess()` 변경
**기존 끝에 추가**:
```csharp
private void HandleParrySuccess()
{
    if (!_authority.IsAuthority) return;
    if (playerShield == null || playerHealth == null) return;
    float maxHp = playerHealth.MaximumHealth;
    float maxDuration = 0f;
    for (int i = 0; i < _onParrySuccessSubscriptions.Count; i++)
    {
        TimedSubscription s = _onParrySuccessSubscriptions[i];
        playerShield.GrantShield(maxHp * s.Magnitude, s.Duration);
        if (s.Duration > maxDuration) maxDuration = s.Duration;
    }

    // VFX (정책 B: 활성 인스턴스 있으면 duration만 갱신)
    if (_shieldVFXPrefab != null && _playerTransform != null && maxDuration > 0f)
    {
        if (_activeShieldVFX == null)
            _activeShieldVFX = VFXSpawner.SpawnAttached(_shieldVFXPrefab, _playerTransform);
        _shieldVFXExpiresAt = Time.time + maxDuration;
    }

    // SFX (1회)
    if (_shieldGrantedSfx != null && _onParrySuccessSubscriptions.Count > 0)
        AudioSource.PlayClipAtPoint(_shieldGrantedSfx, _playerTransform.position, _sfxVolume);
}
```

#### Line 137~146 `HandleEnemyKilled()` 변경
**기존 끝에 추가**:
```csharp
private void HandleEnemyKilled(KhiAttackRequest req, AttackStepData step, Health victim)
{
    if (!_authority.IsAuthority) return;
    if (container == null) return;
    float maxDuration = 0f;
    for (int i = 0; i < _onKillSubscriptions.Count; i++)
    {
        OnKillSubscription s = _onKillSubscriptions[i];
        container.AddTimed(StatId.AttackSpeed, s.Magnitude, s.Duration, s.Source);
        if (s.Duration > maxDuration) maxDuration = s.Duration;
    }

    // VFX
    if (_buffAuraVFXPrefab != null && _playerTransform != null && maxDuration > 0f)
    {
        if (_activeAttackSpeedAura == null)
        {
            _activeAttackSpeedAura = VFXSpawner.SpawnAttached(
                _buffAuraVFXPrefab, _playerTransform, new Vector3(0f, -0.5f, 0f));
            ApplyAuraColor(_activeAttackSpeedAura, _attackSpeedAuraColor);
        }
        _attackSpeedAuraExpiresAt = Time.time + maxDuration;
    }

    // SFX (1회)
    if (_killBuffSfx != null && _onKillSubscriptions.Count > 0)
        AudioSource.PlayClipAtPoint(_killBuffSfx, _playerTransform.position, _sfxVolume);
}
```

#### Line 160~169 `HandleDashEnded()` 변경
**동일 패턴 (이속 / 파랑)**:
```csharp
private void HandleDashEnded()
{
    if (!_authority.IsAuthority) return;
    if (container == null) return;
    float maxDuration = 0f;
    for (int i = 0; i < _onDashEndSubscriptions.Count; i++)
    {
        TimedSubscription s = _onDashEndSubscriptions[i];
        container.AddTimed(StatId.MoveSpeed, s.Magnitude, s.Duration, s.Source);
        if (s.Duration > maxDuration) maxDuration = s.Duration;
    }

    if (_buffAuraVFXPrefab != null && _playerTransform != null && maxDuration > 0f)
    {
        if (_activeMoveSpeedAura == null)
        {
            _activeMoveSpeedAura = VFXSpawner.SpawnAttached(
                _buffAuraVFXPrefab, _playerTransform, new Vector3(0f, -0.5f, 0f));
            ApplyAuraColor(_activeMoveSpeedAura, _moveSpeedAuraColor);
        }
        _moveSpeedAuraExpiresAt = Time.time + maxDuration;
    }

    if (_dashBuffSfx != null && _onDashEndSubscriptions.Count > 0)
        AudioSource.PlayClipAtPoint(_dashBuffSfx, _playerTransform.position, _sfxVolume);
}
```

#### 신규 헬퍼 메서드
```csharp
private static void ApplyAuraColor(GameObject auraInstance, Color color)
{
    var systems = auraInstance.GetComponentsInChildren<ParticleSystem>();
    foreach (var ps in systems)
    {
        var main = ps.main;
        main.startColor = color;
    }
}

private IEnumerator FadeOutAndDestroy(GameObject vfx, float duration)
{
    if (vfx == null) yield break;
    var systems = vfx.GetComponentsInChildren<ParticleSystem>();
    foreach (var ps in systems) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

    var renderers = vfx.GetComponentsInChildren<SpriteRenderer>();
    Color[] startColors = new Color[renderers.Length];
    for (int i = 0; i < renderers.Length; i++) startColors[i] = renderers[i].color;

    float t = 0f;
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

#### Line 174~182 `HandleRunCleared()` 변경
**기존 끝에 추가**:
```csharp
private void HandleRunCleared()
{
    if (!_authority.IsAuthority) return;
    if (container != null) container.ClearAll();
    if (playerShield != null) playerShield.ClearShield();
    _onKillSubscriptions.Clear();
    _onParrySuccessSubscriptions.Clear();
    _onDashEndSubscriptions.Clear();

    // 활성 VFX 즉시 정리 (페이드아웃 없이)
    if (_activeShieldVFX != null) { Destroy(_activeShieldVFX); _activeShieldVFX = null; }
    if (_activeAttackSpeedAura != null) { Destroy(_activeAttackSpeedAura); _activeAttackSpeedAura = null; }
    if (_activeMoveSpeedAura != null) { Destroy(_activeMoveSpeedAura); _activeMoveSpeedAura = null; }
}
```

### `LostMemory/Assets/_Project/Scripts/Runtime/Combat/PlayerHealing.cs`

#### 추가할 필드
```csharp
[Header("VFX / SFX (CL-203)")]
[SerializeField] private GameObject _healVFXPrefab;
[SerializeField] private AudioClip _healSfx;
[SerializeField, Range(0f, 1f)] private float _sfxVolume = 0.6f;
```

#### Line 49~55 `UseConsumable()` 변경
**기존 끝에 추가**:
```csharp
public void UseConsumable(RelicData consumable)
{
    if (consumable == null || !consumable.IsConsumable) return;
    if (consumable.EffectType != RelicEffectType.HealConsumablePercent) return;
    float baseAmount = (health != null ? health.MaximumHealth : 0f) * consumable.Magnitude;
    Heal(baseAmount, consumable);

    // VFX (1회성)
    if (_healVFXPrefab != null)
        VFXSpawner.Spawn(_healVFXPrefab, transform.position, Quaternion.identity, 1.5f);

    // SFX
    if (_healSfx != null)
        AudioSource.PlayClipAtPoint(_healSfx, transform.position, _sfxVolume);
}
```

## 검증 방법
1. **보호막 (ShieldOnParry)**: 패링 성공 시 → 청록 에너지 막 + 별빛 발동, duration 동안 유지
2. **보호막 갱신**: 보호막 활성 중 다시 패링 → VFX 인스턴스은 그대로, duration만 갱신 (깜빡임 없음)
3. **공속 오라 (AttackSpeedOnKillTimed)**: 적 처치 시 → 발 밑 빨간 오라 활성, duration 동안 유지
4. **공속 갱신**: 활성 중 다른 적 처치 → 오라 그대로, duration 갱신
5. **이속 오라 (MoveSpeedAfterDashTimed)**: 대시 종료 시 → 발 밑 파란 오라
6. **공속 + 이속 동시**: 두 오라 겹쳐서 표시 (서로 다른 인스턴스)
7. **회복약 (HealConsumablePercent)**: 회복약 사용 시 (또는 ContextMenu 디버그 트리거) → 녹색 입자 1.5초 위로 떠오름
8. **만료 시 페이드아웃**: duration 종료 시 0.25초 페이드 후 정리
9. **Run 종료**: `HandleRunCleared` 호출 시 모든 활성 VFX 즉시 정리
10. **SFX**: 각 트리거 시점에 1회 재생 (보호막/처치/대시/회복)

## 추정 시간
1일 (프리팹 3종 + 4개 hook + duration 갱신 + 만료 페이드아웃 + SFX + 검증)

## 의존성
- 선행: **CL-200** (VFXSpawner, MMVFX 텍스처 이전 필요)
- 병렬 가능: CL-201, CL-202 (다른 파일/메서드 수정)

## 주의사항
- **버프 갱신 정책 B**: 같은 타입 버프가 연속 발동 시 인스턴스 그대로 유지하고 `expiresAt`만 갱신. 시각 깜빡임 방지.
- **활성 VFX 관리 멤버 3개** (`_activeShieldVFX`, `_activeAttackSpeedAura`, `_activeMoveSpeedAura`): 같은 타입은 단일 인스턴스만 유지. 다른 타입은 동시 표시.
- **PlayerShield 자체 만료 vs VFX 만료**: PlayerShield의 보호막 데이터와 VFX의 `_shieldVFXExpiresAt`이 별개로 관리됨. 보호막이 데미지로 먼저 소진되면 VFX는 duration까지 유지될 수 있음 — 추후 PlayerShield에 OnShieldExpired 이벤트 노출하여 동기화 검토
- **회복 VFX는 Spawn(non-attached)**: 회복약은 1회성이라 부착 불필요. 위치 고정 후 자동 파괴
- **Authority 게이트**: 모든 핸들러가 `_authority.IsAuthority` 안에 있으므로 VFX도 host-only로 표시됨. Multiplayer 환경 동기화는 별도 티켓
- **만료 처리 Update tick**: `Update()`에서 매 프레임 3개 expiresAt 체크. 부담 거의 없으나 향후 코루틴 기반으로 리팩토링 검토 가능
- **공통 BuffAura 프리팹 색 변경**: 인스턴스화 후 `ApplyAuraColor`로 ParticleSystem 색 코드 변경. 같은 프리팹 재활용 → 자산 절약
- **AudioSource.PlayClipAtPoint 풀링 미적용**: CL-201/202와 동일 이슈. 추후 통합 풀링 검토
