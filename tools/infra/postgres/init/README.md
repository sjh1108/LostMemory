# Postgres init

## 목적

이 폴더는 Postgres 컨테이너가 처음 생성될 때 자동 실행할 SQL 파일을 둘 자리다.

`docker-compose.yml`에서 아래처럼 마운트한다.

```yaml
./postgres/init:/docker-entrypoint-initdb.d:ro
```

## 현재 상태

`AI-203`에서는 초기 SQL을 만들지 않는다.

아직 AI 도구 백엔드의 실제 테이블 스키마가 확정되지 않았기 때문이다. 스키마 확정 전 임의 SQL을 넣으면 이후 백엔드 작업에서 다시 뒤집을 가능성이 높다.

## 나중에 들어갈 수 있는 파일

예시는 아래와 같다.

```text
001_create_generation_tables.sql
002_create_indexes.sql
003_seed_initial_users.sql
```

## 주의

Postgres 공식 이미지의 init SQL은 데이터 디렉터리가 비어 있는 첫 초기화 시점에만 자동 실행된다.

이미 volume이 만들어진 뒤 SQL을 추가해도 자동 재실행되지 않는다. 운영 중 스키마 변경은 Flyway, Liquibase, 백엔드 migration, 또는 수동 migration 절차로 따로 관리해야 한다.
