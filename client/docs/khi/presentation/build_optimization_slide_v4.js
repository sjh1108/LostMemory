// Build optimization 3-slide deck (Style B, white bg + blue accent)
// Slide 1 — 결과: 40배 단축 헤드라인 + Before/After 막대
// Slide 2 — 원인: dead asset 1,647 파일 + 폴더별 분포
// Slide 3 — 효과: 가로 비교 + Key Insights
const pptxgen = require("pptxgenjs");

const pres = new pptxgen();
pres.layout = "LAYOUT_16x9";
pres.title = "Build Time Optimization (3-page)";

// Style B palette
const BG = "FFFFFF";
const PRIMARY = "1A6CFA";
const PRIMARY_DIM = "5B91F5";
const DARK = "1F1F1F";
const MUTED = "8E8E93";
const CARD = "F2F2F2";
const CARD_BORDER = "E5E5E5";

// ───────────────────────────────────────────── Common helpers
function addHeader(slide, eyebrow, title) {
  slide.addText(eyebrow, {
    x: 0.5, y: 0.45, w: 9, h: 0.3,
    fontFace: "Pretendard", fontSize: 11, color: PRIMARY,
    bold: true, margin: 0,
  });
  slide.addText(title, {
    x: 0.5, y: 0.75, w: 9, h: 0.6,
    fontFace: "Pretendard", fontSize: 28, color: DARK,
    bold: true, margin: 0,
  });
}

function addCard(slide, x, y, w, h) {
  slide.addShape(pres.shapes.ROUNDED_RECTANGLE, {
    x, y, w, h,
    fill: { color: CARD }, line: { color: CARD_BORDER, width: 0.75 },
    rectRadius: 0.12,
  });
}

function addCardHeader(slide, x, y, label, labelW = 3.0) {
  slide.addShape(pres.shapes.OVAL, {
    x, y, w: 0.28, h: 0.28,
    fill: { color: PRIMARY }, line: { color: PRIMARY, width: 0 },
  });
  slide.addText(">", {
    x, y: y - 0.02, w: 0.28, h: 0.28,
    fontFace: "Calibri", fontSize: 12, color: BG,
    bold: true, align: "center", valign: "middle", margin: 0,
  });
  slide.addText(label, {
    x: x + 0.38, y: y - 0.02, w: labelW, h: 0.32,
    fontFace: "Pretendard", fontSize: 14, color: DARK,
    bold: true, align: "left", valign: "middle", margin: 0,
  });
}

function addBullets(slide, x, y, w, h, lines, fontSize = 12) {
  const lineH = h / lines.length;
  lines.forEach((runs, i) => {
    slide.addText("•", {
      x, y: y + i * lineH, w: 0.2, h: lineH,
      fontFace: "Pretendard", fontSize: fontSize + 2, color: DARK,
      bold: true, align: "left", valign: "middle", margin: 0,
    });
    slide.addText(runs, {
      x: x + 0.22, y: y + i * lineH, w: w - 0.25, h: lineH,
      fontFace: "Pretendard", fontSize, color: DARK,
      align: "left", valign: "middle", margin: 0,
    });
  });
}

// ═════════════════════════════════════════════════════════════════
// SLIDE 1 — 결과: 빌드 시간 40배 단축
// ═════════════════════════════════════════════════════════════════
const s1 = pres.addSlide();
s1.background = { color: BG };
addHeader(s1, "02. 빌드 최적화 · 결과", "빌드 시간 40배 단축");

// Left card — large bar chart Before vs After
const L1_X = 0.5, L1_Y = 1.55, L1_W = 5.5, L1_H = 3.6;
addCard(s1, L1_X, L1_Y, L1_W, L1_H);
addCardHeader(s1, L1_X + 0.3, L1_Y + 0.3, "Before vs After  빌드 시간(분)", 4.5);

s1.addChart(
  pres.charts.BAR,
  [{ name: "빌드 시간", labels: ["Before", "After"], values: [40, 1] }],
  {
    x: L1_X + 0.25, y: L1_Y + 0.75, w: L1_W - 0.5, h: L1_H - 1.0,
    barDir: "col",
    chartColors: [DARK, PRIMARY],
    chartArea: { fill: { color: CARD }, border: { color: CARD, pt: 0 } },
    plotArea: { fill: { color: CARD } },
    showLegend: false,
    showValue: true,
    dataLabelPosition: "outEnd",
    dataLabelFontFace: "Pretendard",
    dataLabelFontSize: 14,
    dataLabelColor: DARK,
    dataLabelFormatCode: '0" min"',
    catAxisLabelFontFace: "Pretendard",
    catAxisLabelFontSize: 12,
    catAxisLabelColor: DARK,
    valAxisLabelFontFace: "Pretendard",
    valAxisLabelFontSize: 10,
    valAxisLabelColor: MUTED,
    valGridLine: { color: CARD_BORDER, size: 0.5, style: "solid" },
    catGridLine: { style: "none" },
    valAxisMaxVal: 50,
    valAxisMinVal: 0,
    valAxisMajorUnit: 10,
    barGapWidthPct: 100,
  }
);

// Right column — three stat callouts stacked
const R1_X = 6.2, R1_W = 3.3;
const stat1Y = 1.55, stat1H = 1.6;
const stat2Y = 3.25, stat2H = 0.9;
const stat3Y = 4.25, stat3H = 0.9;

// Stat 1: big 40×
addCard(s1, R1_X, stat1Y, R1_W, stat1H);
s1.addText("40×", {
  x: R1_X + 0.2, y: stat1Y + 0.2, w: R1_W - 0.4, h: 0.9,
  fontFace: "Pretendard", fontSize: 60, color: PRIMARY,
  bold: true, align: "center", valign: "middle", margin: 0,
});
s1.addText("faster", {
  x: R1_X + 0.2, y: stat1Y + 1.05, w: R1_W - 0.4, h: 0.35,
  fontFace: "Pretendard", fontSize: 16, color: DARK,
  bold: true, align: "center", valign: "middle", margin: 0,
});

// Stat 2: -39 min
addCard(s1, R1_X, stat2Y, R1_W, stat2H);
s1.addText("-39 min", {
  x: R1_X + 0.2, y: stat2Y + 0.1, w: R1_W - 0.4, h: 0.45,
  fontFace: "Pretendard", fontSize: 24, color: DARK,
  bold: true, align: "center", valign: "middle", margin: 0,
});
s1.addText("절감 시간 (1회 빌드 기준)", {
  x: R1_X + 0.2, y: stat2Y + 0.5, w: R1_W - 0.4, h: 0.3,
  fontFace: "Pretendard", fontSize: 10, color: MUTED,
  align: "center", valign: "middle", margin: 0,
});

// Stat 3: 97.5%
addCard(s1, R1_X, stat3Y, R1_W, stat3H);
s1.addText("97.5% 감소", {
  x: R1_X + 0.2, y: stat3Y + 0.1, w: R1_W - 0.4, h: 0.45,
  fontFace: "Pretendard", fontSize: 24, color: DARK,
  bold: true, align: "center", valign: "middle", margin: 0,
});
s1.addText("전체 빌드 시간 대비", {
  x: R1_X + 0.2, y: stat3Y + 0.5, w: R1_W - 0.4, h: 0.3,
  fontFace: "Pretendard", fontSize: 10, color: MUTED,
  align: "center", valign: "middle", margin: 0,
});

// ═════════════════════════════════════════════════════════════════
// SLIDE 2 — 원인: dead asset 1,647 파일 / 182만 줄
// ═════════════════════════════════════════════════════════════════
const s2 = pres.addSlide();
s2.background = { color: BG };
addHeader(s2, "02. 빌드 최적화 · 원인 분석", "Dead asset 1,647 파일이 빌드를 잡고 있었다");

// Left card — folder breakdown horizontal bar chart
const L2_X = 0.5, L2_Y = 1.55, L2_W = 5.5, L2_H = 3.6;
addCard(s2, L2_X, L2_Y, L2_W, L2_H);
addCardHeader(s2, L2_X + 0.3, L2_Y + 0.3, "삭제된 파일 분포  ·  폴더별 (개수)", 4.5);

s2.addChart(
  pres.charts.BAR,
  [
    {
      name: "files",
      labels: ["Koala2D", "MMFeedbacks", "MMTools", "Grasslands", "Inventory"],
      values: [633, 422, 225, 218, 146],
    },
  ],
  {
    x: L2_X + 0.25, y: L2_Y + 0.8, w: L2_W - 0.5, h: L2_H - 1.05,
    barDir: "bar",
    chartColors: [PRIMARY],
    chartArea: { fill: { color: CARD }, border: { color: CARD, pt: 0 } },
    plotArea: { fill: { color: CARD } },
    showLegend: false,
    showValue: true,
    dataLabelPosition: "outEnd",
    dataLabelFontFace: "Pretendard",
    dataLabelFontSize: 11,
    dataLabelColor: DARK,
    catAxisLabelFontFace: "Pretendard",
    catAxisLabelFontSize: 11,
    catAxisLabelColor: DARK,
    valAxisLabelFontFace: "Pretendard",
    valAxisLabelFontSize: 9,
    valAxisLabelColor: MUTED,
    valGridLine: { color: CARD_BORDER, size: 0.5, style: "solid" },
    catGridLine: { style: "none" },
    barGapWidthPct: 60,
  }
);

// Right column — two stat cards + insights
const R2_X = 6.2, R2_W = 3.3;
// Total files stat
addCard(s2, R2_X, 1.55, R2_W, 0.95);
s2.addText("1,647", {
  x: R2_X, y: 1.6, w: R2_W, h: 0.55,
  fontFace: "Pretendard", fontSize: 32, color: PRIMARY,
  bold: true, align: "center", valign: "middle", margin: 0,
});
s2.addText("총 삭제 파일", {
  x: R2_X, y: 2.15, w: R2_W, h: 0.3,
  fontFace: "Pretendard", fontSize: 10, color: MUTED,
  align: "center", valign: "middle", margin: 0,
});

// Total lines stat
addCard(s2, R2_X, 2.6, R2_W, 0.95);
s2.addText("182만 줄", {
  x: R2_X, y: 2.65, w: R2_W, h: 0.55,
  fontFace: "Pretendard", fontSize: 32, color: PRIMARY,
  bold: true, align: "center", valign: "middle", margin: 0,
});
s2.addText("Dead code 삭제", {
  x: R2_X, y: 3.20, w: R2_W, h: 0.3,
  fontFace: "Pretendard", fontSize: 10, color: MUTED,
  align: "center", valign: "middle", margin: 0,
});

// Diagnosis insights card
addCard(s2, R2_X, 3.65, R2_W, 1.5);
addCardHeader(s2, R2_X + 0.25, 3.85, "진단", 1.5);
addBullets(
  s2,
  R2_X + 0.3, 4.2, R2_W - 0.5, 0.85,
  [
    [{ text: "외부 패키지 데모가 빌드에 포함" }],
    [{ text: "asset import + 셰이더 컴파일 부하" }],
  ],
  10.5
);

// ═════════════════════════════════════════════════════════════════
// SLIDE 3 — 효과: 전·후 비교 + Key Insights
// ═════════════════════════════════════════════════════════════════
const s3 = pres.addSlide();
s3.background = { color: BG };
addHeader(s3, "02. 빌드 최적화 · 효과", "반복 사이클 단축으로 개발 속도 가속");

// Top card — large before/after horizontal comparison
const T3_X = 0.5, T3_Y = 1.55, T3_W = 9.0, T3_H = 1.9;
addCard(s3, T3_X, T3_Y, T3_W, T3_H);
addCardHeader(s3, T3_X + 0.3, T3_Y + 0.3, "전·후 비교", 1.8);
s3.addText("단축: -39 min  ·  -97.5%", {
  x: T3_X + 2.2, y: T3_Y + 0.28, w: 4, h: 0.32,
  fontFace: "Pretendard", fontSize: 11, color: MUTED,
  align: "left", valign: "middle", margin: 0,
});

const BARH_X = T3_X + 1.3;
const BARH_W_MAX = T3_W - 1.9;
const BARH_H = 0.32;

// Before
s3.addText("Before", {
  x: T3_X + 0.35, y: T3_Y + 0.85, w: 0.9, h: BARH_H,
  fontFace: "Pretendard", fontSize: 12, color: MUTED,
  bold: true, align: "left", valign: "middle", margin: 0,
});
s3.addShape(pres.shapes.RECTANGLE, {
  x: BARH_X, y: T3_Y + 0.85, w: BARH_W_MAX, h: BARH_H,
  fill: { color: DARK }, line: { color: DARK, width: 0 },
});
s3.addText("40 min", {
  x: BARH_X + BARH_W_MAX - 0.9, y: T3_Y + 0.83, w: 0.85, h: BARH_H,
  fontFace: "Pretendard", fontSize: 12, color: BG,
  bold: true, align: "right", valign: "middle", margin: 0,
});

// After
const afterW = BARH_W_MAX / 40;
s3.addText("After", {
  x: T3_X + 0.35, y: T3_Y + 1.35, w: 0.9, h: BARH_H,
  fontFace: "Pretendard", fontSize: 12, color: MUTED,
  bold: true, align: "left", valign: "middle", margin: 0,
});
s3.addShape(pres.shapes.RECTANGLE, {
  x: BARH_X, y: T3_Y + 1.35, w: afterW, h: BARH_H,
  fill: { color: PRIMARY }, line: { color: PRIMARY, width: 0 },
});
s3.addText("1 min", {
  x: BARH_X + afterW + 0.12, y: T3_Y + 1.33, w: 0.85, h: BARH_H,
  fontFace: "Pretendard", fontSize: 12, color: PRIMARY,
  bold: true, align: "left", valign: "middle", margin: 0,
});

// Bottom card — Key Insights
const B3_X = 0.5, B3_Y = 3.65, B3_W = 9.0, B3_H = 1.55;
addCard(s3, B3_X, B3_Y, B3_W, B3_H);
addCardHeader(s3, B3_X + 0.3, B3_Y + 0.25, "Key Insights", 2.5);

// 4 bullets in 2x2 grid
const colW = (B3_W - 0.7) / 2;
const rowH = (B3_H - 0.85) / 2;
const grid = [
  [
    { text: "클린 빌드 ", options: {} },
    { text: "40 → 1 min", options: { bold: true, color: PRIMARY } },
  ],
  [
    { text: "Demo asset ", options: {} },
    { text: "1,647 파일", options: { bold: true } },
    { text: " 정리", options: {} },
  ],
  [
    { text: "182만 줄", options: { bold: true } },
    { text: " dead code 제거", options: {} },
  ],
  [
    { text: "CI 반복 사이클 ", options: {} },
    { text: "97.5% 단축", options: { bold: true } },
  ],
];

grid.forEach((runs, i) => {
  const col = i % 2;
  const row = Math.floor(i / 2);
  const x = B3_X + 0.35 + col * (colW + 0.1);
  const y = B3_Y + 0.85 + row * rowH;
  s3.addText("•", {
    x, y, w: 0.2, h: rowH,
    fontFace: "Pretendard", fontSize: 14, color: DARK,
    bold: true, align: "left", valign: "middle", margin: 0,
  });
  s3.addText(runs, {
    x: x + 0.22, y, w: colW - 0.25, h: rowH,
    fontFace: "Pretendard", fontSize: 12, color: DARK,
    align: "left", valign: "middle", margin: 0,
  });
});

pres.writeFile({ fileName: "build_optimization_slide_v4.pptx" }).then((f) => {
  console.log("Saved:", f);
});
