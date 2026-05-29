package com.lostmemory.server.auth.dto;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.NotBlank;

public record LoginRequest(
        @Schema(description = "로그인 ID", example = "testuser",
                requiredMode = Schema.RequiredMode.REQUIRED)
        @NotBlank
        String loginId,

        @Schema(description = "비밀번호 (평문 전송)", example = "Pass123!",
                requiredMode = Schema.RequiredMode.REQUIRED)
        @NotBlank
        String password
) {
}
