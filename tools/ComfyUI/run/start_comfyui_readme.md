# start_comfyui.cmd 설명

## 목적

`start_comfyui.cmd`는 GPU 데스크탑에서 ComfyUI를 항상 같은 방식으로 실행하기 위한 표준 실행 파일이다.

## 현재 실행 기준

```bat
".venv_local\Scripts\python.exe" main.py --listen 0.0.0.0 --port 8188 --disable-auto-launch --preview-method auto
```

## 자동화하는 것

- ComfyUI 루트 폴더로 이동한다.
- 로컬 가상환경 `.venv_local`의 Python으로 `main.py`를 실행한다.
- `--listen 0.0.0.0`으로 내부망 다른 PC와 백엔드가 접속할 수 있게 한다.
- `--port 8188`로 ComfyUI 포트를 고정한다.
- `--disable-auto-launch`로 서버 실행 시 브라우저 자동 실행을 막는다.
- stdout과 stderr를 `run/comfyui-stdout.log`, `run/comfyui-stderr.log`에 누적한다.
- 이미 8188에서 ComfyUI(`main.py --port 8188`)가 떠 있으면 중복 실행하지 않는다.
- python 프로세스를 호출한 셸과 분리해 띄운다(CI·비대화 셸에서 호출해도 종료되지 않도록).

## 내부 구조

- `start_comfyui.cmd`는 같은 폴더의 `start_comfyui.ps1`을 호출하는 얇은 래퍼다.
- 실제 실행은 PowerShell `Start-Process`로 분리된 자식 `cmd.exe`에서 일어나며,
  python은 그 자식의 손자가 되어 호출자 셸이 끝나도 살아남는다.

## 접속 주소

- GPU PC 내부 확인:
  - `http://127.0.0.1:8188`
- 내부망 다른 PC 또는 백엔드 확인:
  - `http://192.168.100.77:8188`

## 주의사항

- 외부 PC에서 접속이 안 되면 Windows 방화벽에서 TCP `8188` 포트를 허용해야 한다.
- 도메인 구매와 HTTPS 설정은 1차 MVP 이후에 진행한다.
- 로그 파일은 `.gitignore`의 `*.log` 규칙으로 Git에 올리지 않는다.
