# AI-303 prompt_id-output 매핑 규칙 산출물

`AI-303`에서는 `AI-301`, `AI-302`에서 확보한 실제 성공 샘플을 기준으로 `prompt_id`, 생성 시각, output 파일명, workflow 메타데이터를 어떻게 연결해 DB에 저장할지 기준을 정리한다.

## 기준 샘플

- source workflow file:
  - `tools/docs/1차-mvp/산출물/AI-202-Z-Image-Turbo/workflows/Z-Image-turbo-test3-ksampler-change.json`
- prompt sample:
  - `tools/docs/1차-mvp/산출물/AI-301-prompt-sample/successful-prompt-request-test3.json`
- history sample:
  - `tools/docs/1차-mvp/산출물/AI-302-history-sample/history-response-d4bc5cf9-f555-430c-a32b-b61e4c8b4bb6.json`

## 관련 파일

- `prompt-output-mapping-table.md`

## 핵심 기준

- `prompt_id`는 `/prompt` 성공 응답에서 받은 값을 그대로 1차 추적 키로 사용한다.
- output 파일명은 `/history/{prompt_id}`의 `outputs[*].images[*].filename`에서 읽는다.
- 생성 시각은 `/history` raw JSON에 없으므로, 1차 기준에서는 output 파일의 filesystem timestamp를 사용한다.
- workflow 이름은 ComfyUI `/history` 응답에서 읽지 않고, 1차 MVP에서는 source workflow 파일명 stem을 그대로 저장한다.
- 모델명은 history 응답이 아니라 workflow 또는 prompt request body 내부 model loader node에서 읽는다.

## 메모

- 이번 샘플 기준 `prompt_id`는 `d4bc5cf9-f555-430c-a32b-b61e4c8b4bb6`이다.
- 실제 생성 파일은 `AI301_Test3_PromptSample_00001_.png`이다.
- output node id는 이번 샘플에서는 `8`이지만 고정 규칙이 아니므로 `outputs` map 순회 기준으로 구현해야 한다.
