# CL-200 후속: MMVFX 12종 VFX 프리팹 일괄 제작 스펙

> **본 문서 위치:** [vfx_creation_guide.md](../vfx_creation_guide.md) 일반 절차 + [cl201_onhit_chain_wind_plan.md](cl201_onhit_chain_wind_plan.md) Chain·Wind 코드 교체 사이의 **12종 정량 스펙 시트**.
> 사용자가 Unity Editor 에서 그대로 따라 12개 프리팹을 일괄 제작하기 위한 작업서.

---

## 1. 개요

CL-200 산출물 중 인프라(VFXSpawner) + 자산(텍스처 12종 + 머티리얼 12종)은 완료. 정식 ParticleSystem 프리팹은 0개 상태(`VFXDummy_Test.prefab`은 검증 더미). 본 문서로 12개 프리팹을 일괄 제작한다.

| 12종 | 텍스처 | 머티리얼 | 본 문서 섹션 |
|---|---|---|---|
| Star / Bolt / Light / Flash / Dust / Slash / Smoke / Fire / Electricity / Whirlwind / IceBlockFront / IceBlockBack | `_Project/Art/Effects/MMVFX/<Name>.png` | `_Project/Art/Materials/VFX/<Name>Mat.mat` | §4 |

**본 문서가 다루지 않는 것:**
- 일반 제작 절차 (텍스처 import, 머티리얼 생성, 트러블슈팅) → `vfx_creation_guide.md` 참조
- `OnHitEffectRegistry` 코드 교체 (Chain·Wind) → `cl201_onhit_chain_wind_plan.md` (후속 티켓)

---

## 2. 사전 체크 (한 줄 함정 해결)

CL-200 검증 결과 — 본 6가지는 모든 12종에 공통 적용:

1. **Texture Type** = `Sprite (2D and UI)` (Default 타입은 Sprites/Default 슬롯 ⊘ 거부)
2. **Alpha Source** = `From Gray Scale` (MMVFX 12종은 검정 배경, Input Texture Alpha 쓰면 흰그림+검정사각형)
3. **Shader** = `Sprites/Default` (URP Particles/Unlit, URP 2D Sprite-Unlit-Default 모두 비호환 검증됨)
4. **Start Delay** = `0` (autoDestroy와 충돌해서 안 보이는 첫번째 원인)
5. **Order in Layer** ≥ `100` (타일맵 위로 보이게)
6. **Stop Action** = `None` (Despawn API 가 처리)

---

## 3. 공통 컨벤션

| 항목 | 값 |
|---|---|
| 프리팹명 형식 | `<Name>_VFX.prefab` (예: `Star_VFX.prefab`) |
| 저장 경로 (단발 임팩트, 자식 없음) | `_Project/Prefabs/Effect/` |
| 저장 경로 (복합, LineRenderer/ParticleSystem 자식 있음) | `_Project/Prefabs/VFX/` (CL-201 ChainHitVFX·WindAOEVFX) |
| 머티리얼 참조 | Renderer 모듈 → Material 슬롯에 `<Name>Mat.mat` 드래그 |
| Renderer Render Mode | Billboard (모든 12종 공통) |
| Renderer Sorting Layer | `Foreground` (기본) — 단 IceBlockBack 만 `Default` |

---

## 4. 12종 VFX 스펙

### 공통 baseline (이하 별도 명시 없으면 따름)

```
Main.Start Delay      = 0
Main.Stop Action      = None
Renderer.Render Mode  = Billboard
Renderer.Sorting Layer = Foreground
Texture Alpha Source  = From Gray Scale
Material Shader       = Sprites/Default
```

---

### 4.1 Star_VFX (단발 / 연습용 / 가장 단순)

**경로:** `_Project/Prefabs/Effect/Star_VFX.prefab`
**사용처:** Crit hit 강조, 보스 처치 폭죽, 회복 픽업 시각 피드백
**텍스처/머티리얼:** `MMVFX/Star.png` + `VFX/StarMat.mat`

| 모듈 | 항목 | 값 |
|---|---|---|
| Main | Duration | 0.6 |
| Main | Looping | OFF |
| Main | Start Lifetime | 0.5 |
| Main | Start Speed | 2 |
| Main | Start Size | 0.4 |
| Main | Simulation Space | World |
| Emission | Rate over Time | 0 |
| Emission | Bursts | Time=0, Count=8 |
| Shape | Shape | Sphere |
| Shape | Radius | 0.2 |
| Color over Lifetime | Gradient | white(α=1) → white(α=0) |
| Size over Lifetime | Curve | 1.0 → 1.3 |
| Renderer | Order in Layer | 110 |

**호출 예시:** `VFXSpawner.Spawn(starVfxPrefab, hitPoint, Quaternion.identity, 0.8f);`

---

### 4.2 Bolt_VFX (단발 / 한 줄기 sprite)

**경로:** `_Project/Prefabs/Effect/Bolt_VFX.prefab`
**사용처:** 단발 라이트닝 임팩트. 번개 한 줄기 그대로 표시 (Electricity와 별도 — Electricity는 작은 입자 jitter)
**텍스처/머티리얼:** `MMVFX/Bolt.png` + `VFX/BoltMat.mat`

> **패턴 주의:** Bolt 텍스처는 "번개 한 줄기" 이미지라서 입자 여러 개로 흩뿌리면 모양 망가짐 → **Count=1, Sphere Radius=0, Speed=0** 으로 한 점에서 한 sprite를 그대로 보여주고, 회전은 코드에서 부여.

| 모듈 | 항목 | 값 |
|---|---|---|
| Main | Duration | 0.4 |
| Main | Looping | OFF |
| Main | Start Lifetime | 0.35 |
| Main | **Start Speed** | **0** |
| Main | **Start Size** | **1.2** |
| Main | Simulation Space | World |
| Emission | Rate over Time | 0 |
| Emission | **Bursts** | **Time=0, Count=1** |
| Shape | **Shape** | **Sphere** |
| Shape | **Radius** | **0** |
| Color over Lifetime | Gradient | (1, 0.95, 0.3, 1) → α=0 |
| Size over Lifetime | Curve | 1.0 → 0.7 |
| Renderer | Order in Layer | 120 |

**호출 예시:** 회전 angle 지정해 방향 맞춤
```csharp
Quaternion rot = Quaternion.LookRotation(direction);
VFXSpawner.Spawn(boltVfxPrefab, hitPoint, rot, 0.6f);
```

---

### 4.3 Light_VFX (단발 / 부드러운 광원)

**경로:** `_Project/Prefabs/Effect/Light_VFX.prefab`
**사용처:** 회복 픽업 스폰 빛, NPC 상호작용 강조
**텍스처/머티리얼:** `MMVFX/Light.png` + `VFX/LightMat.mat`

| 모듈 | 항목 | 값 |
|---|---|---|
| Main | Duration | 0.5 |
| Main | Looping | OFF |
| Main | Start Lifetime | 0.5 |
| Main | Start Speed | 1 |
| Main | Start Size | 0.7 |
| Main | Simulation Space | World |
| Emission | Bursts | Time=0, Count=6 |
| Shape | Shape | Sphere, Radius 0.15 |
| Color over Lifetime | Gradient | white(α=1) → white(α=0) |
| Renderer | Order in Layer | 110 |

**autoDestroySeconds:** 0.6

---

### 4.4 Flash_VFX (단발 / 짧은 화이트 플래시)

**경로:** `_Project/Prefabs/Effect/Flash_VFX.prefab`
**사용처:** 강타격 임팩트 첫 프레임, 회피 트리거 시각 피드백
**텍스처/머티리얼:** `MMVFX/Flash.png` + `VFX/FlashMat.mat`

| 모듈 | 항목 | 값 |
|---|---|---|
| Main | Duration | 0.15 |
| Main | Looping | OFF |
| Main | Start Lifetime | 0.15 |
| Main | Start Speed | 0 |
| Main | Start Size | 1.5 |
| Main | Simulation Space | World |
| Emission | Bursts | Time=0, Count=1 |
| Shape | Shape | Sphere, Radius 0 |
| Color over Lifetime | Gradient | white(α=1) → white(α=0) |
| Size over Lifetime | Curve | 0.6 → 1.4 |
| Renderer | Order in Layer | **130** (다른 VFX 위로) |

**autoDestroySeconds:** 0.2

---

### 4.5 Dust_VFX (단발 / 점프·대시 착지)

**경로:** `_Project/Prefabs/Effect/Dust_VFX.prefab`
**사용처:** 플레이어 점프 착지, 대시 시작/종료 발 밑
**텍스처/머티리얼:** `MMVFX/Dust.png` + `VFX/DustMat.mat`

| 모듈 | 항목 | 값 |
|---|---|---|
| Main | Duration | 0.4 |
| Main | Looping | OFF |
| Main | Start Lifetime | 0.4 |
| Main | Start Speed | 1.5 |
| Main | Start Size | 0.3 |
| Main | Simulation Space | World |
| Emission | Bursts | Time=0, Count=10 |
| Shape | Shape | Cone, Angle 90 (수평 퍼짐), Radius 0.05 |
| Color over Lifetime | Gradient | (0.8, 0.75, 0.65, 1) → α=0 |
| Size over Lifetime | Curve | 1.0 → 1.5 |
| Renderer | Order in Layer | 100 |

**autoDestroySeconds:** 0.5

---

### 4.6 Slash_VFX (단발 / 한 줄기 휘두름 sprite)

**경로:** `_Project/Prefabs/Effect/Slash_VFX.prefab`
**사용처:** 평타 트레일, WindAOEVFX(CL-201) 자식 후보
**텍스처/머티리얼:** `MMVFX/Slash.png` + `VFX/SlashMat.mat`

> **패턴 주의:** Bolt와 같은 패턴 — Slash 텍스처는 "휘두른 곡선 한 줄기" 이미지. Cone으로 흩뿌리지 말 것. Count=1, Sphere Radius=0, Speed=0. 회전은 코드에서 공격 방향대로 부여.

| 모듈 | 항목 | 값 |
|---|---|---|
| Main | Duration | 0.3 |
| Main | Looping | OFF |
| Main | Start Lifetime | 0.3 |
| Main | **Start Speed** | **0** |
| Main | **Start Size** | **1.0** |
| Main | Simulation Space | World |
| Emission | Bursts | Time=0, Count=1 |
| Shape | **Shape** | **Sphere** |
| Shape | **Radius** | **0** |
| Color over Lifetime | Gradient | white(α=1) → α=0 |
| Renderer | Order in Layer | 115 |

**호출 예시:**
```csharp
Quaternion rot = Quaternion.LookRotation(swingDirection);
VFXSpawner.Spawn(slashVfxPrefab, hitPoint, rot, 0.4f);
```

**autoDestroySeconds:** 0.4

---

### 4.7 Smoke_VFX (지속형 / SpawnAttached / Slow)

**경로:** `_Project/Prefabs/Effect/Smoke_VFX.prefab`
**사용처:** `OnHitEffectRegistry.ApplySlow` (line 130) — SlowOnHit 부착형
**텍스처/머티리얼:** `MMVFX/Smoke.png` + `VFX/SmokeMat.mat`

| 모듈 | 항목 | 값 |
|---|---|---|
| Main | Duration | 1.5 |
| Main | **Looping** | **ON** |
| Main | Start Lifetime | 0.8 |
| Main | Start Speed | 0.8 |
| Main | Start Size | 0.5 |
| Main | Simulation Space | Local (부착이라 따라가야 함) |
| Emission | Rate over Time | 6 |
| Emission | Bursts | (없음) |
| Shape | Shape | Cone, Angle 25, Radius 0.1 (위향) |
| Color over Lifetime | Gradient | (0.5, 0.5, 0.55, 0.6) → α=0 |
| Size over Lifetime | Curve | 0.8 → 1.4 |
| Renderer | Order in Layer | 105 |

**호출 예시 (ApplySlow에서):**
```csharp
VFXSpawner.SpawnAttached(smokeVfxPrefab, victim.transform, Vector3.zero, _slowDuration);
```

---

### 4.8 Fire_VFX (지속형 / SpawnAttached / Burn)

**경로:** `_Project/Prefabs/Effect/Fire_VFX.prefab`
**사용처:** `OnHitEffectRegistry.ApplyBurn` (line 178) — BurnOnHit 부착형 (`duration` 동안 화상 DoT)
**텍스처/머티리얼:** `MMVFX/Fire.png` + `VFX/FireMat.mat`

| 모듈 | 항목 | 값 |
|---|---|---|
| Main | Duration | 1.0 |
| Main | **Looping** | **ON** |
| Main | Start Lifetime | 0.6 |
| Main | Start Speed | 1.5 |
| Main | Start Size | 0.5 |
| Main | Simulation Space | Local |
| Emission | Rate over Time | 12 |
| Shape | Shape | Cone, Angle 20, Radius 0.15 (상승) |
| Color over Lifetime | Gradient | (1, 0.7, 0.2, 1) → (1, 0.3, 0.1, 0) |
| Size over Lifetime | Curve | 1.0 → 0.5 |
| Renderer | Order in Layer | 115 |

**호출 예시 (ApplyBurn에서):**
```csharp
VFXSpawner.SpawnAttached(fireVfxPrefab, victim.transform, Vector3.zero, duration);
```

---

### 4.9 Electricity_VFX (단발 / ChainHitVFX 자식 후보)

**경로:** `_Project/Prefabs/Effect/Electricity_VFX.prefab`
**사용처:** `OnHitEffectRegistry.ApplyChain` (line 148) — CL-201에서 `ChainHitVFX.prefab` 자식 임팩트로 활용
**텍스처/머티리얼:** `MMVFX/Electricity.png` + `VFX/ElectricityMat.mat`

| 모듈 | 항목 | 값 |
|---|---|---|
| Main | Duration | 0.4 |
| Main | Looping | OFF |
| Main | Start Lifetime | 0.3 |
| Main | Start Speed | 3 |
| Main | Start Size | 0.5 |
| Main | Simulation Space | World |
| Emission | Bursts | Time=0, Count=8 |
| Shape | Shape | Sphere, Radius 0.15 |
| Color over Lifetime | Gradient | (1, 0.95, 0.3, 1) → α=0 |
| Size over Lifetime | Curve | 1.2 → 0.6 |
| Renderer | Order in Layer | 125 |

**autoDestroySeconds:** 0.4 (CL-201 ChainHitVFX 호출 시 0.3과 정렬)

---

### 4.10 Whirlwind_VFX (지속형 / WindAOEVFX 자식 후보)

**경로:** `_Project/Prefabs/Effect/Whirlwind_VFX.prefab`
**사용처:** `OnHitEffectRegistry.ApplyWindBlade` (line 197) — CL-201에서 `WindAOEVFX.prefab` 자식 광역 표시
**텍스처/머티리얼:** `MMVFX/Whirlwind.png` + `VFX/WhirlwindMat.mat`

| 모듈 | 항목 | 값 |
|---|---|---|
| Main | Duration | 0.5 |
| Main | **Looping** | **ON** |
| Main | Start Lifetime | 0.4 |
| Main | Start Speed | 2 |
| Main | Start Size | 0.4 |
| Main | Simulation Space | Local |
| Emission | Rate over Time | 20 |
| Shape | Shape | Circle, Radius 1.0 (인스턴스화 시 동적 조정 — cl201 plan line 184~195) |
| Color over Lifetime | Gradient | white(α=0.9) → α=0 |
| Renderer | Order in Layer | 115 |

**주의:** Whirlwind 반경은 CL-201 `SpawnWindVFX()` 에서 `_windBladeWidth × 1.5` 로 동적 덮어쓰기.

---

### 4.11 IceBlockFront_VFX (지속형 / Freeze 페어 — 앞)

**경로:** `_Project/Prefabs/Effect/IceBlockFront_VFX.prefab`
**사용처:** `OnHitEffectRegistry.ApplyFreeze` (line 139) — FreezeOnHit. Back과 페어로 동시 SpawnAttached.
**텍스처/머티리얼:** `MMVFX/IceBlockFront.png` + `VFX/IceBlockFrontMat.mat`

| 모듈 | 항목 | 값 |
|---|---|---|
| Main | Duration | 1.0 |
| Main | **Looping** | **ON** |
| Main | Start Lifetime | 1.0 |
| Main | Start Speed | 0 (정적) |
| Main | Start Size | 1.0 |
| Main | Simulation Space | Local (부착, 적과 함께 이동) |
| Emission | Rate over Time | 0 |
| Emission | Bursts | Time=0, Count=1 |
| Shape | Shape | Sphere, Radius 0 |
| Color over Lifetime | Gradient | white(α=1) → α=0 (마지막 200ms 페이드) |
| Renderer | Order in Layer | **105** (적 sprite 앞) |
| Renderer | Sorting Layer | Foreground |

**호출 (ApplyFreeze 에서, Back과 페어):**
```csharp
VFXSpawner.SpawnAttached(iceFrontPrefab, victim.transform, Vector3.zero, magnitudeSeconds);
VFXSpawner.SpawnAttached(iceBackPrefab,  victim.transform, Vector3.zero, magnitudeSeconds);
```

---

### 4.12 IceBlockBack_VFX (지속형 / Freeze 페어 — 뒤)

**경로:** `_Project/Prefabs/Effect/IceBlockBack_VFX.prefab`
**사용처:** Front 페어. 적 스프라이트 뒤에 깔리는 얼음 후면.
**텍스처/머티리얼:** `MMVFX/IceBlockBack.png` + `VFX/IceBlockBackMat.mat`

Front와 동일 설정, 단 다음만 다름:

| 모듈 | 항목 | 값 |
|---|---|---|
| Renderer | Order in Layer | **95** (적 sprite 뒤) |
| Renderer | Sorting Layer | `Default` (적과 같은 레이어, Order로만 분리) |

---

## 5. 호출부 매핑 (코드 검증 완료)

`OnHitEffectRegistry.cs` 확인 결과(line 28~244) 다음 5개 Apply* 메서드가 VFX 호출 후보:

| Apply 메서드 | 라인 | 매칭 VFX | 호출 형태 | 본 티켓 vs CL-201 |
|---|---|---|---|---|
| `ApplyBurn` | 178 | Fire_VFX | `SpawnAttached(victim, _, duration)` | 본 티켓 (직접 hook 가능) |
| `ApplyFreeze` | 139 | IceBlockFront_VFX + IceBlockBack_VFX 페어 | `SpawnAttached × 2` | 본 티켓 |
| `ApplySlow` | 130 | Smoke_VFX | `SpawnAttached(victim, _, _slowDuration)` | 본 티켓 |
| `ApplyChain` | 148 | Electricity_VFX (자식) | `ChainHitVFX.prefab` 통해 | **CL-201** |
| `ApplyWindBlade` | 197 | Slash_VFX + Whirlwind_VFX (자식) | `WindAOEVFX.prefab` 통해 | **CL-201** |

**훅 없는 5종** (Star/Bolt/Light/Flash/Dust): 향후 호출부 후보 — 보스 처치, 픽업 스폰, 강타 임팩트, 점프 착지 등. 본 티켓은 프리팹만 만들고 호출은 별도 티켓.

> **본 티켓 범위 주의:** OnHitEffectRegistry에 SerializeField 추가 + Apply* 호출부 변경은 **CL-201 영역**. 본 티켓은 프리팹 12종 제작 + Test Window 검증까지만.

---

## 6. 검증 체크리스트

각 프리팹 완성 직후 `Tools > LostMemory > VFX > Spawner Test` 실행. Console에 `[VFXSpawnerTest]` 로그 3줄 정상 + 시각적으로 보임/사라짐 확인.

| # | 프리팹 | Spawn (world) | SpawnAttached | Despawn |
|---|---|---|---|---|
| 1 | Star_VFX | ☐ | ☐ (선택) | ☐ |
| 2 | Bolt_VFX | ☐ | ☐ (선택) | ☐ |
| 3 | Light_VFX | ☐ | ☐ (선택) | ☐ |
| 4 | Flash_VFX | ☐ | ☐ (선택) | ☐ |
| 5 | Dust_VFX | ☐ | ☐ (선택) | ☐ |
| 6 | Slash_VFX | ☐ | ☐ (선택) | ☐ |
| 7 | Smoke_VFX | ☐ | ☐ (필수, 부착형) | ☐ |
| 8 | Fire_VFX | ☐ | ☐ (필수, 부착형) | ☐ |
| 9 | Electricity_VFX | ☐ | ☐ (선택) | ☐ |
| 10 | Whirlwind_VFX | ☐ | ☐ (선택) | ☐ |
| 11 | IceBlockFront_VFX | ☐ | ☐ (필수, 부착형) | ☐ |
| 12 | IceBlockBack_VFX | ☐ | ☐ (필수, 부착형) | ☐ |

12행 모두 통과 시 본 티켓 완료.

---

## 7. 권장 작업 순서

ParticleSystem UI에 익숙해지는 단계 → 도입 개념 점진적 확장:

| 단계 | 프리팹 | 도입되는 새 개념 |
|---|---|---|
| 1 | **Star_VFX** | 기본 burst, Sphere shape, World space |
| 2 | **Light_VFX** | (반복 — 동일 패턴) |
| 3 | **Flash_VFX** | Order 130 (최상단) |
| 4 | **Dust_VFX** | Cone shape (수평 퍼짐) |
| 5 | **Bolt_VFX** | **단일 sprite (Count=1, Radius 0)** + 회전(`Quaternion`) 호출 |
| 6 | **Slash_VFX** | (반복 — 단일 sprite + 회전) |
| 7 | **Smoke_VFX** | **Loop ON** + **Local space** + **SpawnAttached** + **Rate over Time** |
| 8 | **Fire_VFX** | (반복 — Loop+Attached) |
| 9 | **Electricity_VFX** | jitter Sphere (Bolt와 다름) |
| 10 | **Whirlwind_VFX** | **Circle shape** + 동적 radius (CL-201 사전 검증) |
| 11 | **IceBlockFront_VFX** | Order 105, **단일 burst + 긴 lifetime** |
| 12 | **IceBlockBack_VFX** | Order 95 + Sorting Layer Default (페어 검증) |

각 단계마다 Test Window로 즉시 검증하면 나중에 디버깅할 일 줄어든다.

---

## 8. 참고

- 일반 절차: [vfx_creation_guide.md](../vfx_creation_guide.md)
- 후속 코드 교체: [cl201_onhit_chain_wind_plan.md](cl201_onhit_chain_wind_plan.md)
- API: `_Project/Scripts/Runtime/VFX/VFXSpawner.cs`
- 검증 도구: `Tools > LostMemory > VFX > Spawner Test` (`_Project/Scripts/Editor/VFX/VFXSpawnerTestWindow.cs`)
- Apply* 핸들러: `_Project/Scripts/Runtime/Combat/OnHitEffectRegistry.cs:28~244`
