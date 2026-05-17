@echo off
REM 48x48 아이템 아이콘 변환 (32 colors, 4x display preview)
REM 사용:  pixelize-item48.cmd <파일경로_또는_폴더>
REM 예:    pixelize-item48.cmd C:\path\to\sword.png
REM        pixelize-item48.cmd C:\SSAFY\S14P31C201\tools\ComfyUI\output

chcp 65001 >NUL
if "%~1"=="" (
  echo 사용: pixelize-item48.cmd ^<파일경로_또는_폴더^>
  exit /b 1
)

"C:\SSAFY\S14P31C201\tools\ComfyUI\.venv_local\Scripts\python.exe" ^
  "C:\SSAFY\S14P31C201\tools\ComfyUI\run\pixelize.py" ^
  --input "%~1" --size 48 --colors 32 --display-scale 4
pause
