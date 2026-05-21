# 빌드 시간 최적화 슬라이드 v3 — "로스트 메모리 최종발표" Style B 통일

## Context

v1·v2 슬라이드는 자체 디자인(네이비+골드)으로 만들었더니 **본 발표 deck 의 톤에서 이탈**해서
난해해 보였음. 사용자가 참고로 준 `C:\Users\SSAFY\Downloads\로스트 메모리 최종발표.pdf`
(58 페이지) 를 분석한 결과:

- **Style A** (1~30쪽): 게임 픽셀아트 — 스토리텔링용
- **Style B** (40~55쪽): Canva 표준 "성과 분석" 템플릿 — 흰 배경 + 파랑 액센트 + 회색 라운드 카드

빌드 시간 단축 슬라이드는 이미 deck 의 **분석/성과 섹션**과 같은 카테고리이므로
Style B 를 **그대로 베껴서** 톤 통일하는 것이 정답.

### 참고 페이지

- **p50 (매출 및 실적 분석)** — 메인 레퍼런스. 좌측 카드(차트) + 우측 상단(분석결과 bullet) + 우측 하단(비교 카드) 3분할
- **p52 (트렌드 분석)** — 좌측 차트 카드 + 우측 상단(QoQ 차트) + 우측 하단(Key Insights bullet)
- **p40 (google form)** — 좌측 카드 + 우측 도넛 차트

## 디자인

### 팔레트 (PDF Style B 에서 추출)

| 역할 | hex | 용도 |
|---|---|---|
| 배경 | `FFFFFF` | 슬라이드 전체 |
| Primary (파랑) | `1A6CFA` | 라벨, 차트 강조 series, 아이콘 원 |
| Dark | `1F1F1F` | 메인 텍스트, 비교 series |
| Muted | `8E8E93` | 부가 텍스트, 회색 series |
| Card BG | `F2F2F2` | 라운드 카드 배경 |
| Card Border | `E5E5E5` | 카드 테두리 (옵션) |

### 타이포그래피

- 제목: 굵은 한글 SANS-SERIF (`Pretendard Bold` 가 있으면 베스트, 없으면 `맑은 고딕 Bold` / Calibri Bold)
- 본문: 같은 폰트 Regular
- 모든 폰트 좌측 정렬 (제목/본문 모두)

### 레이아웃 (16:9, 10" × 5.625")

```
┌────────────────────────────────────────────────────────────────┐
│  02. 빌드 최적화                                                │  y: 0.45  파랑 11pt
│                                                                │
│  빌드 시간 40배 단축                                             │  y: 0.75  검정 28pt bold
│                                                                │
│  ┌─────────────────────────────┐  ┌─────────────────────────┐ │
│  │ ⓘ  Before vs After          │  │ ⓘ  Key Insights         │ │  y: 1.55
│  │                             │  │                         │ │
│  │  [세로 막대 차트]              │  │ • Demo asset 1,647 파일 │ │
│  │  Before(검정) 40min          │  │ • 182만 줄 dead code   │ │
│  │  After(파랑)  1min           │  │ • 클린 빌드 40 → 1 min │ │
│  │                             │  │ • CI 반복 사이클 단축    │ │
│  │                             │  └─────────────────────────┘ │
│  │                             │  ┌─────────────────────────┐ │
│  │                             │  │ ⓘ  전·후 비교  -39 min  │ │
│  │                             │  │ Before  ████████  40min │ │
│  │                             │  │ After   █         1min  │ │
│  └─────────────────────────────┘  └─────────────────────────┘ │
└────────────────────────────────────────────────────────────────┘
```

### 좌표 (inches)

- Header section: x=0.5, y=0.45~1.25
  - `02. 빌드 최적화` 파랑 라벨 (h=0.3, fontSize 11 bold)
  - `빌드 시간 40배 단축` 큰 제목 (h=0.55, fontSize 28 bold)
- 좌측 카드 (Before vs After 차트): x=0.5, y=1.55, w=5.0, h=3.6
- 우측 상단 카드 (Key Insights): x=5.7, y=1.55, w=3.8, h=1.85
- 우측 하단 카드 (전후 비교 막대): x=5.7, y=3.55, w=3.8, h=1.6

### Style B "카드" 컴포넌트 만드는 법

각 카드 = `ROUNDED_RECTANGLE` 1개 (fill `F2F2F2`, rectRadius 0.12, line 없음 또는 `E5E5E5`)
+ 좌상단 sub-header:
- 파란 원형 (`OVAL`, w=0.28, h=0.28, fill `1A6CFA`)
- 원 안에 `>` 텍스트 (white, 14pt bold, center align)
- 그 옆에 라벨 텍스트 (`Before vs After`, 14pt bold)

### 좌측 카드: 막대 차트

- pptxgenjs `BAR` (column) 차트
- 데이터: `{ labels: ["Before", "After"], values: [40, 1] }`
- `chartColors: ["1F1F1F", "1A6CFA"]` (Before 검정, After 파랑)
- `barRadius` 또는 capsule 효과는 pptxgenjs 에 직접 옵션 없음 → 표준 column 으로 가되 색만 정확히 맞춤
- `showValue: true, dataLabelPosition: "outEnd", dataLabelFormatCode: "0\" min\""` 로 "40 min" / "1 min" 라벨 표시
- `valAxisLabelColor: "8E8E93", catAxisLabelColor: "1F1F1F"`
- `valGridLine: { color: "E5E5E5", size: 0.5 }`
- `chartArea: { fill: { color: "F2F2F2" } }`

### 우측 상단: Key Insights bullet list

- `addText` 에 4개 bullet 항목 (`bullet: true, breakLine: true`)
- 폰트 13-14pt, color `1F1F1F`
- 첫 키워드는 `bold: true` 로 강조 가능 (예: `"Demo asset 1,647 파일"` 만 bold)

### 우측 하단: 전후 비교 가로 막대

- 라벨 `Before` 좌측 + 검정 막대 (폭에 비례, full 40)
- 라벨 `After`  좌측 + 파랑 막대 (폭 1/40)
- 우측 끝에 `-39 min` 회색 작은 텍스트

## 수정 대상 파일

- **새 파일:** `client/docs/khi/presentation/build_optimization_slide_v3.js`
- 산출물: `client/docs/khi/presentation/build_optimization_slide_v3.pptx`

v1·v2 는 유지 (참고/비교용).

## 재사용 자산

- v1 의 `pptxgenjs` 부트스트랩 패턴(`new pptxgen()`, `LAYOUT_16x9`, `writeFile`) 그대로
- 글로벌 설치된 `pptxgenjs@4.0.1` (`C:\Users\SSAFY\AppData\Roaming\npm\node_modules`)
- 차트 옵션 패턴은 [`pptxgenjs.md` "Better-Looking Charts"](skills/pptx/pptxgenjs.md) 섹션 참고
  - `catGridLine: { style: "none" }`, `valGridLine` 옅게, `chartArea fill` 카드와 동색

## 검증

```bash
NODE_PATH="C:\Users\SSAFY\AppData\Roaming\npm\node_modules" \
  node client/docs/khi/presentation/build_optimization_slide_v3.js
```

수동 체크 (LibreOffice/pdftoppm 미설치 환경 → 자동 시각 QA 불가):

1. PowerPoint 로 열어 다음 확인:
   - 흰 배경 + 파란 라벨 + 검정 제목이 PDF p50 과 같은 비율로 보이는지
   - 3개 카드의 라운드 모서리 + 회색 fill 이 PDF 카드와 일관된지
   - 파란 원형 + `>` 아이콘이 카드 좌상단에 정확히 정렬되는지
   - 막대 차트의 "Before 40 / After 1" 비율이 시각적으로 충분히 극단적인지
2. 가능하면 PDF p50 을 같이 띄워 사이드 바이 사이드 비교
3. 발견되는 차이(폰트 weight, 카드 radius, 색 hue 등) 좌표/스타일 조정 후 재생성
