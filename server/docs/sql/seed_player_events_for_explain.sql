-- =====================================================================
-- player_events 더미 시드 (100,000 row) — query EXPLAIN ANALYZE 용
-- =====================================================================
--   * 실행 전제: users 에 user_id 1~16 (테스트 계정) 존재
--   * 분포:
--     - event_type: session_start 5%, session_end 5%, stage_entered 30%,
--                   stage_cleared 25%, player_died 35%
--     - event_time: 최근 14일 균등 분포
--     - stage_id: 1F_HALL, 1F_BOSS, 2F_HALL, 2F_BOSS, ... 8종
--     - payload: event_type 별 대표 키 (random 한 값)
--   * 실행 후 ANALYZE player_events; 권장 (통계 갱신)
-- =====================================================================

INSERT INTO player_events (
    user_id,
    analytics_session_id,
    event_type,
    event_time,
    stage_id,
    payload,
    client_version
)
SELECT
    1 + (random() * 15)::int AS user_id,
    gen_random_uuid()        AS analytics_session_id,
    CASE
        WHEN r < 0.05 THEN 'session_start'
        WHEN r < 0.10 THEN 'session_end'
        WHEN r < 0.40 THEN 'stage_entered'
        WHEN r < 0.65 THEN 'stage_cleared'
        ELSE               'player_died'
    END AS event_type,
    now() - (random() * interval '14 days') AS event_time,
    (ARRAY['1F_HALL','1F_BOSS','2F_HALL','2F_BOSS','3F_HALL','3F_BOSS','TOWN','LOBBY'])
        [1 + (random() * 7)::int] AS stage_id,
    CASE
        WHEN r >= 0.65 THEN jsonb_build_object(
            'cause_enemy_id',  (ARRAY['Bertha','Goblin','Slime','Skeleton','Dragon'])[1+(random()*4)::int],
            'cause_pattern_id', (ARRAY['DashAttack','RangedShot','Grab','AOE'])[1+(random()*3)::int],
            'run_elapsed_sec', (random() * 600)::int
        )
        WHEN r >= 0.40 AND r < 0.65 THEN jsonb_build_object(
            'duration_sec', (random() * 300)::int
        )
        ELSE NULL
    END AS payload,
    '0.1.0' AS client_version
FROM (
    SELECT generate_series(1, 100000) AS i, random() AS r
) gen;

ANALYZE player_events;


-- =====================================================================
-- 검증 query — 핸드오프 doc §6 의 3개 query
-- 각 query 앞에 EXPLAIN ANALYZE 붙여서 plan + 실측 시간 확인.
-- =====================================================================

-- 1. 일별 DAU
EXPLAIN ANALYZE
SELECT DATE(event_time) AS day, COUNT(DISTINCT user_id) AS dau
FROM player_events
WHERE event_type = 'session_start'
GROUP BY 1 ORDER BY 1 DESC;

-- 2. 사망 hotspot (1주일)
EXPLAIN ANALYZE
SELECT stage_id, payload->>'cause_enemy_id' AS enemy, COUNT(*) AS deaths
FROM player_events
WHERE event_type = 'player_died'
  AND event_time > now() - interval '7 days'
GROUP BY 1, 2 ORDER BY 3 DESC LIMIT 20;

-- 3. 세션 길이 분포
EXPLAIN ANALYZE
SELECT
    user_id,
    analytics_session_id,
    EXTRACT(EPOCH FROM (MAX(event_time) - MIN(event_time)))/60 AS session_min
FROM player_events
GROUP BY 1, 2;


-- =====================================================================
-- 정리 — 시드 데이터 제거
-- =====================================================================
-- TRUNCATE player_events;
