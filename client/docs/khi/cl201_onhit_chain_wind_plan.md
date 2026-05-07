# CL-201: OnHit VFX 정식 교체 — Chain / Wind

## Context
OnHit 효과 중 Chain(번개 체인)과 Wind(바람 광역)는 현재 동적 LineRenderer로 그려진 노란 직선 placeholder만 표시됨. [OnHitEffectRegistry.cs:239](../../LostMemory/Assets/_Project/Scripts/Runtime/Combat/OnHitEffectRegistry.cs) 코드 주석에도 "정식 lightning VFX 는 후속 ticket"이라 명시되어 있고, Wind까지 같은 `DrawChainBolt`를 재사용 중이라 함께 정비한다.

정식 ParticleSystem + LineRenderer 자식 기반 VFX로 교체하고, 적중 시 SFX·히트스톱까지 추가하여 시각·청각·물리 피드백을 한 번에 완성한다.

## 결정 사항 (사용자 확정)
- **아트 톤**: A — Koala 픽셀풍 메인 (`Electricity.png` 활용)
- **Chain 연결선 구현**: B — LineRenderer 자식 노드 (현재 placeholder와 호환성, 표현 자유도)
- **Lightning 색상**: 황색 (현재 `ChainBoltColor = (1, 0.95, 0.3, 1)` 그대로 유지)
- **Wind 색상**: 흰색 (1, 1, 1, 0.9)
- **SFX / 히트스톱**: 이번 티켓에 포함

## 작업 범위
- [ ] `ChainHitVFX.prefab` 제작 (LineRenderer 자식 + Electricity 임팩트 ParticleSystem)
- [ ] `WindAOEVFX.prefab` 제작 (Slash 휘두름 + Whirlwind 광역 ParticleSystem + LineRenderer 자식)
- [ ] `OnHitEffectRegistry.ApplyChain()` LineRenderer 코드 → VFX 인스턴스화 교체
- [ ] `OnHitEffectRegistry.ApplyWindBlade()` LineRenderer 코드 → VFX 인스턴스화 교체
- [ ] `DrawChainBolt()` 메서드 + 관련 정적 필드/머티리얼 캐시 삭제
- [ ] SFX 통합 (Chain hit / Wind blade 각각 AudioClip)
- [ ] 히트스톱 통합 (`KhiHitStopController` 활용)
- [ ] 인스펙터 와이어링 + 인게임 검증

## 신규 프리팹

### ChainHitVFX.prefab
경로: `_Project/Prefabs/VFX/ChainHitVFX.prefab`

구조:
```
ChainHitVFX (root, GameObject + 자동 파괴 컴포넌트)
├── ConnectionLine (LineRenderer 자식)
│   - Material: ElectricityMat (CL-200 결과물)
│   - 색: (1, 0.95, 0.3, 1) 황색
│   - Width: 0.18 → 0.05 페이드 (양 끝 가늘게)
│   - SortingOrder: 9999
│   - positionCount=2, 코드에서 Set
└── Impact (ParticleSystem 자식)
    - Texture: Electricity.png
    - Burst: 8 particles (1회)
    - Duration: 0.3s
    - Color: 황색 (1, 0.95, 0.3) + emission
    - Stop Action: Destroy
```

자체 자동 파괴: 루트에 `VFXAutoDestroy(0.3f)` 또는 ParticleSystem `Stop Action: Destroy` 활용

### WindAOEVFX.prefab
경로: `_Project/Prefabs/VFX/WindAOEVFX.prefab`

구조:
```
WindAOEVFX (root)
├── BladeLine (LineRenderer 자식, victim → endPoint)
│   - Material: WindMat (Sprites-Default)
│   - 색: 흰색 (1, 1, 1, 0.9)
│   - Width: 0.4 → 0.1 페이드
│   - positionCount=2, 코드에서 Set
├── SlashSweep (ParticleSystem 자식)
│   - Texture: Slash.png (또는 KoalaSwordSlash 활용)
│   - Burst: 1 particle, 큰 사이즈
│   - 0.4초 페이드아웃
└── Whirlwind (ParticleSystem 자식)
    - Texture: Whirlwind.png
    - Shape: Circle, 반경 = _windBladeWidth × 1.5 정도
    - Duration: 0.5s
    - Stop Action: Destroy
```

## 수정 파일

### `LostMemory/Assets/_Project/Scripts/Runtime/Combat/OnHitEffectRegistry.cs`

#### 추가할 필드 (Header 단위)
```csharp
[Header("VFX Prefabs (CL-201)")]
[SerializeField] private GameObject _chainHitVFXPrefab;
[SerializeField] private GameObject _windAOEVFXPrefab;

[Header("SFX / Feedback (CL-201)")]
[SerializeField] private AudioClip _chainHitSfx;
[SerializeField] private AudioClip _windBladeSfx;
[SerializeField, Range(0f, 1f)] private float _sfxVolume = 0.7f;
[SerializeField] private KhiHitStopController _hitStopController;
[SerializeField, Min(0f)] private float _chainHitStopDuration = 0.05f;
[SerializeField, Min(0f)] private float _windHitStopDuration = 0.08f;
```

#### Line 148~175 `ApplyChain()` 변경
**기존**:
```csharp
foreach (Health t in targets)
{
    if (t == null) continue;
    t.Damage(chainDamage, gameObject, 0f, 0f, Vector3.zero);
    DrawChainBolt(origin, t.transform.position);  // ← LineRenderer 직접 생성
}
```

**변경**:
```csharp
foreach (Health t in targets)
{
    if (t == null) continue;
    t.Damage(chainDamage, gameObject, 0f, 0f, Vector3.zero);
    SpawnChainVFX(origin, t.transform.position);  // ← VFX 프리팹 인스턴스화
}

// SFX (적중 1회만)
if (_chainHitSfx != null)
    AudioSource.PlayClipAtPoint(_chainHitSfx, origin, _sfxVolume);

// 히트스톱
if (_hitStopController != null && _chainHitStopDuration > 0f)
    _hitStopController.RequestHitStop(_chainHitStopDuration);
```

#### `SpawnChainVFX()` 신규 메서드
```csharp
private void SpawnChainVFX(Vector3 from, Vector3 to)
{
    if (_chainHitVFXPrefab == null) return;
    from.z = 0f; to.z = 0f;

    GameObject go = VFXSpawner.Spawn(_chainHitVFXPrefab, from, Quaternion.identity, 0.3f);
    LineRenderer lr = go.GetComponentInChildren<LineRenderer>();
    if (lr != null)
    {
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
    }
}
```

#### Line 187~236 `ApplyWindBlade()` 변경
**제거**:
```csharp
// 시각화 — victim 에서 사거리 끝점까지 노란 직선 (Range 적용된 길이)
Vector3 endPoint = victimPos + (Vector3)(dir * effectiveLength);
DrawChainBolt(victimPos, endPoint);
```

**대체**:
```csharp
// VFX 인스턴스화 (회전 angle = 공격 방향)
SpawnWindVFX(victimPos, dir, effectiveLength);

// SFX (1회)
if (_windBladeSfx != null)
    AudioSource.PlayClipAtPoint(_windBladeSfx, victimPos, _sfxVolume);

// 히트스톱 (적중 1회 이상일 때만)
if (hitCount > 0 && _hitStopController != null && _windHitStopDuration > 0f)
    _hitStopController.RequestHitStop(_windHitStopDuration);
```

#### `SpawnWindVFX()` 신규 메서드
```csharp
private void SpawnWindVFX(Vector3 origin, Vector2 dir, float length)
{
    if (_windAOEVFXPrefab == null) return;
    origin.z = 0f;

    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
    GameObject go = VFXSpawner.Spawn(
        _windAOEVFXPrefab,
        origin,
        Quaternion.Euler(0f, 0f, angle),
        0.5f);

    // BladeLine 자식 LineRenderer 시작/끝 지정
    LineRenderer lr = go.GetComponentInChildren<LineRenderer>();
    if (lr != null)
    {
        Vector3 endPoint = origin + (Vector3)(dir * length);
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.SetPosition(0, origin);
        lr.SetPosition(1, endPoint);
    }

    // Whirlwind 반경 동적 조정 (Range multiplier 반영된 length 기반)
    ParticleSystem[] systems = go.GetComponentsInChildren<ParticleSystem>();
    foreach (ParticleSystem ps in systems)
    {
        if (ps.gameObject.name == "Whirlwind")
        {
            var shape = ps.shape;
            shape.radius = _windBladeWidth * 1.5f;
            break;
        }
    }
}
```

#### Line 238~289 placeholder 코드 전체 삭제
**삭제 대상**:
- 주석 라인 238-239
- `ChainBoltColor` (Line 241)
- `ChainBoltWidth` (Line 242)
- `ChainBoltDuration` (Line 243)
- `ChainBoltSortingOrder` (Line 244)
- `_chainMaterial` 정적 필드 (Line 246)
- `GetChainMaterial()` 메서드 (Line 248-265)
- `DrawChainBolt()` 메서드 (Line 267-289)

## 검증 방법
1. **번개 세트 빌드** 진입 → 적 처리 시 ChainHitVFX 황색 번개 + Electricity 임팩트 표시
2. 적중 시 SFX 재생 + 히트스톱 약하게 (50ms) 발동
3. 다중 적 존재 시 → 체인 연결선이 정확히 victim→다른 적 사이 표시 (최대 3마리)
4. **바람 세트 빌드** 진입 → WindAOEVFX 청록백 슬래시 + Whirlwind 광역 표시
5. Wind 적중 시 SFX + 히트스톱 (80ms)
6. Range multiplier 증가 시 Wind 사거리 + Whirlwind 반경 비례 확장
7. 노란 직선 LineRenderer placeholder 더 이상 나타나지 않음
8. Profiler에서 GC 폭증 없는지 확인 (Chain 쿨다운 0.5초로 제한되어 부담 적음)
9. 컴파일 시 `DrawChainBolt`, `_chainMaterial` 등 참조 0개 (전부 삭제 확인)

## 추정 시간
1일 (프리팹 2종 디자인 + 코드 리팩토링 + SFX/히트스톱 hook + 검증)

## 의존성
- 선행: **CL-200** (VFXSpawner, MMVFX 텍스처 이전, _Project/Materials/VFX 머티리얼 필요)
- 병렬 가능: CL-202, CL-203 (다른 파일/메서드 수정)

## 주의사항
- **체인 연결선 좌표**: ParticleSystem이 자식으로 있어도 LineRenderer는 별도로 World Space 좌표 명시 필요. `SpawnChainVFX()`에서 명시적 `SetPosition` 호출 필수
- **Wind Whirlwind 반경**: 프리팹은 base 설정만, 인스턴스화 후 `Shape.radius` 동적 조정 (Range multiplier 반영)
- **SFX 볼륨**: `AudioSource.PlayClipAtPoint`는 단순하지만 풀링 안 함. 빈번한 호출 시 GC 우려 → 추후 풀링 도입 검토
- **히트스톱**: `KhiHitStopController` API 시그니처는 구현 시 확인 필요 (`RequestHitStop(float)` 가정 — 실제와 다르면 조정)
- **체인 무한 루프 방지** 코드(주석 23-25)는 현재 그대로 유지 — VFX 교체로 인한 영향 없음
- **`origin.z = 0f` 보정**은 VFX 호출부에서 유지 (2D 평면 가시성)
