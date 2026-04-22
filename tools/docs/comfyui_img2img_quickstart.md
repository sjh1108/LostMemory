# ComfyUI img2img 빠른 시작

## 목적

- 팀 프로젝트 기준 ComfyUI 실험 위치를 `tools/ComfyUI`로 고정한다
- `img2img`를 바로 테스트할 수 있는 최소 실행 절차를 남긴다

실제 실행 명령과 1회 세팅은 `comfyui_local_run_guide.md`를 기준으로 본다.

## 현재 기준

- ComfyUI 소스 위치: `tools/ComfyUI`
- 기본 체크포인트 위치: `tools/ComfyUI/models/checkpoints`
- 입력 이미지 위치: `tools/ComfyUI/input`
- 생성 결과 위치: `tools/ComfyUI/output`

## Git 추적 제외 대상

아래 폴더는 로컬 실행 자산으로 보고 Git에 올리지 않는다.

- `tools/ComfyUI/models`
- `tools/ComfyUI/input`
- `tools/ComfyUI/output`
- `tools/ComfyUI/temp`
- `tools/ComfyUI/.venv*`

## 실행 준비

1. `tools/docs/comfyui_local_run_guide.md` 기준으로 로컬 환경을 준비한다.
2. ComfyUI 서버를 `127.0.0.1:8188`에서 실행한다.
3. 브라우저에서 `http://127.0.0.1:8188`에 접속한다.

이미 체크포인트가 있다면 `models/checkpoints` 아래에서 바로 선택하면 된다.

## 첫 img2img 실습 플로우

기본적으로 아래 노드 조합으로 시작하면 된다.

1. `Load Image`
2. `CheckpointLoaderSimple`
3. `CLIP Text Encode (Prompt)` 2개
4. 한 개는 positive prompt, 한 개는 negative prompt 용도로 쓴다
5. `VAE Encode`
6. `KSampler`
7. `VAE Decode`
8. `Save Image`

`CLIP Text Encode (Negative)`라는 별도 노드는 없다.
노드 검색에서 `CLIP Text Encode` 또는 `Prompt`로 찾은 뒤 같은 노드를 두 번 추가하면 된다.

## 노드 추가 순서

빈 캔버스에서 더블클릭하고 아래 순서대로 노드를 추가한다.

1. `Load Image`
2. `CheckpointLoaderSimple`
3. `CLIP Text Encode (Prompt)` 2개
4. `VAE Encode`
5. `KSampler`
6. `VAE Decode`
7. `Save Image`

## 선 연결 순서

아래처럼 출력 포트에서 입력 포트로 연결한다.

1. `CheckpointLoaderSimple.MODEL` -> `KSampler.model`
2. `CheckpointLoaderSimple.CLIP` -> `CLIP Text Encode (positive).clip`
3. `CheckpointLoaderSimple.CLIP` -> `CLIP Text Encode (negative).clip`
4. `CheckpointLoaderSimple.VAE` -> `VAE Encode.vae`
5. `CheckpointLoaderSimple.VAE` -> `VAE Decode.vae`
6. `Load Image.IMAGE` -> `VAE Encode.pixels`
7. `VAE Encode.LATENT` -> `KSampler.latent_image`
8. `CLIP Text Encode (positive).CONDITIONING` -> `KSampler.positive`
9. `CLIP Text Encode (negative).CONDITIONING` -> `KSampler.negative`
10. `KSampler.LATENT` -> `VAE Decode.samples`
11. `VAE Decode.IMAGE` -> `Save Image.images`

## 각 노드에 넣을 값

### 1. Load Image

- `image`: `example.png` 또는 직접 넣은 원본 도트 캐릭터 이미지

### 2. CheckpointLoaderSimple

- `ckpt_name`: `v1-5-pruned-emaonly-fp16.safetensors`

### 3. CLIP Text Encode (positive)

- `text`:

```text
pixel art character, clean outline, readable silhouette, limited palette, simple shading, game sprite style
```

### 4. CLIP Text Encode (negative)

- `text`:

```text
blurry, smooth shading, anti-aliased, realistic, 3d, painterly, noisy background, extra limbs, deformed
```

### 5. KSampler

- `seed`: 아무 숫자나 가능, 처음엔 `123456789`
- `steps`: `20`
- `cfg`: `7`
- `sampler_name`: `euler`
- `scheduler`: `normal`
- `denoise`: `0.25`

### 6. Save Image

- `filename_prefix`: `img2img_test`

## 권장 시작값

- checkpoint: `v1-5-pruned-emaonly-fp16.safetensors`
- steps: `20`
- cfg: `7`
- sampler: `euler`
- scheduler: `normal`
- denoise: `0.35`에서 `0.6` 사이로 먼저 테스트

`denoise`를 낮추면 원본 형태를 더 유지하고, 높이면 변형 폭이 커진다.

## 첫 테스트 순서

1. `input` 폴더에 원본 이미지를 넣는다.
2. `Load Image`로 원본 이미지를 불러온다.
3. `CheckpointLoaderSimple`에서 체크포인트를 고른다.
4. positive / negative prompt를 각각 넣는다.
5. `KSampler`에 `steps=20`, `cfg=7`, `sampler=euler`, `scheduler=normal`, `denoise=0.25`를 넣는다.
6. `Queue Prompt` 또는 `Ctrl + Enter`로 실행한다.
7. 원본 보존이 부족하면 `denoise`를 `0.15` 또는 `0.20`으로 낮춘다.
8. 변화가 너무 약하면 `denoise`를 `0.30` 또는 `0.35`로 올린다.

## denoise 해석 기준

- `0.15`: 원본 도트를 거의 유지하면서 아주 약하게 정리
- `0.25`: 첫 기준값, 보정과 유지의 균형
- `0.35`: 변화가 더 크고 새로 그리는 느낌이 조금 더 강함
- `0.45` 이상: 원본 캐릭터가 많이 바뀔 수 있어서 첫 실습에는 비추천

## 작은 도트 원본이 무너질 때

아래 조건이면 결과가 원본과 전혀 다르게 무너지기 쉽다.

- 원본이 아주 작은 스프라이트다
- 입력이 `gif`다
- 투명 배경을 그대로 넣었다
- `denoise`가 높다
- 베이스 모델만 쓰고 형태 고정 장치가 없다

SD1.5 img2img는 `32x32`, `48x48`, `64x64` 같은 작은 도트 원본을 직접 다듬는 용도로는 안정적이지 않다.
이 경우에는 먼저 입력 이미지를 실험용 크기로 키운 뒤 넣어야 한다.

## 작은 도트 원본 입력 준비

### 1. `gif` 대신 단일 프레임 `png` 사용

- 애니메이션 `gif`를 그대로 쓰지 말고 첫 프레임 또는 원하는 프레임 하나를 `png`로 저장한다.
- ComfyUI img2img 첫 실습은 단일 이미지 기준으로 진행한다.

### 2. 최근접 보간으로 4배~8배 확대

- 원본이 `32x32`면 `256x256`까지
- 원본이 `48x48`면 `384x384` 또는 `336x336`까지
- 원본이 `64x64`면 `256x256` 또는 `512x512`까지

중요한 점은 `부드럽게 확대`가 아니라 `최근접 보간(Nearest Neighbor)`으로 확대해야 도트 경계가 유지된다는 것이다.

### 2-1. ComfyUI 내부에서 바로 확대하는 방법

ComfyUI 안에서 바로 처리할 수 있다.

- 추천 노드: `Upscale Image`
- 대체 노드: `Resize Image/Mask`

둘 다 `nearest-exact`를 지원한다.

#### 최소 연결 순서

1. `Load Image`
2. `Upscale Image`
3. `VAE Encode`
4. `KSampler`
5. `VAE Decode`
6. `Save Image`

#### 선 연결

1. `Load Image.IMAGE` -> `Upscale Image.image`
2. `Upscale Image.IMAGE` -> `VAE Encode.pixels`

#### `Upscale Image` 값

- `upscale_method`: `nearest-exact`
- `width`, `height`: 실험용 확대 크기
- `crop`: `disabled`

예시:

- 원본이 `10x15`면 `width=160`, `height=240`
- 더 크게 보려면 `width=320`, `height=480`

배율만 넣고 싶으면 `Upscale Image By` 노드를 써도 된다.

- `upscale_method`: `nearest-exact`
- `scale_by`: `16` 또는 `32`

작은 도트 원본은 `Load Image -> Upscale Image(nearest-exact) -> VAE Encode` 순서로 먼저 키운 뒤 img2img에 넣는 것을 기본 기준으로 잡는다.

### 3. 가능하면 배경 단순화

- 투명 배경이 있으면 단색 배경 위에 올린 버전으로 먼저 테스트하는 것이 안정적이다.
- 첫 실험은 흰색, 회색, 단색 파랑 같은 단순 배경을 추천한다.

### 4. 캐릭터를 캔버스 중앙에 크게 배치

- 너무 작게 들어가면 모델이 캐릭터 형태를 제대로 못 잡는다.
- 실험용 입력에서는 캐릭터가 캔버스의 절반 이상을 차지하는 쪽이 낫다.

## 작은 도트 원본용 권장 수치

- `steps`: `12`~`20`
- `cfg`: `4.5`~`6`
- `sampler`: `euler`
- `scheduler`: `normal` 또는 `simple`
- `denoise`: `0.10`~`0.18`

작은 원본은 `denoise=0.25`도 높은 편일 수 있다.
원본 유지가 목표면 `0.12` 또는 `0.15`부터 시작하는 것이 안전하다.

## 작은 도트 원본용 프롬프트 작성법

positive prompt에는 스타일보다 `형태 고정 정보`를 먼저 넣는다.

예시:

```text
pixel art character, full body, front view, same pose, same silhouette, blonde hair, blue eyes, pink dress, clean outline, limited palette, game sprite style
```

negative prompt 예시:

```text
blurry, anti-aliased, realistic, 3d, painterly, soft shading, detailed background, extra limbs, deformed, cropped
```

## 그래도 많이 달라지면

아래 순서로 조정한다.

1. `gif`를 `png`로 바꾼다.
2. 최근접 보간으로 먼저 키운다.
3. `denoise`를 `0.10`~`0.15`로 낮춘다.
4. `cfg`를 `5` 전후로 낮춘다.
5. 그래도 자세가 무너지면 ControlNet 또는 전용 pixel LoRA를 검토한다.

## 다음 정리 후보

- 팀 공용 기본 워크플로 JSON 저장
- BE 연동용 ComfyUI API 호출 스크립트 추가
- infra용 내부 배포 기준 문서 추가

## 업데이트 사항

### 2026-04-21

- `CLIP Text Encode (Negative)`라는 별도 노드가 있는 것처럼 보일 수 있어, 실제 사용 노드가 `CLIP Text Encode (Prompt)` 2개라는 점을 문서에 명확히 반영했다.
- `img2img` 최소 배선에서 빠지기 쉬운 연결을 정리했다.
  - `VAE Encode.LATENT -> KSampler.latent_image`
  - `KSampler.LATENT -> VAE Decode.samples`
  - `VAE Decode.IMAGE -> Save Image.images`
- Windows + NVIDIA 환경에서 `RuntimeError: query is not correctly aligned (strideM)`가 발생할 수 있어 `--use-split-cross-attention` 우회 실행 기준을 `comfyui_local_run_guide.md`에 추가했다.
- 아주 작은 도트 원본을 그대로 넣으면 결과가 무너질 수 있어 `gif -> 단일 프레임 png -> 최근접 보간 확대 -> img2img` 순서로 실험하는 기준을 추가했다.
- ComfyUI 내부에서 처리할 수 있도록 `Upscale Image` 또는 `Upscale Image By` 노드의 `nearest-exact` 사용법을 정리했다.
- 작은 도트 원본용 시작 수치를 별도로 추가했다.
  - `denoise`: `0.10`~`0.18`
  - `cfg`: `4.5`~`6`
  - `steps`: `12`~`20`
