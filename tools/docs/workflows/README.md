# LostMemory ComfyUI 워크플로 모음

게임 자산용 ComfyUI 워크플로 JSON 모음. ComfyUI UI에 import해서 바로 사용.

## 파일

| 파일 | 용도 | 입력 | 출력 |
|---|---|---|---|
| `LostMemory_Item_TXT2IMG.json` | 텍스트만으로 아이템 생성 | 프롬프트 | 512×512 PNG |
| `LostMemory_Item_IMG2IMG.json` | 레퍼런스 이미지 기반 변형 | 이미지 + 프롬프트 | 512×512 PNG |

## 사용법

### 방법 A. 이미 user/default/workflows에 있음 (이 PC에서)
ComfyUI 사이드바 → **Workflows** → 파일명 클릭 → 캔버스에 로드

### 방법 B. 다른 PC/사람이 import
1. 위 .json 파일 다운로드
2. ComfyUI UI 위에 드래그앤드롭
3. 또는 사이드바 Workflow → Open → 파일 선택

### 공통 다음 단계
1. 노드 4 (긍정 프롬프트)에서 `< ITEM HERE >` 부분 교체
2. Queue Prompt (`Ctrl+Enter`)
3. 결과 `tools/ComfyUI/output/LostMemory_xxx_*.png`
4. 이 PC cmd:
   - 일반 아이콘: `pixelize-game-item32.cmd <파일>`
   - 특수 아이템: `pixelize-game-item48.cmd <파일>`
5. 결과를 Unity `Assets/_Project/Art/Icons/`에 import

## 모델 / LoRA 구성

- UNet: `z_image_turbo_bf16.safetensors`
- CLIP: `qwen_3_4b.safetensors` (type lumina2)
- VAE: `ae.safetensors`
- LoRA: `pixel_art_style_z_image_turbo.safetensors` strength 0.85

## 튜닝 다이얼

| 노드 | 옵션 | 효과 |
|---|---|---|
| LoraLoader strength | 0.6 → 0.9 | 픽셀 느낌 강도 |
| KSampler steps | 8 → 12 | 디테일 정밀도 (생성 시간 ↑) |
| KSampler cfg | 1.0 → 2.5 | 프롬프트 충실도 |
| KSampler seed | 변경 | 같은 프롬프트로 다른 결과 |
| KSampler denoise (img2img만) | 0.3 → 0.95 | 레퍼런스 영향력 (낮을수록 원본 유지) |
| EmptyLatentImage / ImageScale 크기 | 512×512 기본 | 768×768 권장 (메모리 여유 있으면) |

## 프롬프트 템플릿

```
Pixel art game icon style. a single < ITEM HERE >,
isolated, centered, plain white background,
clean black outline, simple flat shading,
muted vintage colors with subtle red and pink accents,
no gradient, no anti-aliasing, sharp pixel edges,
game inventory icon
```

`< ITEM HERE >` 예시:
- `red health potion bottle`
- `golden brass key`
- `blue sapphire gem`
- `leather bound spellbook`
- `roasted chicken leg`
- `iron sword with leather grip`
- `flower crown made of red camellia`
- `wooden tea cup with steam`

## Negative prompt (기본값)

```
realistic, 3d render, photo, smooth shading, anti-aliased,
blurry, gradient, detailed background, noisy background,
text, watermark, low quality, multiple objects, cluster,
deformed, side view, back view
```

## 변경 기록

- 2026-05-17: 초기 작성. AI-202 워크플로 베이스로 게임 아이템 용도 분기.
