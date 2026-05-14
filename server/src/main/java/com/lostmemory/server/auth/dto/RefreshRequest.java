package com.lostmemory.server.auth.dto;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.NotBlank;

public record RefreshRequest(
        @Schema(description = "로그인/이전 refresh 호출에서 받은 refreshToken (rotation 정책 — 호출 즉시 무효화)",
                example = "eyJhbGciOiJIUzI1NiJ9...",
                requiredMode = Schema.RequiredMode.REQUIRED)
        @NotBlank String refreshToken
) {
}
