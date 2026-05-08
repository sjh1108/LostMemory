package com.lostmemory.server.session.dto;

import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Size;

public record CreateSessionRequest(
        @NotNull
        @Min(1)
        @Max(4)
        Integer maxPlayers,

        @NotBlank
        @Size(min = 4, max = 20)
        String privateCode
) {
}
