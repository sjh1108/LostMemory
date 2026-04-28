# 1차 MVP 목표 API 정리

## 목적

이 문서는 `컴피유아이-1차-mvp-통합-작업표.csv` 기준으로 1차 MVP 작업이 전부 완료됐을 때 외부에 보이게 될 **최종 API 윤곽**을 정리한 문서다.

범위:

- 헬스체크
- 로그인 / 로그아웃 / 현재 사용자
- 생성 요청 / 생성 상태 polling
- 결과 목록 / 상세 / 다운로드 / workflow JSON 다운로드
- 관리자 실패 로그 조회
- 내부적으로 호출하는 ComfyUI upstream API

즉, 현재 일부 구현이 끝난 endpoint만 모은 문서가 아니라 `AI-801`까지 완료됐을 때 어떤 API 세트가 남는지 정리하는 문서다.

## 설계 전제

- 외부 앱 API는 Spring Boot가 제공한다.
- 실제 이미지 생성은 ComfyUI가 수행한다.
- ComfyUI와의 통신은 MVP 기준 `/prompt + /history/{prompt_id}` polling 전략을 유지한다.
- 상태 조회 API는 비즈니스 상태를 `200 OK + body.status`로 표현한다.
- 결과 이미지는 private S3 기준으로 보고, 다운로드는 **백엔드 프록시 다운로드**를 우선 추천한다.
- 인증은 `Basic Auth + 앱 로그인` 2단 구조로 시작한다.
- 앱 로그인은 서버 세션 또는 HttpOnly 쿠키 기반이 가장 단순한 MVP 방향이다.

## API 한눈에 보기

### 공용

- `GET /api/health`

### 인증

- `POST /api/auth/login`
- `POST /api/auth/logout`
- `GET /api/auth/me`

### 생성 명령 / 상태

- `POST /api/generation-requests`
- `GET /api/generation-requests/{promptId}`

### 결과 조회

- `GET /api/results`
- `GET /api/results/{generationId}`
- `GET /api/results/{generationId}/download`
- `GET /api/results/{generationId}/workflow`

### 관리자 / 운영

- `GET /api/admin/failures`
- `GET /api/admin/failures/{generationId}`

### 내부 upstream (ComfyUI)

- `POST /prompt`
- `GET /history/{prompt_id}`
- `GET /queue`
- `POST /interrupt`
- `GET /system_stats`
- `GET /object_info`

## 1. 공용 API

### `GET /api/health`

목적:

- 프록시 / 앱 서버 / 기본 배포 상태 확인

응답 예시:

```json
{
  "success": true,
  "code": "OK",
  "data": {
    "service": "ai-server",
    "status": "UP"
  }
}
```

## 2. 인증 API

### `POST /api/auth/login`

목적:

- 내부 사용자 로그인

요청 예시:

```json
{
  "username": "ai_admin",
  "password": "********"
}
```

응답 예시:

```json
{
  "success": true,
  "code": "OK",
  "data": {
    "userId": 1,
    "username": "ai_admin",
    "displayName": "AI Admin",
    "role": "ADMIN"
  }
}
```

메모:

- MVP는 세션 쿠키 기반이 단순하다.
- `users.password_hash`, `users.role`, `users.is_active`를 함께 사용한다.

### `POST /api/auth/logout`

목적:

- 세션 종료

응답 예시:

```json
{
  "success": true,
  "code": "OK",
  "data": {
    "loggedOut": true
  }
}
```

### `GET /api/auth/me`

목적:

- 현재 로그인 사용자 정보 확인

응답 예시:

```json
{
  "success": true,
  "code": "OK",
  "data": {
    "userId": 1,
    "username": "ai_admin",
    "displayName": "AI Admin",
    "role": "ADMIN"
  }
}
```

## 3. 생성 요청 / 상태 API

### `POST /api/generation-requests`

목적:

- 생성 요청 시작

처리 흐름:

1. 로그인 사용자 확인
2. workflow snapshot 저장 또는 upsert
3. generation row 생성
4. ComfyUI `/prompt` 호출
5. `prompt_id` 저장
6. `SUBMITTED` 상태 반환

요청 예시:

```json
{
  "workflowId": "pixel-art-character-v1",
  "prompt": "pixel art mage character, blue robe, staff, front view",
  "userId": 1
}
```

응답 예시:

```json
{
  "success": true,
  "code": "ACCEPTED",
  "data": {
    "requestId": "f56f6b5d-5f82-4a83-bad4-8188c5f6f901",
    "promptId": "d4bc5cf9-f555-430c-a32b-b61e4c8b4bb6",
    "workflowId": "pixel-art-character-v1",
    "status": "SUBMITTED",
    "submittedAt": "2026-04-28T13:35:00+09:00"
  }
}
```

메모:

- 이 endpoint는 **명령(command)** 성격이다.
- 결과 다운로드용 최종 리소스 ID는 `generationId`지만, 생성 직후 ComfyUI 추적 키는 `promptId`다.

### `GET /api/generation-requests/{promptId}`

목적:

- `promptId` 기준 상태 polling

비즈니스 상태:

- `SUBMITTED`
- `RUNNING`
- `SUCCEEDED`
- `FAILED`
- `TIMED_OUT`

성공 예시:

```json
{
  "success": true,
  "code": "OK",
  "data": {
    "promptId": "d4bc5cf9-f555-430c-a32b-b61e4c8b4bb6",
    "executionStatus": "SUCCEEDED",
    "storageStatus": "UPLOADED",
    "workflowName": "Z-Image-turbo-test3-ksampler-change",
    "filename": "AI301_Test3_PromptSample_00001_.png",
    "subfolder": "",
    "outputType": "output",
    "mimeType": "image/png",
    "message": "이미지 생성이 완료되었습니다."
  }
}
```

실패 예시:

```json
{
  "success": true,
  "code": "OK",
  "data": {
    "promptId": "d4bc5cf9-f555-430c-a32b-b61e4c8b4bb6",
    "executionStatus": "FAILED",
    "storageStatus": "NOT_STARTED",
    "failureReason": "OUTPUT_MISSING",
    "failedStage": "OUTPUT_DISCOVERY",
    "message": "생성은 완료되었지만 결과 파일을 찾지 못했습니다."
  }
}
```

메모:

- timeout / failed도 HTTP 오류가 아니라 `200 OK + executionStatus`로 표현한다.
- `/history` fetch 연속 실패, output 없음, non-success terminal status도 body 상태로 내려준다.

## 4. 결과 조회 API

### `GET /api/results`

목적:

- 최근 생성 결과 목록 조회

추천 query parameter:

- `createdBy`
- `username`
- `limit`
- `executionStatus` (선택)
- `storageStatus` (선택)

예시:

`GET /api/results?createdBy=1&limit=20`

응답 예시:

```json
{
  "success": true,
  "code": "OK",
  "data": {
    "items": [
      {
        "generationId": 101,
        "promptId": "d4bc5cf9-f555-430c-a32b-b61e4c8b4bb6",
        "workflowName": "Z-Image-turbo-test3-ksampler-change",
        "promptSummary": "pixel art mage character",
        "executionStatus": "SUCCEEDED",
        "storageStatus": "UPLOADED",
        "thumbnailUrl": "https://example-s3/.../thumb.png",
        "createdBy": {
          "userId": 1,
          "username": "ai_admin",
          "displayName": "AI Admin"
        },
        "createdAt": "2026-04-28T13:35:00+09:00"
      }
    ],
    "limit": 20
  }
}
```

메모:

- 기본 정렬은 최신순이다.
- `AI-601-03` 상태 배지는 `execution_status + storage_status` 조합에서 파생한다.

### `GET /api/results/{generationId}`

목적:

- 결과 상세 조회

응답 예시:

```json
{
  "success": true,
  "code": "OK",
  "data": {
    "generationId": 101,
    "promptId": "d4bc5cf9-f555-430c-a32b-b61e4c8b4bb6",
    "workflowName": "Z-Image-turbo-test3-ksampler-change",
    "workflowVersion": "v1",
    "modelName": "z_image_turbo_bf16.safetensors",
    "executionStatus": "SUCCEEDED",
    "storageStatus": "UPLOADED",
    "promptSummary": "pixel art mage character",
    "fullPrompt": "pixel art mage character, blue robe, staff, front view",
    "generatorHost": "gpu-desktop-01",
    "createdAt": "2026-04-28T13:35:00+09:00",
    "completedAt": "2026-04-28T13:36:01+09:00",
    "outputs": [
      {
        "outputIndex": 0,
        "filename": "AI301_Test3_PromptSample_00001_.png",
        "mimeType": "image/png",
        "imageUrl": "https://example-s3/.../AI301_Test3_PromptSample_00001_.png"
      }
    ]
  }
}
```

메모:

- 상세 화면은 `workflow_snapshots`, `generations`, `generation_outputs` 조인이 필요하다.
- `fullPrompt`는 접이식 UI 대상이다.

### `GET /api/results/{generationId}/download`

목적:

- 원본 이미지 다운로드

추천 방식:

- **백엔드 프록시 다운로드**

이유:

- 파일명 / content-type / 권한 정책을 한 곳에서 통제하기 쉽다.
- private S3 기준으로도 일관되게 동작한다.

응답:

- `Content-Type: image/png`
- `Content-Disposition: attachment; filename="AI301_Test3_PromptSample_00001_.png"`

### `GET /api/results/{generationId}/workflow`

목적:

- 재현용 workflow JSON 다운로드

응답:

- `Content-Type: application/json`
- `Content-Disposition: attachment; filename="Z-Image-turbo-test3-ksampler-change.json"`

## 5. 관리자 / 운영 API

### `GET /api/admin/failures`

목적:

- 최근 실패 generate / upload 로그 조회

추천 query parameter:

- `actionType`
- `period`
- `createdBy`
- `limit`

예시:

`GET /api/admin/failures?actionType=GENERATE&period=7d&limit=50`

응답 예시:

```json
{
  "success": true,
  "code": "OK",
  "data": {
    "items": [
      {
        "occurredAt": "2026-04-28T13:40:00+09:00",
        "actionType": "GENERATE",
        "status": "FAILED",
        "promptId": "d4bc5cf9-f555-430c-a32b-b61e4c8b4bb6",
        "username": "ai_admin",
        "workflowName": "Z-Image-turbo-test3-ksampler-change",
        "failedStage": "HISTORY_POLL",
        "failureReason": "POLL_TIMEOUT",
        "errorSummary": "생성 시간이 예상보다 오래 걸려 요청을 종료했습니다."
      }
    ]
  }
}
```

### `GET /api/admin/failures/{generationId}`

목적:

- 실패 건 상세 조회

권장 반환 항목:

- generation 메타데이터
- execution / storage status
- failure reason
- failed stage
- user message
- internal message
- audit payload
- workflow name
- prompt summary / full prompt

## 6. 내부 upstream ComfyUI API

### MVP 필수

- `POST /prompt`
- `GET /history/{prompt_id}`

### 운영 보조

- `GET /queue`
- `POST /interrupt`
- `GET /system_stats`
- `GET /object_info`

메모:

- 외부 사용자에게는 Spring Boot API만 노출한다.
- ComfyUI upstream API는 내부 구현 세부사항으로 본다.

## 7. 상태 모델

### 실행 상태

- `SUBMITTED`
- `RUNNING`
- `SUCCEEDED`
- `FAILED`
- `TIMED_OUT`

### 저장 상태

- `NOT_STARTED`
- `UPLOADING`
- `UPLOADED`
- `UPLOAD_FAILED`

### 추천 표시 규칙

- `executionStatus = RUNNING` -> 생성 중
- `executionStatus = SUCCEEDED`, `storageStatus = UPLOADING` -> 업로드 중
- `executionStatus = SUCCEEDED`, `storageStatus = UPLOADED` -> 완료
- `executionStatus = SUCCEEDED`, `storageStatus = UPLOAD_FAILED` -> 업로드 실패
- `executionStatus = FAILED` / `TIMED_OUT` -> 실패

즉, 화면 배지는 단일 DB 컬럼이 아니라 두 상태를 조합해서 만드는 쪽이 자연스럽다.

## 8. 권한 경계

### 일반 사용자

- 로그인
- 생성 요청
- 상태 조회
- 결과 목록
- 결과 상세
- 이미지 다운로드
- workflow JSON 다운로드

### 관리자

- 실패 목록
- 실패 상세
- 운영 로그 확인
- 필요 시 interrupt / system stats 연계

## 9. 이 문서를 다시 볼 시점

- `AI-505`, `AI-507`, `AI-508` 구현 전
- `AI-601 ~ AI-605` API 설계 / 구현 전
- `AI-701`, `AI-702` 로그인 / 실패 로그 API 설계 전
- `AI-801` end-to-end 검증 시나리오 작성 전
