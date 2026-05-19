package com.lostmemory.server.memory.dto;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotNull;

/**
 * 프레임 내 slot 해금 요청.
 *
 * 처리: 보유 shards >= consumedShards 검증 → 차감 → unlocked_mask |= (1 << slotIndex).
 * 이미 해금된 slot 인 경우 idempotent (변화 없음, shards 차감 없음).
 */
public record UnlockSlotRequest(
        @Schema(description = "프레임 PK", example = "1")
        @NotNull
        Long frameId,

        @Schema(description = "슬롯 인덱스 (0~5)", example = "0")
        @NotNull @Min(0) @Max(5)
        Integer slotIndex,

        @Schema(description = "해금에 소비할 파편 수량 (양수). 클라가 계산해서 보냄.", example = "10")
        @NotNull @Min(1)
        Integer consumedShards
) {
}
