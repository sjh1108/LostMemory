# Jenkins 빌드/배포 파이프라인 설정 가이드

## 개요
Spring Boot 앱을 Docker 이미지로 빌드하고 EC2 에 `docker compose` 로 배포하는 Jenkins 파이프라인입니다.
수동 트리거(Build with Parameters) 기반으로 작동하며, Jenkinsfile 은 `server/Jenkinsfile` 에 위치합니다.

## 전제 조건

- Jenkins 가 SSAFY EC2 (`k14c201.p.ssafy.io`) 에 컨테이너로 실행 중이어야 함 (별도 티켓 `S14P31C201-110 Jenkins 컨테이너 구성` 에서 구축)
- Jenkins 컨테이너에 `/var/run/docker.sock` 이 마운트되어 있어야 함 (DooD — Docker outside of Docker)
- Jenkins 가 이 GitLab 레포를 읽을 수 있어야 함

## 최초 셋업 (관리자 1회만 수행)

### 1. 필요한 플러그인 설치
Manage Jenkins → Plugins → Available 에서 다음 설치 후 Jenkins 재시작:
- **Git** (대부분 기본 포함)
- **Pipeline** (대부분 기본 포함)
- **Pipeline: Stage View** (각 stage 시각화)
- **Credentials Binding** (Secret file/text 주입)
- **JUnit** (테스트 리포트)

### 2. Credential 등록 — 운영용 `.env` 파일

Manage Jenkins → Credentials → System → Global credentials → Add credentials

| 항목 | 값 |
| --- | --- |
| Kind | **Secret file** |
| Scope | Global |
| File | 운영용 `.env` 파일 업로드 (팀 내 공유된 것, `POSTGRES_PASSWORD` / `REDIS_PASSWORD` / `JWT_SECRET` 등 포함) |
| ID | `lostmemory-env` (Jenkinsfile 에서 이 ID 로 참조) |
| Description | Lost Memory prod .env bundle |

**주의**: `.env` 파일은 절대 레포에 커밋하지 말 것. Jenkins Credentials 에만 보관.

### 3. Pipeline Job 생성

Jenkins Dashboard → New Item
- Item name: `lostmemory-server-deploy`
- Type: **Pipeline**
- OK

Job 설정:
- **General → This project is parameterized**: Jenkinsfile 의 parameters 블록이 자동 인식됨 (수동 설정 불필요)
- **Pipeline → Definition**: **Pipeline script from SCM**
- **SCM**: Git
- **Repository URL**: `https://lab.ssafy.com/s14-final/S14P31C201.git`
- **Credentials**: GitLab 접근용 credential (Username with password 또는 SSH key)
- **Branch Specifier**: `*/develop` (develop 만 배포 대상)
- **Script Path**: `server/Jenkinsfile`
- Save

## 사용법

### 배포 실행
1. Jenkins → `lostmemory-server-deploy` → **Build with Parameters** 클릭
2. 파라미터 선택:
   - `SKIP_TESTS` — 기본 true (Gradle test 단계 스킵). 테스트까지 돌리고 싶으면 해제
3. **Build** 클릭 → Pipeline 실행

### Stage 구성
| Stage | 역할 |
| --- | --- |
| Checkout | 레포 체크아웃 (develop 브랜치) |
| Build & Test | (SKIP_TESTS=false 일 때만) `./gradlew clean test` + JUnit 리포트 수집 |
| Docker Build | `docker compose build app` — 이미지 태깅 `server-app:${BUILD_NUMBER}` 병행 |
| Deploy | `docker compose up -d app` — 기존 컨테이너 교체 |
| Smoke Test | `docker compose ps` 가 `(healthy)` 될 때까지 최대 90초 폴링 |

### 실패 시 대응
- `post { failure {} }` 에서 `docker compose logs --tail=200 app` 자동 출력
- 로그에서 원인 확인 후:
  - 코드 수정 → 재배포
  - 혹은 이전 이미지로 수동 롤백: `docker tag server-app:<이전_BUILD_NUMBER> server-app:latest && docker compose up -d app`

## 트러블슈팅

- **`docker: permission denied`** — Jenkins 컨테이너 유저가 host docker group gid 로 실행되어야 함. Ticket S14P31C201-110 의 `docker-compose.jenkins.yml` 참고.
- **`credentials('lostmemory-env') not found`** — Credential ID 가 정확히 `lostmemory-env` 인지 재확인 (오타/대소문자).
- **Smoke Test timeout** — 앱이 healthy 안 되는 상황. 로그에서 DB 연결 / 환경변수 누락 여부 확인. `.env` 에 `POSTGRES_HOST=postgres`, `REDIS_HOST=redis` 로 컨테이너 네트워크 호스트명 쓰고 있는지 확인 (로컬 IDE 실행용 `.env.dev` 와 혼동 주의).
- **Jenkins 에서 `docker compose` 명령 없음** — 구버전 `docker-compose` (v1) 가 아니라 Docker Compose v2 (`docker compose` 띄어쓰기) 가 필요. EC2 의 Docker 29.4.1 + Compose v2 사용 전제.

## 향후 확장 포인트
- `DEPLOY_PROFILE` 파라미터 추가해 dev/prod 분리 배포
- 실패 시 자동 롤백 (이전 `:latest` 태그 보존 + 실패 시 재적용)
- GitLab MR 머지 완료 후 자동 트리거 (webhook → Jenkins Generic Webhook Trigger)
- Docker Registry push (이미지 보관/버전 관리) — 현재는 EC2 로컬 빌드만
