# AI-302 GET /history/{prompt_id} 응답 샘플 산출물

`AI-302`에서는 `AI-301`에서 얻은 `prompt_id` 기준으로 실제 `/history/{prompt_id}` raw JSON과 key 해석 기준을 남긴다.

## 기준 prompt_id

- `d4bc5cf9-f555-430c-a32b-b61e4c8b4bb6`

## 관련 파일

- `history-response-d4bc5cf9-f555-430c-a32b-b61e4c8b4bb6.json`

## 실제 결과

- `status.status_str`: `success`
- `status.completed`: `true`
- output file:
  - `AI301_Test3_PromptSample_00001_.png`
- output file local path:
  - `tools/ComfyUI/output/AI301_Test3_PromptSample_00001_.png`

## output key 추출 규칙

- top-level key:
  - 조회에 사용한 `prompt_id`가 그대로 key로 들어간다.
- output node map:
  - `outputs`
- image list:
  - `outputs["8"].images`
- 첫 번째 파일명:
  - `outputs["8"].images[0].filename`
- subfolder:
  - `outputs["8"].images[0].subfolder`
- 저장 타입:
  - `outputs["8"].images[0].type`
- 성공 상태:
  - `status.status_str == "success"`
  - `status.completed == true`

## 메모

- 이번 sample에서는 `SaveImage` node id가 `8`이라 `outputs["8"]`로 조회된다.
- 이후 다른 workflow에서는 `SaveImage` node id가 달라질 수 있으므로, backend 구현에서는 output map key를 고정값으로 박지 말고 실제 `outputs` map을 순회하는 쪽이 안전하다.
