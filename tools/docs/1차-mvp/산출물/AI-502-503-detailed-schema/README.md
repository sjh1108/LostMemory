# AI-502, AI-503 테이블 상세 필드 구현

## 목적

`AI-501`에서 만든 최소 스키마 뼈대 위에 `users`, `workflow_snapshots`, `generations`, `generation_outputs`의 실제 1차 MVP 운영 컬럼을 확정한다.

이번 단계는 이후 `AI-505`, `AI-507`, `AI-605` 구현에서 다시 스키마를 흔들지 않게 만드는 것이 목적이다.

## 이번 단계에서 확정한 컬럼

## `users`

- `id`
- `username`
- `display_name`
- `is_active`
- `last_login_at`
- `created_at`
- `updated_at`

### 판단

- `password_hash`는 아직 넣지 않는다.
  - 로그인 구현은 `AI-701` 범위다.
- `role`도 아직 넣지 않는다.
  - 현재 1차 MVP에서는 내부 사용자 구분보다 생성 추적이 우선이다.

## `workflow_snapshots`

- `id`
- `workflow_name`
- `workflow_version`
- `workflow_hash`
- `source_filename`
- `primary_model_name`
- `model_metadata_json`
- `workflow_json`
- `created_by`
- `created_at`

### 판단

- workflow snapshot은 immutable 사본으로 본다.
- `workflow_hash`는 반복 workflow 재사용 시 upsert 기준으로 쓴다.
- 모델 정보는 고정 컬럼 여러 개보다 `model_metadata_json`으로 먼저 묶는다.
- `workflow_json`은 추후 workflow 다운로드 API의 source of truth가 된다.

## `generations`

- `id`
- `prompt_id`
- `workflow_snapshot_id`
- `created_by`
- `execution_status`
- `failure_reason`
- `failed_stage`
- `prompt_summary`
- `full_prompt`
- `user_message`
- `internal_message`
- `completed_at`
- `created_at`
- `updated_at`

### 판단

- `execution_status`는 `AI-406` 기준을 그대로 사용한다.
  - `SUBMITTED`, `RUNNING`, `SUCCEEDED`, `FAILED`, `TIMED_OUT`
- `failure_reason`은 `AI-406` 기준을 그대로 사용한다.
  - `HISTORY_FETCH_FAILED`, `COMFYUI_REPORTED_FAILURE`, `OUTPUT_MISSING`, `POLL_TIMEOUT`, `UNKNOWN_FAILURE`
- `failed_stage`는 장애가 어느 단계에서 났는지 남긴다.
  - `PROMPT_SUBMIT`, `GENERATION`, `HISTORY_POLL`, `OUTPUT_DISCOVERY`, `S3_UPLOAD`, `METADATA_SAVE`
- `full_prompt`는 재현성과 상세 조회를 위해 지금 넣는다.

## `generation_outputs`

- `id`
- `generation_id`
- `output_index`
- `filename`
- `subfolder`
- `output_type`
- `mime_type`
- `image_url`
- `created_at`

### 판단

- output image는 1장만 고정하지 않고 여러 장 가능성을 열어둔다.
- `output_index`는 생성 순서를 고정하기 위한 컬럼이다.
- `image_url`은 아직 nullable이며, 이후 `AI-508`에서 실제 업로드 후 채운다.
- `local_path` 같은 머신 의존 경로는 저장하지 않는다.

## 인덱스

### `workflow_snapshots`

- `UNIQUE (workflow_hash)`
- `INDEX (created_by)`
- `INDEX (workflow_name)`

### `generations`

- `UNIQUE (prompt_id)`
- `INDEX (workflow_snapshot_id)`
- `INDEX (created_by)`
- `INDEX (execution_status)`
- `INDEX (created_at DESC)`
- `INDEX (created_by, created_at DESC)`

### `generation_outputs`

- `INDEX (generation_id)`
- `UNIQUE (generation_id, output_index)`

## 다음 단계

- `AI-504`: `audit_logs` action/status/helper 기준 반영
- `AI-505`: generation row insert와 workflow snapshot upsert 연결
- `AI-507`: output 파일 탐색과 `completed_at`, output 메타데이터 실제 저장 연결

## 검증

- `docker compose down -v`로 AI tool Postgres volume을 비운 뒤 fresh init 검증
- `docker compose up -d postgres`
- `docker compose logs --tail 80 postgres`
  - `/docker-entrypoint-initdb.d/001_init_schema.sql`가 실제 실행된 로그 확인
- `docker compose exec -T postgres psql -U ai_tool -d ai_tool -c "\d+ users"`
  - `is_active`, `last_login_at`, username 공백 방지 constraint 확인
- `docker compose exec -T postgres psql -U ai_tool -d ai_tool -c "\d+ workflow_snapshots"`
  - `workflow_hash`, `workflow_json`, `model_metadata_json`, 관련 index / unique constraint 확인
- `docker compose exec -T postgres psql -U ai_tool -d ai_tool -c "\d+ generations"`
  - `failed_stage`, `full_prompt`, `completed_at`, 상태 / 실패 사유 / 단계 check constraint 확인
- `docker compose exec -T postgres psql -U ai_tool -d ai_tool -c "\d+ generation_outputs"`
  - `output_index`, `mime_type`, `(generation_id, output_index)` unique constraint 확인
