# Postgres

## 목적

이 폴더는 AI 도구용 Postgres 운영 기준을 둔다.

`AI-203`에서 DB는 SQLite가 아니라 Postgres를 우선 기준으로 고정한다. 이유는 아래와 같다.

- 운영 데스크탑 또는 EC2에서 Docker Compose로 띄우기 쉽다.
- 여러 사용자가 동시에 생성/조회할 때 SQLite보다 충돌 가능성이 낮다.
- 이후 백엔드, 조회 화면, 감사 로그가 늘어나도 구조를 유지하기 쉽다.

## 현재 범위

현재 `docker-compose.yml`은 Postgres 컨테이너를 준비하고, `AI-501`부터는 최소 스키마 init SQL도 같이 관리한다.

## 폴더 구조

```text
tools/infra/postgres/
  README.md
  init/
    README.md
    001_init_schema.sql
```

## 데이터 볼륨

Postgres 데이터는 compose named volume인 `postgres_data`에 저장된다.

```yaml
volumes:
  postgres_data:
```

이 방식은 컨테이너를 지워도 volume을 지우지 않으면 DB 데이터가 남는다.

### 개발 기준 백업 / 복구 규칙

1. 기본 저장은 `postgres_data` named volume 유지
2. 사람이 보관하는 백업은 raw volume copy가 아니라 `pg_dump` 사용
3. 스키마를 크게 바꾸기 전에는 dump를 한 번 남긴다

예시:

```powershell
docker compose exec postgres pg_dump -U ai_tool -d ai_tool > backup_ai_tool.sql
```

복구 예시:

```powershell
Get-Content .\backup_ai_tool.sql | docker compose exec -T postgres psql -U ai_tool -d ai_tool
```

### 중요한 주의

- `docker-entrypoint-initdb.d` SQL은 **빈 volume 첫 초기화 시점에만 자동 실행**된다.
- 이미 데이터가 들어간 volume에는 init SQL을 추가해도 자동 재실행되지 않는다.
- 그래서 초기 MVP 단계에서는 필요 시 volume을 지우고 다시 띄우는 방식이 가능하지만, 이후에는 migration 절차가 필요하다.

## 직접 외부 노출하지 않는 이유

Postgres는 외부 브라우저나 Nginx가 직접 접근하는 대상이 아니다.

정상 흐름은 아래와 같다.

```text
사용자 -> Nginx -> AI 도구 백엔드 -> Postgres
```

그래서 compose에서는 Postgres를 `private` network에만 붙인다. 나중에 AI 도구 백엔드 compose service가 생기면 백엔드도 같은 `private` network에 붙이면 된다.

## 실제 비밀번호

`.env.example`에는 placeholder만 둔다.

실제 운영에서는 `tools/infra/.env`에 강한 비밀번호를 넣고 Git에 올리지 않는다.

## VSCode 기준 연결

개발 환경에서는 `docker-compose.override.yml`에서만 Postgres 포트를 연다.

- Host: `localhost`
- Port: `55432`
- Database: `ai_tool`
- Username: `ai_tool`
- Password: `tools/infra/.env` 값
