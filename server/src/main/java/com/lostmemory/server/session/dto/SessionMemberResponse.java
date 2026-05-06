package com.lostmemory.server.session.dto;

import com.lostmemory.server.session.entity.SessionJoin;
import com.lostmemory.server.session.entity.SessionRole;

import java.time.Instant;

public record SessionMemberResponse(
        Long userId,
        String nickname,
        SessionRole role,
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
