# 1차 MVP 산출물 지도

## 목적

이 문서는 `tools/docs/1차-mvp/산출물` 전체를 빠르게 훑을 수 있게 만드는 상위 지도다.

다음 작업자가 이 문서만 읽어도
- 지금까지 어떤 산출물이 쌓였는지
- `AI-501` 같은 후속 작업에서 무엇을 다시 봐야 하는지
- DB, API, 인프라, 상태 모델이 어디에 정리되어 있는지
를 바로 찾을 수 있게 하는 것이 목적이다.

## 현재 산출물 한눈에 보기

### Baseline / 실측

- `AI-202-Z-Image-Turbo`
  - 기준 workflow, prompt, baseline output 실험
- `AI-301-prompt-sample`
  - 실제 성공 `/prompt` 요청 body, 응답, curl 재현 절차
- `AI-302-history-sample`
  - `/history/{prompt_id}` raw JSON과 output key 해석 기준
- `AI-303-prompt-output-mapping`
  - `prompt_id`, output filename, workflow 이름, 모델명, created_at 매핑 규칙
- `AI-304-failure-timeout-samples`
  - 실패 샘플 4종과 timeout/message 초안

### 인프라 / 배포

- `AI-203-reverse-proxy`
  - Nginx, Postgres, Compose, reverse proxy 구조 기준
- `AI-204-comfyui-ui-internal-address`
  - ComfyUI UI 내부 주소 연결 검증
- `AI-205-domain-https`
  - 도메인 / HTTPS 적용 기준
- `AI-206-basic-auth`
  - Basic Auth 운영 기준

### AI 도구 백엔드

- `AI-402-config`
  - ComfyUI / S3 / Postgres 설정값, env, fail-fast 기준
- `AI-403-generate-api`
  - `POST /generation-requests` 최소 API 계약
- `AI-404-prompt-submit`
  - `workflowId -> /prompt` body 조립, `prompt_id` submit
- `AI-405-history-polling`
  - `/history/{promptId}` polling, parser, 조회 endpoint
- `AI-406-status-handling`
  - timeout / failed 상태 처리, execution status, failure reason, audit hook

### DB 스키마 / 메타데이터

- `AI-501-postgres-min-schema`
  - Docker Postgres, init SQL, 최소 스키마, backup 기준
- `AI-502-503-detailed-schema`
  - users / workflow_snapshots / generations / generation_outputs 상세 컬럼과 인덱스 기준
- `AI-504-audit-log-standard`
  - audit_logs action_type / status 표준값과 helper 설계 기준
- `AI-505-generation-metadata-persistence`
  - workflow snapshot upsert, `SUBMITTED` generation insert, `workflow_hash` / `created_by` / `prompt_summary` 선택 기준

### 최종 그림 / 종합 설계

- `MVP-ERD-overview`
  - `AI-801`까지 완료됐을 때의 목표 ERD와 상태 / 저장 모델 분리 기준
- `MVP-API-overview`
  - 로그인, 생성, polling, 결과 조회, 다운로드, 실패 로그까지 포함한 목표 API 세트

## AI-501에서 다시 봐야 하는 문서

### 필수 재참조

1. `AI-203-reverse-proxy`
   - `tools/infra/postgres/` 위치와 Compose 구조
2. `AI-402-config`
   - `POSTGRES_HOST`, `POSTGRES_PORT`, `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`
3. `AI-303-prompt-output-mapping`
   - 어떤 메타데이터를 DB에 남겨야 하는지
4. `AI-406-status-handling`
   - `execution_status`, `failure_reason` 기준
5. `AI-502-503-detailed-schema`
   - `full_prompt`, `failed_stage`, `workflow_hash`, `model_metadata_json` 등 상세 컬럼 기준
6. `AI-504-audit-log-standard`
   - `audit_logs.action_type`, `audit_logs.status`, audit helper 기준
7. `AI-505-generation-metadata-persistence`
   - workflow snapshot upsert, generation 메타데이터 insert, summary / hash 기준

### AI-501 이후 DB에 바로 이어질 값

- `prompt_id`
- `workflow_name`
- `workflow_hash`
- `execution_status`
- `failure_reason`
- `failed_stage`
- `prompt_summary`
- `full_prompt`
- `output filename`
- `output subfolder`
- `output type`
- `mime_type`
- 이후 `image_url`

## AI-501 기준 권장 진행 순서

1. 이 문서 확인
2. `AI-402-config`에서 Postgres env 이름 재확인
3. `AI-406-status-handling`에서 상태 모델 재확인
4. `tools/infra/postgres/init/001_init_schema.sql` 기준으로 최소 스키마 작성
5. `AI-502`, `AI-503`, `AI-504`에서 상세 컬럼 확장

## 현재 DB 구현 방향

- AI 도구 DB는 `tools/infra` 기준 Docker Postgres를 사용한다.
- 게임 서버 DB와는 **별도 컨테이너 / 별도 DB 문맥**으로 본다.
- 개발용 VSCode 접속은 `docker-compose.override.yml`에서만 `55432:5432`를 연다.
- 초기 스키마 source of truth는 Spring JPA 자동 생성이 아니라
  `tools/infra/postgres/init/001_init_schema.sql`로 둔다.

## 관련 파일

- `tools/infra/docker-compose.yml`
- `tools/infra/docker-compose.override.yml`
- `tools/infra/postgres/README.md`
- `tools/infra/postgres/init/README.md`
- `tools/infra/postgres/init/001_init_schema.sql`
