# AI Tool Server

`tools/ai_server` is the implementation location fixed by `AI-401-01`.

This project is a Spring Boot bootstrap for the ComfyUI internal tool backend. The current draft now covers `AI-401` through `AI-403`:

- separate project location under `tools/`
- `GET /api/health` endpoint for deployment and proxy checks
- common `ApiResponse` format
- base package/config structure
- Gradle build file and Dockerfile draft
- minimal generation request DTO, controller, validation, and Swagger exposure

## Current endpoints

- `GET /api/health`
- `GET /api/actuator/health`
- `GET /api/swagger-ui/index.html`
- `POST /api/generation-requests`

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

`POST /api/generation-requests` is the `AI-403` draft endpoint. It validates `workflowId`, `prompt`, and optional `userId`, then returns `202 Accepted` with a server-generated draft request id. Actual ComfyUI `/prompt` integration starts in `AI-404`.

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
- This project is intentionally separate from the root `server/` project.
