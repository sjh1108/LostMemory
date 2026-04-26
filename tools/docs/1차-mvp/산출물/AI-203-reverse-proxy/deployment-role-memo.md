# AI-203-02 프록시 서버 위치와 역할 확정

## 결정

AI 도구는 `tools` 하위에서 별도 서비스로 구성한다.

기존 프로젝트 루트의 `server/`, `client/`는 이번 `AI-203` 작업에서 수정하지 않는다.

## 서버 역할

### EC2 공개 진입 서버

현재 우선안은 작은 EC2를 도메인이 가리키는 공개 진입 서버로 두는 것이다.

EC2의 기본 역할은 아래와 같다.

- Nginx 실행
- AI 도구 백엔드 실행
- Postgres 실행
- S3 연동
- 결과 조회 처리
- 외부 사용자가 구매 도메인으로 접속할 때 첫 진입점 역할

도메인은 EC2의 public IP 또는 Elastic IP를 가리킨다.

EC2에는 ComfyUI를 올리지 않는 것을 우선한다. GPU EC2는 비용 부담이 크고, 이번 MVP는 기본 크레딧을 아끼는 방향이기 때문이다.

### 운영 데스크탑

운영 데스크탑은 EC2와 별도 네트워크에 있을 수 있는 이미지 생성 worker다.

역할은 아래와 같다.

- ComfyUI 실행 후보
- 모델 파일과 LoRA 파일 보관
- 이미지 생성 처리
- 필요 시 로컬 실험용 ComfyUI 실행

운영 데스크탑이 꺼져 있으면 EC2의 백엔드와 조회 기능은 살아 있어도 새 이미지 생성은 실패한다.

운영 데스크탑이 다른 네트워크에 있다면 EC2가 운영 데스크탑의 ComfyUI endpoint에 접근할 방법이 필요하다.

### GPU 데스크탑

GPU 데스크탑은 기본 운영 서버가 아니라 대체 추론 서버 또는 학습 서버로 둔다.

역할은 아래와 같다.

- 운영 데스크탑에서 이미지 생성이 무겁거나 불안정할 때 ComfyUI 실행 대상
- LoRA 또는 모델 학습용 서버
- 큰 GPU 메모리가 필요한 실험용 서버

GPU 데스크탑에서 ComfyUI를 실행할 경우 Nginx의 `COMFYUI_UPSTREAM`을 GPU 데스크탑 주소로 바꾼다.

예시:

```text
COMFYUI_UPSTREAM=http://192.168.100.77:8188
```

단, 이 주소는 EC2에서 실제로 접근 가능한 주소여야 한다. 집/학교/회사 내부 IP는 EC2에서 바로 접근할 수 없다.

## 네트워크 연결 방식

EC2와 운영/GPU 데스크탑이 다른 네트워크에 있어도 가능하다.

다만 아래 중 하나가 필요하다.

1. 데스크탑 쪽 공인 IP와 공유기 포트포워딩
2. EC2와 데스크탑을 묶는 VPN 또는 mesh network
3. 데스크탑에서 EC2로 먼저 연결하는 SSH reverse tunnel

집 인터넷이 CGNAT이거나 포트포워딩이 막혀 있으면 2번 또는 3번 방식이 현실적이다.

### S3

S3는 장기 보존과 공유가 필요한 파일 저장소로 사용한다.

저장 후보는 아래와 같다.

- 생성 결과 이미지
- workflow snapshot JSON
- 나중에 공유용 preview 이미지 또는 다운로드 파일

운영 데스크탑 로컬 output 폴더는 임시 생성 위치로 보고, 공유와 조회 기준은 S3를 우선한다.

### Postgres

DB는 SQLite가 아니라 Postgres로 고정한다.

Postgres에 저장할 데이터 후보는 아래와 같다.

- generation 요청 메타데이터
- output 이미지 메타데이터
- S3 object key
- workflow snapshot 메타데이터
- 사용자와 권한 정보
- 실패 단계와 오류 메시지

Postgres는 Nginx가 직접 접근하지 않는다.

정상 요청 흐름은 아래와 같다.

```text
사용자 -> Nginx -> AI 도구 백엔드 -> Postgres
```

## 1차 구성안

현재 우선안:

```text
구매 도메인
  -> EC2 Nginx
  -> AI 도구 백엔드
  -> Postgres
  -> 운영 데스크탑 ComfyUI
  -> S3
```

운영 데스크탑에서 이미지 생성이 무거운 경우:

```text
구매 도메인
  -> EC2 Nginx
  -> AI 도구 백엔드
  -> Postgres
  -> GPU 데스크탑 ComfyUI
  -> S3
```

## 열어둘 질문

- 구매 도메인 이름
- EC2 instance type
- 운영/GPU 데스크탑 연결 방식을 포트포워딩, VPN, reverse tunnel 중 무엇으로 할지 여부
- 집 네트워크 공인 IP와 CGNAT 여부
- 운영 데스크탑에서 ComfyUI까지 상시 실행 가능한지 여부
