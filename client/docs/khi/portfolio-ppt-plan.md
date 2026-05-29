# 게임 포트폴리오 PPT 화 + Ohst 추가 + 서유기 보완

## Context

기존 게임 포트폴리오 PDF(`C:\ssafy\planing_e\김회인 포트폴리오_게임.pdf`, 12페이지)를 동일 디자인 패턴 유지하면서 PPT(.pptx)로 변환. 이번 작업에서 함께 처리할 것:

1. **신규 추가**: Ohst (`C:\ssafy\planing_e\Ohst_TheGhostOfHaverWorld\`) 프로젝트 — 본인은 *기믹/트랩/레벨 인터랙션* 영역 담당 (보스/AI 아님). git config 혼선으로 본인 명의에 팀원 커밋이 섞여있어 정직한 라벨링 필수.
2. **신규 추가**: LostMemory (SSAFY 현재 진행 작품) — 무기 3종, 멀티 동기화, 크리티컬 시스템 등 시스템 영역. 1슬라이드 압축, 메인 출시작 자리는 아님.
3. **서유기 보완**: 학부 3학년 첫 팀플 라벨링, "A* 알고리즘 최적화" → "A* Pathfinding Project 에셋 도입" 정정, 회고(Before/After) 1슬라이드 추가.

목적: 신입 게임 클라이언트 채용용. *현재 작품(LostMemory) → 출시/데이터 경험(ESCAPE ROOM) → 시기별 학습 곡선(Ohst → 서유기)* 의 서사 구축.

**중요 — ESCAPE ROOM 프레이밍 주의**: 포트나이트에 9개 출시했으나 *현재는 활성 플레이어 거의 없는 상태*. "현재 운영 중" 같은 현재형 표현 금지. 모든 표현을 *경험*과 *방법론*에 무게 두는 과거형으로. "출시했고, 데이터로 개선 사이클을 돌렸던 경험" 으로 프레임.

## Source Materials

| 자료 | 경로 | 용도 |
|---|---|---|
| 기존 게임 PDF | `C:\ssafy\planing_e\김회인 포트폴리오_게임.pdf` | 디자인 레퍼런스 + 본문 80% 재사용 |
| 백엔드 PDF | `C:\Users\SSAFY\Documents\김회인_포트폴리오.pdf` | 참고용 (이번엔 사용 안 함) |
| Ohst 레포 | `C:\ssafy\planing_e\Ohst_TheGhostOfHaverWorld\Ohst_TheGhostOfHaverWorld\` | 본인 작성 파일 식별 (`HoeIn/` 폴더) |
| 서유기 레포 | https://github.com/ungan/Romance-of-Journey-to-the-West | 참조 (코드 재사용 없음) |
| LostMemory 코드 | `client/LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/` | 무기/액션 시스템 |
| LostMemory 동료 | `client/LostMemory/Assets/_Project/Scripts/Runtime/MagicalGirl/` | 동료 AI |
| LostMemory 공통 | `client/LostMemory/Assets/_Project/Scripts/Runtime/Combat/CriticalRoller.cs` | 크리티컬 시스템 |

## 디자인 패턴 (기존 PDF에서 추출, 그대로 유지)

- **비율**: 16:9 와이드
- **배경**: 흰색 / 매우 옅은 회색
- **상단 헤더**: 좌 "2025" / 중앙 "Portfolio" / 우 "KimHoeIn" — 모두 옅은 회색, 작은 글씨
- **제목 좌측**: 굵은 검정 세로 막대 + 굵은 검정 타이틀
- **부제(있을 시)**: 타이틀 아래 옅은 회색 작은 글씨
- **본문**: 검정 텍스트, 충분한 여백
- **표**: 검정 얇은 외곽선, 헤더 굵게, 셀 padding 넉넉히
- **코드 캡처**: 어두운 배경(검정/짙은 회색) + 신택스 컬러 유지 (이미지 삽입 형태)
- **하단**: 좌 챕터명(굵게), 우 페이지 번호 — 위에 얇은 검정 가로선
- **폰트**: 한글 — Pretendard 또는 Spoqa Han Sans (없으면 Malgun Gothic), 영문 — 동일 폰트 가족 또는 시스템 산세리프
- **컬러**: 흑백 + 회색. *액센트 컬러 없음* (백엔드 PDF의 청·보라와 의도적으로 다름)

## 최종 슬라이드 구성 (총 16장)

| # | 챕터 | 슬라이드 제목 | 내용 요약 | Status |
|---|---|---|---|---|
| 1 | — | 표지 | "안녕하세요. 개발자 김회인 입니다." | 기존 유지 |
| 2 | — | 목차 | 01 LostMemory / 02 ESCAPE ROOM / 03 Ohst / 04 서유기 연의 / 05 데이터 활용 예시 | 신규 (순서 재구성) |
| 3 | **LostMemory (NEW)** | 프로젝트 표 + 담당 시스템 | 표 상단 + 무기/액션/멀티/VFX 영역 하단 | **신규** |
| 4 | ESCAPE ROOM | 프로젝트 표 | 플랫폼/장르/인원/기간/도구/링크/요약/담당 표. **요약 표현 과거형으로 정정** | **본문 정정** |
| 5 | ESCAPE ROOM | 클래스 구조 — Gimic Controller 개요 | 모듈화 설명 + 클래스 트리 | 기존 유지 |
| 6 | ESCAPE ROOM | 클래스 구조 — 코드 | `@editable` 배열 선언 코드 캡처 | 기존 유지 |
| 7 | ESCAPE ROOM | 클래스 구조 — 인스펙터 | UEFN 인스펙터 캡처 + 자동화 설명 | 기존 유지 |
| 8 | **Ohst (NEW)** | 프로젝트 표 | 플랫폼/장르/인원/기간/엔진/팀/요약/담당 표 | **신규** |
| 9 | **Ohst (NEW)** | 담당 영역 — 기믹 / 트랩 / 컷씬 | 본인 작성 파일 목록 + git config 혼선 노트 | **신규** |
| 10 | 서유기 연의 | 프로젝트 표 | **[학부 3학년 첫 팀플]** 라벨 추가, "astar 알고리즘" → "A* Pathfinding Project 도입" 정정 | **본문 정정** |
| 11 | 서유기 연의 | 보스 구현 — 개발 배경 | 코드 + 코루틴 기반 설명 | 기존 유지 |
| 12 | 서유기 연의 | 보스 구현 — 문제점 및 개선점 | 비대화 / 플래그 난립 / 상태머신 필요 자평 | 기존 유지 |
| 13 | 서유기 연의 | 몬스터 문제 → 해결 | OverlapCircleAll 폴링 전환 (기존 2장 → 1장 통합) | **2→1 통합** |
| 14 | 서유기 연의 | 회고 — Before / After | 1100줄 보스 → Ohst 기믹 분리 / ESCAPE Gimic Controller 모듈화로 학습 적용 | **신규** |
| 15 | 데이터 활용 | 적용 배경 + 생존률 그래프 | **과거형 표현** ("운영 경험"), 방법론 강조 | **본문 정정** |
| 16 | — | 마무리 / Contact | 짧은 클로징 + 이메일 / 깃허브 | **신규** |

## 신규 슬라이드 상세 콘텐츠

### Slide 7 — Ohst 프로젝트 표

| 항목 | 값 |
|---|---|
| 플랫폼 | pc(window) |
| 장르 | 2D 액션 / 메트로배니아 풍 |
| 개발 인원 | 4명 |
| 개발 기간 | 2023.10 ~ 2023.12 |
| 개발 도구 | C#, Unity |
| 영상 링크 | (시연 영상 URL — 유저에게 받을 것) |
| 요약 | 4인 팀 2D 액션 프로젝트. 본인은 레벨 기믹 / 인터랙션 프로그래머로 트랩, 이동 장치, 잡기 오브젝트, 컷씬 트리거 담당. 보스 / 적 AI는 팀원 작업. |
| 담당 내용 | • 트랩 시스템 (떨어지는 돌 / 안개 / 폭탄 등 ~15종)<br>• 이동 장치 (움직이는 발판 / 레버 연동 / 트리거 이동)<br>• 컷씬 트리거 (오스트 첫 만남, 거짓 바닥)<br>• 점성술사 던전 B1/B2 레벨 메커니즘<br>• 잡기 / 박스 / 벽 파괴 인터랙션 (팀원과 공동 작업) |

### Slide 8 — Ohst 담당 영역 상세

**좌측 — 작성 파일 목록 (HoeIn 폴더 27개 .cs)**

3개 그룹으로 정리:
1. **트랩**: Trap_trigger, TrapTrigger_khi, RockBallTrap, FogBallTrap, FallingBall, break_rock, falling_rock, wood_box
2. **이동 장치**: MovingDevice_khi, MovingDevice2, MovingRock_double_set, movingtape, moving_ground_delay, moveaftertrigger_ground, Scaffolding_signal, iron_bar_gate, spaer_lever
3. **컷씬 / 기타**: CutScene_Encounter, FakeFloor_Cutscene, deahana_disapear, crystalball, make_ball, wave_system, RB_Manager, trapevent

**우측 — 회고 박스**

> **서유기에서 보스 1100줄로 부풀린 경험을 반성하여, Ohst에서는 기믹마다 독립 클래스로 분리. 평균 파일 크기 50줄. 트리거-이벤트 패턴 정착 (Trap_trigger / trapevent 명명 컨벤션).**

**하단 작은 글씨 — 정직성 노트**
> 본인 실제 작성 영역: `Assets/A_OriginFiles/A_TeamFiles/HoeIn/` 폴더. 일부 SeHyeon/ 폴더 트랩/잡기 파일에도 공동 기여. 보스 / 적 AI / 메인 시스템은 팀원 담당.

### Slide 9 — LostMemory 1슬라이드 (압축)

**상단 표 (4행)**

| 항목 | 값 |
|---|---|
| 플랫폼 | PC (Windows) |
| 장르 | 2인 협력 던전 크롤러 RPG |
| 엔진 / 멀티 | Unity 6 + Netcode for GameObjects |
| 기간 / 인원 | 2026 진행 중 / SSAFY 자율 프로젝트 팀 |

**하단 — 담당 시스템 4 박스 (2x2)**

| 무기 시스템 | 액션 / 전투 |
|---|---|
| 활(Bow) — 단발/연사<br>스태프 — 마법탄/파이어볼/메테오 차징<br>화염방사기 — Primary/Secondary + DoT | 대시 + 애프터이미지<br>근접 콤보 + 히트박스<br>패리 / 대거 텔레포트 |

| 전투 피드백 / VFX | 멀티플레이 / 동료 AI |
|---|---|
| 크리티컬 통합 헬퍼 (CriticalRoller)<br>히트 스톱 / 스턴 / 플래시 | 공격 브로드캐스트 채널<br>플레이어 동기화 / 협력 부활 UI<br>매지컬걸 동료 AI (카탈로그 기반) |

**하단 한 줄**: *현재 진행 중 — 시스템 프로그래밍 영역 작업 (Unity 6 + URP + Netcode).*

### Slide 14 — 서유기 회고 (Before / After)

**제목**: 서유기 연의 — 회고

**부제**: 학부 3학년 첫 팀플에서 다음 프로젝트로

**좌우 2열 비교 박스**:

| Before (학부 3학년, 2022) | After (이후 프로젝트, 2023~) |
|---|---|
| boss_nachal.cs **1,100줄** 단일 클래스 | Ohst: 기믹마다 **독립 클래스 분리** (평균 50줄) |
| boolean flag **20+개** (isLight_*, isboss_patton_*) | ESCAPE ROOM: **enum 기반 상태 / Gimic Controller 모듈화** |
| Update에 if 폭포 | 트리거-이벤트 패턴 / 콜백 분리 |
| 보스 패턴 추가 시 클래스 수정 | 기믹 추가 시 새 클래스 + 배열 등록만 |

**하단 한 줄**: *서유기에서 자평한 "상태머신 / ENUM 필요"는 그 다음 두 프로젝트에서 실제로 적용되었습니다.*

### Slide 10 — 서유기 표 정정 (변경분만)

| 변경 | Before | After |
|---|---|---|
| 라벨 추가 | — | **[학부 3학년 첫 팀플]** 부제로 추가 |
| 담당 표현 정정 | "오브젝트 풀, astar 알고리즘 최적화, 적 탐지 최적화" | "오브젝트 풀 / **A\* Pathfinding Project 에셋 도입** / 적탐지 폴링 최적화 (OverlapCircleAll 0.1초 주기)" |
| 담당 표현 정정 | "github 관리" | (삭제 — 커밋 메시지가 검증 불가능) |

### Slide 16 — 마무리

- 짧은 한 문장: *"코드 다루는 폭과 학습 곡선을 함께 보여드리는 포트폴리오였습니다. 감사합니다."*
- Contact: 이메일 (gimhoein@gmail.com), GitHub (URL 유저 확인 필요)

## 출력 사양

- **파일**: `C:\ssafy\planing_e\김회인_포트폴리오_게임_v2.pptx`
- **비율**: 16:9 (Widescreen, 13.333 × 7.5 inch)
- **편집 가능**: 모든 텍스트는 PPT 텍스트 박스 (이미지화 금지). 표/도형도 PPT 네이티브 객체.
- **이미지 자료**: 기존 PDF의 코드 캡처 / 인스펙터 스크린샷은 필요 시 PDF에서 추출하여 슬라이드에 삽입 (있는 그대로). 추출 어려우면 유저에게 원본 이미지 요청.

## 구현 단계

1. **pptx 스킬 호출** (`anthropic-skills:pptx`) — PPT 생성/편집 도구 활성화
2. **빈 16:9 deck 생성** — `C:\ssafy\planing_e\김회인_포트폴리오_게임_v2.pptx`
3. **마스터 슬라이드 설정** — 상단 헤더(2025/Portfolio/KimHoeIn), 하단 footer/페이지번호, 좌측 검정 막대 placeholder
4. **슬라이드 1-2 (표지 + 목차)** — 기존 PDF 텍스트 그대로
5. **슬라이드 3-6 (ESCAPE ROOM 4장)** — 기존 PDF 콘텐츠 PPT 형태로 (표/이미지/텍스트). 기존 PDF에서 이미지 추출 필요
6. **슬라이드 7-8 (Ohst 신규 2장)** — 위 상세 콘텐츠대로 작성
7. **슬라이드 9 (LostMemory 신규 1장)** — 위 상세 콘텐츠대로 작성
8. **슬라이드 10-14 (서유기 5장)** — 기존 PDF 콘텐츠 + 정정 사항 + 회고 신규 1장 추가
9. **슬라이드 15 (데이터 활용)** — 기존 PDF 콘텐츠 그대로
10. **슬라이드 16 (마무리)** — 신규 짧은 클로징
11. **검수** — 전체 16장 일관성 확인 (폰트, 좌측 막대, footer, 페이지 번호)

## 유저에게 추가로 받아야 할 자료

| 자료 | 필요한 이유 |
|---|---|
| Ohst 시연 영상 / 출시 페이지 URL | Slide 7 영상 링크 칸 |
| LostMemory GitHub URL (또는 GitLab) | Slide 9 또는 마무리 |
| 본인 GitHub 프로필 URL | Slide 16 Contact |
| 기존 PDF의 코드 캡처 이미지 (가능하면) | Slide 11 (서유기 보스 코드), Slide 13 (몬스터 해결 코드), Slide 5 (ESCAPE 코드) |
| Ohst 게임 스크린샷 1-2장 (가능하면) | Slide 7/8 시각 자료 |
| LostMemory 스크린샷 1-2장 (무기 시연, 던전, 멀티 등) | Slide 9 시각 자료 |

자료가 늦으면 placeholder 박스로 두고 자료 도착 시 교체.

## 결정된 사항 / 미결정 사항

**결정**:
- 저장 위치: `C:\ssafy\planing_e\` ✓
- 프로젝트 순서: ESCAPE → Ohst → LostMemory → 서유기 → 데이터 ✓
- Ohst 분량: 2슬라이드 ✓
- LostMemory: 1슬라이드 (사용자 우려 반영 — 메인 포지션 회피) ✓
- 서유기: 옵션 A (회고 1장 추가 + 본문 정정) ✓

**미결정 / 사용자 자료 대기**:
- 시연 영상 URL들
- 스크린샷 이미지

## 검증 방법

1. **시각 검증**: 완성된 `.pptx`를 PowerPoint 또는 LibreOffice Impress로 열어 슬라이드별 디자인 확인
   - 기존 게임 PDF 옆에 띄우고 디자인 일관성 비교 (좌측 막대, 헤더, footer 동일 여부)
2. **PDF 내보내기 비교**: PPT → PDF 내보내기 후 기존 게임 PDF와 좌우 비교
3. **콘텐츠 정합성**:
   - Ohst 페이지의 작성 파일 목록이 실제 레포 `HoeIn/` 폴더 27개와 일치
   - 서유기 표의 "A* Pathfinding Project" 표현 확인 (이전 "astar 알고리즘" 잔재 없는지)
   - 서유기 회고 페이지의 "1100줄", "20+ flag" 같은 숫자 정확성
4. **링크 검증**: Contact 이메일 / GitHub URL 클릭 가능 여부

## 위험 요소

- **이미지 추출 어려움**: 기존 PDF의 코드 캡처를 깔끔하게 추출 못 할 수 있음. 차선: 코드 텍스트만 monospace 박스로 재현
- **폰트 호환**: Pretendard 미설치 환경에선 대체 폰트(Malgun Gothic). 사용자 PC에서 확인 필요
- **분량 압박**: 16장은 신입 포트폴리오로 적정선. 13장 미만이면 가볍게 보일 수 있고 20장 넘으면 면접관이 끝까지 안 봄. 현재 16장 = OK
