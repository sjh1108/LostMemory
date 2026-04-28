package com.lostmemory.aiserver.generation;

import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import com.lostmemory.aiserver.common.response.ApiResponse;

import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.media.Content;
import io.swagger.v3.oas.annotations.media.Schema;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;

@RestController
@RequestMapping("/generation-requests")
@Tag(name = "Generation Requests", description = "Draft generation request endpoints")
public class GenerationController {

    private final GenerationService generationService;
    private final GenerationHistoryService generationHistoryService;

    public GenerationController(
            GenerationService generationService,
            GenerationHistoryService generationHistoryService
    ) {
        this.generationService = generationService;
        this.generationHistoryService = generationHistoryService;
    }

    @PostMapping
    @Operation(
            summary = "Submit a generation request to ComfyUI",
            description = "AI-404 endpoint. This step validates the request contract, assembles a ComfyUI /prompt body, submits it, and returns the received prompt_id.",
            responses = {
                    @io.swagger.v3.oas.annotations.responses.ApiResponse(
                            responseCode = "202",
                            description = "Generation request submitted to ComfyUI",
                            content = @Content(schema = @Schema(implementation = ApiResponse.class))),
                    @io.swagger.v3.oas.annotations.responses.ApiResponse(
                            responseCode = "400",
                            description = "Invalid request payload",
                            content = @Content(schema = @Schema(implementation = ApiResponse.class))),
                    @io.swagger.v3.oas.annotations.responses.ApiResponse(
                            responseCode = "502",
                            description = "ComfyUI /prompt submission failed",
                            content = @Content(schema = @Schema(implementation = ApiResponse.class)))
            }
    )
    public ResponseEntity<ApiResponse<GenerationRequestAcceptedResponse>> createGenerationRequest(
            @Valid @RequestBody CreateGenerationRequest request) {
        return ResponseEntity.accepted()
                .body(ApiResponse.success(generationService.accept(request)));
    }

    @GetMapping("/{promptId}")
    @Operation(
            summary = "Poll ComfyUI history and resolve generation state",
            description = "AI-406 endpoint. This step polls ComfyUI /history/{promptId}, resolves success, failure, or timeout as a business status, and returns the first output image metadata when available.",
            responses = {
                    @io.swagger.v3.oas.annotations.responses.ApiResponse(
                            responseCode = "200",
                            description = "Generation state resolved as success, failure, timeout, or in-progress business status",
                            content = @Content(schema = @Schema(implementation = ApiResponse.class))),
                    @io.swagger.v3.oas.annotations.responses.ApiResponse(
                            responseCode = "500",
                            description = "Unexpected polling interruption or internal server error",
                            content = @Content(schema = @Schema(implementation = ApiResponse.class)))
            }
    )
    public ResponseEntity<ApiResponse<GenerationHistoryResponse>> getGenerationHistory(
            @PathVariable String promptId
    ) {
        return ResponseEntity.ok(ApiResponse.success(generationHistoryService.pollUntilCompleted(promptId)));
    }
}
