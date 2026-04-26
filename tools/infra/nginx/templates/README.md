# Nginx template

## 목적

이 폴더는 Docker 공식 `nginx` 이미지가 시작 시 환경변수로 치환할 template 파일을 둔다.

`tools/infra/docker-compose.yml`은 이 폴더를 컨테이너의 `/etc/nginx/templates`로 마운트한다. 컨테이너 시작 시 `envsubst`로 아래 파일을 직접 생성한다.

```text
/etc/nginx/templates/ai-tool.conf.template
  -> /etc/nginx/conf.d/ai-tool.conf
```

## 왜 template을 쓰는가

도메인을 아직 구매하지 않았기 때문에 Nginx 설정 파일에 최종 도메인을 하드코딩하지 않는다.

`.env`에서 아래 값을 바꾸면 같은 template으로 다른 배포 환경을 만들 수 있다.

- `PUBLIC_DOMAIN`
- `COMFYUI_DOMAIN`
- `AI_BACKEND_UPSTREAM`
- `COMFYUI_UPSTREAM`

## template 변수

### `PUBLIC_DOMAIN`

AI 도구 대표 도메인이다.

예시:

```text
lostmemory-tools.com
ai.lostmemory-tools.com
```

### `COMFYUI_DOMAIN`

ComfyUI 원본 UI를 노출할 subdomain이다.

예시:

```text
comfy.lostmemory-tools.com
```

### `AI_BACKEND_UPSTREAM`

AI 도구 백엔드가 실제로 실행되는 주소다.

로컬 smoke test에서 백엔드를 host OS에서 직접 실행하면:

```text
http://host.docker.internal:8080
```

EC2에서 백엔드를 compose service로 넣으면:

```text
http://ai-backend:8080
```

### `COMFYUI_UPSTREAM`

ComfyUI가 실제로 실행되는 주소다. EC2에서 접근 가능한 주소여야 한다.

로컬 smoke test에서 같은 PC host에 ComfyUI를 실행하면:

```text
http://host.docker.internal:8188
```

GPU 데스크탑이 EC2와 같은 사설망 또는 VPN에 있으면:

```text
http://192.168.100.77:8188
```

운영/GPU 데스크탑이 집이나 다른 네트워크에 있으면 EC2에서 바로 접근할 수 없을 수 있다. 이 경우 공인 IP와 포트포워딩, VPN/mesh network, SSH reverse tunnel 중 하나를 먼저 준비해야 한다.

## 주의

이 template은 HTTP 80 기준 초안이다.

HTTPS 인증서 경로와 443 server block은 `AI-205`에서 추가한다. Basic Auth 설정은 `AI-206`에서 추가한다.

로컬 smoke test에서는 아직 실제 도메인이 없으므로 `localhost`로 접근하게 된다. 이때도 health check가 동작하도록 `PUBLIC_DOMAIN` server block을 `default_server`로 둔다.

공식 `nginx` 이미지에는 기본 `/etc/nginx/conf.d/default.conf`가 들어 있다. 이 파일은 `server_name localhost`를 가지므로 local smoke test를 방해할 수 있다. `tools/infra/docker-compose.yml`은 컨테이너 시작 시 template을 생성한 뒤 이 기본 파일을 제거한다.

도메인별 동작을 정확히 확인하려면 Host 헤더를 붙여 테스트한다.

```powershell
Invoke-WebRequest -UseBasicParsing -Headers @{Host='example.com'} http://localhost/nginx-health
Invoke-WebRequest -UseBasicParsing -Headers @{Host='comfy.example.com'} http://localhost/nginx-health
```
