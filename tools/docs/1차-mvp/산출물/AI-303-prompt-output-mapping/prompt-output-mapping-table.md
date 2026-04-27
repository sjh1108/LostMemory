# AI-303 prompt_id-output 매핑 표

## 기준 매핑 표

| 항목 | 값 | 읽는 위치 | 저장 기준 |
| --- | --- | --- | --- |
| prompt_id | `d4bc5cf9-f555-430c-a32b-b61e4c8b4bb6` | `/prompt` 응답 `prompt_id` | generation_request 1차 추적 키 |
| ComfyUI status | `success` | `/history/{prompt_id}.status.status_str` | 완료 상태 판단 기준 |
| completed | `true` | `/history/{prompt_id}.status.completed` | 완료 여부 boolean |
| output node key | `8` | `/history/{prompt_id}.outputs` map key | 이번 샘플 기준 only |
| output filename | `AI301_Test3_PromptSample_00001_.png` | `/history/{prompt_id}.outputs["8"].images[0].filename` | 실제 생성 파일명 |
| output subfolder | `""` | `/history/{prompt_id}.outputs["8"].images[0].subfolder` | ComfyUI output 상대 경로 |
| output type | `output` | `/history/{prompt_id}.outputs["8"].images[0].type` | ComfyUI 저장 타입 |
| local output path | `tools/ComfyUI/output/AI301_Test3_PromptSample_00001_.png` | 파일 시스템 | 1차 로컬 검증 기준 |
| created_at | `2026-04-27 14:38:30 +09:00` | output 파일 `CreationTime` | history에 없으므로 filesystem timestamp 사용 |
| workflow_name | `Z-Image-turbo-test3-ksampler-change` | source workflow 파일명 stem | 1차 MVP workflow 식별자 |

## workflow/모델명 추출 규칙

### 1. workflow 이름

- `/history` 응답에는 workflow 이름 필드가 없다.
- 따라서 1차 MVP에서는 source workflow 파일명 stem을 그대로 `workflow_name`으로 저장한다.
- 이번 샘플의 workflow 이름은 `Z-Image-turbo-test3-ksampler-change`로 고정한다.
- 이후 서버에 별도 `workflowId` registry가 생기더라도, 현재 문서 기준 `workflow_name`은 source workflow 파일명 stem과 일치해야 한다.

### 2. 모델명

- 모델명은 `/history` 응답이 아니라 workflow 또는 `/prompt` request body에서 읽는다.
- 이번 샘플에서 저장 후보는 다음과 같다.
  - UNET:
    - node `1` `UNETLoader.inputs.unet_name`
    - `z_image_turbo_bf16.safetensors`
  - LoRA:
    - node `3` `LoraLoader.inputs.lora_name`
    - `pixel_art_style_z_image_turbo.safetensors`
  - CLIP:
    - node `10` `CLIPLoader.inputs.clip_name`
    - `qwen_3_4b.safetensors`
  - VAE:
    - node `9` `VAELoader.inputs.vae_name`
    - `ae.safetensors`

### 3. backend 저장 권장안

- 1차 MVP에서는 아래 정도면 충분하다.
  - `prompt_id`
  - `workflow_name`
  - `output_filename`
  - `output_subfolder`
  - `output_type`
  - `status`
  - `created_at`
- 모델 메타데이터는 한 컬럼 문자열보다 별도 JSON metadata 또는 확장 필드로 두는 쪽이 낫다.

## 구현 메모

- `/history` parser는 특정 output node key를 하드코딩하지 말고 `outputs` map 전체를 순회해야 한다.
- 이미지가 여러 장일 수 있으므로 `images[0]`만 고정하지 말고 list 전체를 처리할 수 있게 열어두는 편이 안전하다.
- `created_at`을 filesystem timestamp로 읽는 현재 기준은 `AI-507` 파일 탐색 로직과 같이 가야 한다.
