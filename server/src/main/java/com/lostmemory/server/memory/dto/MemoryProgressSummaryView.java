package com.lostmemory.server.memory.dto;

import io.swagger.v3.oas.annotations.media.Schema;

import java.util.List;

/**
 * 본인 메모리 진행도 종합 응답. 마을 진입 시 1회 호출로 모든 프레임 + 보유 파편 받기.
 */
public record MemoryProgressSummaryView(
        @Schema(description = "본인 기억의 파편 보유량", example = "125")
        Integer memoryShards,

        @Schema(description = "프레임별 진행도 — 마스터 frame 순서대로 (display_order ASC). 진행도 row 없는 프레임은 mask=0 + Locked")
        List<MemoryProgressView> progresses
) {
}
