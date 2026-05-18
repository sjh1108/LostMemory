"""ComfyUI 생성물 → 게임 픽셀 아트 후처리

사용:
  # 게임 아이콘 표준 (32x32, game palette, 투명배경)
  python pixelize.py --input <png> --preset game-item32

  # 특수 아이템 (48x48, game palette, 투명배경)
  python pixelize.py --input <png> --preset game-item48

  # 직접 옵션
  python pixelize.py --input <png> --size 64 --colors 24 --transparent-bg

  # 게임 팔레트 강제
  python pixelize.py --input <png> --palette-ref game_palette/game-palette-32colors.png --transparent-bg
"""
import argparse
import io
import sys
from pathlib import Path

try:
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
except Exception:
    pass

try:
    from PIL import Image
except ImportError:
    print("[ERROR] Pillow가 없습니다. venv에서 실행하세요.")
    sys.exit(1)


GAME_PALETTE_32 = Path(r"C:\SSAFY\S14P31C201\tools\ComfyUI\run\game_palette\game-palette-32colors.png")
GAME_PALETTE_16 = Path(r"C:\SSAFY\S14P31C201\tools\ComfyUI\run\game_palette\game-palette-16colors.png")

PRESETS = {
    "game-item32":  dict(size=32, colors=32, display_scale=8, palette_ref=GAME_PALETTE_32, transparent_bg=True),
    "game-item48":  dict(size=48, colors=32, display_scale=4, palette_ref=GAME_PALETTE_32, transparent_bg=True),
    "game-item16":  dict(size=16, colors=16, display_scale=8, palette_ref=GAME_PALETTE_16, transparent_bg=True),
    "concept":      dict(size=64, colors=24, display_scale=4, palette_ref=None,            transparent_bg=False),
}


def load_palette_image(path: Path) -> Image.Image:
    """팔레트 PNG 또는 일반 RGB 이미지에서 P-mode palette source 생성"""
    p = Image.open(path)
    if p.mode == "P":
        return p
    # 일반 이미지에서 quantize로 적응형 팔레트 만들기
    return p.convert("RGB").quantize(colors=32, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)


def remove_bg_alpha(img: Image.Image, bg_rgb=(255, 255, 255), threshold: int = 30) -> Image.Image:
    """흰색에 가까운 픽셀의 알파를 0으로. 결과는 RGBA."""
    img = img.convert("RGBA")
    pixels = img.load()
    w, h = img.size
    br, bg, bb = bg_rgb
    t2 = threshold * threshold * 3
    for y in range(h):
        for x in range(w):
            r, g, b, a = pixels[x, y]
            dr, dg, db = r - br, g - bg, b - bb
            if dr*dr + dg*dg + db*db <= t2:
                pixels[x, y] = (r, g, b, 0)
    return img


def pixelize_one(
    src: Path,
    out_dir: Path,
    target_size: int,
    n_colors: int,
    display_scale: int,
    palette_ref: Image.Image | None,
    transparent_bg: bool,
) -> tuple[Path, Path]:
    img = Image.open(src)

    # 1. 알파 결정
    if transparent_bg:
        # 입력이 이미 RGBA면 그 알파 존중하고, 추가로 흰배경 제거
        rgba = img.convert("RGBA")
        rgba = remove_bg_alpha(rgba, bg_rgb=(255, 255, 255), threshold=30)
    else:
        # 흰배경 합성
        if img.mode == "RGBA":
            bg_canvas = Image.new("RGB", img.size, (255, 255, 255))
            bg_canvas.paste(img, mask=img.split()[3])
            rgba = bg_canvas.convert("RGBA")
        else:
            rgba = img.convert("RGB").convert("RGBA")

    # 2. nearest로 다운스케일
    rgba = rgba.resize((target_size, target_size), Image.Resampling.NEAREST)

    # 3. RGB와 alpha 분리, RGB만 quantize
    r, g, b, a = rgba.split()
    rgb = Image.merge("RGB", (r, g, b))

    if palette_ref is not None:
        quantized_p = rgb.quantize(palette=palette_ref, dither=Image.Dither.NONE)
    else:
        quantized_p = rgb.quantize(colors=n_colors, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
    quantized_rgb = quantized_p.convert("RGB")

    # 4. 알파 재결합
    if transparent_bg:
        result = Image.merge("RGBA", (*quantized_rgb.split(), a))
    else:
        result = quantized_rgb.convert("RGBA")

    stem = src.stem
    native_path  = out_dir / f"{stem}_native{target_size}.png"
    display_path = out_dir / f"{stem}_display{target_size * display_scale}.png"
    result.save(native_path, optimize=True)
    result.resize(
        (target_size * display_scale, target_size * display_scale),
        Image.Resampling.NEAREST,
    ).save(display_path, optimize=True)
    return native_path, display_path


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--input", required=True)
    ap.add_argument("--pattern", default="*.png")
    ap.add_argument("--out-dir", default=None)
    ap.add_argument("--preset", choices=list(PRESETS.keys()), default=None,
                    help=f"프리셋 사용 (다른 옵션 무시). 가능: {list(PRESETS.keys())}")
    ap.add_argument("--size", type=int, default=64)
    ap.add_argument("--colors", type=int, default=32)
    ap.add_argument("--display-scale", type=int, default=4)
    ap.add_argument("--palette-ref", default=None)
    ap.add_argument("--transparent-bg", action="store_true", help="흰배경을 투명(alpha=0)으로")
    args = ap.parse_args()

    # 프리셋이 있으면 그 값으로 override
    if args.preset:
        p = PRESETS[args.preset]
        size = p["size"]; colors = p["colors"]; display_scale = p["display_scale"]
        pal_path = p["palette_ref"]; transparent_bg = p["transparent_bg"]
    else:
        size = args.size; colors = args.colors; display_scale = args.display_scale
        pal_path = Path(args.palette_ref) if args.palette_ref else None
        transparent_bg = args.transparent_bg

    palette_ref = None
    if pal_path:
        if not Path(pal_path).exists():
            print(f"[ERROR] palette ref not found: {pal_path}"); sys.exit(1)
        palette_ref = load_palette_image(Path(pal_path))

    input_path = Path(args.input)
    if not input_path.exists():
        print(f"[ERROR] not found: {input_path}"); sys.exit(1)

    if input_path.is_file():
        files = [input_path]
        out_dir = Path(args.out_dir) if args.out_dir else input_path.parent / (input_path.parent.name + "_pixel")
    else:
        files = sorted(input_path.glob(args.pattern))
        out_dir = Path(args.out_dir) if args.out_dir else input_path.parent / (input_path.name + "_pixel")

    out_dir.mkdir(parents=True, exist_ok=True)

    print(f"  preset       : {args.preset or '(custom)'}")
    print(f"  input        : {input_path} (files={len(files)})")
    print(f"  output       : {out_dir}")
    print(f"  size         : {size}px native, {size*display_scale}px display")
    print(f"  colors       : {colors} (or palette-ref)")
    print(f"  palette-ref  : {pal_path or '(adaptive median-cut)'}")
    print(f"  transparent  : {transparent_bg}")
    print()

    for i, f in enumerate(files, 1):
        try:
            nat, disp = pixelize_one(f, out_dir, size, colors, display_scale, palette_ref, transparent_bg)
            print(f"  [{i}/{len(files)}] {f.name} -> {nat.name} + {disp.name}")
        except Exception as e:
            print(f"  [{i}/{len(files)}] {f.name} FAILED: {e}")

    print(f"\n완료. 결과: {out_dir}")


if __name__ == "__main__":
    main()
