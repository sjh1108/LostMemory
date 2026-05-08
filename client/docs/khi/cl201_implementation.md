# CL-201 구현 요약 — OnHit VFX 정식 교체 (Chain / Wind)

**관련 plan**: [cl201_onhit_chain_wind_plan.md](cl201_onhit_chain_wind_plan.md)

## 목적

OnHit 효과 중 Chain(전기 체인)과 Wind(바람 광역)는 동적 LineRenderer로 그려진 노란 직선 placeholder만 표시 중이었음. Plan에서 정식 ParticleSystem + LineRenderer 자식 기반 VFX로 교체하기로 한 작업을 구현. SFX 슬롯 / 히트스톱까지 포함.

## Plan과 달라진 점 (코드베이스 현황 반영)

| 항목 | Plan 원안 | 실제 구현 | 사유 |
|---|---|---|---|
| 프리팹 경로 | `_Project/Prefabs/VFX/` | `_Project/Prefabs/Effect/` | 기존 VFX 프리팹(Bolt_VFX 등)이 모여있는 폴더 컨벤션 일치 |
| HitStop API | `RequestHitStop(float)` | `KhiHitStopController.Instance.RequestFreeze(duration, frozenScale)` | 실제 컨트롤러 시그니처 |
| HitStop 접근 | `[SerializeField]` 필드 | 싱글톤 `Instance` | `KhiCombatFeedbackBinder` 동일 패턴, Inspector 와이어링 누락 시 silent fail 방지 |
| `VFXAutoDestroy` 컴포넌트 | 별도 컴포넌트 | `VFXSpawner.Spawn(..., autoDestroySeconds)` 파라미터 | CL-200에서 진입점에 통합됨 |

## 결정 사항 (사용자 확정)

### 초기 결정 (Plan 기준)
- **아트 톤**: A — Koala 픽셀풍 메인 (`Electricity.png`)
- **Chain 색**: 황색 `(1, 0.95, 0.3, 1)`
- **Wind 색**: 흰색 `(1, 1, 1, 0.9)`
- **HitStop**: Chain 50ms / Wind 80ms, frozenScale 0 (완전 정지)
- **SFX 포함** (슬롯 노출, 자산 결정 후 채우기)

### 작업 중 추가 결정
- **Chain 시각 = 절차적 지그재그 LineRenderer** — 사용자가 픽셀 sprite 체인보다 procedural jagged + texture 튜닝이 더 어울린다고 판단
- **Chain은 P → A → B → C 순차** (별 모양 X) — "체인" 컨벤션 부합. P→A 거리가 임계값 초과 시 P→A 생략 (원거리 무기 호환)
- **Chain 라인 양 끝 Transform 추적** — 적/플레이어 이동 시에도 라인이 따라옴
- **Chain 라인 spawn 시차 0.05초** — 전기 전파되는 느낌
- **Wind에 BladeLine LineRenderer 제외** — SlashSweep + Whirlwind 두 ParticleSystem 만으로 충분 (LineRenderer는 placeholder의 흔적이라 불필요)
- **Wind 파티클 적 방향 이동** — `Velocity over Lifetime` Local space X=5 로 설정 (Cone Shape 방향 헷갈림 회피)
- **Wind VFX lifetime 1.2초** — 즉발 데미지 + 길게 흐르는 시각 (SerializeField `_windVFXLifetime`)

## 변경 파일

### 신규 스크립트

| 파일 | 역할 |
|---|---|
| [JaggedLightningLine.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/VFX/JaggedLightningLine.cs) | LineRenderer vertex 를 지그재그로 배치, Transform 추적 + 노이즈 캐시 |
| [LineRendererTextureScroll.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/VFX/LineRendererTextureScroll.cs) | 머티리얼 UV 오프셋 매 프레임 갱신 (현재 휴면 — Sprites/Default 셰이더가 _MainTex_ST 무시) |

### 수정 스크립트

[OnHitEffectRegistry.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Combat/OnHitEffectRegistry.cs)
- `using LostMemory.VFX;`, `using System.Collections;` 추가
- 신규 SerializeField:
  - VFX Prefabs: `_chainHitVFXPrefab`, `_windAOEVFXPrefab`, `_windVFXLifetime`
  - SFX: `_chainHitSfx`, `_windBladeSfx`, `_sfxVolume`
  - HitStop: `_chainHitStopDuration`, `_windHitStopDuration`, `_hitStopFrozenScale`
  - Chain 옵션: `_chainPlayerLineMaxDistance`, `_chainSpawnInterval`
- placeholder 코드 50줄 제거 (`ChainBoltColor`, `_chainMaterial`, `GetChainMaterial`, `DrawChainBolt`)
- `ApplyChain` — 데미지는 즉시, 시각은 `SpawnChainVFXSequence` 코루틴 위임 + SFX/HitStop
- `ApplyWindBlade` — `SpawnWindVFX` 호출 + SFX/HitStop
- 신규 메서드: `SpawnChainVFXSequence`, `SpawnChainVFX(Transform, Transform)`, `SpawnChainVFX(Vector3, Vector3)` (호환용), `SpawnWindVFX`

### 신규 프리팹

| 프리팹 | 구성 |
|---|---|
| `_Project/Prefabs/Effect/ChainHitVFX.prefab` | LineRenderer (ConnectionLine) + JaggedLightningLine 컴포넌트 |
| `_Project/Prefabs/Effect/WindAOEVFX.prefab` | SlashSweep ParticleSystem + Whirlwind ParticleSystem |

## 핵심 동작

### Chain (전기 체인)

```
1. 평타 적중 → ApplyChain
2. victim 주변 반경 3유닛, 최대 3마리 검색 (FindNearbyEnemies)
3. 데미지 즉시 적용 (모든 적), Transform 리스트 수집
4. SpawnChainVFXSequence 코루틴 시작:
   - P→A 라인 (거리 ≤ 3유닛일 때만)
   - 0.05초 후 A→B
   - 0.05초 후 B→C
5. JaggedLightningLine 이 매 프레임 양 끝 Transform 위치 추적 + 0.05초마다 노이즈 재생성
6. 0.3초 후 VFXSpawner 가 인스턴스 destroy
7. 적중 시 SFX 1회 + 50ms 히트스톱
```

### Wind (바람 광역)

```
1. 평타 적중 → ApplyWindBlade
2. victim 위치에서 공격 방향으로 박스 OverlapBox (effectiveLength × _windBladeWidth)
3. 박스 안 모든 적에게 데미지 즉시
4. SpawnWindVFX:
   - WindAOEVFX prefab 인스턴스 생성, victim 위치, 공격 각도로 회전
   - Whirlwind 자식의 Shape.radius 동적 조정 (= _windBladeWidth × 1.5)
5. SlashSweep 파티클이 Velocity over Lifetime Local X=5 로 적 방향 발사
6. 1.2초 후 destroy (파티클 라이프타임 충분히 살고 정리)
7. 적중 시 SFX 1회 + 80ms 히트스톱
```

## 검증 결과

- 전기 빌드(`정전기 띠` 등) → 평타 적중 시 황색 지그재그 번개가 P→victim→다른적 순차 표시 ✅
- 적이 이동해도 라인이 끊어지지 않고 따라옴 ✅
- 적이 죽어도 라인 잔상 없이 마지막 위치에서 fade ✅
- 바람 빌드(`폭풍 부적`) → SlashSweep 적 방향 sweep + Whirlwind 광역 ✅
- Range 스탯 증가 시 Wind 박스 + Whirlwind 반경 비례 확장 ✅
- 노란 직선 placeholder 더 이상 안 나타남 ✅

## 알려진 이슈 / 보류

- **`LineRendererTextureScroll`** — `Sprites/Default` 셰이더가 `_MainTex_ST` 적용 안 함 → 텍스처 스크롤 시각 효과 없음. 컴포넌트는 무해하게 부착만 가능. URP 호환 커스텀 셰이더 작성 시 활용 가능
- **SFX 자산 미할당** — 슬롯은 노출됨, 사운드 결정 후 인스펙터에서 채우면 됨 (코드 변경 X)
- **`AudioSource.PlayClipAtPoint` 풀링 미적용** — 빈번 호출 시 GC 우려. 추후 풀링 통합 검토

## 인스펙터 와이어링 (플레이어 프리팹)

| 슬롯 | 값 |
|---|---|
| Chain Hit VFX Prefab | `Assets/_Project/Prefabs/Effect/ChainHitVFX.prefab` |
| Wind AOE VFX Prefab | `Assets/_Project/Prefabs/Effect/WindAOEVFX.prefab` |
| Wind VFX Lifetime | 1.2 |
| Chain Hit Sfx / Wind Blade Sfx | (미할당 OK) |
| Sfx Volume | 0.7 |
| Chain Hit Stop Duration | 0.05 |
| Wind Hit Stop Duration | 0.08 |
| Hit Stop Frozen Scale | 0 |
| Chain Player Line Max Distance | 3 |
| Chain Spawn Interval | 0.05 |

## 미래 호환

- 원거리 무기(활/지팡이) 추가 시: `_chainPlayerLineMaxDistance`를 0으로 두면 P→A 라인 항상 생략. 또는 호출부에서 `combat.transform` 자리에 무기/투사체 Transform 넣으면 그쪽에서 시작
- `JaggedLightningLine`은 다른 시각 효과(맵 함정, 보스 패턴 등)에 재사용 가능
