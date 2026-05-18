---
name: 미니맵 방문 안 한 방 회색 표시 (3단계 visibility)
status: planned
related: MinimapFog, MinimapRoomReveal, MinimapMarkerOverlay
---

# Context

현재 미니맵은 2단계 fog-of-war:
- 가본 방 → 완전 노출 (alpha=0)
- 안 가본 방 + 외부(벽/공백) → 검정 (alpha=255), 둘 다 똑같이 안 보임

문제: 플레이어가 **어느 방이 존재하는지 / 어디로 가야 하는지** 구분이 안 됨. 안 가본 방과 단순 벽이 시각적으로 동일.

목표 3단계로 변경:
- **가본 방** → 완전 노출 (alpha=0, 기존 그대로)
- **안 가본 방** → **회색 dim** (alpha=128 중간값, 신규)
- **외부 벽/공백** → 검정 (alpha=255, 기존 그대로)

마커(적/아이템/포탈 아이콘)는 회색 방에서도 **숨김 유지** — 현 [`MinimapMarkerOverlay`](../../LostMemory/Assets/_Project/Scripts/Runtime/UI/Minimap/MinimapMarkerOverlay.cs) 의 `visibilityThreshold=0.5` 가 alpha<127.5 일 때만 마커 노출이라 alpha=128 이면 자동 숨겨짐 (별도 작업 X).

# 핵심 사실 (탐색 결과)

- [`MinimapFog._maskBuffer`](../../LostMemory/Assets/_Project/Scripts/Runtime/UI/Minimap/MinimapFog.cs:66) 는 `Color32[]` — 픽셀당 alpha 0~255 임의값 OK. 텍스처는 검정 RGB + alpha 합성이라 **alpha=128 ⇒ 화면상 회색**으로 그려짐.
- [`MinimapFog.RevealBounds()`](../../LostMemory/Assets/_Project/Scripts/Runtime/UI/Minimap/MinimapFog.cs:116) 의 픽셀 쓰기 조건 `if (_maskBuffer[idx].a > 0)` 가 "더 밝게만 갱신" 의미 — **race-free**: 이미 reveal 된 방에 회색 pre-fill 이 와도 덮어쓰지 않음.
- [`MinimapRoomReveal`](../../LostMemory/Assets/_Project/Scripts/Runtime/UI/Minimap/MinimapRoomReveal.cs) 는 각 방 prefab 의 trigger Collider 에 부착되어 있고 `boundsSource.bounds` 로 방 영역을 제공 — pre-fill 정보 소스로 재사용 가능.
- `MinimapFog.OnEnable()` 이 `ResetMask()` → alpha 전체 255 로 초기화. Pre-fill 은 그 다음에 실행돼야 안전.

# 변경 대상

## 1. [`MinimapFog.cs`](../../LostMemory/Assets/_Project/Scripts/Runtime/UI/Minimap/MinimapFog.cs)

### 1-1. `RevealBounds` 에 `targetAlpha` 파라미터 추가 (default=0, 기존 호출자 무영향)

```csharp
public void RevealBounds(Bounds worldBounds, byte targetAlpha = 0)
{
    // ...기존 좌표 변환 동일...

    bool anyChanged = false;
    for (int y = yMin; y <= yMax; y++)
    {
        int rowBase = y * maskResolution;
        for (int x = xMin; x <= xMax; x++)
        {
            int idx = rowBase + x;
            if (_maskBuffer[idx].a > targetAlpha)   // ← 0 → targetAlpha 로 변경
            {
                _maskBuffer[idx].a = targetAlpha;   // ← 0 → targetAlpha 로 변경
                anyChanged = true;
            }
        }
    }
    // ...기존 적용 동일...
}
```

기존 호출 `RevealBounds(bounds)` 는 default=0 사용해서 완전 reveal — 호환 유지.

### 1-2. 새 SerializeField + 공개 프로퍼티

```csharp
[Header("Unvisited Tint")]
[SerializeField, Range(0, 255),
 Tooltip("안 가본 방의 fog alpha. 0=완전 노출, 255=완전 검정. 128 권장(회색). " +
         "visibilityThreshold(0.5) 보다 높으면 그 방의 마커는 자동 숨김.")]
private byte unvisitedRoomAlpha = 128;

public byte UnvisitedRoomAlpha => unvisitedRoomAlpha;
```

밸런싱은 인스펙터에서 조정 (밝게 = 낮은 값, 어둡게 = 높은 값).

## 2. [`MinimapRoomReveal.cs`](../../LostMemory/Assets/_Project/Scripts/Runtime/UI/Minimap/MinimapRoomReveal.cs)

`Start()` 추가 — scene load 시 (또는 동적 스폰 시) **자신의 영역을 회색 pre-fill**:

```csharp
private void Start()
{
    if (_revealed) return;  // 이미 트리거된 방은 스킵

    MinimapFog targetFog = ResolveFog();
    if (targetFog == null || boundsSource == null) return;

    byte gray = targetFog.UnvisitedRoomAlpha;
    if (gray < 255)  // 비활성(255) 이면 skip
    {
        targetFog.RevealBounds(boundsSource.bounds, gray);
    }
}
```

`OnTriggerEnter2D` (line 41) 는 그대로 — 진입 시 `RevealBounds(bounds)` 호출하면 default=0 으로 alpha=128(회색) 덮어쓰고 완전 노출됨. `if (current > targetAlpha)` 조건이라 이미 0 인 픽셀은 무비용.

**타이밍 안전성**:
- Unity 실행 순서 보장: `MinimapFog.OnEnable() → ResetMask()` 가 모든 GameObject 의 `Start()` 보다 먼저 실행 (Awake/OnEnable → Start 순서)
- 동적으로 늦게 스폰되는 방도 자기 `Start()` 시점에 자동으로 회색 pre-fill — 별도 hook 불필요

## 3. (작업 없음) [`MinimapMarkerOverlay`](../../LostMemory/Assets/_Project/Scripts/Runtime/UI/Minimap/MinimapMarkerOverlay.cs)

현 `visibilityThreshold=0.5` 그대로 → `alpha<127.5` 만 revealed 처리. alpha=128 회색 방의 마커는 자동 숨김. 별도 코드 변경 없음.

# Verification

1. **빌드 확인**: Unity Editor 컴파일 에러 없음
2. **신규 동작 검증** (1F 일반 스테이지에서):
   - 게임 시작 직후 미니맵 확인 → 모든 방이 **회색** 으로 표시되어 보임 (외부/벽은 검정 유지)
   - 첫 번째 방 진입 → 그 방만 **완전 밝게** 변함, 다른 방은 회색 유지
   - 두 번째 방 진입 → 그 방도 밝게, 첫 번째 방은 밝게 유지 (영구 reveal)
   - 회색 방에 적/아이템 마커 **안 보임** (visited 방의 마커만 보임)
3. **회귀 테스트**:
   - 보스 룸 진입 시 fog reveal 정상 동작
   - 멀티플레이 시 다른 플레이어의 reveal 영향 없음 (PlayerLocal 만 트리거)
4. **인스펙터 튜닝**:
   - `MinimapFog.unvisitedRoomAlpha` 를 100 (밝은 회색) ~ 180 (어두운 회색) 범위에서 가시성 조정
   - 255 로 설정하면 기존 2단계 동작으로 fallback (회색 표시 비활성)

# Out of scope

- 마커 표시 정책 변경 (회색 방에서도 마커 노출 등) — 별도 task
- 미니맵 카메라 / UI 레이아웃 변경
- 가본 방의 적 마커가 시간 지나면 stale 되는 등의 동작
