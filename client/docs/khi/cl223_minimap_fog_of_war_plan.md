# CL-223: 미니맵 Fog of War

**Epic**: H. UI / 연출 / 아트 적용
**상태**: 코드 ⏳ 대기 / Editor 작업 ⏳ 대기 / 검증 ⏳ 대기
**선행**: CL-222 (미니맵 카메라 + HUD + 마커 — 코드 완료, Editor/검증 진행 중)
**후속**: CL-224 (멀티플레이 통합)

---

## Context

CL-222 의 미니맵은 던전 전체가 처음부터 보임. 본 CL: **플레이어가 본 영역만 점진적으로 밝히는 fog of war** 추가 → 탐색 동기 + "발견" 재미.

**핵심 결정 — 단순 원형 reveal (벽 LOS 무시)**:
플레이어 위치 기준 반경 N 안의 모든 타일을 reveal. 벽 너머도 보임. Risk of Rain 2 / 디아블로 / Vampire Survivors 등 코옵 액션 던전 표준. DA `GridFlowMinimap` 의 BFS 기반 LOS 보다 단순/빠름.

**Compositing — 별도 RawImage 알파 블렌드 (셰이더 불필요)**:
`Texture2D` fog 마스크를 RawImage 한 장으로 미니맵 위에 덧대고 표준 알파 블렌드. fog 픽셀 alpha=1 → 검정으로 가림, alpha=0 → 투명해서 미니맵 보임. 머티리얼/셰이더 추가 작업 0.

**적도 fog 가림**:
적/보스/포탈/픽업이 처음엔 안 보이다가 시야 들어와야 보임. Player(Local/Remote)만 항상 보임. CL-222 의 `MinimapMarkerOverlay` 가 fog 참조해서 가린 영역 마커는 안 그리도록 patch.

---

## 시스템 사실

- **CL-222 산출물**:
  - `MinimapCanvas` 안 `SmallMapImage` / `BigMapImage` (RawImage) — 같은 `MinimapRT` 참조
  - 각각 자식 `MarkerOverlay_Small` / `MarkerOverlay_Big` (`MinimapMarkerOverlay` 컴포넌트)
  - `MinimapAgent.All` — 활성 agent registry, `Kind` enum 보유 (PlayerLocal/PlayerRemote/Enemy/Boss/Pickup/Portal)
  - `MinimapCameraRig` — orthographic 카메라, Manual fit (`manualCenter` + `manualSize`)
- **2D 프로젝트**: 던전/스프라이트 z≈0, 메인 카메라 z=-10
- **Fit Mode 1차 = Manual**: 인스펙터 값 변경 외에는 카메라 fit 안 변함 → fog 마스크 viewport space 안전
- **씬 전환 시 컴포넌트 재생성**: `OnEnable` 에서 fog 초기화하면 새 run 마다 자동 reset

---

## 결정 사항 (사용자 확정)

| 결정 | 값 | 근거 |
|---|---|---|
| LOS 정책 | **단순 원형 (LOS 무시)** | 코옵 액션 던전 표준. 벽 너머도 reveal — 탐색 힌트 제공 |
| 마스크 해상도 | **256×256** | RT 와 동일. 가벼움. 거칠면 후속에 ↑ |
| Reveal 반경 | **5 world unit** (인스펙터 노출) | Manual fit size 20 가정 시 1/4 시야 |
| Falloff | **0.3** (인스펙터 노출) | 가장자리 부드러움 표준 UX |
| Update throttle | **10Hz** | SetPixels 비용 절감. 충분히 부드러움 |
| Fog 색상 | **검정** (인스펙터 노출) | 디자이너가 톤 조정 가능 |
| 합성 방식 | **별도 RawImage 알파 블렌드** | 셰이더/머티리얼 X. RawImage + Texture2D 표준 |
| Reveal source | **Kind=PlayerLocal\|PlayerRemote** | 적·보스·포탈·픽업은 reveal 안 함 |
| Fog 초기화 시점 | **`OnEnable`** | 단순. 씬 전환 시 자동 reset |
| 적 외 마커 fog 가시성 | **Player 외 모두 fog 가림** | 발견 재미. PlayerLocal/Remote 만 예외 |
| Fog 단계 | **2단계** (안 본 검정 / 본 영구 밝음) | 단순. 3단계는 후속 |
| Fog 좌표계 | **viewport space** (Manual fit 가정) | 1차 단순. AutoFromBounds 도입 시 마스크 무효화 후 재시작 |
| 가려진 마커 처리 | **binary** (표시/숨김) | 1차 단순. alpha 페이드는 후속 |

---

## 작업 범위

### Phase A — 코드 (⏳ 대기)

- [ ] `MinimapFog.cs` 신규 — fog Texture2D + reveal 로직 + 10Hz throttle
- [ ] `MinimapMarkerOverlay.cs` 수정 — `fog` 참조 + agent 위치 fog 샘플링 → 임계값 초과 시 마커 skip
- [ ] `MinimapHUD.cs` 수정 (선택) — fog 컴포넌트 ref 노출 (인스펙터 wiring 편의)

### Phase B — Editor 작업 (⏳ 사용자 / khi)

- [ ] `MinimapCanvas` 안 `SmallMapImage` 자식으로 `Fog_Small` (RawImage) 추가
- [ ] `MinimapCanvas` 안 `BigMapImage` 자식으로 `Fog_Big` (RawImage) 추가
- [ ] `MinimapHUD` GameObject (또는 별도) 에 `MinimapFog` 컴포넌트 부착
- [ ] 인스펙터 wiring: fog texture 가 두 RawImage 에 자동 바인딩
- [ ] `MarkerOverlay_Small` / `MarkerOverlay_Big` 인스펙터의 `fog` 슬롯에 MinimapFog 참조

### Phase C — 검증 (⏳ 사용자 / khi)

- [ ] 던전 입장 → 미니맵 전체 검정 + 플레이어 주위 원형 reveal
- [ ] 플레이어 이동 → reveal 영역 trail 확장
- [ ] 영구 reveal 확인 — 본 곳 다시 안 어두워짐
- [ ] 적이 fog 안에선 안 보임, reveal 영역 안에선 보임
- [ ] 보스/포탈/픽업도 동일 (fog 가림)
- [ ] PlayerLocal 마커 항상 보임 (자기 자신)
- [ ] M 키 큰 맵 — 같은 fog 적용
- [ ] 씬 재진입 / 새 run → fog 검정으로 reset
- [ ] Profiler 확인 — `MinimapFog` Update 비용 문제없음 (10Hz throttle 잘 작동하는지)

### 작업 외 (Out of scope)

- 멀티플레이 통합 (CL-224) — 본 CL 코드는 "Player* Kind agent 는 모두 reveal source" 로 짜서 CL-224 추가 작업 0
- 3단계 fog (currently visible vs explored) — 후속
- alpha 페이드 마커 (가린 적이 부드럽게 사라짐) — 후속
- World space fog 좌표계 — 후속 (AutoFromBounds 도입 시)
- LOS 적용 (벽 막힘) — 후속 또는 영구 미적용
- fog 외곽 그라데이션, fog 색상 패턴/노이즈 — 디자이너 폴리시 후속

---

## 변경 파일

### 신규

| 파일 | 책임 |
|---|---|
| `Assets/_Project/Scripts/Runtime/UI/Minimap/MinimapFog.cs` | Texture2D 마스크 보유, 10Hz throttle, Player* Kind agent 위치 reveal, RawImage 자동 바인딩 |

### 수정

| 파일 | 변경 |
|---|---|
| [MinimapMarkerOverlay.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/UI/Minimap/MinimapMarkerOverlay.cs) | `[SerializeField] MinimapFog fog` 추가. `LateUpdate` 에 agent 위치 fog 샘플링 가드 — Player* Kind 면 항상 그리고, 그 외엔 fog alpha < 임계값일 때만 그림 |
| [MinimapHUD.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/UI/Minimap/MinimapHUD.cs) | (선택) `[SerializeField] MinimapFog fog` 추가 — Editor wiring 편의용. 직접 동작 변경 X |

---

## 코드 spec

### MinimapFog.cs (신규)

```csharp
namespace LostMemory.UI.Minimap;

[DisallowMultipleComponent]
[AddComponentMenu("Lost Memory/UI/Minimap/Minimap Fog")]
public sealed class MinimapFog : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Camera minimapCamera;             // viewport 변환용
    [SerializeField] RawImage[] fogImages;             // SmallMap / BigMap 위에 덧댄 RawImage 배열

    [Header("Mask")]
    [SerializeField, Min(64)] int maskResolution = 256;
    [SerializeField] Color fogColor = Color.black;

    [Header("Reveal")]
    [SerializeField, Min(0.1f)] float revealRadius = 5f;          // world unit
    [SerializeField, Range(0f, 1f)] float falloff = 0.3f;          // 0=단단한 원, 1=완전 그라데이션
    [SerializeField, Range(1f, 30f)] float updateHz = 10f;
    [SerializeField, Range(0f, 1f)] float visibilityThreshold = 0.5f;  // marker 가시성 판정용

    [Header("Filter")]
    [SerializeField, Tooltip("이 Kind 들의 agent 위치 주변을 reveal.")]
    AgentKindMask revealKinds = AgentKindMask.PlayerLocal | AgentKindMask.PlayerRemote;

    private Texture2D _mask;
    private Color32[] _maskBuffer;
    private float _accumulator;

    public Texture2D MaskTexture => _mask;
    public float VisibilityThreshold => visibilityThreshold;

    private void OnEnable()
    {
        EnsureMask();
        ResetMask();
        BindToFogImages();
    }

    private void Update()
    {
        _accumulator += Time.deltaTime;
        float interval = 1f / Mathf.Max(updateHz, 0.1f);
        if (_accumulator < interval) return;
        _accumulator = 0f;

        UpdateReveal();
        _mask.SetPixels32(_maskBuffer);
        _mask.Apply(false);
    }

    public bool IsRevealedAtViewport(Vector2 viewportPos)
    {
        if (_mask == null) return true;
        int x = Mathf.Clamp((int)(viewportPos.x * maskResolution), 0, maskResolution - 1);
        int y = Mathf.Clamp((int)(viewportPos.y * maskResolution), 0, maskResolution - 1);
        Color32 c = _maskBuffer[y * maskResolution + x];
        // alpha=1 = 가림, alpha=0 = 보임. 임계값 = visibilityThreshold (예: 0.5)
        return c.a < (byte)(visibilityThreshold * 255);
    }

    public void ResetMask() { /* 모든 픽셀 (fogColor.r/g/b, 255) 로 초기화 */ }

    private void UpdateReveal() { /* MinimapAgent.All 순회, Kind 매치 시 reveal circle 페인트 */ }

    private void EnsureMask() { /* Texture2D 생성, FilterMode.Bilinear, wrapMode.Clamp */ }

    private void BindToFogImages() { /* fogImages 각각의 .texture = _mask, .color = Color.white */ }
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

**핵심 알고리즘 — UpdateReveal**:
```
foreach (agent in MinimapAgent.All where agent.Kind matches revealKinds)
    Vector3 vp = minimapCamera.WorldToViewportPoint(agent.WorldPosition)
    if (vp.z < 0) continue   // 카메라 뒤
    if (vp.x ∉ [0,1] or vp.y ∉ [0,1]) continue  // 화면 밖

    // 반경을 viewport 비율로 환산 (Manual fit 가정)
    float radiusVp = revealRadius / (camera.orthographicSize * 2f)
    int radiusPx = (int)(radiusVp * maskResolution)
    int cx = (int)(vp.x * maskResolution)
    int cy = (int)(vp.y * maskResolution)
    float falloffStart = 1f - falloff

    for (int y = cy - radiusPx; y <= cy + radiusPx; y++)
        for (int x = cx - radiusPx; x <= cx + radiusPx; x++)
            if (x ∉ [0,maskRes) or y ∉ [0,maskRes)) continue
            float d = sqrt((x-cx)² + (y-cy)²) / radiusPx   // 0..1
            if (d > 1) continue
            float reveal = (d <= falloffStart) ? 1f : 1f - (d - falloffStart) / falloff
            // 영구 reveal: 더 밝은 값으로만 갱신 (alpha 더 낮은 값으로만)
            int idx = y * maskResolution + x
            byte newAlpha = (byte)((1f - reveal) * 255)
            if (newAlpha < _maskBuffer[idx].a) _maskBuffer[idx].a = newAlpha
```

**비용 분석 (256² 마스크, 반경 5 world / size 20 → radiusVp=0.125 → radiusPx=32)**:
- 페인트 영역: 64×64 = 4,096 픽셀
- agent 1명 = 4,096 sqrt + 비교
- 10Hz × 1~2명 = ~80,000 op/s — 무시 가능
- `SetPixels32` + `Apply(false)` ~0.5ms (256² 텍스처 기준)

### MinimapMarkerOverlay.cs (수정)

```csharp
// 추가 필드
[SerializeField, Tooltip("fog 가시성 검사. null 이면 모든 마커 항상 표시 (CL-222 동작).")]
private MinimapFog fog;

// LateUpdate 안 agent 순회 분기
for (int i = 0; i < agents.Count; i++)
{
    MinimapAgent agent = agents[i];
    if (agent == null || agent.Icon == null) continue;

    Vector3 vp = minimapCamera.WorldToViewportPoint(agent.WorldPosition);
    bool offscreen = vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f || vp.z < 0f;

    if (offscreen)
    {
        if (!clampOffscreen) continue;
        vp.x = Mathf.Clamp01(vp.x);
        vp.y = Mathf.Clamp01(vp.y);
    }

    // ★ NEW — fog 가림 검사
    if (fog != null && !IsAlwaysVisible(agent.Kind))
    {
        if (!fog.IsRevealedAtViewport(new Vector2(vp.x, vp.y))) continue;
    }

    // ... 기존 마커 그리기 코드
}

private static bool IsAlwaysVisible(MinimapAgent.AgentKind kind)
    => kind == MinimapAgent.AgentKind.PlayerLocal
    || kind == MinimapAgent.AgentKind.PlayerRemote;
```

---

## Editor 작업 체크리스트 (Phase B 상세)

### 1. Fog RawImage — SmallMap
- `MinimapCanvas` 안 `SmallMap` 자식 `SmallMapImage` (RawImage) 의 자식으로 빈 GameObject `Fog_Small` 추가
- 컴포넌트: `Raw Image`
- RectTransform: Stretch (anchor 0,0 ~ 1,1, offset 0)
- Color: 흰 (1,1,1,1) — fog texture 색은 mask 자체에 들어감
- Texture: 빈 (런타임에 `MinimapFog` 가 자동 바인딩)
- Raycast Target: 끄기

### 2. Fog RawImage — BigMap
- `MinimapCanvas` 안 `BigMap` 자식 `BigMapImage` (RawImage) 의 자식으로 `Fog_Big` 추가
- 컴포넌트/설정 위와 동일

### 3. MinimapFog 컴포넌트
- `MinimapHUD` GameObject (또는 `MinimapCanvas` 루트, 또는 별도 GameObject) 에 `MinimapFog` 컴포넌트 추가
- 인스펙터:
  - Minimap Camera: `MinimapCamera` 의 Camera (CL-222 와 같은 것)
  - Fog Images: 배열 크기 2 → `Fog_Small.RawImage`, `Fog_Big.RawImage`
  - Mask Resolution: 256
  - Fog Color: 검정 (0, 0, 0, 1)
  - Reveal Radius: 5 (필요 시 던전 규모 따라 조정)
  - Falloff: 0.3
  - Update Hz: 10
  - Visibility Threshold: 0.5
  - Reveal Kinds: `PlayerLocal | PlayerRemote` 만 체크

### 4. MarkerOverlay 들에 fog 참조
- `MarkerOverlay_Small` 의 `MinimapMarkerOverlay` 인스펙터 → `Fog` 슬롯에 `MinimapFog` 컴포넌트 드래그
- `MarkerOverlay_Big` 도 동일

### 5. (선택) MinimapHUD 에 fog 참조
- 편의용 — MinimapHUD 인스펙터에서 fog 한 곳 reset 호출 등 확장 시. 기능적 필수 X

---

## 검증 방법 (Phase C 상세)

### 기본 동작
1. Play 모드 진입 → 던전 씬
2. **Expected**: 미니맵 전체 검정 (fog 가림) + 플레이어 위치 주위 원형으로 미니맵 보임
3. 플레이어 이동 → reveal 영역이 trail 처럼 확장
4. 본 곳으로 돌아가지 않고 다른 길 가도 본 곳은 영구 밝음
5. 본 곳 다시 지나가도 더 밝아지거나 어두워지지 않음 (이미 영구)

### 적/마커 fog 가시성
6. 적이 plus radius 밖에 있으면 마커 안 보임
7. 적에게 다가가서 reveal 영역 안에 들어오면 빨강 마커 보임
8. 적이 다시 fog 영역으로 멀어져도 마커 보임 (이미 reveal 된 영역) — 단, 적이 reveal 안 된 영역으로 이동하면 마커 다시 사라짐
9. 보스도 동일 (보스방 입장 + reveal 후 마커 보임)
10. 포탈/픽업도 fog 가림 (배치된 곳 fog 밝아져야 보임)
11. **PlayerLocal 마커는 항상 보임** (fog 한가운데도 자기 자신은 보여야 함)

### 토글
12. M 키 큰 맵 → 같은 fog 패턴 표시 (작은 맵과 동일)
13. M 키 다시 → 큰 맵 사라짐, 작은 맵 fog 유지

### Reset
14. 씬 재진입 또는 새 run 시작 → fog 다시 검정 (영구 reveal 정보 reset)

### 성능
15. Profiler → `MinimapFog.Update` 호출이 약 10Hz, 1프레임 비용 < 1ms 확인
16. SetPixels 영역 페인트가 매 프레임이 아니라 throttle 작동하는지

---

## 알려진 이슈 / 보류

- **AutoFromBounds 도입 시 마스크 무효화**: 1차는 Manual fit 고정 가정. 만약 후속 CL 에서 AutoFromBounds 도입하면 카메라 fit 변할 때 fog 마스크 무효화 → `MinimapFog.ResetMask()` 호출 필요. 본 CL 코드에 `public ResetMask()` 노출만 해두고 사용은 후속에서 wiring
- **viewport space 한계**: 카메라가 회전하거나 fit 이 동적으로 변하면 fog 픽셀 의미가 변함. 1차에선 카메라 회전 X + fit 정적 가정. world space 좌표계는 후속 마이그레이션 (마스크 인덱스 공식 1곳만 변경)
- **던전 plus radius 외**: viewport 밖 영역은 fog 마스크에 못 들어감 → 미니맵 카메라 fit 이 던전 전체를 안 덮으면 일부 던전이 영구 unreveal. Manual fit 인스펙터 값 잘 맞추는 게 디자이너 책임
- **카메라 RT aspect != 1 이면 reveal radius 가 X/Y 다를 수 있음**: 본 CL 1차는 256×256 정사각 RT 가정. 나중에 RT 비율 바꾸면 `radiusVp` 를 X/Y 따로 계산하도록 보강
- **fog 마스크가 Bilinear filter 일 때 가장자리 부드럽게 보임 — 의도**: falloff 와 자연스럽게 결합. Point filter 면 픽셀 경계가 보임
- **Texture2D readback 비용**: `IsRevealedAtViewport` 가 매 프레임 마커별로 호출됨 (수십 회). `_maskBuffer` 직접 인덱싱이라 비싸지 않음 (`Texture2D.GetPixel` 호출 X)
- **PlayerRemote 가 fog reveal 까지 source**: CL-224 멀티 통합 시 자동으로 공유 fog 됨. 본 CL 코드 변경 X — 구조만 미리 준비

---

## 후속 CL

| CL | 내용 |
|---|---|
| **CL-224** | 멀티플레이 통합 — 팀원 마커 색 분리. 공유 fog 는 본 CL 의 reveal Kinds 설정으로 자동 작동 |
| 별도 후속 | 3단계 fog (현재 시야 vs 영구 reveal), alpha 페이드 마커, World space fog 좌표계, LOS 적용 (BFS or raycast), fog 색상/노이즈 패턴 폴리시 |
