package com.lostmemory.server.auth.dto;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.NotBlank;

public record LogoutRequest(
        @Schema(description = "Revoke 할 refreshToken — 이후 해당 토큰으로 /auth/refresh 호출 시 401",
                example = "eyJhbGciOiJIUzI1NiJ9...",
                requiredMode = Schema.RequiredMode.REQUIRED)
        @NotBlank String refreshToken
) {
}
