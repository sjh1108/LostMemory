package com.lostmemory.server.analytics.controller;

import com.lostmemory.server.analytics.dto.AnalyticsBatchRequest;
import com.lostmemory.server.analytics.dto.AnalyticsBatchResponse;
import com.lostmemory.server.analytics.service.AnalyticsIngestService;
import com.lostmemory.server.global.response.ApiResponse;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.media.Content;
import io.swagger.v3.oas.annotations.media.ExampleObject;
import io.swagger.v3.oas.annotations.responses.ApiResponses;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.http.HttpStatus;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;

@Tag(name = "Analytics", description = "플레이어 행동 이벤트 적재 — 클라이언트 batch ingest")
@RestController
@RequestMapping("/analytics")
@RequiredArgsConstructor
public class AnalyticsController {

    private final AnalyticsIngestService ingestService;

    @Operation(
            summary = "이벤트 batch 적재",
            description = """
                    클라이언트가 누적한 이벤트 batch 를 서버 큐에 적재.
                    JWT 의 user_id 가 권위 — 요청 body 의 user_id 는 무시한다.

                    제약:
                    - events 길이 1~100 (초과 시 413 ANALYTICS_BATCH_TOO_LARGE)
                    - event_time 은 ISO-8601, 현재 시각 ±24h 안 (밖이면 400 ANALYTICS_EVENT_TIME_SKEWED)
                    - event_type 은 화이트리스트 통과 (없으면 400 ANALYTICS_EVENT_TYPE_UNKNOWN)
                    - 단일 payload JSON 직렬화 8KB 이하 (초과 시 413 ANALYTICS_PAYLOAD_TOO_LARGE)
                    """
    )
    @ApiResponses({
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "202", description = "큐 적재 성공",
                    content = @Content(examples = @ExampleObject(value = """
                            { "success": true, "data": { "accepted": 12 } }
                            """))),
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "400", description = "검증 실패 (event_type / event_time / batch empty)",
                    content = @Content(examples = @ExampleObject(value = """
                            { "success": false, "error": { "code": "ANALYTICS_EVENT_TYPE_UNKNOWN", "message": "지원하지 않는 event_type 입니다" } }
                            """))),
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "413", description = "batch 100개 초과 또는 payload 8KB 초과",
                    content = @Content(examples = @ExampleObject(value = """
                            { "success": false, "error": { "code": "ANALYTICS_BATCH_TOO_LARGE", "message": "한 번에 보낼 수 있는 events 는 100개 이하입니다" } }
                            """)))
    })
    @PostMapping("/events")
    @ResponseStatus(HttpStatus.ACCEPTED)
    public ApiResponse<AnalyticsBatchResponse> ingest(
            @AuthenticationPrincipal Long userId,
            @Valid @RequestBody AnalyticsBatchRequest request) {
        int accepted = ingestService.enqueue(userId, request);
        return ApiResponse.of(new AnalyticsBatchResponse(accepted));
    }
}
