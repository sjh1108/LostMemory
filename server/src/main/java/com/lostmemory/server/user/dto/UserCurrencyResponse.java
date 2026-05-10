package com.lostmemory.server.user.dto;

import com.lostmemory.server.user.entity.UserCurrency;

import java.time.Instant;

/**
 * 본인 재화 응답. row 가 없으면 default (0) 응답으로 변환 — 신규 유저용.
 */
public record UserCurrencyResponse(
        Long userId,
        Integer memoryShards,
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
