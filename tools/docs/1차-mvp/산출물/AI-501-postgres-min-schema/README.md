# AI-501 Postgres 최소 스키마 생성 및 문서화

## 목적

AI 도구 백엔드에서 이후 `AI-502`, `AI-503`, `AI-504`, `AI-505`, `AI-507`, `AI-508`로 이어질 최소 DB 뼈대를 먼저 고정한다.

이번 단계에서는 로컬 설치형 PostgreSQL이 아니라 `tools/infra` 기준 Docker Postgres를 사용하고, VSCode DB client에서 바로 볼 수 있는 개발용 연결 기준까지 같이 정리한다.

## 이번 단계 결정

### 1. DB 실행 방식

- 로컬 Windows 직접 설치형 PostgreSQL을 쓰지 않는다.
- `tools/infra/docker-compose.yml`의 `postgres` 컨테이너를 AI 도구 DB 기준으로 사용한다.
- 게임 서버 DB와는 **별도 컨테이너 / 별도 DB 문맥**으로 본다.

### 2. 스키마 source of truth

- Spring JPA 자동 생성이 아니라
- `tools/infra/postgres/init/001_init_schema.sql`
  를 초기 스키마 기준 파일로 사용한다.

### 3. VSCode 접속 방식

- 개발 환경에서만 `docker-compose.override.yml`로 `55432:5432`를 연다.
- VSCode DB client 연결값:
  - Host: `localhost`
  - Port: `55432`
  - Database: `ai_tool`
  - Username: `ai_tool`
  - Password: `tools/infra/.env`

### 4. volume / 백업 규칙

- 기본 저장: `postgres_data` named volume
- 최소 백업: `pg_dump`
- init SQL은 빈 volume 첫 생성 시점에만 자동 실행

## 검증

- `docker compose config`로 base compose + override 파싱 확인
- `docker compose up -d postgres`로 AI 도구 Postgres 컨테이너 기동
- `docker compose ps postgres`에서 `healthy` 상태 확인
- `docker compose exec -T postgres psql -U ai_tool -d ai_tool -c "\dt"`로 아래 5개 테이블 생성 확인
  - `users`
  - `workflow_snapshots`
  - `generations`
  - `generation_outputs`
  - `audit_logs`

## 최소 테이블

- `users`
- `workflow_snapshots`
- `generations`
- `generation_outputs`
- `audit_logs`

## 현재 스키마가 반영하는 핵심 값

### `workflow_snapshots`

- `workflow_name`
- `workflow_version`
- `source_filename`
- `workflow_api_json`

### `generations`

- `prompt_id`
- `execution_status`
- `failure_reason`
- `prompt_summary`
- `user_message`
- `internal_message`

### `generation_outputs`

- `filename`
- `subfolder`
- `output_type`
- 이후 `image_url`

### `audit_logs`

- `action_type`
- `status`
- `prompt_id`
- `detail_json`

## 지금 일부러 안 넣은 것

- 로그인 상세 필드
- workflow preset 운영 필드
- S3 업로드 상태 전용 필드
- 상세 에러 분류 테이블
- migration 도구

이건 다음 작업에서 확장한다.

## 다음 단계

- `AI-502`: `users`, `workflow_snapshots` 상세 필드 확정
- `AI-503`: `generations`, `generation_outputs` 상세 필드 확정
- `AI-504`: `audit_logs` action/status 표준화
