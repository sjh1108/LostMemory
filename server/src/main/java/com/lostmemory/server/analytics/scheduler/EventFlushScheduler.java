package com.lostmemory.server.analytics.scheduler;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lostmemory.server.analytics.entity.PlayerEvent;
import com.lostmemory.server.analytics.repository.PlayerEventRedisQueue;
import com.lostmemory.server.analytics.repository.PlayerEventRepository;
import com.lostmemory.server.analytics.service.QueuedPlayerEvent;
import com.lostmemory.server.user.entity.User;
import com.lostmemory.server.user.repository.UserRepository;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Component;
import org.springframework.transaction.annotation.Transactional;

import java.util.ArrayList;
import java.util.List;

/**
 * Redis 큐에 적재된 이벤트를 주기적으로 PG 에 일괄 저장하는 스케줄러.
 *
 * 흐름:
 *   1. analytics:events:queue 우측에서 batch-size 만큼 RPOP
 *   2. QueuedPlayerEvent 로 deserialize → PlayerEvent 엔티티 생성 → saveAll
 *   3. 실패 시 retry-max 회 재시도. 모두 실패하면 batch 통째 DLQ 로 이동
 *
 * 트랜잭션 경계: saveAll 단위 all-or-nothing. 단일 INSERT 실패 = 전체 batch rollback.
 *
 * 설정:
 *   analytics.flush.interval-ms — 실행 주기 (기본 5000)
 *   analytics.flush.batch-size  — 1회당 RPOP 개수 (기본 200)
 *   analytics.flush.retry-max   — 재시도 횟수 (기본 3)
 */
@Slf4j
@Component
@RequiredArgsConstructor
public class EventFlushScheduler {

    private final PlayerEventRedisQueue   queue;
    private final PlayerEventRepository   playerEventRepository;
    private final UserRepository          userRepository;
    private final ObjectMapper            objectMapper;

    @Value("${analytics.flush.batch-size:200}")
    private int batchSize;

    @Value("${analytics.flush.retry-max:3}")
    private int retryMax;

    @Scheduled(fixedDelayString = "${analytics.flush.interval-ms:5000}")
    public void flush() {
        List<String> batch = queue.pollBatch(batchSize);
        if (batch.isEmpty()) {
            return;
        }
        flushWithRetry(batch);
    }

    private void flushWithRetry(List<String> batch) {
        for (int attempt = 1; attempt <= retryMax; attempt++) {
            try {
                saveBatch(batch);
                return;
            } catch (RuntimeException e) {
                if (attempt < retryMax) {
                    log.warn("[Analytics] flush 실패 (시도 {}/{}): {}", attempt, retryMax, e.getMessage());
                    sleepBackoff(attempt);
                } else {
                    log.error("[Analytics] {}건 DLQ 이동 — 재시도 {}회 모두 실패", batch.size(), retryMax, e);
                    queue.moveToDeadLetter(batch);
                }
            }
        }
    }

    @Transactional
    protected void saveBatch(List<String> batch) {
        List<PlayerEvent> entities = new ArrayList<>(batch.size());
        for (String raw : batch) {
            QueuedPlayerEvent queued = deserialize(raw);
            User userRef = userRepository.getReferenceById(queued.userId());
            entities.add(PlayerEvent.create(
                    userRef,
                    queued.analyticsSessionId(),
                    queued.eventType(),
                    queued.eventTime(),
                    queued.stageId(),
                    queued.payload(),
                    queued.clientVersion()
            ));
        }
        playerEventRepository.saveAll(entities);
    }

    private QueuedPlayerEvent deserialize(String raw) {
        try {
            return objectMapper.readValue(raw, QueuedPlayerEvent.class);
        } catch (JsonProcessingException e) {
            throw new IllegalStateException("QueuedPlayerEvent 역직렬화 실패: " + raw, e);
        }
    }

    private void sleepBackoff(int attempt) {
        try {
            Thread.sleep(100L * attempt);
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
        }
    }
}
