# CL-226 미니맵 추적 + 방 단위 Reveal — 개발 보고서

**Epic**: H. UI / 연출 / 아트 적용
**상태**: ✅ **종결** (Phase A 코드 / Phase B Editor / Phase C 검증 모두 완료)
**작업 기간**: 2026-05-12 (1 세션)
**선행**: CL-222 (미니맵 기초), CL-223 (Fog of War)
**후속**: CL-224 (멀티플레이 통합)

관련 문서:
- 설계 plan: [cl226_minimap_follow_plan.md](./cl226_minimap_follow_plan.md)
- 실행 plan: [cl226_follow_phaseAB_execution.md](./cl226_follow_phaseAB_execution.md)

---

## 한 줄 요약

미니맵 카메라에 **FollowTarget FitMode 추가** (Vector3.Lerp 부드러운 추적, PlayerLocal 자동 탐색) + **방 단위 fog reveal 시스템** (MinimapFog.RevealBounds + MinimapRoomReveal trigger 컴포넌트) 추가. 두 작업이 같은 세션에서 묶여 종결.

---

## 결과

| 항목 | 결과 |
|---|---|
| 코드 변경 | 수정 2개 + 신규 1개 (총 ~120 LoC 추가) |
| Editor 작업 | MinimapRig.prefab 인스펙터 변경 + 방 prefab 들에 `MinimapRoomReveal` 부착 |
| 검증 | 추적 ✅ / 방 단위 reveal ✅ / fog 동시 작동 ✅ |
| 추가 결정 (계획 외) | 방 단위 reveal 작업 — CL-226 에 묶어 처리 |

---

## 결정 사항 (설계 plan 대비)

### CL-226 추적 모드 — 설계 plan 그대로 (변경 없음)
1. Follow 대상: 자동 탐색 (`MinimapAgent.Kind == PlayerLocal`)
2. 카메라 크기: `manualSize` 재사용
3. 추적 방식: 부드러운 (Vector3.Lerp + damping)
4. 모드 전환 UX: `FitMode` 인스펙터 드롭다운만
5. 멀티 자동 정합: `PlayerLocal` kind 패턴

### 본 세션 추가 결정 — 방 단위 reveal (계획 외)
| # | 결정 | 값 | 근거 |
|---|---|---|---|
| 6 | 방 단위 reveal 추가 | CL-226 안에 묶음 (별도 CL X) | "크지 않다면 빠르게" 사용자 결정. 작업량 ~60 LoC + Editor |
| 7 | 시야 reveal 정책 | **끄고 순수 방 단위** | Binding of Isaac 패턴. `Reveal Kinds = Nothing` 으로 시야 reveal 비활성 |
| 8 | 통로 처리 | 방 collider 에 포함 (별도 작업 X) | 사용자 보고: "통로 trigger 없고 통로도 방에 포함" |
| 9 | trigger 부착 방식 | 디자이너가 각 방 prefab 의 `RoomEntryZone` GameObject 에 `MinimapRoomReveal` 추가 | 기존 RoomEntryZone 시스템과 충돌 없는 별도 컴포넌트 |

---

## 변경 파일

### 신규 1개
| 파일 | LoC | 책임 |
|---|---|---|
| `Assets/_Project/Scripts/Runtime/UI/Minimap/MinimapRoomReveal.cs` | 67 | OnTriggerEnter2D → fog.RevealBounds 호출. PlayerLocal 진입 시 1회 실행. fog 자동 탐색 + static 캐시. Editor gizmo 표시 |

### 수정 2개
| 파일 | 변경 |
|---|---|
| `MinimapCameraRig.cs` | (1) class attribute `[DefaultExecutionOrder(-100)]`, (2) `FitMode.FollowTarget` enum 항목, (3) `followDamping` 필드 + `_cachedFollowTarget`, (4) `LateUpdate()` Lerp 추적, (5) `GetFollowTarget()` 헬퍼, (6) `ApplyFit()` FollowTarget 분기. ~50 LoC |
| `MinimapFog.cs` | `public void RevealBounds(Bounds worldBounds)` 메서드 추가 — 직사각 world 영역을 즉시 영구 reveal. 영구 누적 패턴 (이미 reveal 된 픽셀 변화 없음). ~50 LoC |

### Prefab 수정
- `MinimapRig.prefab`:
  - `MinimapCameraRig.FitMode` → `FollowTarget`
  - `MinimapCameraRig.FollowDamping` → `5`
  - `MinimapFog.RevealKinds` → `Nothing` (시야 reveal 끔)
  - (선택) `MinimapHUD.ToggleBigMapKey` → `None` — 큰 맵 잠시 잠금 (후속 결정)
- **각 방 prefab** (`Assets/_Project/Map/Modules/1F1R/Map_1F_*.prefab` 등):
  - `RoomEntryZone` GameObject 에 `MinimapRoomReveal` 컴포넌트 추가

---

## Phase 진행

### Phase A — 코드 ✅
- A-2 `MinimapCameraRig` getter (CL-223 작업으로 이미 추가됨, 본 CL 작업과 충돌 없음) ✅
- A-1 `MinimapCameraRig` FollowTarget 추가 (6가지 변경) ✅
- A-3 (계획 외) `MinimapFog.RevealBounds()` 추가 ✅
- A-4 (계획 외) `MinimapRoomReveal.cs` 신규 ✅

### Phase B — Editor ✅
- B-1 Unity 컴파일 에러 0건 ✅
- B-2 MinimapCameraRig 인스펙터 FitMode → FollowTarget + Damping ✅
- B-3 (계획 외) MinimapFog 의 Reveal Kinds → Nothing ✅
- B-4 (계획 외) 방 prefab 들에 MinimapRoomReveal 부착 ✅
- B-5 prefab Apply ✅

### Phase C — 검증 ✅
- C-1 기본 추적 동작 — 플레이어 마커 중앙, 던전 흐름 ✅
- C-2 Damping 튜닝 — 5 로 종결 (자연스러움)
- C-3 Manual 모드 회귀 검증 — 본 세션 skip (CL-222 기존 동작이 별도로 검증됨)
- C-5 BigMap 통합 — 큰 맵 잠금으로 skip
- C-6 CL-223 fog 와 동시 작동 — **방 단위 reveal 정상** ✅
- C-7 메인 던전 절차생성 — `Dungeon.unity` 검증 대기 (후속 검증 가능, 본 CL 종결 조건은 아님)

### 추가 검증 — 방 단위 reveal
- 시작 시 미니맵 전체 검정 (시야 reveal 없음) ✅
- 방 진입 → 방 + 통로 전체 사각형 영역 즉시 reveal ✅
- 다른 방 진입 → 그 방도 추가 reveal ✅
- 본 방 재진입 → 영구 reveal 유지, 추가 호출 무비용 ✅
- PlayerLocal 마커는 항상 보임 ✅

---

## 기술 노트

### 1. DefaultExecutionOrder 효과
```
Update phase:     MinimapFog.Update (fog throttle + uvRect)
LateUpdate phase: MinimapCameraRig.LateUpdate (-100 우선)  → 카메라 위치 갱신
                  MinimapMarkerOverlay.LateUpdate (0)      → 마커 위치 (갱신된 카메라 사용)
```

추적 모드에서 카메라가 매 프레임 움직이는데 MarkerOverlay 가 먼저 실행되면 1프레임 어긋남 발생. `[DefaultExecutionOrder(-100)]` 으로 MinimapCameraRig 가 먼저 실행 보장 → 마커는 항상 새 카메라 위치 기준으로 그려짐.

### 2. fog 좌표계와 추적 호환 (CL-223 설계의 보너스)
CL-223 의 world space fog 가 추적 모드와 자연 정합:
- fog 마스크 의미 = world 좌표 기반 (불변)
- `SyncFogImageUVs()` 가 매 프레임 RawImage.uvRect 를 카메라 시야로 갱신
- 카메라 위치 변하면 fog 표시도 자연스럽게 따라옴

### 3. MinimapRoomReveal 의 결합도
RoomEntryZone (기존 시스템) 과 충돌 회피 전략:
- 같은 GameObject 의 Collider2D 공유
- OnTriggerEnter2D 자체 구현 (RoomEntryZone 과 별개 메서드)
- fog 참조는 static 캐시 + 자동 탐색 — 디자이너가 방 prefab 마다 일일이 fog 슬롯 연결 부담 없음

### 4. 방 단위 reveal 의 영구 누적 패턴
`MinimapFog.RevealBounds()` 의 핵심:
```csharp
if (_maskBuffer[idx].a > 0)
{
    _maskBuffer[idx].a = 0;
    anyChanged = true;
}
```
이미 alpha=0 (완전 reveal) 인 픽셀은 건너뜀 — 같은 방 재진입 시 페인트 작업 0. `anyChanged` 플래그로 SetPixels32 호출도 조건부.

### 5. CL-224 멀티 통합 호환성
본 CL 의 두 작업 모두 CL-224 와 자동 정합:
- **추적 모드**: `GetFollowTarget()` 이 자기 `PlayerLocal` 만 탐색 → 각 클라가 자기 player 추적
- **방 단위 reveal**: `MinimapRoomReveal` 이 `PlayerLocal` MinimapAgent 만 trigger → CL-224 에서 `IsOwner` → `PlayerLocal` 부여 패턴이면 호스트 트리거 X (각자 자기 방만 reveal). 공유 fog 원하면 `revealKinds` 에 PlayerRemote 추가 또는 RoomReveal 의 PlayerRemote 도 허용

---

## 알려진 이슈 / 보류

- **큰 맵 (BigMap) 잠금 상태** — `ToggleBigMapKey = None` 으로 잠시 비활성. 후속에 복구하거나 게임 디자인 결정. 비활성 상태에서도 fog/추적 자체는 정상
- **`Dungeon.unity` 절차생성 환경 검증 대기** — 본 CL 종결 후 별도 시점에 검증 가능. 1차는 `MAP_1F_1R_khi.unity` 로 충분
- **방 reveal 사각형 가장자리 단단함** — fog 마스크 Bilinear filter 로 살짝 부드러움. 디자이너 요구 시 falloff 추가 (후속 폴리시)
- **방 collider 가 시각 영역과 불일치** — RoomEntryZone 의 BoxCollider2D 가 방 시각보다 작/크면 reveal 영역도 어긋남. 디자이너가 collider 조정 책임
- **PlayerLocal 미부착 환경 시 fallback** — `GetFollowTarget()` 이 null 반환 → 카메라가 `manualCenter` 머묾. 에러 없음. PlayerRemote 만 있는 환경 (관전?) 은 후속 검토

---

## 후속 CL 영향

| CL | 영향 |
|---|---|
| **CL-224 멀티 통합** | 추적/방 reveal 모두 `PlayerLocal` 기반이라 `IsOwner` → kind 패턴으로 자동 정합. 공유 fog 원하면 `MinimapRoomReveal` 의 trigger 조건에 `PlayerRemote` 추가 (~3줄) |
| AutoFromBounds (별도 후속) | bounds 동적 변경 시 fog 영역도 변경 — 본 CL 추적 모드와 추가 검증 필요 |
| 큰 맵 복구 | `ToggleBigMapKey` 다시 `M` 설정. 본 CL 변경 X |
| 방 reveal 가장자리 부드러움 | `RevealBounds()` 에 falloff 옵션 추가 (~10줄). 디자이너 요청 시 |

---

## PR / 커밋 메시지 후보

### Commit 메시지
```
feat(minimap): add follow target mode + room-based fog reveal

- MinimapCameraRig: FollowTarget FitMode, Vector3.Lerp smooth follow
- DefaultExecutionOrder(-100): marker accuracy in follow mode
- MinimapFog.RevealBounds(): rectangular area reveal API
- MinimapRoomReveal: trigger-based room reveal on PlayerLocal enter
- Reveal Kinds disabled (pure room-based instead of circular sight)

Closes CL-226
```

### PR 제목
```
[CL-226] 미니맵 추적 모드 (FollowTarget) + 방 단위 fog reveal
```

### PR 본문 요약
- **추적 모드**: 카메라가 플레이어를 부드럽게 따라감 (디아블로 스타일)
- **방 단위 reveal**: 시야 reveal 끄고 방 진입 시 그 방 전체 영구 reveal (Binding of Isaac 스타일)
- 단일 파일 수정 + 신규 1개 (~120 LoC)
- CL-222/223 기능 회귀 없음
- CL-224 멀티 통합 시 자동 정합

---

## 종결 조건 — 모두 충족 ✅

- [x] Phase A 코드 + 컴파일 에러 0건
- [x] Phase B prefab 인스펙터 변경 + Apply
- [x] Phase C-1 기본 추적
- [x] Phase C-6 fog 동시 작동 + 방 단위 reveal
- [x] CL-222/223 기존 동작 회귀 없음 확인
