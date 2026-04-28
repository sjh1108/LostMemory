# Postgres init

## 목적

이 폴더는 Postgres 컨테이너가 처음 생성될 때 자동 실행할 SQL 파일을 둘 자리다.

`docker-compose.yml`에서 아래처럼 마운트한다.

```yaml
./postgres/init:/docker-entrypoint-initdb.d:ro
```

## 현재 상태

`AI-501`부터 최소 스키마를 이 폴더의 SQL 파일로 관리한다.

현재 source of truth는 아래 파일이다.

```text
001_init_schema.sql
```

이 파일은 AI 도구용 Docker Postgres가 **처음 초기화될 때만** 자동 실행된다.

## 나중에 들어갈 수 있는 파일

예시는 아래와 같다.

```text
001_init_schema.sql
002_indexes.sql
003_seed_initial_users.sql
```

## 주의

Postgres 공식 이미지의 init SQL은 데이터 디렉터리가 비어 있는 첫 초기화 시점에만 자동 실행된다.

이미 volume이 만들어진 뒤 SQL을 수정하거나 추가해도 자동 재실행되지 않는다.

따라서 개발 중 스키마를 처음부터 다시 적용하려면 아래 중 하나가 필요하다.

1. `postgres_data` volume 삭제 후 컨테이너 재생성
2. 수동 `psql` 적용
3. 이후 단계에서 migration 도구 도입
