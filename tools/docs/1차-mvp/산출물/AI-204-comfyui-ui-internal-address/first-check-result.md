# AI-204 첫 연결 확인 결과

## 확인 시각

- 2026-04-27 KST

## 환경

- 브랜치: `S14P31C201-292_AI-204_ComfyUI_UI_internal_address`
- Nginx compose 위치: `tools/infra`
- `COMFYUI_UPSTREAM`: `http://host.docker.internal:8188`
- GPU 데스크탑 기존 문서 주소: `http://192.168.100.77:8188`

## 실행한 확인

### Nginx 컨테이너 상태

```powershell
docker compose ps
```

결과:

```text
infra-nginx-1   nginx:1.27-alpine   Up   0.0.0.0:80->80/tcp, 0.0.0.0:443->443/tcp
```

판정:

- Nginx 자체는 실행 중이다.

### ComfyUI 로컬 직접 HTTP

```powershell
Invoke-WebRequest -UseBasicParsing -Uri 'http://127.0.0.1:8188' -TimeoutSec 5
```

결과:

```text
원격 서버에 연결할 수 없습니다.
```

판정:

- 현재 작업 PC의 `8188`에는 ComfyUI가 떠 있지 않다.

### ComfyUI 내부망 직접 HTTP

```powershell
Invoke-WebRequest -UseBasicParsing -Uri 'http://192.168.100.77:8188' -TimeoutSec 5
```

결과:

```text
원격 서버에 연결할 수 없습니다.
```

판정:

- 현재 작업 PC에서 기존 GPU 데스크탑 ComfyUI 주소로 연결되지 않는다.
- 원인은 ComfyUI 미기동, IP 변경, 방화벽, 같은 내부망 미연결 중 하나로 본다.

### Nginx 프록시 경유 HTTP

```powershell
Invoke-WebRequest -UseBasicParsing -Headers @{Host='comfy.example.com'} -Uri 'http://localhost/' -TimeoutSec 10
```

결과:

```text
502 Bad Gateway
```

Nginx 로그:

```text
connect() failed (111: Connection refused) while connecting to upstream,
upstream: "http://192.168.65.254:8188/",
host: "comfy.example.com"
```

판정:

- 프록시 설정은 ComfyUI upstream으로 요청을 전달하고 있다.
- upstream 대상에서 `8188` 연결을 거부하므로 현재 실패 원인은 Nginx routing보다 ComfyUI upstream 미기동이다.

## 현재 결론

AI-204는 시작했지만 완료 상태가 아니다.

현재 블로커:

- ComfyUI가 `127.0.0.1:8188` 또는 `192.168.100.77:8188`에서 응답하지 않는다.

다음 확인:

1. GPU/운영 데스크탑에서 `tools/ComfyUI/run/start_comfyui.cmd`를 실행한다.
2. `tools/infra/.env`의 `COMFYUI_UPSTREAM`을 실제 접근 가능한 주소로 맞춘다.
3. HTTP direct 확인 후 proxy HTTP, proxy WebSocket, browser UI, generation smoke test 순서로 재검증한다.

## 재확인 결과

### ComfyUI 실행 후 direct HTTP

```powershell
Invoke-WebRequest -UseBasicParsing -Uri 'http://127.0.0.1:8188' -TimeoutSec 10
```

결과:

```text
StatusCode: 200
Content: <!doctype html><html lang="en">...
```

판정:

- 운영 데스크탑 역할의 현재 노트북에서 ComfyUI가 정상 실행 중이다.

### Nginx 재생성

```powershell
docker compose up -d --force-recreate nginx
docker compose exec nginx nginx -t
```

결과:

```text
nginx: the configuration file /etc/nginx/nginx.conf syntax is ok
nginx: configuration file /etc/nginx/nginx.conf test is successful
```

### 프록시 경유 HTTP

```powershell
Invoke-WebRequest -UseBasicParsing -Headers @{Host='comfy.example.com'} -Uri 'http://localhost/' -TimeoutSec 10
```

결과:

```text
StatusCode: 200
Content: <!doctype html><html lang="en">...
```

판정:

- `COMFYUI_UPSTREAM=http://host.docker.internal:8188` 기준으로 Nginx가 ComfyUI 원본 UI HTML을 정상 프록시한다.

### 프록시 경유 object_info

```powershell
Invoke-WebRequest -UseBasicParsing -Headers @{Host='comfy.example.com'} -Uri 'http://localhost/object_info' -TimeoutSec 20
```

결과:

```text
StatusCode: 200
Length: 1055989
```

판정:

- ComfyUI 모델/노드 정보 API가 프록시 경유로 응답한다.

### direct WebSocket

```powershell
$ws = [System.Net.WebSockets.ClientWebSocket]::new()
$uri = [Uri]'ws://127.0.0.1:8188/ws?clientId=ai204-direct-smoke'
$ct = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(10))
$ws.ConnectAsync($uri, $ct.Token).GetAwaiter().GetResult()
$ws.State
$ws.Dispose()
```

결과:

```text
Open
```

### 프록시 경유 WebSocket

```powershell
curl.exe -i -N `
  -H "Host: comfy.example.com" `
  -H "Connection: Upgrade" `
  -H "Upgrade: websocket" `
  -H "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==" `
  -H "Sec-WebSocket-Version: 13" `
  "http://localhost/ws?clientId=ai204-proxy-curl"
```

결과:

```text
HTTP/1.1 101 Switching Protocols
Upgrade: websocket
{"type": "status", "data": {"status": {"exec_info": {"queue_remaining": 0}}, "sid": "ai204-proxy-curl"}}
```

판정:

- Nginx 프록시 경유 `/ws` 연결이 정상 upgrade된다.
- timeout은 WebSocket 연결이 열린 채 유지되어 발생한 것으로 실패가 아니다.

## 갱신된 결론

- AI-204-02 HTTP 연결 확인: 로컬 운영 데스크탑 기준 완료
- AI-204-03 WebSocket 연결 확인: 로컬 운영 데스크탑 기준 완료
- AI-204-04 브라우저 UI 점검: 로컬 운영 데스크탑 기준 완료
- AI-204-06 프록시 주소 기준 생성 1회 smoke test: 로컬 운영 데스크탑 기준 완료

## 브라우저 UI와 생성 smoke test

확인 주소:

```text
http://localhost
```

확인 결과:

- ComfyUI 원본 UI가 프록시 주소에서 정상 로드됐다.
- 브라우저 개발자 도구 Network 탭에서 `/ws?clientId=...` 요청이 `101 Switching Protocols` 상태로 확인됐다.
- 이전 작업에서 사용한 workflow로 이미지 생성이 정상 완료됐다.
- 한국어 UI의 `실행` 버튼이 기존 ComfyUI의 `Queue Prompt` 동작임을 확인했다.
- 콘솔의 `user.css`, `favicon.ico`, 일부 `userdata` 404는 사용자 커스텀 파일 또는 선택 데이터 부재로 보이며, UI 로드와 생성 성공을 막지 않았다.

## 최종 결론

AI-204는 로컬 운영 데스크탑 기준으로 완료한다.

남는 운영 이슈:

- 작업장 또는 집 PC로 운영 데스크탑이 바뀌면 내부 IP와 `COMFYUI_UPSTREAM`을 다시 맞춰야 한다.
- EC2 공개 프록시 기준 검증은 도메인/HTTPS 전 단계에서 EC2 public IP 또는 tunnel 주소가 준비된 뒤 별도 확인한다.
