"""기존 게임 아이콘 자산 분석 — 사이즈, 팔레트, 색상 수, 투명 채널"""
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

samples = [
    "48.48 물약 체력회복.png",
    "48.48 물약 정신력 회복.png",
    "48.48 커피.png",
    "48.48 폴라로이드 카메라.png",
    "48.48 손수건.png",
    "48.48 티켓1.png",
    "48.48 상패1.png",
    "식품-사탕.png",
    "식품-복숭아.png",
    "식품-마라소스.png",
    "식품-믹스커피 스틱.png",
    "식품-소다맛 아이스크림.png",
    "악세-반지.png",
    "악세-루비 목걸이.png",
    "악세-사파이어 귀걸이.png",
    "장난감-야구공.png",
    "장난감-게임기.png",
    "장난감-곰인형.png",
    "의약품-반창고.png",
    "의약품-수면제.png",
    "전자기기-카메라.png",
    "전자기기-CD플레이어.png",
    "서적-시집.png",
    "서적-추리 소설.png",
    "식물-국화꽃.png",
    "식물-동백꽃.png",
    "문구-볼펜.png",
    "문구-편지.png",
    "기타-덤벨.png",
    "기타-바늘.png",
    "13.13 귀 아이콘.png",
    "lock.png",
    "unlock.png",
    "LostMemory.png",
]

print(f"{'FILE':<42} {'W':>4} {'H':>4} {'MODE':<5} {'COLORS':>7} {'ALPHA':<6}")
print("-" * 80)

global_colors = Counter()
size_buckets = Counter()
mode_buckets = Counter()
for name in samples:
    p = ICONS / name
    if not p.exists():
        print(f"{name:<42}  (not found)")
        continue
    img = Image.open(p)
    w, h = img.size
    mode = img.mode
    has_alpha = mode in ("RGBA", "LA") or (mode == "P" and "transparency" in img.info)
    rgba = img.convert("RGBA")
    pixels = list(rgba.getdata())
    visible = [(r,g,b) for r,g,b,a in pixels if a > 0]
    unique_colors = len(set(visible))
    print(f"{name:<42} {w:>4} {h:>4} {mode:<5} {unique_colors:>7} {('yes' if has_alpha else 'no'):<6}")

    global_colors.update(visible)
    size_buckets[(w, h)] += 1
    mode_buckets[mode] += 1

print()
print(f"=== Size distribution ===")
for size, count in size_buckets.most_common():
    print(f"  {size[0]}x{size[1]}: {count}")

print()
print(f"=== Mode distribution ===")
for mode, count in mode_buckets.most_common():
    print(f"  {mode}: {count}")

print()
print(f"=== Top 20 colors across all sampled assets ===")
for (r, g, b), cnt in global_colors.most_common(20):
    print(f"  RGB({r:>3},{g:>3},{b:>3})  hex=#{r:02x}{g:02x}{b:02x}  count={cnt}")
print()
print(f"Total unique colors in sample set: {len(global_colors)}")
