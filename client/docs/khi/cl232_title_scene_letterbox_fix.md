# cl232 — Title 씬 빌드 시 위아래 파란색 영역 제거

## Context
Title 씬 빌드 결과에서 화면 위아래에 파란-회색 띠(letterbox)가 보임. 사용자는 "딱 맞게 화면이 커지게" 해달라고 요청. 원인을 코드/씬 분석을 통해 확인했고, 해결책을 Town 씬의 정상 셋업과 동일하게 맞추는 방향으로 정리한다.

---

## 원인 분석

### 파란색의 정체
- `Assets/_Project/Scenes/Title/Title.unity` (Main Camera, line 1284~1363)
  - `m_ClearFlags: 1` (SolidColor)
  - `m_BackGroundColor: (0.192, 0.302, 0.475)` ← 파란-회색
  - 즉, 카메라가 매 프레임 이 색으로 화면을 칠한 위에 UI 가 덮는 구조

### 왜 위아래만 노출되나
- Title 씬 Canvas Scaler 설정 (line 390~412)
  - `m_UiScaleMode: 0` (**Constant Pixel Size**)
  - `m_ReferenceResolution: (800, 600)`
- 빌드 해상도가 1920x1080 이라도 UI/배경 이미지는 **800x600 픽셀 고정 크기**로 렌더링
- 결과: 화면 중앙에 800x600 만큼만 UI 가 보이고, 위아래(그리고 좌우)는 카메라 배경색 노출
- 16:9 빌드에서 800:600 = 4:3 이므로 위아래에 띠가 두드러짐

### Town 씬은 왜 정상인가
- Town Canvas Scaler (line 25507~25513)
  - `m_UiScaleMode: 1` (**Scale with Screen Size**)
  - `m_ReferenceResolution: (1920, 1080)`
  - `m_ScreenMatchMode: 0`, `m_MatchWidthOrHeight: 0.5`
- 화면 해상도에 맞춰 UI 가 자동 스케일

---

## 해결 방안

### 핵심 1: Canvas Scaler 를 Town 과 동일하게 변경
- 대상 파일: `Assets/_Project/Scenes/Title/Title.unity`
- 변경:
  ```
  m_UiScaleMode: 0       → 1   (Scale with Screen Size)
  m_ReferenceResolution: (800, 600) → (1920, 1080)
  m_ScreenMatchMode: 0   (그대로)
  m_MatchWidthOrHeight: 0 → 0.5
  ```
- Unity Editor 에서: Canvas GameObject 선택 → Canvas Scaler 컴포넌트의 UI Scale Mode 변경

### 핵심 2: 배경 이미지(SkyBackground, TileBackground) 가 화면을 cover 방식으로 덮도록
**결정: cover (잘려도 꽉 채움)**

- 현재 `SkyBackground`, `TileBackground` 의 RectTransform 이 anchor (0.5, 0.5)~(0.5, 0.5), SizeDelta (100, 100) 으로 작게 셋업되어 있음
- 변경 (각 배경 GameObject 동일하게 적용):
  1. **RectTransform**
     - Anchor: (0, 0) ~ (1, 1) (양방향 stretch)
     - AnchoredPosition: (0, 0)
     - SizeDelta: (0, 0) → 부모 Canvas 영역 100% 채움
  2. **AspectRatioFitter 컴포넌트 추가**
     - Aspect Mode: **Envelope Parent** (cover 효과)
     - Aspect Ratio: 배경 스프라이트의 원본 비율 (예: 1920/1080 = 1.7778, 일러스트 비율에 맞게 입력)
  3. **Image 컴포넌트**
     - Preserve Aspect: **false** (AspectRatioFitter 가 비율을 보장하므로 Image 자체는 늘어나도 OK)

- 효과:
  - 16:9 화면 → 정확히 맞춤
  - 21:9 (울트라와이드) → 좌우는 꽉 차고 위아래가 살짝 잘림
  - 4:3 (구형 모니터) → 위아래는 꽉 차고 좌우가 살짝 잘림
  - 모든 비율에서 검정/파란 띠 없음 ✓

### 핵심 3: 로그인 UI 요소들은 "안전 영역(Safe Area)" 에 배치
- cover 방식이라 가장자리가 잘릴 수 있으므로, **입력란 / 버튼 / 텍스트는 화면 중앙 80% 영역 안에** 두기
- 이미 중앙 정렬된 UI 라면 그대로 OK, 가장자리에 붙은 요소가 있다면 anchor 점검

### 보조 1: 카메라 배경색을 안전한 검정으로
- line 1308: `m_BackGroundColor` → `(0, 0, 0, 1)`
- 핵심 1+2 가 잘 적용되면 노출되지 않지만, 만약 어떤 해상도에서 살짝 노출되더라도 검정이면 파란 띠보다 자연스러움. 안전망 차원의 변경

### (선택) Camera 도 Town 처럼 Orthographic + Depth Clear 로 통일
- Title 은 어차피 UI Overlay 위주 씬이라 Camera projection 자체는 큰 영향 없지만, 일관성을 위해 변경 가능:
  - line 1332: `orthographic: 0 → 1`
  - line 1307: `m_ClearFlags: 1 → 2` (Depth)
- 단, 다른 World Space 요소가 Title 씬에 있다면 영향 검토 필요. 보조 1 만으로도 충분히 해결됨

---

## 작업 대상 파일

1. `Assets/_Project/Scenes/Title/Title.unity`
   - Canvas Scaler 값 변경 (line 390~412 부근)
   - SkyBackground / TileBackground RectTransform 변경
   - (선택) Main Camera background color 변경 (line 1308)

> Unity 씬 YAML 을 직접 편집하는 것보다 **Editor 에서 GameObject 선택 → 인스펙터에서 값 변경** 후 저장이 안전. YAML diff 가 너무 크지 않으면 직접 편집도 가능.

---

## Verification

1. Editor 에서 Title 씬을 열고 Game View 의 해상도를 변경하며 확인:
   - 1920x1080 (16:9)
   - 1366x768 (16:9 변형)
   - 2560x1440 (16:9 고해상도)
   - 와이드(21:9) — 좌우 배경이 늘어나야
2. 위아래/좌우 어느 쪽에도 파란-회색 띠가 없어야 함
3. UI 텍스트 입력란, 로그인 버튼 등이 잘리지 않고 비율에 맞게 배치되는지 확인
4. 빌드 실행 후 실제 빌드 결과에서도 확인 (Editor 와 빌드의 Canvas 동작 차이는 보통 없지만 최종 확인)
5. 정상 케이스: Town 씬으로 정상 전환되어 마을 진입까지 시각적 문제 없음

---

## 확정 사항 (사용자 답변 반영)

- **배경 처리 방식**: cover (잘려도 꽉 채움) ✓
- **기준 해상도**: 1920x1080 (Town 과 통일) ✓

## 작업 진행 시 확인할 사항

- **Canvas LocalScale (0,0,0) 의심점**: Sub-agent 가 Title 씬 어딘가의 RectTransform LocalScale 을 (0,0,0) 으로 읽었음. 만약 Canvas 자체였다면 UI 가 아예 안 보여야 하는데 빌드 결과는 UI 가 보이는 상태이므로, 다른 GameObject 의 값을 잘못 읽었거나 Screen Space Overlay 모드 특성으로 추정. Editor 에서 Title 씬을 열어 직접 확인하고, 만약 진짜 (0,0,0) 으로 설정된 게 있으면 (1,1,1) 로 복원
- **SkyBackground / TileBackground 원본 비율**: Image 의 Sprite 원본 크기를 확인해서 AspectRatioFitter 의 Aspect Ratio 값에 정확히 입력 (예: 1920x1080 일러스트면 1.7778, 16:10 일러스트면 1.6)
