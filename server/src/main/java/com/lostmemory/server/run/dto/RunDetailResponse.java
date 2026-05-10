package com.lostmemory.server.run.dto;

/**
 * GET /api/runs/{runId} 응답 — 런 메타 + 결과 (없으면 null) 묶음.
 */
public record RunDetailResponse(
        RunResponse run,
        RunResultResponse result
) {
    public static RunDetailResponse of(RunResponse run, RunResultResponse result) {
        return new RunDetailResponse(run, result);
    }
}
