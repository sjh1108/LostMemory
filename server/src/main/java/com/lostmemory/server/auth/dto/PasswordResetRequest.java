package com.lostmemory.server.auth.dto;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.Email;
import jakarta.validation.constraints.NotBlank;

/**
 * 비밀번호 재설정 코드 발송 요청. 이메일만 받는다.
 * 계정 존재 여부와 무관하게 응답은 항상 200 — 계정 열거 방지.
 */
public record PasswordResetRequest(
        @Schema(description = "재설정 코드를 받을 이메일", example = "tester@example.com",
                requiredMode = Schema.RequiredMode.REQUIRED)
        @NotBlank
        @Email
        String email
) {
}
