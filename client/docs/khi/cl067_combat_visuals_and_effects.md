# CL-067 기본 전투 시각화 + 패링 이펙트 연결 (1부 무기 / 2부 슬래시 / 3부 Finisher)

작성일: 2026-04-24

대상 씬: `LostMemory/Assets/Scenes/test_khi.unity`

지라/브랜치: `feat/S14P31C201-262/vfx-sfx-기본-전투-이펙트-패링-이펙트-연결`

**상태**: 🟢 **코드 완료 / 수동 검증 진행 중** — 주요 시각화·피드백 기능 구현 완료.
Inspector 튜닝으로 전투 체감 반복 실험 중.

## 목적

CL-019 전투 감각 체크리스트의 시각화 의존 보류 항목 해소 + Epic E(적)/Epic G(보스)
작업에서 기준 삼을 시각 베이스라인 확보. HLD(Hyper Light Drifter) 스타일의
탑다운 2D 액션 톤을 목표로 한 placeholder 시각화.

## 주요 설계 전환 (초안 대비)

초안 단계 계획은 **TDE Koala 검 스프라이트 차용**이었으나 실험 과정에서 여러 전환:

| 전환 | 초안 | 실제 |
|---|---|---|
| 검 에셋 | TDE Koala(Idle/Slash1/2/3) 4장 | **코드 절차적 생성 (procedural)** — Koala는 사이드뷰 톤이라 탑다운 360° 회전에 부적합 판정 |
| 슬래시 에셋 | TDE `MMParticlesSlash.png` 1장 | **Frostwindz "Pixel Art Animations - Slashes"** (itch.io, 무료 CC-BY) 외부 도입. 128x128 × 3종 × 5색 × 9프레임 = 135 PNG |
| 슬래시 애니메이션 | 정적 sprite + 페이드 | **9프레임 프레임 시퀀스 재생** (신규 컴포넌트) |
| 정렬 해결 | 좌표 맞추기 | **HLD 스타일 — active 구간 검 숨김, 슬래시가 시각 담당** (원천 회피) |

## 확정된 설계 결정

| 결정 | 값 | 근거 |
|---|---|---|
| 검 구현 방식 | 코드 생성 (`CreateProceduralSwordSprite`) | 탑다운 회전 친화 + 외부 라이선스 부담 0 + 즉시 튜닝 가능 |
| 검 배치 | 캐릭터 자식 `Weapon` 오브젝트 + orbit | aim 방향으로 orbit하며 회전 |
| 검 표시 전략 | HLD-style: active 동안 `weaponSprite.enabled = false` | 검·슬래시 정렬 문제 원천 회피. windup/recovery엔 표시 |
| 슬래시 에셋 | Frostwindz 128x128 (64x64는 깨짐 판정) | 해상도·스타일 매칭, 3종(Slash 1/2/3) × 5색 변형 보유 |
| 슬래시 재생 | 신규 `KhiSlashAnimator` — 프레임 시퀀스 + 위치/회전/플립 per-combo | 콤보별 완전 독립 튜닝 필요 |
| 콤보별 차별화 | 1타/2타/3타 각자 frames·tint·scale·rotation·flip·offset 독립 | HLD처럼 공격마다 다른 모션 표현 |
| 방향 적응 | Auto Mirror 토글 (왼쪽/아래 자동 flip + offset 부호 반전) | 오른쪽 calibration 재사용, 수동 4방향 튜닝 부담 제거 |
| 슬래시 parenting | `SetParent(comboController.transform, worldPositionStays=true)` | 캐릭터 이동(lunge 등) 시 슬래시도 같이 따라감 |
| 3타 Finisher 연출 | lunge + 강화 카메라 쉐이크 | HLD 3타 마무리 톤 |
| Lunge 구현 | `KhiDashController.PerformLunge` — `DashStart()` 호출 안 함 | Cooldown/Feedback/무적 등 dash side effect 없이 이동 로직만 재사용 |
| Finisher feedback 분기 | `HandleTargetHit`에 ComboStep==3 판정 | 일반 적중과 톤 차별 |
| 정식 VFX/사운드 | 범위 외 | 후속 CL |

## 수정/신규 파일

### 신규 코드 (3개)
```text
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiWeaponPresenter.cs
    - 절차적 검 sprite 생성 (CreateProceduralSwordSprite)
    - aim 방향 orbit + 회전 (flipY on left aim)
    - AttackActiveStarted 구독 → swing 코루틴 (콤보별 arc/direction/duration 차등, smoothstep easing)
    - HLD-style: hideWeaponDuringActive 토글 — active 동안 SpriteRenderer 비활성화, active 종료 시 복원 + swing offset reset

LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiSlashAnimator.cs
    - AttackActiveStarted 구독 → 콤보 step별 프레임 시퀀스 재생
    - 콤보 1/2/3 각각 9필드: Sprite[] frames / Color tint / float scale /
      rotation offset / flipX / flipY / vertical·forward·lateral offset
    - Global position/rotation offset (모든 콤보 공통)
    - Auto Mirror: autoMirrorOnLeftAim(왼쪽 → flipX + lateral 반전),
                  autoMirrorOnDownAim(아래 → flipY + vertical 반전)
    - Parent Slash To Player 토글 + Parent Override Transform

LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiFinisherLunge.cs
    - AttackActiveStarted 구독 → targetComboSteps(기본 [3]) 일치 시
      KhiDashController.PerformLunge 호출
    - 필드: enabled / lungeDistance / lungeDuration / targetComboSteps
```

### 수정 코드 (3개)
```text
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiAttackVisualPresenter.cs
    - External Slash Sprite 슬롯 추가 (slot 비면 기존 procedural fallback)
    - Use Trail Renderer 토글 + trail color/time/width 필드
    - 현재 실제 사용은 Show Temporary Slash = false로 꺼둠 (KhiSlashAnimator로 대체)

LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiCombatFeedbackBinder.cs
    - Finisher Hit 섹션 추가: enableFinisherFeedback / finisherComboStep=3 /
      finisherHitStop=0.08 / finisherShakeIntensity=0.28 / finisherShakeDuration=0.18
    - HandleTargetHit에 step.ComboStep==3 분기 → Finisher feedback 적용

LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiDashController.cs
    - public void PerformLunge(direction, distance, duration) 신규
    - LungeCoroutine — _controller.MovePosition 사용 (물리 충돌 안전, Cooldown/Feedback 건드림 없음)
```

### 에셋 임포트
```text
Assets/_Project/Art/Effects/Slashes/Slash1/color{1..5}/Slash_colorN_frameM.png (9프레임 × 5색 = 45)
Assets/_Project/Art/Effects/Slashes/Slash2/color{1..5}/Slash2_colorN_frameM.png (7프레임 × 5색 = 35)
Assets/_Project/Art/Effects/Slashes/Slash3/color{1..5}/Slash3_colorN_frameM.png (9프레임 × 5색 = 45)
    (Frostwindz "Pixel Art Animations - Slashes" 128x128 버전)

Import settings 일괄 적용:
    Texture Type: Sprite (2D and UI)
    Pixel Per Unit: 128
    Filter Mode: Point (no filter)
    Compression: None
```

### 씬 wiring (test_khi.unity)
```text
Khi 플레이어 GameObject:
  ├─ 자식 "Weapon" (기존 유지)
  │    SpriteRenderer (procedural sprite 자동 할당)
  │    KhiWeaponPresenter
  │    └─ 자식 "WeaponTip" (빈 transform, TrailRenderer 부착 가능)
  │
  ├─ 추가 컴포넌트:
  │    KhiSlashAnimator — 콤보별 frames/offset 슬롯 9필드 × 3콤보
  │    KhiFinisherLunge — 3타 lunge 트리거
  │
  └─ 기존 유지:
       KhiCombatFeedbackBinder (Finisher 섹션 신규 적용됨)
       KhiAttackVisualPresenter (Show Temporary Slash = false로 비활성)
```

### 문서
```text
client/docs/khi/cl067_combat_visuals_and_effects.md (이 문서)
client/docs/khi/cl019_combat_feel_checklist.md (보류 항목 재평가 예정)
client/docs/khi/client1_tasks_master_plan.md (CL-019 아래 당김 메모 기존 유지)
```

## 컴포넌트 구조

### 1부 — KhiWeaponPresenter

**책임**: 캐릭터 자식 Weapon 오브젝트의 검 sprite 관리.

**절차적 sprite 생성** (`CreateProceduralSwordSprite`):
- `swordWidthPx × swordHeightPx` Texture2D 코드로 픽셀 그리기
- 손잡이 + 가드 + 블레이드 + 팁 테이퍼 + 하이라이트
- PPU 64, pivot (0.04, 0.5) — 손잡이 기준 회전
- 색/크기 Inspector 조정 가능

**Orbit + 회전** (`Update`):
```text
aim = playerAim.GetAimDirection()
angle = atan2(aim.y, aim.x) + swingOffsetAngle
orbitPos = (cos(angle)*effectiveRadius, verticalOffset + sin(angle)*effectiveRadius, 0)
weaponSprite.transform.localPosition = orbitPos
weaponSprite.transform.localRotation = Quaternion.Euler(0, 0, angle)
flipY = aim.x < 0 (optional)
```

**Swing animation** (콤보 active 시):
- 1타: 우→좌 내려치기 (arc 90°)
- 2타: 좌→우 올려치기 (arc 90°)
- 3타: 큰 회전 마무리 (arc 150°, duration 1.3배)
- smoothstep ease-in-out + sin 곡선 forward thrust

**HLD hiding**:
- `AttackActiveStarted` → `weaponSprite.enabled = false`
- `AttackActiveEnded` → `weaponSprite.enabled = true` + swing offset reset

### 2부 — KhiSlashAnimator

**책임**: 콤보 active 시 슬래시 프레임 시퀀스 재생.

**콤보별 독립 필드** (각 콤보에 9개):
```text
Sprite[] combo{N}Frames           — 9프레임 (colorM 폴더 드래그)
Color combo{N}Tint                — 색조 오버라이드
float combo{N}Scale               — 크기
float combo{N}RotationOffset      — 회전 오프셋 (도)
bool combo{N}FlipX / FlipY        — 좌우/상하 반전
float combo{N}VerticalOffset      — Y축 lift (world space)
float combo{N}ForwardOffset       — 공격 방향 거리
float combo{N}LateralOffset       — 공격 방향 수직
```

**Global (모든 콤보 공통)**:
- `frameInterval` (0.005~0.2s 슬라이더) — 재생 속도
- `followAimDirection` — aim 따라 회전
- `globalRotationOffset` / `globalVerticalOffset` / `globalForwardOffset` / `globalLateralOffset`

**Auto Mirror**:
- `autoMirrorOnLeftAim` (기본 ON): 왼쪽 aim 시 자동 `flipX = !flipX`, `lateralOffset = -lateralOffset`
- `autoMirrorOnDownAim` (기본 ON): 아래 aim 시 자동 `flipY = !flipY`, `verticalOffset = -verticalOffset`

**Parent Slash To Player**:
- `parentSlashToPlayer = true` → slashObj.transform.SetParent(parent, worldPositionStays=true)
- 캐릭터 lunge 시 슬래시가 같이 이동 → 자연스러운 정렬

**Spawn 흐름**:
```text
HandleAttackActiveStarted(request, step)
  → switch step.ComboStep → 해당 콤보 필드 추출
  → auto mirror 적용 (direction 보고 flip/offset 반전)
  → SpawnAndPlay:
    1. GameObject 생성
    2. position = request.Origin + hitbox.Offset + 글로벌/콤보 offset
    3. rotation = aim cardinal + 글로벌/콤보 rotation
    4. scale = comboNScale
    5. SpriteRenderer 설정 (tint, flipX, flipY, sortingOrder=1001)
    6. parent 설정 (옵션)
    7. StartCoroutine(PlayFrames) — frameInterval 간격으로 9프레임 재생 후 Destroy
```

### 3부 — KhiFinisherLunge + KhiDashController.PerformLunge

**KhiFinisherLunge 역할**:
- `AttackActiveStarted` 구독 → step.ComboStep이 `targetComboSteps`에 포함되면 lunge 트리거
- `playerAim.GetAimDirection()`으로 방향 결정
- `dashController.PerformLunge(direction, lungeDistance, lungeDuration)` 호출

**PerformLunge 내부**:
```text
public void PerformLunge(direction, distance, duration):
    StartCoroutine(LungeCoroutine(direction.normalized, distance, duration))

LungeCoroutine:
    origin = transform.position
    destination = origin + direction * distance
    while elapsed < duration:
        t = elapsed / duration
        eased = 1 - (1-t)² (ease-out)
        _controller.MovePosition(Lerp(origin, destination, eased))
        yield return null
```

**Side effect 회피 확인**:
- `Cooldown.Start()` 호출 안 함 → 대시 쿨다운 영향 없음
- `DashFeedback.PlayFeedbacks()` 호출 안 함 → 대시 VFX/SFX 안 터짐
- `_dashing` 플래그 건드림 없음 → 대시 중 공격 블록 등 상태 머신 영향 없음
- `_controller.MovePosition` 사용 → Rigidbody2D 기반이라 **벽 충돌 안전**

### 4부 — Finisher Feedback (KhiCombatFeedbackBinder)

**3타 적중 분기** (`HandleTargetHit`):
```text
if (enableFinisherFeedback && step.ComboStep == 3):
    RequestFreeze(finisherHitStop=0.08)          // 일반 0.04의 2배
    playerCamera.ApplyImpulse(0.28, 0.18)         // 일반 0.05/0.08 대비 ~5배/2배
    (optional) flashPresenter.Flash(...)
    return
else:
    RequestFreeze(hitStopOnLanding=0.04)
    playerCamera.ApplyImpulse(0.05, 0.08)
```

**연출 톤**:
- 1·2타: 짧고 가벼운 쉐이크 (리듬 유지)
- 3타: 강한 쉐이크 + 긴 히트스톱 (마무리 강조)

## 주요 설계 결정

### 1. 왜 TDE Koala sprite를 버리고 procedural로 갔는가

사용자가 직접 시험해본 결과 Koala sprite가 "위/아래 방향 공격 시 어색"하다 판단.
원인: Koala2D는 **사이드뷰** 플랫포머용 자료 → 수평 스윙에 최적화된 디자인.
탑다운 360° aim에서 회전시키면 arc 곡률이 불일치.

**대안**:
- A) 다른 외부 에셋 찾기 — 시간 소요
- B) 코드 생성 — 즉시 가능, 튜닝 자유, placeholder에 충분
- → **B 선택**. 정식 도트 아트는 후속 CL에서 교체.

### 2. 왜 슬래시는 외부 에셋(Frostwindz)을 도입했는가

검은 회전 친화적인 단순 모양이면 충분하지만, **슬래시는 "임팩트감"이 핵심**.
코드 생성 슬래시(기존 `KhiAttackVisualPresenter.CreateSlashSprite`)는 가능하지만
도트 아티스트의 본격 프레임 애니메이션 완성도를 따라잡기 어려움.

Frostwindz는 9프레임 시퀀스 × 3종 × 5색 풍부하게 제공 + 무료 → 실험에 이상적.

### 3. 왜 HLD 스타일(active 동안 검 숨김)로 갔는가

초기 시도에서 검과 슬래시 **위치/크기/회전 어긋남** 문제 발견. 원인 3가지:
- 검은 연속 aim(atan2), 슬래시는 카디널 4방향(hitbox.Offset 기반)
- 검 길이 ≠ 슬래시 visual 범위
- 검 swing 속도 ≠ 슬래시 재생 속도

4가지 접근 검토:
- (A) 슬래시가 곧 공격, 검 숨김 — HLD 방식
- (B) 검이 곧 공격, 슬래시 폐기 + TrailRenderer
- (C) 슬래시가 검 끝 추적 — per-frame 좌표 갱신
- (D) 크기/위치 수학 매칭
- (E) 얼라인먼트 포기 (Hades 스타일)

**A 선택**: 가장 깨끗한 원천 해결 + HLD 레퍼런스 톤 일치. 검은 idle/windup/recovery
동안만 보여 "캐릭터가 무기를 든다" 정체성 유지.

### 4. 왜 콤보별 offset을 완전 독립으로 두었는가

1타는 직선 베기, 2타는 사선, 3타는 회전 마무리 등 콤보마다 **시각 의도가 다름**.
Global 값으로만 통일하면 한 콤보 맞출 때 다른 콤보 어긋남. Inspector 슬라이더
수가 늘지만 튜닝 자유도가 필수.

### 5. 왜 Auto Mirror가 수동 방향별 슬라이더보다 나은가

4방향 × 9필드 = 36 슬라이더는 너무 많음. 토글 2개로 "오른쪽 기준 반전"만 자동화
하면 대부분 케이스 처리 가능. 대각선 방향(카디널로 quantize됨)은 같은 카디널
편으로 분류되므로 동일 규칙 적용.

한계: 카디널 4방향 기준이라 실제 대각선 aim의 미세 차이는 반영 못 함.
정식 아트 도입 시 방향별 독립 sprite 또는 sprite rotation 자동 보정이 필요.

### 6. 왜 PerformLunge를 DashStart 호출 없이 구현했는가

TDE `CharacterDash2D.DashStart()`는 monolithic — Cooldown.Start, Feedback.Play,
Invulnerability, animation trigger 등 모두 묶여 있음. 3타 lunge는 이 중 **이동
로직만** 필요. DashStart를 호출하면:
- 대시 쿨다운 시작 → 다음 대시 블록
- 대시 VFX/SFX 터짐 (공격 연출과 충돌)
- 무적 시간 부여 (의도와 무관)

대신 `_controller.MovePosition`만 직접 호출해 이동만 수행 → 깨끗함.
Rigidbody2D 기반이라 벽 충돌은 물리 엔진이 처리.

### 7. 왜 슬래시를 캐릭터 자식으로 parenting 했는가

3타 lunge 중 캐릭터가 이동하는데, 슬래시가 world-space 고정이면 캐릭터는 움직이고
슬래시만 제자리 → 어긋남. `SetParent(character, true)`로 캐릭터 이동 시 슬래시도
자동 추적 → 정렬 유지.

## 검증 (E2E)

### 자동 검증

- 컴파일: 수동 실행 (Unity 에디터 포커스 → 자동 재컴파일)
- 회귀 검증: cl017/cl018 피드백 유지 (HitStun, Parry, HitStop, Flash 모두 정상 동작)

### 수동 검증 (Unity Editor Play Test)

**전제** (이 티켓에서 완료):
- [x] Frostwindz 128x128 png 125장 import + 설정
- [x] Khi 플레이어에 KhiWeaponPresenter 자식 Weapon + KhiSlashAnimator + KhiFinisherLunge 부착
- [x] Inspector 콤보 1/2/3 각각 Frames 슬롯 드래그 연결
- [x] KhiAttackVisualPresenter `Show Temporary Slash = false`
- [x] KhiCombatFeedbackBinder Finisher 섹션 확인

**1부 검 검증**:
- [x] 아이들 상태에서 캐릭터 orbit 위치에 검 표시
- [x] 마우스 방향 따라 검 회전
- [x] 콤보 active 시 검 SpriteRenderer 비활성 (HLD 톤)
- [x] active 종료 시 검 즉시 복원 (snap)

**2부 슬래시 검증**:
- [x] 콤보 1·2·3타 각자 다른 슬래시 재생
- [x] 콤보별 tint/scale/rotation/flip 독립 적용
- [x] 위치 offset 슬라이더 Play 중 즉시 반영
- [x] 왼쪽 aim 시 Auto Mirror flipX 적용
- [x] 아래 aim 시 Auto Mirror flipY 적용
- [x] 슬래시가 캐릭터 자식으로 생성, 캐릭터 이동 시 따라감

**3부 Finisher 검증**:
- [x] 3타 active 시작 시 aim 방향으로 짧은 lunge
- [x] Lunge 중 벽 앞에서 물리 충돌로 멈춤 (뚫지 않음)
- [x] Dash 쿨다운에 영향 없음 (3타 후 즉시 대시 가능)
- [x] Dash VFX/Feedback 안 터짐
- [x] 3타 적중 시 강한 카메라 쉐이크 + 긴 히트스톱 (1·2타와 차별)

**회귀**:
- [ ] cl018 패링 링 펄스, 패링 플래시 정상
- [ ] cl017 히트스톱·플래시·카메라 임펄스 정상
- [ ] KhiDashController의 기존 대시 동작 정상 (PerformLunge 추가가 기존 경로 건드림 없음)

### cl019 체크리스트 갱신
완료 후 [cl019_combat_feel_checklist.md](cl019_combat_feel_checklist.md) 의 시각화
의존 보류 항목들 재평가 필요:
- #B5 anticipation/recovery 박자 — windup/recovery에서 검 보임 → 평가 가능
- #B6 스윙 호 히트프레임 위치 — HLD 톤으로 슬래시가 hitbox 영역에 표시 → 평가 가능
- #C6 패링 successSparkPrefab — **여전히 보류** (이번 CL에서 미구현)

## 미해결 / 알려진 이슈

### 1. 패링 successSparkPrefab 미생성
cl018에서 남긴 보류 항목. 이번 CL에서 슬래시 쪽에 시간 투자하느라 미진행.
별도 경량 프리팹 생성 작업 필요.

### 2. Auto Mirror 대각선 미지원
`KhiAttackDirection`이 카디널 4방향 enum(Up/Down/Left/Right). 실제 마우스가 45°
방향이면 카디널로 quantize됨 → 대각선 방향 슬래시는 가장 가까운 카디널 편으로만
표현. 정식 아트 도입 시 sprite 회전 자동 보정 또는 8방향 sprite 풀세트 필요.

### 3. 검 swing 코루틴이 active 동안 invisible하게 계속 돌아감
HLD hide가 ON일 때 swing 코루틴은 실행되지만 SpriteRenderer가 꺼져 있어서 안 보임.
active 종료 시 swing offset reset으로 snap 복원. 동작엔 문제 없지만 약간의 불필요 계산.
후속에 `if (hideWeaponDuringActive) skipSwingCoroutine` 최적화 가능.

### 4. Finisher shake 값이 Inspector 기본값
튜닝 거치지 않음. 플레이 감각 보고 intensity 0.2~0.4 사이, duration 0.12~0.25 사이
범위에서 조정 필요.

### 5. 정식 도트 아트 도입 시 전체 파이프라인 재검토
현재는 placeholder 단계:
- 검: procedural (코드 생성)
- 슬래시: 외부 무료 에셋 (스타일 구애 없음)
- 캐릭터: Khi 노란 사각형 placeholder

정식 도트 도입 시:
- 검 sprite로 교체 (KhiWeaponPresenter의 externalSprite 슬롯에 드래그)
- 슬래시 sprite 교체 (KhiSlashAnimator의 comboNFrames에 드래그)
- 현재 코드 구조는 유지 가능 (Inspector 교체만으로 충분)

## 후속 CL 연결

- **즉시 후속**: cl019 보류 항목 #B5/#B6 재평가 → 튜닝 세션 수행
- **CL-090 무기 데이터 SO**: 검 외 무기 추가 시 KhiWeaponPresenter의 procedural/external
  분기를 `WeaponData` ScriptableObject 참조로 전환
- **CL-101 사운드 1차 패스**: swing SFX, slash SFX, finisher SFX, lunge SFX 일괄 연결
- **CL-067 본격 VFX 스코프 (정식 도트 들어올 때)**: 외부 에셋 교체, 코드 무수정
- **KhiPlayerAnimator 도입 시**: 캐릭터 본체 애니(idle/walk/attack pose)와 weapon presenter
  는 독립 동작. 둘 다 AttackStarted 이벤트 구독으로 동기화 가능
- **패링 Spark 완성**: cl018 보류 해소 — 별도 작업 or 이번 CL에 추가
- **Auto Mirror 개선**: 대각선 처리 필요 시 sprite 회전 보정 방식 도입

## 2026-04-24 후속 작업 — 360° aim + SlashRig 리팩터

초안 구현 후 플레이테스트에서 발견된 문제들을 단계적으로 해결. 아래는 오늘 반영된 구조 변경 내역.

### 1. Origin drift 수정 — 이동 중 공격 시 슬래시 위치 어긋남

**문제**: 플레이어가 이동하면서 공격 시 슬래시가 캐릭터보다 뒤처져서 스폰. 가만히 있으면 정상.

**원인**: `KhiAttackRequest.Origin`이 `RunAttack` 코루틴 시작 시점(공격 입력 순간)에만 찍힘. windup(0.1~0.2s) 동안 플레이어가 이동해도 `AttackActiveStarted` 이벤트에는 낡은 Origin 그대로 전달. 히트박스 샘플링은 매 프레임 `transform.position` 갱신해서 문제없지만, 슬래시 스폰 이벤트는 갱신 누락.

**수정**: [KhiMeleeComboController.cs:178](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeComboController.cs:178)에서 `AttackActiveStarted` 발행 직전 `request.Origin = transform.position;` 한 줄 추가.

```csharp
// active 시작 시점의 위치를 request.Origin에 반영 (windup 중 플레이어 이동 보정).
request.Origin = transform.position;
AttackActiveStarted?.Invoke(request, step);
```

검증 로그(`[KhiSlashDebug]`)로 이동 시 drift 수치(0.6~1.3 units) 관측 후 제거 확인. 로그는 수정 완료 후 정리.

### 2. 4방향 cardinal → 360° 연속 aim 전환

**동기**: 마우스 방향으로 자유 공격 가능하게. 기존 `KhiAttackDirection` enum(Right/Up/Left/Down 4값) 체계를 벡터/각도 기반으로 전면 교체.

**변경 파일**:
- [KhiMeleeTypes.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeTypes.cs) — enum 제거, `KhiAttackRequest`에 `AimDirection`(Vector2) + `AimAngleDegrees`(float). `KhiMeleeAttackStep`의 4개 hitbox 필드(Right/Up/Left/Down)를 단일 `Baseline`으로 축소. `[FormerlySerializedAs("Right")]` 로 기존 prefab 데이터 자동 마이그레이션.
- [KhiPlayerAim.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiPlayerAim.cs) — cardinal 관련 API(`GetCardinalDirection`, `ToCardinalDirection`, `ToVector`) 제거, `GetAimDirection()`만 유지.
- [KhiMeleeComboController.cs:142-156](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeComboController.cs:142) — 연속 aim 벡터 + atan2 기반 각도 저장. 기본 스텝 팩토리에서 4방향 hitbox → baseline 단일화.
- [KhiMeleeHitbox.cs:32-72](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiMeleeHitbox.cs:32) — `step.Baseline.Offset`을 aim 각도로 회전시켜 center 계산, `Physics2D.OverlapBoxNonAlloc`에 각도 전달, 런타임 preview/Gizmo 박스도 회전.
- [KhiSlashAnimator.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiSlashAnimator.cs) — cardinal switch 제거, 연속 aim 기반 회전.
- [KhiAttackVisualPresenter.cs:113-117](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiAttackVisualPresenter.cs:113) — Baseline + aim 각도 사용으로 API 정리.

**전후 호환**: `KhiFinisherLunge`(`playerAim.GetAimDirection()` 사용), `KhiWeaponPresenter`(atan2 기반 orbit)는 이미 연속 aim이라 수정 불필요.

### 3. Hemisphere mirror — Left 방향 arc curl 보존

**문제**: 순수 회전만 쓰면 aim 180°(Left)에서 sprite arc curl이 뒤집혀 보임. Frostwindz 슬래시처럼 비대칭 curl 가진 sprite는 `R(180°) = FlipX + FlipY` 등식 때문에 수직 반전 효과 발생.

**수학**: `R(θ)·FX = FX·R(-θ)` 등식으로 flipX를 적용하면 회전 부호가 반전됨. 따라서 Left-half aim에서:
- `flipX = !flipX`
- `rotation_sprite = 180° - aim_angle` (Y축 대칭)
- `comboRotationOffset` 부호 반전 (combo별 tilt도 거울상)

**구현**: [KhiSlashAnimator.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiSlashAnimator.cs) `autoMirrorOnLeftAim` 토글(기본 ON). `aim.x < 0`일 때 flipX + 각도 보정 적용.

**주의**: 이 방식은 `aim.x = 0` 경계에서 slash 위치가 snap(약 1 유닛 순간이동)됨. 대신 4-cardinal 감각 일치. 트레이드오프 관계.

### 4. Flip 적용 위치 변경 — SpriteRenderer → transform.scale

**변경**: `SpriteRenderer.flipX/flipY` 대신 `transform.localScale`에 -1 곱하기 방식으로 flip 적용.

```csharp
float scaleX = flipX ? -scale : scale;
float scaleY = flipY ? -scale : scale;
slashObj.transform.localScale = new Vector3(scaleX, scaleY, 1f);
```

**이유**: 상위 rig가 scale로 flip하면 자식들까지 자동 전파됨. 이후 SlashRig 구조에서 rig 하나로 모든 슬롯의 flip을 일괄 처리 가능.

### 5. SlashRig 컨테이너 패턴 — 슬롯 pre-placed, 재활용

**문제**: 매 공격마다 `new GameObject + AddComponent` 로 슬래시 생성 → GC 할당, 에디터에서 안 보여서 튜닝 어려움, 중심값 일관성 낮음.

**해결**: Khi 플레이어 자식으로 `SlashRig` 컨테이너 + 그 하위 `SlashSlot_1/2/3` 사전 배치. SlashRig의 transform(rotation/scale)만 조작해서 모든 슬롯 동시 변환. 공격 시 슬롯의 SpriteRenderer를 enable/disable + 프레임 시퀀스 재생.

```
Khi Player
├─ Weapon (기존)
└─ SlashRig
    ├─ SlashSlot_1 (1타용, localPos (1.05, 0.55), rot 0°, scale 1.5)
    ├─ SlashSlot_2 (2타용, localPos (1.05, 0.2), rot 0°, scale 1.5)
    └─ SlashSlot_3 (3타용, localPos (0.75, 0.3), rot -60°, scale 1.5)
```

**자동 생성 fallback**: Awake에서 `slashRig` 또는 `slashSlots[]`가 null이면 자동으로 GameObject 생성 + baseline 위치 baked. Prefab에서 미리 셋업된 경우 해당 참조 사용.

**에디터 편집 지원**: `[ContextMenu]` 2종 추가 ([KhiSlashAnimator.cs:76-106](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiSlashAnimator.cs:76)):
- **"Setup Slash Rig (Edit Mode)"** — Play 이전에 Inspector 우클릭으로 rig + 슬롯 생성해서 씬에 영구 저장. Scene 뷰에서 슬롯 위치 시각적 편집 가능.
- **"Reset Slot Positions To Baseline"** — 슬롯 localPosition/rotation/scale을 Right calibration 값으로 복원.

**제거된 Inspector 필드**(slot transform에 baked): combo1/2/3 Scale, RotationOffset, FlipX/Y, VerticalOffset, ForwardOffset, LateralOffset, globalRotationOffset, globalVerticalOffset, globalForwardOffset, globalLateralOffset, followAimDirection, parentSlashToPlayer, parentOverride.

**유지된 Inspector 필드**: comboController, slashRig, slashSlots[], combo1/2/3 Frames/Tint, frameInterval, slashSortingOrder/Layer, autoMirrorOnLeftAim.

### 6. 8방향 rigOffset blend — 방향별 중심 보정(임시 비활성화)

**도입 배경**: SlashRig 회전만으로는 방향별 "자연스러운 중심점"이 안 맞음. 예: Right에서 튜닝된 slot local (1.05, 0.55)가 Up aim에선 회전되어 (-0.55, 1.05)가 되면서 왼쪽으로 치우친 느낌.

**시도한 해결**: Inspector에 8방향 anchor 필드(R, UR, U, UL, L, DL, D, DR) + aim 각도를 45° 섹터로 나눠 인접 anchor 사이 linear interp. `Auto-fill Diagonals From Cardinals` ContextMenu로 4-cardinal → 4-diagonal 자동 채움.

**현재 상태**: [KhiSlashAnimator.cs:284-288](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiSlashAnimator.cs:284) **임시 주석 처리**. BlendRigOffset 함수와 8개 Inspector 필드는 유지하되 호출만 끔. 대신 **고정 Y offset(0.6)** 로 회전 중심을 플레이어 상체 위치에 고정:

```csharp
// Vector2 blendedOffset = BlendRigOffset(aimAngleDeg);
// slashRig.localPosition = blendedOffset;
slashRig.localPosition = new Vector3(0f, 0.6f, 0f);
```

**이유**: aim이 Y축(aim.x=0) 가로지를 때 hemisphere mirror 때문에 slash 위치가 snap(약 1 유닛 순간이동)하는 현상 관찰. 회전 중심을 고정된 상체 높이로 올리면 전체적으로 "플레이어 몸통 기준 원형 스윕" 느낌 확보. 8방향 blend는 snap 문제 해결에 직접 기여하지 않아 일단 끔.

### 7. 현재 검증 상태 (2026-04-24)

- [x] Right aim 4방향 cardinal 위치 기존과 동일
- [x] 이동 중 공격 시 drift 제거
- [x] 360° 연속 aim 판정 (hitbox + 슬래시 sprite 모두 회전)
- [x] 회전 중심 Y=0.6 고정 → "원형 스윕" 감각 회복
- [x] SlashRig ContextMenu로 에디터 사전 생성 + 시각적 튜닝 가능
- [ ] Left hemisphere arc curl 보존 (`autoMirrorOnLeftAim = ON`) — 경계 snap 트레이드오프 수용 중
- [ ] 8방향 rigOffset blend 재활용 여부 결정 (현재 주석)
- [ ] 대각선 aim 시 슬래시 방향 자연스러움 최종 체감 검증

### 8. 남은 고민 / 후속 (CL-067 내 마무리 가능)

- **Left hemisphere snap**: 비대칭 sprite 특성상 pure rotation + hemisphere mirror는 트레이드오프 관계. 최종 해결은 sprite 자체 대칭화 or 8방향 sprite sheet 도입 시 가능. 현재 `slashRigCenterY=0.6` 로 시각적 임팩트 완화.
- **대각선 aim 미세 튜닝**: 45°/135°/-135°/-45° 각 구간에서 hitbox·슬래시 체감 일치도 — 플레이테스트 + 슬롯 transform 미세조정으로 해결.
- **1/2/3타 콤보 간 세로 분산**: 현재 slot Y 차이(0.55/0.2/0.3)가 콤보 리듬으로 읽히는지, 통일감 없는 흐름으로 보이는지 판단.

### 9. 후속 ticket으로 위임 (CL-067 범위 외)

이번 ticket 진행 중 발견된 다음 항목들은 본 ticket에서 다루지 않고 후속 ticket으로 분리:

#### CL-090 — WeaponData SO + 라이브 튠 (MVP critical)

- **데미지 영역(hitbox) ≡ 시각 영역(slash) 일치/근접화**
  - 현재: hitbox `step.Baseline.Offset` 와 slash slot localPosition이 별도 baked → 1타·3타에서 위치 불일치
  - 해결 방향: WeaponData SO 안에서 hitbox + visual offset 을 같은 데이터에서 derive. 시각은 hitbox 기준 약간 큰 박스로 ("시각 ≥ 판정" 원칙)
  - 부수효과: SlashAnimator의 8방향 rigOffset blend 재도입 여부도 SO 도입 시점에 데이터 기반으로 결정.

- **밸런스 수치 통합 + 라이브 튠**
  - 현재 분산된 수치들을 무기 단위 ScriptableObject 로 통합
  - Play 중 SO 변경 즉시 반영 + 자동 저장 → MVP 폴리시 iteration 속도 확보

- **8방향 rigOffset blend 재도입 여부**
  - 코드 정리 단계에서 제거됨 (현재 미사용). git history 에 보관.
  - WeaponData SO 도입 후 per-direction 데이터 필요해지면 SO 필드로 부활 검토.

상세 계획: [cl090_weapon_data_so_and_designer_tool.md](cl090_weapon_data_so_and_designer_tool.md) 참조.

#### CL-103 — 디자이너 친화 툴 폴리시 (MVP 후, 포트폴리오)

- Custom Inspector (그룹핑/Tooltip/Validation)
- Scene Gizmo (hitbox/visual 박스 시각화)
- (선택) Scene 핸들 드래그 직접 편집

#### CL-104 — 데이터 카테고리 확장 (MVP 후)

- EnemyData SO (Orc Rider 등 CL-039/040 연계)
- CharacterData SO (Khi 스탯)
- 두 번째 무기/적 추가 시점에 진행

## 관련 문서

- 진행 중 평가표: [cl019_combat_feel_checklist.md](cl019_combat_feel_checklist.md)
- 직전 CL: [cl018_parry_success_feedback_implementation.md](cl018_parry_success_feedback_implementation.md)
- 피드백 시스템: [cl017_hit_feedback_implementation.md](cl017_hit_feedback_implementation.md)
- 마스터 플랜: [client1_tasks_master_plan.md](client1_tasks_master_plan.md) (CL-067 정의, Epic H)
- 통합 작업 plan (로컬): `C:\Users\SSAFY\.claude\plans\eventual-snuggling-cloud.md`
- TDE 안전 규칙: [topdown-engine-extension-and-original-protection.md](../commonness/topdown-engine-extension-and-original-protection.md)
- 외부 에셋 라이선스: Frostwindz "Pixel Art Animations - Slashes" — itch.io 페이지 참조
- 참고: [cl039_cl040_charge_enemy](cl039_cl040_charge_enemy_plan.md) ChargeHitboxAnchor 패턴이 "회전하는 child의 offset.y는 0 유지, 높이는 non-rotating anchor에서 담당" 원칙을 정리해둠. 본 CL의 SlashRig 중심 Y 고정 결정의 선례.
