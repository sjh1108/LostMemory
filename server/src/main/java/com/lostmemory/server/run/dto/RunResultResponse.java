package com.lostmemory.server.run.dto;

import com.lostmemory.server.run.entity.RunResult;
import com.lostmemory.server.run.entity.RunResultStatus;
import io.swagger.v3.oas.annotations.media.Schema;

import java.time.Instant;

/**
 * 런 결과 응답 — 종료 후 마을 결과창에서 보여줄 4 필드 + 메타.
 */
public record RunResultResponse(
        @Schema(description = "런 PK", example = "42")
        Long runId,

        @Schema(description = "결과 (CLEAR / DEATH / SURRENDER). chapterReached 로 백엔드 추론",
                example = "CLEAR")
        RunResultStatus result,

        @Schema(description = "플레이 타임 초", example = "1234")
        Integer durationSeconds,

        @Schema(description = "도달 챕터", example = "1")
        Integer chapterReached,

        @Schema(description = "획득 파편 총량", example = "25")
        Integer memoryShardsEarned,

        @Schema(description = "결과 저장 시각 (UTC)", example = "2026-05-14T11:10:34Z")
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
