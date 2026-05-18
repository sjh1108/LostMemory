# CL-204 후속: Fusion 미소녀 시각 효과 — 구현 완료 보고서

> 메모리 규칙: Plan 은 `client/docs/khi/` 에 저장. `~/.claude/plans/` 사용 금지.

## Context

CL-204 의 5세트 fusion 미소녀가 *단일 sprite + 즉시 등장/소멸* 로 임팩트 약했음. T 키 (laser burst) / Y 키 (AOE) 의 시각 강화 + fusion 등장 연출 풍부화.

## 완료된 작업

### A. 기반 정리

- **MagicalGirlFusion.Awake** 의 절차적 SpriteRenderer 생성 제거 — `EnsureFusion()` 의 SpriteRenderer 와 중복돼 NRE 발생하던 것 해소
- 미사용 SerializeField (`spriteSize`, `spriteSortingOrder`) 제거

### B. T 키 Laser 시각 (완료)

#### B.1 Laser Trail VFX 시스템
- prefab slot: `MagicalGirlSpawner.fusionVfx.laserTrail`
- 매 tick `boxCenter` 위치 + `angleDeg + offset` 회전 sync
- **Trail Rotation Offset** Inspector 슬라이더 (부모 GameObject Z 회전)
- **Trail Spawn Offset** Vector2 (빔 local 좌표 기준 위치 보정)
- 파티클 sprite 회전은 prefab 의 **Renderer Alignment=Local** 옵션으로 처리 (코드 sync 불필요)
- Trail 은 fusion 자식 X (root 레벨 spawn) — fusion scale 5x 영향 회피
- 비정상 종료 시 `OnDestroy` 가 orphan trail 정리

#### B.2 Laser Muzzle VFX (slot 만 존재)
- prefab slot: `fusionVfx.laserMuzzle`
- 비어있어도 동작 정상 (시작점 burst 없을 뿐)
- prefab 작성은 사용자 선택

### C. Y 키 AOE 시각 (완료)

#### C.1 빌드업 → 폭발 패턴
- Y 키 → `ChargeAndFireAOECoroutine` 시작
- Charge 단계: `aoeChargeDuration` 동안 charge VFX 가 `aoeChargeStartScale → aoeChargeEndScale` 으로 lerp
- Charge 종료 → 데미지 + explosion + shockwave + camera shake
- Inspector 슬라이더: `Aoe Charge Duration`, `Aoe Charge Start Scale`, `Aoe Charge End Scale`

#### C.2 VFX prefab 슬롯
- `fusionVfx.aoeCharge` — 빌드업 시 grown-up VFX (SpriteRenderer 기반 권장 — ParticleSystem 의 Hierarchy scaling 신경 안 써도 됨)
- `fusionVfx.aoeExplosion` — 펑 burst (one-shot ParticleSystem)
- `fusionVfx.aoeShockwave` — 링 충격파 (미작성)

### D. Fusion Sprite — Animation / Fade / Gradient

#### D.1 Sprite 애니메이션
- `fusionAnimationFrames` (Sprite[]) — frame 순환 cycle
- `fusionAnimationFrameInterval` (float) — 속도
- 비어있으면 단일 `fusionSprite` 사용 (호환성)

#### D.2 Fade In/Out
- `fusionFadeInDuration` / `fusionFadeOutDuration` — alpha 0↔1 페이드
- DespawnFusion 가 fade out 후 destroy

#### D.3 셰이더 기반 그라데이션 (PNG mask 대체)
- 신규 셰이더: [`LostMemory/Sprites/Alpha Gradient`](../../LostMemory/Assets/_Project/Art/Shaders/SpriteAlphaGradient.shader)
- UV.y 기반 smoothstep — Sprites/Default 와 동일 구조 (URP 2D 호환)
- Inspector 슬라이더: `Fusion Gradient Start`, `Fusion Gradient End`
- MaterialPropertyBlock 으로 매 frame 실시간 갱신 (인스턴스화 회피)

### E. 신 강림 연출 (완료)

#### E.1 Descent / Ascent
- 등장 시 `fusionDescentHeight` 만큼 위에서 시작 → EaseOutCubic 으로 내려옴
- 소멸 시 같은 거리 위로 EaseInCubic 으로 상승
- `fusionDescentDuration` / `fusionAscentDuration` — 페이드와 독립된 속도 슬라이더
- `fusionPositionOffset` (Vector2) — 최종 위치 미세 조정
- `fusionDescentHorizontalOffset` — 강림 시작점의 X (대각 강림 가능)
- `fusionBobAmplitude` — 둥실둥실 진폭 (기본 0 = 강림 후 정지)
- `fusionMirrorXOnFacing` — facing 따라 X 미러 ON/OFF

#### E.2 Mirror 버그 해소
- `MagicalGirlFollower.Init()` 가 초기 transform.position 설정 시 mirror 적용 — 첫 frame snap 방지

### F. Debug 편의

- `debugSkipCooldown` — 25초 쿨다운 무시 (VFX 반복 검증용)

## 변경된 파일

| 경로 | 변경 |
|---|---|
| [MagicalGirlFusion.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlFusion.cs) | Awake 절차 SpriteRenderer 제거, ChargeAndFireAOECoroutine 추가, Trail spawn 위치/회전 offset 적용, FusionVfxBundle.aoeCharge 추가, OnDestroy 정리, AOE 빌드업 패턴 |
| [MagicalGirlSpawner.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlSpawner.cs) | Fusion sprite/animation/fade/gradient/charge SerializeField 다수 추가, Descent/Ascent 코루틴, SpriteAlphaGradient 셰이더 적용, Mirror toggle, 다양한 getter |
| [MagicalGirlFollower.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/MagicalGirl/MagicalGirlFollower.cs) | Init 에서 mirror 즉시 적용 |

## 신규 자산

- [SpriteAlphaGradient.shader](../../LostMemory/Assets/_Project/Art/Shaders/SpriteAlphaGradient.shader) — 수직 알파 그라데이션 셰이더

## 사용자 자산 작업 (미완료 / 선택)

- **Aoe Shockwave VFX prefab** (B4) — 링 충격파 (현재 슬롯 비어있음, Y 키 시 explosion 만 발화)
- **Laser Muzzle VFX prefab** (B1) — T 키 시작점 burst (선택)
- **Aoe Charge VFX prefab** — Y 키 빌드업 visual (선택, 없으면 0.5s 무visual 대기)
- **Fusion Animation Frames** (Sprite[]) — 정적 sprite → 움직이는 sprite
- **Fusion Gradient Start/End** 슬라이더 ON → 하반신 페이드 효과

## Verification

1. Unity 컴파일 에러 0 ✓
2. T 키 → Fusion 강림 + 빔 + Trail + 페이드 OUT + 승천 ✓
3. Y 키 → Fusion 강림 + Charge 빌드업 + Explosion 펑 (현재 Shockwave 없음) ✓
4. 좌/우 facing 모두 자연스럽게 mirror 적용 ✓
5. 5세트 도달 + 쿨다운 25초 (debugSkipCooldown OFF 시) 정상 ✓
6. NRE 0 ✓
7. fusion 자산 (animation/gradient) 와이어링 없어도 호환 동작 ✓

## 후속 권장 (별도 ticket)

- Shockwave prefab + 와이어링 (10분 작업)
- 사용자 자산 (animation frames, gradient values) tuning
- 합체 transition VFX (5명 모이는 → 폭발 → fusion 등장)
- 멀티플레이어 동기화
