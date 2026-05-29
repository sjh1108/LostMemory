package com.lostmemory.server.analytics.repository;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.data.redis.core.ListOperations;
import org.springframework.data.redis.core.StringRedisTemplate;

import java.util.Collections;
import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.lenient;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

/**
 * PlayerEventRedisQueue — LPUSH/RPOP/LLEN 위임 동작.
 */
@ExtendWith(MockitoExtension.class)
class PlayerEventRedisQueueTest {

    @Mock private StringRedisTemplate         redis;
    @Mock private ListOperations<String, String> ops;

    @InjectMocks private PlayerEventRedisQueue queue;

    private static final String QUEUE_KEY = "analytics:events:queue";
    private static final String DLQ_KEY   = "analytics:events:dlq";

    @BeforeEach
    void setUp() {
        lenient().when(redis.opsForList()).thenReturn(ops);
    }

    @Test
    @DisplayName("enqueue — 정상 batch 는 leftPushAll 호출")
    void enqueue_pushesAll() {
        List<String> events = List.of("{\"a\":1}", "{\"b\":2}");

        queue.enqueue(events);

        verify(ops).leftPushAll(QUEUE_KEY, events);
    }

    @Test
    @DisplayName("enqueue — 빈 리스트는 no-op")
    void enqueue_empty_noop() {
        queue.enqueue(Collections.emptyList());
        verify(ops, never()).leftPushAll(eq(QUEUE_KEY), org.mockito.ArgumentMatchers.<List<String>>any());
    }

    @Test
    @DisplayName("enqueue — null 도 no-op")
    void enqueue_null_noop() {
        queue.enqueue(null);
        verify(ops, never()).leftPushAll(eq(QUEUE_KEY), org.mockito.ArgumentMatchers.<List<String>>any());
    }

    @Test
    @DisplayName("pollBatch — count 만큼 rightPop 위임")
    void pollBatch_callsRightPop() {
        List<String> popped = List.of("{\"a\":1}");
        when(ops.rightPop(QUEUE_KEY, 200)).thenReturn(popped);

        List<String> result = queue.pollBatch(200);

        assertThat(result).isEqualTo(popped);
    }

    @Test
    @DisplayName("pollBatch — Redis 가 null 반환하면 빈 리스트")
    void pollBatch_nullReturn_emptyList() {
        when(ops.rightPop(QUEUE_KEY, 200)).thenReturn(null);

        assertThat(queue.pollBatch(200)).isEmpty();
    }

    @Test
    @DisplayName("pollBatch — batchSize 0/음수는 빈 리스트 + Redis 호출 안 함")
    void pollBatch_zeroOrNegative_emptyAndNoCall() {
        assertThat(queue.pollBatch(0)).isEmpty();
        assertThat(queue.pollBatch(-1)).isEmpty();
        verify(ops, never()).rightPop(eq(QUEUE_KEY), org.mockito.ArgumentMatchers.anyLong());
    }

    @Test
    @DisplayName("moveToDeadLetter — DLQ 키로 leftPushAll")
    void moveToDeadLetter_pushesToDlq() {
        List<String> failed = List.of("{\"a\":1}");

        queue.moveToDeadLetter(failed);

        verify(ops).leftPushAll(DLQ_KEY, failed);
    }

    @Test
    @DisplayName("moveToDeadLetter — 빈 리스트는 no-op")
    void moveToDeadLetter_empty_noop() {
        queue.moveToDeadLetter(Collections.emptyList());
        verify(ops, never()).leftPushAll(eq(DLQ_KEY), org.mockito.ArgumentMatchers.<List<String>>any());
    }

    @Test
    @DisplayName("size — Redis null 반환하면 0")
    void size_nullReturn_zero() {
        when(ops.size(QUEUE_KEY)).thenReturn(null);
        assertThat(queue.size()).isZero();
    }

    @Test
    @DisplayName("size — 정상 값 반환")
    void size_normal() {
        when(ops.size(QUEUE_KEY)).thenReturn(42L);
        assertThat(queue.size()).isEqualTo(42L);
    }

    @Test
    @DisplayName("deadLetterSize — DLQ 길이 반환")
    void deadLetterSize_normal() {
        when(ops.size(DLQ_KEY)).thenReturn(7L);
        assertThat(queue.deadLetterSize()).isEqualTo(7L);
    }
}
