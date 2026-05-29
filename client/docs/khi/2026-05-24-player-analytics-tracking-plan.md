# 플레이어 로그 추적 시스템 — Plan

날짜: 2026-05-24
저장 위치 정책: `client/docs/khi/` (사용자 feedback 메모리).

## Context

플레이어 행동 데이터를 수집해서 (a) 게임 밸런싱 신호 (b) 멀티 협력 가치 검증 (c) 단기 리텐션 측정 기반을 만들고자 함. 현재 분석 인프라 전무. 서버(Spring Boot 3.5 + PostgreSQL + Redis + Spring Actuator + monitoring 스택)는 이미 존재하므로 외부 SaaS 도입 없이 자체 적재.

요청 원본:
- 유저간 플레이타임 (동시 접속/세션 단위)
- 모든 유저 누적 플레이타임
- 한 세션 평균 머무름 시간
- 어디서 나가는지 (town vs 특정 단계)
- 이외 추천 metrics

본 plan 은 위 요청 + 추천 metrics 를 **중요도 Tier 로 묶고** 백엔드/적재 전략 결정 + 단계별 구현 순서를 정리한다.

## 추적 metric — 중요도 순

> ROI = 구현 비용 대비 게임 디자인/운영 의사결정에 미치는 영향. Tier1 부터 순차 구현 권장.

### Tier 1 — 즉시 가치 (MVP, 1주 내 가능)

| metric | 정의 | 활용 |
|---|---|---|
| `session_start` / `session_end` | 클라 진입~종료 (또는 disconnect) 이벤트. user_id + timestamp + reason | 세션 길이, 동시 접속, 누적 플레이타임 모두 derive |
| `quit_location` | 세션 종료 직전 stage_id (또는 "TOWN"/"LOBBY") | "어디서 나가는지" 원본 요구 |
| `stage_entered` / `stage_cleared` | stage_id, party_size, timestamp | 진행 funnel — 어느 단계에서 유저 줄어드는지 |
| `player_died` | stage_id + cause(enemy/pattern id) + run_id + run_elapsed_sec | **밸런싱 핵심 신호** — 어떤 적/공격에 가장 많이 죽는지 |

세션 길이 / 누적 플레이타임 / 동시 접속은 모두 `session_start`+`session_end` 두 이벤트만 있으면 SQL aggregate 로 계산 가능 → 별도 metric 불필요.

### Tier 2 — 게임 디자인 신호 (Tier1 안정화 후)

| metric | 정의 | 활용 |
|---|---|---|
| `boss_attempt` / `boss_cleared` | boss_id + party_size + duration_sec | 보스별 클리어율, 평균 도전 시간 → 보스 난이도 조정 |
| `run_completed` | run_id + total_duration + final_stage + outcome(victory/death/quit) | 평균 런 길이, 완주율 |
| `solo_vs_multi` | session_start 의 party_size 필드로 derive | 멀티 가치 검증 |
| `network_disconnect` | stage_id + role(host/guest) + reason | 끊김 hotspot — 안정성 우선순위 |

### Tier 3 — 장기 운영 지표 (서비스 단계 진입 시)

| metric | 정의 |
|---|---|
| D1/D7 retention | session_start 의 user_id+date 로 derive |
| `shop_visited` / `shop_purchased` | item_id, price | 경제 디자인 |
| `weapon_picked` / `skill_picked` | item_id, run_id | 픽률 분포 → 밸런싱 |
| `first_death_time` / `first_boss_reach_time` | onboarding 곡선 |
| `crash_or_exception` | 위치 + stack trace 일부 | 안정성 (Tier1 disconnect 와 보완) |

### 의도적 제외

- 정밀 이동 거리, 공격 명중률, 평균 회피 사용 등 frame-level metric → 데이터량 폭주, ROI 낮음. 필요해지면 Tier 3 이후.
- A/B 테스트, funnel 자동 시각화 등 SaaS 형 dashboard 자동화 → 초기 단계 over-engineering.

## 백엔드 결정

**자체 Spring Boot 서버 + PostgreSQL 적재.** 외부 SaaS (Firebase/GameAnalytics) 도입 X.

근거:
- 서버에 이미 JPA / PostgreSQL / Redis / Actuator / monitoring 스택 갖춤 (`server/build.gradle`, `docker-compose.monitoring.yml`)
- JWT 기반 user_id 이미 발급 — 익명 ID 별도 관리 불필요
- ssafy 프로젝트 데모/평가 시 "자체 백엔드 활용" 가산점
- 외부 SaaS 는 신규 SDK 패키지 + 인증/PII 분리 부담

### 데이터 모델 (events table 1장 + read-model view)

```sql
-- 단일 events 테이블에 type 으로 분기 (append-only, partition by month)
CREATE TABLE player_events (
  id            BIGSERIAL PRIMARY KEY,
  user_id       UUID NOT NULL,
  session_id    UUID NOT NULL,
  event_type    VARCHAR(48) NOT NULL,  -- 'session_start', 'stage_entered', 'player_died', ...
  event_time    TIMESTAMPTZ NOT NULL,
  stage_id      VARCHAR(64),
  payload       JSONB,                  -- type 별 추가 필드 (cause, party_size, boss_id 등)
  client_version VARCHAR(16)
);
CREATE INDEX ix_pe_user_time ON player_events(user_id, event_time);
CREATE INDEX ix_pe_type_time ON player_events(event_type, event_time);
```

집계 view (세션 길이, 누적 플레이타임 등) 는 별도 materialized view 또는 batch job 으로 derive — events table 은 raw append-only.

### 적재 경로

```
Unity Client
   ↓ HTTPS POST /api/analytics/events (batch)
Spring Boot @RestController
   ↓ enqueue
Redis List (player_events:queue)
   ↓ @Scheduled flush (5s)
PostgreSQL bulk insert
```

- 클라는 이벤트를 in-memory 버퍼에 모았다가 **5초마다 또는 50개마다 batch POST**. 동기 호출 시 게임 hitch 위험.
- 서버는 즉시 응답 후 Redis 큐로 enqueue → DB 부담 분산.
- `session_end` 는 batch flush 강제 (Application.quitting 에서 sendBeacon 형 호출).

## 클라이언트 구현 — 최소 진입점

신규 클래스 1개 + 기존 hook 후킹:

| 신규/수정 | 책임 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Analytics/AnalyticsClient.cs` (신규) | in-memory 버퍼 + 5s flush coroutine + HTTPS POST. `LogEvent(type, stage_id, payload)` 단일 진입점 |
| `Assets/_Project/Scripts/Runtime/Analytics/SessionLifecycleTracker.cs` (신규) | Awake: `session_start` 발화. `Application.quitting` + 멀티 disconnect 콜백에서 `session_end` |
| 기존 stage 진입/클리어 hook (탐색 필요) | `stage_entered`/`stage_cleared` 발화 1줄 추가 |
| `Health.OnDeath` 또는 Player damage 콜백 (탐색 필요) | `player_died` + cause 추적 — 마지막 받은 damage 의 source enemy_id 사용 |

> 실제 hook 지점 (어느 StageManager / DungeonProgressTracker 등) 은 본 plan 실행 시점에 Explore 로 추가 조사. 현 plan 은 hook **수**가 적다는 것 (Tier1 기준 4개) 만 보장.

## 서버 구현 — 최소 진입점

| 신규 | 책임 |
|---|---|
| `analytics/dto/PlayerEventBatchDto.java` | 클라 batch payload DTO |
| `analytics/controller/AnalyticsController.java` | POST `/api/analytics/events` — JWT 검증 + Redis enqueue |
| `analytics/scheduler/EventFlushScheduler.java` | @Scheduled(fixedDelay=5000) — Redis → JPA bulk save |
| `analytics/entity/PlayerEvent.java` | JPA entity (events table) |
| `db/migration/Vxxx__create_player_events.sql` (Flyway 사용 시) | 테이블 생성 |

기존 monitoring 스택 (Grafana 가 docker-compose.monitoring.yml 에 있을 가능성) 에 PostgreSQL data source 추가만 하면 즉시 시각화 가능.

## 단계별 구현 순서 (Tier1 기준 — 1주 목표)

1. **DB schema + entity** — `player_events` 테이블 + JPA entity + repository
2. **서버 endpoint** — POST `/api/analytics/events` + JWT 추출해서 user_id binding + Redis enqueue
3. **서버 flush scheduler** — Redis → bulk insert
4. **클라 AnalyticsClient** — 버퍼 + 5s flush + Application.quitting beacon
5. **session_start / session_end 발화** — SessionLifecycleTracker
6. **stage_entered / stage_cleared / player_died 발화** — 기존 manager 후킹 (Explore 단계 필요)
7. **quit_location** = session_end payload 에 현재 stage_id 포함 (별도 이벤트 불요)
8. **Grafana dashboard 1개** — 세션 길이 분포 + stage funnel + 사망 hotspot

## Verification

- 단일 클라이언트 1회 플레이:
  - DB 에 `session_start` (1) → `stage_entered` (N) → `player_died` (M) → `session_end` (1) 순서로 append 되는지 SQL 로 확인
  - `session_end - session_start` = 측정한 세션 길이 (±5s)
- 클라 강제 종료 (Alt+F4) 시 마지막 batch 가 손실되는 비율 측정. 손실 5% 이하면 수용. 그 이상이면 `OnApplicationFocus(false)` 에서도 flush 추가.
- 멀티 host/guest 동시 입장 → 각자 별도 session_id 발행, 같은 stage_id 에 `stage_entered` 2건.
- Grafana 에서 "최근 24시간 stage funnel" 쿼리 결과가 게임 내 손동작과 일치.

## 의도적 비범위 (out of scope)

- A/B 테스트 인프라
- 실시간 alerting (단순 dashboard 만)
- 클라 device/OS/지역 분포 — 필요해지면 session_start payload 에 1줄 추가로 확장 가능
- PII 마스킹/GDPR — ssafy 데모 단계라 user_id (UUID) 만 사용. 닉네임/이메일은 events 에 미포함.
