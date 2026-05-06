package com.lostmemory.server.session.dto;

import com.lostmemory.server.session.entity.Session;

import java.util.List;

/**
 * 세션 생성/조인 후 응답. sessionToken 은 자체 Relay 가 검증할 단명 JWT.
 * 클라는 이 토큰을 NGO 핸드셰이크에 박아 Relay 에 입장한다.
 */
public record SessionResponse(
        Long sessionId,
        Long hostId,
        Integer maxPlayers,
        String privateCode,
        String sessionToken,
        long sessionTokenExpiresIn,
        List<SessionMemberResponse> members
) {
    public static SessionResponse of(Session session,
                                     String sessionToken,
                                     long sessionTokenExpiresIn,
                                     List<SessionMemberResponse> members) {
        return new SessionResponse(
                session.getId(),
                session.getHost().getId(),
                session.getMaxPlayers(),
                session.getPrivateCode(),
                sessionToken,
                sessionTokenExpiresIn,
                members
        );
    }
}
