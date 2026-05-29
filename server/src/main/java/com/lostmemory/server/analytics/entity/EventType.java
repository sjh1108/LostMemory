package com.lostmemory.server.analytics.entity;

/**
 * 플레이어 행동 이벤트 종류.
 *
 * 화이트리스트 — 정의되지 않은 type 의 적재는 거부 (AnalyticsValidator).
 *
 * DB 컬럼은 player_events.event_type VARCHAR(48). Java 대문자 ↔ DB 소문자 (EventTypeConverter).
 */
public enum EventType {
    /** 클라 entry scene 진입. payload: party_size, device */
    SESSION_START,
    /** Application.quitting / disconnect. payload: last_stage_id, reason, duration_sec */
    SESSION_END,
    /** 새 stage 입장. payload: party_size, prev_stage_id */
    STAGE_ENTERED,
    /** stage 클리어 트리거. payload: duration_sec */
    STAGE_CLEARED,
    /** 플레이어 사망. payload: cause_enemy_id, cause_pattern_id, run_id, run_elapsed_sec */
    PLAYER_DIED
}
