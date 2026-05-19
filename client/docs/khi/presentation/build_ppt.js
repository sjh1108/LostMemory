const pptxgen = require("pptxgenjs");

const pres = new pptxgen();
pres.layout = "LAYOUT_WIDE"; // 13.3" x 7.5"
pres.author = "김회인";
pres.title = "Lost Memory — S14 자율프로젝트 발표";

// ====== Palette (pixel-roguelike night) ======
const C = {
  bgDark:   "0B1437",
  bgPanel:  "16204A",
  bgPanel2: "1F2C5C",
  textMain: "FFFFFE",
  textMuted:"A8B0C8",
  gold:     "FFD54F",
  pink:     "FF6B9D",
  cyan:     "4FC3F7",
  green:    "81C784",
  red:      "FF5252",
  divider:  "2A356A",
};

const F = {
  headerKR: "맑은 고딕",
  body:     "맑은 고딕",
  mono:     "Consolas",
};

const W = 13.3, H = 7.5;
const TOTAL = 18;

// ====== Asset paths (Downloads character GIFs) ======
const DL = "C:/Users/SSAFY/Downloads/";
const GIF = {
  runFront:  DL + "run_front.gif",
  runBack:   DL + "run_back.gif",
  stayFront: DL + "stay_front.gif",
  stayBack:  DL + "stay_back.gif",
  hurt:      DL + "hurt_front.gif",
  die:       DL + "die_front.gif",
};

// ====== Helpers ======
function addBg(slide, color = C.bgDark) { slide.background = { color }; }

function addTitle(slide, num, title, subtitle) {
  slide.addText(String(num).padStart(2, "0"), {
    x: 0.6, y: 0.45, w: 1.2, h: 0.5,
    fontSize: 14, fontFace: F.mono, color: C.gold, bold: true, margin: 0,
  });
  slide.addText(title, {
    x: 0.6, y: 0.85, w: 12.0, h: 0.9,
    fontSize: 36, fontFace: F.headerKR, color: C.textMain, bold: true, margin: 0,
  });
  if (subtitle) {
    slide.addText(subtitle, {
      x: 0.6, y: 1.65, w: 12.0, h: 0.5,
      fontSize: 16, fontFace: F.body, color: C.textMuted, margin: 0,
    });
  }
  slide.addText(`${num} / ${TOTAL}`, {
    x: W - 1.6, y: H - 0.55, w: 1.0, h: 0.3,
    fontSize: 10, fontFace: F.mono, color: C.textMuted, align: "right", margin: 0,
  });
}

function placeholderBox(slide, x, y, w, h, label, sub) {
  slide.addShape(pres.shapes.RECTANGLE, {
    x, y, w, h,
    fill: { color: C.bgPanel },
    line: { color: C.gold, width: 1.5, dashType: "dash" },
  });
  slide.addText(label, {
    x, y: y + h / 2 - 0.45, w, h: 0.4,
    fontSize: 18, fontFace: F.body, color: C.gold, bold: true, align: "center", margin: 0,
  });
  if (sub) {
    slide.addText(sub, {
      x, y: y + h / 2 + 0.05, w, h: 0.4,
      fontSize: 12, fontFace: F.body, color: C.textMuted, align: "center", margin: 0,
    });
  }
}

function card(slide, x, y, w, h, fill = C.bgPanel) {
  slide.addShape(pres.shapes.RECTANGLE, {
    x, y, w, h, fill: { color: fill },
    line: { color: C.divider, width: 0.75 },
  });
}

// =====================================================
// Slide 1 — Title
// =====================================================
{
  const s = pres.addSlide();
  addBg(s);

  // 좌측 골드 바
  s.addShape(pres.shapes.RECTANGLE, {
    x: 0, y: 0, w: 0.25, h: H, fill: { color: C.gold }, line: { color: C.gold },
  });

  s.addText("S14 자율프로젝트  ·  광주 C201", {
    x: 0.8, y: 2.2, w: 9, h: 0.4,
    fontSize: 14, fontFace: F.mono, color: C.gold, bold: true, charSpacing: 6, margin: 0,
  });

  // 대형 타이틀
  s.addText("LOST", {
    x: 0.8, y: 2.7, w: 8, h: 1.3,
    fontSize: 110, fontFace: F.headerKR, color: C.textMain, bold: true, charSpacing: 4, margin: 0,
  });
  s.addText("MEMORY", {
    x: 0.8, y: 4.05, w: 9, h: 1.3,
    fontSize: 110, fontFace: F.headerKR, color: C.pink, bold: true, charSpacing: 4, margin: 0,
  });

  s.addText("멸망한 세계에서, 다시 강해지기 위한 훈련", {
    x: 0.8, y: 5.45, w: 12, h: 0.5,
    fontSize: 18, fontFace: F.body, color: C.textMuted, margin: 0,
  });

  // 캐릭터들 배치 (우측 상단)
  s.addImage({ path: GIF.runFront,  x: 10.5, y: 1.8, w: 1.5, h: 1.5 });
  s.addImage({ path: GIF.stayFront, x: 11.6, y: 3.2, w: 1.2, h: 1.2 });
  s.addImage({ path: GIF.runBack,   x: 10.2, y: 4.3, w: 1.3, h: 1.3 });

  // 하단
  s.addShape(pres.shapes.LINE, {
    x: 0.8, y: 6.6, w: 3.5, h: 0, line: { color: C.gold, width: 2 },
  });
  s.addText("발표  ·  김회인", {
    x: 0.8, y: 6.75, w: 8, h: 0.35,
    fontSize: 15, fontFace: F.body, color: C.textMain, margin: 0,
  });
  s.addText("Press Any Key to Start", {
    x: W - 4, y: H - 0.6, w: 3.4, h: 0.4,
    fontSize: 12, fontFace: F.mono, color: C.cyan, align: "right", italic: true, margin: 0,
  });
}

// =====================================================
// Slide 2 — 스토리 (어그로)
// =====================================================
{
  const s = pres.addSlide();
  addBg(s);
  addTitle(s, 2, "스토리 — 잠깐, 만약에", "어그로 한 번 끌고 가겠습니다");

  // 큰 질문 1
  s.addText("\"갑자기 세상이 멸망한다면", {
    x: 0.8, y: 2.8, w: 12, h: 1.0,
    fontSize: 42, fontFace: F.headerKR, color: C.textMain, bold: true, margin: 0,
  });
  s.addText("어떻게 하시겠어요?\"", {
    x: 0.8, y: 3.7, w: 12, h: 1.0,
    fontSize: 42, fontFace: F.headerKR, color: C.gold, bold: true, margin: 0,
  });

  // 강조 라인
  s.addShape(pres.shapes.LINE, {
    x: 0.8, y: 4.95, w: 6, h: 0, line: { color: C.pink, width: 2 },
  });

  // 큰 질문 2
  s.addText("\"취업했는데  다시  싸피 14기가  되셨다면?\"", {
    x: 0.8, y: 5.2, w: 12, h: 0.8,
    fontSize: 26, fontFace: F.headerKR, color: C.pink, bold: true, italic: true, margin: 0,
  });

  // 캐릭터 (우측 하단)
  s.addImage({ path: GIF.hurt, x: 11.2, y: 5.3, w: 1.5, h: 1.5 });
}

// =====================================================
// Slide 3 — 스토리 (주인공 입장)
// =====================================================
{
  const s = pres.addSlide();
  addBg(s);
  addTitle(s, 3, "스토리 — 주인공 시점", "이렇게 됐다고 칩시다");

  // 4-Beat 흐름
  const beats = [
    { tag: "01",  t: "세상이 멸망",      desc: "어쩌구 저쩌구 인게임 컷씬처럼" },
    { tag: "02",  t: "훈련 시스템 구축",  desc: "멸망을 막기 위해 컴퓨터 훈련 시스템" },
    { tag: "03",  t: "주인공 진입",      desc: "강해지기 위해 훈련을 시작" },
    { tag: "04",  t: "강해진다",         desc: "그리고 세상을 구하러 간다" },
  ];

  const cw = 2.9, gap = 0.18, ch = 4.2;
  const totalW = cw * 4 + gap * 3;
  const sx = (W - totalW) / 2;
  const sy = 2.6;

  beats.forEach((b, i) => {
    const x = sx + i * (cw + gap);
    card(s, x, sy, cw, ch, C.bgPanel);
    s.addShape(pres.shapes.RECTANGLE, {
      x, y: sy, w: cw, h: 0.08, fill: { color: C.gold }, line: { color: C.gold },
    });
    s.addText(b.tag, {
      x: x + 0.3, y: sy + 0.3, w: cw - 0.6, h: 0.5,
      fontSize: 26, fontFace: F.mono, color: C.gold, bold: true, margin: 0,
    });
    s.addText(b.t, {
      x: x + 0.2, y: sy + 0.95, w: cw - 0.4, h: 1.0,
      fontSize: 17, fontFace: F.headerKR, color: C.textMain, bold: true, margin: 0,
    });
    s.addText(b.desc, {
      x: x + 0.3, y: sy + 2.1, w: cw - 0.6, h: 1.4,
      fontSize: 12, fontFace: F.body, color: C.textMuted, margin: 0,
    });
  });

  // 흐름 캐릭터 (하단 우측, 달려가는 모습)
  s.addImage({ path: GIF.runFront, x: W - 1.7, y: 6.85, w: 0.55, h: 0.55 });
}

// =====================================================
// Slide 4 — 영상 포폴
// =====================================================
{
  const s = pres.addSlide();
  addBg(s);
  addTitle(s, 4, "플레이 영상", "백 마디 설명보다 한 컷의 영상");

  placeholderBox(s, 1.5, 2.6, 10.3, 4.0,
    "▶  플레이 영상 포폴",
    "1 ~ 2분 · 핵심 플레이 컷 모음");

  s.addText("일단 보시죠.", {
    x: 1.5, y: 6.75, w: 10.3, h: 0.4,
    fontSize: 14, fontFace: F.body, color: C.textMuted, align: "center", italic: true, margin: 0,
  });
}

// =====================================================
// Slide 5 — 게임 소개 (로그라이크)
// =====================================================
{
  const s = pres.addSlide();
  addBg(s);
  addTitle(s, 5, "게임 소개 — 로그라이크", "한 판이 곧 한 번의 모험");

  // 좌측: 죽음 카드
  card(s, 0.8, 2.6, 4.2, 4.4, C.bgPanel);
  s.addShape(pres.shapes.RECTANGLE, {
    x: 0.8, y: 2.6, w: 4.2, h: 0.08, fill: { color: C.red }, line: { color: C.red },
  });
  s.addImage({ path: GIF.die, x: 0.8 + (4.2 - 1.6) / 2, y: 2.9, w: 1.6, h: 1.6 });
  s.addText("죽으면 끝.", {
    x: 1.0, y: 4.65, w: 3.8, h: 0.55,
    fontSize: 22, fontFace: F.headerKR, color: C.red, bold: true, align: "center", margin: 0,
  });
  s.addText("죽으면 그 템 다시 못 얻고,\n다시 처음부터.", {
    x: 1.0, y: 5.3, w: 3.8, h: 1.5,
    fontSize: 14, fontFace: F.body, color: C.textMuted, align: "center", margin: 0,
  });

  // 우측: 빌드 카드
  card(s, 5.4, 2.6, 7.1, 4.4, C.bgPanel);
  s.addShape(pres.shapes.RECTANGLE, {
    x: 5.4, y: 2.6, w: 7.1, h: 0.08, fill: { color: C.gold }, line: { color: C.gold },
  });
  s.addText("그래서 — 매 판이 다르다", {
    x: 5.6, y: 2.85, w: 6.7, h: 0.55,
    fontSize: 22, fontFace: F.headerKR, color: C.gold, bold: true, margin: 0,
  });
  s.addText([
    { text: "방 클리어마다 새로운 아이템을 선택",            options: { bullet: true, breakLine: true } },
    { text: "인벤토리 안에서 여러 빌드 조합 가능",           options: { bullet: true, breakLine: true } },
    { text: "조합에 따라 세트 효과 발동",                    options: { bullet: true, breakLine: true } },
    { text: "나만의 랜덤 빌드를 매번 새로 짤 수 있음",       options: { bullet: true } },
  ], {
    x: 5.6, y: 3.55, w: 6.7, h: 3.2,
    fontSize: 16, fontFace: F.body, color: C.textMain, paraSpaceAfter: 10, margin: 0,
  });
}

// =====================================================
// Slide 6 — 우리 컨텐츠: 무기 1 → 6
// =====================================================
{
  const s = pres.addSlide();
  addBg(s);
  addTitle(s, 6, "우리 컨텐츠 — 무기", "1개 였습니다.   →   이제 6개입니다.");

  // BEFORE
  card(s, 0.8, 2.7, 5.7, 4.2, C.bgPanel);
  s.addText("BEFORE", {
    x: 1.0, y: 2.9, w: 5.3, h: 0.35,
    fontSize: 12, fontFace: F.mono, color: C.red, bold: true, charSpacing: 4, margin: 0,
  });
  s.addText("1", {
    x: 1.0, y: 3.4, w: 5.3, h: 2.3,
    fontSize: 180, fontFace: F.mono, color: C.textMuted, bold: true, align: "center", valign: "middle", margin: 0,
  });
  s.addText("무기 1종", {
    x: 1.0, y: 5.85, w: 5.3, h: 0.5,
    fontSize: 18, fontFace: F.headerKR, color: C.textMuted, align: "center", margin: 0,
  });

  // 화살표
  s.addText("→", {
    x: 6.5, y: 4.4, w: 0.3, h: 0.8,
    fontSize: 40, fontFace: F.mono, color: C.gold, bold: true, align: "center", valign: "middle", margin: 0,
  });

  // AFTER
  card(s, 6.9, 2.7, 5.7, 4.2, C.bgPanel2);
  s.addShape(pres.shapes.RECTANGLE, {
    x: 6.9, y: 2.7, w: 5.7, h: 0.1, fill: { color: C.gold }, line: { color: C.gold },
  });
  s.addText("AFTER", {
    x: 7.1, y: 2.95, w: 5.3, h: 0.35,
    fontSize: 12, fontFace: F.mono, color: C.gold, bold: true, charSpacing: 4, margin: 0,
  });
  s.addText("6", {
    x: 7.1, y: 3.4, w: 5.3, h: 2.3,
    fontSize: 180, fontFace: F.mono, color: C.gold, bold: true, align: "center", valign: "middle", margin: 0,
  });
  s.addText("무기 6종!", {
    x: 7.1, y: 5.85, w: 5.3, h: 0.5,
    fontSize: 22, fontFace: F.headerKR, color: C.textMain, bold: true, align: "center", margin: 0,
  });
}

// =====================================================
// Slide 7 — 아이템 / 세트 / 조합
// =====================================================
{
  const s = pres.addSlide();
  addBg(s);
  addTitle(s, 7, "우리 컨텐츠 — 아이템 & 세트", "조합의 수, 한 번 보시죠");

  // 3개 통계 박스
  const stats = [
    { big: "NN",  unit: "개",    label: "아이템",       sub: "방마다 새로 등장" },
    { big: "N",   unit: "종",    label: "세트 효과",     sub: "조합 시 자동 발동" },
    { big: "NN",  unit: "칸",    label: "인벤토리",      sub: "고민할 자리는 충분" },
  ];

  const cw = 3.8, gap = 0.35, ch = 2.3;
  const totalW = cw * 3 + gap * 2;
  const sx = (W - totalW) / 2;
  const sy = 2.7;

  stats.forEach((st, i) => {
    const x = sx + i * (cw + gap);
    card(s, x, sy, cw, ch, C.bgPanel);
    s.addShape(pres.shapes.RECTANGLE, {
      x, y: sy, w: 0.1, h: ch, fill: { color: C.gold }, line: { color: C.gold },
    });
    s.addText(st.label, {
      x: x + 0.3, y: sy + 0.2, w: cw - 0.4, h: 0.4,
      fontSize: 13, fontFace: F.mono, color: C.gold, bold: true, charSpacing: 3, margin: 0,
    });
    // 큰 숫자 + 단위
    s.addText([
      { text: st.big, options: { fontSize: 64, color: C.textMain, bold: true } },
      { text: " " + st.unit, options: { fontSize: 24, color: C.textMuted } },
    ], {
      x: x + 0.3, y: sy + 0.7, w: cw - 0.4, h: 1.1,
      fontFace: F.mono, valign: "middle", margin: 0,
    });
    s.addText(st.sub, {
      x: x + 0.3, y: sy + 1.85, w: cw - 0.4, h: 0.4,
      fontSize: 12, fontFace: F.body, color: C.textMuted, margin: 0,
    });
  });

  // 임팩트 박스
  card(s, 0.8, 5.4, 11.7, 1.6, C.bgPanel2);
  s.addShape(pres.shapes.RECTANGLE, {
    x: 0.8, y: 5.4, w: 0.1, h: 1.6, fill: { color: C.pink }, line: { color: C.pink },
  });
  s.addText("총", {
    x: 1.1, y: 5.7, w: 0.8, h: 1.0,
    fontSize: 24, fontFace: F.headerKR, color: C.textMuted, valign: "middle", margin: 0,
  });
  s.addText("NNNNNNNNNNNNNNN", {
    x: 1.9, y: 5.5, w: 8.5, h: 1.4,
    fontSize: 44, fontFace: F.mono, color: C.pink, bold: true, valign: "middle", margin: 0,
  });
  s.addText("가지 조합 가능", {
    x: 10.0, y: 5.7, w: 2.6, h: 1.0,
    fontSize: 22, fontFace: F.headerKR, color: C.textMain, bold: true, valign: "middle", margin: 0,
  });

  s.addText("※  실제 수치는 발표 직전 채워서 갱신 예정", {
    x: 0.8, y: H - 0.55, w: 12, h: 0.3,
    fontSize: 10, fontFace: F.mono, color: C.textMuted, italic: true, margin: 0,
  });
}

// =====================================================
// Slide 8 — 재능 시스템
// =====================================================
{
  const s = pres.addSlide();
  addBg(s);
  addTitle(s, 8, "우리 컨텐츠 — 재능 시스템", "내 취향대로, 내 캐릭터를 키워서 시작");

  // 좌: 재능 트리 자리
  placeholderBox(s, 0.8, 2.6, 6.5, 4.4,
    "재능 트리 / 패시브 UI",
    "스크린샷 또는 GIF");

  // 우: 설명
  const rx = 7.6;
  s.addText("Talent Tree", {
    x: rx, y: 2.7, w: 5.2, h: 0.4,
    fontSize: 13, fontFace: F.mono, color: C.gold, bold: true, charSpacing: 4, margin: 0,
  });
  s.addText("취향대로\n재능 포인트를 찍고", {
    x: rx, y: 3.15, w: 5.2, h: 1.4,
    fontSize: 30, fontFace: F.headerKR, color: C.textMain, bold: true, margin: 0,
  });
  s.addText("본인만의 강력한 캐릭터로\n게임을 시작할 수 있습니다.", {
    x: rx, y: 4.65, w: 5.2, h: 1.2,
    fontSize: 16, fontFace: F.body, color: C.textMuted, margin: 0,
  });

  // 캐릭터 (강해진 느낌)
  s.addImage({ path: GIF.stayFront, x: rx + 4.0, y: 5.95, w: 1.2, h: 1.2 });
  s.addText("→ 내 캐릭", {
    x: rx, y: 6.45, w: 4.0, h: 0.4,
    fontSize: 13, fontFace: F.mono, color: C.cyan, italic: true, align: "right", margin: 0,
  });
}

// =====================================================
// Slide 9 — NPC LLM
// =====================================================
{
  const s = pres.addSlide();
  addBg(s);
  addTitle(s, 9, "우리 컨텐츠 — NPC와 LLM 대화", "정해진 대사가 아니라, 진짜 대화");

  // 좌: 대화 화면 자리
  placeholderBox(s, 0.8, 2.6, 7.0, 4.4,
    "NPC ↔ LLM 대화 GIF",
    "실제 게임 화면");

  // 우: 포인트
  const rx = 8.2;
  const points = [
    { k: "LLM",   v: "NPC 대사를 실시간 생성" },
    { k: "HIDDEN",v: "숨겨진 이야기 물어보기" },
    { k: "FREE",  v: "다양한 형태의 교류 가능" },
  ];
  s.addText("어떻게 활용?", {
    x: rx, y: 2.7, w: 4.5, h: 0.5,
    fontSize: 18, fontFace: F.headerKR, color: C.gold, bold: true, margin: 0,
  });
  points.forEach((p, i) => {
    const y = 3.3 + i * 1.2;
    card(s, rx, y, 4.6, 1.0, C.bgPanel);
    s.addShape(pres.shapes.RECTANGLE, {
      x: rx, y, w: 0.08, h: 1.0, fill: { color: C.cyan }, line: { color: C.cyan },
    });
    s.addText(p.k, {
      x: rx + 0.25, y: y + 0.12, w: 4.2, h: 0.35,
      fontSize: 11, fontFace: F.mono, color: C.cyan, bold: true, charSpacing: 3, margin: 0,
    });
    s.addText(p.v, {
      x: rx + 0.25, y: y + 0.45, w: 4.2, h: 0.5,
      fontSize: 15, fontFace: F.headerKR, color: C.textMain, bold: true, margin: 0,
    });
  });
}

// =====================================================
// Slide 10 — 시연 안내
// =====================================================
{
  const s = pres.addSlide();
  addBg(s);
  addTitle(s, 10, "시연 — 직접 보여드립니다", "3인 멀티 플레이");

  // 플로우 다이어그램
  const flow = [
    { t: "마을", icon: GIF.stayFront },
    { t: "전투", icon: GIF.runFront },
    { t: "보스", icon: GIF.hurt },
  ];

  const bw = 2.8, gap = 1.0;
  const totalW = bw * 3 + gap * 2;
  const sx = (W - totalW) / 2;
  const sy = 2.7;

  flow.forEach((f, i) => {
    const x = sx + i * (bw + gap);
    card(s, x, sy, bw, 2.5, C.bgPanel);
    s.addImage({ path: f.icon, x: x + (bw - 1.0) / 2, y: sy + 0.4, w: 1.0, h: 1.0 });
    s.addText(f.t, {
      x, y: sy + 1.6, w: bw, h: 0.8,
      fontSize: 26, fontFace: F.headerKR, color: C.textMain, bold: true, align: "center", margin: 0,
    });
    if (i < flow.length - 1) {
      s.addText("→", {
        x: x + bw, y: sy + 0.85, w: gap, h: 0.6,
        fontSize: 32, fontFace: F.mono, color: C.gold, bold: true, align: "center", valign: "middle", margin: 0,
      });
    }
  });

  // 하단: 역할 분담
  card(s, 0.8, 5.7, 11.7, 1.1, C.bgPanel2);
  const roles = [
    { k: "3인",  v: "멀티 플레이" },
    { k: "1인",  v: "발표 / 백업" },
    { k: "1인",  v: "시연 조율" },
  ];
  roles.forEach((r, i) => {
    const x = 1.0 + i * 3.9;
    s.addText(r.k, {
      x, y: 5.8, w: 1.0, h: 0.9,
      fontSize: 22, fontFace: F.mono, color: C.gold, bold: true, valign: "middle", margin: 0,
    });
    s.addText(r.v, {
      x: x + 1.1, y: 5.8, w: 2.6, h: 0.9,
      fontSize: 14, fontFace: F.body, color: C.textMain, valign: "middle", margin: 0,
    });
  });
}

// =====================================================
// Slide 11 — 기술: ComfyUI 란?
// =====================================================
{
  const s = pres.addSlide();
  addBg(s);
  addTitle(s, 11, "기술 — ComfyUI 란?", "고민 · 선택 · 적용 · 결과");

  // 좌: 한줄 정의
  card(s, 0.8, 2.6, 5.7, 4.4, C.bgPanel);
  s.addText("한 줄 요약", {
    x: 1.0, y: 2.8, w: 5.3, h: 0.4,
    fontSize: 12, fontFace: F.mono, color: C.gold, bold: true, charSpacing: 4, margin: 0,
  });
  s.addText("\"로컬에서 돌리고,\n블럭을 내 맘대로 조절해서\n나만의 그림을 만들어 내는 도구\"", {
    x: 1.0, y: 3.3, w: 5.3, h: 2.4,
    fontSize: 22, fontFace: F.headerKR, color: C.textMain, bold: true, margin: 0,
  });
  s.addText("로컬 GPU에서 노드를 이어붙여\n이미지·LLM·오디오 파이프라인을 조립.", {
    x: 1.0, y: 5.95, w: 5.3, h: 0.9,
    fontSize: 13, fontFace: F.body, color: C.textMuted, margin: 0,
  });

  // 우: 4 가지 흐름 (고민 → 선택 → 적용 → 결과)
  const rx = 6.9;
  const flow = [
    { tag: "고민",     d: "외부 API 비용·지연·검열 이슈" },
    { tag: "선택",     d: "ComfyUI + 로컬 GPU 조합" },
    { tag: "적용",     d: "노드 그래프로 파이프라인 구성" },
    { tag: "결과",     d: "비용 0, 자유로운 커스터마이즈" },
  ];
  flow.forEach((f, i) => {
    const y = 2.7 + i * 1.12;
    card(s, rx, y, 5.7, 0.95, C.bgPanel2);
    s.addShape(pres.shapes.RECTANGLE, {
      x: rx, y, w: 0.08, h: 0.95, fill: { color: C.gold }, line: { color: C.gold },
    });
    s.addText(f.tag, {
      x: rx + 0.25, y: y + 0.15, w: 1.2, h: 0.65,
      fontSize: 18, fontFace: F.headerKR, color: C.gold, bold: true, valign: "middle", margin: 0,
    });
    s.addText(f.d, {
      x: rx + 1.5, y: y + 0.15, w: 4.0, h: 0.65,
      fontSize: 13, fontFace: F.body, color: C.textMain, valign: "middle", margin: 0,
    });
  });
}

// =====================================================
// Slide 12 — ComfyUI 활용
// =====================================================
{
  const s = pres.addSlide();
  addBg(s);
  addTitle(s, 12, "기술 — ComfyUI 활용", "주헌이가 만든 예시로 설명");

  // 좌: 워크플로 자리
  placeholderBox(s, 0.8, 2.6, 7.0, 4.4,
    "ComfyUI 워크플로 스크린샷",
    "주헌이 작업물 + 노드 그래프");

  // 우: 핵심 키워드
  const rx = 8.2;
  const items = [
    { k: "image to image", d: "기존 이미지를 입력으로 변환 생성" },
    { k: "node 방식",      d: "기능 단위를 노드로 연결" },
    { k: "node 종류",      d: "Loader / Sampler / Latent / ..." },
    { k: "어려웠던 점",    d: "필요한 노드 찾기 + 자원 관리" },
  ];
  items.forEach((it, i) => {
    const y = 2.6 + i * 1.1;
    card(s, rx, y, 4.6, 0.95, C.bgPanel);
    s.addShape(pres.shapes.RECTANGLE, {
      x: rx, y, w: 0.08, h: 0.95, fill: { color: C.pink }, line: { color: C.pink },
    });
    s.addText(it.k, {
      x: rx + 0.25, y: y + 0.1, w: 4.2, h: 0.4,
      fontSize: 14, fontFace: F.headerKR, color: C.pink, bold: true, margin: 0,
    });
    s.addText(it.d, {
      x: rx + 0.25, y: y + 0.45, w: 4.2, h: 0.5,
      fontSize: 12, fontFace: F.body, color: C.textMain, margin: 0,
    });
  });
}

// =====================================================
// Slide 13 — 기술: 빌드 최적화
// =====================================================
{
  const s = pres.addSlide();
  addBg(s);
  addTitle(s, 13, "기술 — 빌드 최적화", "40분 → 1분, 어떻게 줄였나");

  // BEFORE 큰 숫자
  card(s, 0.8, 2.7, 5.7, 4.3, C.bgPanel);
  s.addText("BEFORE", {
    x: 1.0, y: 2.9, w: 5.3, h: 0.35,
    fontSize: 12, fontFace: F.mono, color: C.red, bold: true, charSpacing: 4, margin: 0,
  });
  s.addText([
    { text: "40", options: { fontSize: 130, color: C.textMuted, bold: true } },
    { text: " 분", options: { fontSize: 36, color: C.textMuted } },
  ], {
    x: 1.0, y: 3.4, w: 5.3, h: 2.0,
    fontFace: F.mono, valign: "middle", margin: 0,
  });
  s.addText("• 매번 30~40분짜리 빌드\n• 원인: asset을 너무 많이 사용\n• 지우면 빠르지만 깨짐", {
    x: 1.0, y: 5.5, w: 5.3, h: 1.4,
    fontSize: 13, fontFace: F.body, color: C.textMuted, paraSpaceAfter: 4, margin: 0,
  });

  // 화살표
  s.addText("→", {
    x: 6.5, y: 4.4, w: 0.3, h: 0.8,
    fontSize: 40, fontFace: F.mono, color: C.gold, bold: true, align: "center", valign: "middle", margin: 0,
  });

  // AFTER
  card(s, 6.9, 2.7, 5.7, 4.3, C.bgPanel2);
  s.addShape(pres.shapes.RECTANGLE, {
    x: 6.9, y: 2.7, w: 5.7, h: 0.1, fill: { color: C.green }, line: { color: C.green },
  });
  s.addText("AFTER", {
    x: 7.1, y: 2.95, w: 5.3, h: 0.35,
    fontSize: 12, fontFace: F.mono, color: C.green, bold: true, charSpacing: 4, margin: 0,
  });
  s.addText([
    { text: "1", options: { fontSize: 130, color: C.green, bold: true } },
    { text: " 분", options: { fontSize: 36, color: C.textMain } },
  ], {
    x: 7.1, y: 3.4, w: 5.3, h: 2.0,
    fontFace: F.mono, valign: "middle", margin: 0,
  });
  s.addText("• URP 방식으로 미리 render 캐싱\n• 안 쓰는 asset만 정리\n• 깨짐 없이 빌드 시간 ↓", {
    x: 7.1, y: 5.5, w: 5.3, h: 1.4,
    fontSize: 13, fontFace: F.body, color: C.textMain, paraSpaceAfter: 4, margin: 0,
  });
}

// =====================================================
// Slide 14 — 기술: Editor
// =====================================================
{
  const s = pres.addSlide();
  addBg(s);
  addTitle(s, 14, "기술 — 커스텀 Editor", "수치를 게임 안에서 바로 만지기");

  // 좌: 문제
  card(s, 0.8, 2.6, 5.7, 4.4, C.bgPanel);
  s.addText("PROBLEM", {
    x: 1.0, y: 2.8, w: 5.3, h: 0.4,
    fontSize: 12, fontFace: F.mono, color: C.red, bold: true, charSpacing: 4, margin: 0,
  });
  s.addText("DB 값 하나 바꾸려고\n게임을 매번 재시작", {
    x: 1.0, y: 3.3, w: 5.3, h: 1.6,
    fontSize: 24, fontFace: F.headerKR, color: C.textMain, bold: true, margin: 0,
  });
  s.addText("• 수치를 DB로 두면 테스트가 느림\n• 인스펙터로 일일이 보기엔 불편\n• 밸런스 패치 한 번에 한참 걸림", {
    x: 1.0, y: 5.1, w: 5.3, h: 1.8,
    fontSize: 13, fontFace: F.body, color: C.textMuted, paraSpaceAfter: 4, margin: 0,
  });

  // 화살표
  s.addText("→", {
    x: 6.5, y: 4.4, w: 0.3, h: 0.8,
    fontSize: 40, fontFace: F.mono, color: C.gold, bold: true, align: "center", valign: "middle", margin: 0,
  });

  // 우: 해결
  card(s, 6.9, 2.6, 5.7, 4.4, C.bgPanel2);
  s.addShape(pres.shapes.RECTANGLE, {
    x: 6.9, y: 2.6, w: 5.7, h: 0.1, fill: { color: C.green }, line: { color: C.green },
  });
  s.addText("SOLUTION", {
    x: 7.1, y: 2.85, w: 5.3, h: 0.4,
    fontSize: 12, fontFace: F.mono, color: C.green, bold: true, charSpacing: 4, margin: 0,
  });
  s.addText("커스텀 Editor로\n분리해서 핫 리로드", {
    x: 7.1, y: 3.3, w: 5.3, h: 1.6,
    fontSize: 24, fontFace: F.headerKR, color: C.textMain, bold: true, margin: 0,
  });
  s.addText("• 수치를 별도 Editor로 분리\n• 플레이 중에 값 변경 즉시 반영\n• 밸런싱 속도 체감상 수 배 빨라짐", {
    x: 7.1, y: 5.1, w: 5.3, h: 1.8,
    fontSize: 13, fontFace: F.body, color: C.textMain, paraSpaceAfter: 4, margin: 0,
  });
}

// =====================================================
// Slide 15 — Google Form 사이클
// =====================================================
{
  const s = pres.addSlide();
  addBg(s);
  addTitle(s, 15, "유저 피드백 사이클", "Google Form → 문제 발견 → 해결 → 다시 Form");

  const steps = [
    { n: "01", t: "Form 배포",      d: "버전마다 설문 진행" },
    { n: "02", t: "문제 발견",      d: "유저 시선의 이슈를 수집" },
    { n: "03", t: "해결 / 패치",    d: "다음 버전에 반영" },
    { n: "04", t: "재설문",         d: "만족도 변화 측정" },
  ];

  const cw = 2.9, gap = 0.18, ch = 3.5;
  const totalW = cw * 4 + gap * 3;
  const sx = (W - totalW) / 2;
  const sy = 2.8;

  steps.forEach((st, i) => {
    const x = sx + i * (cw + gap);
    card(s, x, sy, cw, ch, C.bgPanel);
    s.addShape(pres.shapes.OVAL, {
      x: x + cw / 2 - 0.4, y: sy + 0.35, w: 0.8, h: 0.8,
      fill: { color: C.gold }, line: { color: C.gold },
    });
    s.addText(st.n, {
      x: x + cw / 2 - 0.4, y: sy + 0.35, w: 0.8, h: 0.8,
      fontSize: 18, fontFace: F.mono, color: C.bgDark, bold: true, align: "center", valign: "middle", margin: 0,
    });
    s.addText(st.t, {
      x: x + 0.2, y: sy + 1.35, w: cw - 0.4, h: 0.6,
      fontSize: 20, fontFace: F.headerKR, color: C.textMain, bold: true, align: "center", margin: 0,
    });
    s.addText(st.d, {
      x: x + 0.2, y: sy + 2.0, w: cw - 0.4, h: 1.3,
      fontSize: 12, fontFace: F.body, color: C.textMuted, align: "center", margin: 0,
    });
  });

  s.addText("↻  버전을 거듭할수록 만족도가 올라갔습니다.", {
    x: 0.8, y: H - 0.85, w: 11.7, h: 0.4,
    fontSize: 14, fontFace: F.body, color: C.gold, italic: true, align: "center", margin: 0,
  });
}

// =====================================================
// Slide 16 — 피드백 결과 (해결 문제 수 / 만족도)
// =====================================================
{
  const s = pres.addSlide();
  addBg(s);
  addTitle(s, 16, "피드백 결과", "숫자로 보는 개선");

  // 좌: 해결된 문제 / 버전별 이슈
  card(s, 0.8, 2.6, 6.0, 4.4, C.bgPanel);
  s.addText("해결된 문제 / 버전별 이슈", {
    x: 1.0, y: 2.8, w: 5.6, h: 0.4,
    fontSize: 13, fontFace: F.mono, color: C.gold, bold: true, charSpacing: 3, margin: 0,
  });
  // 막대 차트
  s.addChart(pres.charts.BAR, [
    { name: "발견",   labels: ["v1", "v2", "v3"], values: [12, 8, 4] },
    { name: "해결",   labels: ["v1", "v2", "v3"], values: [10, 7, 4] },
  ], {
    x: 1.0, y: 3.3, w: 5.6, h: 3.5, barDir: "col",
    chartColors: [C.red, C.green],
    chartArea: { fill: { color: C.bgPanel } },
    catAxisLabelColor: C.textMuted,
    valAxisLabelColor: C.textMuted,
    catAxisLabelFontSize: 10,
    valAxisLabelFontSize: 10,
    valGridLine: { color: C.divider, size: 0.5 },
    catGridLine: { style: "none" },
    showLegend: true,
    legendPos: "b",
    legendColor: C.textMuted,
    legendFontSize: 10,
  });

  // 우: 만족도 라인
  card(s, 7.0, 2.6, 5.5, 4.4, C.bgPanel2);
  s.addText("버전별 만족도 상승률", {
    x: 7.2, y: 2.8, w: 5.1, h: 0.4,
    fontSize: 13, fontFace: F.mono, color: C.gold, bold: true, charSpacing: 3, margin: 0,
  });
  s.addChart(pres.charts.LINE, [
    { name: "만족도", labels: ["v1", "v2", "v3", "Final"], values: [62, 74, 83, 91] },
  ], {
    x: 7.2, y: 3.3, w: 5.1, h: 3.5,
    chartColors: [C.pink],
    chartArea: { fill: { color: C.bgPanel2 } },
    catAxisLabelColor: C.textMuted,
    valAxisLabelColor: C.textMuted,
    catAxisLabelFontSize: 10,
    valAxisLabelFontSize: 10,
    valGridLine: { color: C.divider, size: 0.5 },
    catGridLine: { style: "none" },
    lineSize: 4, lineSmooth: true,
    lineDataSymbol: "circle", lineDataSymbolSize: 10,
    showLegend: false,
    valAxisMinVal: 0, valAxisMaxVal: 100,
  });

  s.addText("※  실제 수치는 최종 빌드 후 갱신 예정", {
    x: 0.8, y: H - 0.5, w: 12, h: 0.3,
    fontSize: 10, fontFace: F.mono, color: C.textMuted, italic: true, margin: 0,
  });
}

// =====================================================
// Slide 17 — 성과
// =====================================================
{
  const s = pres.addSlide();
  addBg(s);
  addTitle(s, 17, "성과", "남긴 것들");

  const stats = [
    { big: "NNN",  unit: "회",   label: "다운로드",      color: C.gold },
    { big: "NN",   unit: "명",   label: "플레이어 수",    color: C.cyan },
    { big: "NN",   unit: "건",   label: "해결한 이슈",    color: C.green },
    { big: "NN",   unit: "%",    label: "만족도",         color: C.pink  },
  ];

  const cw = 2.85, gap = 0.25, ch = 3.5;
  const totalW = cw * 4 + gap * 3;
  const sx = (W - totalW) / 2;
  const sy = 2.8;

  stats.forEach((st, i) => {
    const x = sx + i * (cw + gap);
    card(s, x, sy, cw, ch, C.bgPanel);
    s.addShape(pres.shapes.RECTANGLE, {
      x, y: sy, w: cw, h: 0.1, fill: { color: st.color }, line: { color: st.color },
    });
    s.addText(st.label, {
      x: x + 0.2, y: sy + 0.35, w: cw - 0.4, h: 0.45,
      fontSize: 13, fontFace: F.mono, color: st.color, bold: true, charSpacing: 3, margin: 0,
    });
    s.addText([
      { text: st.big, options: { fontSize: 56, color: C.textMain, bold: true } },
      { text: " " + st.unit, options: { fontSize: 22, color: C.textMuted } },
    ], {
      x: x + 0.2, y: sy + 0.95, w: cw - 0.4, h: 1.6,
      fontFace: F.mono, valign: "middle", margin: 0,
    });
    s.addShape(pres.shapes.LINE, {
      x: x + 0.4, y: sy + 2.7, w: cw - 0.8, h: 0,
      line: { color: C.divider, width: 1 },
    });
    s.addText("최종 빌드 기준", {
      x: x + 0.2, y: sy + 2.85, w: cw - 0.4, h: 0.4,
      fontSize: 10, fontFace: F.mono, color: C.textMuted, italic: true, margin: 0,
    });
  });

  // 캐릭터 (성과 강조)
  s.addImage({ path: GIF.stayBack, x: 0.6, y: H - 1.3, w: 0.8, h: 0.8 });
  s.addText("※  실제 수치는 발표 직전 채워서 갱신 예정", {
    x: 1.6, y: H - 1.05, w: 11, h: 0.3,
    fontSize: 11, fontFace: F.mono, color: C.textMuted, italic: true, margin: 0,
  });
}

// =====================================================
// Slide 18 — Q&A
// =====================================================
{
  const s = pres.addSlide();
  addBg(s);

  s.addShape(pres.shapes.RECTANGLE, {
    x: 0, y: 0, w: 0.25, h: H, fill: { color: C.gold }, line: { color: C.gold },
  });

  s.addText("THE END", {
    x: 0.8, y: 2.2, w: 12, h: 0.5,
    fontSize: 16, fontFace: F.mono, color: C.gold, bold: true, charSpacing: 8, margin: 0,
  });

  s.addText("질문 환영합니다.", {
    x: 0.8, y: 2.8, w: 12, h: 1.4,
    fontSize: 68, fontFace: F.headerKR, color: C.textMain, bold: true, margin: 0,
  });

  s.addText("Lost Memory 의 어떤 부분이든 좋아요.", {
    x: 0.8, y: 4.25, w: 12, h: 0.6,
    fontSize: 22, fontFace: F.body, color: C.textMuted, margin: 0,
  });

  // 캐릭터들 줄지어서
  s.addImage({ path: GIF.stayFront, x: 9.5, y: 5.3, w: 1.0, h: 1.0 });
  s.addImage({ path: GIF.runFront,  x: 10.7, y: 5.3, w: 1.0, h: 1.0 });
  s.addImage({ path: GIF.stayBack,  x: 11.9, y: 5.3, w: 1.0, h: 1.0 });

  s.addShape(pres.shapes.LINE, {
    x: 0.8, y: 6.4, w: 3.5, h: 0, line: { color: C.gold, width: 2 },
  });
  s.addText("김회인  ·  gimhoein@gmail.com", {
    x: 0.8, y: 6.55, w: 9, h: 0.4,
    fontSize: 15, fontFace: F.body, color: C.textMain, margin: 0,
  });
  s.addText("S14 자율프로젝트  ·  광주 C201  ·  2026", {
    x: 0.8, y: 7.0, w: 9, h: 0.35,
    fontSize: 11, fontFace: F.mono, color: C.textMuted, margin: 0,
  });
}

// ====== Output ======
pres.writeFile({ fileName: "C:/ssafy/free_project/S14P31C201/client/docs/khi/presentation/lost_memory_presentation.pptx" })
  .then(fn => console.log("written: " + fn));
