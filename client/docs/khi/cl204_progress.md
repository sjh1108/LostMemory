# CL-204 개발 진행 기록

**브랜치**: `feat/S14P31C201-450/cl-204-magical-girl-vfx`
**기간**: 2026-05-09 ~ 진행 중
**spec 문서**: [`cl204_plan.md`](cl204_plan.md)
**plan 임시 메모**: `~/.claude/plans/plan-tender-rabbit.md`

---

## 목적 요약

CL-144/145 미소녀 시스템 (단순 즉시 데미지 + sprite tint) 을 **5종 차별화 효과** 로 재정의:
- 5종 sprite (girl.png 분할) ↔ 5종 공격 패턴 1:1 매핑
- BuildSet 5티어 fusion → T/Y 키 active skill 궁극 미소녀
- 14개 미소녀-태그 RelicData 를 5대표 + 9보조 카테고리화
- (가) 접근: RelicData/BuildSet JSON 0건 수정, 시각·메커니즘 차별화 우선

---

## Phase A — 코드 인프라 ✅ 완료

| # | 작업 | 산출 |
|---|---|---|
| A1 | MagicalGirlVisual enum 7→5+Default 재정의 + FromTag 매핑 | [MagicalGirlVisual.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlVisual.cs) |
| A2 | MagicalGirlAttackCatalog SO 정의 | [MagicalGirlAttackCatalog.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlAttackCatalog.cs) |
| A3 | MagicalGirlProjectile / MagicalGirlAOE 컴포넌트 | [MagicalGirlProjectile.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlProjectile.cs), [MagicalGirlAOE.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlAOE.cs) |
| A4 | MagicalGirlAI catalog dispatcher | [MagicalGirlAI.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlAI.cs) |
| A5 | MagicalGirlSpawner 5명 cap + visual 단위 spawn | [MagicalGirlSpawner.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlSpawner.cs) |
| A6 | T/Y 키 입력 hook + 궁극 fade-out/in | spawner |
| A7 | MagicalGirlFusion active skill 모드 | [MagicalGirlFusion.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlFusion.cs) |
| A8 | SetEffectApplicator MagicalGirlSummon/Fusion case 변경 | [SetEffectApplicator.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Relics/SetEffectApplicator.cs) |
| A9 | 5세트 multiplier broadcast (SetSetBonusActive / SetEnhanced) | spawner + AI |
| A10 | spawner 인벤토리 hook: Type 24/26/27 모두 spawn 트리거 (5대표 다양한 Type) | spawner.HandleRelicAcquired |
| A11 | MagicalGirlAOE Slow status 적용 — CL-202 EnemyStatusEffect 위임 (Ice 적 푸르게 + 이속↓) | AOE + Catalog Entry slowMagnitude/slowDuration |

---

## Phase B — 자산 + 와이어링 ✅ 완료

| # | 작업 | 상태 |
|---|---|---|
| B1 | girl.png Sprite Mode=Multiple → 5분할 (girl_0~4) | ✅ |
| B2.1 | Girl_FireProjectile_VFX (Fire_VFX 복사 + Trigger Collider) | ✅ |
| B2.2 | Girl_IceField_VFX (마법진 + Bentley 결정 + Slow tint) | ✅ |
| B2.3 | Girl_StarProjectile_VFX (흰↔분홍 cycle + 트레일) | ✅ |
| B2.4 | Girl_Blackhole_VFX (검정 코어 + 보라 spiral + ScaleLifecycle) | ✅ |
| B2.5 | Girl_Arrow_VFX (Arrow03 sprite + Trigger Collider + Kinematic) | ✅ |
| B3 | MagicalGirlAttackCatalog.asset 5 entries 와이어링 | ✅ |
| B4 | Player prefab `MagicalGirlSpawner.attackCatalog` 와이어링 | ✅ |
| B5 | 빛의 미소녀 → "빛의 화살 미소녀" rename (선택, 사용자 결정) | (선택) |
| B6 | 인게임 5종 미소녀 효과 검증 | ✅ |

### 5종 효과 ↔ Sprite ↔ 아이템 (확정)

| # | Sprite | 효과 | Catalog Kind | 대표 아이템 |
|---|---|---|---|---|
| 1 | girl_0 분홍 | 화염 투사체 | Projectile | 별 모양 단추 (Fire) |
| 2 | girl_1 갈색 | 얼음 장판 추적 + Slow | AOEFollow | 얼음 결정 (Ice) |
| 3 | girl_2 분홍 | 별 투사체 + 트레일 | Projectile | 분홍 리본 (Range) |
| 4 | girl_3 검정 | 블랙홀 정지 + 끌어당김 | AOEStationary | 어둠의 미소녀 (Critical) |
| 5 | girl_4 빨강 | 일반 화살 (빠른 연사) | Projectile | 빛의 미소녀 (Health) |

### Catalog Entry 핵심 값

| Visual | Damage | Interval | Speed/Radius | Lifetime/Duration | Special |
|---|---|---|---|---|---|
| Fire | 0.30 | 1.5 | 8 | 1.0 | (B9.3 swap 권장: 0.5 / 2.0 / 5 / 1.4) |
| Ice | 0.10 (tick) | 1.5 | 1.5 (radius) | 6 (duration) | Slow 0.5 / 0.5 |
| Star | 0.30 | 1.5 | 7 | 1.2 | (B9.3 swap 권장: 0.2 / 0.6 / 15 / 0.6) |
| Blackhole | 0.15 (tick) | 2.0 | 2.0 (radius) | 4 (duration) | Pull Speed 0.3 |
| Arrow | 0.35 | 1.2 | 12 | 0.8 | - |

---

## Polish — B7~B10

### B7 Follow Polish ✅ 완료

5명 미소녀가 Player *뒤+위* cluster 형태로 *부드럽게 lag follow* + facing direction mirror + bob (위아래 부유).

**신규 컴포넌트**: [MagicalGirlFollower.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlFollower.cs)
- `Vector3.SmoothDamp` 으로 anchor + offset 향해 lerp
- `KhiPlayerAim.GetAimDirection()` 으로 facing 검출 → X mirror
- Bob: Y sin 진동 (amplitude/speed/phase 노출)

**MagicalGirlSpawner 변경**:
- Girls 를 anchor 자식 X → world-space spawn
- 각 girl 에 Follower 부착 + Init(anchor, aim, offset)
- `_formationOffsets` static dict → SerializeField List<FormationEntry> 로 Inspector 노출
- `clusterScale` (0.66) + `clusterCenter` (Vector2) 추가
- Update 마다 모든 follower 에 tuning sync (실시간 반영)

**Formation 좌표 (facing-right, clusterScale=0.66)**:
- Fire: (-2.5, 0.5), Ice: (-2.7, 1.5), Star: (-1.3, 1.5), Blackhole: (-2.0, 2.5), Arrow: (-1.5, 0.5)

### B8 Fire 임시 폭발 VFX ✅ 코드 완료 / 🟡 Prefab 사용자 진행

[MagicalGirlAttackCatalog.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlAttackCatalog.cs):
- Entry 에 `hitVfxPrefab: GameObject` 필드 추가

[MagicalGirlProjectile.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlProjectile.cs):
- `_hitVfxPrefab` 필드 + Init 5번째 옵션 인자
- `SpawnHitVfx()` — 적중/만료 시 Instantiate

[MagicalGirlAI.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlAI.cs):
- `SpawnProjectile` → `proj.Init(..., entry.hitVfxPrefab)`

**남은 작업 (사용자)**:
- `Girl_Fire_Burst_VFX.prefab` 제작 (one-shot ParticleSystem, Burst 20개, Fire 색상)
- Catalog Entry 0 (Fire) 의 hitVfxPrefab 슬롯에 와이어링

### B9.1 Hold Pulse + B9.2 CRT Outro/Intro ✅ 코드 완료

[ScaleLifecycle.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/MagicalGirl/ScaleLifecycle.cs) 확장:
- `holdPulseEnabled` / `holdPulseAmount` (0.05) / `holdPulseSpeed` (0.8) — hold 중 ±5% 미세 sin 펄스
- `IntroStyle` enum (Smooth / CRT) — 시작 phase 스타일
- `OutroStyle` enum (Smooth / CRT) — 종료 phase 스타일
- `crtSquishY` (0.05) — CRT 시 Y 짜부 최저점

CRT 모드 = 옛 TV 켜지고 꺼지는 효과:
- Intro CRT: 가로선 (X 0→peak, Y squish) → 동그라미 (Y squish→peak)
- Outro CRT: 동그라미 → 가로선 (Y peak→squish) → 점 (X peak→0)

`Update()` 재구조화 — Vector3 scaleMul 로 비균일 scale 지원, `Vector3.Scale(baseScale, scaleMul)` 적용.

**적용 위치**: Girl_Blackhole_VFX 의 Circle/Spiral 자식 ScaleLifecycle Inspector 에서 IntroStyle / OutroStyle = CRT 선택.

### B9.3 Catalog 값 swap 🟡 사용자 진행

코드 변경 0. `MagicalGirlAttackCatalog.asset` Inspector 에서 Fire/Star 값 차별화:
- Fire: 0.5 / 2.0 / 5 / 1.4 (한 발 강한 폭격)
- Star: 0.2 / 0.6 / 15 / 0.6 (빠른 연사)
- Arrow: 그대로 (기준 발사체)

DPS 비교: Fire +25%, Star +66%, Arrow 기준. *체감 차별화* 가 핵심.

### B10 Fusion 미소녀 sprite ✅ 코드 완료 / 🟡 자산 사용자 진행

[MagicalGirlSpawner.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlSpawner.cs):
- `fusionSprite` (Sprite) / `fusionSortingOrder` (130) / `fusionScale` (5) SerializeField
- `EnsureFusion()` 가 spawn 시 SpriteRenderer 부착 + sprite/scale 적용

**남은 작업 (사용자)**: 합체 미소녀 sprite 임포트 + Player prefab 의 Spawner.fusionSprite 슬롯에 와이어링.

### B10.1 Fusion 부유 + Formation 중앙 spawn ✅ 코드 완료

[MagicalGirlSpawner.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlSpawner.cs):
- `GetFormationCenter()` 헬퍼 — 5 offsets 평균 × scale + center
- `EnsureFusion()` 재구조화: parent-child 제거 → world-space, **MagicalGirlFollower 부착** with formationCenter offset
- `SyncFollowTuningToAll()` 확장 — fusion follower 에도 매 frame tuning + offset push

**효과**:
- Fusion 등장 시 5명 cluster 중앙 (formation center) 에서 자연스럽게 등장
- Bob 부유 + smooth lag follow + facing mirror (5명과 동일 동작)
- Cluster Center / Cluster Scale 변경 시 fusion 도 실시간 반영

---

## 신규 / 수정 파일 전체

### 신규 코드 파일

| 경로 | 역할 |
|---|---|
| `MagicalGirlAttackCatalog.cs` | enum→prefab/파라미터 매핑 SO |
| `MagicalGirlProjectile.cs` | 1·3·5번 발사체 컴포넌트 |
| `MagicalGirlAOE.cs` | 2·4번 AOE (추적/정지) 컴포넌트 |
| `MagicalGirlFollower.cs` | (B7) 부드러운 lag follow + bob + facing mirror |
| `ScaleLifecycle.cs` | (B7~B9) grow/hold/shrink + pulse + CRT 스타일 |
| `PulseScale.cs` | (시도) 지속 sin 박동 — 현재 미사용 (ScaleLifecycle 의 hold pulse 가 대체) |
| `ParticlesInward.cs` | (시도) ParticleSystem 입자 중심 향함 — 블랙홀 InwardFlow 용 (선택, 미적용) |

### 수정 코드 파일

| 경로 | 변경 |
|---|---|
| `MagicalGirlVisual.cs` | enum 7→5+Default, FromTag 5종 매핑, Get 색상 |
| `MagicalGirlAI.cs` | catalog dispatcher, multiplier 필드, hitVfxPrefab 전달 |
| `MagicalGirlSpawner.cs` | 5명 cap, visual 단위 spawn, T/Y hook, follow polish (formation/scale/center/tuning), fusion sprite/follower |
| `MagicalGirlFusion.cs` | active skill 모드, TriggerLaserBurst/TriggerAOEPulse public |
| `Relics/SetEffectApplicator.cs` | MagicalGirlSummon/Fusion case 의미 변경 (clamp, ultimate flag) |

### 자산 (사용자 작업)

| 경로 | 상태 |
|---|---|
| `Prefabs/Effect/Girl_FireProjectile_VFX.prefab` | ✅ |
| `Prefabs/Effect/Girl_IceField_VFX.prefab` | ✅ |
| `Prefabs/Effect/Girl_StarProjectile_VFX.prefab` | ✅ |
| `Prefabs/Effect/Girl_Blackhole_VFX.prefab` | ✅ |
| `Prefabs/Effect/Girl_Arrow_VFX.prefab` | ✅ |
| `Prefabs/Effect/Girl_Fire_Burst_VFX.prefab` (B8) | 🟡 진행 중 |
| `ScriptableObjects/MagicalGirl/MagicalGirlAttackCatalog.asset` | ✅ (B9.3 값 swap 진행 중) |
| `Art/Characters/Sprite/girl.png` 5분할 sliced | ✅ |
| `Art/Characters/Sprite/girl_fusion.png` (B10) | 🟡 자산 준비 |
| `Art/Effects/External/Bentley/bentley_01~06.png` (B2.2) | ✅ |
| `Art/Effects/External/Kenney/light_01~03.png` (B2.2) | ✅ |
| `Art/Materials/VFX/IceMagicCircleMat.mat` | ✅ |
| `Art/Materials/VFX/IceFlakeBentleyMat.mat` | ✅ |

### Player prefab 와이어링 (사용자)

| 슬롯 | 와이어링 상태 |
|---|---|
| MagicalGirlSpawner.attackCatalog | ✅ |
| MagicalGirlSpawner.fusionSprite | 🟡 |
| MagicalGirlSpawner.formationOffsets (5 entries) | ✅ |
| MagicalGirlSpawner.clusterScale (0.66) / clusterCenter (0,0) | ✅ |
| Catalog Entry 0 (Fire) hitVfxPrefab | 🟡 |
| Catalog Entries B9.3 값 swap (Fire/Star) | 🟡 |

---

## 검증 시나리오 — 현재 동작 확인

### 단일 미소녀
1. 별 모양 단추 (Fire) 추가 → girl_0 분홍 spawn → 화염 발사체 (1.5s 간격)
2. 분홍 리본 (Star) 추가 → girl_2 spawn → 별 투사체 + 트레일

### 5명 cluster
1. 5종 RelicData 모두 추가 → 5명 *Player 뒤+위* cluster
2. Player 좌우 이동 시 cluster 부드러운 lag follow
3. 마우스 좌우 조준 시 cluster X mirror
4. 각 미소녀 살짝 부유 (bob)

### Slow Status (Ice)
1. 얼음 결정 추가 → girl_1 Ice 미소녀
2. Ice AOE 영역 안 적 → sprite 푸르게 + 이속 50% 감소

### Blackhole 라이프사이클
1. 어둠의 미소녀 추가 → girl_3 Blackhole 미소녀
2. AOE Stationary spawn → 작게 시작 → 펼쳐짐 → 끌어당김 + tick 데미지 → 닫힘
3. (CRT 옵션 적용 시) 옛 TV 켜졌다 꺼지는 효과

### Ultimate (5세트)
1. 5명 모두 spawn → 5세트 도달 → ultimate 활성화
2. T 키 → 5명 fade out → fusion 등장 (formation center) → 5초 laser → fusion 사라짐 → 5명 fade in
3. Y 키 → 5명 fade out → fusion 등장 → AOE 화면 플래시 → 0.5s 대기 → fade in
4. Cooldown 25s

---

## 남은 / 후속 작업

### 본 ticket 마무리 (작업 중)
- B8 Girl_Fire_Burst_VFX prefab 제작 + Catalog 와이어링
- B9.3 Catalog Entry 0 (Fire) + Entry 2 (Star) 값 swap
- B10 Fusion sprite 임포트 + Player prefab 와이어링

### 후속 ticket 권장
| Ticket | 내용 |
|---|---|
| CL-205 (가칭) | 14개 RelicData 정리 — 9개 보조 통폐합/삭제, BuildSet 정책 재정비 |
| CL-206 | Burn DoT 통합 — Fire 발사체 적중 시 EnemyStatusEffect.ApplyBurn 호출 |
| CL-207 | 5세트 Enhanced VFX 변형 — 5명에 outline/glow |
| 별도 | T/Y 키 폴링 → KhiPlayerInputController InputAction 통합 |
| 별도 | NetworkRelicEffectAuthority 멀티 동기화 |
| 별도 | Star/Arrow 도 hitVfxPrefab 와이어링 (Fire 와 동일 mechanism) |
| 별도 | Fusion 합체 transition VFX (5명 모임 → 폭발 → fusion 등장) |
| 별도 | Fusion ScaleLifecycle (CRT intro = 합체 등장) |

---

## CL-204 후속 — Fusion 시각 강화 (2026-05-11)

본 ticket 의 *원래 범위 외 폴리시 작업*. 5세트 ultimate (T/Y) 의 시각 임팩트 부족 문제 해결.

상세: [cl204_followup_fusion_visual_plan.md](cl204_followup_fusion_visual_plan.md)

### 완료 항목 요약

| 영역 | 내용 |
|---|---|
| **T 키 Laser Trail VFX** | 매 tick 위치/회전 sync, Trail Rotation Offset / Spawn Offset Inspector 슬라이더, Renderer Alignment=Local 로 sprite 회전 처리 |
| **Y 키 빌드업 패턴** | Charge VFX 가 0.5s 동안 커지다 펑 → 데미지+explosion+shockwave. Charge Duration/Start/End Scale 슬라이더 |
| **Fusion Sprite 강화** | Animation frames (Sprite[] cycle), Fade In/Out, 셰이더 기반 수직 그라데이션 (PNG mask 대체) |
| **신 강림 연출** | Descent (EaseOutCubic) + Ascent (EaseInCubic), 위치/높이/속도/mirror 슬라이더 |
| **NRE 해소** | MagicalGirlFusion.Awake 의 절차적 SpriteRenderer 중복 생성 제거 |
| **Mirror 버그 해소** | MagicalGirlFollower.Init 가 초기 위치에 mirror 즉시 적용 |
| **Debug 편의** | debugSkipCooldown bool (25s 쿨다운 무시) |

### 신규 파일

- [SpriteAlphaGradient.shader](../../LostMemory/Assets/_Project/Art/Shaders/SpriteAlphaGradient.shader) — `LostMemory/Sprites/Alpha Gradient`. UV.y 기반 smoothstep, Sprites/Default 와 동일 구조 (URP 2D 호환)
- [cl204_followup_fusion_visual_plan.md](cl204_followup_fusion_visual_plan.md) — 후속 작업 상세 보고서

### Inspector 슬롯 (MagicalGirlSpawner)

| Header | 슬롯 |
|---|---|
| Fusion Visual (CL-204 B10) | fusionSprite, fusionAnimationFrames, frameInterval, fadeIn/OutDuration, gradientStart/End, descentHeight, descentDuration, ascentDuration, descentHorizontalOffset, positionOffset, mirrorXOnFacing, bobAmplitude |
| Fusion AOE Charge | aoeChargeDuration, startScale, endScale |
| Fusion VFX | fusionVfx.laserMuzzle/laserTrail/aoeExplosion/aoeShockwave/**aoeCharge** |
| Debug | debugSkipCooldown, trailRotationOffset, trailSpawnOffset |

### 미완료 (사용자 자산)

- 🔴 **Aoe Shockwave VFX prefab** — 슬롯 비어있음, Y 키 시 explosion 만 발화
- 🟡 Laser Muzzle VFX prefab — 선택 (T 키 시작점 burst)
- 🟡 Aoe Charge VFX prefab — 선택 (Y 키 빌드업 visual, 없으면 0.5s 무visual 대기)
- 🟡 Fusion Animation Frames — 정적 sprite 그대로
- 🟡 Fusion Gradient Start/End 슬라이더 ON

---

## 참고

- **spec**: [`cl204_plan.md`](cl204_plan.md)
- **후속 작업 상세**: [`cl204_followup_fusion_visual_plan.md`](cl204_followup_fusion_visual_plan.md)
- **5종 VFX 스펙**: cl204_plan.md §4
- **CL-200 12종 VFX 텍스처**: [`cl200_vfx_12_specs.md`](cl200_vfx_12_specs.md) (재활용)
- **CL-202 EnemyStatusEffect**: [EnemyStatusEffect.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Enemies/EnemyStatusEffect.cs) (Slow/Freeze/Burn 위임)
