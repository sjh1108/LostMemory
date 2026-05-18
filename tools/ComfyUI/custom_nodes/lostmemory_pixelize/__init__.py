"""LostMemory Pixelize — ComfyUI 커스텀 노드.

게임 32색 표준 팔레트로 양자화 + nearest 다운스케일 + 흰 배경 → 투명 변환.
워크플로 안에서 한 번에 처리.
"""
from __future__ import annotations

from pathlib import Path

import numpy as np
import torch
from PIL import Image


GAME_PALETTE_PATH = Path(__file__).resolve().parents[2] / "run" / "game_palette" / "game-palette-32colors.png"
GAME_PALETTE_PATH_16 = Path(__file__).resolve().parents[2] / "run" / "game_palette" / "game-palette-16colors.png"


def _tensor_to_pil(t: torch.Tensor) -> Image.Image:
    """ComfyUI IMAGE tensor (B,H,W,C float 0~1) → PIL RGB (배치 첫 장만)."""
    img = t[0].detach().cpu().numpy()
    img = np.clip(img * 255.0, 0, 255).astype(np.uint8)
    return Image.fromarray(img, mode="RGB")


def _pil_to_tensor(img: Image.Image) -> torch.Tensor:
    """PIL RGBA → ComfyUI IMAGE tensor (1,H,W,3) — 알파는 별도 MASK로."""
    if img.mode == "RGBA":
        rgb = img.convert("RGB")
    else:
        rgb = img.convert("RGB")
    arr = np.asarray(rgb).astype(np.float32) / 255.0
    return torch.from_numpy(arr).unsqueeze(0)


def _pil_alpha_to_mask(img: Image.Image, size: tuple[int, int]) -> torch.Tensor:
    """PIL RGBA의 alpha 채널 → ComfyUI MASK tensor (1,H,W) 0~1."""
    if img.mode == "RGBA":
        a = np.asarray(img.split()[-1]).astype(np.float32) / 255.0
    else:
        a = np.ones(size[::-1], dtype=np.float32)
    return torch.from_numpy(a).unsqueeze(0)


def _remove_white_bg(img: Image.Image, threshold: int) -> Image.Image:
    """흰 배경에 가까운 픽셀의 alpha를 0으로. 입력은 RGBA여야 함."""
    arr = np.asarray(img).astype(np.int32)
    rgb = arr[..., :3]
    a = arr[..., 3]
    diff = rgb - np.array([255, 255, 255], dtype=np.int32)
    dist_sq = (diff * diff).sum(axis=-1)
    mask = dist_sq <= threshold * threshold * 3
    a = np.where(mask, 0, a).astype(np.uint8)
    out = arr.astype(np.uint8).copy()
    out[..., 3] = a
    return Image.fromarray(out, mode="RGBA")


def _load_palette_source(path: Path) -> Image.Image | None:
    if not path.exists():
        return None
    p = Image.open(path)
    if p.mode != "P":
        p = p.convert("RGB").quantize(colors=32)
    return p


_PALETTE_32 = _load_palette_source(GAME_PALETTE_PATH)
_PALETTE_16 = _load_palette_source(GAME_PALETTE_PATH_16)


class LostMemoryPixelize:
    """입력 이미지를 nearest 다운스케일 + 게임 팔레트 양자화 + 투명 배경 처리."""

    PRESETS = {
        "game-item32": dict(size=32, palette="game-32", transparent_bg=True),
        "game-item48": dict(size=48, palette="game-32", transparent_bg=True),
        "game-item16": dict(size=16, palette="game-16", transparent_bg=True),
        "game-item64": dict(size=64, palette="game-32", transparent_bg=True),
        "custom":      dict(size=64, palette="adaptive", transparent_bg=False),
    }

    @classmethod
    def INPUT_TYPES(cls):
        return {
            "required": {
                "image": ("IMAGE",),
                "preset": (list(cls.PRESETS.keys()), {"default": "game-item48"}),
            },
            "optional": {
                "size_override": ("INT", {"default": 0, "min": 0, "max": 1024, "step": 1, "tooltip": "0이면 preset 사용"}),
                "palette_override": (["preset", "game-32", "game-16", "adaptive"], {"default": "preset"}),
                "adaptive_colors": ("INT", {"default": 32, "min": 2, "max": 256, "step": 1, "tooltip": "adaptive 팔레트일 때 색 개수"}),
                "transparent_bg": (["preset", "yes", "no"], {"default": "preset"}),
                "bg_threshold": ("INT", {"default": 30, "min": 0, "max": 255, "step": 1, "tooltip": "흰배경 판정 거리"}),
            },
        }

    RETURN_TYPES = ("IMAGE", "MASK")
    RETURN_NAMES = ("image", "mask")
    FUNCTION = "pixelize"
    CATEGORY = "LostMemory"

    def pixelize(self, image: torch.Tensor, preset: str,
                 size_override: int = 0, palette_override: str = "preset",
                 adaptive_colors: int = 32, transparent_bg: str = "preset", bg_threshold: int = 30):
        cfg = dict(self.PRESETS[preset])
        size = size_override if size_override > 0 else cfg["size"]
        palette_name = cfg["palette"] if palette_override == "preset" else palette_override
        do_transparent = cfg["transparent_bg"] if transparent_bg == "preset" else (transparent_bg == "yes")

        # IMAGE tensor → PIL RGB
        rgb = _tensor_to_pil(image)
        rgba = rgb.convert("RGBA")

        # 1. 흰 배경 → 투명
        if do_transparent:
            rgba = _remove_white_bg(rgba, bg_threshold)

        # 2. nearest 다운스케일
        rgba = rgba.resize((size, size), Image.Resampling.NEAREST)

        # 3. 색상 양자화 (RGB만)
        r, g, b, a = rgba.split()
        rgb_part = Image.merge("RGB", (r, g, b))
        if palette_name == "game-32" and _PALETTE_32 is not None:
            q = rgb_part.quantize(palette=_PALETTE_32, dither=Image.Dither.NONE)
        elif palette_name == "game-16" and _PALETTE_16 is not None:
            q = rgb_part.quantize(palette=_PALETTE_16, dither=Image.Dither.NONE)
        else:
            q = rgb_part.quantize(colors=adaptive_colors, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
        q_rgb = q.convert("RGB")

        result = Image.merge("RGBA", (*q_rgb.split(), a))
        out_image = _pil_to_tensor(result)
        out_mask = _pil_alpha_to_mask(result, (size, size))
        return (out_image, out_mask)


class LostMemoryPixelizeUpscale:
    """양자화된 작은 이미지를 nearest 업스케일 (디스플레이/미리보기용)."""

    @classmethod
    def INPUT_TYPES(cls):
        return {
            "required": {
                "image": ("IMAGE",),
                "scale": ("INT", {"default": 4, "min": 1, "max": 32, "step": 1}),
            }
        }

    RETURN_TYPES = ("IMAGE",)
    FUNCTION = "upscale"
    CATEGORY = "LostMemory"

    def upscale(self, image: torch.Tensor, scale: int):
        rgb = _tensor_to_pil(image)
        w, h = rgb.size
        up = rgb.resize((w * scale, h * scale), Image.Resampling.NEAREST)
        return (_pil_to_tensor(up),)


NODE_CLASS_MAPPINGS = {
    "LostMemoryPixelize": LostMemoryPixelize,
    "LostMemoryPixelizeUpscale": LostMemoryPixelizeUpscale,
}

NODE_DISPLAY_NAME_MAPPINGS = {
    "LostMemoryPixelize": "LostMemory Pixelize (game palette)",
    "LostMemoryPixelizeUpscale": "LostMemory Pixelize Upscale (nearest)",
}
