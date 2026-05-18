# AI-304 실패 샘플 결과

## 실패 가능성 리스트업

이번 단계에서 열어본 후보는 아래와 같다.

1. 모델 없음
2. 잘못된 workflow 구조
3. 잘못된 node reference
4. malformed JSON body
5. 생성 시간이 길어 polling timeout 초과

이 중 실제로 바로 재현 가능한 1~4를 샘플로 수집했고, 5는 관측된 성공 실행 시간 기준으로 timeout 초안만 작성했다.

## 실제 수집 결과

| 케이스 | request 파일 | HTTP status | ComfyUI 로그 핵심 | 비고 |
| --- | --- | --- | --- | --- |
| 모델 없음 | `invalid-unet-name-request.json` | `400` | `Value not in list: unet_name: 'missing_model_for_ai304_test.safetensors'` | `invalid prompt`, `prompt_outputs_failed_validation` |
| output node 없음 | `missing-output-node-request.json` | `400` | `invalid prompt: {'type': 'prompt_no_outputs'` | 결과 저장 node 자체가 없음 |
| 잘못된 node reference | `invalid-node-reference-request.json` | `400` | `Exception when validating inner node: '999'` | `prompt_outputs_failed_validation` |
| malformed JSON | `malformed-json-request.txt` | `500` | `JSONDecodeError: Expecting ',' delimiter` | body 파싱 단계 실패 |

## 샘플별 상세 메모

### 1. 모델 없음

- 변경 지점:
  - `UNETLoader.inputs.unet_name`
  - `z_image_turbo_bf16.safetensors` -> `missing_model_for_ai304_test.safetensors`
- 결과:
  - HTTP `400`
  - stderr:
    - `Failed to validate prompt for output 8`
    - `Value not in list: unet_name: 'missing_model_for_ai304_test.safetensors' not in ['z_image_turbo_bf16.safetensors']`
    - `invalid prompt: {'type': 'prompt_outputs_failed_validation', 'message': 'Prompt outputs failed validation', ...}`
- 해석:
  - request JSON 문법은 맞지만 model loader validation에서 걸린다.
  - backend에서는 `PROMPT_SUBMIT` 또는 `WORKFLOW_VALIDATION` 성격 실패로 분류하는 편이 자연스럽다.

### 2. output node 없음

- 변경 지점:
  - `SaveImage` node `8` 제거
- 결과:
  - HTTP `400`
  - stderr:
    - `invalid prompt: {'type': 'prompt_no_outputs', 'message': 'Prompt has no outputs', ...}`
- 해석:
  - workflow 자체가 결과물을 내보낼 수 없는 구조다.
  - backend에서는 사용자 프리셋/워크플로 구성 오류로 보기 좋다.

### 3. 잘못된 node reference

- 변경 지점:
  - `KSampler.inputs.positive = ["999", 0]`
- 결과:
  - HTTP `400`
  - stderr:
    - `Failed to validate prompt for output 8`
    - `Exception when validating inner node: '999'`
    - `invalid prompt: {'type': 'prompt_outputs_failed_validation', 'message': 'Prompt outputs failed validation', ...}`
- 해석:
  - workflow link가 깨진 구조다.
  - backend에서는 workflow snapshot 자체 오류로 보는 편이 맞다.

### 4. malformed JSON body

- 변경 지점:
  - JSON closing delimiter 누락
- 결과:
  - HTTP `500`
  - stderr:
    - `JSONDecodeError: Expecting ',' delimiter: line 12 column 1 (char 232)`
- 해석:
  - ComfyUI 내부 validation 전, body parsing 단계에서 터진다.
  - backend에서 실제로 이 케이스를 보게 되면 request assembly 또는 직렬화 버그로 봐야 한다.

## 구현 관점 메모

- validation 실패인데도 응답 body가 비어 있는 케이스가 있었다.
- 따라서 1차 MVP에서는 아래 우선순위로 실패를 분류하는 게 안전하다.
  1. backend 사전 검증 실패 여부
  2. ComfyUI HTTP status
  3. ComfyUI 응답 body
  4. 운영 로그/stderr
