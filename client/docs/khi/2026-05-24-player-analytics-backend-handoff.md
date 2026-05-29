# Player Analytics — Backend 핸드오프 문서

날짜: 2026-05-24
발신: 클라이언트 측 (김회인)
수신: 백엔드 개발자
관련: `2026-05-24-player-analytics-tracking-plan.md` (전체 plan)

## 1. 요청 요약

플레이어 행동 데이터 수집 시스템을 자체 백엔드에 구축. 클라이언트는 in-game 이벤트를 batch 로 POST 하고, 백엔드는 이를 PostgreSQL 에 append-only 로 적재. 집계 query 와 대시보드 시각화는 추후 단계.

이 문서는 **백엔드 측에서 구현해야 할 endpoint, 데이터 모델, 운영 정책** 을 정리. 일부 항목은 백엔드 측 결정에 위임 (섹션 7 참조).

## 2. 클라이언트가 보낼 데이터

### 2.1 전송 방식

- **HTTPS POST** `/api/analytics/events`
- **인증**: 기존 JWT (`Authorization: Bearer <token>`) 그대로 사용. user_id 는 token 에서 추출 — 클라이언트 payload 에 별도로 user_id 안 넣음 (위변조 방지).
- **batch**: 클라이언트는 5초 또는 50개 이벤트 누적 시마다 batch POST. 단일 POST 안에 여러 이벤트.
- **session_end**: 강제 flush (Application.quitting 에서 동기 POST 시도). 손실 가능성 있음 → 백엔드는 손실 5~10% 가정.

### 2.2 Request body 스키마

```json
{
  "client_version": "0.1.4",
  "session_id": "550e8400-e29b-41d4-a716-446655440000",
  "events": [
    {
      "event_type": "stage_entered",
      "event_time": "2026-05-24T13:42:11.234Z",
      "stage_id": "1F_BOSS",
      "payload": { "party_size": 2 }
    },
    {
      "event_type": "player_died",
      "event_time": "2026-05-24T13:43:55.012Z",
      "stage_id": "1F_BOSS",
      "payload": {
        "cause_enemy_id": "Bertha",
        "cause_pattern_id": "DashAttack",
        "run_id": "f1a2...",
        "run_elapsed_sec": 412.3
      }
    }
  ]
}
```

- `session_id`: 클라가 발행 (UUID v4). 한 게임 실행 = 한 session_id.
- `event_time`: ISO-8601, 클라 시계 기준 (UTC). 서버 측 보정 필요할 수 있음 (섹션 7 참조).
- `stage_id`: 문자열 enum. `"TOWN"`, `"LOBBY"`, `"1F_BOSS"` 등. null 가능.
- `payload`: type 마다 다른 자유 JSON. 아래 2.3 참조.

### 2.3 event_type 목록 (Tier 1 — MVP)

| event_type | 발생 시점 | payload 필드 |
|---|---|---|
| `session_start` | 클라 entry scene 진입 | `party_size` (1=솔로/2~4=멀티), `device` (PC/모바일 등) |
| `session_end` | Application.quitting / disconnect | `last_stage_id`, `reason` (quit/disconnect/crash), `duration_sec` |
| `stage_entered` | 새 stage 입장 | `party_size`, `prev_stage_id` |
| `stage_cleared` | stage 클리어 트리거 | `duration_sec` |
| `player_died` | 플레이어 사망 | `cause_enemy_id`, `cause_pattern_id` (nullable), `run_id`, `run_elapsed_sec` |

Tier 2/3 event_type 은 plan 문서 참조 (`boss_attempt`, `boss_cleared`, `run_completed`, `network_disconnect`, `shop_visited`, `weapon_picked` 등). **백엔드는 새 event_type 이 추가돼도 코드 변경 없이 적재 가능한 구조** 권장 (payload 는 JSONB).

### 2.4 Response

- **성공**: 202 Accepted, body 없음 (또는 `{"accepted": N}`)
- **실패 4xx**: 클라이언트는 해당 batch 폐기, 다음 batch 부터 재개. 재시도 안 함.
- **실패 5xx / 타임아웃**: 클라이언트는 in-memory 큐에 다시 enqueue, 최대 3회 재시도 (exponential backoff). 그 이후는 폐기.

## 3. 데이터 모델 제안

> 백엔드 측이 더 적절한 스키마 알면 변경 자유. 본 제안은 단순함 우선.

```sql
CREATE TABLE player_events (
  id              BIGSERIAL PRIMARY KEY,
  user_id         UUID NOT NULL,
  session_id      UUID NOT NULL,
  event_type      VARCHAR(48) NOT NULL,
  event_time      TIMESTAMPTZ NOT NULL,
  received_time   TIMESTAMPTZ NOT NULL DEFAULT now(),
  stage_id        VARCHAR(64),
  payload         JSONB,
  client_version  VARCHAR(16)
);

CREATE INDEX ix_pe_user_time  ON player_events(user_id, event_time);
CREATE INDEX ix_pe_type_time  ON player_events(event_type, event_time);
CREATE INDEX ix_pe_session    ON player_events(session_id);
```

특징:
- **append-only**, UPDATE/DELETE 없음.
- `event_time` (클라 시계) 과 `received_time` (서버 시계) 둘 다 보관 — 클라 시계 신뢰 못할 때 fallback.
- 월 단위 partition 권장 (PG declarative partitioning) — 데이터량 증가 대비.

## 4. 적재 경로 (권장)

```
Unity Client
   ↓ POST /api/analytics/events (JWT)
Spring @RestController
   ↓ JWT 검증 + 이벤트 list 즉시 Redis List push
   ↓ 202 Accepted 즉시 반환 (DB write 안 기다림)
Redis (player_events:queue)
   ↓ @Scheduled(fixedDelay=5000) EventFlushScheduler
PostgreSQL bulk insert (saveAll)
```

이유:
- POST 응답 latency 짧게 유지 (게임 hitch 방지)
- 순간 트래픽 spike 흡수 (게임 시작 시 동시 session_start 다수)
- Redis 이미 인프라에 있음 (build.gradle 의 spring-boot-starter-data-redis)

**단순 구현이면 Redis 생략하고 controller 에서 직접 saveAll 도 OK** — 초기 트래픽 적을 때는 over-engineering 일 수 있음. 백엔드 판단.

## 5. 비기능 요구사항

| 항목 | 기준 |
|---|---|
| POST 응답 latency | p95 < 200ms (게임 hitch 방지) |
| 데이터 손실 허용치 | 10% 이하 (Application.quitting 시 손실 가능성 포함) |
| 트래픽 예상 (MVP) | 동시 접속 ~50명 기준 평균 10 events/s, peak 100 events/s |
| 데이터 retention | 최소 90일. 그 이후는 백엔드 정책에 따라 archive/삭제 |
| 인증 실패 처리 | 401 반환. 클라는 재로그인 trigger 안 함 (단순 폐기) |
| client_version 검증 | 안 함. 모든 버전 수용. |

## 6. 어드민/조회 (선택, 후순위)

대시보드 자체는 Grafana + PostgreSQL data source 로 백엔드 측 monitoring 스택에서 직접 시각화 권장. 별도 REST 조회 endpoint 는 필요 시 추가.

샘플 query 들 (참고용):

```sql
-- 일별 DAU
SELECT DATE(event_time) AS day, COUNT(DISTINCT user_id) AS dau
FROM player_events
WHERE event_type = 'session_start'
GROUP BY 1 ORDER BY 1 DESC;

-- 사망 hotspot (어느 stage 의 어느 적에게 가장 많이 죽었는지)
SELECT stage_id, payload->>'cause_enemy_id' AS enemy, COUNT(*) AS deaths
FROM player_events
WHERE event_type = 'player_died'
  AND event_time > now() - interval '7 days'
GROUP BY 1, 2 ORDER BY 3 DESC LIMIT 20;

-- 세션 길이 분포 (분 단위)
SELECT
  user_id, session_id,
  EXTRACT(EPOCH FROM (MAX(event_time) - MIN(event_time)))/60 AS session_min
FROM player_events
GROUP BY 1, 2;
```

## 7. 백엔드 측 결정 위임 항목

다음은 백엔드 측에서 결정해 주세요. 클라이언트는 결정 따라가면 됨.

1. **endpoint URL 경로** — `/api/analytics/events` 제안. 기존 API prefix 규칙에 맞춰 변경 OK.
2. **Redis 큐 사용 여부** — MVP 트래픽 적으면 생략, 직접 saveAll 도 가능.
3. **flush 주기** — 권장 5초. 백엔드 부담 보고 조정.
4. **batch 최대 크기** — 클라는 기본 50개로 끊음. 백엔드가 더 큰/작은 값 원하면 알려주세요 (HTTP 헤더 또는 사양 문서로 명시).
5. **PostgreSQL partition 전략** — 월 partition 권장하나 데이터량 보고 결정.
6. **event_time vs received_time** 신뢰 우선순위 — 집계 query 에서 어느 컬럼 쓸지.
7. **JWT 토큰 만료 시 401 처리** — 클라는 401 받으면 batch 폐기. 더 정교한 처리 필요하면 알려주세요.
8. **Schema migration 도구** — Flyway/Liquidbase 등 기존 사용 도구 따라감.

## 8. 의도적 비범위

- A/B 테스트
- 실시간 alerting
- PII (닉네임/이메일) 수집 — user_id (UUID) 만
- GDPR 삭제 요청 처리 — ssafy 데모 단계라 별도 구현 X
- 클라이언트 → 백엔드 schema 협상/versioning — `client_version` 컬럼만 적재, 호환성은 application code 가 책임

## 9. 진행 순서 제안

1. **백엔드**: 테이블 + entity + controller (POST endpoint) 까지 — 더미 응답으로 클라 통합 테스트 가능하게.
2. **클라이언트**: `AnalyticsClient` + 5초 flush + `session_start`/`session_end` 발화 — 백엔드 endpoint 와 첫 통합.
3. **백엔드**: 실제 DB insert + (선택) Redis flush 분리.
4. **클라이언트**: 나머지 Tier 1 이벤트 (`stage_entered`/`cleared`/`player_died`) 발화 hook 추가.
5. **백엔드**: 샘플 query 확인 + Grafana dashboard 1개.

각 단계 사이에 한 번씩 sync. 1단계 후 통합 테스트하면 schema 미스매치 빨리 발견.

## 10. 참고 — 전체 plan / 추적할 metric 13개 목록

상세한 metric 우선순위 (Tier 1/2/3) 및 의도적 제외 항목은 `2026-05-24-player-analytics-tracking-plan.md` 참조.

---

**질문/이견** 있으면 김회인에게 ping. 본 문서는 살아있는 spec — 협의 후 직접 수정 환영.
