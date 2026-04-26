# AI-204 ComfyUI 실행과 재기동 메모

## 표준 실행 파일

ComfyUI는 아래 파일로 실행한다.

```text
tools/ComfyUI/run/start_comfyui.cmd
```

현재 명령:

```bat
".venv_local\Scripts\python.exe" main.py --listen 0.0.0.0 --port 8188 --disable-auto-launch --preview-method auto 1>>run\comfyui-stdout.log 2>>run\comfyui-stderr.log
```

## 실행 기준

- 작업 디렉터리는 `tools/ComfyUI`다.
- Python은 `tools/ComfyUI/.venv_local`을 사용한다.
- listen 주소는 `0.0.0.0`이다.
- 포트는 `8188`로 고정한다.
- stdout/stderr는 `run/comfyui-stdout.log`, `run/comfyui-stderr.log`에 누적한다.

## 재기동 순서

1. 기존 ComfyUI 콘솔이 떠 있으면 `Ctrl+C`로 종료한다.
2. `tools/ComfyUI/run/start_comfyui.cmd`를 다시 실행한다.
3. GPU/운영 데스크탑에서 `http://127.0.0.1:8188`을 확인한다.
4. 프록시 서버에서 `COMFYUI_UPSTREAM` 주소를 직접 확인한다.
5. `tools/infra`에서 Nginx를 재생성한다.

```powershell
docker compose up -d --force-recreate nginx
docker compose exec nginx nginx -t
```

6. 프록시 주소로 HTTP와 `/ws`를 확인한다.

## 운영 메모

- MVP 단계에서는 Windows 서비스 등록까지 하지 않고 수동 실행 기준으로 둔다.
- 재부팅 후에는 관리자가 `start_comfyui.cmd`를 다시 실행한다.
- 장시간 운영이 필요하면 후속 작업에서 Windows Task Scheduler 또는 NSSM 서비스화를 검토한다.
- `run/*.log`는 Git에 올리지 않는다.

## 장애 시 우선 확인

- ComfyUI 콘솔 또는 `run/comfyui-stderr.log`에 Python exception이 있는지 확인한다.
- `8188` 포트가 열려 있는지 확인한다.
- Windows 방화벽 인바운드 규칙이 프록시 서버 IP를 허용하는지 확인한다.
- `tools/infra/.env`의 `COMFYUI_UPSTREAM`이 실제 접근 가능한 주소인지 확인한다.
- Docker Desktop의 `host.docker.internal`은 같은 PC에서 로컬 smoke test를 할 때만 기본값으로 사용한다.
