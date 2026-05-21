// Build-time optimization 1-slide deck
// Palette: Midnight Executive — navy dominant, gold accent for hero stat
const pptxgen = require("pptxgenjs");

const pres = new pptxgen();
pres.layout = "LAYOUT_16x9"; // 10" x 5.625"
pres.title = "Build Time Optimization";

const NAVY = "0F1B3D";
const NAVY_LIGHT = "1E2761";
const ICE = "CADCFC";
const WHITE = "FFFFFF";
const GOLD = "F2C14E";
const MUTED = "8B97B8";
const CARD_BG = "1A2750";

const slide = pres.addSlide();
slide.background = { color: NAVY };

// ───────────────────────────────────────────── Title row
slide.addText("빌드 파이프라인 최적화", {
  x: 0.5, y: 0.35, w: 9, h: 0.5,
  fontFace: "Calibri", fontSize: 14, color: MUTED,
  bold: false, charSpacing: 4, margin: 0,
});

slide.addText("Build Time Optimization", {
  x: 0.5, y: 0.7, w: 9, h: 0.7,
  fontFace: "Georgia", fontSize: 32, color: WHITE,
  bold: true, margin: 0,
});

// ───────────────────────────────────────────── Left: hero stat (x: 0.5 ~ 5.0)
// "40 min  →  1 min"
slide.addText("40", {
  x: 0.5, y: 1.75, w: 1.5, h: 1.6,
  fontFace: "Georgia", fontSize: 96, color: WHITE,
  bold: true, align: "right", valign: "middle", margin: 0,
});
slide.addText("min", {
  x: 2.05, y: 2.55, w: 0.55, h: 0.5,
  fontFace: "Calibri", fontSize: 18, color: MUTED,
  align: "left", valign: "middle", margin: 0,
});

slide.addText("→", {
  x: 2.65, y: 1.95, w: 0.55, h: 1.2,
  fontFace: "Calibri", fontSize: 36, color: MUTED,
  align: "center", valign: "middle", margin: 0,
});

slide.addText("1", {
  x: 3.3, y: 1.75, w: 0.7, h: 1.6,
  fontFace: "Georgia", fontSize: 96, color: GOLD,
  bold: true, align: "center", valign: "middle", margin: 0,
});
slide.addText("min", {
  x: 4.05, y: 2.55, w: 0.6, h: 0.5,
  fontFace: "Calibri", fontSize: 18, color: GOLD,
  align: "left", valign: "middle", margin: 0,
});

// Caption under hero
slide.addText("Unity 클린 빌드 시간 · 약 40배 단축 (97.5% ↓)", {
  x: 0.5, y: 3.5, w: 4.5, h: 0.4,
  fontFace: "Calibri", fontSize: 12, color: ICE,
  italic: true, align: "left", margin: 0,
});

// Divider between hero and causes
slide.addShape(pres.shapes.LINE, {
  x: 5.2, y: 1.75, w: 0, h: 3.25,
  line: { color: NAVY_LIGHT, width: 1 },
});

// ───────────────────────────────────────────── Right: three cause cards (x: 5.5 ~ 9.5)
const cardX = 5.5;
const cardW = 4.0;
const cardH = 1.0;
const gap = 0.15;
const startY = 1.75;

const causes = [
  {
    n: "01",
    title: "Demo Asset 정리",
    desc: "TopDownEngine·MMTools demo 1,647 파일 / 182만 줄 제거",
  },
  {
    n: "02",
    title: "URP Shader Variant Stripping",
    desc: "SSAO · Soft Shadow · Reflection Probe · XR · HDR 등 25+ 항목 비활성",
  },
  {
    n: "03",
    title: "빌드 씬 정리",
    desc: "테스트 씬 제외 (9개 → 8개)",
  },
];

causes.forEach((c, i) => {
  const y = startY + i * (cardH + gap);

  // Card background
  slide.addShape(pres.shapes.RECTANGLE, {
    x: cardX, y: y, w: cardW, h: cardH,
    fill: { color: CARD_BG }, line: { color: CARD_BG, width: 0 },
  });

  // Left accent bar
  slide.addShape(pres.shapes.RECTANGLE, {
    x: cardX, y: y, w: 0.06, h: cardH,
    fill: { color: GOLD }, line: { color: GOLD, width: 0 },
  });

  // Number
  slide.addText(c.n, {
    x: cardX + 0.2, y: y + 0.05, w: 0.6, h: 0.9,
    fontFace: "Georgia", fontSize: 26, color: GOLD,
    bold: true, align: "left", valign: "middle", margin: 0,
  });

  // Title
  slide.addText(c.title, {
    x: cardX + 0.85, y: y + 0.1, w: cardW - 1.0, h: 0.38,
    fontFace: "Calibri", fontSize: 14, color: WHITE,
    bold: true, align: "left", valign: "middle", margin: 0,
  });

  // Description
  slide.addText(c.desc, {
    x: cardX + 0.85, y: y + 0.48, w: cardW - 1.0, h: 0.45,
    fontFace: "Calibri", fontSize: 10.5, color: ICE,
    align: "left", valign: "top", margin: 0,
  });
});

// ───────────────────────────────────────────── Footer caption
slide.addText("Built-in RP → URP 전환 + dead asset 제거 + variant pre-filtering 의 합", {
  x: 0.5, y: 5.25, w: 9, h: 0.3,
  fontFace: "Calibri", fontSize: 10, color: MUTED,
  italic: true, align: "left", margin: 0,
});

pres.writeFile({ fileName: "build_optimization_slide.pptx" }).then((f) => {
  console.log("Saved:", f);
});
