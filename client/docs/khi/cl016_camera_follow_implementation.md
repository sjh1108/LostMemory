# CL-016 카메라 추적 및 기본 시야 조정 구현 기록 (진행 중)

작성일: 2026-04-23

대상 씬: `LostMemory/Assets/Scenes/test_khi.unity`

**상태**: 🟡 **코드 완료 / 플레이 테스트 대기** — `dotnet build` 통과. Unity Editor에서
Play 진입 후 수동 검증 필요.

## 목적

CL-011~015로 전투/상태 집계는 완성됐지만, 카메라는 여전히
[TestKhiInputManager.FollowMainCamera](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/TestKhiInputManager.cs)
가 Vector3.SmoothDamp로 **플레이어를 항상 화면 중앙에 고정**하고 있다. 작은 입력에도
카메라가 미세하게 진동해 탑다운 전투 가독성이 떨어지고, InputManager가 카메라 책임까지
겸해 관심사가 섞여 있다.

CL-016은 카메라 책임을 별도 컴포넌트로 분리하고 **Deadzone + Damping** 기반 추적으로
교체한다. Lookahead/Cinemachine 마이그레이션은 slot만 예약하고 실제 도입은 후속 CL.

## 설계 기준

- 기존 InputManager 원본 필드는 유지 (씬 직렬화 churn 0). "양보" 가드만 추가.
- 씬/프리팹 YAML 직접 편집 금지. `TestKhiSceneBootstrap`이 Play 시 컴포넌트 부착.
- Lookahead는 외부 주입 구조. 본 CL에서는 기본값 0.
- 행동 변경 0: 대시/피격/패링/다운 상태에서도 카메라 로직 동일.

## 확정된 설계 결정

| 결정 | 값 | 근거 |
|---|---|---|
| 구현 방식 | 커스텀 `KhiPlayerCamera` MonoBehaviour | 유저 요청: "나중에 바꾸어야 하면 커스텀". Cinemachine 의존 회피. |
| Follow feel | Deadzone + SmoothDamp | 전투 가독성 ↑, 잔진동 ↓ |
| Lookahead | slot 예약 (값 0) | 후속 CL(CL-017/018)이 주입만 하면 동작 |
| 부가 항목 | PixelPerfectCamera 부착 | PPU=16 픽셀 아트 기조 유지. 해상도 일관성 |
| Ref Resolution | 320×180 (16:9) | ortho size ≈ 5.625. 2K/4K 모니터 정수배 업스케일 |
| Execution Order | 220 | 플레이어 이동(기본 ~100) 이후, Overlay(300) 이전 |
| 직렬화 전략 | AddComponent at runtime (Bootstrap) | 씬 YAML 비수정. 기존 패턴과 일치 |

보류(미채택): Cinemachine Confiner2D, Impulse Listener, ortho 동적 적응 → Cinemachine
전환 시점에 일괄 도입.

## 추가/수정 파일

신규 파일 (1개):

```text
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/KhiPlayerCamera.cs
```

수정 파일 (2개):

```text
LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/TestKhiInputManager.cs
    - _overrideCamera 필드 추가
    - FollowMainCamera 초입에 "KhiPlayerCamera 존재 시 양보" 가드 추가
    - 기타 필드/메서드 서명 불변

LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/TestKhiSceneBootstrap.cs
    - using UnityEngine.U2D 추가
    - EnsureCamera 끝에서 EnsurePlayerCameraRig 호출
    - EnsurePlayerCameraRig 신설: Main Camera에 KhiPlayerCamera + PixelPerfectCamera 부착
```

csproj(gitignore)에 신규 파일 1개 수동 추가. Unity 재실행 시 자동 재생성.

문서:

```text
client/docs/khi/cl016_camera_follow_implementation.md
```

## 컴포넌트 구조

### KhiPlayerCamera

- `[DefaultExecutionOrder(220)]` — 플레이어 이동 이후
- `[RequireComponent(typeof(Camera))]`
- LateUpdate에서 follow 계산

**공개 API**:

```text
Transform FollowTarget                  // read-only
void      SetFollowTarget(Transform)    // Bootstrap/InputManager가 호출
Vector2   ExternalLookahead { get; set; }
Func<Vector2> LookaheadProvider { get; set; }
```

**SerializeField**:

| 필드 | 기본값 | 설명 |
|---|---|---|
| followTarget | null | 추적 대상 Transform |
| offset | (0, 0, -10) | 카메라 z 고정. xy는 선택적 오프셋 |
| deadzoneWidth / deadzoneHeight | 1.2 / 0.7 (world units) | 중앙 무반응 영역 크기 |
| smoothTime | 0.18 | SmoothDamp time |
| maxSpeed | 40 | SmoothDamp max speed |
| snapOnStart | true | 첫 프레임 즉시 follow 위치로 스냅 |
| snapOnTargetChange | true | SetFollowTarget 호출 시 스냅 |
| editorLookahead | (0, 0) | Inspector 수동 테스트용 |
| drawDeadzoneGizmo | true | Scene 뷰에 deadzone 시각화 |

**Follow 수식 (LateUpdate)**:

```text
1. lookahead = LookaheadProvider?.Invoke() ?? (ExternalLookahead + editorLookahead)
2. followPoint = target.xy + offset.xy + lookahead
3. If !_hasSnapped: transform.position = (followPoint.x, followPoint.y, offset.z); snap.
4. Else:
   - halfW/halfH = deadzone / 2
   - dx = followPoint.x - cam.x;  dy = followPoint.y - cam.y
   - adjustX = max(0, |dx| - halfW) * sign(dx)   (if dx 밖이면 밀기, 안이면 0)
   - adjustY = 동일
   - desiredCam = cam + (adjustX, adjustY)
   - transform.position = SmoothDamp(cam, desiredCam, smoothTime, maxSpeed)
   - z 는 offset.z 유지
```

### TestKhiInputManager.FollowMainCamera — 양보 가드

```csharp
if (_overrideCamera == null && _mainCamera != null)
    _overrideCamera = _mainCamera.GetComponent<KhiPlayerCamera>();

if (_overrideCamera != null)
{
    if (_overrideCamera.FollowTarget != _followTarget)
        _overrideCamera.SetFollowTarget(_followTarget);
    return;
}
// 기존 SmoothDamp 경로 그대로
```

- `followPlayerWithMainCamera` 등 기존 SerializeField는 건드리지 않음. scene YAML field IDs 불변.
- Override 해제: 카메라에서 `KhiPlayerCamera` 컴포넌트만 제거하면 자동으로 InputManager
  기존 SmoothDamp 경로 복귀.

### TestKhiSceneBootstrap.EnsurePlayerCameraRig

Bootstrap.Awake → EnsureCamera → EnsurePlayerCameraRig 순서로:

1. Main Camera에 `KhiPlayerCamera` 없으면 AddComponent
2. Main Camera에 `PixelPerfectCamera` 없으면 AddComponent
3. PPC 파라미터 세팅:
   - `assetsPPU = 16`
   - `refResolutionX = 320`, `refResolutionY = 180`
   - `upscaleRT = false`
   - `cropFrameX = false`, `cropFrameY = false`

Play 진입 시마다 idempotent하게 실행되므로 씬/프리팹 YAML 수정 없음.

## 주요 설계 결정

### 1. 왜 커스텀인가 (Cinemachine 대신)

유저 요청: "나중에 바꾸어야 하면 커스텀". Cinemachine 3은 훌륭하지만 follow 커브와
damping 모델이 고정적이라 프로젝트 고유 전투 감각 튜닝에 제약이 된다. 자체 컴포넌트는
~120 줄로 충분하고, 이후 Cinemachine으로 교체 시 `KhiPlayerCamera` → `CinemachineCamera` +
`CinemachineFollow` 설정 대체로 매몰 비용이 낮다.

### 2. 왜 Deadzone 먼저, Lookahead 나중인가

유저 요청: "1번 기본으로, 나중에 4번(Deadzone+Lookahead) 변환 가능한 형태로, 4번 갔을 때
side-effect 최소화". Deadzone만 있는 상태에서 Lookahead를 더할 때 `followPoint = target +
lookahead` 한 줄만 바뀌도록 설계 → 기존 Deadzone 파라미터/튜닝 그대로 유효.
`ExternalLookahead` 필드와 `LookaheadProvider` 델리게이트를 미리 파두어 후속 CL이 문법적
침투할 구멍 확보.

### 3. InputManager 기존 카메라 필드를 안 지운 이유

`TestKhiInputManager` 프리팹 인스턴스가 씬에 직렬화돼 `followPlayerWithMainCamera` 등 값이
YAML에 저장됨. 필드 삭제 → scene YAML diff 발생. 대신 "양보" 가드만 추가해
`KhiPlayerCamera` 존재 시 자연스럽게 비활성. Override 제거 시 자동으로 기존 경로 복귀.

### 4. 씬 YAML 편집 대신 Bootstrap 확장

`.unity`/`.prefab` 직접 편집은 [agent-unity-safety-rules.md](../commonness/agent-unity-safety-rules.md)
원칙에 위배. 기존 `TestKhiSceneBootstrap`이 이미 Main Camera, Tilemap, DamageTrap,
ReviveZone 등을 코드로 보장하는 패턴을 따른다. 같은 파일에 한 블록 추가.

### 5. PixelPerfectCamera 파라미터

- **Ref Resolution 320×180**: 16:9, ortho size ≈ 5.625 (PPU 16 기준). 현재 ortho 5와
  근사. 16:9 모니터에서 스케일 정수배.
- `UpscaleRT = false`: RT 생성 비용 제거, 단순 카메라 렌더. CL-017 히트스톱/shake와
  호환성 우선.
- `CropFrame* = false`: 검은 테두리 없이 ortho로 화면 채움. Ref Res 비율과 화면 비율이
  다를 때는 세로/가로로 약간 더 보이게 됨 (모바일 대응 유리).
- PPC는 OnEnable에서 카메라 orthographicSize를 스스로 덮어씀. Bootstrap이 ortho=6으로
  세팅해도 PPC가 Play 시 5.625 근처로 재계산.

### 6. Execution Order 220 선택

- CL-015 `KhiPlayerStateAggregator` = 200 (플레이어 이동 후 상태 집계)
- 신규 `KhiPlayerCamera` = 220 (상태 집계 후 카메라 추적)
- CL-015 `KhiPlayerStateDebugOverlay` = 300 (모두 끝난 후 화면 그리기)

카메라가 LateUpdate에서 동작하므로 Update 순서는 엄밀히 중요하지 않지만, 동일 프레임에
Aggregator가 `StateChanged` 이벤트를 발사하고 카메라가 그 상태를 읽어야 할 경우(예: Defeat
시 줌 아웃)가 후속 CL에 생길 수 있어 여유 있게 배치.

### 7. Snap 정책

- `snapOnStart = true`: Play 진입 시 플레이어가 원점(0,0)에서 꽤 멀리 있으면 카메라가
  SmoothDamp로 천천히 따라가면서 보기 싫음. 첫 프레임에 즉시 snap.
- `snapOnTargetChange = true`: `DebugRespawn` 후 플레이어 위치가 원점으로 리셋될 때
  카메라가 부드럽게 이동하지 않고 즉시 붙도록. UX 편의.

## 검증 결과

### 자동 검증

```text
dotnet build LostMemory/Assembly-CSharp.csproj --no-restore
→ 기존 2개 deprecation 경고만, 신규 에러/경고 0
```

csproj(gitignore)에 신규 파일 1개 수동 추가. Unity 재실행 시 자동 재생성.

### 수동 검증 (플레이 테스트) — 대기 중

- [ ] #1 Play 진입 시 카메라가 플레이어 근처로 즉시 snap (snapOnStart=true 동작)
- [ ] #2 가만히 서서 방향만 돌리기 → 카메라 정지 (deadzone 내부)
- [ ] #3 WASD로 deadzone 경계 넘도록 이동 → 카메라가 부드럽게 따라옴
- [ ] #4 이동 정지 시 카메라 진동 없음 (SmoothDamp velocity → 0)
- [ ] #5 대시(Space) 중 카메라 과도한 overshoot 없음 (maxSpeed 작동)
- [ ] #6 F3 Debug Overlay 여전히 좌상단 260×240 박스로 정상 표시
- [ ] #7 Defeat (체력 0, 10초 대기) → 플레이어 GameObject 비활성 → 카메라 마지막 위치 유지
- [ ] #8 P 키 Debug Respawn 후 플레이어 원점 재등장 → 카메라 즉시 snap (snapOnTargetChange)
- [ ] #9 픽셀 선명도: 이동 중 sprite 가장자리 지글거림/흐려짐 없음 (PixelPerfectCamera)
- [ ] #10 Scene 뷰에서 카메라 선택 시 노란 deadzone Gizmo 표시
- [ ] #11 InputManager `followPlayerWithMainCamera` off ↔ on 변경해도 카메라 동작 동일
      (양보 가드 동작 확인 — off여도 KhiPlayerCamera가 독립적으로 추적)

### 미래 확장 smoke test

- [ ] Inspector에서 `editorLookahead = (2, 0)` 수동 설정 → 카메라가 오른쪽으로 2 유닛
      선행. (0,0) 복원 시 즉시 원복. (Lookahead slot 동작 검증)

## 미해결 / 알려진 이슈

### PixelPerfectCamera와 에디터 Aspect Ratio

PPC는 Game 뷰의 Free Aspect일 때 예기치 못한 ortho size를 산출할 수 있다. 개발 중에는
16:9 고정(1920×1080) Aspect 사용을 권장. 실제 빌드에서는 화면 비율에 맞춰 PPC가
자동 조정하므로 문제 없음.

### Bootstrap이 EditMode에서 카메라 컴포넌트를 추가하지 않음

`EnsureCamera`가 `Application.isPlaying` 경로에서만 호출되므로 EditMode에서는
`KhiPlayerCamera`와 `PixelPerfectCamera`가 씬에 보이지 않는다. 이는 YAML churn 방지를
위한 의도된 동작. Play 진입마다 idempotent하게 부착된다. 만약 EditMode에서도 보고 싶다면
Inspector에서 수동 AddComponent 가능 (권장하지 않음 — 씬 저장 시 diff 발생).

## 후속 CL 연결

- **CL-017 히트스톱/VFX**: `KhiPlayerCamera.ApplyImpulse(Vector2, float)` 추가 고려.
  본격 shake는 Cinemachine Impulse 도입 시점으로 이연.
- **CL-018 패링 성공 피드백**: 패링 순간 0.05s ortho size pulse(5 → 4.8 → 5) 추가 가능.
  PixelPerfectCamera와 호환성 필요 (PPC가 ortho를 덮어써 pulse가 무효화될 수 있음 → PPC
  `pixelRatio` 조작 또는 CinemachineCompatibilityMode로 우회).
- **CL-016.1 (가칭) 전투 HUD**: 별도 티켓. `KhiPlayerStateAggregator.StateChanged` +
  `KhiDownController.DownTimerTicked`/`ReviveProgressChanged` + `Health.OnHealthChanged`
  구독해 Canvas/TMP HUD 구성.
- **CL-020대 네트워크**: 카메라는 로컬 관심사이므로 네트워크 동기화 없음. Defeated 타
  플레이어 화면 처리(스펙테이터 모드)는 별도 고려.
- **Cinemachine 전환 (후보)**: 전투 방 Confiner2D가 실제로 필요해지는 시점(CL-033~035
  방 경계). `KhiPlayerCamera` → `CinemachineCamera` + `CinemachineFollow` 마이그레이션.

## 관련 문서

- 구현 계획 (로컬): `C:\Users\AD\.claude\plans\sparkling-snuggling-meteor.md`
- 직전 CL: [cl015_player_state_aggregator_implementation.md](cl015_player_state_aggregator_implementation.md)
- 클라1 마스터 플랜: [client1_tasks_master_plan.md](client1_tasks_master_plan.md) (CL-016 정의 L36)
- 아트 방향 (ortho/PPU 근거): [docs/09_art_direction.md](../../../docs/09_art_direction.md)
- 씬/프리팹 안전 규칙: [agent-unity-safety-rules.md](../commonness/agent-unity-safety-rules.md)
