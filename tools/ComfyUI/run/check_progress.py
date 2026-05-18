"""ComfyUI 모델 다운로드 진척 + 예상 잔여시간"""
import io
import os
import socket
import sys
from pathlib import Path

# Windows 콘솔(CP949) 한글 깨짐 방지 — UTF-8 강제
try:
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
except Exception:
    pass

ROOT = Path(r"C:\SSAFY\S14P31C201\tools\ComfyUI\models")

ITEMS = [
    ("SD1.5",      ROOT / "checkpoints"      / "v1-5-pruned-emaonly-fp16.safetensors", 2_132_696_762),
    ("pixel-lora", ROOT / "loras"            / "pixel_art_style_z_image_turbo.safetensors", 170_128_328),
    ("ae-vae",     ROOT / "vae"              / "ae.safetensors", 335_304_388),
    ("qwen",       ROOT / "text_encoders"    / "qwen_3_4b.safetensors", 8_044_982_048),
    ("zit-diff",   ROOT / "diffusion_models" / "z_image_turbo_bf16.safetensors", 12_309_866_400),
]

def fmt_mb(b): return f"{b/1024/1024:>9,.1f}"

print()
print("=== ComfyUI 모델 다운로드 진척 ===")
tc = tt = 0
for name, path, target in ITEMS:
    cur = path.stat().st_size if path.exists() else 0
    tc += cur
    tt += target
    if cur >= target:
        status = "DONE"
    elif cur == 0:
        status = "-"
    else:
        status = f"{cur*100/target:5.1f}%"
    print(f"  {name:<12} {fmt_mb(cur)} / {fmt_mb(target)} MB  {status}")

print("  " + "-" * 50)
print(f"  {'TOTAL':<12} {fmt_mb(tc)} / {fmt_mb(tt)} MB  {tc*100/tt:5.1f}%")

remaining = tt - tc
print()
print("=== 잔여 ===")
print(f"  남은 용량: {remaining/1024**3:.2f} GB")
if remaining > 0:
    # 관측 평균 ~670 KB/s
    sec = remaining / 1024 / 670
    print(f"  670 KB/s 기준 예상: {sec/60:.0f}분 ({sec/3600:.1f}시간)")
else:
    print("  ✅ 전부 완료")

print()
def is_listening(port):
    try:
        s = socket.create_connection(("127.0.0.1", port), timeout=1)
        s.close()
        return True
    except Exception:
        return False

if is_listening(8188):
    print("ComfyUI: 실행 중 (http://127.0.0.1:8188)")
else:
    print("ComfyUI: 중지됨")
print()
