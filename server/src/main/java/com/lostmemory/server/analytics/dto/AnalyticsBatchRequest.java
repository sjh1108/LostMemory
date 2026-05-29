package com.lostmemory.server.analytics.dto;

import com.fasterxml.jackson.annotation.JsonProperty;
import jakarta.validation.constraints.NotNull;

import java.util.List;
import java.util.UUID;

/**
 * 클라가 보내는 분석 이벤트 batch.
 *
 * session_id 는 클라 발행 UUID (한 게임 실행 단위) — 매칭룸 sessions.session_id 와 무관.
 */
public record AnalyticsBatchRequest(
        @JsonProperty("client_version") String clientVersion,
        @NotNull @JsonProperty("session_id") UUID analyticsSessionId,
        @NotNull @JsonProperty("events")    List<AnalyticsEventDto> events
) {}
