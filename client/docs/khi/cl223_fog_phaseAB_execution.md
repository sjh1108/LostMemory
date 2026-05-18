# CL-223 미니맵 Fog of War — 구현 실행 plan

**상태**: ✅ **종결** (Phase A ✅ / Phase B ✅ / Phase C ✅) — 2026-05-12

> 본 plan 은 [cl223_minimap_fog_of_war_plan.md](./cl223_minimap_fog_of_war_plan.md) (설계 plan) 의 후속 — 실제 코드/Editor 작업 순서 + CL-222 진행 중 변경분 + 본 turn 결정 사항 반영.
>
> 종결 회고는 [cl223_minimap_fog_of_war_report.md](./cl223_minimap_fog_of_war_report.md) 참조.

---

## Context

CL-222 미니맵이 종결됨 (코드 ✅, Editor ✅, 검증 ✅ — Player + Enemy 9개 + Boss 2개 부착 완료). 본 CL: **fog of war 추가**. 플레이어가 본 영역만 점진적으로 밝힘 → 탐색 동기 + 발견 재미.

기존 cl-223 plan 이 이미 매우 자세하나, 본 turn 에서 **3가지 결정 사항이 변경**되어 spec 일부 갱신 필요. 본 문서는 변경분 + 실행 순서를 강조.

---

## 결정 사항 (2026-05-12 확정)

### 기존 plan 대비 변경 사항

| 항목 | 기존 plan | 본 turn 결정 | 영향 |
|---|---|---|---|
| **fog 좌표계** | viewport space (Manual fit 정적 가정) | **world space** | CL-226 추적 모드 호환. fog 마스크 좌표 의미가 카메라 fit 과 무관 |
| **fog 영역 결정** | (미명시) | **`MinimapCameraRig.manualCenter` + `manualSize` 자동 follow** | 디자이너 추가 설정 0. CL-222 셋업 재사용 |
| **MarkerOverlay `agent.Icon == null` skip 결함** | (별도 후속) | **본 CL 에서 같이 정리** | `agent.Icon ?? markerPrefab.sprite` fallback. 코드 +3줄 |
| **알려진 이슈 "viewport space 한계"** | 명시 | **제거** (world space 로 해결) | — |

### 변경 없이 그대로 유지
| 결정 | 값 |
|---|---|
| LOS 정책 | 단순 원형 (LOS 무시) |
| 마스크 해상도 | 256×256 |
| Reveal 반경 | 5 world unit (인스펙터) |
| Falloff | 0.3 (인스펙터) |
| Update throttle | 10Hz |
| Fog 색상 | 검정 |
| 합성 방식 | 별도 RawImage 알파 블렌드 |
| Reveal source | `Kind=PlayerLocal\|PlayerRemote` |
| Fog 초기화 시점 | `OnEnable` |
| 적 외 마커 fog 가시성 | Player 외 모두 fog 가림 |
| Fog 단계 | 2단계 (검정 / 영구 밝음) |
| 가려진 마커 처리 | binary |

---

## CL-222 진행 중 변경 컨텍스트 (본 CL 작업 시 주의)

CL-222 plan 이후 진행 중 결정/변경된 사항으로, 본 CL 작업 시 영향:

1. **`MinimapRig.prefab` 통합 prefab 사용** — MinimapCamera + MinimapCanvas + MinimapHUD 가 하나의 prefab. fog 컴포넌트도 이 prefab 안에 부착 + Apply
2. **배경 정책 = 화이트리스트 Culling Mask (`Default` Layer 만)** — fog 와 무관 (fog 는 RawImage 오버레이라 카메라 culling 과 별개)
3. **검증 씬 = `Assets/Scenes/MAP_1F_1R_khi.unity`** — kjs_test 폴더 아님
4. **`MinimapMarkerOverlay.cs:70` 의 icon null skip 결함** — 본 CL 에서 같이 정리 (위 결정사항)

---

## Phase A — 코드 작업 (한 번에 가능)

### A-1. `MinimapFog.cs` 신규 (world space 버전)

`Assets/_Project/Scripts/Runtime/UI/Minimap/MinimapFog.cs`

기존 cl223 plan 의 spec 을 **world space 로 마이그레이션**:

```csharp
namespace LostMemory.UI.Minimap;

[DisallowMultipleComponent]
[AddComponentMenu("Lost Memory/UI/Minimap/Minimap Fog")]
public sealed class MinimapFog : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private MinimapCameraRig cameraRig;   // ← 변경: Camera 직접 X, Rig 참조 (manualCenter/manualSize 접근)
    [SerializeField] private RawImage[] fogImages;          // SmallMap/BigMap 위 RawImage

    [Header("Mask")]
    [SerializeField, Min(64)] private int maskResolution = 256;
    [SerializeField] private Color fogColor = Color.black;

    [Header("Reveal")]
    [SerializeField, Min(0.1f)] private float revealRadius = 5f;            // world unit
    [SerializeField, Range(0f, 1f)] private float falloff = 0.3f;
    [SerializeField, Range(1f, 30f)] private float updateHz = 10f;
    [SerializeField, Range(0f, 1f)] private float visibilityThreshold = 0.5f;

    [Header("Filter")]
    [SerializeField] private AgentKindMask revealKinds =
        AgentKindMask.PlayerLocal | AgentKindMask.PlayerRemote;

    private Texture2D _mask;
    private Color32[] _maskBuffer;
    private float _accumulator;

    public Texture2D MaskTexture => _mask;
    public float VisibilityThreshold => visibilityThreshold;

    private void OnEnable() { EnsureMask(); ResetMask(); BindToFogImages(); }

    private void Update()
    {
        _accumulator += Time.deltaTime;
        float interval = 1f / Mathf.Max(updateHz, 0.1f);
        if (_accumulator < interval) return;
        _accumulator = 0f;

        UpdateReveal();
        _mask.SetPixels32(_maskBuffer);
        _mask.Apply(false);

        SyncFogImageUVs();   // ← world space 핵심: RawImage 의 uvRect 를 카메라 시야에 맞춰 갱신
    }

    /// <summary>world position 이 reveal 됐는지 검사. MarkerOverlay 에서 호출.</summary>
    public bool IsRevealedAtWorld(Vector3 worldPos)
    {
        if (_mask == null) return true;
        if (!TryWorldToMaskPixel(worldPos, out int x, out int y)) return false;

        Color32 c = _maskBuffer[y * maskResolution + x];
        return c.a < (byte)(visibilityThreshold * 255);
    }

    public void ResetMask() { /* 모든 픽셀 (fogColor.r/g/b, 255) */ }

    /// <summary>world pos → fog 마스크 픽셀 좌표. fog 영역 = manualCenter ± manualSize.</summary>
    private bool TryWorldToMaskPixel(Vector3 worldPos, out int x, out int y)
    {
        x = y = 0;
        if (cameraRig == null) return false;

        Vector2 center = cameraRig.ManualCenter;
        float halfSize = cameraRig.ManualSize;
        // fog 영역: [center - halfSize, center + halfSize] (정사각)
        float u = (worldPos.x - (center.x - halfSize)) / (halfSize * 2f);
        float v = (worldPos.y - (center.y - halfSize)) / (halfSize * 2f);
        if (u < 0f || u > 1f || v < 0f || v > 1f) return false;

        x = Mathf.Clamp((int)(u * maskResolution), 0, maskResolution - 1);
        y = Mathf.Clamp((int)(v * maskResolution), 0, maskResolution - 1);
        return true;
    }

    /// <summary>fog 마스크의 일부 영역만 RawImage 에 표시 (카메라 시야 영역에 해당).</summary>
    private void SyncFogImageUVs()
    {
        if (cameraRig == null) return;

        Vector2 center = cameraRig.ManualCenter;
        float halfSize = cameraRig.ManualSize;
        // 카메라가 보는 영역 → fog 마스크 UV [0,1] 범위
        // CL-226 추적 모드 시 cameraRig.transform.position.xy 가 매 프레임 변함 → uvRect 자동 갱신
        float camHalf = cameraRig.GetCurrentCameraOrthographicSize();
        Vector2 camCenter = cameraRig.GetCurrentCameraCenter();

        float uMin = (camCenter.x - camHalf - (center.x - halfSize)) / (halfSize * 2f);
        float vMin = (camCenter.y - camHalf - (center.y - halfSize)) / (halfSize * 2f);
        float uvSize = (camHalf * 2f) / (halfSize * 2f);

        Rect uvRect = new Rect(uMin, vMin, uvSize, uvSize);
        for (int i = 0; i < fogImages.Length; i++)
        {
            if (fogImages[i] != null) fogImages[i].uvRect = uvRect;
        }
    }

    private void UpdateReveal()
    {
        var agents = MinimapAgent.All;
        for (int i = 0; i < agents.Count; i++)
        {
            var agent = agents[i];
            if (agent == null) continue;
            if (!HasKind(revealKinds, agent.Kind)) continue;

            PaintCircle(agent.WorldPosition);
        }
    }

    private void PaintCircle(Vector3 worldCenter) { /* 알고리즘 cl223 plan 과 동일, 단 world→pixel 변환만 변경 */ }

    private void EnsureMask() { /* Texture2D 생성, FilterMode.Bilinear, wrapMode.Clamp */ }
    private void BindToFogImages() { /* fogImages[i].texture = _mask, color = Color.white */ }
    private static bool HasKind(AgentKindMask mask, MinimapAgent.AgentKind kind) { ... }
}

[System.Flags]
public enum AgentKindMask
{
    PlayerLocal = 1 << 0,
    PlayerRemote = 1 << 1,
    Enemy = 1 << 2,
    Boss = 1 << 3,
    Pickup = 1 << 4,
    Portal = 1 << 5,
}
```

### A-2. `MinimapCameraRig.cs` 작은 수정

`MinimapFog` 가 참조할 manualCenter / manualSize / 현재 카메라 위치/size 의 public getter 추가:

```csharp
public Vector2 ManualCenter => manualCenter;
public float ManualSize => manualSize;
public Vector2 GetCurrentCameraCenter() => new Vector2(transform.position.x, transform.position.y);
public float GetCurrentCameraOrthographicSize() => _camera != null ? _camera.orthographicSize : manualSize;
```

3~4줄 추가. 기존 동작 변경 없음.

### A-3. `MinimapMarkerOverlay.cs` 수정

두 가지 변경:
1. **fog 가시성 검사 추가** (cl223 plan 그대로)
2. **icon null skip 결함 정리** (본 turn 결정)

```csharp
// 추가 필드
[SerializeField, Tooltip("fog 가시성 검사. null 이면 모든 마커 항상 표시 (CL-222 동작).")]
private MinimapFog fog;

// LateUpdate 안 agent 순회 — 변경 부분만
for (int i = 0; i < agents.Count; i++)
{
    MinimapAgent agent = agents[i];
    if (agent == null) continue;          // ← 변경: icon null check 제거

    // ★ icon fallback (본 turn 결정)
    Sprite spriteToUse = agent.Icon != null ? agent.Icon : markerPrefab.sprite;
    if (spriteToUse == null) continue;     // 둘 다 없으면 skip

    Vector3 vp = minimapCamera.WorldToViewportPoint(agent.WorldPosition);
    bool offscreen = vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f || vp.z < 0f;

    if (offscreen)
    {
        if (!clampOffscreen) continue;
        vp.x = Mathf.Clamp01(vp.x);
        vp.y = Mathf.Clamp01(vp.y);
    }

    // ★ fog 가시성 검사 (cl223 plan)
    if (fog != null && !IsAlwaysVisible(agent.Kind))
    {
        if (!fog.IsRevealedAtWorld(agent.WorldPosition)) continue;
    }

    // ... 기존 마커 생성/위치/색 적용 ...
    marker.sprite = spriteToUse;           // ← 변경: agent.Icon 대신 spriteToUse
    // 기존: marker.sprite = agent.Icon;
    // ... 나머지
}

private static bool IsAlwaysVisible(MinimapAgent.AgentKind kind)
    => kind == MinimapAgent.AgentKind.PlayerLocal
    || kind == MinimapAgent.AgentKind.PlayerRemote;
```

### A-4. (선택) `MinimapHUD.cs` 수정

`[SerializeField] MinimapFog fog;` 슬롯 추가 — Editor wiring 편의용. 직접 동작 변경 X. 본 CL 에선 skip 가능 (필수 아님).

---

## Phase B — Editor 작업

> CL-222 의 `MinimapRig.prefab` 안에서 작업. 변경 후 Apply.

### B-1. `Fog_Small` RawImage 생성
- `MinimapRig.prefab` 더블클릭 → 편집 모드
- `MinimapCanvas > SmallMap > SmallMapImage` 의 자식으로 빈 GameObject `Fog_Small` 추가
- Add Component → `Raw Image`
- RectTransform: Anchor `stretch-stretch`, offset 모두 0 (SmallMapImage 안 가득)
- Raw Image:
  - Texture: 비움 (런타임에 MinimapFog 가 바인딩)
  - Color: `(1, 1, 1, 1)` 흰색 — texture 색 그대로 표시
  - Raycast Target: **체크 해제**

### B-2. `Fog_Big` RawImage 생성
- `MinimapCanvas > BigMap > BigMapImage` 의 자식으로 `Fog_Big` 동일 셋업

### B-3. **z-order 확인** (중요)
fog 가 미니맵 위에 덧대지고, 마커는 fog 위에 그려져야 함:
```
SmallMap
├─ SmallMapImage          (z 가장 뒤)
│   └─ Fog_Small          (그 위)
└─ MarkerOverlay_Small    (z 가장 앞 — 마커가 fog 위)
```
Hierarchy 순서로 결정됨. `MarkerOverlay_Small` 이 `SmallMap` 의 마지막 자식이어야 함. BigMap 동일.

### B-4. `MinimapFog` 컴포넌트 부착
- `MinimapHUD` GameObject 에 `MinimapFog` 컴포넌트 추가 (또는 별도 `MinimapFog` GameObject)
- 인스펙터:
  - **Camera Rig**: `MinimapCamera` 의 `MinimapCameraRig` 컴포넌트
  - **Fog Images**: 배열 크기 2 → `Fog_Small` 의 RawImage, `Fog_Big` 의 RawImage
  - **Mask Resolution**: 256
  - **Fog Color**: 검정 (0, 0, 0, 1)
  - **Reveal Radius**: 5 (Manual Size 보다 작아야)
  - **Falloff**: 0.3
  - **Update Hz**: 10
  - **Visibility Threshold**: 0.5
  - **Reveal Kinds**: `PlayerLocal | PlayerRemote` 만 체크

### B-5. MarkerOverlay 들에 fog 참조
- `MarkerOverlay_Small` 의 `MinimapMarkerOverlay` 인스펙터 → `Fog` 슬롯에 `MinimapFog` 드래그
- `MarkerOverlay_Big` 동일

### B-6. prefab Apply
- `MinimapRig.prefab` 편집 모드 종료 → 자동 저장. 또는 씬 instance 의 Overrides ▾ → Apply All

---

## Phase C — 검증

### C-1. 기본 fog 동작
1. `MAP_1F_1R_khi.unity` 열기 → Play
2. **Expected**: 미니맵 전체 검정 + 플레이어 위치 주위 원형 reveal
3. 플레이어 이동 → reveal 영역 trail 확장
4. 본 곳 다시 안 어두워짐 (영구)

### C-2. 적/마커 fog 가시성
5. 적이 reveal 영역 밖 → 빨강 마커 안 보임
6. 적 다가가서 reveal 영역 안 → 마커 보임
7. 적이 reveal 된 영역에 있는 한 마커 보임 (영구 reveal 이라)
8. 보스 동일 — 보스방 입장 reveal 후 주황 마커
9. **PlayerLocal 마커 항상 보임** (fog 한가운데서도)

### C-3. 토글 + reset
10. M 키 큰 맵 → 같은 fog 패턴
11. 씬 재진입 → fog 검정으로 reset

### C-4. CL-226 호환 검증 (선택, CL-226 작업 시점)
12. `FitMode = FollowTarget` 변경 → 카메라가 플레이어 따라가도 fog reveal 정상 (world space 라 fit 변해도 의미 유지)
13. `Fog_Small.uvRect` 가 매 프레임 카메라 위치 따라 갱신되는지 (Frame Debugger 또는 인스펙터 watch)

### C-5. 성능
14. Profiler → `MinimapFog.Update` 약 10Hz, 1프레임 비용 < 1ms
15. 던전 절차생성 환경 (`Dungeon.unity`) 에서 fog 정상

---

## 변경 파일

### 신규
| 파일 | 책임 |
|---|---|
| `Assets/_Project/Scripts/Runtime/UI/Minimap/MinimapFog.cs` | world space fog 마스크, 10Hz throttle, RawImage uvRect 동기화, `IsRevealedAtWorld()` 공개 |

### 수정
| 파일 | 변경 |
|---|---|
| `MinimapCameraRig.cs` | public getter 4개 추가 (ManualCenter, ManualSize, GetCurrentCameraCenter, GetCurrentCameraOrthographicSize) |
| `MinimapMarkerOverlay.cs` | (1) `[SerializeField] MinimapFog fog` 추가, (2) fog 가시성 검사 분기, (3) icon null skip 결함 정리 — `agent.Icon ?? markerPrefab.sprite` fallback |
| (선택) `MinimapHUD.cs` | `[SerializeField] MinimapFog fog` Editor wiring 슬롯 — 직접 동작 변경 X |

### Prefab 수정
- `MinimapRig.prefab`:
  - `Fog_Small` (UI > Raw Image) 신규 — `SmallMapImage` 자식
  - `Fog_Big` (UI > Raw Image) 신규 — `BigMapImage` 자식
  - `MinimapFog` 컴포넌트 신규 — `MinimapHUD` GameObject 에
  - `MarkerOverlay_Small/Big` 의 `fog` 슬롯 wiring

---

## 작업 순서 + 의존성

```
A-2 (CameraRig getter)
  └─ A-1 (MinimapFog.cs) ─┐
A-3 (MarkerOverlay)        │
  ※ icon null 결함 정리 ─┘
                           ↓
                       B-1~B-6 (Editor)
                           ↓
                       C-1~C-5 (검증)
```

A 단계 3개 파일은 서로 의존. A-2 먼저 (CameraRig getter), 그 후 A-1 (MinimapFog), A-3 (MarkerOverlay) 병렬 가능.

---

## 후속 CL 영향

| CL | 영향 |
|---|---|
| **CL-224 멀티 통합** | reveal Kinds 가 `PlayerLocal\|PlayerRemote` 라 자동 공유 fog. NetworkBehaviour 변환 시 PlayerRemote kind 부여만 하면 끝. 본 CL 코드 변경 X |
| **CL-226 추적 모드** | world space fog 라 fit 변경에 안전. `SyncFogImageUVs()` 가 매 프레임 카메라 위치 따라 uvRect 갱신. 추가 작업 X |
| AutoFromBounds 후속 | bounds 변하면 manualCenter/manualSize 동기화 필요 → MinimapFog 가 자동 follow 라 OK. 단 fog 영역 자체가 변하면 이미 본 reveal 정보 손실. 그 시점에 reset 정책 결정 |

---

## 알려진 이슈 (보류)

- **RawImage uvRect 음수/1초과 가능**: 카메라가 fog 영역 밖으로 나가면 uvRect 가 [0,1] 범위 벗어남. RawImage 의 `Wrap Mode = Clamp` 면 가장자리 픽셀 stretch 됨. fog 영역을 던전보다 충분히 크게 잡으면 발생 X
- **fog 마스크 Texture2D 의 `Apply(false)` 비용** — 256² 기준 약 0.3ms. 10Hz 라 무시 가능. 1Hz 까지 낮춰도 OK
- **마커 깜빡임 가능성** — fog 가 10Hz 갱신인데 MarkerOverlay 는 매 프레임. fog 갱신 직전에 마커가 reveal 경계에 있으면 잠깐 깜빡일 수 있음. visibilityThreshold 0.5 + falloff 부드러움이면 체감 거의 없음
