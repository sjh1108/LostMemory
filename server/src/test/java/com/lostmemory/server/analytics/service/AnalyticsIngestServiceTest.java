package com.lostmemory.server.analytics.service;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lostmemory.server.analytics.dto.AnalyticsBatchRequest;
import com.lostmemory.server.analytics.dto.AnalyticsEventDto;
import com.lostmemory.server.analytics.entity.EventType;
import com.lostmemory.server.analytics.repository.PlayerEventRedisQueue;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;

import java.time.Clock;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.List;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.verifyNoInteractions;

/**
 * AnalyticsIngestService — 검증 위임 → 직렬화 → 큐 LPUSH.
 *
 * 핵심: 요청 payload 가 아닌 JWT 에서 받은 user_id 가 queued event 에 들어감.
 */
class AnalyticsIngestServiceTest {

    private static final Instant NOW = Instant.parse("2026-05-25T12:00:00Z");
    private static final UUID    SESSION_UUID = UUID.fromString("550e8400-e29b-41d4-a716-446655440000");

    private final ObjectMapper             objectMapper = new ObjectMapper().findAndRegisterModules();
    private final Clock                    clock        = Clock.fixed(NOW, ZoneOffset.UTC);
    private final AnalyticsValidator       validator    = new AnalyticsValidator(objectMapper, clock);
    private final PlayerEventRedisQueue    queue        = mock(PlayerEventRedisQueue.class);

    private final AnalyticsIngestService   service      =
            new AnalyticsIngestService(validator, queue, objectMapper, clock);

    @Test
    @DisplayName("enqueue — 정상 batch 는 검증 통과 후 큐에 적재되고 accepted 반환")
    void enqueue_normalBatch_returnsAccepted() {
        AnalyticsBatchRequest request = new AnalyticsBatchRequest(
                "0.1.0",
                SESSION_UUID,
                List.of(
                        new AnalyticsEventDto("session_start", NOW, null, null),
                        new AnalyticsEventDto("player_died",   NOW, "1F_BOSS", null)
                )
        );

        int accepted = service.enqueue(7L, request);

        assertThat(accepted).isEqualTo(2);
        @SuppressWarnings("unchecked")
        ArgumentCaptor<List<String>> captor = ArgumentCaptor.forClass(List.class);
        verify(queue).enqueue(captor.capture());
        assertThat(captor.getValue()).hasSize(2);
    }

    @Test
    @DisplayName("enqueue — 직렬화된 QueuedPlayerEvent 안에 JWT user_id 가 들어감")
    void enqueue_usesJwtUserId() throws Exception {
        AnalyticsBatchRequest request = new AnalyticsBatchRequest(
                "0.1.0",
                SESSION_UUID,
                List.of(new AnalyticsEventDto("session_start", NOW, null, null))
        );

        service.enqueue(123L, request);

        @SuppressWarnings("unchecked")
        ArgumentCaptor<List<String>> captor = ArgumentCaptor.forClass(List.class);
        verify(queue).enqueue(captor.capture());
        QueuedPlayerEvent decoded = objectMapper.readValue(captor.getValue().get(0), QueuedPlayerEvent.class);

        assertThat(decoded.userId()).isEqualTo(123L);
        assertThat(decoded.analyticsSessionId()).isEqualTo(SESSION_UUID);
        assertThat(decoded.eventType()).isEqualTo(EventType.SESSION_START);
        assertThat(decoded.eventTime()).isEqualTo(NOW);
        assertThat(decoded.clientVersion()).isEqualTo("0.1.0");
    }

    @Test
    @DisplayName("enqueue — payload JsonNode 는 문자열로 직렬화되어 큐에 들어감")
    void enqueue_serializesPayload() throws Exception {
        JsonNode payload = objectMapper.readTree("{\"party_size\":2,\"device\":\"PC\"}");
        AnalyticsBatchRequest request = new AnalyticsBatchRequest(
                "0.1.0",
                SESSION_UUID,
                List.of(new AnalyticsEventDto("session_start", NOW, null, payload))
        );

        service.enqueue(7L, request);

        @SuppressWarnings("unchecked")
        ArgumentCaptor<List<String>> captor = ArgumentCaptor.forClass(List.class);
        verify(queue).enqueue(captor.capture());
        QueuedPlayerEvent decoded = objectMapper.readValue(captor.getValue().get(0), QueuedPlayerEvent.class);

        assertThat(decoded.payload()).contains("party_size").contains("PC");
    }

    @Test
    @DisplayName("enqueue — payload null 은 그대로 null 로 직렬화")
    void enqueue_nullPayload_serializedAsNull() throws Exception {
        AnalyticsBatchRequest request = new AnalyticsBatchRequest(
                "0.1.0",
                SESSION_UUID,
                List.of(new AnalyticsEventDto("session_start", NOW, null, null))
        );

        service.enqueue(7L, request);

        @SuppressWarnings("unchecked")
        ArgumentCaptor<List<String>> captor = ArgumentCaptor.forClass(List.class);
        verify(queue).enqueue(captor.capture());
        QueuedPlayerEvent decoded = objectMapper.readValue(captor.getValue().get(0), QueuedPlayerEvent.class);

        assertThat(decoded.payload()).isNull();
    }

    @Test
    @DisplayName("enqueue — validator 가 throw 하면 큐에 적재 안 함")
    void enqueue_validatorRejects_doesNotEnqueue() {
        AnalyticsBatchRequest empty = new AnalyticsBatchRequest("0.1.0", SESSION_UUID, List.of());

        try {
            service.enqueue(7L, empty);
        } catch (RuntimeException expected) {
            // Validator 의 ANALYTICS_BATCH_EMPTY
        }

        verifyNoInteractions(queue);
    }
}
