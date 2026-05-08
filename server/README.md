# Server

백엔드 및 서비스 API 프로젝트 폴더.

아래는 개발 계획입니다.

# Roguelike Co-op Game — 백엔드/인프라 개발 계획

> 싸피 자율 프로젝트 | 5주 개발 계획 | 백엔드/인프라 2인 담당

## 목차

- [프로젝트 개요](#프로젝트-개요)
- [기술 스택](#기술-스택)
- [아키텍처](#아키텍처)
- [팀 구성 및 역할](#팀-구성-및-역할)
- [배포 마일스톤](#배포-마일스톤)
- [주차별 계획](#주차별-계획)
- [주요 의사결정 사항](#주요-의사결정-사항)
- [리스크 및 대응](#리스크-및-대응)
- [평가 산출물 체크리스트](#평가-산출물-체크리스트)

---

## 프로젝트 개요

**2인 협동 로그라이트 게임**의 백엔드/인프라 구축 계획입니다.

### 목표

- 2인이 각자 PC에서 접속해 첫 스테이지 보스까지 완주 가능한 데모
- 싸피 제공 VM에 HTTPS 기반으로 배포
- 발표 시연에 안정적으로 동작하는 수준

### 범위

**포함**
- 게스트 로그인, JWT 기반 인증
- 유저 진행 저장 / 런 결과 기록
- 유물/적/상점 데이터 서빙
- 메타 성장 최소 버전 (기억 시스템 1개)
- CI/CD 자동 배포, HTTPS, 모니터링

**제외**
- 실시간 게임서버 운영 (Unity Relay가 담당)
- 랭킹 시스템
- 관리자 페이지
- 재접속 복구, 호스트 마이그레이션
- 4인 이상 플레이

---

## 기술 스택

| 계층 | 기술 |
|---|---|
| 백엔드 | Spring Boot 3.x + Java 17 |
| 데이터베이스 | PostgreSQL 15 |
| 캐시 | Redis 7 |
| 리버스 프록시 | Nginx |
| 컨테이너 | Docker + docker-compose |
| CI/CD | GitLab + Jenkins |
| 실시간 네트워킹 | Unity Relay (UGS) + NGO |
| SSL | Let's Encrypt (certbot) |
| 모니터링 | netdata 또는 Prometheus+Grafana |
| 이슈 트래킹 | Jira + Notion |
| 인프라 | 싸피 제공 VM (Ubuntu, 16GB RAM, 4 vCPU) |

---

## 아키텍처

### 네트워크 분리 구조

게임의 네트워크는 두 계층으로 분리되며, 실시간 게임 트래픽은 두 가지 Relay 모드를 공존 운영합니다.

**1. 실시간 게임 트래픽** (Unity Relay 또는 자체 Relay 7777/UDP)
- **옵션 A — Unity Relay**: 플레이어 PC ↔ Unity Relay ↔ 플레이어 PC (외부 인프라, 포트포워딩 불필요, 초대코드 6자리)
- **옵션 B — 자체 Relay**: 플레이어 PC ↔ 싸피 VM Nginx (UDP 7777) ↔ 자체 Netty Relay 컨테이너 ↔ 플레이어 PC (백엔드 인프라)
- 클라이언트가 NGO Transport 로 두 모드 중 선택. 자체 Relay 운영 절차는 [자체 Relay 운영 (UDP 7777)](#자체-relay-운영-udp-7777) 참고

**2. 일반 서비스 요청** (우리 백엔드 담당)
- 플레이어 PC → 싸피 VM (HTTPS)
- 인증, 진행 저장, 런 결과, 데이터 서빙

### 배포 구조

싸피 VM 1대에 다음 컨테이너가 함께 기동됩니다.

```
[싸피 VM]
 ├─ Nginx        (리버스 프록시, HTTPS 종단 + UDP 7777 stream proxy)
 ├─ Spring Boot  (백엔드 API — server-app:latest 의 ServerApplication main)
 ├─ Relay        (자체 Netty UDP — server-app:latest 의 RelayApplication main 재사용)
 ├─ PostgreSQL   (데이터 영속)
 ├─ Redis        (세션/캐시)
 └─ Jenkins      (CI/CD)
```

**호스트 기반 + Relay 선택 이유**
- MVP 범위가 2인 코옵이라 전용 게임서버 필요성이 낮음
- Relay 사용 시 플레이어의 포트포워딩/공유기 설정 불필요
- 백엔드/인프라 2인으로 5주 내 구현 가능한 범위
- Steam 출시 시점에 NGO Transport 계층만 교체하여 전용 서버로 마이그레이션 가능

---

## 팀 구성 및 역할

**백엔드/인프라 2인** (송주헌 / 손홍민)

| 담당 | 주 역할 |
|---|---|
| **송주헌** | 인프라 주담당 — AWS/Docker/CI-CD 경험 |
| **손홍민** | 백엔드 API 주담당 — Spring Boot 프로젝트 경험 |

**협업 원칙**
- 고정 분리가 아닌 주담당 체제. 필요시 상호 지원
- 멀티플레이 동기화는 클라이언트 책임. 백엔드는 테스트 환경/로그/배포로 지원
- 실시간 룸 중계는 Unity Services가 담당. 백엔드는 계정/저장/운영에 집중

---

## 배포 마일스톤

| 배포 | 시점 | 의미 | 핵심 DoD |
|---|---|---|---|
| **1차 MVP** | W2 말 | 내부 테스트 | HTTP + 로그인 + 진행 저장 동작 |
| **2차 MVP** | W3 말 | 외부 접근 가능 | HTTPS + 큰방 루프 + 런 결과 저장 |
| **3차 MVP** | W4 말 | 데모 후보 | 보스전 완주 + 모니터링 |
| **최종 배포** | W5 말 | 발표 당일 | 버그 0 + 문서 완성 + 리허설 완료 |

---

## 주차별 계획

### 1주차 — 싸피 환경 + 공통 기반

**핵심**
- 싸피 VM 위에서 모든 컨테이너가 기동되는 상태
- 팀 전체가 합의한 기술 결정이 문서화된 상태

**세부 작업**

#### 송주헌 (인프라 주담당) — 19 SP

- **INFRA-0** 팀 킥오프 안건 정리 및 논의 (2 SP) — Highest
  - 네트워크 구조(호스트+Relay), 인증 방식, 데이터 관리 방식 3개를 월요일 스탠드업에서 합의
  - 결정 내용을 Notion에 문서화

- **INFRA-1** 싸피 VM 접속 및 환경 파악 (2 SP) — Highest
  - SSH 접속 확인
  - OS/메모리/CPU/디스크 확인 (`cat /etc/os-release`, `free -h`, `nproc`, `df -h`)
  - 팀원 SSH 키 등록

- **INFRA-2** 싸피 VM 네트워크 정책 확인 (1 SP) — Highest
  - 80/443 인바운드 개방 확인
  - 아웃바운드 HTTPS 443 확인
  - ※ 플레이어 PC의 Relay UDP 접속 테스트는 클라팀 담당

- **INFRA-3** VM 기본 패키지 설치 (1 SP) — High
  - Docker, docker-compose, git, curl, vim, htop
  - 비root 사용자에 docker 권한 부여

- **INFRA-4** 싸피 도메인 확인 및 HTTP 접근 세팅 (2 SP) — High
  - 싸피 제공 도메인(`jXXX.p.ssafy.io` 형태) 확인
  - DNS가 VM IP 가리키는지 확인
  - 80 포트 응답 테스트

- **INFRA-5** 인프라 저장소 구조 및 .env 관리 원칙 (2 SP) — High
  - `infra/` 디렉토리 구조: `docker-compose.yml`, `nginx/`, `.env.example`
  - `.gitignore` 정리, 시크릿 관리 규칙

- **INFRA-6** PostgreSQL 컨테이너 구성 (3 SP) — High
  - compose에 postgres 15+ 서비스
  - named volume 데이터 영속화
  - healthcheck 설정

- **INFRA-7** Redis 컨테이너 구성 (2 SP) — High
  - compose에 redis 서비스
  - `requirepass` 설정, AOF 영속화

- **INFRA-8** Nginx 리버스 프록시 초기 설정 (3 SP) — High
  - `/api/*` → Spring Boot(`app:8080`) 프록시
  - gzip, timeout, proxy 헤더
  - HTTPS는 W3에서

#### 손홍민 (백엔드 주담당) — 16 SP

- **BE-1** 선결정 항목 확정 참여 (2 SP) — Highest
  - 인증 방식(게스트 추천), 데이터 관리 방식(JSON 서빙 추천)

- **BE-2** DB ERD 설계 초안 (3 SP) — High
  - 유저/유저진행/런결과/메타성장 테이블
  - 발표 자료용 ERD 이미지

- **BE-3** Spring Boot 프로젝트 초기 구조 (3 SP) — High
  - 전역 예외 처리, 응답 포맷 통일(ApiResponse)
  - 패키지 구조: controller/service/repository/dto/entity
  - 로깅 구조

- **BE-4** 게스트 로그인 API 구현 시작 (5 SP) — High
  - `POST /api/auth/guest`
  - UUID 기반 게스트 ID 생성
  - JWT 발급 (access + refresh)

- **BE-5** UGS 프로젝트 생성 및 환경변수 공유 (1 SP) — Highest
  - UGS 대시보드에서 프로젝트 생성
  - Relay/Sessions 서비스 활성화
  - Project ID/환경키 팀 공유

- **BE-6** API 명세 초안 및 Swagger 설정 (2 SP) — Medium
  - `springdoc-openapi` 설정
  - `/swagger-ui` 접근 가능하게

**1주차 말 DoD**
- [ ] 팀원 전원 VM 접속 가능
- [ ] `docker-compose up` 으로 DB/Redis/Nginx 기동
- [ ] Spring Boot 로컬 기동 + 샘플 API 200 OK
- [ ] 선결정 3개 항목 문서화 완료
- [ ] UGS 프로젝트 생성 완료

---

### 2주차 — 1차 MVP 배포 (내부 테스트)

**핵심**
- Spring Boot가 싸피 VM에 올라가서 Unity 클라의 로그인 요청을 처리하는 상태
- GitLab push → Jenkins 빌드 → 배포가 한 번이라도 성공한 상태

**배포 의미**
외부 공개 아님. 팀원과 코치가 내부 접속 테스트 가능한 수준. HTTPS는 아직 없음.

**세부 작업**

#### 송주헌 — 15 SP

- **INFRA-9** Spring Boot Dockerfile (3 SP) — High
  - multi-stage build (gradle build → JRE 17-slim)
  - non-root 사용자 실행, 이미지 크기 200MB 이하

- **INFRA-10** compose에 app 서비스 통합 (2 SP) — High
  - postgres/redis `depends_on` + healthcheck wait
  - 환경변수 주입, actuator `/health` 연결

- **INFRA-11** Jenkins 컨테이너 구성 (3 SP) — High
  - Jenkins LTS 컨테이너, 별도 볼륨/포트
  - Nginx `/jenkins` 프록시 + basic auth
  - 초기 관리자 계정, 필수 플러그인 설치

- **INFRA-12** GitLab → Jenkins Webhook 연동 (2 SP) — High
  - access token 발급
  - Jenkins GitLab 플러그인 설정
  - push 이벤트 시 job 자동 시작

- **INFRA-13** 빌드/배포 파이프라인 (3 SP) — High
  - Jenkinsfile: gradle build → docker build → push → 배포
  - 수동 트리거까지 완성

- **INFRA-14** 1차 MVP 배포 수행 및 검증 (2 SP) — Highest
  - 1차 배포 실행, 2인 접속 테스트

#### 손홍민 — 15 SP

- **BE-7** 게스트 로그인 + JWT 필터 완성 (5 SP) — High
  - Spring Security 설정
  - JWT 검증 필터, 인증/인가 흐름
  - refresh 토큰 API

- **BE-8** 유저 정보 조회 API (2 SP) — High
  - `GET /api/users/me`

- **BE-9** 진행 저장 API 1차 (5 SP) — High
  - `GET/PUT /api/progress`
  - save slot 단위, 현재 방/유물/체력 저장

- **BE-10** 클라 연동 지원 (3 SP) — High
  - Unity 클라 1차 연동
  - CORS 설정, Relay 연동 지원

**2주차 말 DoD (1차 MVP)**
- [ ] `http://jXXX.p.ssafy.io/api/health` 200 OK
- [ ] Unity 클라 게스트 로그인 → 토큰 수신 성공
- [ ] 진행 저장/조회 API 연동 확인
- [ ] GitLab push → Jenkins → 자동 배포 1회 성공
- [ ] 2인이 다른 PC에서 Relay 접속 + 백엔드 로그인 성공

---

### 3주차 — 2차 MVP 배포 (외부 접근 가능)

**핵심**
- HTTPS 적용으로 외부에서 안전하게 접근 가능
- 큰방 1세트 → 보상 → 상점 루프의 데이터가 저장되는 상태

**배포 의미**
중간평가 시연 가능 수준. 외부 공개 가능.

**세부 작업**

#### 송주헌 — 13 SP

- **INFRA-15** Let's Encrypt HTTPS 발급 및 Nginx 연결 (5 SP) — Highest
  - certbot 설치 (싸피 가이드 확인)
  - Nginx SSL 설정, HTTP → HTTPS 리다이렉트
  - 자동 갱신 cron

- **INFRA-16** CI/CD 배포 알림 연동 (2 SP) — Medium
  - Jenkins 실패/성공 알림을 Mattermost 또는 Slack으로

- **INFRA-17** 로그 수집 및 logrotate (2 SP) — Medium
  - docker logging driver 설정
  - 호스트 logrotate로 디스크 폭주 방지

- **INFRA-18** 배포 스크립트 리팩토링 (2 SP) — Medium
  - `deploy.sh`에 롤백 기능 추가
  - 태그 기반 이전 버전 복구

- **INFRA-19** 2차 MVP 배포 및 SSL 검증 (2 SP) — Highest
  - HTTPS 적용 후 전체 기능 검증
  - 중간평가 시연 수준 확인

#### 손홍민 — 15 SP

- **BE-11** 런 결과 저장 API (5 SP) — High
  - `POST /api/runs`, `GET /api/runs/history`
  - 스키마: 플레이타임/처치/획득유물/사망여부/2인여부

- **BE-12** 유물/적/상점 데이터 서빙 API (5 SP) — High
  - `src/main/resources/data/` 하위 JSON 관리
  - `GET /api/data/artifacts` 등
  - 응답 캐싱

- **BE-13** 진행 저장 고도화 (3 SP) — High
  - 획득 유물 배열, 현재 방 위치, 체력/재화

- **BE-14** Swagger 문서 1차 정리 + 클라 QA (2 SP) — Medium
  - 엔드포인트 설명/예시 보강

**3주차 말 DoD (2차 MVP)**
- [ ] `https://jXXX.p.ssafy.io/api/health` 200 OK
- [ ] SSL Labs 테스트 B 이상
- [ ] 2인 기준 큰방 1세트 → 보상 → 상점 → 전투 루프 저장 정상
- [ ] 유물 데이터 API 응답 OK
- [ ] 배포 실패 시 알림 수신 확인

---

### 4주차 — 3차 MVP 배포 (데모 후보 / 첫 스테이지 보스까지)

**핵심**
- 2인이 첫 스테이지 → 엘리트 → 보스 완주가 가능한 상태
- 발표 시나리오 1회 완주 성공

**배포 의미**
발표 후보 빌드. 시연 가능한 최소 품질.

**세부 작업**

#### 송주헌 — 15 SP

- **INFRA-20** 간단 모니터링 대시보드 (5 SP) — Medium
  - netdata 또는 cAdvisor+Prometheus+Grafana
  - 평가 가점 산출물

- **INFRA-21** 서버 상태 알림 (2 SP) — Medium
  - CPU/Mem/Disk 80% 임계 알림

- **INFRA-22** 발표용 아키텍처 다이어그램 업데이트 (3 SP) — High
  - drawio 최신화: Unity 게임서버 제거, UGS/Relay 반영
  - Jenkins/GitLab 포함 인프라 구성도

- **INFRA-23** 배포 롤백 리허설 + 런북 초안 (2 SP) — Medium
  - 접속/배포/롤백 절차
  - 자주 쓰는 명령어 모음

- **INFRA-24** 3차 MVP 배포 및 보스전 완주 검증 (3 SP) — Highest
  - 보스전 포함 한 판 완주 테스트
  - 재접속 진행 유지 확인

#### 손홍민 — 15 SP

- **BE-15** 메타 성장 최소 버전 (5 SP) — Medium
  - 기억 포인트 저장/소비
  - 구매 가능 능력 1~2개

- **BE-16** 런 결과 통계 집계 (3 SP) — Medium
  - 유저별 플레이 누적 통계
  - 발표용 `GET /api/stats/me`

- **BE-17** 히스토리 조회 + 재접속 진행 유지 (3 SP) — High
  - `GET /api/runs` 페이징
  - 재접속 시 save slot 복원

- **BE-18** API Swagger 완성 (2 SP) — High
  - 전체 엔드포인트 설명/예시/에러 코드
  - 발표 산출물 수준

- **BE-19** 보스전 완주 시나리오 QA 지원 (2 SP) — High
  - 페이즈 전환 저장 이슈
  - 결과 저장 엣지 케이스

**4주차 말 DoD (3차 MVP)**
- [ ] 2인 기준 첫 스테이지 → 엘리트 → 보스 완주 성공
- [ ] 결과 저장 + 재접속 시 진행 유지
- [ ] 모니터링 대시보드 접근 가능
- [ ] 발표 시나리오 1회 완주 성공
- [ ] 아키텍처 다이어그램 최신화

---

### 5주차 — 수정 + 최종 배포 (발표 대비)

**핵심**
- 신규 기능 추가 금지. 버그 수정과 품질 향상만.
- 모든 발표 산출물(문서, 다이어그램, 런북)이 완성된 상태.

**배포 의미**
발표 당일 동작 보장. 롤백 시나리오까지 리허설 완료.

**세부 작업**

#### 송주헌 — 15 SP

- **INFRA-25** 인프라 이슈 수정 (5 SP) — Highest
  - 3차 배포 플레이테스트에서 나온 인프라 이슈 처리

- **INFRA-26** DB 백업 1회 수동 + 스크립트화 (2 SP) — Medium
  - `pg_dump` 수행, 타임스탬프 파일명

- **INFRA-27** 운영 런북 + README + 인프라 구성도 최종 (5 SP) — Highest
  - 접속/배포/롤백/장애 대응 런북
  - README 최종판
  - 발표용 인프라 구성도

- **INFRA-28** 배포 리허설 2회 (1 SP) — High
  - 팀 전체 관찰 하에 배포 과정 리허설

- **INFRA-29** 최종 배포 및 발표 당일 준비 (2 SP) — Highest
  - 최종 배포, 비상 대응 체크리스트

#### 손홍민 — 15 SP

- **BE-20** 치명 버그 수정 (5 SP) — Highest
  - QA에서 나온 치명 버그 처리

- **BE-21** 에러 응답 포맷 최종 점검 + 로깅 정리 (3 SP) — High
  - 표준 에러 응답 확인
  - 로그 레벨 정리, 민감정보 마스킹

- **BE-22** API 명세 최종 + 테스트 코드 보강 (3 SP) — High
  - 주요 엔드포인트 MockMvc 테스트
  - 커버리지 50% 이상

- **BE-23** 발표용 시드 데이터 준비 (2 SP) — Medium
  - 발표 계정, 샘플 런 결과, 메타 성장 진척

- **BE-24** 최종 검증 + 롤백 시나리오 (2 SP) — High
  - 배포 리허설 참여
  - 롤백 시나리오 연습

**최종 DoD**
- [ ] 발표 시나리오 기준 3회 연속 완주 성공
- [ ] 치명 버그 0건
- [ ] 모든 발표 산출물 완성 (ERD, API, 아키텍처, 파이프라인)
- [ ] 백업 1회 수행
- [ ] 롤백 절차 1회 리허설 완료

---

## 주요 의사결정 사항

### 1. 네트워크 구조: 호스트 기반 + Unity Relay

**배경**
- 초기 drawio 아키텍처에는 Unity Headless 전용 게임서버 풀이 있었으나, 이는 MVP 범위 초과
- 기획서는 호스트 기반 + Relay 방식을 명시

**결정 근거**
- MVP가 2인 코옵 데모 수준이므로 전용 게임서버는 과투자
- "재접속 복구", "호스트 마이그레이션", "자동 매칭"은 모두 MVP 제외 항목
- Steam 출시가 추후 계획으로 빠져 전용 서버 정당화 근거 부족
- Unity Relay는 초대코드 기반이라 플레이어의 포트포워딩 불필요

**영향**
- 인프라 작업량 약 15 SP 감소
- 백엔드는 계정/저장/운영에만 집중
- 싸피 VM은 HTTPS 기반 API 서버로만 운영

### 2. 인증 방식: 게스트 로그인

**결정 근거**
- MVP 수준에 이메일/비밀번호나 소셜 로그인은 과잉
- 기획서도 "게스트 또는 간단 로그인"을 명시
- 구현 복잡도 최소화로 개발 속도 확보

**구현**
- UUID 기반 게스트 ID 자동 생성
- JWT (access + refresh) 발급
- 추후 확장 여지를 남긴 설계

### 3. 게임 데이터 관리: JSON 파일 서빙

**결정 근거**
- 유물 10종, 적 수 종 규모라 DB 테이블 관리 과잉
- 수치 밸런싱 시 코드와 함께 버전 관리 가능
- 관리자 UI 불필요

**구현**
- `src/main/resources/data/` 하위에 JSON 파일 배치
- 애플리케이션 기동 시 로드, 캐싱
- 변경 시 배포로 반영

### 4. 네트워크 트래픽 분리

**원칙**
- 실시간 게임 트래픽: Unity Relay가 담당 (UDP)
- 일반 서비스 요청: 우리 백엔드가 담당 (HTTPS)
- 두 트래픽은 완전히 독립. 싸피 VM은 실시간 트래픽 경로에 없음

---

## 리스크 및 대응

### 1. 플레이어 네트워크의 UDP 아웃바운드 차단

**위험도**: 중

**증상**
- 싸피 교육장 네트워크에서 Unity Relay 접속 실패 가능성
- 가능성은 낮지만 발견 시 개발/테스트 지연

**대응**
- 1주차에 클라팀이 실제 교육장 네트워크에서 Relay 접속 테스트 수행
- 막혀있을 경우 개인 핫스팟 또는 자택 네트워크로 테스트
- 발표 당일에도 핫스팟 대안 준비

### 2. UGS (Unity Gaming Services) 연동 지연

**위험도**: 중

**증상**
- UGS 콘솔 설정, SDK 연동에서 발견되지 않은 이슈 발생
- 클라팀 작업 진행 차단

**대응**
- 1주차 월요일에 UGS 프로젝트 생성 (BE-5)
- 화요일까지 최소 기능(Relay 호출) 확인
- 금요일까지 접속 테스트 완료

### 3. 5주 압축으로 인한 버퍼 부족

**위험도**: 높음

**증상**
- 기획서 8주 계획을 5주로 압축하여 버퍼 주차 없음
- 한 주 지연이 다음 배포에 직접 영향

**대응**
- 매주 금요일에 다음 주 컷 가능 항목 1개 사전 식별
- 배포는 수요일 1차 시도 → 목요일 수정 → 금요일 재배포 구조
- 5주차는 신규 기능 절대 금지 원칙 준수

### 4. 범위 팽창 압력

**위험도**: 중

**증상**
- "랭킹 좀 넣자", "메타 성장 더 넣자", "관리자 페이지 붙이자" 등의 요청
- 기획서 리스크 1번에도 명시된 사항

**대응**
- 컷 우선순위 사전 합의: 랭킹 → 관리자 UI → 메타 성장 축소 → 모니터링 고도화
- 신규 기능 제안은 "버퍼 확보 후 검토" 원칙

### 5. 단일 VM 장애 시 전체 중단

**위험도**: 낮음

**증상**
- 싸피 VM 1대에 모든 컴포넌트가 집중되어 있음
- VM 장애 시 복구까지 시연 불가

**대응**
- DB 백업 스크립트로 데이터 손실 방지
- 배포 롤백 리허설로 복구 시간 단축
- 발표 직전 상태를 별도 태그로 보존

---

## 평가 산출물 체크리스트

싸피 평가에서 가점 또는 필수로 요구될 가능성이 있는 산출물입니다.

**필수 산출물 (반드시 제출)**
- [ ] README (본 문서)
- [ ] 아키텍처 다이어그램 (최신화된 drawio)
- [ ] ERD
- [ ] API 명세 (Swagger)
- [ ] CI/CD 파이프라인 설명
- [ ] 실행 방법 및 환경 변수 가이드

**가점 산출물**
- [ ] 기술 의사결정 문서 (왜 이 스택인지)
- [ ] 모니터링 대시보드
- [ ] 테스트 코드 (커버리지 포함)
- [ ] 트러블슈팅 기록
- [ ] 발표 시연 영상

---

## 전체 개발 요약

- **총 작업량**: 153 SP / 54 티켓
- **주당 평균**: 인당 15 SP × 2명 = 30 SP
- **배포 마일스톤**: 1차(W2) → 2차(W3) → 3차(W4) → 최종(W5)
- **핵심 원칙**: 범위 고정, 배포 단계별 검증, 신규 기능 컷 우선

---

## HTTPS / Let's Encrypt 운영

`k14c201.p.ssafy.io` 의 HTTPS 는 Let's Encrypt 인증서를 `server/docker-compose.yml` 의 `certbot` 서비스(profile `certbot`)로 발급/갱신한다. nginx 는 인증서를 `:ro` 로만 마운트해서 손상 가능성을 차단한다.

### 첫 발급 (1회)

EC2 호스트(`ubuntu@k14c201.p.ssafy.io`)에서 `server/` 디렉토리 기준:

1. `.env` 에 아래 두 줄을 채운다 (`server/.env.example` 참고).
   ```env
   DOMAIN=k14c201.p.ssafy.io
   LETSENCRYPT_EMAIL=<운영자 메일>
   ```
   Jenkins credential `lostmemory-env` (Secret file) 도 동일하게 갱신한다.
2. DNS 가 EC2 를 가리키는지 확인.
   ```bash
   dig +short k14c201.p.ssafy.io @8.8.8.8   # → 54.180.247.19
   ```
3. 부트스트랩 스크립트 실행. dummy self-signed cert 로 nginx 가 먼저 정상 기동한 뒤 → staging dry-run → prod 발급 → reload 까지 자동 처리한다.
   ```bash
   chmod +x scripts/*.sh
   ./scripts/init-letsencrypt.sh
   ```
4. 검증 시퀀스(아래) 통과 확인.

### 자동 갱신 (cron, 1회 등록)

호스트 root crontab 에 wrapper 등록.
```bash
sudo crontab -e
```
```cron
17 3 * * 1 /home/ubuntu/lostmemory/server/scripts/certbot-renew.sh >> /var/log/certbot-renew.log 2>&1
```
Let's Encrypt 정책상 `renew` 는 만료 30일 이내일 때만 실제 갱신하므로 평소엔 no-op. 등록 직후 한 번 수동 실행해 동작 확인.
```bash
sudo /home/ubuntu/lostmemory/server/scripts/certbot-renew.sh
docker compose --env-file .env --profile certbot run --rm certbot renew --dry-run
```

### Jenkins 시스템 URL 갱신 (1회, 수동)

HTTPS 활성 직후 운영자가 Jenkins UI 에서 변경:

1. `https://k14c201.p.ssafy.io/jenkins/` 접속, 관리자 로그인.
2. Jenkins 관리 → System → "Jenkins URL" 을 `https://k14c201.p.ssafy.io/jenkins/` 로 변경 → 저장.
3. 다음 빌드의 메일/웹훅 링크가 https 로 생성되는지 확인.

### 검증 시퀀스

```bash
curl -I https://k14c201.p.ssafy.io/api/actuator/health     # HTTP/2 200
curl -I http://k14c201.p.ssafy.io/api/actuator/health      # 301 → https
curl -I http://k14c201.p.ssafy.io/.well-known/acme-challenge/probe   # 404 (정상)
openssl s_client -connect k14c201.p.ssafy.io:443 \
  -servername k14c201.p.ssafy.io < /dev/null 2>/dev/null \
  | openssl x509 -noout -issuer -subject -dates           # issuer = Let's Encrypt
curl -I https://k14c201.p.ssafy.io/jenkins/login           # 200 또는 403
curl -I http://k14c201.p.ssafy.io/nginx-health             # 200 (HTTP 에서도 살림)
curl -I https://k14c201.p.ssafy.io/nginx-health            # 200
```

### 롤백

발급 실패 또는 nginx 가 cert 문제로 안 뜨는 경우:

1. `server/nginx/conf.d/default.conf` 의 443 server 블록 전체를 임시로 주석 처리.
2. ```bash
   docker compose --env-file .env exec nginx nginx -t
   docker compose --env-file .env exec nginx nginx -s reload
   ```
3. 80 만 살아있는 상태에서 원인 파악(주로 ACME challenge 응답 안 됨 / DNS / UFW / AWS SG / rate limit) 후 다시 `init-letsencrypt.sh`.
4. 또는 git revert 후 `docker compose up -d nginx` 로 직전 상태 복귀.

### 주의

- prod 발급은 도메인당 주 5회 rate limit. 부트스트랩 스크립트가 staging dry-run 을 먼저 돌리는 이유.
- certbot 은 webroot 모드로 동작하므로 nginx 가 80 에 계속 떠있어야 한다 (standalone 모드로 바꾸지 말 것).
- `certbot_etc` 와 `certbot_webroot` 는 named volume 이라 호스트 경로로 직접 보지 못한다. 인증서 확인은 `docker compose --profile certbot run --rm --entrypoint sh certbot -c "ls /etc/letsencrypt/live/$DOMAIN/"` 로.

---

## 자체 Relay 운영 (UDP 7777)

게임 멀티플레이용 자체 Relay 서버 (Netty UDP 7777). `server-app:latest` 단일 이미지 안에 두 main 클래스 (`ServerApplication` + `RelayApplication`) 가 패키징되며, **Dockerfile 변경 없이 compose 의 `relay` 서비스 entrypoint 에서 PropertiesLauncher 로 다른 main 을 띄우는** 구조다. nginx 의 `stream {}` 블록이 UDP 7777 외부 진입점을 담당하고 docker network 로 relay 컨테이너에 proxy 한다.

### 정책

- Unity Relay (외부 인프라) 와 자체 Relay (백엔드 인프라) 공존 — 클라이언트가 NGO Transport 로 모드 선택. [네트워크 분리 구조](#네트워크-분리-구조) 참고.
- 같은 `server-app:latest` image 재사용 → relay 전용 빌드 불필요. `deploy.sh deploy/rollback` 가 `app + relay` 함께 force-recreate.
- nginx 외부 진입점은 UDP 7777 (TCP X). AWS SG 인바운드 UDP 7777 허용 필수.
- Relay 컨테이너는 DB / Redis 미사용 — `frontend` 네트워크만 연결. healthcheck 는 `pgrep -f RelayApplication` (UDP 라 HTTP healthcheck 불가).

### 1회 등록 절차 (운영자)

EC2 SSH 후:

1. **AWS Security Group UDP 7777 인바운드 허용** (운영자 콘솔):
   - Inbound rule: Custom UDP, Port 7777, Source `0.0.0.0/0` + `::/0`
   - 보안상 더 좁히려면 클라이언트 가능 ip 범위로 제한
2. **Jenkins credential `lostmemory-env` 갱신** (메모리 룰 — `.env` 변수 hardcode 추측 X):
   - Jenkins UI → Manage Jenkins → Credentials → `lostmemory-env` (Secret file)
   - 다운로드 → 텍스트 끝에 추가:
     ```
     JWT_SESSION_EXPIRATION=600
     RELAY_PORT=7777
     HANDSHAKE_TIMEOUT_MS=5000
     RELAY_PEER_IDLE_TIMEOUT_MS=10000
     RELAY_CLEANUP_INTERVAL_MS=2000
     CORS_ALLOWED_ORIGIN_PATTERNS=https://k14c201.p.ssafy.io
     ```
   - "Replace" 로 갱신
3. **EC2 워킹트리 sync** + `.env` 갱신:
   ```bash
   cd /home/ubuntu/lostmemory
   git fetch origin && git switch develop && git pull --ff-only origin develop

   # 호스트 .env 에도 같은 3개 키 추가 (Jenkins credential 과 일치)
   sudo vi server/.env
   ```
4. **nginx 재기동** (stream {} 블록 신설 — `nginx -s reload` 로는 stream 모듈 활성화 안 될 수 있어 force-recreate 권장):
   ```bash
   cd server
   docker compose --env-file .env exec nginx nginx -t
   docker compose --env-file .env up -d --force-recreate nginx
   ```
5. **app + relay 기동** (백엔드 코드 RelayApplication 가 develop 머지된 상태 전제):
   ```bash
   docker compose --env-file .env build app
   ./scripts/deploy.sh deploy <BUILD_NUMBER>
   # 또는 수동: docker compose --env-file .env up -d app relay
   ```

### 검증 시퀀스

```bash
# (a) PropertiesLauncher 클래스 사전 점검 (1회)
docker run --rm --entrypoint sh server-app:latest -c 'find /app -name "PropertiesLauncher*" | head -3'
# → org/springframework/boot/loader/launch/PropertiesLauncher.class 보여야 함

# (b) 컨테이너 healthy
docker compose --env-file .env ps                        # app + relay 모두 healthy
docker compose --env-file .env logs relay --tail=20      # "[Relay] UDP listener started on port 7777"

# (c) app 회귀 — 기존 HTTPS 트래픽 정상
curl -I https://k14c201.p.ssafy.io/api/actuator/health   # HTTP/2 200

# (d) UDP 외부 도달성 검증 (운영자 PC)
# macOS / Linux:
nc -u k14c201.p.ssafy.io 7777
# 임의 문자열 입력 → relay 로그에 "[Relay] Handshake failed from <ip>:..." 떠야 도달 확인
# (JSON 핸드셰이크 아니라 거절은 정상)

# Windows (PowerShell):
$udp = New-Object System.Net.Sockets.UdpClient
$bytes = [Text.Encoding]::UTF8.GetBytes("ping")
$udp.Send($bytes, $bytes.Length, "k14c201.p.ssafy.io", 7777)
# 동일하게 relay 로그에 Handshake failed 떠야 도달 확인
```

### 롤백

- relay 만 stop: `docker compose --env-file .env stop relay`
- relay + nginx stream block 함께 비활성: `docker-compose.yml` 에서 relay 서비스 + nginx ports 의 `7777:7777/udp` 주석, `nginx/nginx.conf` 의 `stream {}` 블록 주석 → `docker compose up -d --force-recreate nginx app`
- 또는 본 PR `git revert`
- AWS SG UDP 7777 차단 (Source 비움)

### 주의

- 백엔드 `RelayApplication` 클래스가 develop 에 머지되어 있어야 함 (compose 변경만으로는 불충분 — `ClassNotFoundException` 발생)
- nginx 의 `stream {}` 블록 신설 후 첫 적용은 `nginx -s reload` 가 아닌 `up -d --force-recreate nginx` 권장 (master process 재시작 필요)
- Relay 와 app 이 같은 image 공유하므로 `docker image prune -a` 같이 태그 붙은 이미지를 지우는 명령은 절대 사용 X (deploy.sh history rollback 자산 손실). `server/scripts/docker-prune.sh` 는 dangling 만 정리 — 안전.

---

## Docker 자원 정리 (cron, 1회 등록)

Jenkins 가 매 빌드마다 `docker compose build app` 으로 새 이미지를 만들고 이전 layer 가 dangling 으로 쌓여 EC2 디스크가 점진적으로 소모된다. `server/scripts/docker-prune.sh` 가 일요일 03:30 에 dangling image + 30일 이상된 build cache 만 정리한다.

### 정책

- **`docker image prune -f`** — dangling image (untagged + 컨테이너 미참조) 만 제거. 태그 붙은 `server-app:N` rollback 이력 (`deploy.sh history`) 은 그대로 보존.
- **`docker builder prune --filter "until=720h"`** — 30일 이상된 BuildKit 캐시만 제거. 30일 미만 캐시는 다음 빌드 hit 유지.
- **volume / network / running container 절대 미건드림** — `postgres_data` / `redis_data` / `certbot_etc` / `certbot_webroot` 데이터 손실 위험 차단. `docker system prune` 사용 X.
- **시간대** — 일요일 03:30. certbot-renew (월 03:17) 와 분리, 트래픽 가장 적은 시간대.

### 1회 등록 절차 (운영자)

EC2 SSH 후 develop 동기화는 [HTTPS 절차](#-자동-갱신-cron-1회-등록) 와 동일.

1. logrotate 정책 적용 (이미 등록돼 있다면 갱신 cp 만):
   ```bash
   sudo cp /home/ubuntu/lostmemory/server/etc/logrotate.d/lostmemory /etc/logrotate.d/lostmemory
   sudo logrotate -d /etc/logrotate.d/lostmemory   # docker-prune.log 정책 dry-run 검증
   ```
2. 호스트 root crontab 에 wrapper 등록:
   ```bash
   sudo crontab -e
   ```
   ```cron
   30 3 * * 0 /home/ubuntu/lostmemory/server/scripts/docker-prune.sh >> /var/log/docker-prune.log 2>&1
   ```
3. 등록 직후 한 번 수동 실행해 동작 확인:
   ```bash
   docker system df                                    # before
   sudo /home/ubuntu/lostmemory/server/scripts/docker-prune.sh
   docker system df                                    # after — Images / Build Cache 줄어듦, Volumes 동일
   docker volume ls | grep -E 'postgres_data|redis_data|certbot_'   # 볼륨 4개 모두 그대로
   tail -20 /var/log/docker-prune.log                  # 정상 종료 로그 확인
   ```
4. 다음 일요일 03:30 자동 실행 후 같은 명령으로 검증.

### 롤백

- cron 만 빼기: `sudo crontab -e` 에서 해당 줄 삭제.
- 로그/정책 함께 정리: `sudo rm /var/log/docker-prune.log` (선택), `sudo rm /etc/logrotate.d/lostmemory` 후 git revert + 재적용.

### 주의

- `docker image prune -a` 는 사용하지 않는다 (`-a` 는 태그 붙은 이미지도 제거 → rollback 이력 손실).
- volume prune 이 필요한 경우는 운영자가 SSH 직접 + 백업 후 수동 (자동화 절대 X).

---

## Jenkins 빌드 알림 (Mattermost)

Jenkins 빌드의 성공/실패 결과를 Mattermost 채널로 자동 알림한다. Jenkinsfile 의 `post.success` / `post.failure` 가 ENV_FILE(`lostmemory-env` Secret file) 의 `MATTERMOST_WEBHOOK_URL` 라인을 grep 으로 추출해 incoming webhook 으로 호출한다.

### 1회 등록 절차 (운영자)

1. Jenkins 알림 전용 Mattermost incoming webhook URL 보관 (도메인 `meeting.ssafy.com`).
2. `https://k14c201.p.ssafy.io/jenkins/` → Manage Jenkins → Credentials → System → Global → `lostmemory-env`.
3. 현재 Secret file 다운로드 → `.env` 텍스트 끝에 한 줄 추가:
   ```
   MATTERMOST_WEBHOOK_URL=<발급한 webhook URL>
   ```
4. Jenkins UI 에서 같은 credential 의 "Replace" 로 갱신된 `.env` 업로드.
5. 빌드 한 번 트리거 → 채널에 ✅ 메시지 도착 확인.

> webhook URL 은 commit / Jenkinsfile / 코드 어디에도 hardcode 하지 않는다. 운영자가 .env 에 넣을 때만 입력한다.

### 트러블슈팅

- Jenkins 콘솔에 `[notify] MATTERMOST_WEBHOOK_URL 미설정 — 알림 건너뜀` → `.env` 갱신 누락. 위 1회 등록 절차 다시 진행.
- `[notify] webhook 호출 실패 — 빌드 자체는 영향 없음` → webhook URL 의 token 만료 / 채널 삭제 / 네트워크 차단 점검. 빌드 자체는 정상 종료된다.
- `set +x` 로 webhook URL 의 콘솔 노출은 차단되지만, 로그 자체에 외부 인증을 두지 말 것 (현재 Jenkins 가 nginx + auth 뒤에 있어 OK).

### 알림 OFF / 롤백

`server/Jenkinsfile` 의 `post` 블록 안 `notifyMattermost('SUCCESS')` / `notifyMattermost('FAILURE')` 두 줄을 주석 처리하면 즉시 알림 OFF. 헬퍼 함수는 그대로 둬도 무방.

---

## 로그 운영 (Docker logging + logrotate)

### 정책
- 모든 docker 컨테이너의 stdout/stderr 로그는 `json-file` driver, **max-size 10MB × max-file 3** = 컨테이너당 최대 30MB 보관 + 회전 시 gzip 압축.
- compose 의 `logging` 섹션이 명시 적용 (postgres / redis / app / nginx / certbot / jenkins).
- compose 외부 컨테이너 (gitlab-runner 등) 는 호스트 `/etc/docker/daemon.json` 의 글로벌 default 로 같은 정책 적용.
- `/var/log/certbot-renew.log` 는 logrotate 로 주1회 회전, 8주 보관.
- `/var/log/docker-prune.log` 도 같은 정책 (주1회, 8주 보관). [Docker 자원 정리 절](#docker-자원-정리-cron-1회-등록) 참고.
- 호스트 `/var/log/*` 의 syslog / kern / journal 은 Ubuntu 기본 logrotate / journald 가 이미 처리 — 추가 작업 없음.

### 1회 등록 절차 (운영자)

EC2 SSH 후:

1. develop 동기화:
   ```bash
   cd /home/ubuntu/lostmemory
   git fetch origin && git switch develop && git pull --ff-only origin develop
   ```
2. 호스트 daemon.json 적용:
   ```bash
   sudo cp server/etc/docker/daemon.json /etc/docker/daemon.json
   sudo systemctl reload docker
   ```
3. logrotate conf 적용:
   ```bash
   sudo cp server/etc/logrotate.d/lostmemory /etc/logrotate.d/lostmemory
   sudo logrotate -d /etc/logrotate.d/lostmemory   # dry-run 검증 (실제 회전 X)
   ```
4. compose service 재생성 (logging 섹션 새로 적용):
   ```bash
   cd /home/ubuntu/lostmemory/server
   docker compose --env-file .env up -d --force-recreate postgres redis app nginx
   docker compose -f docker-compose.jenkins.yml --env-file .env up -d --force-recreate jenkins
   ```
5. gitlab-runner 는 글로벌 default 만으로 충분 — 다음 재시작 시 효과:
   ```bash
   docker restart gitlab-runner
   ```

### 검증

```bash
for c in server-app server-postgres server-nginx server-redis jenkins gitlab-runner; do
  docker inspect "$c" --format "$c: {{.HostConfig.LogConfig.Type}} {{.HostConfig.LogConfig.Config}}"
done
```
모든 행에 `max-size:10m max-file:3 compress:true` 표시되어야 통과.

회전 동작 확인 (선택):
```bash
sudo ls -la /var/lib/docker/containers/<container-id>/    # 회전된 .1, .2 또는 .gz 파일 보임
```

### 롤백

- daemon.json 원복: `sudo rm /etc/docker/daemon.json && sudo systemctl reload docker`
- compose 의 `logging` / `*default-logging` 라인 주석 처리 → `docker compose up -d --force-recreate` (또는 git revert)
- logrotate conf 원복: `sudo rm /etc/logrotate.d/lostmemory`

---

## 배포 / 롤백 운영 (deploy.sh)

`server/scripts/deploy.sh` 가 배포/롤백/상태조회의 단일 진입점이다. Jenkins 자동 배포와 운영자 SSH 수동 운영 양쪽 모두 같은 스크립트를 호출한다.

### Sub-command

| 명령 | 동작 |
|---|---|
| `./scripts/deploy.sh deploy <N>` | 빌드된 `server-app:latest` 를 `server-app:N` 으로 태깅 + `up -d` + healthy 폴링. Jenkins 가 호출하는 경로 |
| `./scripts/deploy.sh rollback <N>` | 보존된 `server-app:N` 태그를 `server-app:latest` 로 재태깅 + `up -d --force-recreate` + healthy 폴링. image 빌드 없이 ~10초 |
| `./scripts/deploy.sh status` | 현재 app 컨테이너의 image / state / health |
| `./scripts/deploy.sh history` | 보존된 `server-app:*` 태그 목록 (디스크 점유 같이) |

### 자동 배포 흐름

develop push → GitLab CI `trigger_jenkins_build` → Jenkins 빌드 #N
1. Docker Build stage: `docker compose build app` → `server-app:latest` image 생성
2. Deploy stage: `./scripts/deploy.sh deploy <N>` → 태깅 + `up -d` + healthy 폴링
3. Smoke Test stage: `./scripts/deploy.sh status` (가시성용)

### 수동 배포 / 롤백 (EC2 SSH)

```bash
ssh -i ~/.ssh/K14C201T.pem ubuntu@k14c201.p.ssafy.io
cd /home/ubuntu/lostmemory/server

./scripts/deploy.sh status                  # 현재 떠있는 image / health 확인
./scripts/deploy.sh history                 # 보존된 태그 목록 (#1, #2, ... + size)
./scripts/deploy.sh rollback 13             # 빌드 #13 으로 즉시 복귀 (~10초)
```

### 롤백 시나리오 예시

빌드 #14 가 schema-validation fail 로 startup 안 되는 상황 (이번 5월 초의 실제 케이스):

```bash
./scripts/deploy.sh status      # → server-app-1 가 Restarting 으로 보임
./scripts/deploy.sh history     # → 13, 12, 11 가 살아있는지 확인
./scripts/deploy.sh rollback 13 # → 즉시 복귀 + healthy 자동 검증
```

### 보존 태그 정책 / 디스크

- Jenkinsfile 의 `buildDiscarder(logRotator(numToKeepStr: '20'))` 는 빌드 메타데이터 20개만 유지 (image 자체와는 별개).
- docker image 자체는 `./scripts/deploy.sh history` 출력의 합계가 디스크 점유.
- 한 image 약 250MB × 20 ≈ 5GB. 현재 디스크 309GB 여유 충분.
- 무한 누적 방지하려면 별도 cron 으로 `docker image prune --filter 'until=720h' --force` 같은 정책 추가 (이번 PR 범위 밖).

### 주의

- **rollback 시 새 image build 안 함** — 호스트에 보존된 `server-app:N` 태그를 사용한다. 그 image 가 이미 prune 됐으면 rollback 불가 (`history` 로 사전 확인).
- **rollback 후 Jenkins 빌드 번호 vs 실제 떠있는 image 불일치 가능** — `./scripts/deploy.sh status` 로 항상 실제 상태 확인.
- **app service 만 영향**. nginx / postgres / redis 는 별도. schema 누락처럼 DB 차원 이슈는 rollback 만으로 해결되지 않을 수 있음.
- **healthy 폴링 timeout 90초** (30회 × 3초). 네트워크/DB 가 느려서 그 안에 healthy 못 되면 로그 100줄 출력 후 exit 1 — 빌드도 fail 처리.

---

## 문서 변경 이력

| 날짜 | 내용 | 작성자 |
|---|---|---|
| 2026-04-21 | 초안 작성 | 송주헌 |
| 2026-05-04 | INFRA-15 Let's Encrypt HTTPS 운영 절차 추가 | 송주헌 |
| 2026-05-06 | INFRA-16 Jenkins 빌드 Mattermost 알림 운영 절차 추가 | 송주헌 |
| 2026-05-06 | INFRA-17 Docker 로그 rotation + logrotate 운영 절차 추가 | 송주헌 |
| 2026-05-06 | INFRA-18 deploy.sh 배포/롤백 운영 절차 추가 | 송주헌 |

---