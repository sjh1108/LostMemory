package com.lostmemory.server.auth.dto;

import com.lostmemory.server.global.validation.PasswordPolicy;
import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.Email;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Pattern;

public record PasswordResetConfirmRequest(
        @Schema(description = "재설정 대상 이메일", example = "tester@example.com",
                requiredMode = Schema.RequiredMode.REQUIRED)
        @NotBlank
        @Email
        String email,

        @Schema(description = "메일로 받은 6자리 숫자 재설정 코드", example = "049317",
                requiredMode = Schema.RequiredMode.REQUIRED)
        @NotBlank
        @Pattern(regexp = "\\d{6}", message = "재설정 코드는 6자리 숫자여야 합니다")
        String code,

        @Schema(description = "새 비밀번호 (8~100자 + 영문·숫자·특수문자 모두 포함)",
                example = "NewPass123!",
                requiredMode = Schema.RequiredMode.REQUIRED)
        @PasswordPolicy
        String newPassword
) {
}
