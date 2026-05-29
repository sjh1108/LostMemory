package com.lostmemory.server.auth.dto;

import io.swagger.v3.oas.annotations.media.Schema;

/**
 * 회원가입 응답. 가입 시점엔 토큰을 발급하지 않는다 — 이메일 인증(/auth/email/verify) 완료 후 발급.
 */
public record SignupResponse(
        @Schema(description = "내부 유저 ID", example = "42")
        Long userId,

        @Schema(description = "계정 상태 — 가입 직후엔 항상 'pending'", example = "pending")
        String status
) {
}
