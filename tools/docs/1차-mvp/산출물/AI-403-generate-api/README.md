# AI-403 생성 요청용 최소 API 엔드포인트 산출물

`AI-403`에서는 `tools/ai_server`에 내부 화면 또는 테스트 클라이언트가 호출할 수 있는 최소 생성 요청 API 계약을 추가한다.

## 엔드포인트

- `POST /api/generation-requests`

## 요청 본문

```json
{
  "workflowId": "pixel-art-character-v1",
  "prompt": "pixel art mage girl, blue robe, idle pose",
  "userId": "ssafy-user-01"
}
```

## 검증 규칙

- `workflowId`: 필수, 최대 100자
- `prompt`: 필수, 최대 2000자
- `userId`: 선택, 최대 64자

## 응답 기준

- 성공 시 `202 Accepted`
- 공통 `ApiResponse` 포맷 사용
- 현재 단계에서는 ComfyUI `/prompt`를 아직 호출하지 않고, 서버가 생성 요청 초안을 받았다는 의미의 `requestId`, `status=RECEIVED`, `acceptedAt`를 반환한다.

## 오류 응답

- 필수 필드 누락 또는 길이 제한 위반: `INVALID_REQUEST`
- 잘못된 JSON body: `INVALID_REQUEST_BODY`

## 다음 단계

- `AI-404`에서 controller 입력을 ComfyUI `/prompt` body로 조립하는 service를 추가한다.
- `AI-404`에서 ComfyUI 응답의 `prompt_id`를 받아 실제 생성 흐름과 연결한다.
