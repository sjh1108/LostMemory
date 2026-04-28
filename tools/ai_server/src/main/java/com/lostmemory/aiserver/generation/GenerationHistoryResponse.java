package com.lostmemory.aiserver.generation;

import java.util.List;

import io.swagger.v3.oas.annotations.media.Schema;

@Schema(description = "Polled ComfyUI history result")
public record GenerationHistoryResponse(
        @Schema(description = "ComfyUI prompt identifier", example = "d4bc5cf9-f555-430c-a32b-b61e4c8b4bb6")
        String promptId,

        @Schema(description = "Resolved generation execution status")
        GenerationExecutionStatus executionStatus,

        @Schema(description = "Resolved failure reason when the execution did not succeed", nullable = true)
        GenerationFailureReason failureReason,

        @Schema(description = "Whether ComfyUI marked the execution as completed")
        boolean completed,

        @Schema(description = "ComfyUI status string", example = "success", nullable = true)
        String statusText,

        @Schema(description = "User-facing message for the current execution state", nullable = true)
        String message,

        @Schema(description = "First generated output image metadata", nullable = true)
        GenerationOutputImage outputImage,

        @Schema(description = "Observed ComfyUI status message types in order", nullable = true)
        List<String> messageTypes
) {
}
