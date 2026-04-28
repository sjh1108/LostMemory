# AI-405 /history 폴링 서비스 구현

`AI-405`에서는 `AI-404`에서 받은 `prompt_id`를 기준으로 ComfyUI `/history/{promptId}`를 polling하고, 완료 시 첫 output image 메타데이터를 읽어오는 success-path를 구현한다.

## 구현 범위

- `GenerationHistoryService` 추가
- polling 상수 확정
  - `interval = 2초`
  - `soft warning = 60초`
  - `hard timeout = 120초`
- `GenerationHistoryParser` 추가
- `GET /api/generation-requests/{promptId}` endpoint 추가
- `/history` raw response debug logging 추가

## 핵심 기준

- polling 종료 기준은 우선 `history[promptId]`가 존재하고 `status.completed == true`인 경우로 둔다.
- `outputs["8"]` 같은 특정 node id는 하드코딩하지 않고 `outputs` map을 순회해 첫 번째 image entry를 읽는다.
- `workflow_name`은 `/history` 응답에서 읽지 않는다.
- `AI-405`는 success-path polling과 parser까지만 다루고, timeout/failed 상태 맵핑은 `AI-406`으로 넘긴다.

## 현재 응답 형식

- `promptId`
- `completed`
- `statusText`
- `outputImage.filename`
- `outputImage.subfolder`
- `outputImage.type`
- `messageTypes`

## 예외 처리 기준

- `/history` 요청 실패:
  - `502`
  - `COMFYUI_HISTORY_FETCH_FAILED`
- polling timeout:
  - `504`
  - `HISTORY_POLL_TIMEOUT`

## 테스트

- `GenerationHistoryParserTest`
  - `outputs` map 순회 기반 first image 파싱 검증
- `GenerationHistoryServiceTest`
  - pending -> completed polling 검증
  - timeout 예외 검증
- `GenerationControllerTest`
  - `GET /generation-requests/{promptId}` 성공 응답 검증
