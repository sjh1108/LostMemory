package com.lostmemory.server.analytics.scheduler;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lostmemory.server.analytics.entity.EventType;
import com.lostmemory.server.analytics.entity.PlayerEvent;
import com.lostmemory.server.analytics.repository.PlayerEventRedisQueue;
import com.lostmemory.server.analytics.repository.PlayerEventRepository;
import com.lostmemory.server.analytics.service.QueuedPlayerEvent;
import com.lostmemory.server.user.entity.User;
import com.lostmemory.server.user.repository.UserRepository;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.test.util.ReflectionTestUtils;

import java.time.Instant;
import java.util.Collections;
import java.util.List;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyList;
import static org.mockito.ArgumentMatchers.anyLong;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.times;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

/**
 * EventFlushScheduler — RPOP batch → entity 변환 → saveAll. 실패 시 재시도 후 DLQ.
 */
@ExtendWith(MockitoExtension.class)
class EventFlushSchedulerTest {

    @Mock private PlayerEventRedisQueue   queue;
    @Mock private PlayerEventRepository   playerEventRepository;
    @Mock private UserRepository          userRepository;

    private final ObjectMapper objectMapper = new ObjectMapper().findAndRegisterModules();

    @InjectMocks private EventFlushScheduler scheduler;

    private User userStub;

    @BeforeEach
    void setUp() {
        ReflectionTestUtils.setField(scheduler, "objectMapper", objectMapper);
        ReflectionTestUtils.setField(scheduler, "batchSize", 200);
        ReflectionTestUtils.setField(scheduler, "retryMax", 3);
        userStub = org.mockito.Mockito.mock(User.class);
    }

    private String serialize(long userId) {
        try {
            QueuedPlayerEvent q = new QueuedPlayerEvent(
                    userId,
                    UUID.fromString("550e8400-e29b-41d4-a716-446655440000"),
                    EventType.SESSION_START,
                    Instant.parse("2026-05-25T12:00:00Z"),
                    null,
                    null,
                    "0.1.0"
            );
            return objectMapper.writeValueAsString(q);
        } catch (Exception e) {
            throw new IllegalStateException(e);
        }
    }

    @Test
    @DisplayName("flush — 큐 비어있으면 saveAll/DLQ 호출 안 함")
    void flush_emptyQueue_noop() {
        when(queue.pollBatch(200)).thenReturn(Collections.emptyList());

        scheduler.flush();

        verify(playerEventRepository, never()).saveAll(anyList());
        verify(queue, never()).moveToDeadLetter(anyList());
    }

    @Test
    @DisplayName("flush — 정상 batch 는 한 번 saveAll 호출")
    void flush_normalBatch_savesAll() {
        when(queue.pollBatch(200)).thenReturn(List.of(serialize(7L), serialize(8L)));
        when(userRepository.getReferenceById(anyLong())).thenReturn(userStub);

        scheduler.flush();

        verify(playerEventRepository, times(1)).saveAll(anyList());
        verify(queue, never()).moveToDeadLetter(anyList());
    }

    @Test
    @DisplayName("flush — saveAll 이 매번 실패하면 재시도 3회 후 DLQ 이동")
    void flush_repeatedFailure_movesToDlq() {
        List<String> batch = List.of(serialize(7L));
        when(queue.pollBatch(200)).thenReturn(batch);
        when(userRepository.getReferenceById(anyLong())).thenReturn(userStub);
        when(playerEventRepository.saveAll(anyList()))
                .thenThrow(new RuntimeException("DB unavailable"));

        scheduler.flush();

        verify(playerEventRepository, times(3)).saveAll(anyList());
        verify(queue).moveToDeadLetter(batch);
    }

    @Test
    @DisplayName("flush — saveAll 첫 시도 실패 후 두 번째에 성공하면 DLQ 호출 안 함")
    void flush_succeedsOnRetry_noDlq() {
        when(queue.pollBatch(200)).thenReturn(List.of(serialize(7L)));
        when(userRepository.getReferenceById(anyLong())).thenReturn(userStub);
        when(playerEventRepository.saveAll(anyList()))
                .thenThrow(new RuntimeException("first attempt fail"))
                .thenReturn(List.of());

        scheduler.flush();

        verify(playerEventRepository, times(2)).saveAll(anyList());
        verify(queue, never()).moveToDeadLetter(anyList());
    }

    @Test
    @DisplayName("flush — 역직렬화 실패도 재시도 후 DLQ")
    void flush_deserializationFails_movesToDlq() {
        List<String> corrupt = List.of("not-a-valid-json");
        when(queue.pollBatch(200)).thenReturn(corrupt);

        scheduler.flush();

        verify(playerEventRepository, never()).saveAll(anyList());
        verify(queue).moveToDeadLetter(corrupt);
    }

    @Test
    @DisplayName("flush — saveAll 호출 시 entity 변환 정상 (eventType + sessionId 보존)")
    void flush_entitiesContainExpectedFields() {
        String raw = serialize(42L);
        when(queue.pollBatch(200)).thenReturn(List.of(raw));
        when(userRepository.getReferenceById(eq(42L))).thenReturn(userStub);

        scheduler.flush();

        @SuppressWarnings("unchecked")
        org.mockito.ArgumentCaptor<List<PlayerEvent>> captor =
                org.mockito.ArgumentCaptor.forClass(List.class);
        verify(playerEventRepository).saveAll(captor.capture());
        List<PlayerEvent> saved = captor.getValue();
        assertThat(saved).hasSize(1);
        assertThat(saved.get(0).getEventType()).isEqualTo(EventType.SESSION_START);
        assertThat(saved.get(0).getAnalyticsSessionId())
                .isEqualTo(UUID.fromString("550e8400-e29b-41d4-a716-446655440000"));
    }
}
