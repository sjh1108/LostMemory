// Build-time optimization slide v3 — replicate "Lost Memory final presentation" Style B
// Reference: p50 (매출 및 실적 분석), p52 (트렌드 분석) of 로스트 메모리 최종발표.pdf
// Canva-style: white background, blue accent, gray rounded cards, capsule-ish bars.
const pptxgen = require("pptxgenjs");

const pres = new pptxgen();
pres.layout = "LAYOUT_16x9"; // 10" x 5.625"
pres.title = "Build Time Optimization v3";

// Style B palette (extracted from the PDF)
const BG = "FFFFFF";
const PRIMARY = "1A6CFA";
const DARK = "1F1F1F";
const MUTED = "8E8E93";
const CARD = "F2F2F2";
const CARD_BORDER = "E5E5E5";

const slide = pres.addSlide();
slide.background = { color: BG };

// ───────────────────────────────────────────── Header
slide.addText("02. 빌드 최적화", {
  x: 0.5, y: 0.45, w: 6, h: 0.3,
  fontFace: "Pretendard", fontSize: 11, color: PRIMARY,
  bold: true, margin: 0,
});

slide.addText("빌드 시간 40배 단축", {
  x: 0.5, y: 0.75, w: 9, h: 0.6,
  fontFace: "Pretendard", fontSize: 28, color: DARK,
  bold: true, margin: 0,
});

// ───────────────────────────────────────────── Helper: card sub-header (blue circle + > + label)
function addCardHeader(s, x, y, label, opts = {}) {
  const labelW = opts.labelW || 3.0;
  // Blue circle
  s.addShape(pres.shapes.OVAL, {
    x: x, y: y, w: 0.28, h: 0.28,
    fill: { color: PRIMARY }, line: { color: PRIMARY, width: 0 },
  });
  // ">" arrow inside circle
  s.addText(">", {
    x: x, y: y - 0.02, w: 0.28, h: 0.28,
    fontFace: "Calibri", fontSize: 12, color: BG,
    bold: true, align: "center", valign: "middle", margin: 0,
  });
  // Label text
  s.addText(label, {
    x: x + 0.38, y: y - 0.02, w: labelW, h: 0.32,
    fontFace: "Pretendard", fontSize: 14, color: DARK,
    bold: true, align: "left", valign: "middle", margin: 0,
  });
}

// ───────────────────────────────────────────── Left card: bar chart "Before vs After"
const LEFT_X = 0.5, LEFT_Y = 1.55, LEFT_W = 5.0, LEFT_H = 3.6;
slide.addShape(pres.shapes.ROUNDED_RECTANGLE, {
  x: LEFT_X, y: LEFT_Y, w: LEFT_W, h: LEFT_H,
  fill: { color: CARD }, line: { color: CARD_BORDER, width: 0.75 },
  rectRadius: 0.12,
});

addCardHeader(slide, LEFT_X + 0.3, LEFT_Y + 0.3, "Before vs After  빌드 시간(분)", { labelW: 4.5 });

// Bar chart (column, 2 categories) — per-point coloring via single-series + chartColors array
slide.addChart(
  pres.charts.BAR,
  [
    { name: "빌드 시간", labels: ["Before", "After"], values: [40, 1] },
  ],
  {
    x: LEFT_X + 0.25, y: LEFT_Y + 0.75, w: LEFT_W - 0.5, h: LEFT_H - 1.0,
    barDir: "col",
    chartColors: [DARK, PRIMARY],
    chartColorsOpacity: 100,
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

// Color the After bar blue by adding a covering rectangle? pptxgenjs limits per-point coloring.
// Workaround: use 2 series so we can color independently.

// ───────────────────────────────────────────── Right top card: Key Insights
const RT_X = 5.7, RT_Y = 1.55, RT_W = 3.8, RT_H = 1.85;
slide.addShape(pres.shapes.ROUNDED_RECTANGLE, {
  x: RT_X, y: RT_Y, w: RT_W, h: RT_H,
  fill: { color: CARD }, line: { color: CARD_BORDER, width: 0.75 },
  rectRadius: 0.12,
});

addCardHeader(slide, RT_X + 0.25, RT_Y + 0.25, "Key Insights", { labelW: 2.5 });

// Each bullet line as separate addText for reliable rendering
const bulletLines = [
  [
    { text: "Demo asset " },
    { text: "1,647 파일", options: { bold: true } },
    { text: " 제거" },
  ],
  [
    { text: "182만 줄", options: { bold: true } },
    { text: " dead code 정리" },
  ],
  [
    { text: "클린 빌드 " },
    { text: "40 → 1 min", options: { bold: true, color: PRIMARY } },
  ],
  [
    { text: "CI 반복 사이클 " },
    { text: "97.5% 단축", options: { bold: true } },
  ],
];

const bulletStartY = RT_Y + 0.72;
const bulletLineH = 0.27;
bulletLines.forEach((runs, i) => {
  // Bullet dot
  slide.addText("•", {
    x: RT_X + 0.32, y: bulletStartY + i * bulletLineH, w: 0.2, h: bulletLineH,
    fontFace: "Pretendard", fontSize: 14, color: DARK,
    bold: true, align: "left", valign: "middle", margin: 0,
  });
  // Line content
  slide.addText(runs, {
    x: RT_X + 0.55, y: bulletStartY + i * bulletLineH, w: RT_W - 0.7, h: bulletLineH,
    fontFace: "Pretendard", fontSize: 12, color: DARK,
    align: "left", valign: "middle", margin: 0,
  });
});

// ───────────────────────────────────────────── Right bottom card: before/after comparison bar
const RB_X = 5.7, RB_Y = 3.55, RB_W = 3.8, RB_H = 1.6;
slide.addShape(pres.shapes.ROUNDED_RECTANGLE, {
  x: RB_X, y: RB_Y, w: RB_W, h: RB_H,
  fill: { color: CARD }, line: { color: CARD_BORDER, width: 0.75 },
  rectRadius: 0.12,
});

addCardHeader(slide, RB_X + 0.25, RB_Y + 0.25, "전·후 비교", { labelW: 1.5 });
slide.addText("단축: -39 min  (-97.5%)", {
  x: RB_X + 1.85, y: RB_Y + 0.23, w: 1.8, h: 0.32,
  fontFace: "Pretendard", fontSize: 10, color: MUTED,
  align: "right", valign: "middle", margin: 0,
});

// Horizontal bars
const BARH_X = RB_X + 0.7;
const BARH_W_MAX = RB_W - 1.55;
const BARH_H = 0.22;

// Before row
slide.addText("Before", {
  x: RB_X + 0.2, y: RB_Y + 0.7, w: 0.55, h: BARH_H,
  fontFace: "Pretendard", fontSize: 10, color: MUTED,
  bold: true, align: "left", valign: "middle", margin: 0,
});
slide.addShape(pres.shapes.RECTANGLE, {
  x: BARH_X, y: RB_Y + 0.72, w: BARH_W_MAX, h: BARH_H,
  fill: { color: DARK }, line: { color: DARK, width: 0 },
});
slide.addText("40 min", {
  x: BARH_X + BARH_W_MAX - 0.7, y: RB_Y + 0.71, w: 0.7, h: BARH_H,
  fontFace: "Pretendard", fontSize: 10, color: BG,
  bold: true, align: "right", valign: "middle", margin: 0,
});

// After row
const afterW = BARH_W_MAX / 40;
slide.addText("After", {
  x: RB_X + 0.2, y: RB_Y + 1.1, w: 0.55, h: BARH_H,
  fontFace: "Pretendard", fontSize: 10, color: MUTED,
  bold: true, align: "left", valign: "middle", margin: 0,
});
slide.addShape(pres.shapes.RECTANGLE, {
  x: BARH_X, y: RB_Y + 1.12, w: afterW, h: BARH_H,
  fill: { color: PRIMARY }, line: { color: PRIMARY, width: 0 },
});
slide.addText("1 min", {
  x: BARH_X + afterW + 0.1, y: RB_Y + 1.11, w: 0.7, h: BARH_H,
  fontFace: "Pretendard", fontSize: 10, color: PRIMARY,
  bold: true, align: "left", valign: "middle", margin: 0,
});

pres.writeFile({ fileName: "build_optimization_slide_v3.pptx" }).then((f) => {
  console.log("Saved:", f);
});
