# AI-301 POST /prompt 요청 샘플 산출물

`AI-301`에서는 ComfyUI `/prompt` 호출에 실제로 성공한 request body와 재현 절차를 남긴다.

## 기준 workflow

- source workflow: `../AI-202-Z-Image-Turbo/workflows/Z-Image-turbo-test3-ksampler-change.json`
- 선택 이유:
  - `KSampler` 설정이 `steps=8`, `cfg=1.5`, `scheduler=simple`로 조정된 최종 실험 파일이라 API 연동 기준 sample로 쓰기 적합하다.

## 실제 검증 결과

- request sample file: `successful-prompt-request-test3.json`
- response sample file: `successful-prompt-response-test3.json`
- 실제 성공 응답 `prompt_id`: `d4bc5cf9-f555-430c-a32b-b61e4c8b4bb6`
- 실제 생성 output: `AI301_Test3_PromptSample_00001_.png`

## 이번 sample에서 고정한 값

- `client_id`: `ai301-test3-sample-client`
- `seed`: `30120260427`
- `filename_prefix`: `AI301_Test3_PromptSample`

## request body 기준

- `prompt`:
  - node id를 key로 가지는 map 구조
  - 각 node는 `inputs`, `class_type`만 포함하는 최소 형식 사용
- `client_id`:
  - 요청 단위를 식별하기 위한 문자열

`_meta`, workflow UI layout 정보 등은 `/prompt` 최소 성공 sample에서 제외했다.

## 관련 파일

- `successful-prompt-request-test3.json`
- `successful-prompt-response-test3.json`
- `reproduce-with-curl.md`
