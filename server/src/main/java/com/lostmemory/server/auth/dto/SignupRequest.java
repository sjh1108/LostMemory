package com.lostmemory.server.auth.dto;

import com.lostmemory.server.global.validation.PasswordPolicy;
import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.Email;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;

public record SignupRequest(
        @Schema(description = "로그인 ID (4~30자, UNIQUE)", example = "testuser",
                requiredMode = Schema.RequiredMode.REQUIRED)
        @NotBlank
        @Size(min = 4, max = 30)
        String loginId,

        @Schema(description = "비밀번호 (8~100자 + 영문·숫자·특수문자 모두 포함, 평문 전송 → BCrypt 해시 저장)",
                example = "Pass123!",
                requiredMode = Schema.RequiredMode.REQUIRED)
        @PasswordPolicy
        String password,

        @Schema(description = "연동 이메일 (인증 코드·비밀번호 재설정용, UNIQUE)", example = "tester@example.com",
                requiredMode = Schema.RequiredMode.REQUIRED)
        @NotBlank
        @Email
        @Size(max = 255)
        String email,

        @Schema(description = "인게임 닉네임 (2~30자, UNIQUE)", example = "테스터1",
                requiredMode = Schema.RequiredMode.REQUIRED)
        @NotBlank
        @Size(min = 2, max = 30)
        String nickname
) {
}
