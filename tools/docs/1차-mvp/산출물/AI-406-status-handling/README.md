# AI-406 timeout 및 failed 상태 처리 구현

## 목적

`AI-405`까지 구현한 `/history/{promptId}` success-path polling 위에 timeout, failed, audit logging hook, 실행 상태 분리를 추가해 이후 DB 저장과 실패 로그 조회 작업의 기준을 고정한다.

## 이번 단계에서 확정한 기준

### 1. 제출 상태와 실행 상태를 분리한다

- 제출 상태: `GenerationRequestStatus`
  - `RECEIVED`
  - `SUBMITTED`
- 실행 상태: `GenerationExecutionStatus`
  - `SUBMITTED`
  - `RUNNING`
  - `SUCCEEDED`
  - `FAILED`
  - `TIMED_OUT`

`AI-406`에서는 `/history` 기준 실행 상태만 새 enum으로 분리하고, S3 업로드 같은 저장 상태는 여기 넣지 않는다.

### 2. 성공 조건은 세 가지를 모두 만족해야 한다

- `status.completed == true`
- `status.status_str == "success"`
- output image 존재

즉 `completed == true`만으로 성공을 판정하지 않는다.

### 3. 실패 사유는 상위 분류로 먼저 묶는다

- `HISTORY_FETCH_FAILED`
- `COMFYUI_REPORTED_FAILURE`
- `OUTPUT_MISSING`
- `POLL_TIMEOUT`
- `UNKNOWN_FAILURE`

local ComfyUI의 `/prompt` validation 실패처럼 stderr에만 원인이 남는 세부 케이스는 이후 단계에서 더 세분화할 수 있지만, `AI-406`에서는 상위 failure reason으로 먼저 고정한다.

### 4. `/history` fetch는 연속 3회 실패해야 terminal failure로 본다

- 일시적인 네트워크 흔들림이나 순간 응답 실패를 바로 terminal failure로 보지 않는다.
- 연속 실패 카운트는 성공 응답이 오면 reset한다.

### 5. timeout과 failed는 HTTP가 아니라 body status로 표현한다

- `GET /generation-requests/{promptId}`는 비즈니스 상태를 `200 OK`로 반환한다.
- timeout도 `TIMED_OUT`
- output 없음도 `FAILED`
- 실제 시스템 예외만 `5xx`

### 6. 감사 로그는 공통 계약만 분리하고 지금은 logger로 남긴다

- 공통 계약: `common/audit/AuditRecorder`
- 현재 구현: `common/audit/LoggingAuditRecorder`
- generation 전용 판단 로직:
  - `GenerationStatusResolver`
  - `GenerationFailureMessageResolver`

즉 `audit_logs` 테이블이 아직 없더라도, 이후 DB 적재로 교체할 수 있는 연결 지점을 먼저 마련한다.

### 7. polling 방식은 초기 MVP에서 동기로 유지한다

- `GET /generation-requests/{promptId}` 호출 안에서 polling을 수행한다.
- background worker, queue, scheduler는 `AI-406` 범위에 넣지 않는다.

## 사용자 메시지 / 내부 로그 메시지

### 정상 상태

- `SUBMITTED`
  - 사용자: `생성 요청을 접수했습니다.`
  - 내부: `Prompt submitted to ComfyUI.`
- `RUNNING`
  - 사용자: `이미지 생성 중입니다.`
  - 내부: `ComfyUI history indicates execution is still running.`
- `SUCCEEDED`
  - 사용자: `이미지 생성이 완료되었습니다.`
  - 내부: `Generation completed successfully.`

### 실패 상태

- `HISTORY_FETCH_FAILED`
  - 사용자: `생성 결과를 확인하는 중 문제가 발생했습니다. 잠시 후 다시 시도해주세요.`
  - 내부: `ComfyUI /history request failed repeatedly while polling promptId={promptId}.`
- `COMFYUI_REPORTED_FAILURE`
  - 사용자: `이미지 생성에 실패했습니다.`
  - 내부: `ComfyUI reported a non-success terminal status. promptId={promptId}, statusText={statusText}`
- `OUTPUT_MISSING`
  - 사용자: `생성은 완료되었지만 결과 파일을 찾지 못했습니다.`
  - 내부: `ComfyUI history completed without a discoverable output image. promptId={promptId}`
- `POLL_TIMEOUT`
  - 사용자: `생성 시간이 예상보다 오래 걸려 요청을 종료했습니다.`
  - 내부: `ComfyUI history polling timed out before reaching a terminal success state. promptId={promptId}`
- `UNKNOWN_FAILURE`
  - 사용자: `이미지 생성에 실패했습니다.`
  - 내부: `Generation failed for an unknown reason. promptId={promptId}, statusText={statusText}`

## 변경 파일

- `tools/ai_server/src/main/java/com/lostmemory/aiserver/common/audit/AuditRecorder.java`
- `tools/ai_server/src/main/java/com/lostmemory/aiserver/common/audit/LoggingAuditRecorder.java`
- `tools/ai_server/src/main/java/com/lostmemory/aiserver/generation/GenerationExecutionStatus.java`
- `tools/ai_server/src/main/java/com/lostmemory/aiserver/generation/GenerationFailureReason.java`
- `tools/ai_server/src/main/java/com/lostmemory/aiserver/generation/GenerationFailureMessageResolver.java`
- `tools/ai_server/src/main/java/com/lostmemory/aiserver/generation/GenerationStatusResolver.java`
- `tools/ai_server/src/main/java/com/lostmemory/aiserver/generation/GenerationHistoryService.java`
- `tools/ai_server/src/main/java/com/lostmemory/aiserver/generation/GenerationHistoryResponse.java`
- `tools/ai_server/src/main/java/com/lostmemory/aiserver/generation/GenerationController.java`
- `tools/ai_server/src/test/java/com/lostmemory/aiserver/generation/GenerationHistoryServiceTest.java`
- `tools/ai_server/src/test/java/com/lostmemory/aiserver/generation/GenerationControllerTest.java`

## 검증

- `./gradlew.bat --no-daemon test`
- `./gradlew.bat --no-daemon bootJar`

## 다음 단계

- `AI-501 ~ AI-505`에서 DB schema와 generation row 저장 구조를 연결한다.
- `AI-507`, `AI-508`에서 output 파일 탐색과 S3 업로드 상태를 별도 storage status로 이어간다.
- `AI-702-01`에서 지금 만든 audit logging hook를 실제 실패 로그 조회 기능으로 확장한다.
