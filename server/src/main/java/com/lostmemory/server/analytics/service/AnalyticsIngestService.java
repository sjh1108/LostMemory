package com.lostmemory.server.analytics.service;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lostmemory.server.analytics.dto.AnalyticsBatchRequest;
import com.lostmemory.server.analytics.dto.AnalyticsEventDto;
import com.lostmemory.server.analytics.entity.EventType;
import com.lostmemory.server.analytics.repository.PlayerEventRedisQueue;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;

import java.time.Clock;
import java.util.ArrayList;
import java.util.List;

/**
 * 분석 이벤트 batch 적재 서비스.
 *
 * 흐름: 검증 → QueuedPlayerEvent 직렬화 → Redis 큐 LPUSH.
 * DB 적재는 별도 컴포넌트가 큐에서 꺼내 saveAll.
 */
@Slf4j
@Service
@RequiredArgsConstructor
public class AnalyticsIngestService {

    private final AnalyticsValidator      validator;
    private final PlayerEventRedisQueue   queue;
    private final ObjectMapper            objectMapper;
    private final Clock                   clock;

    /**
     * batch 를 검증하고 큐에 적재한다. 큐에 push 한 이벤트 개수를 반환.
     *
     * @param userId   JWT 에서 추출한 user_id (요청 payload 의 user_id 는 무시)
     * @param request  클라 batch
     */
    public int enqueue(Long userId, AnalyticsBatchRequest request) {
        validator.validate(request.events());

        List<String> serialized = new ArrayList<>(request.events().size());
        for (AnalyticsEventDto event : request.events()) {
            EventType type = validator.validateOne(event, clock.instant());
            QueuedPlayerEvent queued = new QueuedPlayerEvent(
                    userId,
                    request.analyticsSessionId(),
                    type,
                    event.eventTime(),
                    event.stageId(),
                    serializePayload(event.payload()),
                    request.clientVersion()
            );
            serialized.add(serialize(queued));
        }

        queue.enqueue(serialized);
        return serialized.size();
    }

    private String serializePayload(JsonNode payload) {
        if (payload == null || payload.isNull()) {
            return null;
        }
        try {
            return objectMapper.writeValueAsString(payload);
        } catch (JsonProcessingException e) {
            throw new IllegalStateException("payload 직렬화 실패", e);
        }
    }

    private String serialize(QueuedPlayerEvent event) {
        try {
            return objectMapper.writeValueAsString(event);
        } catch (JsonProcessingException e) {
            throw new IllegalStateException("QueuedPlayerEvent 직렬화 실패", e);
        }
    }
}
