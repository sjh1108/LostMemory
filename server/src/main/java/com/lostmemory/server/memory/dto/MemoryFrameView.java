package com.lostmemory.server.memory.dto;

import com.lostmemory.server.memory.entity.MemoryFrame;
import io.swagger.v3.oas.annotations.media.Schema;

/**
 * 액자 마스터 응답. 칸 정보(rows/cols/cost) 는 클라가 관리 — 백엔드는 식별자 + 정렬만.
 */
public record MemoryFrameView(
        @Schema(description = "프레임 PK", example = "1")
        Long frameId,

        @Schema(description = "정렬 순서 (오름차순)", example = "1")
        Integer displayOrder
) {
    public static MemoryFrameView from(MemoryFrame entity) {
        return new MemoryFrameView(entity.getId(), entity.getDisplayOrder());
    }
}
