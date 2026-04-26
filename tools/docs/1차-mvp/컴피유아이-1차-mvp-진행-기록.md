# 컴피유아이 1차 MVP 진행 기록

## 문서 목적

이 문서는 `컴피유아이-1차-mvp-통합-작업표.csv`의 주요 작업코드별 진행 결과를 기록한다.

CSV는 완료 여부를 빠르게 보기 위한 표이고, 이 문서는 왜 완료로 판단했는지와 산출물이 어디에 있는지를 남기는 기록이다.

## 기록 기준

- 완료여부 `Y`: 실행 또는 산출물 확인까지 끝난 작업
- 상태 `완료`: 다음 작업이 이 결과를 선행 조건으로 사용할 수 있는 상태
- 완료 근거: 접속 URL, 실행 명령, workflow JSON, output 이미지처럼 재확인 가능한 자료를 우선 기록

## AI-201. GPU 데스크탑 ComfyUI host, port, output 경로 고정

### 작업 범위

- GPU 데스크탑 내부 IP 또는 고정 호스트명 확인
- ComfyUI listen 주소를 내부망 접근 가능 상태로 조정
- `output`, `models`, `loras` 경로와 운영 위치 확인
- 실행 명령과 재기동 스크립트 표준화

### 결정 내용

- GPU 데스크탑 내부 IP: `192.168.100.77`
- ComfyUI 포트: `8188`
- GPU PC 로컬 접속 주소: `http://127.0.0.1:8188`
- 팀원 내부망 접속 주소: `http://192.168.100.77:8188`
- 실행 스크립트: `tools/ComfyUI/run/start_comfyui.cmd`
- 실행 기준: `main.py --listen 0.0.0.0 --port 8188`

### 완료 근거

- GPU PC에서 `http://127.0.0.1:8188` 접속 확인
- GPU PC에서 `http://192.168.100.77:8188` 접속 확인
- 팀원 PC에서 `http://192.168.100.77:8188` 접속 확인
- `tools/ComfyUI/run/start_comfyui_readme.md`에 실행 기준 정리
- `tools/ai_server/.env.example`에 `COMFYUI_BASE_URL` 기준 추가

### 상태

- 완료여부: `Y`
- 상태: `완료`
- 후속 작업: `AI-202`, `AI-301`, `AI-507`

## AI-202. 기준 workflow 1종과 기준 프롬프트 확정

### 작업 범위

- 1차 MVP 기준 workflow 1종 선정
- 기준 프롬프트, negative prompt, seed, steps 등 테스트 값을 정리
- 기준 workflow로 output 이미지 생성

### 결정 내용

- 기준 모델: `Z-Image-Turbo`
- 기준 용도: 1차 MVP의 text-to-image 생성, API 샘플 확보, 저장/조회 검증
- 장기 방향: 도트풍은 공개 pixel-art LoRA와 이후 프로젝트 전용 LoRA 학습으로 강화
- 현재 단계: 직접 학습 모델이 아니라 Z-Image-Turbo + pixel-art LoRA 적용 가능성 확인

### 모델 구성

- diffusion model: `z_image_turbo_bf16`
- text encoder: `qwen_3_4b`
- CLIP loader type: `lumina2`
- VAE: `ae.safetensors`
- LoRA: `pixel_art_style_z_image_turbo`

### 기준 프롬프트

Positive prompt:

```text
Pixel art style. a small fantasy game character, full body, front view, yellow spiky hair, pale white skin, green tunic, red scarf, wooden shield, iron sword, brown boots, clean black outline, limited color palette, simple shading, readable silhouette, game sprite, centered, plain white background
```

Negative prompt:

```text
blurry, smooth shading, realistic, 3d render, painterly, noisy background, detailed background, text, watermark, deformed, extra limbs, bad hands, cropped, side view, back view
```

### 산출물

Workflow JSON:

- `산출물/AI-202-Z-Image-Turbo/workflows/Z-Image-turbo-test1.json`
- `산출물/AI-202-Z-Image-Turbo/workflows/Z-Image-turbo-test2-prompt-change.json`
- `산출물/AI-202-Z-Image-Turbo/workflows/Z-Image-turbo-test3-ksampler-change.json`

Output 이미지:

- `산출물/AI-202-Z-Image-Turbo/outputs/AI202_ZImageTurbo_PixelArt_00001.png`
- `산출물/AI-202-Z-Image-Turbo/outputs/AI202_ZImageTurbo_PixelArt_00002.png`
- `산출물/AI-202-Z-Image-Turbo/outputs/AI202_ZImageTurbo_PixelArt_00003.png`

### 완료 근거

- Z-Image-Turbo workflow에서 이미지 생성 성공
- pixel-art LoRA를 붙인 상태에서 output 이미지 3개 생성
- 프롬프트 구체화 후 캐릭터 속성 반영 방향 확인
- 생성 output은 `tools/ComfyUI/output`이 gitignore 대상이므로, MR 검토용 산출물은 `tools/docs/1차-mvp/산출물/AI-202-Z-Image-Turbo/` 아래로 복사해 보존

### 상태

- 완료여부: `Y`
- 상태: `완료`
- 후속 작업: `AI-301`, `AI-302`, `AI-303`

## AI-203. Reverse Proxy 기본 경로 설계

### 작업 범위

- 구매 예정 도메인 기준 공개 경로 전략 선택
- 운영 데스크탑, GPU 데스크탑, S3, Postgres 역할 확정
- `tools` 하위 AI 도구 전용 Nginx 설정 초안 작성
- ComfyUI WebSocket 프록시 기준 포함
- `tools/infra` 기준 Docker Compose와 디렉터리 구조 작성
- Nginx 단독 smoke test 절차 문서화

### 결정 내용

- 기존 프로젝트 루트의 `server/`, `client/`는 이번 작업에서 수정하지 않는다.
- AI 도구 인프라는 `tools/infra`에 새로 둔다.
- 도메인은 직접 구매할 예정이며, 최종 도메인 이름은 추후 확정한다.
- ComfyUI 공개 경로는 `comfy.<구매한-도메인>` 형태의 subdomain 방식으로 한다.
- 작은 EC2를 도메인이 가리키는 공개 진입 서버로 두는 방향을 우선한다.
- EC2는 Nginx, AI 도구 백엔드, Postgres, S3 연동, 결과 조회를 맡는다.
- 운영 데스크탑은 ComfyUI 이미지 생성 worker 후보로 둔다.
- 운영 데스크탑에서 이미지 생성이 어렵거나 학습이 필요하면 GPU 데스크탑을 ComfyUI 추론 또는 학습용으로 쓴다.
- EC2와 운영/GPU 데스크탑이 다른 네트워크에 있어도 가능하지만, 포트포워딩, VPN/mesh network, SSH reverse tunnel 같은 연결 통로가 필요하다.
- DB는 SQLite가 아니라 Postgres로 고정한다.
- S3는 결과 이미지와 workflow snapshot JSON 저장 후보로 둔다.

### 산출물

인프라 초안:

- `tools/infra/docker-compose.yml`
- `tools/infra/.env.example`
- `tools/infra/nginx/nginx.conf`
- `tools/infra/nginx/templates/ai-tool.conf.template`
- `tools/infra/postgres/`

결정 문서:

- `산출물/AI-203-reverse-proxy/proxy-path-decision.md`
- `산출물/AI-203-reverse-proxy/deployment-role-memo.md`
- `산출물/AI-203-reverse-proxy/proxy-checklist.md`

### 상태

- 완료여부: `Y`
- 상태: `완료`
- 후속 작업: `AI-203-07`, `AI-204`, `AI-205`, `AI-206`

### AI-203-07 smoke test 결과

- Docker Desktop 실행 후 `docker compose up -d nginx` 성공
- `nginx:1.27-alpine` image pull 성공
- `infra-nginx-1` container 생성 성공
- `docker compose exec nginx nginx -t` 성공
- `Host: example.com`, `Host: comfy.example.com` 기준 `/nginx-health` 응답 200 확인
- `localhost` Host 요청은 공식 nginx 이미지의 기본 `default.conf`가 잡아 404가 발생했으므로, compose에서 시작 시 기본 `default.conf`를 제거하도록 보강
- 공식 entrypoint template 자동 생성은 `command: sh -c ...`일 때 실행되지 않아 `/etc/nginx/conf.d`가 비는 문제가 있었고, compose command에서 `envsubst`로 `ai-tool.conf`를 직접 생성하도록 보강
- 최종 재검증에서 `/etc/nginx/conf.d/ai-tool.conf`만 남는 것을 확인
- `localhost`, `Host: example.com`, `Host: comfy.example.com` 기준 `/nginx-health` 응답 200 확인

## 현재 주의사항

- `tools/ComfyUI/output`은 런타임 산출물이라 Git 추적 대상이 아니다.
- MR에 남길 기준 이미지는 `tools/docs/1차-mvp/산출물` 아래에 별도로 복사해야 한다.
- 이번 AI-202는 “생성 가능 여부와 기준 workflow 확정”까지이며, 프로젝트 전용 LoRA 학습은 2차 이후 별도 작업으로 분리한다.
- AI 도구 인프라는 `tools/infra` 기준으로 새로 관리하며, 기존 루트 `server/` 설정과 섞지 않는다.
- AI 도구 백엔드 실제 구현 위치는 `tools/ai_server`를 기준으로 유지한다.

## AI-205. 도메인 및 HTTPS 적용

### 작업 범위

- AI 도구용 대표 도메인과 ComfyUI subdomain 기준 정리
- 80, 443 포트 외부 접근 체크리스트 작성
- Let's Encrypt HTTP-01 challenge용 Nginx webroot 설정
- HTTPS 443 server block과 HTTP -> HTTPS redirect 초안 작성
- certbot service, 인증서 volume, 갱신 검증 절차 작성

### 결정 내용

- ComfyUI 공개 경로는 기존 AI-203 결정대로 `comfy.<구매한-도메인>` subdomain 방식을 유지한다.
- 인증서는 Let's Encrypt HTTP-01 challenge와 certbot webroot 방식으로 발급한다.
- Nginx는 `/.well-known/acme-challenge/`와 `/nginx-health`를 제외한 HTTP 요청을 HTTPS로 리다이렉트한다.
- Basic Auth는 AI-206에서 별도로 적용한다.

### 산출물

- `tools/infra/docker-compose.yml`
- `tools/infra/.env.example`
- `tools/infra/nginx/templates/ai-tool.conf.template`
- `산출물/AI-205-domain-https/README.md`
- `산출물/AI-205-domain-https/dns-and-port-checklist.md`
- `산출물/AI-205-domain-https/letsencrypt-nginx-runbook.md`
- `산출물/AI-205-domain-https/renewal-check-result.md`
- `산출물/AI-205-domain-https/mr-description.md`

### 현재 판정

2026-04-27 기준 구매 도메인이 아직 없으므로 실제 DNS A 레코드 생성, Let's Encrypt 인증서 발급, `certbot renew --dry-run` 검증은 수행하지 않는다.

이번 작업에서는 AI-205의 설정과 운영 절차를 준비한 상태로 둔다. AI-204에서 내부망/로컬 프록시 기준 ComfyUI UI, WebSocket, 생성 smoke test가 통과했으므로, 도메인 구매 전에는 이 상태를 AI-206 Basic Auth 작업의 선행 조건으로 사용한다.

### 상태

- 완료여부: `N`
- 상태: `설정/절차 준비 완료, 실제 도메인 검증 대기`
- 후속 작업: `AI-206`, 도메인 구매 후 `AI-205-01`~`AI-205-05` 실검증

## AI-206. 도메인 앞단 Basic Auth 적용

### 작업 범위

- Basic Auth 보호 대상과 예외 경로 확정
- Nginx Basic Auth 설정 반영
- `.htpasswd` 파일 마운트와 Git 제외 규칙 반영
- 팀 공유용 계정 생성, 전달, 교체 기준 문서화

### 결정 내용

- `COMFYUI_DOMAIN`은 전체 경로를 Basic Auth로 보호한다.
- `PUBLIC_DOMAIN`은 현재 백엔드 진입점인 `/api/`를 Basic Auth로 보호한다.
- `/.well-known/acme-challenge/`는 Let's Encrypt HTTP-01 검증을 위해 인증 예외로 둔다.
- `/nginx-health`는 운영 health check를 위해 인증 예외로 둔다.
- 실제 계정 파일은 `tools/infra/nginx/auth/.htpasswd`에 만들고 Git에는 올리지 않는다.

### 산출물

- `tools/infra/docker-compose.yml`
- `tools/infra/.env.example`
- `tools/infra/README.md`
- `tools/infra/nginx/README.md`
- `tools/infra/nginx/auth/README.md`
- `tools/infra/nginx/templates/ai-tool.conf.template`
- `산출물/AI-206-basic-auth/README.md`
- `산출물/AI-206-basic-auth/mr-description.md`

### 현재 판정

2026-04-27 기준 구매 도메인과 실제 인증서가 아직 없으므로 브라우저에서 실제 Basic Auth 팝업, 401 응답, 인증 성공 후 ComfyUI UI/API 접근은 검증하지 않는다.

이번 작업에서는 AI-206의 설정과 운영 절차를 준비한 상태로 둔다. 실제 도메인과 인증서가 준비되면 `.htpasswd`를 생성하고 Nginx를 재시작한 뒤 실검증한다.

### 상태

- 완료여부: `N`
- 상태: `설정/절차 준비 완료, 실제 도메인 검증 대기`
- 후속 작업: 도메인/인증서 준비 후 `AI-206-01`, `AI-206-03` 실검증

## AI-401. AI 도구 백엔드 프로젝트 부트스트랩

### 작업 범위

- AI 도구 백엔드 실제 구현 위치 확정
- `GET /health`와 공통 응답 포맷 추가
- 패키지 구조와 기본 설정 파일 뼈대 생성
- `build.gradle` 의존성과 기본 패키지 정리
- Dockerfile과 jar 빌드 경로 정리

### 결정 내용

- 실제 구현 위치는 루트 `server/`가 아니라 `tools/ai_server`로 고정한다.
- 기존 게임 서버와 AI 도구 백엔드는 코드와 배포 단위를 분리한다.
- Spring Boot base package는 `com.lostmemory.aiserver`를 사용한다.
- 외부 진입 경로는 프록시 기준을 맞추기 위해 `/api` context path를 유지한다.
- health check endpoint는 `GET /api/health`로 두고, 공통 응답 포맷은 `ApiResponse` record로 통일한다.
- OpenAPI 확인용 `swagger-ui`와 actuator health endpoint를 같이 연다.
- JPA/Postgres 의존성은 먼저 넣되, 실제 DB 연결 전까지는 datasource/JPA auto-configuration을 제외해 부트스트랩 단계에서도 서버가 뜨게 한다.

### 산출물

- `tools/ai_server/README.md`
- `tools/ai_server/.env.example`
- `tools/ai_server/build.gradle`
- `tools/ai_server/settings.gradle`
- `tools/ai_server/Dockerfile`
- `tools/ai_server/gradlew`
- `tools/ai_server/gradlew.bat`
- `tools/ai_server/gradle/wrapper/`
- `tools/ai_server/src/main/java/com/lostmemory/aiserver/`
- `tools/ai_server/src/main/resources/application.yml`
- `tools/ai_server/src/main/resources/application-dev.yml`
- `tools/ai_server/src/main/resources/application-prod.yml`
- `tools/ai_server/src/test/java/com/lostmemory/aiserver/`

### 완료 근거

- `tools/ai_server` 아래에 독립 Gradle/Spring Boot 프로젝트 생성
- `GET /api/health`용 controller, service, `ApiResponse` 공통 응답 추가
- `config`, `health`, `repository`, `common` 패키지 뼈대 생성
- ComfyUI, Postgres, S3 환경변수 skeleton을 `application.yml`과 `.env.example`에 반영
- `ai-server.jar` 고정 파일명과 Dockerfile build path를 정리
- `tools/ai_server`에서 `GRADLE_USER_HOME=.gradle-home ./gradlew test bootJar` 검증 성공
- `tools/ai_server/build/libs/ai-server.jar` 생성 확인

### 상태

- 완료여부: `Y`
- 상태: `완료`
- 후속 작업: `AI-402`, `AI-403`, `AI-501`
