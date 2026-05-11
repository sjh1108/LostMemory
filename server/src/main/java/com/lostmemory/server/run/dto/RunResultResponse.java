package com.lostmemory.server.run.dto;

import com.lostmemory.server.run.entity.RunResult;
import com.lostmemory.server.run.entity.RunResultStatus;

import java.time.Instant;

/**
 * 런 결과 응답 — 종료 후 마을 결과창에서 보여줄 4 필드 + 메타.
 */
public record RunResultResponse(
        Long runId,
        RunResultStatus result,
        Integer durationSeconds,
        Integer chapterReached,
        Integer memoryShardsEarned,
        Instant savedAt
) {
    public static RunResultResponse from(RunResult entity) {
        return new RunResultResponse(
                entity.getRunId(),
                entity.getResult(),
                entity.getDurationSeconds(),
                entity.getChapterReached(),
                entity.getMemoryShardsEarned(),
                entity.getSavedAt()
        );
    }
}
