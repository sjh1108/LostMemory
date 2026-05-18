package com.lostmemory.server.auth.dto;

import io.swagger.v3.oas.annotations.media.Schema;

public record TokenResponse(
        @Schema(description = "API 호출용 access 토큰 (Authorization: Bearer <token>)",
                example = "eyJhbGciOiJIUzI1NiJ9...")
        String accessToken,

        @Schema(description = "토큰 갱신용 refresh 토큰 (rotation — 1회 사용 후 무효화)",
                example = "eyJhbGciOiJIUzI1NiJ9...")
        String refreshToken,

        @Schema(description = "accessToken 만료까지 남은 초 (default 1800 = 30분)",
                example = "1800")
        long accessTokenExpiresIn
) {
}
