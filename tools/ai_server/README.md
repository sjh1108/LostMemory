# AI Tool Server

`tools/ai_server` is the implementation location fixed by `AI-401-01`.

This project is a Spring Boot bootstrap for the ComfyUI internal tool backend. The current draft now covers `AI-401` through `AI-405`:

- separate project location under `tools/`
- `GET /api/health` endpoint for deployment and proxy checks
- common `ApiResponse` format
- base package/config structure
- Gradle build file and Dockerfile draft
- minimal generation request DTO, controller, validation, and Swagger exposure
- ComfyUI `/prompt` template assembly and submit flow with `prompt_id` return
- ComfyUI `/history/{promptId}` polling and first output metadata parsing

## Current endpoints

- `GET /api/health`
- `GET /api/actuator/health`
- `GET /api/swagger-ui/index.html`
- `POST /api/generation-requests`
- `GET /api/generation-requests/{promptId}`

## Environment

Copy `.env.example` to `.env` before running the app. `AI-402` now treats the following values as required runtime configuration:

- `COMFYUI_BASE_URL`
- `POSTGRES_HOST`, `POSTGRES_PORT`, `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`
- `AWS_REGION`, `AWS_S3_BUCKET`, `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`

Optional profile override files:

- `.env.dev`
- `.env.prod`

## Run

```bash
./gradlew test
./gradlew bootRun
```

The server uses `/api` as its servlet context path, so the health check URL is `http://localhost:8080/api/health`.

`POST /api/generation-requests` now performs the `AI-404` submit flow. It validates `workflowId`, assembles a ComfyUI `/prompt` body from the current classpath template, submits it, and returns `202 Accepted` with both a server-side `requestId` and the ComfyUI `promptId`.

`GET /api/generation-requests/{promptId}` now performs the `AI-405` polling flow. It polls ComfyUI `/history/{promptId}` until the execution is completed, then returns the parsed first output image metadata and observed message types.

## Current structure

```text
tools/ai_server/
  build.gradle
  settings.gradle
  Dockerfile
  .env.example
  src/
    main/
      java/com/lostmemory/aiserver/
        config/
        common/
        generation/
        health/
        repository/
      resources/
        application.yml
        application-dev.yml
        application-prod.yml
    test/
      java/com/lostmemory/aiserver/
```

## Notes

- `spring-boot-starter-data-jpa` and PostgreSQL dependencies are already included, but datasource/JPA auto-configuration is intentionally excluded for now. Actual DB wiring starts in `AI-402` and `AI-501`.
- `AI-402` adds typed configuration properties, startup validation, and a reusable ComfyUI HTTP client bean.
- `AI-403` adds the minimal generation request API contract before actual ComfyUI submission logic.
- `AI-404` adds `PromptAssemblyService`, a classpath prompt template, real `/prompt` submit, `promptId` extraction, and debug logging for request/response payloads.
- `AI-405` adds `GenerationHistoryService`, `GenerationHistoryParser`, `/history` polling constants, and a success-path parser that reads the first output image by iterating the `outputs` map instead of hardcoding a node id.
- This project is intentionally separate from the root `server/` project.
