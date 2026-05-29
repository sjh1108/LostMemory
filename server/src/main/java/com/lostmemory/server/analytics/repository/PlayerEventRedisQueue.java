package com.lostmemory.server.analytics.repository;

import lombok.RequiredArgsConstructor;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.stereotype.Component;

import java.util.Collections;
import java.util.List;

/**
 * 플레이어 이벤트 큐 — Redis LIST 기반.
 *
 * 키:
 *   analytics:events:queue — 메인 큐 (LPUSH ↔ RPOP, FIFO)
 *   analytics:events:dlq   — flush 재시도 모두 실패한 batch 격리
 *
 * Redis AOF persistence 활성 권장 — 장애 시 큐 데이터 손실 방지.
 */
@Component
@RequiredArgsConstructor
public class PlayerEventRedisQueue {

    static final String QUEUE_KEY = "analytics:events:queue";
    static final String DLQ_KEY   = "analytics:events:dlq";

    private final StringRedisTemplate redis;

    /** batch 의 모든 직렬화된 event 를 큐 좌측에 push. 빈 리스트는 no-op. */
    public void enqueue(List<String> serializedEvents) {
        if (serializedEvents == null || serializedEvents.isEmpty()) {
            return;
        }
        redis.opsForList().leftPushAll(QUEUE_KEY, serializedEvents);
    }

    /**
     * 큐 우측에서 최대 batchSize 만큼 RPOP. count argument 는 Redis 6.2+ 지원.
     * 큐 비어있으면 빈 리스트.
     */
    public List<String> pollBatch(int batchSize) {
        if (batchSize <= 0) {
            return Collections.emptyList();
        }
        List<String> popped = redis.opsForList().rightPop(QUEUE_KEY, batchSize);
        return popped == null ? Collections.emptyList() : popped;
    }

    /** DB 적재 실패 batch 를 DLQ 좌측에 push. 운영자가 dlq 보고 수동 처리. */
    public void moveToDeadLetter(List<String> failedEvents) {
        if (failedEvents == null || failedEvents.isEmpty()) {
            return;
        }
        redis.opsForList().leftPushAll(DLQ_KEY, failedEvents);
    }

    /** 메인 큐 길이. */
    public long size() {
        Long len = redis.opsForList().size(QUEUE_KEY);
        return len == null ? 0L : len;
    }

    /** DLQ 길이. */
    public long deadLetterSize() {
        Long len = redis.opsForList().size(DLQ_KEY);
        return len == null ? 0L : len;
    }
}
