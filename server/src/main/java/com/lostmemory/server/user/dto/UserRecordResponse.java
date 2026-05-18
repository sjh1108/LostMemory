package com.lostmemory.server.user.dto;

import com.lostmemory.server.user.entity.UserRecord;
import io.swagger.v3.oas.annotations.media.Schema;

import java.time.Instant;

/**
 * 본인 최고 전적 응답. row 가 없으면 default (0/0) 응답.
 */
public record UserRecordResponse(
        @Schema(description = "유저 PK", example = "1")
        Long userId,

        @Schema(description = "최고 도달 챕터 (1 챕터 = 1 보스 클리어)", example = "1")
        Integer clearedChapter,

        @Schema(description = "최고 도달 스테이지 (챕터 내 작은 방 단위, 미사용 가능)", example = "0")
        Integer clearedStage,

        @Schema(description = "최종 갱신 시각 (UTC). row 미존재 시 null", example = "2026-05-14T11:10:34Z",
                nullable = true)
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
