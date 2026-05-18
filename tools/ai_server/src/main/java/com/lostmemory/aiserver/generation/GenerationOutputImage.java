package com.lostmemory.aiserver.generation;

import io.swagger.v3.oas.annotations.media.Schema;

@Schema(description = "Generated output image metadata from ComfyUI history")
public record GenerationOutputImage(
        @Schema(description = "Generated output filename", example = "AI301_Test3_PromptSample_00001_.png")
        String filename,

        @Schema(description = "ComfyUI output subfolder", example = "", nullable = true)
        String subfolder,

        @Schema(description = "ComfyUI output type", example = "output")
        String type
) {
}
