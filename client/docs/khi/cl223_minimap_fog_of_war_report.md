# CL-223 미니맵 Fog of War — 개발 보고서

**Epic**: H. UI / 연출 / 아트 적용
**상태**: ✅ **종결** (Phase A 코드 / Phase B Editor / Phase C 검증 모두 완료)
**작업 기간**: 2026-05-12 (1 세션)
**선행**: CL-222 (미니맵 카메라 + HUD + 마커)
**후속**: CL-224 (멀티 통합), CL-226 (추적 모드)

관련 문서:
- 설계 plan: [cl223_minimap_fog_of_war_plan.md](./cl223_minimap_fog_of_war_plan.md)
- 실행 plan: [cl223_fog_phaseAB_execution.md](./cl223_fog_phaseAB_execution.md)

---

## 한 줄 요약

플레이어가 본 영역만 점진적으로 밝히는 **world space fog of war** 시스템 추가. CL-222 미니맵 위에 RawImage 알파 블렌드로 덧대고, 적/보스/포탈 마커는 reveal 된 영역에서만 표시. **CL-226 추적 모드와 무손실 호환**되도록 설계.

---

## 결과

| 항목 | 결과 |
|---|---|
| 코드 변경 | 신규 1개 + 수정 2개 (총 ~290 LoC 추가) |
| Editor 작업 | `MinimapRig.prefab` 안에 `Fog_Small`/`Fog_Big` RawImage + `MinimapFog` 컴포넌트 부착 |
| 검증 | Phase C-1 ~ C-4 모두 통과 (C-5 성능 검증 skip — 부담 없음) |
| 후속 CL 영향 | CL-224/CL-226 코드 변경 X — 본 CL 설계만으로 자동 호환 |

---

## 결정 사항 (설계 plan 대비)

본 CL 진행 중 3가지 결정 변경 — 모두 더 견고한 방향:

| 항목 | 설계 plan | 실제 채택 | 이유 |
|---|---|---|---|
| **fog 좌표계** | viewport space (Manual fit 정적 가정) | **world space** | CL-226 추적 모드와 무손실 호환. fog 의미가 카메라 fit 과 무관 |
| **fog 영역 결정** | (미명시) | **MinimapCameraRig.manualCenter / manualSize 자동 follow** | 디자이너 추가 설정 0. CL-222 셋업 재사용 |
| **MarkerOverlay icon null skip 결함** | 별도 후속 | **본 CL 에 같이 정리** | `agent.Icon ?? markerPrefab.sprite` fallback. plan 의 "Icon 비움 → prefab Image 사용" 원래 의도와 일치 |

---

## 변경 파일

### 신규 1개
| 파일 | LoC | 책임 |
|---|---|---|
| `Assets/_Project/Scripts/Runtime/UI/Minimap/MinimapFog.cs` | 273 | world space fog 마스크, 10Hz throttle, RawImage uvRect 동기화, 영구 reveal, `IsRevealedAtWorld()` 공개. `AgentKindMask` flags enum 포함 |

### 수정 2개
| 파일 | 변경 |
|---|---|
| `MinimapCameraRig.cs` | public getter 4개 추가 — `ManualCenter`, `ManualSize`, `GetCurrentCameraCenter()`, `GetCurrentCameraOrthographicSize()`. 기존 동작 변경 없음 |
| `MinimapMarkerOverlay.cs` | (1) `[SerializeField] MinimapFog fog` 슬롯 추가, (2) LateUpdate 에 fog 가시성 검사 분기, (3) icon null skip 결함 정리 (`markerPrefab.sprite` fallback), (4) `IsAlwaysVisible(kind)` static 헬퍼 |

### Prefab 수정
`MinimapRig.prefab`:
- `Fog_Small` (UI > Raw Image) 신규 — `SmallMapImage` 자식, stretch-stretch
- `Fog_Big` (UI > Raw Image) 신규 — `BigMapImage` 자식, stretch-stretch
- `MinimapFog` 컴포넌트 신규 — `MinimapHUD` GameObject 에
- `MarkerOverlay_Small/Big` 의 `Fog` 슬롯 → `MinimapFog` 참조 연결

---

## Phase 진행

### Phase A — 코드 ✅
- A-2 `MinimapCameraRig` getter 4개 추가 ✅
- A-1 `MinimapFog.cs` 신규 (world space, 273 LoC) ✅
- A-3 `MinimapMarkerOverlay` fog 슬롯 + 검사 + icon fallback ✅
- A-4 (선택) `MinimapHUD` fog wiring slot — skip (필수 아님)

### Phase B — Editor ✅
- B-1 Unity 컴파일 에러 0건 ✅
- B-2 `MinimapRig.prefab` 편집 모드 진입 ✅
- B-3 `Fog_Small` RawImage 생성 ✅
- B-4 `Fog_Big` RawImage 생성 ✅
- B-5 z-order 확인 — `MarkerOverlay` 가 fog 위로 ✅
- B-6 `MinimapFog` 컴포넌트 부착 + ref 연결 ✅
- B-7 `MarkerOverlay_Small/Big` fog 슬롯 연결 ✅
- B-8 prefab Apply ✅

### Phase C — 검증 ✅
- C-1 기본 fog 동작 — 검정 + 플레이어 원형 reveal ✅
- C-2 적/보스 마커 fog 가시성 ✅
- C-3 M키 큰 맵 토글 ✅
- C-4 씬 재진입 reset ✅
- C-5 (선택) Profiler 성능 — skip (256² × 10Hz 부담 없음)

---

## 검증 통과 시나리오

1. **기본 fog** — 던전 입장 시 미니맵 전체 검정 + 플레이어 위치 주위 원형 reveal
2. **영구 reveal** — 본 곳 다시 안 어두워짐. trail 처럼 영역 확장
3. **적/보스 마커 가시성** — fog 안에선 안 보임, reveal 영역에 들어와야 보임
4. **PlayerLocal 마커 항상 보임** — fog 가린 영역에서도 자기 마커는 표시
5. **M키 큰 맵** — 같은 fog 패턴 (600×600)
6. **씬 재진입** — fog 검정으로 reset (OnEnable 의 ResetMask)

진행 중 발견 + 해결한 이슈:
- **Fog_Small RectTransform 우상단 anchor + 작은 size** → stretch-stretch + offset 0 으로 변경
- (Phase A 코드의 fog null 가드 덕분에) MarkerOverlay 의 `fog` 슬롯 비어있어도 CL-222 동작 보존 — 회귀 없음

---

## 기술 노트

### 1. World space fog 의 핵심
설계 plan 은 viewport space 였으나, CL-226 (추적 모드) 호환을 위해 world space 채택:

```
fog 영역 = MinimapCameraRig.ManualCenter ± ManualSize  (정사각 world 영역)
픽셀 → world: u,v 비례 변환
PaintCircle: world center → mask pixel 변환 후 영구 reveal
SyncFogImageUVs: 카메라 시야 영역 → RawImage.uvRect 매 프레임 갱신
```

추적 모드에서 카메라가 매 프레임 움직여도 `SyncFogImageUVs` 가 uvRect 를 카메라 시야로 갱신 → fog 표시도 자연스럽게 따라옴. **fog 마스크 자체의 의미는 불변**.

### 2. Throttle 분리
- **Reveal 계산 + SetPixels32 = 10Hz** (CPU 부담 절감)
- **uvRect 갱신 = 매 프레임** (추적 모드 부드러움)

`Update()` 안에서 throttle 통과 못 했으면 `SyncFogImageUVs()` 만 실행하고 return.

### 3. 영구 reveal 패턴
PaintCircle 의 핵심:
```csharp
if (newAlpha < _maskBuffer[idx].a)
{
    _maskBuffer[idx].a = newAlpha;
}
```
더 밝은 값 (낮은 alpha) 으로만 갱신 → 한 번 본 곳은 영원히 reveal.

### 4. AgentKindMask flags enum
`MinimapAgent.AgentKind` (enum) → `AgentKindMask` (flags) 변환은 `HasKind()` switch case 로. switch expression 대신 statement (Unity 호환).

---

## 후속 CL 영향

| CL | 영향 | 작업량 |
|---|---|---|
| **CL-224 멀티 통합** | reveal Kinds 가 `PlayerLocal\|PlayerRemote` 라 자동 공유 fog. NetworkBehaviour 변환 시 `PlayerRemote` kind 부여만 하면 끝 | **본 CL 코드 변경 X** |
| **CL-226 추적 모드** | world space fog 라 fit 변경에 안전. `SyncFogImageUVs()` 가 매 프레임 카메라 위치 따라 uvRect 갱신 | **본 CL 코드 변경 X** |
| AutoFromBounds (별도 후속) | bounds 변하면 manualCenter/manualSize 동기화 필요 → MinimapFog 자동 follow. 단 fog 영역 자체가 변하면 이미 본 reveal 정보 손실. 그 시점에 reset 정책 결정 | TBD |

---

## 알려진 이슈 / 보류 항목

- **RawImage uvRect 범위 외 표시** — 카메라가 fog 영역(`manualSize` 정사각) 밖으로 나가면 uvRect 가 [0,1] 범위 벗어남. RawImage `Wrap Mode = Clamp` 면 가장자리 픽셀 stretch. fog 영역을 던전 보다 크게 잡으면 발생 X
- **마커 깜빡임 가능성** — fog 10Hz 갱신 vs MarkerOverlay 매 프레임. fog 갱신 직전 마커가 reveal 경계에 있으면 잠깐 깜빡일 수 있음. visibilityThreshold 0.5 + falloff 0.3 면 체감 거의 없음
- **3단계 fog (currently visible vs explored) 후속 폴리시** — 본 CL 은 2단계 (검정 / 영구 밝음). 현재 시야는 더 밝게, explored 는 dim 표시는 별도 CL
- **LOS 무시 (벽 통과)** — 단순 원형 reveal. 코옵 액션 던전 표준이라 그대로 유지 권장
- **fog 색상/노이즈/그라데이션 패턴** — 디자이너 폴리시 후속

---

## 향후 작업 후보

폴리시 / 확장 영역:
1. **3단계 fog** — 현재 시야 영역만 더 밝게 (currently visible) vs 영구 reveal (explored)
2. **마커 alpha 페이드** — 가린 적이 binary on/off 가 아니라 부드럽게 사라짐
3. **fog 텍스처 dither/noise 패턴** — 디자이너 톤 조정
4. **줌 인/아웃** — 큰 맵 시야 조절
5. **fog 영역 자동 follow AutoFromBounds** — 던전 빌더 통합

---

## PR / 커밋 메시지 후보

### Commit 메시지
```
feat(minimap): add fog of war with world space mask

- New MinimapFog component: 256x256 mask, 10Hz throttle, permanent reveal
- World space coordinate (CL-226 follow mode compatible)
- MarkerOverlay fog visibility check + icon fallback fix
- MinimapCameraRig: 4 public getters for fog wiring

Closes CL-223
```

### PR 제목
```
[CL-223] 미니맵 Fog of War (world space, RawImage 알파 블렌드)
```

### PR 본문 요약
- 플레이어 본 영역만 점진적 reveal (영구 누적)
- 적/보스/포탈은 fog 가린 영역에선 마커 숨김
- CL-222 의 MinimapRig.prefab 위에 RawImage 알파 블렌드로 덧댐 (셰이더/머티리얼 없음)
- CL-226 추적 모드와 자동 호환 (world space 좌표계)
- 부수: MarkerOverlay 의 icon null skip 결함 같이 정리
