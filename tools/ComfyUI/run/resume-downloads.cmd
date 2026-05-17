@echo off
REM Z-Image-Turbo set resume downloader.
REM Re-run anytime — curl --continue-at picks up from where partial files left off.
REM Safe to interrupt (Ctrl+C) and re-run.

setlocal
set MODELS=C:\SSAFY\S14P31C201\tools\ComfyUI\models
set LOG=C:\SSAFY\S14P31C201\tools\ComfyUI\run\curl-zimage.log

echo === Resume started: %DATE% %TIME% ===
echo === Resume started: %DATE% %TIME% === >> "%LOG%"

echo.
echo [1/4] pixel_art_style_z_image_turbo (170 MB)
curl.exe -L --retry 30 --retry-all-errors --retry-delay 10 --continue-at - ^
  -o "%MODELS%\loras\pixel_art_style_z_image_turbo.safetensors" ^
  "https://huggingface.co/tarn59/pixel_art_style_lora_z_image_turbo/resolve/main/pixel_art_style_z_image_turbo.safetensors"

echo.
echo [2/4] ae.safetensors (335 MB)
curl.exe -L --retry 30 --retry-all-errors --retry-delay 10 --continue-at - ^
  -o "%MODELS%\vae\ae.safetensors" ^
  "https://huggingface.co/Comfy-Org/z_image_turbo/resolve/main/split_files/vae/ae.safetensors"

echo.
echo [3/4] qwen_3_4b.safetensors (8.0 GB)
curl.exe -L --retry 30 --retry-all-errors --retry-delay 10 --continue-at - ^
  -o "%MODELS%\text_encoders\qwen_3_4b.safetensors" ^
  "https://huggingface.co/Comfy-Org/z_image_turbo/resolve/main/split_files/text_encoders/qwen_3_4b.safetensors"

echo.
echo [4/4] z_image_turbo_bf16.safetensors (12.3 GB)
curl.exe -L --retry 30 --retry-all-errors --retry-delay 10 --continue-at - ^
  -o "%MODELS%\diffusion_models\z_image_turbo_bf16.safetensors" ^
  "https://huggingface.co/Comfy-Org/z_image_turbo/resolve/main/split_files/diffusion_models/z_image_turbo_bf16.safetensors"

echo.
echo === Done: %DATE% %TIME% ===
echo === Done: %DATE% %TIME% === >> "%LOG%"
endlocal
