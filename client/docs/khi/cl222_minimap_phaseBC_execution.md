# CL-222 미니맵 Phase B/C 실행 계획

> 본 문서는 [cl222_minimap_camera_hud_plan.md](./cl222_minimap_camera_hud_plan.md) 의 후속 — 코드(Phase A)는 완료되었고, 남은 **Editor 작업(Phase B) + 검증(Phase C)** 의 실행 순서를 확정한 실행 plan.

---

## Context

LostMemory(절차생성 던전 코옵)에 미니맵이 없어 자기 위치/적·팀원 위치 파악 불가. CL-222 코드 4개 파일(`MinimapAgent`, `MinimapMarkerOverlay`, `MinimapHUD`, `MinimapCameraRig`)은 spec 일치하게 완성. 이제 Unity Editor 에서 **자산 생성 + 씬 배치 + prefab 부착 + 1차 검증**을 끝내면 CL-222 종결.

---

## 결정 사항

### 초기 확정 (2026-05-11)
| 결정 | 값 | 영향 |
|---|---|---|
| Player prefab | **TestKhi_MinimalCharacter2D** | MinimapAgent 1차 부착 대상. KhiParrySpark/TestKhi_Net_AD 는 후속 |
| 검증 씬 | **kjs_test/MAP_1F_1R ~ 1F_4R** | 고정 방 → 좌표 디버깅 쉬움. Manual fit center/size 잡기 편함 |
| 미니맵 배경 정책 | **C: 빈 배경 + 마커만** | 1차는 마커 동작만 검증. A/B 결정은 후속 |
| 부착 범위 | **Player + Enemy 9개 + Boss 2개** | Phase B-9~11 모두 수행 |

### 진행 중 확정 (2026-05-12)
| 결정 | 값 | 경위 |
|---|---|---|
| 검증 씬 (실제) | **`Assets/Scenes/MAP_1F_1R_khi.unity`** + 메인 `Dungeon.unity` | 초기 plan 의 kjs_test 씬 대신 khi 사본 사용. 메인 던전에서 멀티방 검증 ✅ |
| 통합 prefab 사용 | **`MinimapRig.prefab`** (MinimapCamera + MinimapCanvas 통합) | prefab 끼리 ref 불가 문제 해결 위해 옵션 B(통합) 선택. 다른 씬 재사용 용이 |
| 미니맵 배경 정책 (최종) | **화이트리스트 Culling Mask = `Default` Layer 만** | C 로 1차 검증 후 던전 시각 노출 단계에서 결정. enemy 자식(WeaponAttachment 등) Layer 추적 회피. [상세](./cl222_minimap_camera_hud_plan.md#결정-완료--미니맵-배경-정책-2026-05-12) |
| Player Icon 슬롯 | `icon_minimap_player.png` (DungeonArchitect 샘플) | plan 의 "Icon 비움" spec 결함 우회 — `MinimapMarkerOverlay.cs:70` 에서 `agent.Icon == null` 이면 skip 하는 코드 발견. Editor 옵션 1 로 우회 |
| 부착 진행 현황 | **Player ✅ / Enemy 9개 ✅ / Boss 2개 ✅** (전부 완료) | 2026-05-12 모두 부착 완료 |

### 알려진 spec 결함 (후속 정리 후보)
- **`MinimapMarkerOverlay.cs:70`** 의 `agent.Icon == null` skip 조건 — plan 의 "Icon 비움 → prefab Image 사용" 의도와 어긋남. 정석은 `agent.Icon ?? markerPrefab.sprite` fallback. 본 CL 에선 Editor 우회로 진행, 후속에 코드 정리 권장

---

## Phase B 실행 순서 (Editor 작업, khi 수행)

> 각 step 끝나면 체크. 순서대로 진행 — 앞 단계가 뒷 단계의 ref 가 됨.

### Step 1. Layer 추가 ✅
- `Edit > Project Settings > Tags and Layers` → 빈 User Layer 슬롯에 **`Minimap`** 추가
- (배경 정책 C 이므로 이 Layer 에 아무 GameObject 도 옮기지 않음. Culling Mask 용으로만 존재)

### Step 2. 자산 폴더 + 생성 ✅
- `Assets/_Project/RenderTextures/` 폴더가 없으면 생성
- 우클릭 → `Create > Render Texture` → 이름 **`MinimapRT`** (`Assets/_Project/RenderTextures/MinimapRT.renderTexture`)
  - Size **256×256**
  - Color Format: 기본
  - Depth Buffer: **No depth**

### Step 3. MinimapMarker prefab 생성 ✅
- 임시 씬에서 Hierarchy → `UI > Image` 추가, 이름 `MinimapMarker`
- RectTransform Size **16×16**
- Image Source: Unity 내장 `Knob` (원형) 또는 `UISprite`
- `Assets/_Project/Prefabs/UI/Minimap/` 폴더 생성 → 끌어서 prefab 화
- 씬에서 원본 삭제

### Step 4. MinimapCamera GameObject ✅ (MinimapRig.prefab 으로 통합)
- 검증 씬(`MAP_1F_1R`) 열기 → Hierarchy 루트에 빈 GameObject `MinimapCamera`
- Camera 컴포넌트 자동 부착 (Add Component → Camera)
- `MinimapCameraRig` 컴포넌트 추가
- 인스펙터 설정:
  - Target Texture: **`MinimapRT`**
  - Culling Mask: **`Minimap` 만 체크** (Default 포함 모두 해제)
  - Background Color: 검정 (0,0,0,1)
  - Camera Z: -50 (기본)
  - Fit Mode: **Manual**
  - Manual Center: `(0, 0)` (씬에서 던전 방의 중심 좌표 확인 후 조정)
  - Manual Size: `20` (방이 안 들어가면 키움 — Scene 뷰에서 던전 sprite bounds 확인)

### Step 5. MinimapCanvas (별도 Canvas) ✅ (MinimapRig.prefab 으로 통합)
- Hierarchy 루트에 `UI > Canvas` 추가, 이름 **`MinimapCanvas`**
- Canvas:
  - Render Mode: Screen Space - Overlay
  - Sort Order: **10**
- Canvas Scaler: Scale With Screen Size, Reference 1920×1080

### Step 6. SmallMap (코너 HUD) ✅
`MinimapCanvas` 자식으로:
- 빈 GameObject **`SmallMap`** — RectTransform Anchor 우상단(1,1), Pivot 우상단(1,1), Anchored Position 으로 여유 (예: -20, -20), **Width 200, Height 200**
  - 자식: `UI > Raw Image` 이름 **`SmallMapImage`** — RectTransform Stretch (anchor 0,0 ~ 1,1), Texture **`MinimapRT`**
  - 자식: 빈 GameObject **`MarkerOverlay_Small`** — RectTransform Stretch
    - `MinimapMarkerOverlay` 컴포넌트:
      - Minimap Camera: `MinimapCamera` 의 Camera
      - Map Rect: **`SmallMapImage` 의 RectTransform**
      - Marker Parent: 비움 (mapRect 사용)
      - Marker Prefab: `MinimapMarker.prefab` 의 Image 컴포넌트
      - Clamp Offscreen: true
      - Initial Pool Size: 16

### Step 7. BigMap (M키 토글) ✅
`MinimapCanvas` 자식으로:
- 빈 GameObject **`BigMap`** — RectTransform Anchor 중앙(0.5,0.5), Pivot 중앙(0.5,0.5), Position (0,0)
  - **GameObject 비활성화** (체크 해제 — `startWithBigMapVisible=false` 와 일관)
  - 자식: `UI > Image` 이름 **`Backdrop`** — Stretch 풀화면, Color (0,0,0,0.6)
  - 자식: `UI > Raw Image` 이름 **`BigMapImage`** — 600×600 중앙, Texture **`MinimapRT`**
  - 자식: 빈 GameObject **`MarkerOverlay_Big`** — 600×600 중앙
    - `MinimapMarkerOverlay` 컴포넌트: Camera 동일, Map Rect = `BigMapImage` RectTransform, Marker Prefab 동일

### Step 8. MinimapHUD 컴포넌트 부착 ✅
- `MinimapCanvas` 루트에 `MinimapHUD` 컴포넌트 추가
- 인스펙터:
  - Minimap Render Texture: **`MinimapRT`**
  - Small Map Root: `SmallMap`
  - Small Map Image: `SmallMapImage` (RawImage)
  - Big Map Root: `BigMap`
  - Big Map Image: `BigMapImage` (RawImage)
  - Toggle Big Map Key: **M**
  - Start With Big Map Visible: **false**
  - Pause Time When Big Map Open: **false**

### Step 9. Player prefab — MinimapAgent ✅ (Icon: `icon_minimap_player.png`)
- `Assets/_Project/Prefabs/Characters/TestKhi_MinimalCharacter2D.prefab` 열기
- 루트에 `MinimapAgent` 추가
- 인스펙터:
  - Kind: **PlayerLocal**
  - Icon: `MinimapMarker` 원형 sprite (또는 비움 — overlay 에서 prefab Image 사용)
  - Tint: **파랑** (0.2, 0.5, 1.0, 1.0)
  - Icon Scale: 1.2
  - Rotate With Transform: **true** (이동 방향 화살표 — 안 돌아가면 후속에 false 로 회귀)
  - Priority: 100

### Step 10. Enemy 7개 prefab — MinimapAgent ✅
`Assets/_Project/Prefabs/Enemies/` 하위 다음 7개 각각에 `MinimapAgent` 추가, **Kind=Enemy, Tint=빨강(1.0, 0.2, 0.2, 1.0), Icon Scale=0.8, Rotate With Transform=false, Priority=10**:
- `Orc_CL037`
- `OrcRider_CL039`
- `SkeletonArcher_CL041`
- `Chobomb_CL212`
- `Moose1_Test`
- `StoneGolem_Test`
- `MapMaker_LM`

### Step 11. Boss 2개 — MinimapAgent ✅
`Assets/_Project/Prefabs/Enemies/Boss/` 의 `BerthaRoot.prefab`, `BerthaRoot2.prefab` 각각에:
- Kind: **Boss**
- Tint: **주황** (1.0, 0.55, 0.0, 1.0)
- Icon Scale: **1.8**
- Rotate With Transform: false
- Priority: 80

---

## Phase C 검증 (kjs_test/MAP_1F_1R 우선)

### C-1. 기본 HUD 동작 ✅
1. `MAP_1F_1R.unity` 열기 → Play
2. **Expected**: 우상단 200×200 코너에 검정 배경 + 플레이어 파랑 마커
3. 플레이어 이동 → 마커 따라옴
   - 반대 방향이면 `MinimapCamera.Manual Center` 조정
   - 마커가 화면 밖으로 나가면 `Manual Size` 키움
4. 회전 → 마커 화살표 회전 (안 되면 `MinimapAgent.rotateWithTransform=false` 로 회귀)

### C-2. 적 마커 ✅
5. 적 스폰 트리거 → 빨강 마커
6. 처치 → 마커 사라짐
7. **풀링 검증** → 같은 방에서 적 재스폰 시 두 번째도 마커 정상 (콘솔에 디버그 로그 임시 추가 권장)

### C-3. M키 토글 ✅
8. M → 화면 중앙 600×600 큰 맵 + 반투명 배경
9. M 다시 → 사라짐
10. 큰 맵에서도 마커 동일 동작
11. 큰 맵 열고도 게임 계속 진행 (pauseTimeWhenBigMapOpen=false)

### C-4. 보스 (Dungeon_1F_Boss.unity) ✅
12. 보스방 입장 → **주황 큰 마커** (Icon Scale 1.8) 가 다른 적과 구분됨

### C-5. 풀링 안전성 (Dungeon.unity) ✅ (메인 던전 정상 동작, 멀티 방 검증 통과)
13. 메인 던전에서 절차생성 + 풀링 환경에서 마커 누락/잔존 없음 검증

---

## 결정 보류 (본 CL 종결 후 별도 논의)

- **던전 visual 미니맵 반영** (배경 정책 A vs B) — 디자이너 합의 필요
- **AutoFromBounds 자동 fit** — 던전 builder 에서 bounds 노출 코드 필요. 본 CL 종결 후 별도 CL
- **GetMarkerRotation 한계** — SpriteRenderer.flipX 만 쓰는 캐릭터는 마커 회전 안 됨. 후속에 서브클래싱

---

## 변경 파일

본 plan 은 **코드 변경 없음** — Editor 작업 + 자산 생성 + prefab 부착만.

생성 자산:
- `Assets/_Project/RenderTextures/MinimapRT.renderTexture`
- `Assets/_Project/Prefabs/UI/Minimap/MinimapMarker.prefab`

씬 수정:
- `kjs_test/MAP_1F_1R.unity` (+ 다른 검증 씬들) — MinimapCamera + MinimapCanvas 추가
- 또는 prefab 화하여 모든 씬에 instance 로 배치 권장

Prefab 수정 (MinimapAgent 부착):
- `Characters/TestKhi_MinimalCharacter2D.prefab`
- `Enemies/` 의 7개 (Step 10 참조)
- `Enemies/Boss/` 의 BerthaRoot, BerthaRoot2

---

## 마이그레이션 권장 (선택)

검증 씬마다 MinimapCamera + MinimapCanvas 를 매번 만들면 작업량이 많음. **`MinimapRig.prefab` 으로 묶어서** 모든 검증 씬에 instance 로 두면 한 번만 셋업하고 재사용 가능. Step 4~8 끝난 후 두 GameObject 를 합쳐 prefab 화 권장.
