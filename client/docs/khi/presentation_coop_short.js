// 코옵 멀티플레이 서버 연동 — 1장 압축 버전
// 청중: SSAFY 웹 개발 희망자 / 컨설턴트 / 실습코치 (60~90초 분량)

const pptxgen = require("pptxgenjs");

const pres = new pptxgen();
pres.layout = "LAYOUT_16x9"; // 10 x 5.625
pres.author = "LostMemory Team";
pres.title = "코옵 멀티플레이 서버 연동 (요약본)";

// ── Palette: Ocean Gradient (8장 버전과 동일) ────────
const C = {
  deep: "065A82",
  teal: "1C7293",
  midnight: "21295C",
  ink: "0F172A",
  mute: "64748B",
  line: "E2E8F0",
  bg: "FFFFFF",
  band: "F1F5F9",
  accent: "F59E0B",
};

const FH = "맑은 고딕";
const FB = "맑은 고딕";
const FM = "Consolas";

const s = pres.addSlide();
s.background = { color: C.bg };

// ── 제목 영역 ────────────────────────────────────────
s.addText("LOSTMEMORY · MULTIPLAYER", {
  x: 0.5, y: 0.3, w: 9, h: 0.28,
  fontFace: FB, fontSize: 10.5, color: C.teal, bold: true,
  charSpacing: 5, margin: 0,
});
s.addText("코옵 멀티플레이 서버 연동", {
  x: 0.5, y: 0.55, w: 9, h: 0.55,
  fontFace: FH, fontSize: 26, color: C.ink, bold: true, margin: 0,
});

// ╔══════════════════════════════════════════════════════
// ║ 좌측: 아키텍처 다이어그램  (x:0.5 ~ 5.4, y:1.3 ~ 4.7)
// ╚══════════════════════════════════════════════════════
const dx = 0.5, dy = 1.3, dw = 4.9, dh = 3.4;

// 배경 박스
s.addShape(pres.shapes.RECTANGLE, {
  x: dx, y: dy, w: dw, h: dh,
  fill: { color: C.band }, line: { color: C.line, width: 0.75 },
});

s.addText("ARCHITECTURE", {
  x: dx + 0.2, y: dy + 0.15, w: dw - 0.4, h: 0.25,
  fontFace: FB, fontSize: 9.5, color: C.teal, bold: true,
  charSpacing: 4, margin: 0,
});

// Host (좌상)
s.addShape(pres.shapes.OVAL, {
  x: dx + 0.35, y: dy + 0.65, w: 1.0, h: 1.0,
  fill: { color: C.deep }, line: { color: C.deep, width: 0 },
});
s.addText("Host", {
  x: dx + 0.35, y: dy + 0.7, w: 1.0, h: 0.45,
  fontFace: FH, fontSize: 13, color: "FFFFFF", bold: true,
  align: "center", valign: "middle", margin: 0,
});
s.addText("Unity", {
  x: dx + 0.35, y: dy + 1.05, w: 1.0, h: 0.4,
  fontFace: FB, fontSize: 9, color: "CADCFC",
  align: "center", valign: "middle", margin: 0,
});

// Guest (좌하)
s.addShape(pres.shapes.OVAL, {
  x: dx + 0.35, y: dy + 2.2, w: 1.0, h: 1.0,
  fill: { color: C.teal }, line: { color: C.teal, width: 0 },
});
s.addText("Guest", {
  x: dx + 0.35, y: dy + 2.25, w: 1.0, h: 0.45,
  fontFace: FH, fontSize: 13, color: "FFFFFF", bold: true,
  align: "center", valign: "middle", margin: 0,
});
s.addText("Unity", {
  x: dx + 0.35, y: dy + 2.6, w: 1.0, h: 0.4,
  fontFace: FB, fontSize: 9, color: "CADCFC",
  align: "center", valign: "middle", margin: 0,
});

// Spring 중앙 박스
const sx = dx + 2.4, sy = dy + 1.0, sw = 1.6, sh = 1.4;
s.addShape(pres.shapes.ROUNDED_RECTANGLE, {
  x: sx, y: sy, w: sw, h: sh, rectRadius: 0.08,
  fill: { color: C.midnight }, line: { color: C.midnight, width: 0 },
});
s.addText("Spring", {
  x: sx, y: sy + 0.15, w: sw, h: 0.35,
  fontFace: FH, fontSize: 13, color: "FFFFFF", bold: true,
  align: "center", margin: 0,
});
s.addText("Backend", {
  x: sx, y: sy + 0.48, w: sw, h: 0.28,
  fontFace: FB, fontSize: 10, color: "CADCFC",
  align: "center", margin: 0,
});
s.addShape(pres.shapes.LINE, {
  x: sx + 0.3, y: sy + 0.82, w: sw - 0.6, h: 0,
  line: { color: C.accent, width: 0.75 },
});
s.addText("REST · JWT", {
  x: sx, y: sy + 0.85, w: sw, h: 0.25,
  fontFace: FM, fontSize: 8.5, color: "97AEDC",
  align: "center", margin: 0,
});
s.addText("UDP Relay", {
  x: sx, y: sy + 1.08, w: sw, h: 0.25,
  fontFace: FM, fontSize: 8.5, color: C.accent,
  align: "center", margin: 0,
});

// NGO 라벨 (우측)
const nx = dx + 4.2, ny = dy + 1.45, nw = 0.6, nh = 0.5;
s.addShape(pres.shapes.ROUNDED_RECTANGLE, {
  x: nx, y: ny, w: nw, h: nh, rectRadius: 0.06,
  fill: { color: "FFFFFF" }, line: { color: C.mute, width: 0.75 },
});
s.addText("NGO", {
  x: nx, y: ny, w: nw, h: nh,
  fontFace: FB, fontSize: 9, color: C.ink, bold: true,
  align: "center", valign: "middle", margin: 0,
});

// Spring ↔ NGO 짧은 연결선
s.addShape(pres.shapes.LINE, {
  x: sx + sw, y: sy + sh / 2, w: nx - (sx + sw), h: 0,
  line: { color: C.mute, width: 0.75 },
});

// ── 화살표: Host → Spring ────────────────────────────
// REST (실선)
s.addShape(pres.shapes.LINE, {
  x: dx + 1.4, y: dy + 1.05, w: sx - (dx + 1.4) - 0.05, h: (sy + 0.4) - (dy + 1.05),
  line: { color: C.deep, width: 1.75, endArrowType: "triangle", beginArrowType: "triangle" },
});
s.addText("REST", {
  x: dx + 1.45, y: dy + 0.75, w: 0.9, h: 0.25,
  fontFace: FM, fontSize: 9, color: C.deep, bold: true, margin: 0,
});
// UDP (점선)
s.addShape(pres.shapes.LINE, {
  x: dx + 1.4, y: dy + 1.35, w: sx - (dx + 1.4) - 0.05, h: (sy + sh - 0.35) - (dy + 1.35),
  line: { color: C.accent, width: 1.75, dashType: "dash", endArrowType: "triangle", beginArrowType: "triangle" },
});
s.addText("UDP", {
  x: dx + 1.45, y: dy + 1.55, w: 0.9, h: 0.25,
  fontFace: FM, fontSize: 9, color: C.accent, bold: true, margin: 0,
});

// ── 화살표: Guest → Spring ───────────────────────────
// REST (실선)
s.addShape(pres.shapes.LINE, {
  x: dx + 1.4, y: dy + 2.6, w: sx - (dx + 1.4) - 0.05, h: (sy + 0.45) - (dy + 2.6),
  line: { color: C.teal, width: 1.75, endArrowType: "triangle", beginArrowType: "triangle" },
});
s.addText("REST", {
  x: dx + 1.45, y: dy + 2.65, w: 0.9, h: 0.25,
  fontFace: FM, fontSize: 9, color: C.teal, bold: true, margin: 0,
});
// UDP (점선)
s.addShape(pres.shapes.LINE, {
  x: dx + 1.4, y: dy + 2.85, w: sx - (dx + 1.4) - 0.05, h: (sy + sh - 0.3) - (dy + 2.85),
  line: { color: C.accent, width: 1.75, dashType: "dash", endArrowType: "triangle", beginArrowType: "triangle" },
});
s.addText("UDP", {
  x: dx + 1.45, y: dy + 2.9, w: 0.9, h: 0.25,
  fontFace: FM, fontSize: 9, color: C.accent, bold: true, margin: 0,
});

// 다이어그램 하단 캡션
s.addText("실선 = REST API   ·   점선 = UDP 실시간 중계", {
  x: dx + 0.2, y: dy + dh - 0.4, w: dw - 0.4, h: 0.28,
  fontFace: FB, fontSize: 9.5, color: C.mute, italic: true,
  align: "center", margin: 0,
});

// ╔══════════════════════════════════════════════════════
// ║ 우측: 3개 핵심 포인트  (x:5.6 ~ 9.5, y:1.3 ~ 4.7)
// ╚══════════════════════════════════════════════════════
const cards = [
  {
    n: "01",
    color: C.deep,
    title: "방 매칭",
    sub: "REST API + JWT",
    body: "POST /sessions 로 방 생성, 6자리 코드로 친구 입장",
  },
  {
    n: "02",
    color: C.teal,
    title: "실시간 동기화",
    sub: "자체 UDP Relay + 호스트 권위",
    body: "초당 ~20회 패킷 교환, 게임 판정은 호스트가 담당",
  },
  {
    n: "03",
    color: C.accent,
    title: "왜 자체 Relay?",
    sub: "통합과 비용",
    body: "인증·LLM·게임 로직을 같은 Spring 백엔드에 일원화",
  },
];

const cx = 5.6, cw = 3.9, cyStart = 1.3, ch = 1.05, cgap = 0.13;
cards.forEach((c, i) => {
  const y = cyStart + (ch + cgap) * i;
  // 카드 배경
  s.addShape(pres.shapes.RECTANGLE, {
    x: cx, y, w: cw, h: ch,
    fill: { color: C.band }, line: { color: C.line, width: 0.75 },
  });
  // 좌측 액센트 바
  s.addShape(pres.shapes.RECTANGLE, {
    x: cx, y, w: 0.08, h: ch,
    fill: { color: c.color }, line: { color: c.color, width: 0 },
  });
  // 번호 OVAL
  s.addShape(pres.shapes.OVAL, {
    x: cx + 0.22, y: y + 0.2, w: 0.6, h: 0.6,
    fill: { color: c.color }, line: { color: c.color, width: 0 },
  });
  s.addText(c.n, {
    x: cx + 0.22, y: y + 0.2, w: 0.6, h: 0.6,
    fontFace: FH, fontSize: 12, color: "FFFFFF", bold: true,
    align: "center", valign: "middle", margin: 0,
  });
  // 제목 + 서브
  s.addText(c.title, {
    x: cx + 0.95, y: y + 0.12, w: cw - 1.1, h: 0.35,
    fontFace: FH, fontSize: 14, color: C.ink, bold: true, margin: 0,
  });
  s.addText(c.sub, {
    x: cx + 0.95, y: y + 0.43, w: cw - 1.1, h: 0.28,
    fontFace: FM, fontSize: 10, color: c.color, bold: true, margin: 0,
  });
  // 본문
  s.addText(c.body, {
    x: cx + 0.95, y: y + 0.68, w: cw - 1.1, h: 0.35,
    fontFace: FB, fontSize: 10.5, color: C.mute, margin: 0,
  });
});

// ╔══════════════════════════════════════════════════════
// ║ 하단: 한 줄 요약 띠
// ╚══════════════════════════════════════════════════════
const by = 4.85, bh = 0.55;
s.addShape(pres.shapes.RECTANGLE, {
  x: 0.5, y: by, w: 9, h: bh,
  fill: { color: C.midnight }, line: { color: C.midnight, width: 0 },
});
s.addShape(pres.shapes.RECTANGLE, {
  x: 0.5, y: by, w: 0.08, h: bh,
  fill: { color: C.accent }, line: { color: C.accent, width: 0 },
});
s.addText([
  { text: "한 줄 요약  ", options: { color: C.accent, bold: true, fontSize: 10, charSpacing: 3 } },
  { text: "방 매칭은 REST + JWT, 실시간은 자체 UDP — 모두 같은 Spring 백엔드", options: { color: "FFFFFF", bold: true, fontSize: 13 } },
], {
  x: 0.75, y: by, w: 8.7, h: bh,
  fontFace: FH, valign: "middle", margin: 0,
});

// ─────────────────────────────────────────────────────
pres.writeFile({ fileName: "C:/ssafy/free_project/S14P31C201/client/docs/khi/coop_server_short.pptx" })
  .then((f) => console.log("OK:", f))
  .catch((e) => { console.error(e); process.exit(1); });
