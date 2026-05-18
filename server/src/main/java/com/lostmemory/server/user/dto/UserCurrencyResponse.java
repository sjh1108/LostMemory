package com.lostmemory.server.user.dto;

import com.lostmemory.server.user.entity.UserCurrency;
import io.swagger.v3.oas.annotations.media.Schema;

import java.time.Instant;

/**
 * 본인 재화 응답. row 가 없으면 default (0) 응답으로 변환 — 신규 유저용.
 */
public record UserCurrencyResponse(
        @Schema(description = "유저 PK", example = "1")
        Long userId,

        @Schema(description = "기억의 파편 잔량. 런 종료 시 적립, 메타 성장 구매 시 차감",
                example = "125")
        Integer memoryShards,

        @Schema(description = "최종 갱신 시각 (UTC). row 미존재 시 null", example = "2026-05-14T11:10:34Z",
                nullable = true)
        Instant updatedAt
) {
    public static UserCurrencyResponse from(UserCurrency entity) {
        return new UserCurrencyResponse(
                entity.getUserId(),
                entity.getMemoryShards(),
                entity.getUpdatedAt()
        );
    }

    /** row 미존재 시 기본 응답 — memory_shards 0 */
    public static UserCurrencyResponse defaultFor(Long userId) {
        return new UserCurrencyResponse(userId, 0, null);
    }
}
