package com.lostmemory.server.analytics.service;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lostmemory.server.analytics.dto.AnalyticsEventDto;
import com.lostmemory.server.analytics.entity.EventType;
import com.lostmemory.server.global.exception.BusinessException;
import com.lostmemory.server.global.exception.ErrorCode;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Component;

import java.nio.charset.StandardCharsets;
import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.util.List;

/**
 * 분석 이벤트 batch 검증.
 *
 * 규칙:
 *  - events 1 ≤ N ≤ 100
 *  - event_type 은 EventType enum 안에 있는 값 (소문자 매칭)
 *  - event_time 은 현재 시각 ±24h 범위
 *  - payload JSON 직렬화 길이 ≤ 8KB
 *
 * 실패 시 BusinessException 으로 통일 — GlobalExceptionHandler 가 ErrorCode 의 HTTP status 그대로 응답.
 */
@Component
@RequiredArgsConstructor
public class AnalyticsValidator {

    static final int           MAX_BATCH_SIZE   = 100;
    static final int           MAX_PAYLOAD_BYTES = 8 * 1024;
    static final Duration      MAX_TIME_SKEW     = Duration.ofHours(24);

    private final ObjectMapper objectMapper;
    private final Clock        clock;

    /** batch 전체 검증 — 첫 위반에서 즉시 throw. */
    public void validate(List<AnalyticsEventDto> events) {
        if (events == null || events.isEmpty()) {
            throw new BusinessException(ErrorCode.ANALYTICS_BATCH_EMPTY);
        }
        if (events.size() > MAX_BATCH_SIZE) {
            throw new BusinessException(ErrorCode.ANALYTICS_BATCH_TOO_LARGE);
        }
        Instant now = clock.instant();
        for (AnalyticsEventDto event : events) {
            validateOne(event, now);
        }
    }

    /** 단일 event 검증. */
    public EventType validateOne(AnalyticsEventDto event, Instant now) {
        EventType type = parseEventType(event.eventType());
        validateEventTime(event.eventTime(), now);
        validatePayloadSize(event.payload());
        return type;
    }

    private EventType parseEventType(String raw) {
        if (raw == null || raw.isBlank()) {
            throw new BusinessException(ErrorCode.ANALYTICS_EVENT_TYPE_UNKNOWN);
        }
        try {
            return EventType.valueOf(raw.trim().toUpperCase());
        } catch (IllegalArgumentException e) {
            throw new BusinessException(ErrorCode.ANALYTICS_EVENT_TYPE_UNKNOWN);
        }
    }

    private void validateEventTime(Instant eventTime, Instant now) {
        if (eventTime == null) {
            throw new BusinessException(ErrorCode.ANALYTICS_EVENT_TIME_INVALID);
        }
        Duration skew = Duration.between(eventTime, now).abs();
        if (skew.compareTo(MAX_TIME_SKEW) > 0) {
            throw new BusinessException(ErrorCode.ANALYTICS_EVENT_TIME_SKEWED);
        }
    }

    private void validatePayloadSize(JsonNode payload) {
        if (payload == null || payload.isNull()) {
            return;
        }
        int bytes;
        try {
            bytes = objectMapper.writeValueAsString(payload).getBytes(StandardCharsets.UTF_8).length;
        } catch (JsonProcessingException e) {
            throw new BusinessException(ErrorCode.ANALYTICS_EVENT_TIME_INVALID);
        }
        if (bytes > MAX_PAYLOAD_BYTES) {
            throw new BusinessException(ErrorCode.ANALYTICS_PAYLOAD_TOO_LARGE);
        }
    }
}
