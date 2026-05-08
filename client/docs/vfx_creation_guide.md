# VFX 제작 가이드 (CL-201 이후)

> CL-200에서 구축한 VFX 공통 인프라를 활용해 새 이펙트를 만드는 절차.
> CL-201~205 및 이후 모든 VFX 추가 작업이 본 문서를 따른다.

## 사전 조건 (CL-200 산출물)

이미 다음이 준비되어 있다:

| 산출물 | 경로 | 용도 |
|---|---|---|
| `VFXSpawner` API | `_Project/Scripts/Runtime/VFX/VFXSpawner.cs` | 모든 VFX 스폰/정리 진입점 |
| `VFXSpawnerTestWindow` | `_Project/Scripts/Editor/VFX/VFXSpawnerTestWindow.cs` | Editor 메뉴 `Tools > LostMemory > VFX > Spawner Test` |
| MMVFX 텍스처 12종 | `_Project/Art/Effects/MMVFX/` | Bolt/Whirlwind/Slash/Light/Star/Smoke/Dust/Flash/Electricity/Fire/IceBlockFront/IceBlockBack |
| MMVFX 머티리얼 12종 | `_Project/Art/Materials/VFX/` | 위 텍스처별 매칭 머티리얼 (Sprites/Default 셰이더) |
| 검증 더미 프리팹 | `_Project/Prefabs/Effect/VFXDummy_Test.prefab` | smoke test용 |

**원칙:**
- 본 인프라를 사용하지 않고 직접 `Instantiate`/`Destroy`하지 말 것 (풀링 도입 시 일괄 마이그레이션 안 됨)
- TopDownEngine 자산 직접 참조 금지 (`_Project/`로 옮긴 사본만 사용)

---

## 새 VFX 만드는 표준 절차

### Step 1. 텍스처 준비 (필요 시)

기존 12종 중 적절한 게 있으면 건너뛰고 Step 2로. 새 텍스처가 필요하면:

1. **소스 결정**
   - TopDownEngine 데모 폴더의 추가 자산 → Unity Editor `Ctrl+D`로 복제 (OS 복사 금지, GUID 충돌)
   - 직접 그린 PNG → `_Project/Art/Effects/MMVFX/` (또는 카테고리 서브폴더)에 추가

2. **Import 설정** (Project 창에서 PNG 선택 후 Inspector)
   - **Texture Type**: `Sprite (2D and UI)` ← 필수 (Default 타입은 Sprites/Default 슬롯이 ⊘ 거부)
   - **Alpha Is Transparency**: ✓ (체크)
   - **Alpha Source**: 텍스처 외형에 따라 결정 (아래 표 참조)
   - **`Apply`** 클릭

#### Alpha Source 결정 트리

| 텍스처 미리보기 외형 | Alpha Source | 비고 |
|---|---|---|
| 그림 + **체커 무늬 배경** (이미 투명) | `Input Texture Alpha` | 직접 그린 sprite, 일부 에셋 팩 |
| 그림 + **검정 배경** (불투명) | `From Gray Scale` | MoreMountains MMParticles, Koala Effects 류 |
| 그림 + **다른 단색 배경** | 별도 처리 | Photoshop으로 알파 추가 후 import |

검증: 머티리얼 연결 후 화면에 `흰 그림 + 검정 사각형`으로 보이면 → `From Gray Scale`로 변경.

### Step 2. 머티리얼 생성

1. `_Project/Art/Materials/VFX/` 우클릭 → Create → Material
2. 이름: `<효과명>Mat.mat` 형식 (예: `LightningBoltMat.mat`, `BurnFlameMat.mat`)
3. Inspector 설정:
   - **Shader**: `Sprites/Default` ← 본 프로젝트의 검증된 정답
   - **Sprite Texture** 슬롯 (오른쪽 작은 박스, 라벨 회색이어도 정상): 매칭 텍스처 드래그
   - **Tint**: 흰색(1,1,1,1) — 기본 색상. 색조 적용 원하면 변경
   - **Pixel Snap**: 끄기 (기본값)

#### 절대 쓰지 말 것 (CL-200 검증 결과)

- `Universal Render Pipeline/2D/Sprite-Unlit-Default` — ParticleSystem 비호환 (검정 미리보기)
- `Universal Render Pipeline/Particles/Unlit` — 분홍 (URP 2D Renderer에서 stripped)
- `Universal Render Pipeline/Unlit` — 분홍 (3D 파이프라인용)

### Step 3. ParticleSystem 프리팹 제작

1. **Hierarchy**: `GameObject > Effects > Particle System` → 이름 변경 (예: `LightningBolt_VFX`)

2. **Particle System 설정** (Inspector):

#### Main 모듈 (필수 체크)
- **Duration**: 효과 길이 (보통 1~2초)
- **Looping**: **OFF** (대부분의 일회성 VFX는 Loop 안 함. 지속성 VFX만 ON)
- **Start Delay**: **0** ← 필수. `VFXSpawner.Spawn(autoDestroy=N)` 호출 시 N초 후 파괴되는데, Start Delay가 있으면 파티클 안 보이고 사라짐
- **Start Lifetime**: 파티클 1개 수명. autoDestroy 값보다 짧거나 같게
- **Start Speed**: 1~5 (효과 따라)
- **Start Size**: 0.3~1.0 (게임 카메라 줌에 맞춰)
- **Stop Action**: `None` (Despawn API가 처리)
- **Simulation Space**: `Local` (부착형) 또는 `World` (월드 고정형)

#### Emission 모듈
- **Rate over Time**: 지속 분사면 N (초당 입자 수). 일회성 burst면 0
- **Bursts**: 일회성 효과면 Time=0, Count=20~50 추가

#### Shape 모듈
- 효과 모양에 맞게 (Sphere, Cone, Box 등)
- 점 폭발: Sphere, Radius 0.1~0.3
- 방향성 효과: Cone

#### Color over Lifetime / Size over Lifetime (선택)
- 페이드 아웃, 크기 변화 등 — Plan에 따라

#### Renderer 모듈 (필수)
- **Render Mode**: `Billboard` (2D 게임 권장)
- **Material**: Step 2에서 만든 머티리얼 드래그
- **Sorting Layer**: 게임 씬과 일치 (Default, Foreground 등)
- **Order in Layer**: `100` 이상 권장 (타일맵/스프라이트 위로 보이게)

3. **프리팹화**: Hierarchy의 GameObject를 `_Project/Prefabs/Effect/` (또는 `Prefabs/VFX/`)로 드래그 → Hierarchy 인스턴스 삭제

### Step 4. 코드에서 호출

VFXSpawner API 사용. 절대 `Instantiate`/`Destroy` 직접 쓰지 말 것.

```csharp
using LostMemory.VFX;
using UnityEngine;

public class MyAbility : MonoBehaviour
{
    [SerializeField] private GameObject impactVfx;     // 인스펙터에 프리팹 할당
    [SerializeField] private GameObject buffVfx;

    void OnHit(Vector3 hitPoint)
    {
        // 1) 일회성 — 월드 좌표에 스폰, 1초 후 자동 파괴
        VFXSpawner.Spawn(impactVfx, hitPoint, Quaternion.identity, 1f);

        // 2) 일회성 + 회전 — 공격 방향에 맞춤
        Quaternion rot = Quaternion.LookRotation(direction);
        VFXSpawner.Spawn(impactVfx, hitPoint, rot, 1.5f);
    }

    void ApplyBuff(Transform target)
    {
        // 3) 부착형 — 타겟이 움직이면 따라옴, 5초 후 자동 파괴
        VFXSpawner.SpawnAttached(buffVfx, target, Vector3.zero, 5f);
    }

    void CancelBuff(GameObject vfxInstance)
    {
        // 4) 즉시 정리 — autoDestroy 도달 전 강제 종료
        VFXSpawner.Despawn(vfxInstance);
    }
}
```

#### API 시그니처 요약

```csharp
// 단발성 스폰 (월드 좌표). autoDestroySeconds=0이면 수동 정리 필요
VFXSpawner.Spawn(prefab, position, rotation = default, autoDestroySeconds = 0f, parent = null)

// 부착형 스폰 (타겟 추적). localOffset은 부모 로컬 좌표
VFXSpawner.SpawnAttached(prefab, target, localOffset = default, autoDestroySeconds = 0f)

// 즉시 정리. 풀링 도입 시 내부만 "return to pool"로 교체됨
VFXSpawner.Despawn(vfxInstance)
```

---

## 검증 (Test Window 사용)

새 프리팹 만들 때마다:

1. Unity 메뉴 `Tools > LostMemory > VFX > Spawner Test` 클릭
2. 새 프리팹을 `Prefab` 슬롯에 드래그
3. ▶️ Play 버튼으로 Play 모드 진입
4. **테스트 1 — Spawn**: `Spawn (world origin)` 클릭 → 화면에 효과 보이고 `Auto Destroy` 시간 후 사라짐
5. **테스트 2 — SpawnAttached**: 임의 GameObject를 `Attach Target`에 드래그 → `Spawn Attached` 클릭 → 그 GameObject 따라가는지 확인
6. **테스트 3 — Despawn**: Spawn 후 `Despawn` 클릭 → 즉시 사라짐
7. Console에 `[VFXSpawnerTest] ...` 로그 3줄 모두 정상

❌ 효과 안 보이면: [트러블슈팅](#트러블슈팅) 참조.

---

## 트러블슈팅

### 화면에 아무것도 안 보임
1. **`Start Delay`가 0인지 확인** — 1초로 되어있으면 autoDestroy=1과 충돌해서 안 보임
2. **Order in Layer**: 0이면 타일맵 뒤에 가려질 수 있음 → 100 이상으로
3. **Sorting Layer**: 씬의 카메라 컬링 마스크에 포함되는 레이어인지 확인
4. **Scene 카메라 위치**: VFX 스폰 좌표 근처로 카메라 이동

### 흰 그림 + 검정 사각형으로 보임
- 텍스처 **Alpha Source**를 `From Gray Scale`로 변경 + Apply
- MoreMountains/Koala 류 텍스처는 검정 배경이라 항상 이 설정 필요

### 머티리얼이 분홍색 (마젠타)
- 셰이더 호환 문제. `Sprites/Default`로 변경 (URP 변종은 본 프로젝트에서 작동 안 함)

### 머티리얼이 검정색
- `URP 2D Sprite-Unlit-Default` 셰이더가 ParticleSystem 비호환. `Sprites/Default`로 변경

### 텍스처 슬롯 드래그 시 ⊘ (금지) 표시
- 텍스처 **Texture Type**이 `Default`로 되어있음 → `Sprite (2D and UI)`로 변경 + Apply

### 텍스처 슬롯이 회색으로 보임
- **정상 시각 스타일**임. 클릭/드래그는 작동함. 회색 라벨에 속지 말 것

### Spawn 후 파티클이 너무 빨리 사라짐
- `autoDestroySeconds`가 너무 짧음. 효과의 `Start Lifetime` 이상으로 늘리기

### `[VFXSpawner] Spawn 호출에 prefab=null` 경고
- 호출 측에서 프리팹 할당 누락. Inspector 확인

---

## 폴더 컨벤션

```
_Project/
├── Art/
│   ├── Effects/
│   │   ├── MMVFX/       — TopDownEngine 출신 텍스처
│   │   ├── Khi/         — 캐릭터 전용 (선택)
│   │   └── <Category>/  — 새 카테고리는 서브폴더로
│   └── Materials/
│       └── VFX/         — 모든 VFX 머티리얼
├── Prefabs/
│   └── Effect/          — VFX 프리팹 (또는 VFX/)
└── Scripts/
    ├── Runtime/VFX/     — VFXSpawner 등 런타임 코드
    └── Editor/VFX/      — Test Window 등 에디터 도구
```

**주의:** 신규 폴더 생성 시 `.meta` 파일 반드시 git 커밋 (안 하면 팀원 import 시 GUID 새로 할당되어 참조 깨짐).

---

## 체크리스트 (새 VFX 추가 시)

- [ ] 텍스처: Texture Type = Sprite (2D and UI), Alpha Is Transparency ✓
- [ ] 텍스처: Alpha Source 결정 (검정 배경이면 From Gray Scale)
- [ ] 머티리얼: Shader = Sprites/Default
- [ ] 머티리얼: Sprite Texture 슬롯에 텍스처 연결, 미리보기에 텍스처 모양 보임
- [ ] 프리팹: Start Delay = 0, Looping = OFF (일회성 효과)
- [ ] 프리팹: Renderer Material 할당, Order in Layer ≥ 100
- [ ] 코드: `VFXSpawner` API 사용, 직접 `Instantiate` 금지
- [ ] 검증: Test Window로 Spawn/SpawnAttached/Despawn 3개 모두 통과
- [ ] git: 신규 폴더의 `.meta` 파일 커밋 포함

---

## 풀링 도입 (미래)

`VFXSpawner` API 시그니처(Spawn/SpawnAttached/Despawn)는 **불변**. 추후 풀링 도입 시:
1. `VFXSpawner` 내부 구현만 교체 (`Instantiate` → 풀에서 꺼내기, `Destroy` → 풀로 반납)
2. 호출 측 코드 변경 불필요

빈번한 OnHit VFX (1초당 수십 회) 도입 시점에 프로파일링 후 결정. 현재 0.5일 분량 작업이라 후속 티켓.

---

## 참고 자료

- 본 가이드 작성 근거: CL-200 plan 파일 — `C:\Users\SSAFY\.claude\plans\plan-200-md-atomic-widget.md`
- VFXSpawner 코드: `_Project/Scripts/Runtime/VFX/VFXSpawner.cs`
- 검증 도구: `_Project/Scripts/Editor/VFX/VFXSpawnerTestWindow.cs`
- 셰이더 fallback chain 참고: `_Project/Scripts/Runtime/Combat/OnHitEffectRegistry.cs:262-266`
