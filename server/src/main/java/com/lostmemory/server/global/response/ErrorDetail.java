package com.lostmemory.server.global.response;

import io.swagger.v3.oas.annotations.media.Schema;

@Schema(description = "API 실패 시 에러 상세. ErrorCode enum 의 name() 과 message 그대로 노출.")
public record ErrorDetail(
        @Schema(description = "에러 코드 (ErrorCode enum 의 name)",
                example = "USER_ALREADY_IN_SESSION")
        String code,

        @Schema(description = "사용자에게 노출 가능한 에러 메시지",
                example = "이미 다른 세션에 참여 중입니다")
        String message
) {
}
