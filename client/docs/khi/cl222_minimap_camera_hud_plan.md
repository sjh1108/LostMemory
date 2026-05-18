# CL-222: 미니맵 카메라 + HUD + 마커 (싱글)

**Epic**: H. UI / 연출 / 아트 적용
**상태**: 코드 ✅ 완료 (4 파일) / Editor ⚠️ 부분 완료 (Player+Chobomb) / 검증 ⚠️ 부분 통과 (HUD/M키/풀링/멀티방 ✅, 적 6개+보스 2개 부착 대기)
**선행**: 없음
**후속**: CL-223 (Fog of War), CL-224 (멀티 통합), CL-226 (추적 모드)

> **Phase B/C 실행 plan**: [cl222_minimap_phaseBC_execution.md](./cl222_minimap_phaseBC_execution.md) — 구체 prefab/씬명 + step-by-step Editor 작업 순서
> **후속 추적 모드 plan**: [cl226_minimap_follow_plan.md](./cl226_minimap_follow_plan.md) — FollowTarget FitMode 추가

---

## Context

LostMemory(절차 생성 던전 코옵)에 미니맵이 없어 플레이어가 자기 위치/탐색 진행/적·팀원 위치를 파악할 방법이 없음.

본 CL: **싱글플레이에서 작동하는 미니맵 1차 산출물** — 코너 HUD + M키 큰 맵 + 플레이어/적 마커. Fog of war 와 멀티 통합은 후속 CL 에서.

**구현 전략 결정 — 자체 카메라 방식**:
DungeonArchitect 의 `GridFlowMinimap` 을 쓰지 않고 자체 orthographic 카메라 + RenderTexture + uGUI 마커로 구현. 이유:
- `DungeonRunBootstrap.usePrebuiltLayout=true` 모드(방을 씬에 직접 배치)에서 DA 미니맵이 빈 화면이 됨 ([DungeonRunBootstrap.cs:51-53](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/DungeonRunBootstrap.cs))
- DA build 자체가 불안정한 이슈가 있어 미니맵까지 휘말리는 위험 회피
- 좌표계가 2D(XY 평면) — DA `GridFlowMinimap` 은 XZ 기준이라 추가 override 필요했음
- 멀티플레이에서 호스트만 던전 빌드 → 클라 측 모델 부재 우려가 사라짐 (각 클라가 자체 카메라)

비용: 코드 ~150줄 추가 (DA 글루 ~30줄 대비). DA 안정성과 무관한 시각 시스템 확보.

---

## 시스템 사실

- **PlayerHUD prefab**: HP/MP widget — Canvas 가 아닌 child widget. Canvas 자체는 씬에 별도 존재 ([HealthBarView.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/UI/HealthBarView.cs))
- **풀링 적**: `EnemyDataRuntimeAdapter` 가 `OnEnable`/`OnDisable` 에 `ActiveAdapters` HashSet 추가/제거 ([EnemyDataRuntimeAdapter.cs:69-77](../../LostMemory/Assets/_Project/Scripts/Runtime/Enemies/EnemyDataRuntimeAdapter.cs)). `MMSimpleObjectPooler` 사용 — `Start` 만 쓰는 등록은 풀 재사용 시 누락. **MinimapAgent 도 동일 패턴 (`OnEnable`/`OnDisable`)** 적용
- **2D 프로젝트** (XY 평면): 메인 카메라 z=-10, 던전/스프라이트 z≈0
- **DA Dungeon Build 흐름**: `DungeonRunBootstrap.OnSpawnedManagedObjects` → `DungeonBuilt` 이벤트 발화 ([DungeonRunBootstrap.cs:132-186](../../LostMemory/Assets/_Project/Scripts/Runtime/Stage/DungeonRunBootstrap.cs)). 미니맵 fit 자동 갱신을 후속에서 추가하면 이 hook 사용 가능
- **M 키**: 프로젝트 전체에서 미사용 — 충돌 없음 (grep 검증 완료)

---

## 결정 사항 (사용자 확정)

| 결정 | 값 | 근거 |
|---|---|---|
| 구현 전략 | 자체 카메라 + RenderTexture + uGUI 마커 | DA `GridFlowMinimap` 우회 — `usePrebuiltLayout` 호환 + DA 안정성 의존 제거 + XY 평면 직접 처리 |
| HUD Canvas | **별도** `MinimapCanvas` (Sort Order 10) | 풀스크린 큰 맵 + 후속 폴리시(줌/일시정지) 자유 |
| 큰 맵 레이아웃 | 화면 중앙 600×600 + 반투명 검정 배경 | option (a) — 조정 쉽고 후속 폴리시 자유 |
| Fit Mode 1차 | **Manual** (인스펙터 수동) | 던전 builder 코드 손 안 대고 미니맵만 끝냄. AutoFromBounds 자동화는 CL-225+ 후속 |
| 마커 색상 (1차) | 플레이어=**파랑**, 팀=**초록**, 적=**빨강**, 보스=**주황** + 큰 사이즈 | 임시 흰원형 sprite 1종 공유 + `MinimapAgent.tint` 분리. 후속에 진짜 아이콘 교체 |
| 토글 키 | **M** | 충돌 검사 — 사용처 0건 확인 |
| Fog of war | **본 CL 범위 외** (CL-223) | 1차는 fog 없이 전체 노출. fog 는 별 작업이라 분리 |
| 멀티 통합 | **본 CL 범위 외** (CL-224) | 싱글로 1차 검증 후 멀티 분기 |

---

## 작업 범위

### Phase A — 코드 (✅ 완료)

- [x] `MinimapAgent.cs` — Player/Enemy prefab 부착용 등록 컴포넌트 (`OnEnable`/`OnDisable`)
- [x] `MinimapMarkerOverlay.cs` — 마커 풀, world→Viewport→RectTransform 매핑
- [x] `MinimapHUD.cs` — RawImage 2 장 관리, M 키 토글, Big map pause 옵션
- [x] `MinimapCameraRig.cs` — orthographic 카메라 설정 + Manual/AutoFromBounds fit

### Phase B — Editor 작업 (⏳ 사용자 / khi)

- [ ] `Minimap` Layer 추가
- [ ] `MinimapRT.renderTexture` 자산 생성 (256×256)
- [ ] `MinimapMarker.prefab` 생성 (UI Image, 임시 흰 원형)
- [ ] `MinimapCanvas` 씬 추가 + Sort Order 10
- [ ] `MinimapCamera` GameObject + `MinimapCameraRig` 컴포넌트 + Manual fit 값 설정
- [ ] HUD Canvas 안에 SmallMap / BigMap RawImage 2 장 + 각각 `MinimapMarkerOverlay` 부착
- [ ] `MinimapHUD` 컴포넌트 부착 + 모든 ref 연결
- [ ] Player prefab 에 `MinimapAgent` 부착 (Kind=PlayerLocal, Tint=파랑)
- [ ] Enemy prefab(들)에 `MinimapAgent` 부착 (Kind=Enemy, Tint=빨강)
- [ ] (선택) Boss prefab 에 `MinimapAgent` 부착 (Kind=Boss, Tint=주황, IconScale=1.5+)

### Phase C — 검증 (⏳ 사용자 / khi)

- [ ] 던전 입장 → 코너 HUD 에 검정 배경 + 플레이어 파랑 마커가 자기 위치
- [ ] 플레이어 이동 → 마커 따라옴 (회전 화살표 동작 확인)
- [ ] 적 스폰 → 빨강 마커, 처치 → 사라짐
- [ ] 풀링된 적 재사용 → 다시 등록되어 마커 복귀 (`OnEnable` 패턴 검증)
- [ ] M 키 → 큰 맵 표시/숨김
- [ ] 보스방 입장 → 주황 큰 마커
- [ ] 미니맵 배경 정책 결정 후 던전 visual 이 미니맵에 반영 확인 (아래 "결정 보류")

### 작업 외 (Out of scope)

- Fog of war (CL-223)
- 멀티플레이 통합 (팀원 마커 색 분리, 공유 fog) (CL-224)
- AutoFromBounds 자동 fit — 던전 builder 에서 bounds 노출 추가 코드 필요 (후속)
- 줌 인/아웃, 큰 맵 텍스트 라벨 (후속)
- 진짜 아이콘 자산 (후속, 디자이너)
- 일시정지 / 큰 맵에서의 입력 차단 (현재 코드는 `pauseTimeWhenBigMapOpen` 옵션 노출만 — 기본 false)

---

## 변경 파일

### 신규

| 파일 | 책임 |
|---|---|
| [MinimapAgent.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/UI/Minimap/MinimapAgent.cs) | Player/Enemy prefab 부착, 활성 agent registry, 마커 시각 데이터 (icon/tint/scale/rotate/priority) |
| [MinimapMarkerOverlay.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/UI/Minimap/MinimapMarkerOverlay.cs) | 매 프레임 활성 agent 순회, `Camera.WorldToViewportPoint` → `mapRect` 내 `anchoredPosition` 매핑, UI Image 풀 |
| [MinimapHUD.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/UI/Minimap/MinimapHUD.cs) | SmallMap/BigMap RawImage 2 장 관리, M 키 토글, RenderTexture 공통 바인딩 |
| [MinimapCameraRig.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/UI/Minimap/MinimapCameraRig.cs) | orthographic 설정 + culling mask + target texture + Manual/AutoFromBounds fit |

### 수정 — 본 CL

없음 (기존 코드 무수정)

---

## 코드 spec (Phase A 산출물 상세)

### MinimapAgent

```csharp
namespace LostMemory.UI.Minimap;

[DisallowMultipleComponent]
public class MinimapAgent : MonoBehaviour
{
    public enum AgentKind { PlayerLocal, PlayerRemote, Enemy, Boss, Pickup, Portal }

    [SerializeField] AgentKind kind;
    [SerializeField] Sprite icon;
    [SerializeField] Color tint;
    [SerializeField, Min(0.1f)] float iconScale = 1f;
    [SerializeField] bool rotateWithTransform;
    [SerializeField] int priority;

    public static IReadOnlyList<MinimapAgent> All { get; }
    public Vector3 WorldPosition => transform.position;

    public virtual Quaternion GetMarkerRotation();  // 2D: Quaternion.Euler(0,0,transform.eulerAngles.z) when rotateWithTransform
    protected virtual void OnEnable();   // ActiveAgents.Add(this) — 풀 재사용 안전
    protected virtual void OnDisable();  // ActiveAgents.Remove(this)
}
```

**확장 포인트**: `GetMarkerRotation()` `virtual` — 캐릭터가 sprite 자체로 회전하지 않고 별도 aim 변수를 갖는 경우 서브클래싱.

### MinimapMarkerOverlay

```csharp
namespace LostMemory.UI.Minimap;

[DisallowMultipleComponent]
public sealed class MinimapMarkerOverlay : MonoBehaviour
{
    [SerializeField] Camera minimapCamera;     // 보통 MinimapCameraRig 의 Camera
    [SerializeField] RectTransform mapRect;    // 보통 RawImage 의 RectTransform
    [SerializeField] RectTransform markerParent; // 비어있으면 mapRect 사용
    [SerializeField] Image markerPrefab;
    [SerializeField] bool clampOffscreen = true;
    [SerializeField, Min(0)] int initialPoolSize = 16;

    private void Awake();        // 풀 prewarm
    private void LateUpdate();   // 매 프레임 마커 갱신
}
```

**좌표 계산**:
```
viewportPoint = camera.WorldToViewportPoint(agent.WorldPosition)  // [0,1]² 범위
anchoredPosition = ((viewportPoint - 0.5) * mapRect.size)
```

마커 prefab 의 anchor/pivot 설정과 무관하게 작동하도록 `CreateMarker` 에서 강제 (0.5, 0.5) 설정.

**같은 RT 가 두 곳(SmallMap/BigMap)에 표시되므로 각 표시 영역마다 별도 `MinimapMarkerOverlay` 인스턴스 필요** (RectTransform 크기가 달라 좌표 별도 계산).

### MinimapHUD

```csharp
namespace LostMemory.UI.Minimap;

[DisallowMultipleComponent]
public sealed class MinimapHUD : MonoBehaviour
{
    [SerializeField] RenderTexture minimapRenderTexture;
    [SerializeField] GameObject smallMapRoot;
    [SerializeField] RawImage smallMapImage;
    [SerializeField] GameObject bigMapRoot;
    [SerializeField] RawImage bigMapImage;
    [SerializeField] KeyCode toggleBigMapKey = KeyCode.M;
    [SerializeField] bool startWithBigMapVisible;
    [SerializeField] bool pauseTimeWhenBigMapOpen;  // 기본 false (디아블로 스타일)

    public bool IsBigMapVisible { get; }
    public void ToggleBigMap();
    public void SetBigMapVisible(bool);
    public void SetMinimapRenderTexture(RenderTexture);
}
```

**시간 정지 옵션**: `pauseTimeWhenBigMapOpen=true` 면 BigMap 열 때 `Time.timeScale = 0`. `OnDisable` 에서 자동 복구. 1차 권장은 false.

### MinimapCameraRig

```csharp
namespace LostMemory.UI.Minimap;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class MinimapCameraRig : MonoBehaviour
{
    public enum FitMode { Manual, AutoFromBounds }

    [SerializeField] RenderTexture targetTexture;
    [SerializeField] LayerMask cullingMask;        // "Minimap" 만 권장
    [SerializeField] Color backgroundColor;
    [SerializeField] float cameraZ = -50f;         // 메인 카메라 z=-10 보다 멀리
    [SerializeField] FitMode fitMode = FitMode.Manual;
    [SerializeField] Vector2 manualCenter;
    [SerializeField, Min(1f)] float manualSize = 20f;
    [SerializeField] Bounds autoBounds;
    [SerializeField, Range(1f,1.5f)] float autoPadding = 1.05f;

    public void RefreshFit();
    public void SetAutoBounds(Bounds);   // 후속에서 DungeonRunBootstrap.DungeonBuilt 시점에 호출
    public void SetFitMode(FitMode);
}
```

**Camera 설정**: orthographic, `clearFlags=SolidColor`, `depth=-1` (메인 카메라보다 먼저 렌더 → RT 가 다음 프레임에 RawImage 에 반영), `allowHDR=false`, `allowMSAA=false`.

---

## Editor 작업 체크리스트 (Phase B 상세)

### 1. Layer 추가
- `Edit > Project Settings > Tags and Layers` → 빈 User Layer 슬롯에 `Minimap` 추가

### 2. RenderTexture 자산
- Project 창 우클릭 → `Create > Render Texture`
- 경로: `Assets/_Project/RenderTextures/MinimapRT.renderTexture`
- 설정: Size 256×256, 기본 ColorFormat, Depth Buffer No depth

### 3. Marker prefab
- Hierarchy 에 UI > Image 생성
- 이름: `MinimapMarker`
- RectTransform Size 16×16, Image Source = 임시 흰 원형 sprite (Unity 내장 `UISprite` 또는 `Knob` 사용 가능)
- 프리팹화: 끌어서 `Assets/_Project/Prefabs/UI/Minimap/MinimapMarker.prefab`
- 씬에서 원본 GameObject 삭제

### 4. Minimap Camera
- Hierarchy 루트에 빈 GameObject 생성, 이름 `MinimapCamera`
- `Camera` 컴포넌트 자동 추가 (Reset)
- `MinimapCameraRig` 컴포넌트 추가
- 인스펙터:
  - Target Texture: `MinimapRT`
  - Culling Mask: `Minimap` 만 체크 (다른 다 해제)
  - Fit Mode: `Manual`
  - Manual Center: 던전 대략 중앙 좌표 (예: `(0, 0)`)
  - Manual Size: 던전 절반 크기 + 여유 (예: 20)
- 검증: Play 모드 진입 시 인스펙터에서 Camera 의 OrthographicSize 가 자동 적용되는지 확인

### 5. MinimapCanvas (별도 Canvas)
- Hierarchy 루트에 UI > Canvas 추가, 이름 `MinimapCanvas`
- `Canvas`:
  - Render Mode: Screen Space - Overlay
  - Sort Order: 10 (다른 HUD 보다 위)
- `Canvas Scaler`: Scale With Screen Size, Reference Resolution 1920×1080

### 6. SmallMap (HUD 코너)
`MinimapCanvas` 안에:
- 빈 GameObject `SmallMap` (RectTransform Anchor 우상단, Pivot 우상단, Position offset 으로 가장자리 여유)
  - Width 200, Height 200
  - 자식: UI > Raw Image, 이름 `SmallMapImage`, RectTransform Stretch (anchor 0,0 ~ 1,1)
    - Texture: `MinimapRT`
  - 자식: 빈 GameObject `MarkerOverlay_Small`, RectTransform Stretch
    - 컴포넌트: `MinimapMarkerOverlay`
      - Minimap Camera: `MinimapCamera` 의 Camera
      - Map Rect: `SmallMapImage` 의 RectTransform
      - Marker Prefab: `MinimapMarker` 의 Image 컴포넌트

### 7. BigMap (M키 토글)
`MinimapCanvas` 안에:
- 빈 GameObject `BigMap` (RectTransform Anchor 중앙, Pivot 중앙, Position 0,0)
  - 처음에 비활성화 (체크 해제) — `MinimapHUD.startWithBigMapVisible=false` 와 일관
- 자식: UI > Image, 이름 `Backdrop` (Stretch 풀 화면, Color (0,0,0,0.6))
- 자식: UI > Raw Image, 이름 `BigMapImage`, RectTransform 600×600 중앙
  - Texture: `MinimapRT`
- 자식: 빈 GameObject `MarkerOverlay_Big`, RectTransform 600×600 중앙
  - 컴포넌트: `MinimapMarkerOverlay`
    - 같은 카메라 + `BigMapImage` RectTransform + `MinimapMarker` prefab

### 8. MinimapHUD 컴포넌트 부착
- `MinimapCanvas` 루트에 `MinimapHUD` 컴포넌트 추가
- 인스펙터:
  - Minimap Render Texture: `MinimapRT`
  - Small Map Root: `SmallMap` GameObject
  - Small Map Image: `SmallMapImage` (RawImage)
  - Big Map Root: `BigMap` GameObject
  - Big Map Image: `BigMapImage` (RawImage)
  - Toggle Big Map Key: `M`
  - Start With Big Map Visible: false
  - Pause Time When Big Map Open: false

### 9. Player prefab — MinimapAgent 부착
- `Assets/_Project/Prefabs/Characters/<플레이어 prefab>` 열기
- 루트에 `MinimapAgent` 컴포넌트 추가
- 인스펙터:
  - Kind: `PlayerLocal`
  - Icon: `MinimapMarker` 와 동일하거나 별도 player sprite (1차는 같은 흰 원형 OK)
  - Tint: 파랑 `(0.2, 0.5, 1.0, 1.0)`
  - Icon Scale: 1.0 (또는 1.2 — 본인 강조)
  - Rotate With Transform: true (이동 방향 화살표용 — sprite 가 회전하는 경우)
  - Priority: 100

### 10. Enemy prefab(들) — MinimapAgent 부착
- 각 적 prefab 에 `MinimapAgent` 컴포넌트 추가
- 인스펙터:
  - Kind: `Enemy`
  - Icon: 흰 원형 (공통)
  - Tint: 빨강 `(1.0, 0.2, 0.2, 1.0)`
  - Icon Scale: 0.8
  - Rotate With Transform: false
  - Priority: 10

### 11. (선택) Boss prefab — MinimapAgent
- Boss prefab 에 `MinimapAgent`:
  - Kind: `Boss`
  - Tint: 주황 `(1.0, 0.55, 0.0, 1.0)`
  - Icon Scale: 1.8
  - Priority: 80

---

## 검증 방법 (Phase C 상세)

### 코너 HUD 마커 동작
1. Play 모드 진입, 던전 씬 또는 테스트 씬 이동
2. **Expected**: 우상단 200×200 코너에 검정 배경(`MinimapCameraRig.backgroundColor`) + 플레이어 파랑 마커가 화면 중앙
3. 플레이어 이동 → 마커가 같이 이동 (반대 방향이면 manualCenter 잘못 — 던전 중앙 좌표 다시 확인)
4. 플레이어 회전(이동 방향 변경) → 마커 화살표 회전 (회전 안 하면 player prefab 의 transform.eulerAngles.z 가 안 변하는 것 — `rotateWithTransform=false` 로 두거나 후속에서 `MinimapAgent` 서브클래싱)

### 적 마커
5. 적 스폰 → 빨강 마커 등장
6. 적 처치 → 마커 사라짐
7. **풀링 검증**: 같은 적 prefab 이 풀에서 재사용될 때 두 번째도 마커 정상. 콘솔에 `MinimapAgent.OnEnable` 호출 로그 확인 (필요 시 디버그 로그 추가)

### M키 큰 맵 토글
8. M 키 → 화면 중앙 600×600 큰 맵 + 반투명 배경 표시
9. M 키 다시 → 큰 맵 사라짐
10. 큰 맵에서도 마커 동일 동작
11. 큰 맵 토글하면서 게임 진행 가능 (`pauseTimeWhenBigMapOpen=false` 인 한)

### 보스
12. 보스방 입장 → 주황 큰 마커 (다른 적보다 눈에 띔)

### 던전 visual 반영 (결정 보류 항목 — 아래 참조)

---

## 결정 완료 — 미니맵 배경 정책 (2026-05-12)

**결정**: **화이트리스트 Culling Mask** — `MinimapCameraRig.cullingMask` 에 `Default` Layer 만 체크 (던전 sprite 만 노출).

### 경위
1. 1차 검증은 C(빈 배경 + 마커만) 로 시작 → 마커 동작 확인 ✅
2. 던전 시각 노출 단계에서 블랙리스트 방식 시도 (`Everything - Enemies`) → enemy prefab 의 자식(예: `WeaponAttachment`)이 `Default` Layer 라 추적 불가능. 새 sub-object 추가 시 매번 손봐야 함
3. **화이트리스트 전환** — `Default` Layer (던전 sprite) 만 체크. 적/이펙트/투사체/UI 모두 자동 차단. 유지보수 부담 0

### 옵션 비교
| 옵션 | 결과 | 채택 |
|---|---|---|
| A. 블랙리스트 (`Everything - Enemies`) | enemy 자식 Layer 추적 끝없음 | ❌ |
| B. 프록시 sprite (Minimap Layer 전용 도형) | 디자이너 작업 동반 | 후속 폴리시 |
| C. 빈 배경 + 마커만 | 던전 형상 안 보임 | 1차 검증용으로만 사용 |
| **D. 화이트리스트 (Default Layer 만)** | **던전만 노출, 적/이펙트 자동 차단** | ✅ 채택 |

### 적용
- `MinimapRig.prefab > MinimapCamera > MinimapCameraRig.cullingMask` = `Default` 만 체크
- 후속에 프록시 sprite 추가 시 `Minimap` Layer 도 함께 체크
- 다른 Layer (Enemies/UI/이펙트 등) 분류 작업 불필요

---

## 알려진 이슈 / 보류

- **풀링 적 등록**: `MinimapAgent` 가 `OnEnable`/`OnDisable` 패턴 사용 — `EnemyDataRuntimeAdapter` 와 동일. 풀링 안전. 다만 `ActiveAgents` static List 가 씬 전환 시 stale 참조 갖지 않게 `OnDisable` 에서 확실히 제거 (코드 검토 시 확인됨)
- **마커 prefab 의 RectTransform**: `CreateMarker` 에서 anchor/pivot 강제 (0.5,0.5) — prefab 인스펙터 값과 무관. 디자이너가 prefab 의 anchor 를 다르게 설정해도 마커 위치 안 깨짐
- **카메라 z 위치**: `MinimapCameraRig.cameraZ = -50f` 기본값. 메인 카메라 z=-10 과 충돌 없음. orthographic 이라 거리 자체는 시야에 영향 없음 — near/far plane 안에 던전이 들어오기만 하면 OK
- **Fit Mode AutoFromBounds**: 인스펙터에 옵션 노출되어 있으나 1차는 Manual 만 사용. 던전 builder 에서 bounds 계산 + `SetAutoBounds()` 호출 추가는 후속 CL
- **GetMarkerRotation 의 한계**: 2D 캐릭터가 sprite 자체로 회전 안 하고 SpriteRenderer.flipX 같은 기법으로 방향 표시하면 마커 회전 안 됨. 후속에 `MinimapAgent` 서브클래싱 또는 외부 아이콘 회전 source 주입 필요
- **MinimapCanvas Sort Order 10 가정**: 기존 PausePanel / RunResult 등이 Sort Order 더 높으면 그 위로 띄움. 본 CL 검증 시 다른 패널과 겹쳐 보이는지 확인 후 필요하면 조정

---

## 후속 CL

| CL | 내용 |
|---|---|
| **CL-223** | Fog of War — `Texture2D` 영구 누적 reveal 마스크, RawImage 곱하기 합성 |
| **CL-224** | 멀티플레이 통합 — `IsOwner` 기반 로컬/팀원 색 분리, 공유 fog |
| 별도 후속 | AutoFromBounds 자동 fit (던전 builder bounds 노출), 진짜 마커 아이콘 자산, 줌 인/아웃, 큰 맵 텍스트 라벨, 미니맵 프록시 sprite 정책 (A/B) |
