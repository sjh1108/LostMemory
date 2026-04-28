package com.lostmemory.aiserver.generation;

import org.springframework.http.ResponseEntity;
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

    public GenerationController(GenerationService generationService) {
        this.generationService = generationService;
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
}
