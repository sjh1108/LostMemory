package com.lostmemory.server.session.dto;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;

public record JoinSessionRequest(
        @Schema(description = "입장 코드 — path 의 sessionId 와 일치해야 함 (호스트가 설정한 값)",
                example = "GKTYF6",
                requiredMode = Schema.RequiredMode.REQUIRED)
        @NotBlank
        @Size(min = 4, max = 20)
        String privateCode
) {
}
