# AI-504 audit_logs 표준화 및 helper 기준

## 목적

`audit_logs` 테이블이 단순 자유 텍스트 로그가 아니라, 이후 조회와 장애 분석에서 필터링 가능한 최소 표준값을 갖도록 고정한다.

## action_type 표준값

- `LOGIN`
- `GENERATE`
- `UPLOAD`
- `DOWNLOAD`

## status 표준값

- `SUCCESS`
- `FAILED`

### 판단

- timeout은 별도 status로 두지 않고 `FAILED`로 기록한다.
- 세부 실패 사유는 `detail_json.failureReason`과 같은 payload 내부 값으로 구분한다.

## 현재 helper 설계

코드 기준 공통 계약:

- `common/audit/AuditRecorder`
- `common/audit/LoggingAuditRecorder`
- `common/audit/AuditActionType`
- `common/audit/AuditStatus`

현재는 DB insert가 아니라 구조화된 application log만 남기고,
이후 `audit_logs` 테이블 적재 시 구현체만 교체하는 방식으로 간다.

## 현재 generate 실패 payload 예시

- `promptId`
- `executionStatus`
- `failureReason`
- `completed`
- `statusText`
- `messageTypes`
- `internalMessage`
- `outputFound`

## SQL 기준

- `audit_logs.action_type`는 네 개 표준값만 허용
- `audit_logs.status`는 `SUCCESS`, `FAILED`만 허용

## 다음 단계

- `AI-505`, `AI-507`, `AI-508`, `AI-701`에서 필요한 위치마다 `AuditRecorder` 호출 확장
- 이후 DB write 단계에서 `LoggingAuditRecorder`를 DB 저장 구현체로 교체

## 검증

- `./gradlew.bat --no-daemon test`
  - `AuditRecorder` 시그니처 변경 후 `GenerationHistoryServiceTest`까지 통과 확인
- `./gradlew.bat --no-daemon bootJar`
  - 새 audit enum / helper 기준으로 패키징 성공 확인
- `docker compose exec -T postgres psql -U ai_tool -d ai_tool -c "\d+ audit_logs"`
  - `action_type`, `status` check constraint와 인덱스 확인
