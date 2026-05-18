@echo off
cd /d "%~dp0.."
".venv_local\Scripts\python.exe" main.py --listen 0.0.0.0 --port 8188 --disable-auto-launch --preview-method auto 1>>run\comfyui-stdout.log 2>>run\comfyui-stderr.log
