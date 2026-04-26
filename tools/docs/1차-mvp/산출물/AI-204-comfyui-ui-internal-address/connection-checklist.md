# AI-204 연결 체크리스트

## 사전 조건

- `tools/infra/.env`의 `COMFYUI_UPSTREAM`이 프록시 서버에서 접근 가능한 주소를 가리킨다.
- ComfyUI는 `tools/ComfyUI/run/start_comfyui.cmd` 기준으로 실행한다.
- ComfyUI 실행 옵션은 `--listen 0.0.0.0 --port 8188 --disable-auto-launch --preview-method auto`다.
- 외부 공개 전까지 ComfyUI `8188` 포트는 프록시 서버 또는 내부망 허용 대상에서만 접근 가능하게 둔다.

## AI-204-01 방화벽 범위

- [ ] Windows 방화벽에서 TCP `8188` 인바운드 규칙을 만든다.
- [ ] 허용 원격 IP는 프록시 서버 IP 또는 내부망 대역으로 제한한다.
- [ ] 임시 전체 허용을 사용했다면 테스트 후 원격 IP 범위를 다시 좁힌다.
- [ ] 방화벽 규칙 이름 예시는 `ComfyUI 8188 from AI proxy`로 둔다.

PowerShell 확인 예시:

```powershell
Get-NetFirewallRule -DisplayName '*ComfyUI*' | Format-Table DisplayName, Enabled, Direction, Action
Get-NetFirewallPortFilter -Protocol TCP | Where-Object { $_.LocalPort -eq 8188 }
```

## AI-204-02 HTTP 직접 연결

프록시 서버에서 실행한다.

```powershell
Invoke-WebRequest -UseBasicParsing -Uri "$env:COMFYUI_UPSTREAM" -TimeoutSec 10
```

현재 로컬 검증 예시:

```powershell
Invoke-WebRequest -UseBasicParsing -Uri 'http://127.0.0.1:8188' -TimeoutSec 5
Invoke-WebRequest -UseBasicParsing -Uri 'http://192.168.100.77:8188' -TimeoutSec 5
```

완료 기준:

- HTTP status가 `200`이다.
- 응답 본문에 ComfyUI HTML이 포함된다.

2026-04-27 로컬 운영 데스크탑 기준 확인:

- [x] `http://127.0.0.1:8188` direct HTTP `200`
- [x] `Host: comfy.example.com` + `http://localhost/` proxy HTTP `200`
- [x] `Host: comfy.example.com` + `http://localhost/object_info` proxy API `200`

## AI-204-03 WebSocket 연결

프록시 서버에서 직접 upstream을 먼저 확인한다.

```powershell
$ws = [System.Net.WebSockets.ClientWebSocket]::new()
$uri = [Uri]'ws://127.0.0.1:8188/ws?clientId=ai204-smoke'
$ct = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(10))
$ws.ConnectAsync($uri, $ct.Token).GetAwaiter().GetResult()
$ws.State
$ws.Dispose()
```

프록시 경유 확인은 Nginx가 떠 있는 상태에서 실행한다.

```powershell
$ws = [System.Net.WebSockets.ClientWebSocket]::new()
$ws.Options.SetRequestHeader('Host', 'comfy.example.com')
$uri = [Uri]'ws://localhost/ws?clientId=ai204-proxy-smoke'
$ct = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(10))
$ws.ConnectAsync($uri, $ct.Token).GetAwaiter().GetResult()
$ws.State
$ws.Dispose()
```

완료 기준:

- `Open` 상태가 한 번 이상 확인된다.
- Nginx 로그에 WebSocket upgrade 관련 에러가 없다.

2026-04-27 로컬 운영 데스크탑 기준 확인:

- [x] direct WebSocket `Open`
- [x] proxy WebSocket `HTTP/1.1 101 Switching Protocols`
- [x] proxy WebSocket에서 ComfyUI status message 수신

## AI-204-04 UI 점검

- [x] 브라우저에서 `http://localhost` 프록시 주소로 접속한다.
- [x] ComfyUI 화면이 로드된다.
- [x] 핵심 JS와 화면 리소스가 로드된다.
- [x] 모델 목록이 보인다.
- [x] `실행` 버튼이 반응한다.
- [x] 브라우저 개발자 도구 Network 탭에서 `/ws` 연결이 `101 Switching Protocols` 상태다.

메모:

- 한국어 UI의 `실행` 버튼은 기존 ComfyUI의 `Queue Prompt` 동작이다.
- `user.css`, `favicon.ico`, 일부 `userdata` 파일의 `404`는 사용자 커스텀 파일 또는 선택 데이터가 없을 때 발생할 수 있어, 화면 로드와 생성 동작이 정상이라면 AI-204 차단 이슈로 보지 않는다.

## AI-204-06 생성 smoke test

- [x] 이전 작업에서 사용한 기준 workflow를 로드한다.
- [x] 프록시 주소 `http://localhost`로 접속한 UI에서 생성 1회를 실행한다.
- [x] 이미지 생성 결과가 정상 출력된다.
- [x] `/ws`가 열린 상태에서 생성 큐가 동작한다.
- [x] 브라우저 화면 기준 치명적 UI 중단은 없다.

## 실패 판정

- direct HTTP가 실패하면 프록시 문제가 아니라 ComfyUI 실행, 방화벽, 네트워크 경로 문제로 본다.
- direct HTTP는 성공하지만 proxy HTTP가 실패하면 Nginx `COMFYUI_UPSTREAM`, DNS, Docker network, Host 헤더 문제로 본다.
- HTTP는 성공하지만 `/ws`만 실패하면 Nginx Upgrade/Connection 헤더와 proxy timeout을 먼저 확인한다.
