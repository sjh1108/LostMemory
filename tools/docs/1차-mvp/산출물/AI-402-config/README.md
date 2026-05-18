# AI-402 ComfyUI·S3·Postgres 설정값 구현 산출물

`AI-402`에서는 `tools/ai_server`가 ComfyUI, S3, Postgres 관련 설정값을 코드에서 읽고 시작 시 검증하도록 정리한다.

## 환경파일 로드 기준

| Profile | 기본 파일 | 추가 override | 용도 |
| --- | --- | --- | --- |
| `dev` | `.env` | `.env.dev` | 로컬 개발, desktop ComfyUI 연결 |
| `prod` | `.env` | `.env.prod` | EC2/운영 실행값 override |

`application.yml`은 항상 `.env`를 읽고, `application-dev.yml`과 `application-prod.yml`이 profile별 override 파일을 추가로 읽는다.

## 주요 환경변수

| 변수 | dev 예시 | prod 예시 | 사용 위치 |
| --- | --- | --- | --- |
| `COMFYUI_BASE_URL` | `http://host.docker.internal:8188` | `http://<reachable-comfyui-host>:8188` | ComfyUI HTTP client |
| `COMFYUI_CONNECT_TIMEOUT_MS` | `5000` | `5000` | ComfyUI HTTP client |
| `COMFYUI_READ_TIMEOUT_SECONDS` | `30` | `60` | ComfyUI HTTP client |
| `POSTGRES_HOST` | `localhost` | `postgres` or managed DB host | PostgresProperties |
| `POSTGRES_PORT` | `5432` | `5432` | PostgresProperties |
| `POSTGRES_DB` | `ai_tool` | real DB name | PostgresProperties |
| `POSTGRES_USER` | `ai_tool` | real DB user | PostgresProperties |
| `POSTGRES_PASSWORD` | local password | real DB password | PostgresProperties |
| `AWS_REGION` | `ap-northeast-2` | actual region | StorageS3Properties |
| `AWS_S3_BUCKET` | test bucket | actual bucket | StorageS3Properties |
| `AWS_ACCESS_KEY_ID` | test key | actual key | StorageS3Properties |
| `AWS_SECRET_ACCESS_KEY` | test secret | actual secret | StorageS3Properties |
| `JWT_SECRET` | placeholder only | real secret later | future login env reservation |
| `JWT_EXPIRATION` | `3600` | `3600` | future login env reservation |

## 구현 결과

- `ComfyUiProperties`, `PostgresProperties`, `StorageS3Properties`를 각각 분리
- `RestClient` 기반 `ComfyUiClient` bean 추가
- 필수 설정값은 서버 시작 시 바로 검증
- `.env.example`에 ComfyUI timeout과 로그인 예약 변수까지 반영
