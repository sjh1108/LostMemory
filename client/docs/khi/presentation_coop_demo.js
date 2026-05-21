// 코옵 멀티플레이 서버 연동 — 발표용 샘플 PPT
// 청중: SSAFY 웹 개발 희망자 / 컨설턴트 / 실습코치

const pptxgen = require("pptxgenjs");

const pres = new pptxgen();
pres.layout = "LAYOUT_16x9"; // 10 x 5.625
pres.author = "LostMemory Team";
pres.title = "코옵 멀티플레이 서버 연동";

// ── Palette: Ocean Gradient ──────────────────────────
const C = {
  deep: "065A82",     // primary deep blue
  teal: "1C7293",     // secondary teal
  midnight: "21295C", // dark accent
  ink: "0F172A",      // text on light
  mute: "64748B",     // muted text
  line: "E2E8F0",     // hairline
  bg: "FFFFFF",       // light bg
  band: "F1F5F9",     // light band
  accent: "F59E0B",   // warm accent (numbers / highlight)
};

const FH = "맑은 고딕";   // header
const FB = "맑은 고딕";   // body
const FM = "Consolas";   // mono / endpoints

// ── 공통 헬퍼 ────────────────────────────────────────
function pageFooter(slide, page, total) {
  slide.addShape(pres.shapes.LINE, {
    x: 0.5, y: 5.25, w: 9, h: 0,
    line: { color: C.line, width: 0.5 },
  });
  slide.addText("LostMemory · 코옵 서버 연동", {
    x: 0.5, y: 5.32, w: 5, h: 0.25,
    fontFace: FB, fontSize: 9, color: C.mute,
  });
  slide.addText(`${page} / ${total}`, {
    x: 8.5, y: 5.32, w: 1, h: 0.25,
    fontFace: FB, fontSize: 9, color: C.mute, align: "right",
  });
}

function slideTitle(slide, kicker, title) {
  slide.addText(kicker, {
    x: 0.5, y: 0.35, w: 9, h: 0.3,
    fontFace: FB, fontSize: 11, color: C.teal, bold: true,
    charSpacing: 4, margin: 0,
  });
  slide.addText(title, {
    x: 0.5, y: 0.6, w: 9, h: 0.7,
    fontFace: FH, fontSize: 28, color: C.ink, bold: true, margin: 0,
  });
}

const TOTAL = 8;

// ═══════════════════════════════════════════════════════
// SLIDE 1 — TITLE
// ═══════════════════════════════════════════════════════
{
  const s = pres.addSlide();
  s.background = { color: C.midnight };

  // 좌측 accent block
  s.addShape(pres.shapes.RECTANGLE, {
    x: 0, y: 0, w: 0.25, h: 5.625,
    fill: { color: C.accent }, line: { color: C.accent, width: 0 },
  });

  s.addText("LOSTMEMORY · MULTIPLAYER", {
    x: 0.7, y: 1.2, w: 8, h: 0.4,
    fontFace: FB, fontSize: 12, color: "CADCFC", bold: true,
    charSpacing: 8, margin: 0,
  });

  s.addText("코옵 멀티플레이 서버 연동", {
    x: 0.7, y: 1.7, w: 8.5, h: 1.1,
    fontFace: FH, fontSize: 44, color: "FFFFFF", bold: true, margin: 0,
  });

  s.addText("방 매칭부터 실시간 동기화까지, 우리가 푼 방식", {
    x: 0.7, y: 2.85, w: 8.5, h: 0.5,
    fontFace: FB, fontSize: 18, color: "97AEDC", margin: 0,
  });

  // bottom meta
  s.addShape(pres.shapes.LINE, {
    x: 0.7, y: 4.6, w: 3, h: 0,
    line: { color: C.accent, width: 1.5 },
  });
  s.addText([
    { text: "Unity Netcode  ", options: { color: "CADCFC", bold: true } },
    { text: "+  Spring Boot Relay  ", options: { color: "97AEDC" } },
    { text: "+  JWT 인증", options: { color: "97AEDC" } },
  ], {
    x: 0.7, y: 4.7, w: 8, h: 0.4,
    fontFace: FM, fontSize: 12, margin: 0,
  });
}

// ═══════════════════════════════════════════════════════
// SLIDE 2 — 풀고자 한 문제
// ═══════════════════════════════════════════════════════
{
  const s = pres.addSlide();
  s.background = { color: C.bg };
  slideTitle(s, "PROBLEM", "두 명을 같은 방으로, 그리고 실시간으로");

  s.addText(
    "온라인 코옵 게임을 만들려면 본질적으로 두 가지 문제를 풀어야 합니다.",
    { x: 0.5, y: 1.4, w: 9, h: 0.4, fontFace: FB, fontSize: 14, color: C.mute, margin: 0 }
  );

  // 좌측 카드
  const cardY = 2.0, cardH = 2.7;
  s.addShape(pres.shapes.RECTANGLE, {
    x: 0.5, y: cardY, w: 4.35, h: cardH,
    fill: { color: C.band }, line: { color: C.line, width: 0.75 },
  });
  s.addShape(pres.shapes.RECTANGLE, {
    x: 0.5, y: cardY, w: 0.08, h: cardH,
    fill: { color: C.deep }, line: { color: C.deep, width: 0 },
  });
  s.addText("01", {
    x: 0.75, y: cardY + 0.2, w: 1, h: 0.5,
    fontFace: FH, fontSize: 28, color: C.deep, bold: true, margin: 0,
  });
  s.addText("방 매칭 (Matchmaking)", {
    x: 0.75, y: cardY + 0.75, w: 4, h: 0.4,
    fontFace: FH, fontSize: 18, color: C.ink, bold: true, margin: 0,
  });
  s.addText(
    "누구와 같이 플레이할지 정해야 한다.\n공개 매칭? 친구 초대? 코드 공유?\n→ 우리는 6자리 코드 방식 선택",
    {
      x: 0.75, y: cardY + 1.25, w: 4, h: 1.4,
      fontFace: FB, fontSize: 13, color: C.mute, margin: 0, lineSpacingMultiple: 1.4,
    }
  );

  // 우측 카드
  s.addShape(pres.shapes.RECTANGLE, {
    x: 5.15, y: cardY, w: 4.35, h: cardH,
    fill: { color: C.band }, line: { color: C.line, width: 0.75 },
  });
  s.addShape(pres.shapes.RECTANGLE, {
    x: 5.15, y: cardY, w: 0.08, h: cardH,
    fill: { color: C.teal }, line: { color: C.teal, width: 0 },
  });
  s.addText("02", {
    x: 5.4, y: cardY + 0.2, w: 1, h: 0.5,
    fontFace: FH, fontSize: 28, color: C.teal, bold: true, margin: 0,
  });
  s.addText("실시간 상태 동기화", {
    x: 5.4, y: cardY + 0.75, w: 4, h: 0.4,
    fontFace: FH, fontSize: 18, color: C.ink, bold: true, margin: 0,
  });
  s.addText(
    "초당 수십 번 바뀌는 위치 · 체력 · 입력을\n두 플레이어 화면에 동일하게 보여줘야 한다.\n→ 자체 중계(Relay) 서버 구현",
    {
      x: 5.4, y: cardY + 1.25, w: 4, h: 1.4,
      fontFace: FB, fontSize: 13, color: C.mute, margin: 0, lineSpacingMultiple: 1.4,
    }
  );

  pageFooter(s, 2, TOTAL);
}

// ═══════════════════════════════════════════════════════
// SLIDE 3 — 전체 아키텍처 (다이어그램)
// ═══════════════════════════════════════════════════════
{
  const s = pres.addSlide();
  s.background = { color: C.bg };
  slideTitle(s, "ARCHITECTURE", "REST + 실시간 중계, 두 계층으로 구성");

  // 호스트 박스
  const boxStyle = (color) => ({
    fill: { color: "FFFFFF" }, line: { color, width: 1.5 },
  });

  // Host (좌측 상단)
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, {
    x: 0.6, y: 1.6, w: 2.1, h: 1.0, rectRadius: 0.08, ...boxStyle(C.deep),
  });
  s.addText("Host (Unity)", {
    x: 0.6, y: 1.7, w: 2.1, h: 0.4,
    fontFace: FH, fontSize: 13, color: C.ink, bold: true, align: "center", margin: 0,
  });
  s.addText("호스트 권위", {
    x: 0.6, y: 2.1, w: 2.1, h: 0.3,
    fontFace: FB, fontSize: 10, color: C.mute, align: "center", margin: 0,
  });

  // Guest (좌측 하단)
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, {
    x: 0.6, y: 3.5, w: 2.1, h: 1.0, rectRadius: 0.08, ...boxStyle(C.teal),
  });
  s.addText("Guest (Unity)", {
    x: 0.6, y: 3.6, w: 2.1, h: 0.4,
    fontFace: FH, fontSize: 13, color: C.ink, bold: true, align: "center", margin: 0,
  });
  s.addText("따라가는 쪽", {
    x: 0.6, y: 4.0, w: 2.1, h: 0.3,
    fontFace: FB, fontSize: 10, color: C.mute, align: "center", margin: 0,
  });

  // Center server box (Spring)
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, {
    x: 4.0, y: 2.0, w: 3.0, h: 2.1, rectRadius: 0.08,
    fill: { color: C.midnight }, line: { color: C.midnight, width: 0 },
  });
  s.addText("Spring Boot Backend", {
    x: 4.0, y: 2.15, w: 3.0, h: 0.35,
    fontFace: FH, fontSize: 14, color: "FFFFFF", bold: true, align: "center", margin: 0,
  });
  s.addShape(pres.shapes.LINE, {
    x: 4.4, y: 2.55, w: 2.2, h: 0,
    line: { color: C.accent, width: 1 },
  });
  s.addText([
    { text: "REST API", options: { color: "CADCFC", bold: true, breakLine: true } },
    { text: "  /sessions  /find  /join", options: { color: "97AEDC", fontFace: FM, breakLine: true } },
    { text: "Relay (UDP)", options: { color: "CADCFC", bold: true, breakLine: true } },
    { text: "  세션 토큰 기반 중계", options: { color: "97AEDC", fontFace: FM, breakLine: true } },
    { text: "JWT · 세션 DB", options: { color: "CADCFC", bold: true } },
  ], {
    x: 4.15, y: 2.65, w: 2.7, h: 1.45,
    fontFace: FB, fontSize: 10.5, margin: 0,
  });

  // Right: NGO label
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, {
    x: 8.1, y: 2.55, w: 1.4, h: 1.0, rectRadius: 0.08, ...boxStyle(C.mute),
  });
  s.addText("Netcode for\nGameObjects", {
    x: 8.1, y: 2.7, w: 1.4, h: 0.7,
    fontFace: FB, fontSize: 10, color: C.ink, bold: true, align: "center", margin: 0,
  });

  // 화살표 (Host ↔ Server)
  s.addShape(pres.shapes.LINE, {
    x: 2.75, y: 2.1, w: 1.2, h: 0,
    line: { color: C.deep, width: 1.5, endArrowType: "triangle", beginArrowType: "triangle" },
  });
  s.addText("REST · JWT", {
    x: 2.55, y: 1.7, w: 1.6, h: 0.3,
    fontFace: FM, fontSize: 9, color: C.deep, align: "center", margin: 0,
  });

  s.addShape(pres.shapes.LINE, {
    x: 2.75, y: 2.45, w: 1.2, h: 0,
    line: { color: C.accent, width: 1.5, dashType: "dash", endArrowType: "triangle", beginArrowType: "triangle" },
  });
  s.addText("UDP 중계", {
    x: 2.55, y: 2.55, w: 1.6, h: 0.3,
    fontFace: FM, fontSize: 9, color: C.accent, bold: true, align: "center", margin: 0,
  });

  // 화살표 (Guest ↔ Server)
  s.addShape(pres.shapes.LINE, {
    x: 2.75, y: 3.85, w: 1.2, h: 0,
    line: { color: C.teal, width: 1.5, endArrowType: "triangle", beginArrowType: "triangle" },
  });
  s.addText("REST · JWT", {
    x: 2.55, y: 3.5, w: 1.6, h: 0.3,
    fontFace: FM, fontSize: 9, color: C.teal, align: "center", margin: 0,
  });

  s.addShape(pres.shapes.LINE, {
    x: 2.75, y: 4.2, w: 1.2, h: 0,
    line: { color: C.accent, width: 1.5, dashType: "dash", endArrowType: "triangle", beginArrowType: "triangle" },
  });
  s.addText("UDP 중계", {
    x: 2.55, y: 4.3, w: 1.6, h: 0.3,
    fontFace: FM, fontSize: 9, color: C.accent, bold: true, align: "center", margin: 0,
  });

  // Server → NGO link
  s.addShape(pres.shapes.LINE, {
    x: 7.0, y: 3.05, w: 1.1, h: 0,
    line: { color: C.mute, width: 1 },
  });

  // 캡션
  s.addText("실선 = HTTP 요청(방 만들기·찾기·입장)   ·   점선 = UDP 패킷(게임 중 위치·이벤트 동기화)", {
    x: 0.5, y: 4.8, w: 9, h: 0.3,
    fontFace: FB, fontSize: 10, color: C.mute, align: "center", italic: true, margin: 0,
  });

  pageFooter(s, 3, TOTAL);
}

// ═══════════════════════════════════════════════════════
// SLIDE 4 — REST 매칭 흐름
// ═══════════════════════════════════════════════════════
{
  const s = pres.addSlide();
  s.background = { color: C.bg };
  slideTitle(s, "STEP 1 · REST", "방 매칭 — 익숙한 HTTP + JWT 패턴");

  // 3-step row
  const steps = [
    { n: "01", title: "방 만들기", endpoint: "POST /sessions", body: "호스트가 요청 → 백엔드가 6자리 코드와 세션 토큰 발급" },
    { n: "02", title: "방 찾기",   endpoint: "GET /sessions/find?code=ABC123", body: "게스트가 코드 입력 → sessionId 조회 (방 정원 검증)" },
    { n: "03", title: "방 입장",   endpoint: "POST /sessions/{id}/join",       body: "입장 권한 + 세션 토큰 받음 → 이후 Relay 접속에 사용" },
  ];

  const startY = 1.55, h = 1.05, gap = 0.18;
  steps.forEach((st, i) => {
    const y = startY + (h + gap) * i;
    s.addShape(pres.shapes.RECTANGLE, {
      x: 0.5, y, w: 9, h,
      fill: { color: C.band }, line: { color: C.line, width: 0.75 },
    });
    s.addShape(pres.shapes.RECTANGLE, {
      x: 0.5, y, w: 0.08, h,
      fill: { color: C.deep }, line: { color: C.deep, width: 0 },
    });
    // step number circle
    s.addShape(pres.shapes.OVAL, {
      x: 0.8, y: y + 0.2, w: 0.65, h: 0.65,
      fill: { color: C.deep }, line: { color: C.deep, width: 0 },
    });
    s.addText(st.n, {
      x: 0.8, y: y + 0.2, w: 0.65, h: 0.65,
      fontFace: FH, fontSize: 13, color: "FFFFFF", bold: true, align: "center", valign: "middle", margin: 0,
    });
    s.addText(st.title, {
      x: 1.6, y: y + 0.15, w: 2, h: 0.35,
      fontFace: FH, fontSize: 15, color: C.ink, bold: true, margin: 0,
    });
    s.addText(st.endpoint, {
      x: 1.6, y: y + 0.5, w: 3.6, h: 0.35,
      fontFace: FM, fontSize: 11, color: C.deep, margin: 0,
    });
    s.addText(st.body, {
      x: 5.3, y: y + 0.3, w: 4.0, h: 0.6,
      fontFace: FB, fontSize: 11.5, color: C.mute, margin: 0, lineSpacingMultiple: 1.3,
    });
  });

  s.addText("모든 요청에는  Authorization: Bearer <JWT>  부착 — 웹에서 쓰는 인증 방식과 동일", {
    x: 0.5, y: 5.0, w: 9, h: 0.25,
    fontFace: FB, fontSize: 10.5, color: C.midnight, italic: true, align: "center", margin: 0,
  });

  pageFooter(s, 4, TOTAL);
}

// ═══════════════════════════════════════════════════════
// SLIDE 5 — 실시간 동기화 (Relay)
// ═══════════════════════════════════════════════════════
{
  const s = pres.addSlide();
  s.background = { color: C.bg };
  slideTitle(s, "STEP 2 · REALTIME", "실시간 동기화 — 자체 UDP 중계 서버");

  // 좌측: 다이어그램
  const dx = 0.5, dy = 1.5, dw = 4.6, dh = 3.4;
  s.addShape(pres.shapes.RECTANGLE, {
    x: dx, y: dy, w: dw, h: dh,
    fill: { color: C.band }, line: { color: C.line, width: 0.75 },
  });

  // host
  s.addShape(pres.shapes.OVAL, {
    x: dx + 0.4, y: dy + 0.6, w: 1.0, h: 1.0,
    fill: { color: C.deep }, line: { color: C.deep, width: 0 },
  });
  s.addText("Host", {
    x: dx + 0.4, y: dy + 0.6, w: 1.0, h: 1.0,
    fontFace: FH, fontSize: 12, color: "FFFFFF", bold: true, align: "center", valign: "middle", margin: 0,
  });

  // relay
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, {
    x: dx + 1.85, y: dy + 0.5, w: 1.4, h: 1.2, rectRadius: 0.1,
    fill: { color: C.midnight }, line: { color: C.midnight, width: 0 },
  });
  s.addText("Relay", {
    x: dx + 1.85, y: dy + 0.55, w: 1.4, h: 0.4,
    fontFace: FH, fontSize: 13, color: "FFFFFF", bold: true, align: "center", margin: 0,
  });
  s.addText("Spring UDP", {
    x: dx + 1.85, y: dy + 0.95, w: 1.4, h: 0.3,
    fontFace: FM, fontSize: 10, color: C.accent, align: "center", margin: 0,
  });
  s.addText("(broadcast)", {
    x: dx + 1.85, y: dy + 1.25, w: 1.4, h: 0.3,
    fontFace: FB, fontSize: 9, color: "97AEDC", align: "center", italic: true, margin: 0,
  });

  // guest
  s.addShape(pres.shapes.OVAL, {
    x: dx + 3.65, y: dy + 0.6, w: 1.0, h: 1.0,
    fill: { color: C.teal }, line: { color: C.teal, width: 0 },
  });
  s.addText("Guest", {
    x: dx + 3.65, y: dy + 0.6, w: 1.0, h: 1.0,
    fontFace: FH, fontSize: 12, color: "FFFFFF", bold: true, align: "center", valign: "middle", margin: 0,
  });

  // arrows
  s.addShape(pres.shapes.LINE, {
    x: dx + 1.45, y: dy + 1.1, w: 0.35, h: 0,
    line: { color: C.deep, width: 2, endArrowType: "triangle", beginArrowType: "triangle" },
  });
  s.addShape(pres.shapes.LINE, {
    x: dx + 3.25, y: dy + 1.1, w: 0.35, h: 0,
    line: { color: C.teal, width: 2, endArrowType: "triangle", beginArrowType: "triangle" },
  });

  // bottom row: data tags
  s.addText("위치 · 회전 · 체력 · 입력 · 이벤트", {
    x: dx + 0.2, y: dy + 2.0, w: dw - 0.4, h: 0.3,
    fontFace: FB, fontSize: 11, color: C.midnight, bold: true, align: "center", margin: 0,
  });
  s.addText("초당 ~20회 패킷 교환", {
    x: dx + 0.2, y: dy + 2.35, w: dw - 0.4, h: 0.3,
    fontFace: FM, fontSize: 10, color: C.mute, align: "center", margin: 0,
  });
  s.addText("UDP를 쓰는 이유: 손실 1~2개 < 지연 100ms", {
    x: dx + 0.2, y: dy + 2.75, w: dw - 0.4, h: 0.3,
    fontFace: FB, fontSize: 10, color: C.mute, italic: true, align: "center", margin: 0,
  });

  // 우측: 동작 설명
  const rx = 5.4, rw = 4.1;
  s.addText("어떻게 동작하나", {
    x: rx, y: 1.55, w: rw, h: 0.4,
    fontFace: FH, fontSize: 16, color: C.ink, bold: true, margin: 0,
  });
  s.addText([
    { text: "1. 입장 시 받은 세션 토큰으로 Relay 접속", options: { breakLine: true } },
    { text: "2. Unity는 자체 Transport를 통해 UDP 패킷 송수신", options: { breakLine: true } },
    { text: "3. 서버는 같은 세션의 모든 참여자에게 브로드캐스트", options: { breakLine: true } },
    { text: "4. 수신측은 패킷의 targetUserId로 필요한 것만 처리", options: {} },
  ], {
    x: rx, y: 2.05, w: rw, h: 2.0,
    fontFace: FB, fontSize: 12, color: C.midnight, margin: 0,
    paraSpaceAfter: 8, lineSpacingMultiple: 1.3,
  });

  // 핵심 코드 경로
  s.addShape(pres.shapes.RECTANGLE, {
    x: rx, y: 4.1, w: rw, h: 0.95,
    fill: { color: C.midnight }, line: { color: C.midnight, width: 0 },
  });
  s.addText("핵심 파일", {
    x: rx + 0.15, y: 4.18, w: rw, h: 0.3,
    fontFace: FB, fontSize: 10, color: C.accent, bold: true, margin: 0,
  });
  s.addText([
    { text: "RelaySession.cs", options: { color: "FFFFFF", breakLine: true } },
    { text: "RelaySessionHost / Client.cs", options: { color: "CADCFC", breakLine: true } },
    { text: "LostMemoryRelayTransport.cs", options: { color: "97AEDC" } },
  ], {
    x: rx + 0.15, y: 4.45, w: rw - 0.2, h: 0.6,
    fontFace: FM, fontSize: 10, margin: 0,
  });

  pageFooter(s, 5, TOTAL);
}

// ═══════════════════════════════════════════════════════
// SLIDE 6 — 왜 자체 Relay?
// ═══════════════════════════════════════════════════════
{
  const s = pres.addSlide();
  s.background = { color: C.bg };
  slideTitle(s, "DECISION", "왜 Unity Relay 안 쓰고 자체 구현했나");

  s.addText(
    "Unity가 제공하는 클라우드 중계 서비스를 쓸 수도 있었지만, 두 가지 이유로 직접 만들었습니다.",
    { x: 0.5, y: 1.4, w: 9, h: 0.4, fontFace: FB, fontSize: 13, color: C.mute, margin: 0 }
  );

  // 비교 표
  const tx = 0.5, ty = 2.0, tw = 9, rowH = 0.55;
  // header
  s.addShape(pres.shapes.RECTANGLE, {
    x: tx, y: ty, w: tw, h: rowH,
    fill: { color: C.midnight }, line: { color: C.midnight, width: 0 },
  });
  ["관점", "Unity Relay (클라우드)", "자체 Relay (우리 선택)"].forEach((h, i) => {
    s.addText(h, {
      x: tx + [0, 2.2, 5.6][i], y: ty, w: [2.2, 3.4, 3.4][i], h: rowH,
      fontFace: FH, fontSize: 12, color: "FFFFFF", bold: true,
      align: "left", valign: "middle", margin: 0, paraSpaceBefore: 0,
    });
    // padding 보정
  });
  // header text with manual padding
  s.addText("관점", { x: tx + 0.2, y: ty, w: 2.0, h: rowH, fontFace: FH, fontSize: 12, color: "FFFFFF", bold: true, valign: "middle", margin: 0 });
  s.addText("Unity Relay (클라우드)", { x: tx + 2.4, y: ty, w: 3.2, h: rowH, fontFace: FH, fontSize: 12, color: "FFFFFF", bold: true, valign: "middle", margin: 0 });
  s.addText("자체 Relay (우리 선택)", { x: tx + 5.8, y: ty, w: 3.2, h: rowH, fontFace: FH, fontSize: 12, color: C.accent, bold: true, valign: "middle", margin: 0 });

  const rows = [
    ["비용", "동시접속자 기준 과금", "추가 비용 없음 (기존 서버 활용)"],
    ["통합", "별도 시스템 — 인증 분리", "기존 JWT · 유저 DB 와 일원화"],
    ["LLM 친구·성장 API", "별도 백엔드 필요", "같은 백엔드에서 함께 관리"],
    ["확장성", "글로벌 자동 분산 ✓", "필요 시 직접 구축 (트레이드오프)"],
  ];
  rows.forEach((r, i) => {
    const y = ty + rowH * (i + 1);
    s.addShape(pres.shapes.RECTANGLE, {
      x: tx, y, w: tw, h: rowH,
      fill: { color: i % 2 ? C.band : "FFFFFF" }, line: { color: C.line, width: 0.5 },
    });
    s.addText(r[0], { x: tx + 0.2, y, w: 2.0, h: rowH, fontFace: FH, fontSize: 11.5, color: C.ink, bold: true, valign: "middle", margin: 0 });
    s.addText(r[1], { x: tx + 2.4, y, w: 3.2, h: rowH, fontFace: FB, fontSize: 11, color: C.mute, valign: "middle", margin: 0 });
    s.addText(r[2], { x: tx + 5.8, y, w: 3.2, h: rowH, fontFace: FB, fontSize: 11, color: C.midnight, bold: true, valign: "middle", margin: 0 });
  });

  s.addText("→ 인증 · 세션 관리 · 게임 로직이 한 백엔드에 모이는 게 우리 팀 규모에선 더 유리했습니다.", {
    x: 0.5, y: 4.85, w: 9, h: 0.3,
    fontFace: FB, fontSize: 11, color: C.deep, bold: true, italic: true, margin: 0,
  });

  pageFooter(s, 6, TOTAL);
}

// ═══════════════════════════════════════════════════════
// SLIDE 7 — 호스트 권위 모델
// ═══════════════════════════════════════════════════════
{
  const s = pres.addSlide();
  s.background = { color: C.bg };
  slideTitle(s, "AUTHORITY", "누가 ‘진실’을 갖는가 — 호스트 권위 모델");

  // 좌측: 질문 (코너 케이스)
  s.addShape(pres.shapes.RECTANGLE, {
    x: 0.5, y: 1.5, w: 4.3, h: 3.5,
    fill: { color: C.band }, line: { color: C.line, width: 0.75 },
  });
  s.addShape(pres.shapes.RECTANGLE, {
    x: 0.5, y: 1.5, w: 0.08, h: 3.5,
    fill: { color: C.accent }, line: { color: C.accent, width: 0 },
  });
  s.addText("문제 상황", {
    x: 0.75, y: 1.7, w: 4, h: 0.4,
    fontFace: FB, fontSize: 11, color: C.accent, bold: true, charSpacing: 3, margin: 0,
  });
  s.addText("두 플레이어가 동시에\n같은 적을 공격했다.", {
    x: 0.75, y: 2.15, w: 4, h: 0.85,
    fontFace: FH, fontSize: 18, color: C.ink, bold: true, margin: 0, lineSpacingMultiple: 1.25,
  });
  s.addText([
    { text: "누가 데미지 줬다고 칠 것인가?", options: { breakLine: true } },
    { text: "적은 누구를 향해 반격하나?", options: { breakLine: true } },
    { text: "두 화면이 다른 결과를 보이면?", options: {} },
  ], {
    x: 0.75, y: 3.15, w: 4, h: 1.7,
    fontFace: FB, fontSize: 13, color: C.mute, margin: 0,
    bullet: { code: "25CB" }, paraSpaceAfter: 6,
  });

  // 우측: 해결책
  s.addShape(pres.shapes.RECTANGLE, {
    x: 5.2, y: 1.5, w: 4.3, h: 3.5,
    fill: { color: C.midnight }, line: { color: C.midnight, width: 0 },
  });
  s.addText("우리의 답", {
    x: 5.45, y: 1.7, w: 4, h: 0.4,
    fontFace: FB, fontSize: 11, color: C.accent, bold: true, charSpacing: 3, margin: 0,
  });
  s.addText("호스트가 모든\n판정을 한다.", {
    x: 5.45, y: 2.15, w: 4, h: 0.85,
    fontFace: FH, fontSize: 18, color: "FFFFFF", bold: true, margin: 0, lineSpacingMultiple: 1.25,
  });
  s.addText([
    { text: "게스트는 ‘공격했어요’ 만 전송", options: { color: "CADCFC", breakLine: true } },
    { text: "데미지·사망 계산은 호스트만", options: { color: "CADCFC", breakLine: true } },
    { text: "결과를 다시 게스트에게 통보", options: { color: "CADCFC", breakLine: true } },
    { text: "= REST에서 서버가 진실인 것과 동일", options: { color: C.accent, italic: true } },
  ], {
    x: 5.45, y: 3.15, w: 4, h: 1.8,
    fontFace: FB, fontSize: 12.5, margin: 0, paraSpaceAfter: 6,
  });

  pageFooter(s, 7, TOTAL);
}

// ═══════════════════════════════════════════════════════
// SLIDE 8 — 요약 + 한계
// ═══════════════════════════════════════════════════════
{
  const s = pres.addSlide();
  s.background = { color: C.midnight };

  s.addText("SUMMARY", {
    x: 0.7, y: 0.5, w: 9, h: 0.35,
    fontFace: FB, fontSize: 11, color: C.accent, bold: true, charSpacing: 6, margin: 0,
  });
  s.addText("정리 · 한계 · 다음 단계", {
    x: 0.7, y: 0.8, w: 9, h: 0.7,
    fontFace: FH, fontSize: 28, color: "FFFFFF", bold: true, margin: 0,
  });
  s.addShape(pres.shapes.LINE, {
    x: 0.7, y: 1.6, w: 1.5, h: 0,
    line: { color: C.accent, width: 1.5 },
  });

  // 한 줄 요약
  s.addText("방 매칭은 REST + JWT, 게임 중 동기화는 자체 UDP 중계 — 모두 같은 Spring 백엔드.", {
    x: 0.7, y: 1.9, w: 8.6, h: 0.5,
    fontFace: FH, fontSize: 16, color: "CADCFC", italic: true, margin: 0,
  });

  // 3-column footer
  const colY = 2.7, colH = 2.3;
  const cols = [
    { tag: "성과",       title: "통합된 백엔드", body: "인증·세션·게임 로직·LLM 친구\n모두 한 서버에서 관리" },
    { tag: "현재 한계", title: "단순 브로드캐스트", body: "Relay가 unicast 라우팅을 안 함\n인원이 늘면 트래픽이 제곱으로 증가" },
    { tag: "다음",      title: "서버측 라우팅", body: "타깃 사용자에게만 직접 전달\n데디케이티드 서버 확장 여지" },
  ];
  cols.forEach((c, i) => {
    const x = 0.7 + i * 3.0;
    s.addShape(pres.shapes.RECTANGLE, {
      x, y: colY, w: 2.8, h: colH,
      fill: { color: "1A2350" }, line: { color: C.accent, width: 0 },
    });
    s.addShape(pres.shapes.RECTANGLE, {
      x, y: colY, w: 2.8, h: 0.06,
      fill: { color: C.accent }, line: { color: C.accent, width: 0 },
    });
    s.addText(c.tag, {
      x: x + 0.2, y: colY + 0.2, w: 2.4, h: 0.3,
      fontFace: FB, fontSize: 10, color: C.accent, bold: true, charSpacing: 4, margin: 0,
    });
    s.addText(c.title, {
      x: x + 0.2, y: colY + 0.55, w: 2.4, h: 0.5,
      fontFace: FH, fontSize: 16, color: "FFFFFF", bold: true, margin: 0,
    });
    s.addText(c.body, {
      x: x + 0.2, y: colY + 1.15, w: 2.4, h: 1.1,
      fontFace: FB, fontSize: 11.5, color: "CADCFC", margin: 0, lineSpacingMultiple: 1.35,
    });
  });

  s.addText("LostMemory · S14P31C201", {
    x: 0.7, y: 5.25, w: 5, h: 0.25,
    fontFace: FB, fontSize: 9, color: "97AEDC", margin: 0,
  });
  s.addText("8 / 8", {
    x: 8.5, y: 5.25, w: 1, h: 0.25,
    fontFace: FB, fontSize: 9, color: "97AEDC", align: "right", margin: 0,
  });
}

// ═══════════════════════════════════════════════════════
pres.writeFile({ fileName: "C:/ssafy/free_project/S14P31C201/client/docs/khi/coop_server_demo.pptx" })
  .then((f) => console.log("OK:", f))
  .catch((e) => { console.error(e); process.exit(1); });
