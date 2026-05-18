package com.lostmemory.server.session.dto;

import io.swagger.v3.oas.annotations.media.Schema;

/**
 * 코드로 세션 조회 시 응답 — 입장 전이라 sessionToken 은 발급 안 함.
 * 클라가 sessionId 알면 POST /api/sessions/{id}/join 으로 입장 가능.
 */
public record SessionFindResponse(
        @Schema(description = "세션 PK", example = "56")
        Long sessionId,

        @Schema(description = "호스트 user_id", example = "1")
        Long hostId,

        @Schema(description = "최대 인원", example = "2")
        Integer maxPlayers,

        @Schema(description = "현재 참가자 수 (호스트 + 게스트)", example = "1")
        long currentMembers,

        @Schema(description = "정원 마감 여부. true 면 join 호출 시 409 SESSION_FULL", example = "false")
        boolean isFull
) {
}
