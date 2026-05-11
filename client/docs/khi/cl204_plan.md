# CL-204: 미소녀 5종 효과·궁극 활성·아이템 매핑

브랜치: `feat/S14P31C201-450/cl-204-magical-girl-vfx`
선행: CL-144 (미소녀 1~4) ✅ / CL-145 (미소녀 5합체) ✅ / CL-200/201/202/203 VFX 인프라 ✅
관련 자산: `Assets/_Project/Art/Characters/Sprite/girl.png` (5분할 sprite 시트)

---

## 1. Context

**왜 이 변경을 하는가**
- 기존 미소녀 시스템 (CL-144/145) 은 `MagicalGirlAI.Attack()` 이 매 1.5초 *즉시 데미지 + sprite 색상 tint* 만 적용. 7가지 속성이 모두 시각만 다름 → 차별화 약함.
- `girl.png` 5명 도트 자산 도착 → 5종 sprite 를 5종 *전혀 다른 공격 패턴* 에 1:1 매핑.
- 14개 미소녀-태그 RelicData 를 *대표 5개* + *보조 9개* 로 정리 (대표 = 5종 효과 직접 트리거, 보조 = 부수 stat 만).
- BuildSet 5티어 (현재 fusion 자동 발화) → *T/Y 키 active skill 궁극 미소녀* 로 재해석 — CL-145 자산 재사용.

**합의된 전제 (사용자 확정)**
- 5종 효과 ↔ sprite 매핑: `girl.png` 좌→우 = 1·2·3·4·5 그대로
- 평상시 5명 floating, 5세트 도달 → 궁극 활성 표시 + T/Y 키 입력으로 발동, 끝나면 5명 복귀
- 5세트 발동 시 5종 미소녀의 데미지·속도 강화 (multiplier × 1.5, 별도 VFX 변형 자산 X)
- "빛의 미소녀" 아이템 = 5번 sprite 로 재명명, 효과·태그 유지, 일반 등급
- 9개 보조 아이템은 *stat 부수효과만* 작동 (수 cap=5), 데이터 정리는 후속 별도 ticket

**비목표**
- `RelicEffectType` enum 에 5종 추가하는 데이터 모델 교체 ((나) 접근) — 본 ticket 범위 외, 후속 CL-206 (가칭) 으로 분리
- 14개 RelicData/BuildSet JSON 일괄 정리 — 후속 CL-205 (가칭)
- 멀티플레이어 동기화 (NetworkRelicEffectAuthority) — 별도 ticket
- 5세트 *VFX 변종* (Enhanced 별도 prefab) — 자산 폭증 회피, multiplier 만

---

## 2. 효과 ↔ Sprite ↔ 아이템 매핑 (확정)

### 5종 효과 ↔ Sprite

| # | Sprite 색 (girl.png) | 효과 이름 | Catalog Kind | 핵심 동작 |
|---|---|---|---|---|
| 1 | 분홍 | 화염 투사체 | Projectile | 직선 발사, 첫 타격 시 1회 데미지 |
| 2 | 갈색 | 얼음 장판 (추적) | AOEFollow | 가장 가까운 적에 부착, 6초 지속 tick 데미지 |
| 3 | 하늘 | 별 투사체 + 주변 별 | Projectile | 직선 발사 + ambient star 시각 |
| 4 | 노랑 | 블랙홀 장판 | AOEStationary | 미소녀 전방 정지 spawn, tick 데미지 + 끌어당김 |
| 5 | 빨강 | 일반 화살 | Projectile | 직선 발사 (1번보다 빠름·짧은 간격) |

### MagicalGirlVisual enum (5종 + Default)

```csharp
public enum MagicalGirlVisual
{
    Default,    // 매핑 전 placeholder
    Fire,       // 1
    Ice,        // 2
    Star,       // 3
    Blackhole,  // 4
    Arrow,      // 5
}
```

### RelicTag → Visual 매핑 (`MagicalGirlVisualPalette.FromTag`)

| RelicTag | → Visual |
|---|---|
| Fire | Fire (1) |
| Ice | Ice (2) |
| Range | Star (3) |
| Critical | Blackhole (4) |
| Health, Wind, Lightning, Luck | Arrow (5) |
| 그 외 | Default |

### 14개 RelicData → 5종 분류

| 아이템 | 매핑 Visual | 효과 타입 | 역할 |
|---|---|---|---|
| 별 모양 단추 (불) | 1 Fire | 26 Elemental | 대표 — 화염 미소녀 소환 |
| 합체 부적 (강화 불) | 1 Fire | 27 Enhanced | 보조 — 화염 미소녀 강화 |
| 얼음 결정 (얼음) | 2 Ice | 26 Elemental | 대표 — 얼음 미소녀 소환 |
| 분홍 리본 (범위) | 3 Star | 24 Summon + 14 Range | 대표 — 별 미소녀 + 범위+10% |
| 마법진 (범위) | 3 Star | 24 + 14 (0.25) | 보조 — 범위+25% |
| 우정의 증표 (범위) | 3 Star | 24 + 14 (0.5) | 보조 — 범위+50% |
| 마법소녀 합체석 (전설 범위) | 3 Star | 24 + 14 (1.0) | 보조 — 범위+100% |
| 마법소녀의 행운 (행운) | 3 Star (Luck→Arrow) → 실제는 5 Arrow | 24 + 18 Luck | 보조 — 행운+1 |
| 어둠의 미소녀 (치명타) | 4 Blackhole | 26 Elemental | 대표 — 블랙홀 미소녀 소환 |
| 빛의 미소녀 → "빛의 화살 미소녀" (체력) | 5 Arrow | 26 Elemental | 대표 — 화살 미소녀 소환 |
| 포근한 가호 (체력 primary, 미소녀 secondary) | 5 Arrow | 5 Health + 24 | 보조 — 체력+10% |
| 바람의 깃털 (바람) | 5 Arrow | 26 Elemental | 보조 — Wind→Arrow |
| 바람의 약속 (강화 바람) | 5 Arrow | 27 Enhanced | 보조 — 화살 미소녀 강화 |
| 전기 안경 (전기) | 5 Arrow | 26 Elemental | 보조 — Lightning→Arrow |

> **주의:** "마법소녀의 행운" 의 미소녀 secondary 는 Luck 이라 Arrow 로 매핑됨. Visual 충돌 (Luck→Arrow) 의도된 단순화.

---

## 3. 아키텍처 결정

### (가) 접근 — 데이터 변경 없는 시각·공격 패턴 교체

| 항목 | 값 | 근거 |
|---|---|---|
| RelicEffectType enum 변경 | 없음 | (가) 핵심 |
| BuildSet_미소녀 JSON 변경 | 없음 | 1~5 티어 그대로 (T5 RequiredCount=5 가정) |
| RelicData JSON 변경 | 1개 (`RelicData_빛의 미소녀.json` `_displayName` 만) | 효과·태그 0 변경 |
| MagicalGirlVisual enum | 7→5+Default 재정의 | enum 의미 = 효과 카테고리로 격상 |
| MagicalGirlAI.Attack() | catalog driven dispatcher | (나) 마이그레이션 시 키만 swap |
| 미소녀 cap | **5명** (지속형 2/4 포함) | 5종=5명 모델 |
| 5스택 fusion | *active skill* 모드로 재정의 | T/Y 키, CL-145 자산 재사용 |
| 강화 표현 (Type=27 / 5세트) | 데미지 ×1.5 + 발사속도 ×1.5 multiplier | 자산 추가 0 |

### Spawner 상태 모델

| 필드 | 의미 |
|---|---|
| `_girlsByVisual: Dictionary<Visual, AI>` | visual 별 1명 (cap=5) |
| `_enhancedVisuals: HashSet<Visual>` | Type=27 RelicData 보유 visual |
| `_setBonusActive: bool` | BuildSet T5 도달 (수 ≥ 5) |
| `_ultimateAvailable: bool` | T/Y 입력 가능 여부 (= setBonus) |
| `_ultimateActive: bool` | 발동 ~ burst 종료 사이 |
| `_ultimateCooldownEndsAt: float` | 공유 쿨다운 시점 (CL-145 호환) |

### Spawner 이벤트 hook

| 이벤트 | 동작 |
|---|---|
| `inventory.OnRelicAcquired(RelicData)` | _effects 검사 → Type=26/27 매칭 visual 추가/강화 |
| `inventory.OnCleared()` | 모든 미소녀 + flag 일괄 정리 (Run 종료) |
| `BuildManager.OnSetTierChanged → SetEffectApplicator → SetCount(N)` | flag 만 갱신 (수 변화 X) |
| `Update()` 내 `Keyboard.current.tKey/yKey` | ultimate 발동 |

### MagicalGirlAI 데미지/속도 계산

```text
playerAtk = combat.WeaponData.BaseDamage × stat.GetTotalMultiplier(AttackPower)
ratio     = catalog.entry.damageRatio (default 0.30)
damage    = playerAtk × ratio
            × (setBonusActive ? 1.5 : 1.0)
            × (enhanced ? 1.5 : 1.0)

interval  = catalog.entry.attackInterval (default 1.5)
            × (setBonusActive ? 0.67 : 1.0)
            × (enhanced ? 0.67 : 1.0)
```

5세트 + Enhanced 둘 다면 ×2.25 데미지 + ×0.45 간격 (이론상 약 5배 DPS 증가, 의도적 강력함).

---

## 4. 5종 VFX 상세 스펙 (Phase B 작업 시트)

`cl200_vfx_12_specs.md` 패턴. Unity Editor 에서 그대로 따라 5개 prefab 제작.

### 공통 baseline

```text
Renderer.Sorting Layer = Foreground
Renderer.Order in Layer ≥ 110
Texture Type = Sprite (2D and UI)
Texture Alpha Source = From Gray Scale
Material Shader = Sprites/Default
ParticleSystem.Main.Stop Action = None  (런타임에서 Destroy)
```

### 4.1 Girl_FireProjectile_VFX (1번)

**경로:** `_Project/Prefabs/Effect/Girl_FireProjectile_VFX.prefab`
**컴포넌트 (root):** `CircleCollider2D (radius 0.2, isTrigger=true)` + `Rigidbody2D (kinematic)` — 본체에는 `MagicalGirlProjectile` 자동 부착 (런타임 `AddComponent`).
**자식 ParticleSystem:** `Trail`

| 모듈 | 항목 | 값 |
|---|---|---|
| Main | Duration | 1.0 |
| Main | Looping | OFF |
| Main | Start Lifetime | 0.4 |
| Main | Start Speed | 0 (parent 가 이동) |
| Main | Start Size | 0.3 |
| Main | Simulation Space | World |
| Emission | Rate over Time | 30 |
| Shape | Shape | Sphere, Radius 0.05 |
| Color over Lifetime | Gradient | (1, 0.7, 0.2, 1) → α=0 |
| Renderer | Texture | `Fire.png` (CL-200) |

**Catalog entry:** `kind=Projectile, damageRatio=0.30, attackInterval=1.5, projectileSpeed=8, projectileLifetime=1.0`

### 4.2 Girl_IceField_VFX (2번)

**경로:** `_Project/Prefabs/Effect/Girl_IceField_VFX.prefab`
**컴포넌트 (root):** `MagicalGirlAOE` 자동 부착 (런타임).
**자식 ParticleSystem:** `IceField`

| 모듈 | 항목 | 값 |
|---|---|---|
| Main | Duration | 6.0 |
| Main | Looping | ON |
| Main | Start Lifetime | 0.8 |
| Main | Start Speed | 0.5 |
| Main | Start Size | 0.4 |
| Main | Simulation Space | Local |
| Emission | Rate over Time | 15 |
| Shape | Shape | Circle, Radius 1.5 |
| Color over Lifetime | Gradient | (0.6, 0.85, 1, 0.8) → α=0 |
| Renderer | Texture | `IceBlockFront.png` (CL-200) |
| Renderer | Order in Layer | 105 |

**Catalog entry:** `kind=AOEFollow, damageRatio=0.10, attackInterval=1.5, aoeRadius=1.5, aoeDuration=6, aoeTickInterval=0.3, pullSpeed=0`

> **참고:** AOE 의 `damageRatio` 는 *tick 당* 데미지. 6초 동안 0.3s 간격 = 20 tick → 누적 0.10×20 = 2.0× playerAtk.

### 4.3 Girl_StarProjectile_VFX (3번)

**경로:** `_Project/Prefabs/Effect/Girl_StarProjectile_VFX.prefab`
**컴포넌트 (root):** `CircleCollider2D (Trigger)` + `Rigidbody2D (kinematic)`.
**자식 ParticleSystem 2개:** `Core` (별 본체) + `Ambient` (주변 별)

`Core` 모듈:

| 모듈 | 항목 | 값 |
|---|---|---|
| Main | Duration | 1.0 |
| Main | Looping | OFF |
| Main | Start Lifetime | 0.5 |
| Main | Start Speed | 0 |
| Main | Start Size | 0.5 |
| Emission | Bursts | Time=0, Count=1 |
| Renderer | Texture | `Star.png` (CL-200) |
| Renderer | Color | (1, 1, 1, 1) — 흰색 + 분홍 outline 은 머티리얼/PostFX |

`Ambient` 모듈 (주변 별 floating):

| 모듈 | 항목 | 값 |
|---|---|---|
| Main | Duration | 1.5 |
| Main | Looping | ON |
| Main | Start Lifetime | 0.6 |
| Main | Start Speed | 1.0 |
| Main | Start Size | 0.2 |
| Emission | Rate over Time | 20 |
| Shape | Shape | Sphere, Radius 0.5 |
| Renderer | Texture | `Star.png` |
| Color over Lifetime | Gradient | (1, 0.7, 0.85, 1) → α=0 |

**Catalog entry:** `kind=Projectile, damageRatio=0.30, attackInterval=1.5, projectileSpeed=7, projectileLifetime=1.2`

### 4.4 Girl_Blackhole_VFX (4번)

**경로:** `_Project/Prefabs/Effect/Girl_Blackhole_VFX.prefab`
**컴포넌트 (root):** `MagicalGirlAOE` 자동 부착.
**자식 ParticleSystem 2개:** `Core` (검정 코어) + `Spiral` (보라 회오리)

`Core`:

| 모듈 | 항목 | 값 |
|---|---|---|
| Main | Duration | 4.0 |
| Main | Looping | ON |
| Main | Start Lifetime | 0.6 |
| Main | Start Speed | 0 |
| Main | Start Size | 1.5 |
| Emission | Rate over Time | 8 |
| Color over Lifetime | Gradient | (0, 0, 0, 1) → α=0 |
| Renderer | Texture | `Light.png` (검은색 tint) |

`Spiral`:

| 모듈 | 항목 | 값 |
|---|---|---|
| Main | Duration | 4.0 |
| Main | Looping | ON |
| Main | Start Lifetime | 0.5 |
| Main | Start Speed | 1.5 |
| Main | Start Size | 0.5 |
| Emission | Rate over Time | 25 |
| Shape | Shape | Circle, Radius 1.8 |
| Velocity over Lifetime | Orbital | y=3.0 (회전) |
| Color over Lifetime | Gradient | (0.5, 0.2, 0.7, 0.9) → (0.3, 0.1, 0.5, 0) |
| Renderer | Texture | `Whirlwind.png` (CL-200) |

**Catalog entry:** `kind=AOEStationary, damageRatio=0.15, attackInterval=2.0, aoeRadius=2.0, aoeDuration=4, aoeTickInterval=0.2, pullSpeed=0.3`

### 4.5 Girl_Arrow_VFX (5번)

**경로:** `_Project/Prefabs/Effect/Girl_Arrow_VFX.prefab`
**컴포넌트 (root):** `CircleCollider2D (Trigger)` + `Rigidbody2D (kinematic)`.
**자식 ParticleSystem:** `Trail`

| 모듈 | 항목 | 값 |
|---|---|---|
| Main | Duration | 0.6 |
| Main | Looping | OFF |
| Main | Start Lifetime | 0.3 |
| Main | Start Speed | 0 |
| Main | Start Size | 0.3 |
| Main | Simulation Space | World |
| Emission | Rate over Time | 25 |
| Color over Lifetime | Gradient | (1, 0.95, 0.4, 1) → α=0 |
| Renderer | Texture | `Bolt.png` (CL-200) — 회전 진행 방향 |

**Catalog entry:** `kind=Projectile, damageRatio=0.35, attackInterval=1.2, projectileSpeed=12, projectileLifetime=0.8`

> **5번 ↑ 데미지/속도/빈도** — "기본" 미소녀지만 약하지 않음. 화살의 *연사력* 으로 차별화.

---

## 5. Phase A — 코드 변경 이력 (이미 적용 ✅)

### 신규 파일 (4)

| 경로 | 내용 |
|---|---|
| `Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlAttackCatalog.cs` | enum→prefab/파라미터 매핑 ScriptableObject |
| `Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlProjectile.cs` | 1·3·5번 발사체 (직선 + Trigger 데미지) |
| `Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlAOE.cs` | 2·4번 AOE (추적/정지 + tick 데미지 + 끌어당김) |

### 수정 파일 (5)

| 경로 | 변경 |
|---|---|
| `MagicalGirlVisual.cs` | enum 7종→5종+Default, FromTag 5종 매핑 |
| `MagicalGirlAI.cs` | `Init(stat, combat, catalog)` 시그니처 추가, `Attack()` catalog dispatcher, 5세트/Enhanced multiplier 필드 |
| `MagicalGirlSpawner.cs` | visual 단위 5명 cap, T/Y 키 폴링 → fade out → fusion spawn → 발화 → fade in 복귀, `OnRelicAcquired` Type=26/27 분기, `OnCleared` 일괄 정리 |
| `MagicalGirlFusion.cs` | T/Y 자체 폴링 제거, `TriggerLaserBurst()` / `TriggerAOEPulse()` public 메서드 (spawner-driven), `using UnityEngine.InputSystem` 제거 |
| `_Project/Scripts/Runtime/Relics/SetEffectApplicator.cs` | `SetCount` 의미 변경 주석 (flag-only) |

### 신규 자산 (Phase B 에서 생성)

`MagicalGirlAttackCatalog.asset` (1개) — 메뉴 *Create → Lost Memory → Magical Girl → Attack Catalog* 로 생성.

---

## 6. Phase B — Unity Editor 작업 가이드 (사용자 수행)

### B1. girl.png 분할 (5분)

1. `Assets/_Project/Art/Characters/Sprite/girl.png` 선택 → Inspector
2. **Sprite Mode** = `Multiple`
3. **Pixels Per Unit** = `16` (기존 도트 기준; 픽셀 깨지면 32 시도)
4. **Filter Mode** = `Point (no filter)`
5. **Compression** = `None`
6. *Apply*
7. **Sprite Editor** 열기 → **Slice** → **Type=Grid By Cell Count, Column=5, Row=1** → *Slice* → *Apply*
8. 자동 생성된 5개 sprite 이름:
   - `girl_0` (분홍) → 1번 Fire 매핑
   - `girl_1` (갈색) → 2번 Ice
   - `girl_2` (하늘) → 3번 Star
   - `girl_3` (노랑) → 4번 Blackhole
   - `girl_4` (빨강) → 5번 Arrow

### B2. 5종 VFX prefab 제작 (40분)

§4 표 따라 5개 prefab 만들고 `_Project/Prefabs/Effect/` 에 저장. 공통 절차:

1. 빈 GameObject 생성 → 적절한 이름 (`Girl_FireProjectile_VFX` 등)
2. **Projectile (1·3·5):** root 에 `CircleCollider2D (Radius 0.2, isTrigger=true)` + `Rigidbody2D (BodyType=Kinematic)` 추가. `MagicalGirlProjectile` 컴포넌트는 *부착 안 함* (런타임 자동 추가).
3. **AOE (2·4):** root 에 컴포넌트 없음. `MagicalGirlAOE` 도 부착 안 함 (런타임 자동).
4. 자식 GameObject + ParticleSystem 추가, §4 표 파라미터 입력
5. Texture 는 CL-200 의 `_Project/Art/Effects/MMVFX/<Name>.png` 재활용
6. Material 은 `_Project/Art/Materials/VFX/<Name>Mat.mat` 재활용 (또는 `Sprites/Default` shader 신규)
7. *prefab 으로 저장* (`Assets/_Project/Prefabs/Effect/`)
8. CL-200 검증 도구 *Tools > LostMemory > VFX > Spawner Test* 로 시각 단독 확인 (선택)

### B3. MagicalGirlAttackCatalog 생성 (5분)

1. Project 창 → `Assets/_Project/ScriptableObjects/MagicalGirl/` 폴더 생성
2. 우클릭 → **Create → Lost Memory → Magical Girl → Attack Catalog**
3. 이름: `MagicalGirlAttackCatalog`
4. Inspector → **Entries** 5개 추가:

| Index | visual | sprite | vfxPrefab | kind | damageRatio | attackInterval | projectileSpeed | projectileLifetime | aoeRadius | aoeDuration | aoeTickInterval | pullSpeed |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 0 | Fire | girl_0 | Girl_FireProjectile_VFX | Projectile | 0.30 | 1.5 | 8 | 1.0 | - | - | - | - |
| 1 | Ice | girl_1 | Girl_IceField_VFX | AOEFollow | 0.10 | 1.5 | - | - | 1.5 | 6 | 0.3 | 0 |
| 2 | Star | girl_2 | Girl_StarProjectile_VFX | Projectile | 0.30 | 1.5 | 7 | 1.2 | - | - | - | - |
| 3 | Blackhole | girl_3 | Girl_Blackhole_VFX | AOEStationary | 0.15 | 2.0 | - | - | 2.0 | 4 | 0.2 | 0.3 |
| 4 | Arrow | girl_4 | Girl_Arrow_VFX | Projectile | 0.35 | 1.2 | 12 | 0.8 | - | - | - | - |

### B4. Player prefab 와이어링 (3분)

1. Player prefab (`TestKhi_MinimalCharacter2D.prefab` 또는 현재 사용 중인 변형) 열기
2. `MagicalGirlSpawner` 컴포넌트 찾기
3. **Attack Catalog** 필드 → 위 `MagicalGirlAttackCatalog.asset` 드래그
4. (없으면 새 필드 추가됨 — Inspector 에서 미리 변경된 spawner 의 새 SerializeField 보일 것)
5. *Save prefab*

### B5. 빛의 미소녀 이름 변경 (선택, 1분)

1. `RelicData_빛의 미소녀.json` 열기 (`_Project/ScriptableObjects/Relics/Buildset/Relics/`)
2. `_displayName` "빛의 미소녀" → **"빛의 화살 미소녀"** (또는 사용자 선호)
3. Generated SO 재생성 — 메뉴 *Tools > LostMemory > Relics > Sync Generated SO* (있다면) 또는 Unity 가 자동 동기화 (json watcher 있는지 확인)

### B6. Unity 컴파일 검증 (1분)

1. Unity Console 에 컴파일 에러 0 확인
2. 에러 있으면 → 본 문서 또는 작성자에게 알림

---

## 7. Verification

### 자산 미설정 시 fallback 동작 (Phase A 만 적용 시)
- catalog 미와이어링 → `MagicalGirlAI._catalog == null` → `Attack()` fallback 경로 → CL-144 즉시 데미지 + 흰색 placeholder sprite
- 시스템은 안 깨짐, 다만 5종 차별화 X
- *Phase B 자산 와이어링 후* 5종 효과 활성화

### Phase A 검증 (코드만, fallback 모드)

1. 미소녀 RelicData 1개 (예: 별 모양 단추) 획득 → MagicalGirlAI 1개 spawn (placeholder 흰색)
2. 적 근처 → 매 1.5초 즉시 데미지 적용 (sprite flash)
3. 미소녀 6개 시도 (예: 분홍 리본 + 마법진 + 우정의 증표 + ...) → 5명 cap 동작 확인
4. 5세트 도달 → spawner 로그 `SetBonus=True, ultimateAvailable=True`
5. T 또는 Y 키 → fusion entity 임시 spawn → 5초 (T) / 즉발 (Y) 후 5명 복귀
6. 궁극 쿨다운 25초 → T/Y 무반응 (로그)

### Phase B 검증 (자산 적용 후, 5종 차별화 모드)

1. 별 모양 단추 (Fire) 획득 → 분홍 sprite 미소녀 1명 + **화염 투사체** 발사
2. 얼음 결정 (Ice) 추가 → 갈색 sprite + **얼음 장판 추적**
3. 분홍 리본 (Range→Star) 추가 → 하늘 sprite + **별 투사체 + 주변 별**
4. 어둠의 미소녀 (Critical→Blackhole) 추가 → 노랑 sprite + **블랙홀 장판 + 끌어당김**
5. 빛의 화살 미소녀 (Health→Arrow) 추가 → 빨강 sprite + **일반 화살**
6. 5세트 도달 (5명 모임) → 5명에 multiplier 적용 (체감 데미지 × 1.5, 발사속도 ×1.5)
7. T/Y → 5명 fade out → fusion 레이저/AOE → 3-5초 후 fade in 복귀
8. 9개 보조 아이템 추가 → 미소녀 수 증가 X, 부수 stat (범위/체력/행운) 적용 확인
9. 합체 부적 (Fire Enhanced) 획득 → 화염 미소녀만 추가 multiplier (×1.5 데미지/속도)
10. Run 종료 (boss clear) → 5명 + flag 일괄 정리, 다음 run 처음부터 spawn 흐름 확인

---

## 8. (가) → (나) 마이그레이션 부담

후속 ticket CL-206 (가칭) 에서 RelicEffectType enum 5종 추가 + 데이터 모델 교체 시:

| (가) 산출 | (나) 마이그레이션 |
|---|---|
| 5종 VFX prefab | 그대로 |
| `MagicalGirlProjectile` / `MagicalGirlAOE` | 그대로 (데미지 모델 동일) |
| `MagicalGirlAttackCatalog` SO | entry 키 type 만 swap (`MagicalGirlVisual` → `RelicEffectType`) |
| `MagicalGirlAI` dispatcher | catalog lookup 키 type 변경 (한 함수) |
| `MagicalGirlVisualPalette.FromTag` | 폐기 (SetEffectApplicator switch 가 대체) |
| `MagicalGirlSpawner.HandleRelicAcquired` 인벤토리 hook | 폐기 (SetEffectApplicator 로 통합) |

부채로 남는 죽은 코드: 함수 2개 + enum 1개. 추가 비용 핵심은 **BuildSet 정책 재설계** (count 의미를 5종 별로 분리할지 통합할지) — 본 ticket 범위 외.

---

## 9. 후속 ticket 권장

| 후속 | 내용 |
|---|---|
| CL-205 (가칭) | 14개 RelicData 정리 — 9개 보조 통폐합/삭제, BuildSet 정책 재정비 |
| CL-206 (가칭) | RelicEffectType enum 5종 추가 + 14개 JSON `_effects.Type` 일괄 수정 + SetEffectApplicator switch 5 case ((나) 접근) |
| CL-207 (가칭) | 5세트 강화 VFX 변종 5종 prefab (Enhanced 변형, 자산 폭증 — 시각 차별화 더 원할 때) |
| 별도 | Ultimate 입력 hook 을 `KhiPlayerInputController` InputAction 통합 (현재 단순 `Keyboard.current` 폴링) |
| 별도 | 멀티플레이어 동기화 (NetworkRelicEffectAuthority 패턴) |

---

## 10. Open Questions (default 정해 두면 막힘 없음)

- 빛의 미소녀 정확한 새 명칭 — "빛의 화살 미소녀" 권장 / 사용자 결정
- T/Y 두 키 다 매핑 (T=레이저 / Y=AOE) — CL-145 패턴 그대로
- 궁극 쿨다운 25s — CL-145 그대로
- 5세트 강화 multiplier ×1.5 — 밸런스 후 조정 가능
- AOE damageRatio 가 *tick 당* 데미지인 점 명시 — §4.2 / §4.4 표 참고

---

## 11. 추정 시간

| Phase | 작업 | 시간 |
|---|---|---|
| A | 코드 (이미 완료 ✅) | - |
| B1 | girl.png 5분할 | 5분 |
| B2 | 5종 VFX prefab 제작 | 40분 |
| B3 | Catalog SO 생성 + entry 5개 | 5분 |
| B4 | Player prefab 와이어링 | 3분 |
| B5 | 빛의 미소녀 이름 변경 | 1분 |
| B6 | Unity 컴파일 검증 | 1분 |
| 검증 | 인게임 시나리오 1~10 | 30분 |
| **합계** | (Phase B + 검증) | **약 1시간 25분** |

---

## 12. 참고

- 일반 VFX 절차: `client/docs/vfx_creation_guide.md`
- CL-200 12종 VFX 스펙: `cl200_vfx_12_specs.md`
- CL-201 Chain·Wind: `cl201_onhit_chain_wind_plan.md`
- CL-203 트리거 VFX: `cl203_relic_trigger_fx_plan.md`
- API: `_Project/Scripts/Runtime/VFX/VFXSpawner.cs`
- 검증 도구: `Tools > LostMemory > VFX > Spawner Test`
