package com.lostmemory.server.session.dto;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Size;

public record CreateSessionRequest(
        @Schema(description = "최대 인원 (호스트 포함, 1~4)", example = "2",
                requiredMode = Schema.RequiredMode.REQUIRED)
        @NotNull
        @Min(1)
        @Max(4)
        Integer maxPlayers,

        @Schema(description = "비공개 입장 코드 (4~20자 영숫자, 게스트가 /sessions/find?code= 로 사용)",
                example = "GKTYF6",
                requiredMode = Schema.RequiredMode.REQUIRED)
        @NotBlank
        @Size(min = 4, max = 20)
        String privateCode
) {
}
