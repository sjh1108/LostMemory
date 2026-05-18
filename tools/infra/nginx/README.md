# Nginx 설정

## 목적

이 폴더는 AI 도구 전용 Nginx 설정을 둔다.

Nginx는 사용자의 HTTP 요청을 직접 처리하지 않고, 요청 목적에 따라 뒤쪽 서비스로 전달한다.

- AI 도구 API 요청은 AI 도구 백엔드로 전달한다.
- ComfyUI UI와 WebSocket 요청은 ComfyUI upstream으로 전달한다.
- Postgres와 S3에는 직접 접근하지 않는다.

## 파일 구조

```text
tools/infra/nginx/
  README.md
  auth/
    README.md
    .htpasswd        # local only
  nginx.conf
  templates/
    README.md
    ai-tool.conf.template
```

## `nginx.conf`

Nginx 전역 설정이다.

주요 역할은 아래와 같다.

- 로그 포맷 정의
- gzip 설정
- 요청 timeout 기본값 설정
- WebSocket용 `$connection_upgrade` map 정의
- `/etc/nginx/conf.d/*.conf` 로드

Nginx 컨테이너가 시작되면 `templates/ai-tool.conf.template`이 환경변수 치환을 거쳐 `/etc/nginx/conf.d/ai-tool.conf`로 생성된다. `nginx.conf`는 그 결과 파일을 include한다.

## `templates/ai-tool.conf.template`

도메인별 server block 초안이다.

현재 두 개의 server block을 둔다.

1. `PUBLIC_DOMAIN`
   - 루트 도메인 또는 대표 도메인
   - `/nginx-health`는 Nginx 자체 health check
   - `/api/`는 AI 도구 백엔드 upstream으로 전달
   - `/`는 아직 프론트엔드가 없으므로 placeholder 응답

2. `COMFYUI_DOMAIN`
   - ComfyUI 전용 subdomain
   - 모든 요청을 ComfyUI upstream으로 전달
   - `/ws`를 포함한 WebSocket upgrade 헤더를 전달
   - 이미지 생성 UI를 고려해 read timeout을 길게 둔다.

## 왜 subdomain 방식인가

ComfyUI는 정적 파일, REST API, WebSocket `/ws`를 함께 사용한다.

`example.com/comfy` 같은 path 방식은 정적 파일 경로와 WebSocket 경로 rewrite가 꼬일 수 있다. `comfy.example.com` 같은 subdomain 방식은 ComfyUI를 루트 경로로 그대로 전달할 수 있어 프록시 설정이 단순하다.

## upstream 의미

upstream은 Nginx가 요청을 넘겨주는 실제 뒤쪽 서버다.

예시는 아래와 같다.

```text
AI_BACKEND_UPSTREAM=http://host.docker.internal:8080
COMFYUI_UPSTREAM=http://host.docker.internal:8188
```

운영 데스크탑에서 백엔드와 ComfyUI를 직접 실행하면 위처럼 `host.docker.internal`을 쓸 수 있다.

GPU 데스크탑에서 ComfyUI를 실행하면 아래처럼 바꾼다.

```text
COMFYUI_UPSTREAM=http://192.168.100.77:8188
```

## HTTPS와 Basic Auth

`AI-205`에서 HTTPS server block과 HTTP -> HTTPS redirect를 적용한다.

- 인증서는 Let's Encrypt HTTP-01 challenge를 기준으로 발급한다.
- challenge 파일은 `/var/www/certbot/.well-known/acme-challenge/` 아래에서 제공한다.
- 인증서 파일은 `/etc/letsencrypt/live/${PUBLIC_DOMAIN}`과 `/etc/letsencrypt/live/${COMFYUI_DOMAIN}`을 참조한다.
- Basic Auth는 `AI-206`에서 계정 파일 마운트와 인증 설정을 추가한다.

Basic Auth 보호 대상은 아래와 같다.

- `COMFYUI_DOMAIN` 전체
- `PUBLIC_DOMAIN`의 `/api/`

Basic Auth 예외 대상은 아래와 같다.

- `/.well-known/acme-challenge/`
- `/nginx-health`

실제 계정 파일은 `tools/infra/nginx/auth/.htpasswd`에 만들고 Git에는 올리지 않는다. 생성 절차는 `auth/README.md`에서 본다.
