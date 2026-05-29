# Player Analytics — 적재 인프라 운영 가이드

> **Spec source of truth**: 클라이언트 측 핸드오프 문서 `런_통계_기획서.md` (사용자 Downloads, 김회인 발신, 2026-05-24).
> 이 가이드는 그 spec 의 백엔드 측 운영 문서. **기획서는 변경 불가** — 백엔드 결정/갭/추가 동작은 모두 이 문서에 반영.

플레이어 행동 이벤트 (session_start / session_end / stage_entered / stage_cleared / player_died) 를 클라이언트로부터 받아 PostgreSQL `player_events` 테이블에 append-only 적재하는 파이프라인의 운영 가이드.

---

## 1. 데이터 흐름

```
Unity Client
   │  POST /api/analytics/events  (JWT, batch 1~100건)
   ▼
AnalyticsController
   │  AnalyticsValidator  (batch size / event_time ±24h / payload 8KB / event_type whitelist)
   ▼
AnalyticsIngestService
   │  JWT user_id 주입 → JSON 직렬화
   ▼
PlayerEventRedisQueue (LPUSH analytics:events:queue)
   ▼
EventFlushScheduler (5초 주기 RPOP)
   │  역직렬화 → User.getReferenceById → PlayerEvent.create
   │  saveAll(트랜잭션). 실패 시 재시도 3회 + DLQ
   ▼
PostgreSQL player_events
```

응답 흐름은 `Redis LPUSH 까지만 동기` — PG 적재는 별도 thread 가 5초마다 처리. 클라이언트는 큐 적재 직후 202 받음.

---

## 2. 설정값

`application.yaml` 의 `analytics.flush.*` (env 로 override 가능):

| 키 | env | 기본값 | 의미 |
|---|---|---:|---|
| `analytics.flush.interval-ms` | `ANALYTICS_FLUSH_INTERVAL_MS` | 5000 | flush scheduler 실행 주기 (ms) |
| `analytics.flush.batch-size`  | `ANALYTICS_FLUSH_BATCH_SIZE`  | 200  | 1회 RPOP 개수 |
| `analytics.flush.retry-max`   | `ANALYTICS_FLUSH_RETRY_MAX`   | 3    | saveAll 실패 시 재시도 횟수 |

---

## 3. Redis 키 스키마

| 키 | 타입 | 의미 |
|---|---|---|
| `analytics:events:queue` | LIST | 메인 큐. LPUSH (적재) / RPOP (flush) |
| `analytics:events:dlq` | LIST | dead-letter. 재시도 3회 모두 실패한 batch 격리 |

### Redis AOF 권장
큐 데이터 손실 방지를 위해 AOF persistence 활성화 권장. 비활성 상태에서 Redis 다운 = 미적재 batch 손실.

확인:
```bash
redis-cli CONFIG GET appendonly        # yes 권장
redis-cli CONFIG GET appendfsync       # everysec 권장
```

---

## 4. PostgreSQL 테이블

`player_events` (schema.sql §16):

| 컬럼 | 타입 | 의미 |
|---|---|---|
| `event_id` | BIGINT IDENTITY | PK |
| `user_id` | BIGINT FK | users.user_id. ON DELETE CASCADE |
| `analytics_session_id` | UUID | 클라 발행, 한 게임 실행 단위. 매칭룸 sessions.session_id 와 무관 |
| `event_type` | VARCHAR(48) | session_start / session_end / stage_entered / stage_cleared / player_died |
| `event_time` | TIMESTAMPTZ | 클라 시계. 집계 기본 |
| `received_time` | TIMESTAMPTZ DEFAULT now() | 서버 시계. lag 분석용 |
| `stage_id` | VARCHAR(64) nullable | 1F_HALL, 1F_BOSS, ... |
| `payload` | JSONB nullable | event_type 별 자유 JSON |
| `client_version` | VARCHAR(16) | 클라 빌드 버전 |

인덱스:
- `idx_pe_user_time` — `(user_id, event_time)`
- `idx_pe_type_time` — `(event_type, event_time)`
- `idx_pe_session`   — `(analytics_session_id)`

---

## 5. DLQ 처리

DLQ 에 들어간 batch 는 자동 재처리되지 않음 — 운영자가 수동 점검.

확인:
```bash
redis-cli LLEN analytics:events:dlq
redis-cli LRANGE analytics:events:dlq 0 9         # 앞 10건 미리보기
```

복구 옵션:
- **A. 메인 큐로 되돌리기** (DB 측 문제가 해결된 경우):
  ```bash
  redis-cli RPOPLPUSH analytics:events:dlq analytics:events:queue
  # 반복 실행 또는 스크립트
  ```
- **B. 영구 격리** (역직렬화 실패 등 구조 문제):
  ```bash
  redis-cli DEL analytics:events:dlq                # 또는 다른 키로 백업 후 삭제
  ```

---

## 6. 검증 query (핸드오프 doc §6)

### DAU (일별)
```sql
SELECT DATE(event_time) AS day, COUNT(DISTINCT user_id) AS dau
FROM player_events
WHERE event_type = 'session_start'
GROUP BY 1 ORDER BY 1 DESC;
```

### 사망 hotspot
```sql
SELECT stage_id, payload->>'cause_enemy_id' AS enemy, COUNT(*) AS deaths
FROM player_events
WHERE event_type = 'player_died'
  AND event_time > now() - interval '7 days'
GROUP BY 1, 2 ORDER BY 3 DESC LIMIT 20;
```

### 세션 길이 분포
```sql
SELECT
    user_id,
    analytics_session_id,
    EXTRACT(EPOCH FROM (MAX(event_time) - MIN(event_time)))/60 AS session_min
FROM player_events
GROUP BY 1, 2;
```

EXPLAIN ANALYZE 검증용 더미 시드는 `docs/sql/seed_player_events_for_explain.sql` 참조 (100k row 생성).

---

## 7. 새 event_type 추가 절차

화이트리스트 정책이라 enum 확장 PR 1개 필요.

순서:
1. `EventType` enum 에 새 상수 추가 (대문자)
2. (선택) `AnalyticsValidator` 의 validation 룰에 새 type 별 payload 검증 추가
3. 클라 측 핸드오프 doc 의 §2.3 표에 새 type / 발생 시점 / payload 필드 명시

---

## 8. 적재 동작 검증 (로컬)

```bash
# 1. 서버 + Redis + PG 기동
./gradlew bootRun

# 2. JWT 발급
curl -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"loginId":"testuser1","password":"..."}'

# 3. batch POST
curl -X POST http://localhost:8080/api/analytics/events \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{
    "client_version": "0.1.0",
    "session_id": "550e8400-e29b-41d4-a716-446655440000",
    "events": [
      {"event_type":"session_start","event_time":"<ISO-8601 now>","stage_id":null,"payload":{"party_size":1}}
    ]
  }'
# → 202 { "success": true, "data": { "accepted": 1 } }

# 4. Redis 큐 확인
redis-cli LRANGE analytics:events:queue 0 -1

# 5. 5초 후 PG 적재 확인
psql -c "SELECT COUNT(*) FROM player_events;"
```

---

## 9. 향후 운영 항목

- **partition 도입** — 정식 출시 직전 별 티켓. 월별 partition + 90일 retention (오래된 partition DROP)
- **수료 직전 백업** — `pg_dump --table=player_events ... | gzip > player_events_YYYYMMDD.sql.gz` 1회
- **Tier 2/3 이벤트 추가** — boss_attempt, run_completed, weapon_picked 등. 클라 hook 추가 시점에 enum 확장 PR

---

## 10. Unity 클라이언트 hook 발화 규칙

클라 측 `AnalyticsClient` (싱글톤 MonoBehaviour, `EnsureExists()` 로 자동 생성, `DontDestroyOnLoad`) 가 5종 이벤트를 batch 적재. 모두 **명시 호출** (씬 전환 자동 감지 X) — 호출 시점은 도메인 코드 (RunManager 등) 가 책임.

### 10.1 이벤트 발화 시점 (호출처 매핑)

| event_type | 호출 메서드 | 호출처 |
|---|---|---|
| `session_start` | `Track("session_start", ...)` | `TitleSceneController.LoadNextSceneAfterAuth` — 로그인 성공 직후 (`AnalyticsClient.EnsureExists()` 가 동시에 인스턴스 생성) |
| `session_end` | `FireSessionEnd("quit")` | `AnalyticsClient.OnApplicationQuit` — 동기 flush (`Task.Wait(2초)`) best-effort |
| `stage_entered` | `FireStageEntered(roomId, partySize, prevStageId)` | `RunManager` — RoomEntered 이벤트 구독 |
| `stage_cleared` | `FireStageCleared(roomId)` | `RunManager` — RoomCleared 이벤트 구독 |
| `player_died` | `FirePlayerDied(stageId, runId, runElapsedSec)` | `RunManager` — KhiDownController.Defeated* 이벤트 구독 |

### 10.2 stage_id 의 값 단위

stage_id 는 **방 (room) 단위** — `RoomData.RoomId` 가 그대로 들어감 (예: `"1F_HALL"`, `"1F_BOSS"`).
씬 (scene) 단위 (예: `"Dungeon_1F_1R"`) 가 아니라 RunManager 가 진행 중인 RoomEntered/Cleared payload 의 RoomId 사용.

→ PG `stage_id` 컬럼은 클라 측 RoomData 의 ID 체계. 던전 씬 이름과 별개. 분석 쿼리 시 RoomData ID 카탈로그 참조 필요.

### 10.3 cause_enemy_id / cause_pattern_id 자동 캐싱

`AnalyticsDamageTracker` (AnalyticsClient.EnsureExists 가 함께 부착) 가 `MMDamageTakenEvent` listener:

- 플레이어가 받은 마지막 데미지의 instigator → `LastEnemyId` 캐싱
- instigator 부모 chain 의 `AnalyticsPatternTag` 컴포넌트 → `LastPatternId` 매핑
- 둘 다 비면 instigator root GameObject 이름 (Clone 접미사 제거) 으로 fallback

`FirePlayerDied` 호출 시 이 정적 캐시 값이 payload 에 박힘 — 별도 인자 없이 자동.

### 10.4 LastStageId (last_stage_id 의 진실의 원천)

`AnalyticsClient.LastStageId` 는 `FireStageEntered` 호출 시에만 갱신되는 public sticky 변수.

- `FirePlayerDied` 에서 stage_id 인자로 사용 → 사망 시 stage_id 는 마지막 진입한 방 ID
- `FireSessionEnd` payload 의 `last_stage_id` 에도 사용 → 게임 종료 시 마지막 활동 방 보존

---

## 11. 트러블슈팅

| 증상 | 원인 | 대응 |
|---|---|---|
| `stage_id` 가 클라 측 RoomData ID 와 매칭 안 됨 | 클라 RoomData asset 의 RoomId 변경 또는 새 추가 | 클라 측 RoomData 카탈로그 확인 (`Assets/_Project/...RoomData.asset`) |
| `cause_enemy_id` 가 null 또는 `"(Clone)"` 접미사 포함 | `AnalyticsPatternTag` 미부착 + GameObject 이름이 비정상 | 보스/적 공격 controller GameObject 에 `AnalyticsPatternTag` 부착하면 깔끔. 또는 `AnalyticsDamageTracker.StripClone` 동작 확인 |
| `cause_pattern_id` 가 항상 null | `AnalyticsPatternTag.patternId` 가 모든 공격에서 비어있음 | 보스 공격 별로 PatternTag.patternId 채워야 (예: `"Bertha.LightAttack1"`) |
| `session_end.last_stage_id` 가 null | 던전 진입 한 번도 안 한 채 종료 (Title/Town 만) | 정상 동작. 마지막 방 진입이 없었으니 null |
| Editor freeze on Stop Play | OnApplicationQuit 의 sync flush deadlock | 김회인 본은 `Task.Wait(2초)` 패턴 사용. 그래도 freeze 면 클라 측 협의 |
| Console `POST /analytics/events 401` | `SessionApiClient.AccessToken` 비어있음 (로그인 전 또는 만료) | 정상. 로그인 후 발화는 적재됨 |
| 4xx → batch 폐기 vs 5xx → 재시도 | 의도된 동작 — 백엔드 validator 거부 (4xx) 는 batch 자체 문제라 재시도 무의미 | 폐기 batch 검토는 클라 Console 의 `[Analytics] POST … N — batch 폐기` 로그 |

---

## 12. 클라이언트 hook 발화 규칙 변경 시

이 §10 표는 클라 측 `AnalyticsClient.cs` / `AnalyticsApiClient.cs` / `AnalyticsDamageTracker.cs` / `AnalyticsPatternTag.cs` / `RunManager.cs` 동작과 동기. 클라 hook 로직 변경 시:

1. 위 §10.1~10.4 표 갱신
2. §11 트러블슈팅에 새 증상/대응 추가
3. 데이터 해석 영향 (예: stage_id 의미 변경) 있으면 §6 검증 query 도 함께 갱신
4. 클라 측 spec 변경이면 `런_통계_기획서.md` 도 함께 갱신 요청 (백엔드는 따라가는 입장)
