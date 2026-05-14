package com.lostmemory.server.run.dto;

import com.lostmemory.server.run.entity.Run;
import com.lostmemory.server.run.entity.RunStatus;
import io.swagger.v3.oas.annotations.media.Schema;

import java.time.Instant;

/**
 * 런 정보 응답. 시작 직후 / 진행 중 조회 시 사용. 결과 데이터는 별도 RunResultResponse.
 */
public record RunResponse(
        @Schema(description = "런 PK (runs.run_id)", example = "42")
        Long runId,

        @Schema(description = "런이 속한 세션 PK", example = "56")
        Long sessionId,

        @Schema(description = "런 상태 (IN_PROGRESS / ENDED)", example = "IN_PROGRESS")
        RunStatus status,

        @Schema(description = "시작 시각 (UTC)", example = "2026-05-14T10:50:00Z")
        Instant startedAt,

        @Schema(description = "종료 시각 (UTC). 진행 중이면 null", example = "null", nullable = true)
        Instant endedAt
) {
    public static RunResponse from(Run run) {
        return new RunResponse(
                run.getId(),
                run.getSession().getId(),
                run.getStatus(),
                run.getStartedAt(),
                run.getEndedAt()
        );
    }
}
