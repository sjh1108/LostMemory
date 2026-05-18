package com.lostmemory.server.auth.dto;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;

public record SignupRequest(
        @Schema(description = "로그인 ID (영숫자 4~30자, UNIQUE)", example = "testuser",
                requiredMode = Schema.RequiredMode.REQUIRED)
        @NotBlank
        @Size(min = 4, max = 30)
        String loginId,

        @Schema(description = "비밀번호 (8~100자 평문 전송 → BCrypt 해시 저장)", example = "p@ssw0rd",
                requiredMode = Schema.RequiredMode.REQUIRED)
        @NotBlank
        @Size(min = 8, max = 100)
        String password,

        @Schema(description = "인게임 닉네임 (2~30자, UNIQUE)", example = "테스터1",
                requiredMode = Schema.RequiredMode.REQUIRED)
        @NotBlank
        @Size(min = 2, max = 30)
        String nickname
) {
}
