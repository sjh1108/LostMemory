// Build-time optimization 1-slide deck — v2 (visualized, audience-tuned for SSAFY web devs)
// Strategy: kill jargon (URP/shader variant), amplify the 40x story + raw scale numbers.
const pptxgen = require("pptxgenjs");

const pres = new pptxgen();
pres.layout = "LAYOUT_16x9"; // 10" x 5.625"
pres.title = "Build Time Optimization v2";

const NAVY = "0F1B3D";
const NAVY_LIGHT = "1E2761";
const ICE = "CADCFC";
const WHITE = "FFFFFF";
const GOLD = "F2C14E";
const MUTED = "8B97B8";
const RED_DIM = "C95D63";

const slide = pres.addSlide();
slide.background = { color: NAVY };

// ───────────────────────────────────────────── Title
slide.addText("BUILD TIME OPTIMIZATION", {
  x: 0.5, y: 0.3, w: 9, h: 0.32,
  fontFace: "Calibri", fontSize: 11, color: MUTED,
  bold: true, charSpacing: 8, margin: 0,
});
slide.addText("빌드 40분 → 1분", {
  x: 0.5, y: 0.62, w: 9, h: 0.55,
  fontFace: "Georgia", fontSize: 28, color: WHITE,
  bold: true, margin: 0,
});

// ───────────────────────────────────────────── Hero: Before/After horizontal bars
// Bar geometry — full width 8.0", proportional 40:1
const BAR_LEFT = 1.4;          // left edge of bar (after label)
const BAR_FULL = 7.6;          // Before bar width
const BAR_AFTER = BAR_FULL / 40; // After bar width (proportional)
const BAR_H = 0.5;

// Before row
slide.addText("Before", {
  x: 0.5, y: 1.5, w: 0.85, h: BAR_H,
  fontFace: "Calibri", fontSize: 12, color: MUTED,
  bold: true, align: "left", valign: "middle", margin: 0,
});
slide.addShape(pres.shapes.RECTANGLE, {
  x: BAR_LEFT, y: 1.5, w: BAR_FULL, h: BAR_H,
  fill: { color: RED_DIM }, line: { color: RED_DIM, width: 0 },
});
slide.addText("40 min", {
  x: BAR_LEFT + BAR_FULL - 1.2, y: 1.5, w: 1.1, h: BAR_H,
  fontFace: "Calibri", fontSize: 14, color: WHITE,
  bold: true, align: "right", valign: "middle", margin: 0,
});

// After row
slide.addText("After", {
  x: 0.5, y: 2.25, w: 0.85, h: BAR_H,
  fontFace: "Calibri", fontSize: 12, color: MUTED,
  bold: true, align: "left", valign: "middle", margin: 0,
});
slide.addShape(pres.shapes.RECTANGLE, {
  x: BAR_LEFT, y: 2.25, w: BAR_AFTER, h: BAR_H,
  fill: { color: GOLD }, line: { color: GOLD, width: 0 },
});
slide.addText("1 min", {
  x: BAR_LEFT + BAR_AFTER + 0.15, y: 2.25, w: 1.0, h: BAR_H,
  fontFace: "Calibri", fontSize: 14, color: GOLD,
  bold: true, align: "left", valign: "middle", margin: 0,
});

// "40× faster" hero callout (centered under bars)
slide.addText("40×", {
  x: 0.5, y: 3.0, w: 3.0, h: 1.0,
  fontFace: "Georgia", fontSize: 64, color: GOLD,
  bold: true, align: "right", valign: "middle", margin: 0,
});
slide.addText("faster", {
  x: 3.55, y: 3.25, w: 2.0, h: 0.55,
  fontFace: "Calibri", fontSize: 22, color: WHITE,
  bold: true, align: "left", valign: "middle", margin: 0,
});
slide.addText("· 97.5% 감소", {
  x: 3.55, y: 3.75, w: 2.5, h: 0.35,
  fontFace: "Calibri", fontSize: 12, color: MUTED,
  italic: true, align: "left", valign: "middle", margin: 0,
});

// ───────────────────────────────────────────── Bottom stat row
// Divider above the stats
slide.addShape(pres.shapes.LINE, {
  x: 0.7, y: 4.35, w: 8.6, h: 0,
  line: { color: NAVY_LIGHT, width: 1 },
});

// Section label
slide.addText("원인  ·  안 쓰는 demo asset 정리", {
  x: 0.5, y: 4.45, w: 9, h: 0.3,
  fontFace: "Calibri", fontSize: 11, color: MUTED,
  bold: true, charSpacing: 2, align: "center", valign: "middle", margin: 0,
});

// Stat 1 — files removed (left)
slide.addText("1,647", {
  x: 0.5, y: 4.75, w: 4.4, h: 0.7,
  fontFace: "Georgia", fontSize: 50, color: GOLD,
  bold: true, align: "right", valign: "middle", margin: 0,
});
slide.addText("파일 제거", {
  x: 0.5, y: 5.18, w: 4.4, h: 0.3,
  fontFace: "Calibri", fontSize: 11, color: ICE,
  align: "right", valign: "middle", margin: 0,
});

// Center divider between stats
slide.addShape(pres.shapes.LINE, {
  x: 5.0, y: 4.8, w: 0, h: 0.65,
  line: { color: NAVY_LIGHT, width: 1 },
});

// Stat 2 — lines removed (right)
slide.addText("182만", {
  x: 5.1, y: 4.75, w: 4.4, h: 0.7,
  fontFace: "Georgia", fontSize: 50, color: GOLD,
  bold: true, align: "left", valign: "middle", margin: 0,
});
slide.addText("줄 제거", {
  x: 5.1, y: 5.18, w: 4.4, h: 0.3,
  fontFace: "Calibri", fontSize: 11, color: ICE,
  align: "left", valign: "middle", margin: 0,
});

pres.writeFile({ fileName: "build_optimization_slide_v2.pptx" }).then((f) => {
  console.log("Saved:", f);
});
