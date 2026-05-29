package com.lostmemory.server.analytics.service;

import com.fasterxml.jackson.annotation.JsonProperty;
import com.lostmemory.server.analytics.entity.EventType;

import java.time.Instant;
import java.util.UUID;

/**
 * Redis 큐에 직렬화되어 들어가는 이벤트 단위.
 *
 * 클라 DTO 와 분리된 별도 record — 큐에는 entity 생성에 필요한 모든 정보 (user_id 포함) 가 있어야 한다.
 * JWT 에서 추출한 user_id 가 ingest 단계에서 합쳐지고, flush 단계에서 그대로 PlayerEvent 로 변환된다.
 */
public record QueuedPlayerEvent(
        @JsonProperty("user_id")              Long userId,
        @JsonProperty("analytics_session_id") UUID analyticsSessionId,
        @JsonProperty("event_type")           EventType eventType,
        @JsonProperty("event_time")           Instant eventTime,
        @JsonProperty("stage_id")             String stageId,
        @JsonProperty("payload")              String payload,
        @JsonProperty("client_version")       String clientVersion
) {}
