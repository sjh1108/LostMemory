package com.lostmemory.server.run.dto;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.NotNull;

/**
 * 런 시작 요청. 호스트가 호출.
 * 솔로 모드도 1인 세션 자동 생성 흐름이라 sessionId 항상 필요.
 */
public record StartRunRequest(
        @Schema(description = "런이 진행될 세션 PK (sessions.session_id)", example = "56",
                requiredMode = Schema.RequiredMode.REQUIRED)
        @NotNull(message = "sessionId 는 필수입니다") Long sessionId
) {
}
