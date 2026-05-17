"""게임 32x32 sprite들에서 공통 팔레트 추출 → 변환용 .png 팔레트 파일 생성"""
import io
import sys
from collections import Counter
from pathlib import Path

try:
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
except Exception:
    pass

from PIL import Image

ICONS = Path(r"C:\SSAFY\S14P31C201\client\LostMemory\Assets\_Project\Art\Icons")
OUT_DIR = Path(r"C:\SSAFY\S14P31C201\tools\ComfyUI\run\game_palette")
OUT_DIR.mkdir(exist_ok=True, parents=True)

# 32x32 + 48x48 RGBA 아이콘만 (로고/UI 제외)
def is_target(p: Path) -> bool:
    try:
        img = Image.open(p)
        w, h = img.size
        return (w, h) in [(32, 32), (48, 48)] and img.mode in ("RGBA", "P")
    except Exception:
        return False

files = [p for p in ICONS.glob("*.png") if is_target(p)]
print(f"분석 대상 파일: {len(files)}개")

# 모든 visible 픽셀 색 수집
color_counter = Counter()
for p in files:
    img = Image.open(p).convert("RGBA")
    for r, g, b, a in img.getdata():
        if a > 16:  # 거의 투명한 픽셀 무시
            color_counter[(r, g, b)] += 1

print(f"전체 unique RGB 색상: {len(color_counter)}")
print(f"전체 visible 픽셀: {sum(color_counter.values())}")

# 가장 흔한 32색을 게임 표준 팔레트로
TARGET_COLORS = 32
top_colors = [c for c, _ in color_counter.most_common(TARGET_COLORS)]
print(f"\n=== Top {TARGET_COLORS} colors (game standard palette) ===")
for i, (r, g, b) in enumerate(top_colors):
    pct = color_counter[(r,g,b)] * 100 / sum(color_counter.values())
    print(f"  {i:>2}  #{r:02x}{g:02x}{b:02x}  RGB({r:>3},{g:>3},{b:>3})  {pct:.2f}%")

# 팔레트 이미지 만들기 (PIL quantize용)
# 1픽셀씩 32색 늘어놓은 가로 띠. quantize의 palette 인자로 사용 가능.
palette_img = Image.new("P", (TARGET_COLORS, 1))
palette_flat = []
for r, g, b in top_colors:
    palette_flat.extend([r, g, b])
# 256색 슬롯 다 채워야 함 (나머지는 0,0,0)
palette_flat.extend([0, 0, 0] * (256 - TARGET_COLORS))
palette_img.putpalette(palette_flat)

palette_out = OUT_DIR / f"game-palette-{TARGET_COLORS}colors.png"
palette_img.save(palette_out)
print(f"\n팔레트 PNG 저장: {palette_out}")

# 시각 미리보기: 색 사각형으로 한 줄로 늘어놓은 그림
PREVIEW_SIZE = 32
preview = Image.new("RGB", (PREVIEW_SIZE * TARGET_COLORS, PREVIEW_SIZE), (255, 255, 255))
for i, (r, g, b) in enumerate(top_colors):
    swatch = Image.new("RGB", (PREVIEW_SIZE, PREVIEW_SIZE), (r, g, b))
    preview.paste(swatch, (i * PREVIEW_SIZE, 0))
preview_out = OUT_DIR / f"game-palette-{TARGET_COLORS}colors-preview.png"
preview.save(preview_out)
print(f"미리보기 저장: {preview_out}")

# 더 작은 팔레트(16색)도
TARGET_COLORS_SMALL = 16
top_small = [c for c, _ in color_counter.most_common(TARGET_COLORS_SMALL)]
palette_img_small = Image.new("P", (TARGET_COLORS_SMALL, 1))
flat_small = []
for r, g, b in top_small:
    flat_small.extend([r, g, b])
flat_small.extend([0, 0, 0] * (256 - TARGET_COLORS_SMALL))
palette_img_small.putpalette(flat_small)
palette_out_small = OUT_DIR / f"game-palette-{TARGET_COLORS_SMALL}colors.png"
palette_img_small.save(palette_out_small)
print(f"\n16색 팔레트도 저장: {palette_out_small}")
