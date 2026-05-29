package com.lostmemory.server.analytics.service;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lostmemory.server.analytics.dto.AnalyticsEventDto;
import com.lostmemory.server.analytics.entity.EventType;
import com.lostmemory.server.global.exception.BusinessException;
import com.lostmemory.server.global.exception.ErrorCode;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

/**
 * AnalyticsValidator — batch size / event_type / event_time / payload size 규칙.
 *
 * Clock 은 고정값으로 주입해 시간 검증을 재현 가능하게 한다.
 */
class AnalyticsValidatorTest {

    private static final Instant NOW = Instant.parse("2026-05-25T12:00:00Z");

    private final ObjectMapper        objectMapper = new ObjectMapper().findAndRegisterModules();
    private final Clock               clock        = Clock.fixed(NOW, ZoneOffset.UTC);
    private final AnalyticsValidator  validator    = new AnalyticsValidator(objectMapper, clock);

    private AnalyticsEventDto event(String type, Instant time, JsonNode payload) {
        return new AnalyticsEventDto(type, time, "1F_BOSS", payload);
    }

    @Test
    @DisplayName("validate — batch 비어있으면 ANALYTICS_BATCH_EMPTY")
    void validate_empty_throws() {
        assertThatThrownBy(() -> validator.validate(Collections.emptyList()))
                .isInstanceOf(BusinessException.class)
                .extracting("errorCode").isEqualTo(ErrorCode.ANALYTICS_BATCH_EMPTY);
    }

    @Test
    @DisplayName("validate — null 도 ANALYTICS_BATCH_EMPTY")
    void validate_null_throws() {
        assertThatThrownBy(() -> validator.validate(null))
                .isInstanceOf(BusinessException.class)
                .extracting("errorCode").isEqualTo(ErrorCode.ANALYTICS_BATCH_EMPTY);
    }

    @Test
    @DisplayName("validate — 1건 정상 통과")
    void validate_one_event_passes() {
        List<AnalyticsEventDto> batch = List.of(event("session_start", NOW, null));
        validator.validate(batch);
    }

    @Test
    @DisplayName("validate — 100건 경계 통과")
    void validate_100_events_passes() {
        List<AnalyticsEventDto> batch = new ArrayList<>();
        for (int i = 0; i < 100; i++) {
            batch.add(event("session_start", NOW, null));
        }
        validator.validate(batch);
    }

    @Test
    @DisplayName("validate — 101건은 ANALYTICS_BATCH_TOO_LARGE")
    void validate_101_events_throws() {
        List<AnalyticsEventDto> batch = new ArrayList<>();
        for (int i = 0; i < 101; i++) {
            batch.add(event("session_start", NOW, null));
        }
        assertThatThrownBy(() -> validator.validate(batch))
                .isInstanceOf(BusinessException.class)
                .extracting("errorCode").isEqualTo(ErrorCode.ANALYTICS_BATCH_TOO_LARGE);
    }

    @Test
    @DisplayName("validateOne — 알려진 event_type 소문자 통과 + enum 반환")
    void validateOne_knownType_returnsEnum() {
        EventType type = validator.validateOne(event("player_died", NOW, null), NOW);
        assertThat(type).isEqualTo(EventType.PLAYER_DIED);
    }

    @Test
    @DisplayName("validateOne — 대문자도 허용")
    void validateOne_uppercase_passes() {
        EventType type = validator.validateOne(event("SESSION_END", NOW, null), NOW);
        assertThat(type).isEqualTo(EventType.SESSION_END);
    }

    @Test
    @DisplayName("validateOne — 알려지지 않은 type 은 ANALYTICS_EVENT_TYPE_UNKNOWN")
    void validateOne_unknownType_throws() {
        assertThatThrownBy(() -> validator.validateOne(event("boss_attempt", NOW, null), NOW))
                .isInstanceOf(BusinessException.class)
                .extracting("errorCode").isEqualTo(ErrorCode.ANALYTICS_EVENT_TYPE_UNKNOWN);
    }

    @Test
    @DisplayName("validateOne — event_type 이 null/blank 면 ANALYTICS_EVENT_TYPE_UNKNOWN")
    void validateOne_blankType_throws() {
        assertThatThrownBy(() -> validator.validateOne(event("", NOW, null), NOW))
                .isInstanceOf(BusinessException.class)
                .extracting("errorCode").isEqualTo(ErrorCode.ANALYTICS_EVENT_TYPE_UNKNOWN);
    }

    @Test
    @DisplayName("validateOne — event_time null 이면 ANALYTICS_EVENT_TIME_INVALID")
    void validateOne_nullTime_throws() {
        assertThatThrownBy(() -> validator.validateOne(event("session_start", null, null), NOW))
                .isInstanceOf(BusinessException.class)
                .extracting("errorCode").isEqualTo(ErrorCode.ANALYTICS_EVENT_TIME_INVALID);
    }

    @Test
    @DisplayName("validateOne — event_time 이 정확히 24h 앞이면 통과 (경계 inclusive)")
    void validateOne_24hAhead_passes() {
        Instant ahead = NOW.plus(Duration.ofHours(24));
        validator.validateOne(event("session_start", ahead, null), NOW);
    }

    @Test
    @DisplayName("validateOne — event_time 이 24h+1s 앞이면 ANALYTICS_EVENT_TIME_SKEWED")
    void validateOne_overSkewFuture_throws() {
        Instant tooFar = NOW.plus(Duration.ofHours(24)).plusSeconds(1);
        assertThatThrownBy(() -> validator.validateOne(event("session_start", tooFar, null), NOW))
                .isInstanceOf(BusinessException.class)
                .extracting("errorCode").isEqualTo(ErrorCode.ANALYTICS_EVENT_TIME_SKEWED);
    }

    @Test
    @DisplayName("validateOne — event_time 이 24h+1s 과거여도 ANALYTICS_EVENT_TIME_SKEWED")
    void validateOne_overSkewPast_throws() {
        Instant tooFar = NOW.minus(Duration.ofHours(24)).minusSeconds(1);
        assertThatThrownBy(() -> validator.validateOne(event("session_start", tooFar, null), NOW))
                .isInstanceOf(BusinessException.class)
                .extracting("errorCode").isEqualTo(ErrorCode.ANALYTICS_EVENT_TIME_SKEWED);
    }

    @Test
    @DisplayName("validateOne — payload 8KB 경계 통과")
    void validateOne_payload8kbBoundary_passes() throws Exception {
        String filler = "a".repeat(8 * 1024 - 10);   // JSON 오버헤드 (key + 따옴표) 고려해 10 빼고 채움
        JsonNode payload = objectMapper.readTree("{\"x\":\"" + filler + "\"}");
        validator.validateOne(event("session_start", NOW, payload), NOW);
    }

    @Test
    @DisplayName("validateOne — payload 8KB+ 면 ANALYTICS_PAYLOAD_TOO_LARGE")
    void validateOne_payloadTooLarge_throws() throws Exception {
        String over = "a".repeat(8 * 1024 + 100);
        JsonNode payload = objectMapper.readTree("{\"x\":\"" + over + "\"}");
        assertThatThrownBy(() -> validator.validateOne(event("session_start", NOW, payload), NOW))
                .isInstanceOf(BusinessException.class)
                .extracting("errorCode").isEqualTo(ErrorCode.ANALYTICS_PAYLOAD_TOO_LARGE);
    }

    @Test
    @DisplayName("validateOne — payload null 통과")
    void validateOne_nullPayload_passes() {
        validator.validateOne(event("session_start", NOW, null), NOW);
    }
}
