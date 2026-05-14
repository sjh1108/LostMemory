package com.lostmemory.server.session.dto;

import com.lostmemory.server.session.entity.Session;
import io.swagger.v3.oas.annotations.media.Schema;

import java.util.List;

/**
 * 세션 생성/조인 후 응답. sessionToken 은 자체 Relay 가 검증할 단명 JWT.
 * 클라는 이 토큰을 NGO 핸드셰이크에 박아 Relay 에 입장한다.
 */
public record SessionResponse(
        @Schema(description = "세션 PK (sessions.session_id)", example = "56")
        Long sessionId,

        @Schema(description = "호스트 user_id", example = "1")
        Long hostId,

        @Schema(description = "최대 인원", example = "2")
        Integer maxPlayers,

        @Schema(description = "입장 코드", example = "GKTYF6")
        String privateCode,

        @Schema(description = "자체 Relay HELLO 핸드셰이크용 단명 JWT. NGO Transport 에 SetSession() 으로 주입",
                example = "eyJhbGciOiJIUzI1NiJ9...")
        String sessionToken,

        @Schema(description = "sessionToken 만료까지 남은 초 (default 3600 = 1시간)", example = "3600")
        long sessionTokenExpiresIn,

        @Schema(description = "현재 참가자 목록 (호스트 + 게스트)")
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
