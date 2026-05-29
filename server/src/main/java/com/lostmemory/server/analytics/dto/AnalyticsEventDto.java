package com.lostmemory.server.analytics.dto;

import com.fasterxml.jackson.annotation.JsonProperty;
import com.fasterxml.jackson.databind.JsonNode;

import java.time.Instant;

/**
 * batch 안의 단일 이벤트 (요청 DTO).
 *
 * payload 는 event_type 별 자유 JSON — JsonNode 로 받아 application 단에서 size 검증 후 그대로 직렬화.
 * stage_id 는 session_start/end 에선 null 허용.
 */
public record AnalyticsEventDto(
        @JsonProperty("event_type") String eventType,
        @JsonProperty("event_time") Instant eventTime,
        @JsonProperty("stage_id")   String stageId,
        @JsonProperty("payload")    JsonNode payload
) {}
