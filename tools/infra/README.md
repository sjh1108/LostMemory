# AI 도구 인프라

## 목적

이 폴더는 `tools` 하위 AI 도구 전용 배포 초안을 둔다.

기존 프로젝트 루트의 `server/` 인프라와 분리해서 관리한다. `server/`는 기존 게임 서버와 공용 배포 설정에 속하고, 이 폴더는 ComfyUI 기반 이미지 생성 도구를 별도 서비스로 운영하기 위한 기준이다.

## 현재 범위

`AI-206` 기준으로 이 폴더에서 다루는 범위는 Reverse Proxy에 도메인, HTTPS, Basic Auth를 붙이는 운영 초안이다.

- 구매 예정 도메인 기준 EC2 Nginx 진입점
- AI 도구 백엔드 upstream 초안
- 운영/GPU 데스크탑 ComfyUI upstream 초안
- Postgres 컨테이너 초안
- Let's Encrypt HTTP-01 challenge용 webroot
- HTTPS 443 server block과 HTTP -> HTTPS redirect
- Basic Auth용 `htpasswd` 파일 마운트와 보호 경로 설정

아직 이 폴더에서 하지 않는 일은 아래와 같다.

- 실제 도메인 구매
- DNS 레코드 생성
- 실제 Basic Auth 계정 파일 커밋
- 실제 AI 도구 백엔드의 배포 연동
- ComfyUI Docker 이미지화
- EC2와 운영/GPU 데스크탑 사이 네트워크 개통 검증

## 폴더 구조

```text
tools/infra/
  README.md
  docker-compose.yml
  docker-compose.override.yml
  .env.example
  nginx/
    README.md
    auth/
      README.md
    nginx.conf
    templates/
      README.md
      ai-tool.conf.template
  postgres/
    README.md
    init/
      README.md
```

## 역할

### `docker-compose.yml`

EC2 같은 공개 진입 서버에서 띄울 최소 인프라 초안이다.

현재 포함 서비스는 아래 세 개다.

- `nginx`: 외부 HTTP 요청을 받아 백엔드와 ComfyUI로 전달한다.
- `certbot`: Let's Encrypt 인증서 발급과 갱신을 수행한다. 기본 실행 대상에서는 제외하고 `certbot` profile로 필요할 때만 실행한다.
- `postgres`: 생성 이력, output 메타데이터, workflow snapshot 메타데이터를 저장한다.

`nginx` service는 시작 시 `envsubst`로 template을 `/etc/nginx/conf.d/ai-tool.conf`로 생성하고, 공식 이미지에 기본 포함된 `/etc/nginx/conf.d/default.conf`를 제거한다. 이 기본 파일이 남아 있으면 `server_name localhost`가 local smoke test 요청을 먼저 받아 `/nginx-health`가 404로 보일 수 있기 때문이다.

AI 도구 백엔드 초안은 `tools/ai_server`에 생성했다. 아직 compose service로 붙이지는 않았으므로, 다음 단계에서는 `ai-backend` service를 추가하거나 운영 데스크탑 host에서 실행한 백엔드로 `AI_BACKEND_UPSTREAM`을 연결한다.

ComfyUI도 compose service로 넣지 않았다. 모델 파일, GPU 드라이버, Python 가상환경 기준이 커서 `AI-203` 범위에서는 운영 데스크탑 또는 GPU 데스크탑에서 실행되는 외부 upstream으로 둔다.

### `.env.example`

배포자가 실제 `.env`를 만들 때 참고할 예시 파일이다.

도메인 이름은 아직 확정 전이므로 `example.com` 계열 placeholder를 사용한다. 실제 도메인을 구매한 뒤 아래 값을 바꾼다.

- `PUBLIC_DOMAIN`
- `COMFYUI_DOMAIN`
- `LETSENCRYPT_EMAIL`
- `BASIC_AUTH_REALM`
- `AI_BACKEND_UPSTREAM`
- `COMFYUI_UPSTREAM`
- `POSTGRES_*`
- `POSTGRES_DEV_PORT`

실제 `.env` 파일에는 비밀번호가 들어가므로 Git에 올리지 않는다.

Basic Auth 계정 파일은 `tools/infra/nginx/auth/.htpasswd`에 별도로 만든다. 이 파일도 비밀번호 해시가 들어가므로 Git에 올리지 않는다.

### `docker-compose.override.yml`

개발용 override 파일이다.

기본 compose는 Postgres를 외부에 노출하지 않지만, VSCode DB client에서 확인하기 쉽도록 개발 환경에서는 아래 포트만 연다.

```yaml
services:
  postgres:
    ports:
      - "${POSTGRES_DEV_PORT:-55432}:5432"
```

기본값을 `55432`로 둔 이유는 게임 서버 쪽 Postgres와 `5432` 충돌 가능성을 줄이기 위해서다.

VSCode 기준 연결 정보는 아래와 같다.

- Host: `localhost`
- Port: `55432`
- Database: `ai_tool`
- Username: `ai_tool`
- Password: `tools/infra/.env`의 `POSTGRES_PASSWORD`

### `nginx/`

Nginx 전역 설정과 site template을 둔다.

공식 `nginx:alpine` 이미지는 `/etc/nginx/templates/*.template` 파일을 컨테이너 시작 시 환경변수로 치환해서 `/etc/nginx/conf.d/*.conf`로 만든다. 그래서 도메인 이름을 나중에 바꿔도 `tools/infra/.env`만 수정하면 된다.

### `postgres/`

Postgres 데이터베이스 초기화 파일을 둘 자리다.

현재는 초기 SQL을 넣지 않는다. 실제 스키마는 AI 도구 백엔드 작업에서 확정한 뒤 `postgres/init/` 아래에 추가한다.

## EC2와 운영/GPU 데스크탑 선택 기준

현재 우선안은 작은 EC2를 공개 진입 서버로 두고, 운영 데스크탑 또는 GPU 데스크탑을 ComfyUI 실행 worker로 두는 구조다.

- 도메인은 EC2의 public IP 또는 Elastic IP를 가리킨다.
- EC2는 Nginx, AI 도구 백엔드, Postgres를 실행한다.
- S3는 EC2 또는 백엔드가 직접 사용하는 AWS 저장소다.
- ComfyUI는 운영 데스크탑 또는 GPU 데스크탑에서 실행한다.
- 운영/GPU 데스크탑이 꺼져 있으면 결과 조회는 가능해도 새 이미지 생성은 실패한다.

서로 다른 네트워크에 있어도 구조 자체는 가능하다. 단, EC2에서 운영/GPU 데스크탑의 ComfyUI 주소로 접속할 수 있는 통로가 필요하다.

접속 통로 후보는 아래와 같다.

1. 운영/GPU 데스크탑에 공인 IP와 포트포워딩을 설정한다.
2. VPN 또는 mesh network를 사용해 EC2와 데스크탑을 같은 사설망처럼 묶는다.
3. SSH reverse tunnel 같은 outbound tunnel을 사용해 데스크탑이 EC2로 먼저 연결한다.

집 인터넷이 CGNAT이거나 포트포워딩이 어렵다면 2번 또는 3번 방식이 더 현실적이다.

## 205 이후 후속 작업

- 도메인 구매 후 `AI-205`: DNS, 인증서 발급, 자동 갱신 실검증
- `AI-206`: 실제 도메인 기준 Basic Auth 브라우저 검증
- `AI-401` 이후: AI 도구 백엔드 구현
- `AI-501` 이후: Postgres 최소 스키마와 init SQL 정리
