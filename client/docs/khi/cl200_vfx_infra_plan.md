# CL-200: VFX 공통 인프라 및 자산 이전

## Context
모든 VFX 작업의 선행 인프라 티켓. TopDownEngine 데모 폴더에 흩어진 텍스처/머티리얼을 `_Project`로 이전하여 외부 의존성을 분리하고, VFX 스폰/자동 정리 컨벤션을 확립한다. 이후 CL-201~205 모든 티켓이 이 인프라를 사용한다.

## 작업 범위
- [ ] MMParticles 텍스처 8종 `_Project`로 복사
- [ ] Koala 효과 텍스처 4종 `_Project`로 복사
- [ ] 머티리얼 재생성 (의존성 끊고 _Project 내부 텍스처 참조)
- [ ] 폴더 컨벤션 확립
- [ ] VFXSpawner 유틸리티 도입
- [ ] 더미 프리팹으로 동작 검증

## 신규 폴더 구조
```
_Project/
├── Art/
│   └── Effects/
│       └── MMVFX/         ← 신규 (텍스처)
├── Materials/
│   └── VFX/               ← 신규 (머티리얼)
├── Prefabs/
│   └── VFX/               ← 신규 (VFX 프리팹)
└── Scripts/
    └── Runtime/
        └── VFX/           ← 신규 (VFXSpawner 등)
```

## 자산 이전 목록

### 텍스처 (총 12개)

**MMParticles 8종** (원본: `Assets/TopDownEngine/ThirdParty/MoreMountains/MMTools/Accessories/MMVFX/MMParticles/`)

| 원본 | 이전 후 |
|---|---|
| MMParticlesBolt.png | _Project/Art/Effects/MMVFX/Bolt.png |
| MMParticlesWhirlwind.png | _Project/Art/Effects/MMVFX/Whirlwind.png |
| MMParticlesSlash.png | _Project/Art/Effects/MMVFX/Slash.png |
| MMParticlesLight.png | _Project/Art/Effects/MMVFX/Light.png |
| MMParticlesStar.png | _Project/Art/Effects/MMVFX/Star.png |
| MMParticlesSmoke.png | _Project/Art/Effects/MMVFX/Smoke.png |
| MMParticlesDust.png | _Project/Art/Effects/MMVFX/Dust.png |
| MMParticlesFlash.png | _Project/Art/Effects/MMVFX/Flash.png |

**Koala 효과 4종** (원본: `Assets/TopDownEngine/Demos/Koala2D/Sprites/Effects/`)

| 원본 | 이전 후 |
|---|---|
| KoalaElectricity.png | _Project/Art/Effects/MMVFX/Electricity.png |
| KoalaFire.png | _Project/Art/Effects/MMVFX/Fire.png |
| KoalaIceBlockFront.png | _Project/Art/Effects/MMVFX/IceBlockFront.png |
| KoalaIceBlockBack.png | _Project/Art/Effects/MMVFX/IceBlockBack.png |

### 머티리얼 재생성
각 텍스처별로 `_Project/Materials/VFX/<이름>Mat.mat` 생성 (Sprites-Default 셰이더, Default 색상)

## VFXSpawner 유틸 도입

신규 파일: `LostMemory/Assets/_Project/Scripts/Runtime/VFX/VFXSpawner.cs`

```csharp
namespace LostMemory.VFX
{
    public static class VFXSpawner
    {
        // 단발성 VFX 스폰 (자동 파괴)
        public static GameObject Spawn(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation = default,
            float autoDestroySeconds = 0f,
            Transform parent = null);

        // 대상에 부착되는 VFX (지속성, 적/플레이어 따라다님)
        public static GameObject SpawnAttached(
            GameObject prefab,
            Transform target,
            Vector3 localOffset = default,
            float autoDestroySeconds = 0f);

        // 활성 VFX 인스턴스 즉시 정리
        public static void Despawn(GameObject vfxInstance);
    }
}
```

- `autoDestroySeconds = 0f`: 수동 Despawn 또는 ParticleSystem `Stop Action: Destroy`로 자동 정리
- `autoDestroySeconds > 0f`: 코루틴 또는 `Destroy(go, t)` 사용
- 추후 풀링 도입 시 이 함수 시그니처 유지하고 내부 구현만 교체

## 검증 방법
1. 더미 프리팹 작성 (ParticleSystem만 있는 빈 GameObject, MMParticlesStar 텍스처)
2. 테스트 씬에서 `VFXSpawner.Spawn(dummyPrefab, Vector3.zero, default, 1f)` → 1초 후 자동 파괴
3. `VFXSpawner.SpawnAttached(dummyPrefab, enemyTransform)` → 적이 움직일 때 따라옴
4. `VFXSpawner.Despawn(instance)` → 즉시 파괴
5. 인스펙터에서 텍스처가 `_Project` 내부 머티리얼만 참조 (TopDownEngine 의존성 없음)

## 추정 시간
0.5일 (자산 복사 + 머티리얼 재생성 + VFXSpawner 작성 + 검증)

## 의존성
- 선행: 없음
- 후속: CL-201, CL-202, CL-203, CL-204, CL-205 모두 이 인프라 의존

## 주의사항
- 텍스처 복사 시 `.meta` 파일도 같이 복사 (Import 설정 보존)하거나 Unity 에디터의 Duplicate 활용 — GUID 충돌 주의
- TopDownEngine 자산을 `_Project`로 옮겨도 라이선스 의무 유지됨. 출시 전 약관 재확인
- 빈번한 Spawn (OnHit 효과)일 경우 GC 부담 우려. 일단 단순 `Instantiate` + `Destroy`로 시작하고, 프로파일링 후 풀링 도입 검토
- 원본 TopDownEngine 폴더는 **삭제하지 말 것** (다른 데모/예제 의존 가능성)
