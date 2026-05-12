# CL-226: 미니맵 추적 (FollowTarget) 모드

**Epic**: H. UI / 연출 / 아트 적용
**상태**: plan ⏳ 검토 / 코드 ⏳ / Editor ⏳ / 검증 ⏳
**선행**: [CL-222](./cl222_minimap_camera_hud_plan.md) (코드 완료, Phase B/C 진행 중)
**관련**: CL-223 (Fog of War), CL-224 (멀티플레이 통합) — 독립이라 순서 자유

---

## Context

CL-222 에서 미니맵 카메라가 **Manual 모드** (고정 좌표 + 고정 크기) 로 작동 중. 사용자가 검증 중 발견:

> "맵 밖으로 나가도 미니맵 움직임이 없음"

이는 plan 결정 ("Manual 1차, 자동화는 후속") 대로 작동하는 것이지만, **방마다 `Manual Center` 를 수동 설정해야 하고 절차생성 던전에서는 사실상 사용 불가**. 추적(FollowTarget) 모드가 게임 체감에 더 자연스러움 (디아블로/스타듀밸리 스타일).

본 CL: `MinimapCameraRig` 에 **`FollowTarget`** FitMode 추가. 카메라가 매 프레임 PlayerLocal 의 위치를 부드럽게 따라감.

### CL-222 와의 관계
- CL-222 의 코드/prefab/씬 셋업 전부 재사용
- `MinimapCameraRig.cs` 만 수정 (확장)
- 다른 컴포넌트 (`MinimapAgent`, `MinimapMarkerOverlay`, `MinimapHUD`) 변경 없음

---

## 결정 사항 (사용자 확정 2026-05-12)

| # | 결정 | 값 | 근거 |
|---|---|---|---|
| 1 | Follow 대상 결정 | **자동 탐색** — `MinimapAgent.All` 에서 `Kind == PlayerLocal` 인 첫 번째 agent 의 transform | 인스펙터 슬롯 수동 연결 불필요. 풀링 안전 (OnEnable 시 자동 등록). CL-224 멀티 통합 시 `IsOwner` 가 PlayerLocal kind 를 만들면 자연 호환 |
| 2 | 카메라 크기 | **`manualSize` 재사용** | 추가 필드 없이 단순. 추적 시야 별도 조정 필요해지면 후속에서 `followViewSize` 분리 |
| 3 | 추적 방식 | **부드러운 (Lerp + damping)** | 디아블로/탑다운 표준. 즉시 추적은 카메라가 떨려 보임. 마커 1프레임 지연 가능하나 시각적 무시 수준 |
| 4 | 모드 전환 UX | **`FitMode` 인스펙터 드롭다운만** | 디자이너가 씬/prefab override 로 선택. 런타임 키 토글 (Shift+M 등) 은 후속 폴리시 작업 |
| 5 | 멀티플레이 사전 고려 | **자동 탐색 방식이 자연 정합** | CL-224 에서 `IsOwner` 인 player 만 `Kind=PlayerLocal` 로 설정하면 각 클라가 자기 player 따라감. 본 CL 에선 single 만 검증 |

---

## 시스템 사실

- **`MinimapCameraRig.FitMode`** 현재: `Manual`, `AutoFromBounds` 두 모드. enum 에 `FollowTarget` 항목 추가 필요 ([MinimapCameraRig.cs:19-23](../../LostMemory/Assets/_Project/Scripts/Runtime/UI/Minimap/MinimapCameraRig.cs))
- **`ApplyFit()` 호출 시점**: 현재 `Awake()` + `OnValidate()` 에서만. **추적은 매 프레임 갱신 필요** → `LateUpdate()` 추가 ([MinimapCameraRig.cs:52-64](../../LostMemory/Assets/_Project/Scripts/Runtime/UI/Minimap/MinimapCameraRig.cs))
- **`MinimapAgent.All`**: 활성 agent static List. `Kind` getter 노출됨 → follow 대상 탐색 가능 ([MinimapAgent.cs:42-44](../../LostMemory/Assets/_Project/Scripts/Runtime/UI/Minimap/MinimapAgent.cs))
- **2D XY 평면**: 카메라는 `(target.x, target.y, cameraZ)` 만 추적. Z 는 `cameraZ` 그대로
- **MarkerOverlay LateUpdate 순서**: `MinimapMarkerOverlay.LateUpdate` 와 `MinimapCameraRig.LateUpdate` 둘 다 LateUpdate. 카메라가 먼저 위치 갱신 후 overlay 가 `WorldToViewportPoint` 호출해야 마커 위치 정확
  - 해결: `[DefaultExecutionOrder(-100)]` 로 `MinimapCameraRig` 가 먼저 실행되도록 명시

---

## 작업 범위

### Phase A — 코드 변경 (단일 파일)

- [ ] `MinimapCameraRig.cs` 수정:
  - `FitMode` enum 에 `FollowTarget` 추가
  - `[DefaultExecutionOrder(-100)]` 어트리뷰트 (overlay 보다 먼저 LateUpdate)
  - `followDamping` (Lerp 강도) 필드 추가
  - `LateUpdate()` 추가 — FollowTarget 모드면 target 위치로 부드럽게 이동
  - `ApplyFit()` 의 `FollowTarget` 분기 추가 — 카메라 size 는 `manualSize` 그대로 적용
  - private `FindFollowTarget()` 헬퍼 — `MinimapAgent.All` 중 `Kind==PlayerLocal` 첫 번째 transform 반환

### Phase B — Editor 작업

- [ ] `MinimapRig.prefab` 의 `MinimapCameraRig` 인스펙터:
  - **Fit Mode**: `FollowTarget` 으로 변경
  - **Manual Size**: 추적 시 시야 크기 (예: `15`) — 적당히 좁게 잡아야 추적감 살아남
  - **Follow Damping**: `5` (기본) — 더 크면 빠른 추적, 작으면 느릿
- [ ] 변경 후 prefab Apply (`Overrides ▾ → Apply All`)

### Phase C — 검증 (single)

- [ ] `MAP_1F_1R_khi.unity` Play → 플레이어 이동 시 미니맵이 부드럽게 따라옴
- [ ] 플레이어 정지 → 카메라도 즉시 정지 (over-shoot 없음)
- [ ] 빠르게 방향 전환 → 카메라 부드럽게 따라옴 (떨림 없음)
- [ ] BigMap (M키) 토글 → 큰 맵도 동일하게 추적 (같은 RT 이므로 자동)
- [ ] **Fit Mode 를 `Manual` 로 잠시 변경 → 카메라 고정** (기존 동작 보존 검증)
- [ ] 메인 `Dungeon.unity` 절차생성 던전에서 검증 — 모든 방 자유 이동 + 카메라 추적 정상

### 작업 외 (Out of scope)

- 런타임 키 토글 (Shift+M 등으로 Manual ↔ Follow 전환) — 후속 폴리시
- `followViewSize` 별도 필드 — 결정 2 에 따라 본 CL 에서 안 만듦
- 멀티플레이 분기 (`IsOwner` 기반 follow 대상) — CL-224 에서
- AutoFromBounds 와 FollowTarget 동시 사용 (예: 방에선 follow, 큰 맵 열면 auto) — 후속
- 카메라 추적 시 회전 (top-down 회전 미니맵) — 본 게임 2D 라 불필요

---

## 변경 파일

### 수정
| 파일 | 변경 내용 |
|---|---|
| [MinimapCameraRig.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/UI/Minimap/MinimapCameraRig.cs) | `FollowTarget` 모드 추가, `LateUpdate`, `followDamping` 필드, `FindFollowTarget()` 헬퍼 |

### 신규
없음.

### Prefab 수정
- `MinimapRig.prefab` 의 `MinimapCameraRig` 인스펙터 값 (Fit Mode = FollowTarget)

---

## 코드 spec (Phase A 상세)

### MinimapCameraRig 추가/변경 사항

```csharp
namespace LostMemory.UI.Minimap;

[DisallowMultipleComponent]
[AddComponentMenu("Lost Memory/UI/Minimap/Minimap Camera Rig")]
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(-100)]   // ← 추가: MinimapMarkerOverlay 보다 먼저 LateUpdate
public sealed class MinimapCameraRig : MonoBehaviour
{
    public enum FitMode
    {
        Manual,
        AutoFromBounds,
        FollowTarget               // ← 추가
    }

    // ... 기존 필드 그대로 ...

    [Header("Follow")]
    [SerializeField, Min(0.1f), Tooltip("Lerp 강도. 클수록 빠른 추적. 5 정도 권장.")]
    private float followDamping = 5f;

    private Transform _cachedFollowTarget;   // ← 매 프레임 탐색 비용 회피용

    // 기존 Awake / OnValidate / RefreshFit / SetAutoBounds / SetFitMode 그대로

    private void LateUpdate()            // ← 신규
    {
        if (fitMode != FitMode.FollowTarget || _camera == null)
        {
            return;
        }

        Transform target = GetFollowTarget();
        if (target == null)
        {
            return;
        }

        Vector3 current = transform.position;
        Vector3 desired = new Vector3(target.position.x, target.position.y, cameraZ);

        // damping 기반 부드러운 추적. Time.deltaTime * damping 가 1.0 을 넘으면 사실상 즉시.
        float t = Mathf.Clamp01(Time.deltaTime * followDamping);
        transform.position = Vector3.Lerp(current, desired, t);
    }

    private Transform GetFollowTarget()        // ← 신규
    {
        if (_cachedFollowTarget != null && _cachedFollowTarget.gameObject.activeInHierarchy)
        {
            return _cachedFollowTarget;
        }

        // MinimapAgent.All 에서 PlayerLocal 첫 번째 찾기
        var agents = MinimapAgent.All;
        for (int i = 0; i < agents.Count; i++)
        {
            if (agents[i] != null && agents[i].Kind == MinimapAgent.AgentKind.PlayerLocal)
            {
                _cachedFollowTarget = agents[i].transform;
                return _cachedFollowTarget;
            }
        }

        return null;
    }

    private void ApplyFit()
    {
        // ... 기존 로직 그대로 ...
        // FollowTarget 분기:
        else if (fitMode == FitMode.FollowTarget)
        {
            // 카메라 size 는 manualSize 그대로 사용 (결정 2)
            // 위치는 LateUpdate 가 매 프레임 갱신하므로 여기선 손대지 않음
            orthographicSize = manualSize;
            // 초기 위치 — target 이 이미 있으면 그 위치, 없으면 manualCenter
            Transform target = GetFollowTarget();
            if (target != null)
            {
                center = new Vector3(target.position.x, target.position.y, 0f);
            }
            else
            {
                center = new Vector3(manualCenter.x, manualCenter.y, 0f);
            }
        }
        // ... 기존 transform.position / _camera.orthographicSize 적용 ...
    }
}
```

### 변경 요약 (라인 단위)

| 위치 | 변경 |
|---|---|
| class attribute | `[DefaultExecutionOrder(-100)]` 추가 |
| `FitMode` enum | `FollowTarget` 항목 추가 |
| 필드 | `followDamping` 추가, `_cachedFollowTarget` (private) 추가 |
| 메서드 | `LateUpdate()` 신규, `GetFollowTarget()` 신규 |
| `ApplyFit()` | `FollowTarget` 분기 추가 (manualSize 적용 + 초기 위치 target 사용) |

**기존 동작 보존**: `Manual` / `AutoFromBounds` 분기는 변경 없음. FitMode 가 `FollowTarget` 일 때만 새 로직 동작.

---

## 검증 방법 (Phase C 상세)

### C-1. 기본 추적 동작
1. `MAP_1F_1R_khi.unity` 열기
2. `MinimapRig > MinimapCamera` 의 `MinimapCameraRig` 인스펙터:
   - Fit Mode: `FollowTarget`
   - Manual Size: `15` (방 1개가 화면에 적당히 보이는 크기)
   - Follow Damping: `5`
3. Play
4. **Expected**: 플레이어 마커가 미니맵 중앙에 머물고, 배경 (던전) 이 플레이어 움직임 반대로 흘러감
5. 빠른 방향 전환 → 부드러운 추적 (over-shoot 없음, 떨림 없음)
6. 정지 → 1초 안에 카메라도 정지

### C-2. Damping 튜닝
7. Follow Damping 을 `1` (느리게), `10` (빠르게) 로 바꿔보며 체감
8. 디자이너 선호도에 따라 prefab 의 기본값 확정

### C-3. Manual 모드 호환성 검증
9. Fit Mode 를 `Manual` 로 변경 → 카메라 즉시 정지, 기존 plan 대로 고정 시야
10. 다시 `FollowTarget` → 추적 재개

### C-4. PlayerLocal 미부착 환경
11. 씬에 `MinimapAgent.Kind=PlayerLocal` 인 GameObject 없는 상태로 Play
12. **Expected**: 카메라가 `manualCenter` 위치에 머묾 (target 없으면 ApplyFit 의 fallback). 에러 없음

### C-5. BigMap 통합
13. Play 중 M키 → 큰 맵도 동일하게 추적 (같은 RT 라 자동)
14. M키 다시 → 코너 HUD 로 돌아감

### C-6. 메인 던전 절차생성 (가장 중요)
15. `Dungeon.unity` Play → 절차생성 던전 입장
16. 모든 방 자유 이동 시 미니맵이 항상 플레이어 중심
17. 풀링 적이 시야에 들어왔다 나갔다 시 마커 정상 (CL-222 검증과 동일하지만 추적 환경에서 재검증)

### C-7. 마커 정확도
18. 빠르게 이동 중 마커가 카메라 위치와 어긋나는지 확인 (`DefaultExecutionOrder` 효과 검증)
19. **Expected**: 1프레임 어긋남 없음. 어긋나면 `DefaultExecutionOrder` 값이 부족한 것 → `-100` → `-1000` 으로 더 낮게

---

## 알려진 이슈 / 고려

- **`GetFollowTarget` 매 프레임 호출 비용**: `MinimapAgent.All` 순회는 보통 List 크기 작아 부담 없음. 캐시도 추가 (`_cachedFollowTarget`). PlayerLocal 이 비활성/파괴되면 재탐색
- **씬 전환**: `MinimapAgent` 의 `OnDisable` 에서 List 제거되므로 씬 전환 후 새 PlayerLocal 이 다시 List 에 들어오면 자동 픽업. 캐시는 stale 되지만 `activeInHierarchy` 체크로 무효화
- **여러 PlayerLocal**: 단일 가정. 둘 이상 있으면 List 의 첫 번째 사용 (멀티에선 보통 자기 player 만 PlayerLocal 이라 문제 없음)
- **Manual ↔ Follow 전환 시 카메라 점프**: `ApplyFit` 호출 시 즉시 target 위치로 이동. Lerp 없음. 모드 전환은 디자이너 작업이라 OK. 런타임 토글 추가 시 부드러운 전환 별도 처리 필요
- **`Time.timeScale = 0`**: `pauseTimeWhenBigMapOpen=true` 면 LateUpdate 의 `Time.deltaTime = 0` → Lerp 멈춤. 그 사이 target 위치 안 갱신. 큰 맵 닫으면 다음 프레임에 catch-up. 일시정지 의도엔 자연스러움
- **카메라 z 위치**: `cameraZ=-50` 유지. 추적 중에도 z 는 안 바뀜

---

## 후속 CL

| CL | 내용 |
|---|---|
| CL-223 | Fog of War — 추적 모드와 호환 (텍스처 좌표가 카메라 위치 기반이라 좌표 매핑만 추가) |
| CL-224 | 멀티플레이 통합 — `IsOwner` 가 PlayerLocal kind 부여 → 본 CL 의 자동 탐색이 자연 정합 |
| 후속 폴리시 | 런타임 키 토글 (Shift+M Manual ↔ Follow), `followViewSize` 별도 필드, 줌 인/아웃 키, 카메라 over-shoot 보정 |
