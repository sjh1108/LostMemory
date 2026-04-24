# AI 도구 백엔드 준비

## 목적

이 폴더는 ComfyUI 기반 AI 도구 백엔드를 둘 위치다.

1차 MVP에서는 `생성 요청 -> ComfyUI /prompt 호출 -> /history 폴링 -> output 탐색 -> S3 업로드 -> DB 메타데이터 저장 -> 결과 조회` 흐름을 닫는 것을 목표로 한다.

## 현재 상태

- 아직 Spring Boot 프로젝트는 생성하지 않았다.
- 먼저 `.env.example`로 백엔드가 필요로 하는 환경변수 이름만 고정한다.
- 실제 비밀값이 들어가는 `.env` 파일은 Git에 올리지 않는다.

## 환경변수 파일

- 공유용 예시:
  - `.env.example`
- 로컬 실제 파일:
  - `.env`

루트 `.gitignore`가 `.env`, `.env.*`를 무시하고 `!.env.example`만 허용하므로 실제 키와 비밀번호는 커밋되지 않는다.

## ComfyUI 연결 기준

- GPU 데스크탑 IPv4:
  - `192.168.100.77`
- ComfyUI base URL:
  - `http://192.168.100.77:8188`
- GPU PC 내부 확인 URL:
  - `http://127.0.0.1:8188`
- 내부망 다른 PC 또는 백엔드 확인 URL:
  - `http://192.168.100.77:8188`

## 경로 기준

- ComfyUI output:
  - `C:/Users/SSAFY/Desktop/2학기 3PJT/S14P31C201/tools/ComfyUI/output`
- ComfyUI models:
  - `C:/Users/SSAFY/Desktop/2학기 3PJT/S14P31C201/tools/ComfyUI/models`
- ComfyUI loras:
  - `C:/Users/SSAFY/Desktop/2학기 3PJT/S14P31C201/tools/ComfyUI/models/loras`

## 1차 구현 순서

1. Spring Boot 프로젝트 생성
2. Health endpoint 추가
3. `.env` 로딩 방식 결정
4. ComfyUI HTTP client 설정
5. SQLite 설정
6. S3 설정
7. 생성 요청 API 구현
8. `/history` 비동기 폴링 구현
9. output 파일 탐색과 S3 업로드 구현
10. 목록/상세 조회 API 구현

## 아직 하지 않는 것

- 도메인 구매
- HTTPS 설정
- 외부 공개용 인증
- img2img 기반 도트 화풍 변환
- 프로젝트 전용 LoRA 학습
