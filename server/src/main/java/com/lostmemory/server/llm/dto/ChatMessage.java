package com.lostmemory.server.llm.dto;

import com.fasterxml.jackson.annotation.JsonIgnoreProperties;
import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Pattern;

/**
 * OpenAI Chat Completions 호환 메시지.
 * role 은 system / user / assistant 셋만 — tool/function 은 본 게임 단계에서 불요.
 * unknown 필드 거부 ({@code ignoreUnknown=false}) → request smuggling 차단.
 */
@JsonIgnoreProperties(ignoreUnknown = false)
public record ChatMessage(
        @Schema(description = "메시지 역할", example = "user",
                requiredMode = Schema.RequiredMode.REQUIRED,
                allowableValues = {"system", "user", "assistant"})
        @NotBlank
        @Pattern(regexp = "system|user|assistant",
                message = "role 은 system/user/assistant 중 하나여야 합니다")
        String role,

        @Schema(description = "메시지 본문 텍스트",
                requiredMode = Schema.RequiredMode.REQUIRED)
        @NotBlank
        String content
) {
}
