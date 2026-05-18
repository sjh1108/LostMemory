# AI-404 서버에서 ComfyUI /prompt 호출 구현

`AI-404`에서는 `POST /api/generation-requests` 요청을 받아 실제 ComfyUI `/prompt` body를 조립하고, `/prompt` 응답의 `prompt_id`를 반환하는 최소 submit path를 구현한다.

## 구현 범위

- `PromptAssemblyService` 추가
- `AI-301` 성공 sample 기반 prompt template classpath resource 추가
- `GenerationService`에서 실제 `ComfyUiClient.submitPrompt(...)` 호출
- `/prompt` 응답의 `prompt_id` 추출
- `status = SUBMITTED` 응답 계약 반영
- `/prompt` request/response debug logging 추가

## 현재 지원 범위

- supported `workflowId`
  - `pixel-art-character-v1`
- source template
  - `src/main/resources/comfyui/prompt-templates/pixel-art-character-v1.json`
- prompt injection node
  - positive prompt node `4`
- save image node
  - `8`

## 응답 변화

- `AI-403`:
  - validation 후 draft `202 Accepted`
  - `status = RECEIVED`
- `AI-404`:
  - 실제 ComfyUI `/prompt` submit 수행
  - `promptId` 포함
  - `status = SUBMITTED`

## 예외 처리 기준

- unsupported workflow:
  - `400`
  - `UNSUPPORTED_WORKFLOW`
- ComfyUI `/prompt` submit 실패:
  - `502`
  - `COMFYUI_SUBMIT_FAILED`
- ComfyUI 응답에 `prompt_id` 없음:
  - `502`
  - `INVALID_COMFYUI_RESPONSE`

## 테스트

- `GenerationControllerTest`
  - `202 Accepted`
  - `promptId` 포함
  - `status = SUBMITTED`
- `PromptAssemblyServiceTest`
  - prompt text 치환
  - client_id / filename_prefix / seed 생성
  - unsupported workflow 거부
