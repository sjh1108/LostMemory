# 내부용 ComfyUI 웹 개발 프로젝트 - GPU 데스크탑 세팅 가이드 반영본 v2

## 문서 상태

- 기준 원본: `C:/Users/SSAFY/Downloads/internal_comfyui_gpu_setup_guide_v1.md`
- 반영 기준: 2026-04-21까지 테스트 저장소에서 실제로 검증한 ComfyUI 실행 방식
- 편집 원칙: v1의 운영 의도는 유지하되, 실제 프로젝트에서 먼저 검증된 `source install + 로컬 venv` 경로를 기준으로 다시 정리

---

## 1. 문서 목적

이 문서는 GPU 데스크탑을 ComfyUI 추론 서버로 세팅하는 현재 기준 가이드다.

이 문서의 목표는 다음 4가지를 끝까지 완료하는 것이다.

1. Windows 데스크탑에서 ComfyUI가 실제로 기동되는지 확인한다.
2. 현재 프로젝트에서 사용 중인 경로와 실행 명령을 기준으로 정리한다.
3. 체크포인트 적용과 첫 generation 검증까지 이어질 수 있게 한다.
4. 이후 Spring Boot 연동과 S3 업로드 준비 기준을 남긴다.

---

## 2. 대상 장비 정보

### 2.1 현재 장비 스펙

- OS: Windows 64-bit
- CPU: AMD Ryzen 5 2600X Six-Core Processor
- RAM: 32GB
- GPU: NVIDIA GeForce GTX 1060 6GB

### 2.2 해석

- MVP용 ComfyUI 추론 서버로 사용 가능
- 다만 VRAM이 6GB이므로 SD1.5 + text-to-image부터 검증
- 1차 체크포인트는 `v1-5-pruned-emaonly-fp16.safetensors`

---

## 3. 최종 운영 구조

### 3.1 고정 구조

- AWS = 웹 진입점
- GPU 데스크탑 = 추론 서버
- S3 = 결과 저장소
- SQLite = 메타데이터 DB

### 3.2 데이터 흐름

1. 사용자가 AWS 도메인으로 접속한다.
2. Spring Boot가 요청을 받는다.
3. Spring Boot가 GPU 데스크탑의 ComfyUI API를 호출한다.
4. GPU 데스크탑이 로컬에 결과 이미지를 임시 저장한다.
5. GPU 데스크탑이 결과 이미지를 S3로 직접 업로드한다.
6. Spring Boot가 SQLite에 메타데이터를 기록한다.

### 3.3 MVP 연동 방식

- API 방식: `POST /prompt` + `GET /history/{prompt_id}` 폴링
- WebSocket(`/ws`)는 후속 고도화 대상

---

## 4. 현재 프로젝트에서 실제로 채택한 방식

### 4.1 v1과 달라진 점

원본 v1은 `Portable -> Manual/comfy-cli` 흐름을 기본 전제로 잡았지만, 현재 프로젝트에서는 먼저 아래 방식이 실제로 검증되었다.

- `테스트 저장소/third_party/ComfyUI`에 upstream clone 반입
- `.venv_local` 로컬 가상환경 사용
- `python main.py` 직접 실행
- `http://127.0.0.1:8188` 접속 확인
- `GET /queue` 응답 확인

### 4.2 현재 기준 결론

- **지금 기준 1차 운영 방식**
  - source install + 로컬 venv
- **아직 미확정**
  - comfy-cli 운영 전환 여부
  - vendor snapshot 유지 여부

### 4.3 현재 기준 저장소 역할

- `테스트`
  - ComfyUI 로컬 검증
  - 실행 가이드 정리
  - 체크포인트/모델 경로 확인
- `팀프로젝트`
  - 실제 기능 구현과 Story 브랜치 작업

---

## 5. 현재 프로젝트 적용 경로

### 5.1 기준 경로

```text
테스트 저장소 루트:
C:\Users\SSAFY\Desktop\2학기 3PJT\comfy_UI_Test

ComfyUI 실행 경로:
C:\Users\SSAFY\Desktop\2학기 3PJT\comfy_UI_Test\third_party\ComfyUI
```

### 5.2 현재 프로젝트 기준 중요 경로

```text
C:\Users\SSAFY\Desktop\2학기 3PJT\comfy_UI_Test\third_party\ComfyUI\models\checkpoints
C:\Users\SSAFY\Desktop\2학기 3PJT\comfy_UI_Test\third_party\ComfyUI\models\loras
C:\Users\SSAFY\Desktop\2학기 3PJT\comfy_UI_Test\third_party\ComfyUI\output
```

### 5.3 실행 환경

- 로컬 venv: `.venv_local`
- 접속 주소: `http://127.0.0.1:8188`

---

## 6. 1차 검증: 현재 방식으로 ComfyUI 기동 확인

### Step 1. ComfyUI 경로로 이동

```powershell
cd "C:\Users\SSAFY\Desktop\2학기 3PJT\comfy_UI_Test\third_party\ComfyUI"
```

### Step 2. 서버 실행

```powershell
.\.venv_local\Scripts\python.exe main.py --listen 127.0.0.1 --port 8188 --disable-auto-launch --preview-method auto
```

### Step 3. 접속 확인

브라우저:

```text
http://127.0.0.1:8188
```

### Step 4. 현재까지 실제 확인한 것

- ComfyUI 서버 시작 메시지 확인
- 로컬 UI 접속 가능
- `/queue` 응답 확인

### Step 5. 아직 남은 것

- SD1.5 체크포인트 배치
- 첫 text-to-image 성공
- output 파일 실제 생성 확인

---

## 7. 체크포인트 배치

### 7.1 기준 체크포인트

```text
v1-5-pruned-emaonly-fp16.safetensors
```

### 7.2 배치 경로

```text
C:\Users\SSAFY\Desktop\2학기 3PJT\comfy_UI_Test\third_party\ComfyUI\models\checkpoints\
```

### 7.3 현재 상태

- 체크포인트 파일은 아직 실제 반입 전
- 첫 generation 검증의 선행 조건으로 남아 있음

---

## 8. 첫 generation 검증 절차

### Step 1. 모델 인식 확인

- ComfyUI 실행 후 `Load Checkpoint` 드롭다운에서 모델이 보이는지 확인
- 안 보이면 `R` 새로고침 또는 서버 재시작

### Step 2. 최소 txt2img 그래프 사용

- `Load Checkpoint`
- `CLIP Text Encode (Prompt)` 2개
- `Empty Latent Image`
- `KSampler`
- `VAE Decode`
- `Save Image`

### Step 3. 권장 시작값

- width / height: `512 x 512`
- batch size: `1`
- steps: `20`
- cfg: `7`
- sampler: `euler`

### Step 4. 성공 기준

- 이미지 1장 생성 성공
- `output` 폴더 파일 생성 확인
- 생성 로그 기록

---

## 9. API 확인 절차

### 9.1 현재 확인 완료

- `GET /queue`

### 9.2 다음 확인 대상

- `POST /prompt`
- `GET /history/{prompt_id}`

### 9.3 확인 순서

1. 체크포인트 배치
2. UI에서 첫 generation 성공
3. API로 같은 흐름 재현
4. `prompt_id`와 output 연결 구조 확인

---

## 10. 네트워크 / 방화벽 설정 원칙

### 10.1 권장 원칙

- GPU 데스크탑의 ComfyUI를 완전 공개하지 않는다
- 가능하면 허용된 접근만 받는다
- AWS(Spring Boot)만 직접 호출하도록 제한하는 구조를 우선 검토한다

### 10.2 현재 프로젝트 단계 해석

- 지금은 로컬 기동 검증 단계
- 외부 노출과 방화벽 세부 설정은 실제 연동 단계에서 확정

---

## 11. S3 업로드 구조

### 11.1 고정 방식

- GPU 데스크탑 direct upload to S3
- AWS 백엔드는 메타데이터 기록

### 11.2 처리 순서

1. ComfyUI가 로컬 `output` 폴더에 결과 저장
2. 업로드 대상 파일 경로 확인
3. S3 업로드 수행
4. 업로드 완료 URL 확보
5. Spring Boot에 메타데이터 전달

### 11.3 현재 상태

- 설계 확정
- 구현 전

---

## 12. comfy-cli / Portable에 대한 현재 판단

### 12.1 Portable

- 빠른 1차 검증용 대안으로는 여전히 유효
- 하지만 현재 저장소에서는 실제로 사용하지 않았다

### 12.2 comfy-cli

- 운영 전환 후보로는 남겨 둔다
- 다만 현재 프로젝트에서 먼저 검증된 방식은 아니다

### 12.3 지금 기준 권장 해석

- 당장 문서를 읽고 따라할 때는 `source install + .venv_local + main.py 실행`을 기준으로 본다
- comfy-cli는 별도 R&D 항목으로 남긴다

---

## 13. Spring Boot 연동 준비 체크리스트

### 필수

- ComfyUI 서버 주소
- 포트 `8188`
- `POST /prompt` 요청 JSON 구조
- `GET /history/{prompt_id}` 응답 구조
- S3 버킷 이름
- 업로드 키 네이밍 규칙
- SQLite 파일 위치

### 권장

- `generator_host` 이름 규칙
- workflow snapshot 저장 규칙
- 실패 로그 저장 규칙

---

## 14. 운영 시 기본 점검 항목

### 매번 확인할 것

- ComfyUI 프로세스 살아 있는지
- 체크포인트 로딩 되는지
- output 폴더에 파일 생성되는지
- S3 업로드 되는지
- AWS 백엔드에 메타데이터 기록되는지

### 장애 시 먼저 볼 것

- ComfyUI 콘솔 로그
- 체크포인트 경로
- 포트 충돌
- Windows 방화벽
- S3 자격 증명
- Spring Boot 요청 로그

---

## 15. 이번 단계에서 하지 않는 것

- img2img 운영 적용
- LoRA 다수 적용
- ControlNet 적용
- WebSocket 실시간 진행률 UI
- Kafka 기반 작업 큐
- 복잡한 관리자 운영 도구

---

## 16. 2026-04-21 기준 체크리스트

### 완료

- `third_party/ComfyUI` upstream clone 반입
- `.venv_local` 환경으로 로컬 서버 기동
- `http://127.0.0.1:8188` 접속 확인
- `/queue` 응답 확인
- 첫 실행 가이드 문서화

### 진행중

- ComfyUI 관리 방식 결정
- 체크포인트 확보 준비

### 해야 할 일

- SD1.5 체크포인트 배치
- 첫 generation 성공
- `/prompt`, `/history/{prompt_id}` 확인
- S3 업로드 구조 구현
- Spring Boot 연동 구현

---

## 17. 최종 정리

원본 v1의 큰 방향은 유지하지만, 현재 프로젝트에서는 `Portable -> comfy-cli`보다 먼저 `source install + 로컬 venv` 경로가 실제로 검증되었다.

따라서 지금 기준으로 가장 중요한 다음 단계는 아래 세 가지다.

1. 체크포인트 확보
2. 첫 generation 성공
3. API / 업로드 / 메타데이터 저장 연결 검증
