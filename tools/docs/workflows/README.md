# LostMemory ComfyUI 워크플로 모음

게임 자산용 ComfyUI 워크플로 JSON 모음. ComfyUI UI에 import해서 바로 사용.

## 파일

### 아이템 생성 (KSampler prompt 기반)

| 파일 | 용도 | 입력 | 출력 |
|---|---|---|---|
| `LostMemory_Item_TXT2IMG.json` | 텍스트만으로 아이템 생성 + 픽셀 양자화 + **배경 투명화** 한 번에 | 프롬프트 | PNG 3장 (원본 / 픽셀 48×48 / RGBA) |
| `LostMemory_Item_IMG2IMG.json` | 레퍼런스 이미지 변형 + 픽셀 양자화 + **배경 투명화** | 이미지 + 프롬프트 | PNG 3장 (원본 / 픽셀 48×48 / RGBA) |

### 외부 일러스트 → 픽셀 아트 변환 (캐릭터/미소녀는 직접 그린 일러스트 입력)

| 파일 | 용도 | 입력 | 출력 |
|---|---|---|---|
| `LostMemory_Illust_To_Pixel.json` | 단순 다운스케일 (KSampler 없음) | 일러스트 1장 | 16×16 도트 + RGBA 배경 투명 |
| `LostMemory_Illust_To_PixelArt.json` | 일러스트 → 일반 픽셀 아트 그림체 (LoRA 스타일 트랜스퍼, denoise 0.55) | 일러스트 1장 | PNG 3장 (원본 / 16×16 / RGBA) |
| **`LostMemory_OurPixelTone.json`** | 단일 입력 → 두 갈래(denoise 0.55 / 0.4) 동시 변환. LostMemory 5명 reference 톤 강제 | 일러스트 또는 픽셀 아트 1장 | **PNG 6장** (illust 갈래 3장 + pixel 갈래 3장) |
| `LostMemory_PixelArt_To_FrontView.json` | 임의 view/pose 픽셀 아트 → 정면 전신 sprite 재구성 (denoise 0.7) | 픽셀 아트 1장 | PNG 3장 (원본 / 16×16 / RGBA) |
| **`LostMemory_BustToFullChibi.json`** | **3단계 chain**: 흉상 → 전신 → 2등신 chibi → 픽셀 (KSampler 2개, LoRA 0.4/0.7) | 흉상 일러스트 1장 | **PNG 4장** (전신 / 2등신 / 16×16 / RGBA) |

### 후처리

| 파일 | 용도 | 입력 | 출력 |
|---|---|---|---|
| `LostMemory_BgRemove_Standalone.json` | 기존 PNG 한 장을 배경 투명화 (단독) | 이미지 1장 | RGBA PNG |

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

## 배경 투명화 (BgRemove) 워크플로 — `LostMemory_BgRemove_Standalone.json`

생성 이미지의 배경을 자동 투명화해 게임 sprite로 바로 사용 (RGBA PNG, alpha 채널).

### 사용

1. ComfyUI input/ 폴더에 변환할 이미지 두기 (또는 LoadImage upload 사용)
2. 워크플로 로드 → 노드 1 (LoadImage)에서 파일 선택
3. (선택) 노드 2 (LostMemoryBgRemove)의 `model` 변경
4. Queue Prompt
5. 결과 `tools/ComfyUI/output/LostMemory_BgRemove_*.png` (alpha 채널 포함)

### 모델 선택 가이드

| 모델 | 특화 | 다운로드 크기 | 추론 속도 |
|---|---|---|---|
| `u2net` (기본) | 범용 | ~170MB | 빠름 |
| `u2net_human_seg` | 인물 | ~170MB | 빠름 |
| `u2netp` | 경량 범용 | ~4.7MB | 매우 빠름 |
| `isnet-general-use` | 범용 신모델 | ~170MB | 빠름 |
| `isnet-anime` | 애니/일러스트 캐릭터 | ~170MB | 빠름 |
| `silueta` | 단순 객체 | ~43MB | 빠름 |

- 첫 사용 시 모델 자동 다운로드 (`~/.u2net/`)
- 미소녀/캐릭터 (S14P31C201-589, 590) → `isnet-anime` 권장
- 아이템 흰배경 → `u2net` 또는 더 단순한 `LostMemoryPixelize`의 흰배경 변환으로 충분

### alpha matting 후처리 (선택)

기본 출력 윤곽이 거칠면 노드 2의 `alpha_matting`을 `yes`로 → 더 정밀, 단 느려짐.
임계값: `alpha_threshold_fg`(전경), `alpha_threshold_bg`(배경), `erode_size`(erosion 강도).

### 검증 결과 (2026-05-18, AI-310 아이템 결과 4장 + AutoPix 1장)

| 입력 | 투명 영역 비율 | 비고 |
|---|---|---|
| LostMemory_Item_00001 | 86.8% | 깔끔 |
| LostMemory_Item_00002 | 74.5% | 깔끔 |
| LostMemory_Item_00003 | 1.5% | u2net이 복잡 배경 처리 못 함 → `isnet-anime` 또는 alpha_matting 시도 |
| LostMemory_Item_00004 | 58.7% | 깔끔 |
| AutoPix_Test_00001 | 75.3% | 깔끔 |

### 기존 아이템 워크플로 통합

`LostMemory_Item_TXT2IMG.json` / `LostMemory_Item_IMG2IMG.json`은 VAEDecode 출력을 세 갈래로 분기해 한 번의 Queue Prompt로 PNG 3장을 동시에 생성합니다.

```
VAEDecode → SaveImage                  → output/LostMemory_Item_*.png            (원본 512×512)
         → LostMemoryPixelize → SaveImage     → output/LostMemory_Item_pixel_*.png      (게임 톤 48×48)
         → LostMemoryBgRemove → SaveImageRGBA → output/LostMemory_Item_bgremove_*.png   (RGBA 512×512, 알파 배경)
```

배경 투명화가 필요 없는 경우 노드 32 (BgRemove)와 33 (SaveImageRGBA)을 우클릭 → Bypass(`Ctrl+B`)로 해당 갈래만 끔.

## 외부 일러스트 → 픽셀 아트 변환 3종 ([S14P31C201-624](https://ssafy.atlassian.net/browse/S14P31C201-624))

미소녀/캐릭터 일러스트는 외부에서 직접 그려 입력하는 흐름이라 ComfyUI 측은 **순수 변환 파이프라인**만 담당합니다. 용도별 3종:

### 1. `LostMemory_Illust_To_Pixel.json` — 단순 다운스케일

```
LoadImage → LostMemoryPixelize(16×16, adaptive 16색) → SaveImage
         → LostMemoryBgRemove(isnet-anime)            → SaveImageRGBA
```

KSampler 없음. 원본 일러스트가 변형 없이 그대로 16×16 도트로 다운스케일됩니다. 일러스트가 이미 픽셀 아트 톤일 때 사용.

### 2. `LostMemory_Illust_To_PixelArt.json` — 스타일 트랜스퍼

```
LoadImage → ImageScale → VAEEncode
   → KSampler (pixel_art LoRA 1.0, denoise 0.55, pixel art prompt) → VAEDecode
   ├─ SaveImage                                          → 픽셀 아트 일러스트 512×512
   ├─ LostMemoryPixelize(16, adaptive 16) → SaveImage    → 16×16 도트 sprite
   └─ LostMemoryBgRemove(isnet-anime)     → SaveImageRGBA → RGBA 배경 투명
```

매끄러운 anime 일러스트를 픽셀 아트 그림체로 재생성. denoise 0.55에서 원본 윤곽은 유지되면서 톤만 픽셀화. 0.4~0.6 사이로 조정.

### 3. `LostMemory_PixelArt_To_FrontView.json` — 정면 전신 재구성

PixelArt 워크플로와 동일 구조 + denoise 0.7 + 정면/전신/T-pose prompt 강제. 임의 view/pose 픽셀 아트를 LostMemory 톤 정면 전신 sprite로 변환. character reference sheet 컨셉 학습량이 적어 seed 변경하며 3~5회 시도 권장.

### 폐기 (MR !303 → 본 MR로 정리)

`LostMemory_Bishoujo_TXT2IMG/IMG2IMG`, `LostMemory_Character_TXT2IMG/IMG2IMG` (총 4종) 폐기. 캐릭터/미소녀 KSampler prompt 생성은 외부 일러스트 흐름과 맞지 않음. 추가로 시도했던 `LostMemory_Bishoujo_Chibi_TXT2IMG/IMG2IMG` 2종도 chibi prompt 한계(거대 얼굴 결과 등)로 폐기.

## 변경 기록

- 2026-05-18: `[AI]` 외부 일러스트 → 픽셀 아트 변환 3종 추가, 미소녀/캐릭터 KSampler 생성 4종 폐기. ([S14P31C201-624](https://ssafy.atlassian.net/browse/S14P31C201-624))
- 2026-05-18: `[AI][313]` BgRemove standalone 워크플로 + `lostmemory_bgremove` 커스텀 노드 (rembg 백엔드) 추가. ([S14P31C201-595](https://ssafy.atlassian.net/browse/S14P31C201-595))
- 2026-05-17: 초기 작성. AI-202 워크플로 베이스로 게임 아이템 용도 분기.
