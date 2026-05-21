# 빌드 시간 최적화 슬라이드 v2 — 시각화 강화 버전

## Context

SSAFY 발표용 1-슬라이드. 청중은 **대부분 웹 개발자**.

v1 (`build_optimization_slide.pptx`) 은 정보를 다 욱여넣은 텍스트 중심 설계라
시각적 임팩트가 부족했음. v2 는 청중 특성에 맞춰 **메시지를 압축하고
숫자를 크게** 보여주는 방향으로 재설계.

### 청중 분석 결론

- 웹 개발자는 Unity / URP / shader variant 를 모름 → 그 디테일은 **메인 슬라이드에서 제거**
- 하지만 "빌드 시간 단축" 자체는 100% 공감대 (CI / 배포 / 반복 사이클)
- 안 쓰는 의존성 제거로 빌드 단축한 경험은 웹에서도 흔함 → "**의외의 dead asset이 범인**" 스토리가 본능적으로 와닿음
- LOC 감각이 있는 청중 → **182만 줄** 같은 raw 숫자에 강한 반응

### 강조 우선순위
1. **40 → 1 의 시각적 임팩트** (슬라이드 무게의 70%)
2. **1,647 파일 / 182만 줄** raw 숫자 (규모의 충격)
3. URP / 씬 정리는 **메인에서 제외**, 질문 들어오면 backup 슬라이드로 응대

## 디자인

**팔레트:** Midnight Executive (네이비 배경 + 골드 액센트). v1 과 통일.
**타이포:** Georgia (제목), Calibri (본문). v1 과 통일.
**16:9, 10" × 5.625"**

### 레이아웃

```
┌──────────────────────────────────────────────────────────────┐
│  BUILD TIME OPTIMIZATION                                     │   y: 0.3
│  빌드 40분 → 1분                                              │   y: 0.65
│                                                              │
│  ┌─ Before ──────────────────────────────── 40 min ──┐       │
│  │ ████████████████████████████████████████████████  │       │   bar y: 1.6
│  └────────────────────────────────────────────────────┘       │
│  ┌─ After ──────────────────────────────────  1 min ─┐       │
│  │ █                                                  │       │   bar y: 2.5
│  └────────────────────────────────────────────────────┘       │
│                                                              │
│             40×  faster                                      │   y: 3.2
│             97.5% 감소                                       │   y: 4.0
│                                                              │
│  ─────────────────────────────────────────────────           │   divider y: 4.5
│                                                              │
│    1,647            ·            182만                       │   y: 4.7
│    파일 제거                       줄 제거                     │   y: 5.15
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

### 구성 요소

1. **타이틀 (y: 0.3 ~ 1.25)**
   - 작은 라벨 "BUILD TIME OPTIMIZATION" (charSpacing, muted)
   - 큰 한글 "빌드 40분 → 1분" (Georgia 28-32pt, white)

2. **Hero bar comparison (y: 1.4 ~ 3.0)**
   - Before 막대: 폭 7" (4배 더 wide), 색 `C95D63` (붉은 톤)
   - After 막대: 폭 7" × (1/40) = 0.175", 색 `F2C14E` (gold)
   - 각 막대 우측에 시간 라벨 정렬

3. **큰 캘리아웃 "40× faster" (y: 3.2 ~ 4.2)**
   - 60-72pt Georgia, gold
   - 아래 "97.5% 감소" 작게 italic muted

4. **하단 stat 행 (y: 4.7 ~ 5.4)**
   - 좌우 2분할
   - **1,647** (Georgia 54pt gold) + "파일 제거" (Calibri 14pt ice)
   - **182만** (Georgia 54pt gold) + "줄 제거" (Calibri 14pt ice)
   - 중앙에 얇은 vertical divider

### 제외할 요소 (v1 대비)

- 3개 원인 카드/칩 (Demo 정리 / URP variant stripping / 씬 정리)
- footer caption "Built-in RP → URP 전환…"
- 도넛 차트 및 폴더별 legend (Koala2D, MMFeedbacks 등)

이유: 웹 개발자 청중에게 Unity 폴더명/URP 용어는 노이즈. 슬라이드 호흡을
짧게 가져가는 게 임팩트가 큼.

## 수정 대상 파일

- `client/docs/khi/presentation/build_optimization_slide_v2.js` (이미 존재 — 도넛/칩/footer 제거하고 hero/stat 재구성하도록 수정)

산출물:
- `client/docs/khi/presentation/build_optimization_slide_v2.pptx`

## 기존 자산 재사용

- v1 의 팔레트 상수 (`NAVY`, `GOLD`, `ICE`, `WHITE`, `MUTED`, `CARD_BG`) 그대로
- v1 의 `pptxgenjs` 임포트 패턴, `writeFile` 처리 패턴 동일
- 글로벌 설치된 `pptxgenjs@4.0.1` (`C:\Users\SSAFY\AppData\Roaming\npm\node_modules`) — `NODE_PATH` 환경변수로 참조

## 검증

생성 후:

```bash
NODE_PATH="C:\Users\SSAFY\AppData\Roaming\npm\node_modules" \
  node client/docs/khi/presentation/build_optimization_slide_v2.js
```

1. `build_optimization_slide_v2.pptx` 가 생성되는지 확인
2. PowerPoint 로 열어서 육안 확인 (이 환경에 LibreOffice/pdftoppm 미설치 → 자동 시각 QA 불가)
3. 체크포인트:
   - Before/After 막대 길이 비례 정확한지 (40:1)
   - "40×" 큰 숫자가 막대와 안 겹치는지
   - 하단 1,647 · 182만 숫자가 slide 하단 여백을 넘지 않는지 (y < 5.5")
   - 모든 텍스트 박스가 슬라이드 경계(0.5~9.5) 안에 있는지

문제 발견되면 좌표 조정 후 재생성.
