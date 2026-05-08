package com.lostmemory.server.session.dto;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;

public record JoinSessionRequest(
        @NotBlank
        @Size(min = 4, max = 20)
        String privateCode
) {
}
