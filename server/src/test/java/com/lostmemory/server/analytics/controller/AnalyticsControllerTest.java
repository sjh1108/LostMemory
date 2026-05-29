package com.lostmemory.server.analytics.controller;

import com.lostmemory.server.analytics.dto.AnalyticsBatchRequest;
import com.lostmemory.server.analytics.dto.AnalyticsBatchResponse;
import com.lostmemory.server.analytics.dto.AnalyticsEventDto;
import com.lostmemory.server.analytics.service.AnalyticsIngestService;
import com.lostmemory.server.global.response.ApiResponse;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.time.Instant;
import java.util.List;
import java.util.UUID;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

/**
 * AnalyticsController — JWT user_id 위임 + 서비스 결과 wrapping.
 *
 * Controller 자체 로직이 service 위임 1줄이라 단위 테스트만으로 충분 — 검증/예외 매핑은
 * Validator·GlobalExceptionHandler 가 각각의 단위 테스트로 다룬다.
 */
@ExtendWith(MockitoExtension.class)
class AnalyticsControllerTest {

    @Mock private AnalyticsIngestService ingestService;
    @InjectMocks private AnalyticsController controller;

    private static final UUID SESSION_UUID = UUID.fromString("550e8400-e29b-41d4-a716-446655440000");

    private AnalyticsBatchRequest request() {
        return new AnalyticsBatchRequest(
                "0.1.0",
                SESSION_UUID,
                List.of(new AnalyticsEventDto("session_start", Instant.parse("2026-05-25T12:00:00Z"), null, null))
        );
    }

    @Test
    @DisplayName("ingest — JWT user_id 를 service.enqueue 의 1번째 인자로 전달")
    void ingest_passesJwtUserId() {
        AnalyticsBatchRequest req = request();
        when(ingestService.enqueue(eq(42L), any())).thenReturn(1);

        controller.ingest(42L, req);

        verify(ingestService).enqueue(42L, req);
    }

    @Test
    @DisplayName("ingest — service 반환 accepted 가 응답 body 에 그대로 들어감")
    void ingest_wrapsAccepted() {
        when(ingestService.enqueue(any(), any())).thenReturn(7);

        ApiResponse<AnalyticsBatchResponse> resp = controller.ingest(7L, request());

        assertThat(resp.data().accepted()).isEqualTo(7);
    }
}
