# AI-204 최종 검증 결과

## 확인 시각

- 2026-04-27 KST

## 환경

- 브랜치: `S14P31C201-304/204_comfy_UI_주소_연결`
- Nginx compose 위치: `tools/infra`
- `COMFYUI_UPSTREAM`: `http://host.docker.internal:8188`
- 운영 데스크탑 기준: 현재 노트북
- 브라우저 검증 주소: `http://localhost`

## 최종 확인 결과

| 항목 | 결과 | 판정 |
| --- | --- | --- |
| ComfyUI direct HTTP | `http://127.0.0.1:8188` `200` | 완료 |
| Nginx proxy HTTP | `http://localhost` `200` | 완료 |
| ComfyUI object_info API | `/object_info` `200` | 완료 |
| direct WebSocket | `Open` | 완료 |
| proxy WebSocket | `101 Switching Protocols` | 완료 |
| browser UI | `http://localhost`에서 ComfyUI UI 로드 | 완료 |
| generation smoke test | 프록시 주소에서 기존 workflow 이미지 생성 성공 | 완료 |

## 실행 기준

```powershell
cd "C:\Users\SSAFY\Desktop\2학기 3PJT\S14P31C201\tools\ComfyUI"
.\run\start_comfyui.cmd
Invoke-WebRequest -UseBasicParsing -Uri 'http://127.0.0.1:8188' -TimeoutSec 10
```

## 프록시 HTTP 확인

```powershell
cd "C:\Users\SSAFY\Desktop\2학기 3PJT\S14P31C201\tools\infra"
docker compose up -d --force-recreate nginx
docker compose exec nginx nginx -t
Invoke-WebRequest -UseBasicParsing -Uri 'http://localhost/' -TimeoutSec 10
```

## 프록시 WebSocket 확인

```powershell
curl.exe -i -N `
  -H "Connection: Upgrade" `
  -H "Upgrade: websocket" `
  -H "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==" `
  -H "Sec-WebSocket-Version: 13" `
  "http://localhost/ws?clientId=ai204-localhost-browser-path"
```

확인 결과:

```text
HTTP/1.1 101 Switching Protocols
```

## 브라우저 UI와 생성 확인

- ComfyUI 원본 UI가 프록시 주소에서 정상 로드됐다.
- 브라우저 개발자 도구 Network 탭에서 `/ws?clientId=...` 요청이 `101 Switching Protocols` 상태로 확인됐다.
- 이전 작업에서 사용한 workflow로 이미지 생성이 정상 완료됐다.
- 한국어 UI의 `실행` 버튼이 기존 ComfyUI의 `Queue Prompt` 동작임을 확인했다.
- 콘솔의 `user.css`, `favicon.ico`, 일부 `userdata` 404는 UI 로드와 생성 성공을 막지 않았다.

## 최종 결론

AI-204는 로컬 운영 데스크탑 기준으로 완료한다.

남는 운영 이슈:

- 작업장 또는 집 PC로 운영 데스크탑이 바뀌면 내부 IP와 `COMFYUI_UPSTREAM`을 다시 맞춰야 한다.
- EC2 공개 프록시 기준 검증은 도메인/HTTPS 전 단계에서 EC2 public IP 또는 tunnel 주소가 준비된 뒤 별도 확인한다.
