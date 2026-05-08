package com.lostmemory.server.session.dto;

/**
 * 코드로 세션 조회 시 응답 — 입장 전이라 sessionToken 은 발급 안 함.
 * 클라가 sessionId 알면 POST /api/sessions/{id}/join 으로 입장 가능.
 */
public record SessionFindResponse(
        Long sessionId,
        Long hostId,
        Integer maxPlayers,
        long currentMembers,
        boolean isFull
) {
}
