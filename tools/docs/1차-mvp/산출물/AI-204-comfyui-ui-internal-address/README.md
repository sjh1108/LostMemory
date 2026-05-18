# AI-204 ComfyUI 원본 UI 내부 주소 연결 산출물

## 목적

이 폴더는 `AI-204 ComfyUI 원본 UI 내부 주소 연결` 작업의 확인 결과와 재시도 절차를 둔다.

`AI-203`에서 만든 Nginx reverse proxy 초안이 실제 ComfyUI upstream으로 연결되는지 HTTP, WebSocket, 브라우저 UI, 생성 smoke test 순서로 확인한다.

## 포함 문서

```text
AI-204-comfyui-ui-internal-address/
  README.md
  connection-checklist.md
  run-and-restart-memo.md
  first-check-result.md
```

## 작업 코드와 문서 매핑

| 작업 코드 | 내용 | 문서 |
| --- | --- | --- |
| AI-204-01 | GPU 데스크탑 방화벽에서 ComfyUI 포트 허용 범위 설정 | `connection-checklist.md`, `run-and-restart-memo.md` |
| AI-204-02 | 프록시 서버에서 ComfyUI HTTP 연결 확인 | `first-check-result.md`, `connection-checklist.md` |
| AI-204-03 | 프록시 서버에서 ComfyUI WebSocket 연결 확인 | `connection-checklist.md` |
| AI-204-04 | 정적 파일과 UI 동작 최종 점검 | `connection-checklist.md` |
| AI-204-05 | ComfyUI 백그라운드 실행과 재기동 기준 정리 | `run-and-restart-memo.md` |
| AI-204-06 | 프록시 주소 기준 생성 1회 smoke test | `connection-checklist.md` |

## 최종 판정

2026-04-27 기준 운영 데스크탑 역할의 현재 노트북에서 ComfyUI를 실행하고 로컬 Nginx 프록시 검증을 수행했다.

확인 결과:

- direct HTTP: `http://127.0.0.1:8188` `200`
- proxy HTTP: `http://localhost` `200`
- direct WebSocket: `Open`
- proxy WebSocket: `101 Switching Protocols`
- browser UI: 프록시 주소에서 ComfyUI 화면 로드
- generation smoke test: 프록시 주소에서 이전 workflow 기준 이미지 생성 성공

따라서 AI-204는 로컬 운영 데스크탑 기준으로 완료한다.

## 다음 액션

1. 작업장 또는 집 PC로 운영 데스크탑이 바뀌면 내부 IP를 다시 확인한다.
2. `tools/infra/.env`의 `COMFYUI_UPSTREAM`을 새 운영 데스크탑 주소로 맞춘다.
3. EC2 공개 프록시를 사용할 때는 EC2에서 운영 데스크탑으로 접근 가능한 tunnel 또는 VPN 경로를 먼저 준비한다.
4. EC2 기준 HTTP, WebSocket, 브라우저 UI, 생성 smoke test를 다시 확인한다.
