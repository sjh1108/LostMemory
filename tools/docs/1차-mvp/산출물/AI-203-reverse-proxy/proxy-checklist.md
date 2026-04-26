# AI-203 프록시 체크리스트

## 설정 파일

- [x] `tools/infra/docker-compose.yml` 초안 작성
- [x] `tools/infra/.env.example` 작성
- [x] `tools/infra/nginx/nginx.conf` 작성
- [x] `tools/infra/nginx/templates/ai-tool.conf.template` 작성
- [x] `tools/infra/postgres` 폴더 기준 작성

## 공개 경로

- [x] ComfyUI 공개 경로는 subdomain 방식으로 결정
- [x] 실제 도메인 이름은 구매 후 결정
- [x] `PUBLIC_DOMAIN`, `COMFYUI_DOMAIN` 환경변수로 분리

## upstream

- [x] AI 백엔드 upstream을 `AI_BACKEND_UPSTREAM`으로 분리
- [x] ComfyUI upstream을 `COMFYUI_UPSTREAM`으로 분리
- [x] EC2 공개 진입 서버 기준 예시 작성
- [x] 운영 데스크탑 ComfyUI worker 기준 예시 작성
- [x] GPU 데스크탑 ComfyUI 대체 기준 예시 작성

## WebSocket

- [x] `nginx.conf`에 `$connection_upgrade` map 추가
- [x] AI 백엔드 proxy에 Upgrade/Connection 헤더 추가
- [x] ComfyUI proxy에 Upgrade/Connection 헤더 추가
- [x] ComfyUI proxy buffering 비활성화
- [x] ComfyUI read timeout을 길게 설정

## Postgres

- [x] SQLite 대신 Postgres 기준으로 결정
- [x] Postgres compose service 초안 작성
- [x] Postgres를 private network에만 배치
- [ ] 실제 AI 도구 스키마 작성

## 아직 하지 않은 일

- [ ] 실제 도메인 구매
- [ ] DNS A record 또는 CNAME 설정
- [ ] EC2 instance type 선택
- [ ] 운영/GPU 데스크탑 네트워크 연결 방식 선택
- [ ] 운영/GPU 데스크탑 공인 접근 또는 tunnel 확인
- [ ] HTTPS 인증서 발급
- [ ] Basic Auth 적용
- [ ] 실제 AI 백엔드 health endpoint 연결
- [ ] 실제 ComfyUI HTTP 연결 확인
- [ ] 실제 ComfyUI WebSocket 연결 확인

## AI-203-07 smoke test 절차

`tools/infra`에서 실행한다.

```powershell
Copy-Item .env.example .env
docker compose config
docker compose up -d nginx
docker compose exec nginx nginx -t
Invoke-WebRequest -UseBasicParsing http://localhost/nginx-health
Invoke-WebRequest -UseBasicParsing -Headers @{Host='example.com'} http://localhost/nginx-health
Invoke-WebRequest -UseBasicParsing -Headers @{Host='comfy.example.com'} http://localhost/nginx-health
```

예상 결과:

- `docker compose config`가 compose 문법 오류 없이 종료된다.
- `nginx` 컨테이너가 기동된다.
- `nginx -t`가 `syntax is ok`, `test is successful`을 출력한다.
- `http://localhost/nginx-health`가 `ok`를 반환한다.
- Host 헤더를 `example.com`, `comfy.example.com`으로 준 요청도 `ok`를 반환한다.

주의:

- `AI_BACKEND_UPSTREAM`과 `COMFYUI_UPSTREAM` 대상이 꺼져 있어도 Nginx 자체는 떠야 한다.
- upstream 연결 성공 여부는 `AI-204`에서 확인한다.
- Docker에 `nginx:1.27-alpine` 이미지가 없으면 최초 실행 시 image pull이 필요하다.
- `open //./pipe/dockerDesktopLinuxEngine: The system cannot find the file specified`가 나오면 Docker Desktop 또는 Docker Engine이 꺼진 상태다. Docker Desktop을 먼저 실행한 뒤 재시도한다.
- `http://localhost/nginx-health`가 404를 반환하는 경우는 공식 nginx 이미지의 기본 `default.conf`가 `server_name localhost` 요청을 잡는 상황일 수 있다. 이 경우 컨테이너를 재생성해 compose의 기본 파일 제거 command가 반영됐는지 확인한다.
- `/etc/nginx/conf.d`가 비어 있으면 template 생성이 실행되지 않은 것이다. compose command는 `envsubst`로 `ai-tool.conf.template`을 `ai-tool.conf`로 만든 뒤 Nginx를 실행해야 한다.
