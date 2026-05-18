# CL-017 히트스톱 · 피격 플래시 · 카메라 임펄스 구현 기록 (진행 중)

작성일: 2026-04-24

대상 씬: `LostMemory/Assets/Scenes/test_khi.unity`

**상태**: 🟡 **코드 완료 / 플레이 테스트 대기** — `dotnet build` 통과. Unity Editor에서
프리팹에 Binder/FlashPresenter 부착 후 수동 검증 필요.

## 목적

CL-011~016으로 컨트롤러/상태집계/카메라는 갖춰졌지만 "타격감(game feel)"은 아직 건조함.
피격/타격/패링 순간에 시각·시간적 보정이 없어 감각적 코어가 비어 있음.

CL-017은 기존 이벤트 세 개를 구독해 히트스톱 + 플래시 + 카메라 임펄스 세 가지 피드백을
추가한다. 기존 Khi 컨트롤러는 **0바이트 수정** — 새 컴포넌트만 이벤트 구독 방식으로
연결.

## 설계 기준

- 기존 Khi 컨트롤러(Parry/HitStun/Dash/Down/MeleeCombo) **0바이트 수정**
- 씬/프리팹 YAML 직접 편집 금지 → Bootstrap + Binder가 런타임 부착/구독
- 피드백 루프는 `Time.unscaledDeltaTime` 기준 → 히트스톱 중에도 Flash/Shake 진행
- 중첩 호출 안전: HitStop은 더 긴 요청만 승계, Impulse/Flash는 최신값으로 덮어씀
- 모든 이펙트 독립 동작 — 하나 비활성화해도 다른 둘 정상

## 확정된 설계 결정

| 결정 | 값 | 근거 |
|---|---|---|
| 이벤트 커버리지 | HitStunStarted + TargetHit + ParrySucceeded | 유저: "플레이어 피격 + 타격 성공". DownEntered는 CL-018 이연. |
| 히트스톱 구현 | `Time.timeScale` 직접 조작 (커스텀 싱글톤) | MMFeedbacks 의존 회피, duration 세밀 튜닝. |
| 플래시 구현 | MaterialPropertyBlock `_Color` tint | 원본 머티리얼 보존, SRP Batcher 친화적. |
| 카메라 임펄스 | `KhiPlayerCamera.ApplyImpulse` 메서드 | Cinemachine 의존 없음. Impulse는 follow 후 오프셋만 더함 → drift 없음. |
| 피드백 시간 기준 | unscaled time | Flash/Shake가 freeze 중에도 시각적으로 진행. |
| 더미 플래시 | TestKhiDamageDummy 기존 color-swap 유지 | 스코프 관리, 회귀 0. |
| Binder 패턴 | 플레이어 루트의 단일 오케스트레이터 | 모든 튜닝 파라미터 Inspector 단일 진실 소스. |

## 추가/수정 파일

신규 파일 (3개):

```text
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiHitStopController.cs
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiHitFlashPresenter.cs
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiCombatFeedbackBinder.cs
```

수정 파일 (2개):

```text
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiPlayerCamera.cs
    - ApplyImpulse(intensity, duration) 공개 API 추가
    - LateUpdate에 impulse decay 로직 (unscaledTime 기준)
    - follow base position vs. impulse offset 분리로 drift 방지

LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/TestKhiSceneBootstrap.cs
    - EnsureHitStopController() 신설 (Awake 호출)
```

csproj(gitignore)에 신규 파일 3개 수동 등록. Unity 재실행 시 자동 재생성.

문서:

```text
client/docs/khi/cl017_hit_feedback_implementation.md
```

## 컴포넌트 구조

### KhiHitStopController (씬 레벨 싱글톤)

- `[DefaultExecutionOrder(-200)]` — 모든 Binder 구독 시점에 Instance 보장
- `public static KhiHitStopController Instance`
- `RequestFreeze(duration, frozenScale=0)` — Time.timeScale pulse
- OnDestroy/OnDisable/OnApplicationQuit에서 timeScale 강제 복원

**중첩 규칙**:
```text
RequestFreeze(d):
    새 요청의 end time이 기존보다 늦으면 연장, 짧으면 무시
    freeze 진입 시점에만 현재 timeScale 캐시
Update():
    unscaledTime >= end 이면 복원
```

### KhiHitFlashPresenter

- `[DefaultExecutionOrder(310)]` — 디버그 오버레이(300) 이후
- `Flash(Color color, float duration)` 공개 API
- `MaterialPropertyBlock`으로 SpriteRenderer `_Color` 오버라이드
- unscaledTime 기준 linear ease-out
- Flash 종료 시 `MaterialPropertyBlock.Clear()` → 원본 머티리얼 컬러 복원

`targetRenderers` 비우면 Awake에서 `GetComponentsInChildren<SpriteRenderer>(true)` 자동 탐색.

### KhiCombatFeedbackBinder (플레이어 루트)

- `[DefaultExecutionOrder(250)]`
- Awake에서 hitStun/parry/meleeCombo/flashPresenter/playerCamera 참조 `??=` fallback
- OnEnable/OnDisable에서 3개 이벤트 구독/해제
  - `hitStun.HitStunStarted`
  - `parry.ParrySucceeded`
  - `meleeCombo.TargetHit(KhiAttackRequest, KhiMeleeAttackStep, Health)`
- 각 이벤트 핸들러가 `RequestFreeze + Flash + ApplyImpulse` 3개 호출

**Inspector 파라미터 기본값**:

| 이벤트 | Freeze | Flash Color | Flash Dur | Shake Int | Shake Dur |
|---|---|---|---|---|---|
| HitStunStarted | 0.06 | white | 0.12 | 0.15 | 0.12 |
| ParrySucceeded | 0.08 | cyan(0.5,0.85,1) | 0.15 | 0.10 | 0.10 |
| TargetHit | 0.04 | warm(1,0.95,0.7) — `flashOnLanding=false` 기본 | 0.08 | 0.05 | 0.08 |

### KhiPlayerCamera (수정)

**추가**: `ApplyImpulse(intensity, duration)` + 4개 private 필드.

**LateUpdate 변경**:
```text
1. basePos = transform.position - _currentImpulseOffset   // 이전 오프셋 제거
2. basePos에 대해 follow (SmoothDamp) 계산
3. _currentImpulseOffset = UpdateImpulseOffset()           // 새 랜덤 오프셋
4. transform.position = basePos + _currentImpulseOffset
```

다음 프레임 LateUpdate는 다시 step 1에서 정확한 basePos 복원 → drift 없음.

impulse magnitude 계산: 선형 ease-out (`remaining = (end - now) / duration`).
Random 오프셋: `(Random.value - 0.5) * 2 * mag` → `[-mag, +mag]` uniform.

### TestKhiSceneBootstrap (수정)

Awake(Application.isPlaying 경로)에서 `EnsureHitStopController()` 호출.
없으면 새 GameObject 생성 후 컴포넌트 부착. Instance 자동 할당.

## 주요 설계 결정

### 1. 왜 커스텀 HitStop (MMFeedbacks 대신)

~30줄 timeScale pulse로 충분. MMF_FreezeFrame도 내부는 동일한 timeScale 조작이며 씬에
MMTimeManager 컴포넌트 추가 의존이 생긴다. MMFeedbacks 프로젝트 전역 도입 시점에
`RequestFreeze` → `MMFreezeFrameEvent.Trigger(...)` 한 줄 교체로 마이그레이션 가능.

### 2. 왜 unscaled time 기준 피드백

히트스톱 중 scaled time은 0이지만 Flash/Shake는 **시각적으로 진행**되어야 타격감이 산다.
업계 관행: Flash가 freeze 동안 선명하게 유지, freeze 풀리면 이미 페이드 시작된 상태에서
shake 계속. scaled time 기준이면 freeze 동안 모든 피드백이 같이 멈춰 "튀었다 멈춰버림"
느낌.

### 3. 왜 MaterialPropertyBlock

`SpriteRenderer.color` 직접 교체(TestKhiDamageDummy 패턴)는 원본 틴트 손실 위험. 예:
플레이어 스프라이트가 (0.9, 0.9, 1.0) 파스텔 틴트일 때 Flash 종료 시 Color.white로
덮어쓰면 원본 틴트 증발. MPB는 셰이더 단에서 오버라이드 → `Clear()` 호출 시 원본 값 복원.
SRP Batcher와도 호환(동일 머티리얼 다른 MPB는 batch 유지).

### 4. 왜 Binder 패턴

- 모든 튜닝 파라미터가 Inspector 한 곳 → CL-019 체크리스트 튜닝 시 단일 진실 소스
- Hit stop/Flash/Camera 컴포넌트가 Khi 컨트롤러 존재를 몰라도 됨 → 재사용 ↑
- 후속 이벤트 추가(DownEntered 등) 시 Binder에만 블록 추가

### 5. 왜 TestKhiDamageDummy 미수정

기존 color-swap flash는 이미 작동 중이며 테스트 경로의 일부. 새 플래시 시스템은
플레이어 전용으로 시작해 위험 최소화. 후속 CL에서 일반 적 프리팹 만들 때
`KhiHitFlashPresenter`를 재사용.

### 6. 카메라 drift 방지 — base/offset 분리

단순히 `transform.position += impulseOffset` 하면 다음 프레임 SmoothDamp가 offset된 위치
에서 시작 → follow 중심선이 서서히 밀림. 해결: 매 프레임 LateUpdate 초입에 이전 오프셋
제거하여 순수 base position 복원, 그 위에 follow 계산, 마지막에 새 오프셋 추가.

### 7. HitStop duration 계층화

Parry(0.08s) > HitStun(0.06s) > TargetHit(0.04s). Parry 성공은 가장 강조 포인트이므로
freeze가 가장 길다. TargetHit은 빈번히 발생(콤보 3타)하므로 짧게 유지해 리듬 방해 최소화.

## 검증

### 자동 검증

```text
dotnet build LostMemory/Assembly-CSharp.csproj --no-restore
→ 기존 2개 deprecation 경고만, 신규 에러/경고 0
```

csproj에 3개 신규 파일 등록 완료.

### 수동 검증 (Unity Editor Play Test) — 대기 중

**전제 작업** (최초 1회):

- [ ] `TestKhi_MinimalCharacter2D` 프리팹 인스턴스(씬)에 `KhiHitFlashPresenter` 부착
      (Inspector에서 `targetRenderers` 필드 비워두면 자동 탐색 — 자식 `MinimalCharacterModel`
      SpriteRenderer 포함)
- [ ] 동일 플레이어에 `KhiCombatFeedbackBinder` 부착
      (Awake에서 hitStun/parry/meleeCombo/flashPresenter/playerCamera 자동 resolve)
- [ ] Play 시 "KhiHitStopController" GameObject가 씬에 자동 생성되는지 확인 (Bootstrap 부착)

**히트스톱 체크**:

- [ ] #1 트랩 피격 → 화면 0.06s 정지 후 복귀
- [ ] #2 더미 공격 적중(1타) → 화면 0.04s 정지
- [ ] #3 우클릭 타이밍 패링 성공 → 화면 0.08s 정지 (가장 오래)
- [ ] #4 트랩 다단히트 중첩 호출 → timeScale 정상 복원, 0.99 미만 방치 없음
- [ ] #5 Play 종료 → `Time.timeScale = 1` 자동 복원 (OnDestroy)

**플래시 체크**:

- [ ] #6 피격 시 플레이어 스프라이트 플래시 0.12s → 원본 틴트 복원
      (주의: Binder `Hit Flash Color`가 white 기본값이면 흰색 스프라이트에서 시각 효과 없음.
      비-흰색으로 바꿔 체감 확인. "알려진 이슈" 섹션 참조)
- [ ] #7 패링 성공 시 시안색 페이드 0.15s
- [ ] #8 공격 적중 시 기본으로 플래시 없음 (flashOnLanding=false). 토글 시 따뜻한 색 표시
- [ ] #9 TestKhiDamageDummy 피격 시 기존 hitColor flash 정상 (회귀 없음)

**카메라 임펄스 체크**:

- [ ] #10 피격/적중/패링 각각 카메라 흔들림, 강도 계층 체감 (Parry > HitStun > TargetHit)
- [ ] #11 셰이크 후 카메라가 플레이어 중심선으로 복귀 (drift 없음)
- [ ] #12 Deadzone 동작 유지 (셰이크와 독립)
- [ ] #13 F3 오버레이가 셰이크와 무관하게 고정 위치 (OnGUI는 카메라 영향 없음)

**안정성**:

- [ ] #14 Defeat(체력 0) → 이벤트 발생 안 함, 이상 없음
- [ ] #15 Debug Respawn(P 키) → 새 플레이어에 Binder 이벤트 재구독 (Awake 재실행)

### 파라미터 튜닝

Binder Inspector에서 각 파라미터 변경 → Play → 체감 확인 반복. 기본값은 시작점.
CL-019 체크리스트 시 본격 튜닝.

## 미해결 / 알려진 이슈

### Hit Flash Color 기본값이 white → 흰 스프라이트에서 시각 효과 없음

`KhiHitFlashPresenter.Update()`의 tint 계산은 `Color.Lerp(Color.white, _currentColor, intensity)`.
플레이어 스프라이트의 SpriteRenderer.color가 white(기본)이고 Binder의 `Hit Flash Color`도
white(기본)이면 모든 intensity에서 결과가 white로 동일 → MPB `_Color = white` 오버라이드가
원본과 구분 불가.

**실사용 필수 조치**: Binder Inspector에서 `Hit Flash Color`를 **비-흰색**(예: `(1.0, 0.3, 0.3)`
빨강, `(1.0, 0.6, 0.4)` 핑크)으로 변경. Parry는 기본값이 cyan `(0.5, 0.85, 1)`이라 바로 보임.

진정한 "하얀 번쩍"은 Sprites/Default 셰이더 한계로 MPB만으로는 불가능하며 커스텀 셰이더
(`_FlashColor`, `_FlashAmount`)이나 HDR overbright가 필요. CL-018 패링 전용 피드백 또는
CL-067 본격 VFX 스코프에서 셰이더와 함께 도입 고려.

### Time.timeScale이 FixedUpdate도 정지

피격 중 물리 시뮬레이션 정지. 0.04~0.08s는 너무 짧아 영향 무시 가능. 장시간 freeze 필요
시 `Physics2D.simulationMode` 별도 관리 (현재 MVP 범위 밖).

### 다른 시스템의 timeScale 조작

향후 일시정지 메뉴가 `timeScale=0`을 설정한 상태에서 HitStop이 enter/exit하면 `pause`
해제 버그 가능. 현재 `_restoreTimeScale`이 "freeze 직전 값" 캐시하므로 대부분 안전.
pause와 hit stop 둘 다 통합 시점에 우선순위/스택 설계 필요.

### MPB 충돌 가능성

TDE `CharacterDamageFlash` 같은 내장 flash가 같은 SpriteRenderer에 MPB를 쓰면 최종
`SetPropertyBlock` 호출이 승리. 현재 TDE 내장 flash는 미사용. 후속 CL에서 TDE 내장
교체 시 주의.

### Debug Respawn(P 키) 후 이벤트 재구독

`KhiCombatFeedbackBinder`의 OnEnable/OnDisable이 이벤트 구독을 관리하므로 플레이어
GameObject 비활성화 → 재활성화 사이클에서 구독이 자연 복구된다. 단 Binder가 플레이어
루트가 아닌 곳에 붙어 있을 경우 `hitStun`, `parry`, `meleeCombo` 참조가 무효화될 수 있음
→ 현재 설계는 플레이어 루트 전제로 안전.

## 후속 CL 연결

- **CL-018 패링 성공 전용 피드백**: Binder parry 파라미터를 기본 유지, CL-018에서 신규
  `KhiParryBurstPresenter`(링/빛번짐/파티클) 추가. Binder Inspector 한 블록 추가로 연결.
- **CL-019 전투 감각 체크리스트**: Binder Inspector 값이 튜닝 대상의 단일 진실 소스.
  체크리스트 기준값 통과 시 그 값으로 고정.
- **CL-067 본격 VFX/사운드**: Binder 핸들러에 `HitFX.Play(kind, position)` 호출 추가.
  기존 구조 유지.
- **적 AI 등장 시**: 신규 적 프리팹에 `KhiHitFlashPresenter` 부착 (플레이어와 동일 코드).
  또는 TestKhiDamageDummy 자체 flash 제거 후 presenter로 통일.
- **Cinemachine 전환 시**: `KhiPlayerCamera.ApplyImpulse` → `CinemachineImpulseSource`로
  교체. Binder 호출부 한 줄 수정.
- **DownEntered 피드백**: CL-018 또는 별도 티켓에서 Binder에 구독 추가 (카메라 줌 아웃 +
  장시간 플래시 등).

## 관련 문서

- 구현 계획 (로컬): `C:\Users\AD\.claude\plans\sparkling-snuggling-meteor.md`
- 마스터 플랜: [client1_tasks_master_plan.md:37](client1_tasks_master_plan.md) (CL-017 정의)
- CL-013 HitStun 이벤트 원천: [cl013_player_hit_stun_implementation.md](cl013_player_hit_stun_implementation.md)
- CL-016 카메라: [cl016_camera_follow_implementation.md](cl016_camera_follow_implementation.md)
- 아트 방향 (약한 흔들림 기조): [docs/09_art_direction.md:35](../../../docs/09_art_direction.md)
- 씬/프리팹 안전 규칙: [agent-unity-safety-rules.md](../commonness/agent-unity-safety-rules.md)
