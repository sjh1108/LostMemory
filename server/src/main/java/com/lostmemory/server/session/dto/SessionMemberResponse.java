package com.lostmemory.server.session.dto;

import com.lostmemory.server.session.entity.SessionJoin;
import com.lostmemory.server.session.entity.SessionRole;
import io.swagger.v3.oas.annotations.media.Schema;

import java.time.Instant;

public record SessionMemberResponse(
        @Schema(description = "참가자 user_id", example = "1")
        Long userId,

        @Schema(description = "인게임 닉네임", example = "테스터1")
        String nickname,

        @Schema(description = "역할 (host / guest)", example = "host")
        SessionRole role,

        @Schema(description = "참가 시각 (UTC)", example = "2026-05-14T10:42:42Z")
        Instant joinedAt
) {
    public static SessionMemberResponse from(SessionJoin sessionJoin) {
        return new SessionMemberResponse(
                sessionJoin.getUser().getId(),
                sessionJoin.getUser().getNickname(),
                sessionJoin.getRole(),
                sessionJoin.getJoinedAt()
        );
    }
}
