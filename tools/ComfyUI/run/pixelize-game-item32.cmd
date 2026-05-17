@echo off
REM 32x32 게임 아이콘 (LostMemory 표준 팔레트 + 투명 배경)
REM 사용:  pixelize-game-item32.cmd <PNG 또는 폴더>

chcp 65001 >NUL
if "%~1"=="" (
  echo 사용: pixelize-game-item32.cmd ^<PNG파일 또는 폴더^>
  pause
  exit /b 1
)

"C:\SSAFY\S14P31C201\tools\ComfyUI\.venv_local\Scripts\python.exe" ^
  "C:\SSAFY\S14P31C201\tools\ComfyUI\run\pixelize.py" ^
  --input "%~1" --preset game-item32
pause
