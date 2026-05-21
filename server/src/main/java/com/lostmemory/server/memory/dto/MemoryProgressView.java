package com.lostmemory.server.memory.dto;

import com.lostmemory.server.memory.entity.MemoryProgressState;
import com.lostmemory.server.memory.entity.UserMemoryProgress;
import io.swagger.v3.oas.annotations.media.Schema;

/**
 * 프레임별 진행도. unlockedMask 의 bit n=1 이 slot n 해금.
 * state 는 mask 로부터 derive — DB 컬럼 X.
 */
public record MemoryProgressView(
        @Schema(description = "프레임 PK", example = "1")
        Long frameId,

        @Schema(description = "6칸 비트마스크 (0~63). bit n=1 이면 slot n 해금. 63 이면 6칸 다 해금.",
                example = "42")
        Integer unlockedMask,

        @Schema(description = "진행 상태 — mask 로부터 derive (Locked/In Progress/Done)",
                example = "In Progress")
        String state
) {
    /** entity 로부터 응답 조립 — state 는 mask 로부터 계산. */
    public static MemoryProgressView from(UserMemoryProgress entity) {
        int mask = entity.getUnlockedMask();
        return new MemoryProgressView(
                entity.getFrameId(),
                mask,
                MemoryProgressState.fromMask(mask).displayValue()
        );
    }

    /** 진행도 row 가 없는 프레임용 default — mask=0, state=Locked. */
    public static MemoryProgressView lockedFor(Long frameId) {
        return new MemoryProgressView(frameId, 0, MemoryProgressState.LOCKED.displayValue());
    }
}
