"""LostMemory Background Remove — ComfyUI 커스텀 노드 (rembg 백엔드).

생성 이미지의 배경을 자동 투명화해 게임 sprite로 바로 사용 가능한 RGBA PNG로.
모델 6종(u2net / u2net_human_seg / u2netp / isnet-general-use / isnet-anime / silueta) 중 선택.

함께 제공: LostMemorySaveImageRGBA — IMAGE + MASK를 alpha 합성해 PNG로 저장.
"""
from __future__ import annotations

import json
from pathlib import Path

import numpy as np
import torch
from PIL import Image, PngImagePlugin

try:
    import folder_paths  # ComfyUI 빌트인 (output 디렉토리 위치 등)
except ImportError:
    folder_paths = None


_SESSIONS: dict[str, object] = {}


def _get_session(model: str):
    """rembg 세션 캐시 (모델 로딩 비용 절감)."""
    from rembg import new_session
    if model not in _SESSIONS:
        _SESSIONS[model] = new_session(model)
    return _SESSIONS[model]


def _tensor_to_pil_rgb(t: torch.Tensor) -> Image.Image:
    """ComfyUI IMAGE (B,H,W,C float 0~1) → PIL RGB (배치 첫 장)."""
    img = t[0].detach().cpu().numpy()
    img = np.clip(img * 255.0, 0, 255).astype(np.uint8)
    return Image.fromarray(img, mode="RGB")


def _pil_rgba_to_image_tensor(img: Image.Image) -> torch.Tensor:
    """PIL RGBA → ComfyUI IMAGE tensor (1,H,W,3). 알파는 별도 MASK로."""
    rgb = img.convert("RGB")
    arr = np.asarray(rgb).astype(np.float32) / 255.0
    return torch.from_numpy(arr).unsqueeze(0)


def _pil_rgba_to_mask_tensor(img: Image.Image) -> torch.Tensor:
    """PIL RGBA의 alpha → ComfyUI MASK tensor (1,H,W) 0~1."""
    if img.mode != "RGBA":
        img = img.convert("RGBA")
    a = np.asarray(img.split()[-1]).astype(np.float32) / 255.0
    return torch.from_numpy(a).unsqueeze(0)


def _image_mask_to_rgba(image: torch.Tensor, mask: torch.Tensor) -> Image.Image:
    """ComfyUI IMAGE(RGB) + MASK(alpha) → PIL RGBA. 배치 첫 장만."""
    rgb_arr = np.clip(image[0].detach().cpu().numpy() * 255.0, 0, 255).astype(np.uint8)
    if mask.ndim == 3:
        a_arr = np.clip(mask[0].detach().cpu().numpy() * 255.0, 0, 255).astype(np.uint8)
    else:
        a_arr = np.clip(mask.detach().cpu().numpy() * 255.0, 0, 255).astype(np.uint8)
    rgba = np.dstack([rgb_arr, a_arr])
    return Image.fromarray(rgba, mode="RGBA")


class LostMemoryBgRemove:
    """배경 자동 투명화 (rembg) — IMAGE → (IMAGE, MASK)."""

    MODELS = [
        "u2net",                # 범용 (170MB, 검증된 시작점)
        "u2net_human_seg",      # 인물 특화
        "u2netp",               # 경량 (4.7MB)
        "isnet-general-use",    # 범용 신모델 (170MB)
        "isnet-anime",          # 애니/일러스트 캐릭터
        "silueta",              # 단순/객체 (43MB)
    ]

    @classmethod
    def INPUT_TYPES(cls):
        return {
            "required": {
                "image": ("IMAGE",),
                "model": (cls.MODELS, {"default": "u2net"}),
            },
            "optional": {
                "alpha_matting": (["no", "yes"], {"default": "no", "tooltip": "alpha matting 후처리 (느림, 윤곽 더 정밀)"}),
                "alpha_threshold_fg": ("INT", {"default": 240, "min": 0, "max": 255, "step": 1, "tooltip": "matting 전경 임계"}),
                "alpha_threshold_bg": ("INT", {"default": 10, "min": 0, "max": 255, "step": 1, "tooltip": "matting 배경 임계"}),
                "erode_size": ("INT", {"default": 10, "min": 0, "max": 100, "step": 1, "tooltip": "matting erosion 크기"}),
            },
        }

    RETURN_TYPES = ("IMAGE", "MASK")
    RETURN_NAMES = ("image", "mask")
    FUNCTION = "remove_bg"
    CATEGORY = "LostMemory"

    def remove_bg(self, image: torch.Tensor, model: str,
                  alpha_matting: str = "no",
                  alpha_threshold_fg: int = 240,
                  alpha_threshold_bg: int = 10,
                  erode_size: int = 10):
        from rembg import remove
        rgb = _tensor_to_pil_rgb(image)
        session = _get_session(model)
        use_matting = (alpha_matting == "yes")
        result = remove(
            rgb,
            session=session,
            alpha_matting=use_matting,
            alpha_matting_foreground_threshold=alpha_threshold_fg,
            alpha_matting_background_threshold=alpha_threshold_bg,
            alpha_matting_erode_size=erode_size,
        )
        return (_pil_rgba_to_image_tensor(result), _pil_rgba_to_mask_tensor(result))


class LostMemorySaveImageRGBA:
    """IMAGE + MASK → RGBA PNG (alpha 채널 보존). ComfyUI 빌트인 SaveImage가 alpha 미지원이라 별도 제공."""

    def __init__(self):
        self.output_dir = folder_paths.get_output_directory() if folder_paths else "output"
        self.type = "output"

    @classmethod
    def INPUT_TYPES(cls):
        return {
            "required": {
                "image": ("IMAGE",),
                "mask": ("MASK",),
                "filename_prefix": ("STRING", {"default": "LostMemory_BgRemove"}),
            },
            "hidden": {"prompt": "PROMPT", "extra_pnginfo": "EXTRA_PNGINFO"},
        }

    RETURN_TYPES = ()
    FUNCTION = "save"
    OUTPUT_NODE = True
    CATEGORY = "LostMemory"

    def save(self, image: torch.Tensor, mask: torch.Tensor, filename_prefix: str,
             prompt=None, extra_pnginfo=None):
        rgba = _image_mask_to_rgba(image, mask)
        if folder_paths:
            full_output_folder, filename, counter, subfolder, _ = folder_paths.get_save_image_path(
                filename_prefix, self.output_dir, rgba.width, rgba.height
            )
        else:
            full_output_folder = Path(self.output_dir)
            full_output_folder.mkdir(parents=True, exist_ok=True)
            filename = filename_prefix
            counter = 1
            subfolder = ""

        pnginfo = PngImagePlugin.PngInfo()
        if prompt is not None:
            pnginfo.add_text("prompt", json.dumps(prompt))
        if extra_pnginfo is not None:
            for k, v in extra_pnginfo.items():
                pnginfo.add_text(k, json.dumps(v))

        file = f"{filename}_{counter:05}_.png"
        out_path = Path(full_output_folder) / file
        rgba.save(out_path, format="PNG", pnginfo=pnginfo, compress_level=4)

        return {"ui": {"images": [{"filename": file, "subfolder": subfolder, "type": self.type}]}}


NODE_CLASS_MAPPINGS = {
    "LostMemoryBgRemove": LostMemoryBgRemove,
    "LostMemorySaveImageRGBA": LostMemorySaveImageRGBA,
}

NODE_DISPLAY_NAME_MAPPINGS = {
    "LostMemoryBgRemove": "LostMemory Background Remove (rembg)",
    "LostMemorySaveImageRGBA": "LostMemory Save Image (RGBA)",
}
