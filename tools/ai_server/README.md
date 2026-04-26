# AI Tool Server

`tools/ai_server` is the implementation location fixed by `AI-401-01`.

This project is a Spring Boot bootstrap for the ComfyUI internal tool backend. The current draft covers the `AI-401` scope only:

- separate project location under `tools/`
- `GET /api/health` endpoint for deployment and proxy checks
- common `ApiResponse` format
- base package/config structure
- Gradle build file and Dockerfile draft

## Current endpoints

- `GET /api/health`
- `GET /api/actuator/health`
- `GET /api/swagger-ui/index.html`

## Run

```bash
./gradlew test
./gradlew bootRun
```

The server uses `/api` as its servlet context path, so the health check URL is `http://localhost:8080/api/health`.

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
- ComfyUI, S3, and Postgres values are already reserved in `application.yml` and `.env.example` as skeleton configuration only.
- This project is intentionally separate from the root `server/` project.
