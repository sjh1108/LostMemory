# CL-226 미니맵 추적 (FollowTarget) 모드 — 구현 실행 plan

**상태**: ✅ **종결** (Phase A ✅ / Phase B ✅ / Phase C ✅) — 2026-05-12

> 본 plan 은 [cl226_minimap_follow_plan.md](./cl226_minimap_follow_plan.md) (설계 plan) 의 구현 실행 단계.
> CL-222/223 패턴: 설계 plan + 실행 plan 분리.
>
> 종결 회고는 [cl226_minimap_follow_report.md](./cl226_minimap_follow_report.md) 참조.
> **본 세션에 방 단위 fog reveal 작업이 묶여 종결** (계획 외 추가) — 보고서에서 자세히.

---

## Context

CL-222 미니맵 카메라가 Manual fit 모드 (고정 좌표 + 고정 크기) 로 작동. 플레이어가 맵 밖으로 가도 카메라 안 움직임.

본 CL: **FollowTarget FitMode 추가** — 카메라가 매 프레임 `PlayerLocal` 위치를 부드럽게 따라감 (디아블로/스타듀밸리 스타일).

### CL-222/223 진행 후 컨텍스트
- ✅ `MinimapCameraRig` 에 public getter 4개 추가됨 (CL-223 A-2). CL-226 작업과 충돌 없음
- ✅ CL-223 fog 가 world space — 추적 모드 카메라 위치 변경 시 `SyncFogImageUVs()` 가 매 프레임 uvRect 갱신 → fog 가 추적 따라 자연 정합
- ✅ MarkerOverlay icon fallback (CL-223) — 추적 모드와 무관

---

## 결정 사항 (cl226 설계 plan 그대로, 변경 없음)

| # | 결정 | 값 | 근거 |
|---|---|---|---|
| 1 | Follow 대상 | 자동 탐색 — `MinimapAgent.All` 에서 `Kind == PlayerLocal` 첫 번째 transform | 인스펙터 슬롯 수동 연결 X. CL-224 멀티 IsOwner 패턴 자연 정합 |
| 2 | 카메라 크기 | `manualSize` 재사용 | 추가 필드 없이 단순. 추적 시야 별도 조정은 후속 |
| 3 | 추적 방식 | 부드러운 (Vector3.Lerp + damping) | 디아블로/탑다운 표준 |
| 4 | 모드 전환 UX | `FitMode` 인스펙터 드롭다운만 | 디자이너 씬/prefab override 로 선택. 런타임 키 토글은 후속 폴리시 |
| 5 | 멀티 사전 고려 | 자동 탐색 방식이 IsOwner 와 자연 정합 | CL-224 시 PlayerRemote kind 부여만으로 호환 |
| 6 (본 turn) | Phase C 검증 시 fog 동시 검증 | fog 켠 상태로 추적 + fog 조합 확인 | CL-223 호환성 즉시 검증, 회귀 방지 |

### 추가 결정 없음
cl226 설계 plan 의 코드 spec / Phase A-C 모두 명확. CL-223 진행 후 새 결정 사항 없음.

---

## Phase A — 코드 (단일 파일 수정)

### 변경 파일
- `Assets/_Project/Scripts/Runtime/UI/Minimap/MinimapCameraRig.cs` (수정만, 신규 X)

### 변경 항목 (cl226 설계 plan 의 코드 spec 그대로)

1. **class attribute** — `[DefaultExecutionOrder(-100)]` 추가
   - 효과: `MinimapMarkerOverlay.LateUpdate` 보다 먼저 실행 → 마커 위치 1프레임 어긋남 방지
   - 이미 `MinimapFog` 가 `Update` 사용 (LateUpdate 아님) — 충돌 없음

2. **`FitMode` enum** — `FollowTarget` 항목 추가
   ```csharp
   public enum FitMode { Manual, AutoFromBounds, FollowTarget }
   ```

3. **새 필드 2개**
   ```csharp
   [Header("Follow")]
   [SerializeField, Min(0.1f), Tooltip("Lerp 강도. 클수록 빠른 추적. 5 권장.")]
   private float followDamping = 5f;

   private Transform _cachedFollowTarget;   // 매 프레임 탐색 회피용
   ```

4. **`LateUpdate()` 신규** — FollowTarget 분기에서 부드러운 추적
   ```csharp
   private void LateUpdate()
   {
       if (fitMode != FitMode.FollowTarget || _camera == null) return;

       Transform target = GetFollowTarget();
       if (target == null) return;

       Vector3 current = transform.position;
       Vector3 desired = new Vector3(target.position.x, target.position.y, cameraZ);
       float t = Mathf.Clamp01(Time.deltaTime * followDamping);
       transform.position = Vector3.Lerp(current, desired, t);
   }
   ```

5. **`GetFollowTarget()` 헬퍼 신규**
   ```csharp
   private Transform GetFollowTarget()
   {
       if (_cachedFollowTarget != null && _cachedFollowTarget.gameObject.activeInHierarchy)
           return _cachedFollowTarget;

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
   ```

6. **`ApplyFit()` 분기 추가** — FollowTarget 모드의 초기 위치/size 적용
   ```csharp
   else if (fitMode == FitMode.FollowTarget)
   {
       orthographicSize = manualSize;
       Transform target = GetFollowTarget();
       center = target != null
           ? new Vector3(target.position.x, target.position.y, 0f)
           : new Vector3(manualCenter.x, manualCenter.y, 0f);
   }
   ```

### 기존 동작 보존
- `Manual` / `AutoFromBounds` 분기 변경 X — 회귀 없음
- CL-223 fog 가 사용하는 getter 4개 변경 X
- Awake / OnValidate 흐름 변경 X

---

## Phase B — Editor 작업

### MinimapRig.prefab 의 MinimapCameraRig 인스펙터
1. `MinimapRig.prefab` 더블클릭 → 편집 모드
2. `MinimapCamera > MinimapCameraRig` 인스펙터:
   - **Fit Mode**: `FollowTarget` (드롭다운에서 선택)
   - **Manual Size**: `15` (시야 좁힘 — 추적감 살리기. 너무 크면 fog 영역 부족)
   - **Follow Damping**: `5` (기본)
3. 편집 종료 → 자동 저장

### 검증 환경 준비
- 검증 씬: `MAP_1F_1R_khi.unity` (1차), `Dungeon.unity` (2차 — 절차생성)
- fog 켠 상태 유지 (CL-223 셋업 그대로)

---

## Phase C — 검증

### C-1. 기본 추적 동작
1. `MAP_1F_1R_khi.unity` Play
2. **Expected**: 플레이어 마커가 미니맵 중앙에 머묾, 배경 (던전) 이 플레이어 움직임 반대로 흘러감
3. 빠른 방향 전환 → 부드러운 추적 (over-shoot 없음, 떨림 없음)
4. 정지 → 1초 안에 카메라도 정지

### C-2. Damping 튜닝 (선택)
5. Follow Damping `1` (느림), `10` (빠름) 로 바꿔보며 체감
6. 디자이너 선호도 따라 prefab 기본값 확정

### C-3. Manual 모드 회귀 검증
7. Fit Mode `Manual` 변경 → 카메라 즉시 정지, CL-222 기존 동작 유지
8. 다시 `FollowTarget` → 추적 재개

### C-4. PlayerLocal 미부착 환경
9. `MinimapAgent.Kind=PlayerLocal` 없는 상태로 Play
10. **Expected**: 카메라가 `manualCenter` 위치 머묾 (fallback). 에러 없음

### C-5. BigMap (M키) 통합
11. Play 중 M키 → 큰 맵도 추적 (같은 RT 라 자동)
12. M키 다시 → 코너 HUD 로

### C-6. **CL-223 fog 와 동시 작동 (본 turn 결정)**
13. fog 켠 상태에서 추적 모드 Play
14. 카메라가 플레이어 따라가면서 **fog 도 같이 부드럽게 따라옴** (`SyncFogImageUVs` uvRect 매 프레임 갱신)
15. 추적 중 reveal 영역이 정상 확장
16. 적/보스 마커도 fog 가시성 정상 (PlayerLocal 항상 보임)

### C-7. 메인 던전 절차생성
17. `Dungeon.unity` Play → 절차생성 던전에서 추적 정상
18. 모든 방 자유 이동 시 미니맵 항상 플레이어 중심
19. 풀링 적 마커 정상

### C-8. 마커 정확도 (DefaultExecutionOrder 효과)
20. 빠르게 이동 중 마커가 카메라 위치와 어긋나는지 (Scene 뷰 + 미니맵 동시 비교)
21. **Expected**: 1프레임 어긋남 없음

---

## 알려진 이슈 / 보류

- **`GetFollowTarget` 매 프레임 호출 비용** — `MinimapAgent.All` List 순회는 부담 없음. 캐시 (`_cachedFollowTarget`) 로 추가 절감
- **씬 전환 시 stale 캐시** — `activeInHierarchy` 체크로 자동 무효화
- **Manual ↔ Follow 전환 시 카메라 점프** — `ApplyFit` 호출 시 즉시 target 위치로. Lerp 없음. 디자이너 작업이라 OK
- **fog 영역(`manualSize`) 밖으로 추적 이동 시** — uvRect 가 [0,1] 범위 벗어남. RawImage Wrap Mode = Clamp 라 가장자리 stretch. `manualSize` 를 던전보다 크게 잡으면 발생 X
- **`pauseTimeWhenBigMapOpen=true`** — Time.deltaTime=0 으로 Lerp 멈춤. 의도된 동작

---

## 변경 파일

### 수정 (단일)
| 파일 | 변경 |
|---|---|
| `MinimapCameraRig.cs` | `[DefaultExecutionOrder(-100)]` + `FitMode.FollowTarget` + `followDamping` + `_cachedFollowTarget` + `LateUpdate()` + `GetFollowTarget()` + `ApplyFit` 분기 추가. ~30줄 |

### Prefab 수정
- `MinimapRig.prefab` 의 `MinimapCameraRig`:
  - Fit Mode → `FollowTarget`
  - Manual Size → `15`
  - Follow Damping → `5`

### 신규
없음.

---

## 후속 CL 영향

| CL | 영향 |
|---|---|
| **CL-223 fog** | world space fog 라 추적 모드와 무손실 호환. 본 CL 코드 변경 X |
| **CL-224 멀티** | NetworkBehaviour 화 시 `IsOwner` → `PlayerLocal` 부여만 하면 자동 정합 — 각 클라이언트가 자기 PlayerLocal 추적. 본 CL 코드 변경 X |
| AutoFromBounds 후속 | bounds 동적 변경 시 추적 모드와 조합 — 후속 결정 |

---

## 종결 조건
- Phase A 코드 수정 + 컴파일 에러 0건
- Phase B prefab 인스펙터 값 변경 + Apply
- Phase C-1, C-3, C-5, C-6, C-7 모두 통과
- (선택) C-2 damping 튜닝, C-4 미부착 환경, C-8 마커 정확도
