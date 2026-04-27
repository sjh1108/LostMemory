package com.lostmemory.aiserver.generation;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;

@Schema(description = "AI image generation request draft payload")
public record CreateGenerationRequest(
        @Schema(description = "Registered workflow identifier", example = "pixel-art-character-v1")
        @NotBlank(message = "workflowId is required")
        @Size(max = 100, message = "workflowId must be 100 characters or fewer")
        String workflowId,

        @Schema(description = "Prompt text to use for generation", example = "pixel art mage girl, blue robe, idle pose")
        @NotBlank(message = "prompt is required")
        @Size(max = 2000, message = "prompt must be 2000 characters or fewer")
        String prompt,

        @Schema(description = "Optional requester identifier", example = "ssafy-user-01", nullable = true)
        @Size(max = 64, message = "userId must be 64 characters or fewer")
        String userId
) {
}
