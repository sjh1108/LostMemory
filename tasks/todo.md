# ComfyUI 로컬 런타임 셋업 — 핸드오프 (2026-05-17 #3 EC2까지 동작)

> 다음 Claude Code 세션이 이 문서만 읽고 그대로 이어받을 수 있게 작성됨.

## 한 줄 요약

**전체 흐름 동작 검증 완료.** 팀원이 `https://lostmemory.duckdns.org` 접속 → EC2 nginx (Basic Auth) → tailnet → 이 PC RTX 4050에서 추론 → 결과 반환. 1차 MVP의 AI-201~206 + GPU worker 분리 구조가 실제로 닫혔음.

## 운영 정보 한 페이지

| 항목 | 값 |
|---|---|
| 공개 URL | `https://lostmemory.duckdns.org` |
| EC2 public IP | `43.201.152.168` (서울 ap-northeast-2) |
| EC2 hostname | `ip-172-31-28-88` (Ubuntu 24.04 LTS) |
| EC2 SSH alias | `ssh lostmemory` (사용자 SSH config 등록됨) |
| EC2 tailnet IP | `100.112.97.56` |
| 이 PC tailnet IP | `100.97.51.45` (`desktop-urke2vl`) |
| ComfyUI listen | `0.0.0.0:8188` |
| Windows 방화벽 룰 | `ComfyUI 8188 from Tailscale` (Inbound TCP 8188, 100.64.0.0/10 allow) |
| Tailscale latency | 12ms (direct peer-to-peer) |
| nginx upstream | `upstream comfyui { server 100.97.51.45:8188; }` |
| nginx config 경로 | `/etc/nginx/sites-enabled/comfyui` |
| TLS 인증서 | Let's Encrypt via certbot (`/etc/letsencrypt/live/lostmemory.duckdns.org/`) |
| Basic Auth 파일 | `/etc/nginx/.htpasswd` |
| WebSocket | nginx Upgrade/Connection 헤더 설정됨 |
| 타임아웃 | proxy_read/send 86400s (긴 generation 대응) |
| 업로드 한계 | `client_max_body_size 100M` |

## Tailscale / 네트워크 상태

- Tailscale 1.98.2 설치됨 (`C:\Program Files\Tailscale\`)
- 서비스 자동 시작 (Tailscale 서비스, Running/Automatic)
- 로그인 완료 (사용자 `sjh1108@`)
- 이 PC hostname: `desktop-urke2vl`
- **이 PC tailnet IPv4: `100.97.51.45`**
- ComfyUI listen: **`0.0.0.0:8188`** (loopback + tailnet 모두 응답 200 OK)
- Windows 방화벽 룰: ❌ **아직 없음 (admin 권한 필요, 아래 명령 참고)**

### Admin 방화벽 룰 명령 (사용자 직접 실행)

**관리자 권한 PowerShell**에서 한 줄:

```powershell
New-NetFirewallRule -DisplayName "ComfyUI 8188 from Tailscale" -Direction Inbound -Protocol TCP -LocalPort 8188 -Action Allow -RemoteAddress "100.64.0.0/10" -Profile Any
```

- 시작 메뉴 → PowerShell 우클릭 → **관리자 권한으로 실행** 후 위 명령 붙여넣기
- `100.64.0.0/10`은 Tailscale CGNAT 대역 — 이 대역에서만 8188 허용, 일반 인터넷은 차단
- 룰 확인: `Get-NetFirewallRule -DisplayName "ComfyUI 8188 from Tailscale"`
- 룰 삭제(돌리고 싶을 때): `Remove-NetFirewallRule -DisplayName "ComfyUI 8188 from Tailscale"`

## 환경 정보 (고정값)

- ComfyUI 루트: `C:\SSAFY\S14P31C201\tools\ComfyUI`
- venv: `tools\ComfyUI\.venv_local\Scripts\python.exe` (Python 3.13.13)
- GPU: NVIDIA GeForce RTX 4050 Laptop GPU, VRAM 6140 MB (DynamicVRAM 자동)
- torch: 2.12.0+cu130 / torchvision 0.27.0+cu130 / torchaudio 2.11.0+cu130
- CUDA runtime: 13.0
- ComfyUI: 0.19.3
- listen: `127.0.0.1:8188`

## 모델 파일 (전부 DONE)

| 파일 | 경로 | 크기 |
|---|---|---|
| v1-5-pruned-emaonly-fp16 | `models/checkpoints/` | 2034 MB |
| z_image_turbo_bf16 | `models/diffusion_models/` | 11740 MB |
| qwen_3_4b | `models/text_encoders/` | 7672 MB |
| ae | `models/vae/` | 320 MB |
| pixel_art_style_z_image_turbo | `models/loras/` | 162 MB |

## ✅ 검증 결과

### SD1.5 smoke (8.52초)
- prompt_id: `c86cb272-beb6-416d-9db2-e1a499e6da43`
- 20 step / cfg 7 / euler / 512×512 / ~5 it/s
- 출력: `output/SmokeTest_SD15_Cat_00001_.png` (368 KB)

### AI-202 Z-Image-Turbo 워크플로 (72.22초)
- prompt_id: `a1d28bdb-6fbb-43d6-bf37-16672a7323dc`
- z_image_turbo_bf16 + qwen_3_4b + ae VAE + pixel_art LoRA
- 8 step / cfg 1.5 / euler+simple / 512×512
- 초기 모델 로딩 30s + KSampler 23s + VAE decode
- 출력: `output/SmokeTest_ZIT_AI202_00001_.png` (100 KB)
- 결과 이미지: 픽셀 아트 판타지 캐릭터, 프롬프트(yellow hair, green tunic, red scarf, wooden shield, brown boots) 정확히 반영
- /history: `status.status_str=success, completed=true`
- **AI-202 기준 워크플로 1:1 재현 성공**

## 자주 쓰는 명령

### 진척 확인 (사람용)
```
C:\SSAFY\S14P31C201\tools\ComfyUI\run\check-progress.cmd
```
PowerShell 한 줄로:
```powershell
& "C:\SSAFY\S14P31C201\tools\ComfyUI\.venv_local\Scripts\python.exe" "C:\SSAFY\S14P31C201\tools\ComfyUI\run\check_progress.py"
```

### ComfyUI 기동
```powershell
& "C:\SSAFY\S14P31C201\tools\ComfyUI\.venv_local\Scripts\python.exe" "C:\SSAFY\S14P31C201\tools\ComfyUI\main.py" --listen 127.0.0.1 --port 8188 --disable-auto-launch --preview-method auto
```
또는 내부망 노출:
```
C:\SSAFY\S14P31C201\tools\ComfyUI\run\start_comfyui.cmd
```
(`start_comfyui.cmd`는 `--listen 0.0.0.0`)

### Smoke 테스트 재실행
```powershell
# SD1.5
curl.exe -X POST -H "Content-Type: application/json" --data "@C:\SSAFY\S14P31C201\tools\ComfyUI\run\smoke-sd15-prompt.json" http://127.0.0.1:8188/prompt

# Z-Image-Turbo (AI-202 reproduction)
curl.exe -X POST -H "Content-Type: application/json" --data "@C:\SSAFY\S14P31C201\tools\ComfyUI\run\smoke-zit-prompt.json" http://127.0.0.1:8188/prompt
```

## 🚀 다음 큰 작업: EC2/Lightsail + Tailscale로 팀 공유

목표 흐름:
```
팀원 브라우저
  → https://comfy.<우리도메인>            (EC2 nginx, Basic Auth)
  → (Tailscale tunnel)
  → 이 PC ComfyUI :8188                  (RTX 4050 추론)
  → S3 + Postgres                        (메타데이터/이미지 저장)
```

### 단계별 작업

1. **Lightsail 인스턴스 생성** (가장 작은 nano 인스턴스 충분, ~$3.5/월) ⏳ 다음 작업
   - Ubuntu 22.04 권장
   - Static IP 할당
   - 보안 그룹: 22, 80, 443 인바운드

2. **Tailscale 설치** ✅ 이 PC 완료, ❌ Lightsail 대기
   - 이 PC: ✅ 1.98.2 설치, 로그인 완료, tailnet IP `100.97.51.45`
   - Lightsail: `curl -fsSL https://tailscale.com/install.sh | sh && sudo tailscale up`
   - 같은 Tailscale 계정으로 로그인하면 자동으로 양쪽이 묶임

3. **이 PC의 ComfyUI listen 변경** ✅ 완료 / ⏳ 방화벽 룰만 남음
   - ✅ `--listen 0.0.0.0 --port 8188`로 실행 중 (loopback + tailnet 자체 응답 확인)
   - ⏳ Windows 방화벽 8188 인바운드 룰 — 위 admin 명령 한 번 실행 필요

4. **Lightsail에 인프라 배포**
   - `tools/infra` 폴더를 Lightsail로 rsync/scp
   - `tools/infra/.env` 생성 (`.env.example` 복사 후 채우기):
     - `PUBLIC_DOMAIN`, `COMFYUI_DOMAIN` → 실제 도메인 (구매 또는 무료 DDNS)
     - **`COMFYUI_UPSTREAM=http://100.97.51.45:8188`** ← 이 PC의 tailnet IP 확정값
     - `AI_BACKEND_UPSTREAM=http://host.docker.internal:8080` (ai_server 띄울 곳)
     - `POSTGRES_*` 채우기
   - `htpasswd -c tools/infra/nginx/auth/.htpasswd <team_member>` 팀원 수만큼
   - `docker compose up -d nginx postgres`

5. **DNS + Let's Encrypt** (AI-205 runbook 참고)
   - 도메인 A 레코드 → Lightsail Static IP
   - `docker compose --profile certbot run --rm certbot certonly --webroot ...`

6. **검증**
   - 팀원 PC에서 `https://comfy.<도메인>` 접속
   - Basic Auth 팝업
   - ComfyUI UI 로드
   - Queue Prompt → 이 PC GPU에서 추론 → output 반환
   - `/ws` 연결 (WebSocket) 정상

### 관련 기존 산출물

- 아키텍처: `tools/docs/1차-mvp/산출물/AI-203-reverse-proxy/`
- 도메인/HTTPS: `tools/docs/1차-mvp/산출물/AI-205-domain-https/`
- Basic Auth: `tools/docs/1차-mvp/산출물/AI-206-basic-auth/`
- 인프라 설정: `tools/infra/docker-compose.yml`, `tools/infra/.env.example`, `tools/infra/nginx/templates/ai-tool.conf.template`

### 결정 필요한 것 (다음 세션 시작 시 사용자에게 물어볼 항목)

- [ ] 도메인 구매 여부 / 사용할 도메인 이름 (없으면 `*.duckdns.org` 또는 `*.nip.io` 가능)
- [ ] Lightsail 리전 (서울 `ap-northeast-2` 권장)
- [ ] 팀원 수 + 계정 ID (`.htpasswd` 생성용)
- [ ] AWS 자격증명 (콘솔에서 직접 만들지, IaC로 만들지)
- [ ] Tailscale 계정 (HuggingFace, Google, Microsoft 등 OAuth 가능)

## 알려진 함정 / 함정 메모

- **PowerShell 5.1 (Windows 기본)**: `Tee-Object -Encoding utf8` ❌, `-ExecutionPolicy Bypass` ❌(classifier 차단), `&&` ❌, ps1 직접 실행 ❌ (실행 정책). 인라인 명령 또는 `.cmd` 우회.
- **cmd 안에 PowerShell 인라인**: `%`/`%%` 이스케이프, 변수치환(`%5` 등)과 충돌. 차라리 별도 `.py` 파일로 분리하고 `.cmd`는 단순 호출만.
- **HuggingFace repo 이름은 추측 금지**: 문서의 모델 파일명만 보고 repo id 추측 시 401 받음. 항상 `https://huggingface.co/api/models?search=...`로 확인. Z-Image-Turbo는 `Comfy-Org/z_image_turbo` (소문자+언더스코어).
- **대용량 HF 다운로드**: 한국 회선에서 connection reset 잦음. `curl --continue-at - --retry 20`으로 resume. pip로 직접 받지 말고 로컬 wheel install.
- **HF 미러(hf-mirror.com)**: 한국 네트워크에서 의미 있는 속도 차이 없음 — 보틀넥은 가정용 회선.
- **ComfyUI `custom_nodes/` 디렉터리 없으면 즉시 죽음** (FileNotFoundError). 빈 디렉터리도 OK.
- **ComfyUI는 부분 다운로드된 모델도 `/object_info`에 노출**. 100% 완료 전엔 사용 금지.
- **RTX 4050 6GB VRAM**: Z-Image-Turbo bf16(11.7GB)이 VRAM보다 큼. DynamicVRAM(automatic, comfy-aimdo)이 작동해 정상 추론. 첫 로딩 30초, 이후 8 step 23초.
- **이 PC ≠ 문서의 "GPU 데스크탑(192.168.100.77, GTX 1060)"**: 별도 노트북. 1차 MVP 문서들의 host/port 기준은 그대로 두되, 이번 setup은 별도 worker로 취급.

## 파일 위치 모음

| 자료 | 경로 |
|---|---|
| ComfyUI source + venv | `tools/ComfyUI/` |
| 모델 5종 | `tools/ComfyUI/models/{checkpoints,diffusion_models,vae,text_encoders,loras}/` |
| 출력 PNG | `tools/ComfyUI/output/` |
| 진척 확인 cmd | `tools/ComfyUI/run/check-progress.cmd` |
| 진척 확인 py | `tools/ComfyUI/run/check_progress.py` |
| 재개 다운로드 | `tools/ComfyUI/run/resume-downloads.cmd` |
| 기동 cmd | `tools/ComfyUI/run/start_comfyui.cmd` |
| ComfyUI 콘솔 로그 | `tools/ComfyUI/run/comfyui.log` |
| SD1.5 smoke JSON | `tools/ComfyUI/run/smoke-sd15-prompt.json` |
| Z-Image smoke JSON | `tools/ComfyUI/run/smoke-zit-prompt.json` |
| 1차 MVP 진행기록 | `tools/docs/1차-mvp/컴피유아이-1차-mvp-진행-기록.md` |
| AI-202 워크플로 JSON | `tools/docs/1차-mvp/산출물/AI-202-Z-Image-Turbo/workflows/` |
| AI-301 prompt 샘플 | `tools/docs/1차-mvp/산출물/AI-301-prompt-sample/` |
| 핸드오프 doc (이 파일) | `tasks/todo.md` |
| 함정 메모 | `tasks/lessons.md` |

## 다음 세션 첫 한 마디 예시

EC2/Tailscale로 진행:
> `tasks/todo.md` 보고 EC2/Tailscale 단계로 진행. Lightsail 띄우고 Tailscale로 이 PC랑 연결.

ComfyUI만 다시 띄울 거면:
> ComfyUI 다시 띄워줘

이미지 더 만들 거면:
> Z-Image-Turbo로 [프롬프트] 이미지 생성해줘
