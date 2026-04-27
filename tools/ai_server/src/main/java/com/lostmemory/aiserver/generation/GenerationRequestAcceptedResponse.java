package com.lostmemory.aiserver.generation;

import java.time.Instant;

import io.swagger.v3.oas.annotations.media.Schema;

@Schema(description = "Accepted generation request summary")
public record GenerationRequestAcceptedResponse(
        @Schema(description = "Server-side draft request identifier", example = "3f2f2b6b-5654-4474-aadc-fb28279e3148")
        String requestId,

        @Schema(description = "Current request status")
        GenerationRequestStatus status,

        @Schema(description = "Registered workflow identifier", example = "pixel-art-character-v1")
        String workflowId,

        @Schema(description = "Optional requester identifier", example = "ssafy-user-01", nullable = true)
        String userId,

        @Schema(description = "Prompt text received by the endpoint", example = "pixel art mage girl, blue robe, idle pose")
        String prompt,

        @Schema(description = "Time when the server accepted the request")
        Instant acceptedAt
) {
}
