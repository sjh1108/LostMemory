# 컴피유아이 1차 MVP 진행 기록

## 문서 목적

이 문서는 `컴피유아이-1차-mvp-통합-작업표.csv`의 주요 작업코드별 진행 결과를 기록한다.

CSV는 완료 여부를 빠르게 보기 위한 표이고, 이 문서는 왜 완료로 판단했는지와 산출물이 어디에 있는지를 남기는 기록이다.

## 기록 기준

- 완료여부 `Y`: 실행 또는 산출물 확인까지 끝난 작업
- 상태 `완료`: 다음 작업이 이 결과를 선행 조건으로 사용할 수 있는 상태
- 완료 근거: 접속 URL, 실행 명령, workflow JSON, output 이미지처럼 재확인 가능한 자료를 우선 기록

## AI-201. GPU 데스크탑 ComfyUI host, port, output 경로 고정

### 작업 범위

- GPU 데스크탑 내부 IP 또는 고정 호스트명 확인
- ComfyUI listen 주소를 내부망 접근 가능 상태로 조정
- `output`, `models`, `loras` 경로와 운영 위치 확인
- 실행 명령과 재기동 스크립트 표준화

### 결정 내용

- GPU 데스크탑 내부 IP: `192.168.100.77`
- ComfyUI 포트: `8188`
- GPU PC 로컬 접속 주소: `http://127.0.0.1:8188`
- 팀원 내부망 접속 주소: `http://192.168.100.77:8188`
- 실행 스크립트: `tools/ComfyUI/run/start_comfyui.cmd`
- 실행 기준: `main.py --listen 0.0.0.0 --port 8188`

### 완료 근거

- GPU PC에서 `http://127.0.0.1:8188` 접속 확인
- GPU PC에서 `http://192.168.100.77:8188` 접속 확인
- 팀원 PC에서 `http://192.168.100.77:8188` 접속 확인
- `tools/ComfyUI/run/start_comfyui_readme.md`에 실행 기준 정리
- `tools/BE/.env.example`에 `COMFYUI_BASE_URL` 기준 추가

### 상태

- 완료여부: `Y`
- 상태: `완료`
- 후속 작업: `AI-202`, `AI-301`, `AI-507`

## AI-202. 기준 workflow 1종과 기준 프롬프트 확정

### 작업 범위

- 1차 MVP 기준 workflow 1종 선정
- 기준 프롬프트, negative prompt, seed, steps 등 테스트 값을 정리
- 기준 workflow로 output 이미지 생성

### 결정 내용

- 기준 모델: `Z-Image-Turbo`
- 기준 용도: 1차 MVP의 text-to-image 생성, API 샘플 확보, 저장/조회 검증
- 장기 방향: 도트풍은 공개 pixel-art LoRA와 이후 프로젝트 전용 LoRA 학습으로 강화
- 현재 단계: 직접 학습 모델이 아니라 Z-Image-Turbo + pixel-art LoRA 적용 가능성 확인

### 모델 구성

- diffusion model: `z_image_turbo_bf16`
- text encoder: `qwen_3_4b`
- CLIP loader type: `lumina2`
- VAE: `ae.safetensors`
- LoRA: `pixel_art_style_z_image_turbo`

### 기준 프롬프트

Positive prompt:

```text
Pixel art style. a small fantasy game character, full body, front view, yellow spiky hair, pale white skin, green tunic, red scarf, wooden shield, iron sword, brown boots, clean black outline, limited color palette, simple shading, readable silhouette, game sprite, centered, plain white background
```

Negative prompt:

```text
blurry, smooth shading, realistic, 3d render, painterly, noisy background, detailed background, text, watermark, deformed, extra limbs, bad hands, cropped, side view, back view
```

### 산출물

Workflow JSON:

- `산출물/AI-202-Z-Image-Turbo/workflows/Z-Image-turbo-test1.json`
- `산출물/AI-202-Z-Image-Turbo/workflows/Z-Image-turbo-test2-prompt-change.json`
- `산출물/AI-202-Z-Image-Turbo/workflows/Z-Image-turbo-test3-ksampler-change.json`

Output 이미지:

- `산출물/AI-202-Z-Image-Turbo/outputs/AI202_ZImageTurbo_PixelArt_00001.png`
- `산출물/AI-202-Z-Image-Turbo/outputs/AI202_ZImageTurbo_PixelArt_00002.png`
- `산출물/AI-202-Z-Image-Turbo/outputs/AI202_ZImageTurbo_PixelArt_00003.png`

### 완료 근거

- Z-Image-Turbo workflow에서 이미지 생성 성공
- pixel-art LoRA를 붙인 상태에서 output 이미지 3개 생성
- 프롬프트 구체화 후 캐릭터 속성 반영 방향 확인
- 생성 output은 `tools/ComfyUI/output`이 gitignore 대상이므로, MR 검토용 산출물은 `tools/docs/1차-mvp/산출물/AI-202-Z-Image-Turbo/` 아래로 복사해 보존

### 상태

- 완료여부: `Y`
- 상태: `완료`
- 후속 작업: `AI-301`, `AI-302`, `AI-303`

## 현재 주의사항

- `tools/ComfyUI/output`은 런타임 산출물이라 Git 추적 대상이 아니다.
- MR에 남길 기준 이미지는 `tools/docs/1차-mvp/산출물` 아래에 별도로 복사해야 한다.
- 이번 AI-202는 “생성 가능 여부와 기준 workflow 확정”까지이며, 프로젝트 전용 LoRA 학습은 2차 이후 별도 작업으로 분리한다.
