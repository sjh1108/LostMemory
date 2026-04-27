# AI-301 curl 재현 절차

## 사전 조건

- ComfyUI 실행 주소: `http://127.0.0.1:8188`
- request sample file:
  - `successful-prompt-request-test3.json`

## curl 예시

PowerShell 기준:

```powershell
curl.exe -X POST "http://127.0.0.1:8188/prompt" `
  -H "Content-Type: application/json" `
  --data "@tools/docs/1차-mvp/산출물/AI-301-prompt-sample/successful-prompt-request-test3.json"
```

## 성공 응답 예시

```json
{
  "prompt_id": "d4bc5cf9-f555-430c-a32b-b61e4c8b4bb6",
  "number": 2,
  "node_errors": {}
}
```

## 확인 포인트

- `prompt_id`가 반환되어야 한다.
- `node_errors`는 비어 있어야 한다.
- 이후 `GET /history/{prompt_id}`로 결과와 상태를 조회한다.
