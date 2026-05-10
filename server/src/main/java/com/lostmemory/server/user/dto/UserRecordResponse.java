package com.lostmemory.server.user.dto;

import com.lostmemory.server.user.entity.UserRecord;

import java.time.Instant;

/**
 * 본인 최고 전적 응답. row 가 없으면 default (0/0) 응답.
 */
public record UserRecordResponse(
        Long userId,
        Integer clearedChapter,
        Integer clearedStage,
        Instant updatedAt
) {
    public static UserRecordResponse from(UserRecord entity) {
        return new UserRecordResponse(
                entity.getUser().getId(),
                entity.getClearedChapter(),
                entity.getClearedStage(),
                entity.getUpdatedAt()
        );
    }

    /** row 미존재 시 기본 응답 — chapter/stage 모두 0 */
    public static UserRecordResponse defaultFor(Long userId) {
        return new UserRecordResponse(userId, 0, 0, null);
    }
}
