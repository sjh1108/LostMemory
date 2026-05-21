// 코옵 서버 연동 — 1장 (로스트메모리 최종발표 디자인 톤)
// 레이아웃: "구조" 박스 1개에 다이어그램 + 각 요소에 부착된 라벨
//  ① 방 매칭 · REST API + JWT      → REST 화살표 위쪽
//  ② 실시간 동기화 · 자체 UDP Relay → UDP 화살표 아래쪽
//  ③ 왜 자체 Relay? · 통합 + 비용    → Spring 박스 바로 아래
// 참고 디자인: C:\Users\SSAFY\Downloads\로스트 메모리 최종발표.pdf

const pptxgen = require("pptxgenjs");

const pres = new pptxgen();
pres.layout = "LAYOUT_16x9"; // 10 x 5.625
pres.author = "LostMemory Team";
pres.title = "코옵 서버 연동";

// ── 팔레트 ──────────────────────────────────────────
const C = {
  blue: "1E63FF",
  ink: "111111",
  text: "1F2937",
  mute: "6B7280",
  box: "F1F1F1",
  boxLine: "E5E5E5",
  white: "FFFFFF",
  red: "EF4444",
};
const FH = "맑은 고딕";
const FB = "맑은 고딕";
const FM = "Consolas";

const s = pres.addSlide();
s.background = { color: C.white };

// ── 헤더: 킥커 + 제목 ────────────────────────────────
s.addText("03. 멀티플레이", {
  x: 0.6, y: 0.4, w: 6, h: 0.32,
  fontFace: FB, fontSize: 12, color: C.blue, bold: true, margin: 0,
});
s.addText("코옵 서버 연동", {
  x: 0.6, y: 0.68, w: 8, h: 0.7,
  fontFace: FH, fontSize: 36, color: C.ink, bold: true, margin: 0,
});

// ── 알약 헤더 헬퍼 ───────────────────────────────────
function pillHeader(slide, x, y, w, label) {
  const h = 0.5;
  slide.addShape(pres.shapes.ROUNDED_RECTANGLE, {
    x, y, w, h, rectRadius: h / 2,
    fill: { color: C.white }, line: { color: C.white, width: 0 },
    shadow: { type: "outer", color: "000000", opacity: 0.06, blur: 6, offset: 1, angle: 90 },
  });
  const cd = 0.34, cx = x + 0.1, cy = y + (h - cd) / 2;
  slide.addShape(pres.shapes.OVAL, {
    x: cx, y: cy, w: cd, h: cd,
    fill: { color: C.blue }, line: { color: C.blue, width: 0 },
  });
  slide.addText(">", {
    x: cx, y: cy - 0.04, w: cd, h: cd,
    fontFace: "Arial", fontSize: 13, color: C.white, bold: true,
    align: "center", valign: "middle", margin: 0,
  });
  slide.addText(label, {
    x: x + cd + 0.15, y, w: w - cd - 0.3, h,
    fontFace: FH, fontSize: 14, color: C.ink, bold: true,
    align: "center", valign: "middle", margin: 0,
  });
}

// ╔══════════════════════════════════════════════════════
// ║ 큰 "구조" 박스
// ╚══════════════════════════════════════════════════════
const Bx = 0.6, By = 1.55, Bw = 8.8, Bh = 3.7;
s.addShape(pres.shapes.ROUNDED_RECTANGLE, {
  x: Bx, y: By, w: Bw, h: Bh, rectRadius: 0.2,
  fill: { color: C.box }, line: { color: C.boxLine, width: 0.75 },
});
pillHeader(s, Bx + (Bw - 1.6) / 2, By + 0.22, 1.6, "구조");

// ╔══════════════════════════════════════════════════════
// ║ 다이어그램 좌표 계산
// ╚══════════════════════════════════════════════════════
const cy = 3.75;           // 다이어그램 세로 중심
const arrowY1 = cy - 0.35; // REST (위)
const arrowY2 = cy + 0.35; // UDP (아래)

// Host (좌)
const hd = 0.9;
const hx = Bx + 0.55, hy = cy - hd / 2;
s.addShape(pres.shapes.OVAL, {
  x: hx, y: hy, w: hd, h: hd,
  fill: { color: C.blue }, line: { color: C.blue, width: 0 },
});
s.addText("Host", {
  x: hx, y: hy, w: hd, h: hd,
  fontFace: FH, fontSize: 13, color: C.white, bold: true,
  align: "center", valign: "middle", margin: 0,
});

// Guest (우)
const gd = 0.9;
const gx = Bx + Bw - 0.55 - gd, gy = cy - gd / 2;
s.addShape(pres.shapes.OVAL, {
  x: gx, y: gy, w: gd, h: gd,
  fill: { color: C.ink }, line: { color: C.ink, width: 0 },
});
s.addText("Guest", {
  x: gx, y: gy, w: gd, h: gd,
  fontFace: FH, fontSize: 12, color: C.white, bold: true,
  align: "center", valign: "middle", margin: 0,
});

// Spring (중앙)
const spW = 1.7, spH = 1.25;
const spX = Bx + Bw / 2 - spW / 2, spY = cy - spH / 2;
s.addShape(pres.shapes.ROUNDED_RECTANGLE, {
  x: spX, y: spY, w: spW, h: spH, rectRadius: 0.1,
  fill: { color: C.white }, line: { color: C.ink, width: 1.25 },
});
s.addText("Spring", {
  x: spX, y: spY + 0.15, w: spW, h: 0.4,
  fontFace: FH, fontSize: 16, color: C.ink, bold: true,
  align: "center", margin: 0,
});
s.addText("Backend", {
  x: spX, y: spY + 0.55, w: spW, h: 0.3,
  fontFace: FB, fontSize: 11, color: C.mute,
  align: "center", margin: 0,
});
s.addShape(pres.shapes.LINE, {
  x: spX + 0.35, y: spY + 0.9, w: spW - 0.7, h: 0,
  line: { color: C.boxLine, width: 0.75 },
});
s.addText("JWT 인증", {
  x: spX, y: spY + 0.93, w: spW, h: 0.28,
  fontFace: FB, fontSize: 10, color: C.mute,
  align: "center", margin: 0,
});

// Unity NGO 칩 — Spring 박스 위에 작게
const nW = 1.3, nH = 0.38;
const nX = spX + (spW - nW) / 2, nY = spY - nH - 0.1;
s.addShape(pres.shapes.ROUNDED_RECTANGLE, {
  x: nX, y: nY, w: nW, h: nH, rectRadius: 0.08,
  fill: { color: C.white }, line: { color: C.mute, width: 0.75 },
});
s.addText("Unity · NGO", {
  x: nX, y: nY, w: nW, h: nH,
  fontFace: FB, fontSize: 9.5, color: C.ink, bold: true,
  align: "center", valign: "middle", margin: 0,
});
s.addShape(pres.shapes.LINE, {
  x: spX + spW / 2, y: nY + nH, w: 0, h: spY - (nY + nH),
  line: { color: C.mute, width: 0.75, dashType: "dash" },
});

// ── 화살표 (REST 위, UDP 아래 — 작은 라벨 글자 제거, 통합 라벨로 대체) ──
// Host → Spring REST
s.addShape(pres.shapes.LINE, {
  x: hx + hd + 0.05, y: arrowY1, w: spX - (hx + hd) - 0.1, h: 0,
  line: { color: C.blue, width: 2, endArrowType: "triangle", beginArrowType: "triangle" },
});
// Spring → Guest REST
s.addShape(pres.shapes.LINE, {
  x: spX + spW + 0.05, y: arrowY1, w: gx - (spX + spW) - 0.1, h: 0,
  line: { color: C.blue, width: 2, endArrowType: "triangle", beginArrowType: "triangle" },
});
// Host → Spring UDP
s.addShape(pres.shapes.LINE, {
  x: hx + hd + 0.05, y: arrowY2, w: spX - (hx + hd) - 0.1, h: 0,
  line: { color: C.red, width: 2, dashType: "dash", endArrowType: "triangle", beginArrowType: "triangle" },
});
// Spring → Guest UDP
s.addShape(pres.shapes.LINE, {
  x: spX + spW + 0.05, y: arrowY2, w: gx - (spX + spW) - 0.1, h: 0,
  line: { color: C.red, width: 2, dashType: "dash", endArrowType: "triangle", beginArrowType: "triangle" },
});

// ╔══════════════════════════════════════════════════════
// ║ 라벨 헬퍼 — 흰 라운드 박스 + 좌측 컬러 점 + 헤더·서브 한 줄
// ╚══════════════════════════════════════════════════════
function labelBar(slide, x, y, w, h, color, head, sub) {
  slide.addShape(pres.shapes.ROUNDED_RECTANGLE, {
    x, y, w, h, rectRadius: 0.1,
    fill: { color: C.white }, line: { color: color, width: 1.25 },
    shadow: { type: "outer", color: "000000", opacity: 0.05, blur: 4, offset: 1, angle: 90 },
  });
  // 좌측 컬러 점
  const dotD = 0.14;
  slide.addShape(pres.shapes.OVAL, {
    x: x + 0.18, y: y + (h - dotD) / 2, w: dotD, h: dotD,
    fill: { color }, line: { color, width: 0 },
  });
  slide.addText([
    { text: head, options: { color: C.ink, bold: true, fontSize: 13.5 } },
    { text: "   " + sub, options: { color, bold: true, fontSize: 11.5 } },
  ], {
    x: x + 0.42, y, w: w - 0.5, h,
    fontFace: FH, valign: "middle", margin: 0,
  });
}

// ── 라벨 ① — REST 위쪽 (다이어그램 위) ──
const L1w = 4.4, L1h = 0.5;
const L1x = Bx + (Bw - L1w) / 2;
const L1y = arrowY1 - L1h - 0.2;
labelBar(s, L1x, L1y, L1w, L1h, C.blue, "방 매칭", "REST API + JWT");

// ── 라벨 ② — UDP 아래쪽 (다이어그램 아래) ──
const L2w = 4.7, L2h = 0.5;
const L2x = Bx + (Bw - L2w) / 2;
const L2y = arrowY2 + 0.2;
labelBar(s, L2x, L2y, L2w, L2h, C.red, "실시간 동기화", "자체 UDP Relay");

// ── 라벨 ③ — Spring 박스 바로 아래 (작은 캡션) ──
const L3w = spW + 0.4, L3h = 0.35;
const L3x = spX - 0.2;
const L3y = spY + spH + 0.05;
s.addText([
  { text: "왜 자체 Relay?", options: { color: C.ink, bold: true, fontSize: 10.5 } },
  { text: "  통합 + 비용", options: { color: C.blue, bold: true, fontSize: 10.5 } },
], {
  x: L3x, y: L3y, w: L3w, h: L3h,
  fontFace: FH, align: "center", valign: "middle", margin: 0,
});
// Spring 박스 → ③ 라벨 점선 연결
s.addShape(pres.shapes.LINE, {
  x: spX + spW / 2, y: spY + spH, w: 0, h: L3y - (spY + spH),
  line: { color: C.mute, width: 0.5, dashType: "dot" },
});

// ─────────────────────────────────────────────────────
pres.writeFile({ fileName: "C:/ssafy/free_project/S14P31C201/client/docs/khi/coop_server_lostmemory_v2.pptx" })
  .then((f) => console.log("OK:", f))
  .catch((e) => { console.error(e); process.exit(1); });
