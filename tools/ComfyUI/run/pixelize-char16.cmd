@echo off
REM 16x16 캐릭터 스프라이트 변환 (12 colors, 8x display preview)
REM 사용:  pixelize-char16.cmd <파일경로_또는_폴더>
REM 예:    pixelize-char16.cmd C:\path\to\image.png
REM        pixelize-char16.cmd C:\SSAFY\S14P31C201\tools\ComfyUI\output

chcp 65001 >NUL
if "%~1"=="" (
  echo 사용: pixelize-char16.cmd ^<파일경로_또는_폴더^>
  exit /b 1
)

"C:\SSAFY\S14P31C201\tools\ComfyUI\.venv_local\Scripts\python.exe" ^
  "C:\SSAFY\S14P31C201\tools\ComfyUI\run\pixelize.py" ^
  --input "%~1" --size 16 --colors 12 --display-scale 8
pause
