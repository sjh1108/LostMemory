package com.lostmemory.server.run.dto;

import io.swagger.v3.oas.annotations.media.Schema;

/**
 * GET /api/runs/{runId} 응답 — 런 메타 + 결과 (없으면 null) 묶음.
 */
public record RunDetailResponse(
        @Schema(description = "런 메타 정보 (start/end/status)")
        RunResponse run,

        @Schema(description = "런 결과. 진행 중 (run.status=IN_PROGRESS) 이면 null", nullable = true)
        RunResultResponse result
) {
    public static RunDetailResponse of(RunResponse run, RunResultResponse result) {
        return new RunDetailResponse(run, result);
    }
}
