# Postgres

## 목적

이 폴더는 AI 도구용 Postgres 운영 기준을 둔다.

`AI-203`에서 DB는 SQLite가 아니라 Postgres를 우선 기준으로 고정한다. 이유는 아래와 같다.

- 운영 데스크탑 또는 EC2에서 Docker Compose로 띄우기 쉽다.
- 여러 사용자가 동시에 생성/조회할 때 SQLite보다 충돌 가능성이 낮다.
- 이후 백엔드, 조회 화면, 감사 로그가 늘어나도 구조를 유지하기 쉽다.

## 현재 범위

현재 `docker-compose.yml`은 Postgres 컨테이너만 준비한다.

아직 실제 테이블 생성 SQL은 넣지 않는다. 생성 이력, output 메타데이터, workflow snapshot 스키마는 백엔드 구현 작업에서 확정한다.

## 폴더 구조

```text
tools/infra/postgres/
  README.md
  init/
    README.md
```

## 데이터 볼륨

Postgres 데이터는 compose named volume인 `postgres_data`에 저장된다.

```yaml
volumes:
  postgres_data:
```

이 방식은 컨테이너를 지워도 volume을 지우지 않으면 DB 데이터가 남는다.

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
