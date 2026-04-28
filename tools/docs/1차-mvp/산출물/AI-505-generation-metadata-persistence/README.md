# AI-505 generation 메타데이터 저장 구현

## 목적

`POST /api/generation-requests`가 ComfyUI에 `/prompt`를 submit하는 것에서 끝나지 않고, 같은 시점의 workflow snapshot과 generation 메타데이터를 Postgres에 실제 저장하도록 연결한다.

이번 단계의 핵심은 다음 세 가지를 코드로 고정하는 것이다.

- `workflow_hash`는 어떤 기준으로 계산할지
- `created_by`를 지금 어떻게 저장할지
- `prompt_summary`를 어떤 역할의 값으로 만들지

## 이번 단계에서 고정한 선택

### 1. `workflow_hash`

- 기준: runtime 값이 들어가기 전 source workflow template JSON
- 방식: canonicalized JSON -> `SHA-256`

### 왜 이렇게 했는가

- `requestId`, random `seed`, `filename_prefix`, `client_id`를 그대로 hash에 넣으면 요청마다 hash가 달라진다.
- 그러면 같은 workflow를 재사용하지 못하고 `workflow_snapshots`가 매 요청마다 새 row로 늘어난다.
- 따라서 hash는 "실행 시점 payload"가 아니라 "workflow 자체"를 대표해야 한다.

### 구현 메모

- `PromptAssemblyService`는 source template와 submit payload를 분리한다.
- source template는 DB snapshot / hash 기준으로 쓴다.
- submit payload는 source template를 복사한 뒤 prompt, seed, filename prefix, client_id를 주입한다.

## 2. `created_by`

- 현재 기준: `nullable`

### 왜 이렇게 했는가

- 아직 `AI-701`, `AI-702` 로그인 라인이 붙지 않았다.
- 현재 API의 `userId`는 optional 문자열이고, DB의 `created_by`는 `users.id`를 가리키는 숫자 FK다.
- 이 시점에 억지로 값을 채우면 실제 인증 사용자가 아닌 임시 문자열을 잘못 저장하게 된다.

### 구현 메모

- `WorkflowSnapshotEntity.createdBy`
- `GenerationEntity.createdBy`

둘 다 현재는 `null`로 저장한다.

## 3. `prompt_summary`

- 기준: full prompt에서 사람이 목록에서 읽기 좋은 한 줄 요약 텍스트

### 왜 JSON 일부를 넣지 않았는가

- `prompt_summary`는 사람용 목록 텍스트이지, 구조화 메타데이터 저장 칸이 아니다.
- workflow JSON 일부를 summary에 우겨 넣으면 길어지고, UI 가독성이 나빠지고, `workflow_json`과 역할이 겹친다.
- 구조화 정보는 `workflow_json`, `model_metadata_json`에 남기고, summary는 짧은 문장으로 유지하는 편이 낫다.

### 규칙

- 공백 normalize
- trim
- 최대 100자
- 비어 있으면 `prompt unavailable`

## 구현 범위

- JPA / DataSource auto-configuration 활성화
- test profile H2 datasource 추가
- `WorkflowSnapshotEntity`, `GenerationEntity` 추가
- `WorkflowSnapshotRepository`, `GenerationRepository` 추가
- `WorkflowHashCalculator` 추가
- `PromptSummaryExtractor` 추가
- `WorkflowSnapshotService`로 workflow snapshot upsert 구현
- `GenerationMetadataService`로 `SUBMITTED` generation insert 구현
- `GenerationService`에서 `/prompt` 성공 직후 DB 저장 연결

## 저장 시점

### workflow snapshot

- `/prompt` submit 전에 저장 또는 재사용
- 이유:
  - snapshot은 workflow 자체 메타데이터라 요청 실패와 무관하게 재사용 가능하다
  - submit 전에 실패하면 외부 ComfyUI 호출 자체를 막을 수 있다

### generation row

- `/prompt` 응답에서 `prompt_id`를 받은 직후 insert
- 저장 값:
  - `prompt_id`
  - `workflow_snapshot_id`
  - `execution_status = SUBMITTED`
  - `prompt_summary`
  - `full_prompt`
  - `user_message`
  - `internal_message`

## 이번 단계에서 아직 하지 않는 것

- `/history` 완료 후 `completed_at`, `failure_reason`, `failed_stage` update
- `generation_outputs` 저장
- S3 upload 결과 저장
- `image_url` update
- `storage_status` 분리 저장

이 값들은 `AI-507`, `AI-508`에서 이어진다.

## 검증

- `./gradlew.bat --no-daemon test`
  - H2 test datasource 기준 전체 테스트 통과
  - `GenerationControllerTest`에서 `/generation-requests` 호출 후 실제 `generations`, `workflow_snapshots` row 저장 확인
  - `PromptSummaryExtractorTest`, `WorkflowHashCalculatorTest` 추가
- `./gradlew.bat --no-daemon bootJar`
  - JPA 활성화 후에도 패키징 정상

## 관련 파일

- `tools/ai_server/src/main/java/com/lostmemory/aiserver/generation/PromptAssemblyService.java`
- `tools/ai_server/src/main/java/com/lostmemory/aiserver/generation/PromptAssemblyResult.java`
- `tools/ai_server/src/main/java/com/lostmemory/aiserver/generation/WorkflowHashCalculator.java`
- `tools/ai_server/src/main/java/com/lostmemory/aiserver/generation/PromptSummaryExtractor.java`
- `tools/ai_server/src/main/java/com/lostmemory/aiserver/generation/WorkflowSnapshotEntity.java`
- `tools/ai_server/src/main/java/com/lostmemory/aiserver/generation/GenerationEntity.java`
- `tools/ai_server/src/main/java/com/lostmemory/aiserver/generation/WorkflowSnapshotService.java`
- `tools/ai_server/src/main/java/com/lostmemory/aiserver/generation/GenerationMetadataService.java`
- `tools/ai_server/src/main/java/com/lostmemory/aiserver/generation/GenerationService.java`
- `tools/ai_server/src/main/java/com/lostmemory/aiserver/repository/WorkflowSnapshotRepository.java`
- `tools/ai_server/src/main/java/com/lostmemory/aiserver/repository/GenerationRepository.java`
- `tools/ai_server/src/main/resources/application.yml`
- `tools/ai_server/src/test/resources/application-test.yml`
