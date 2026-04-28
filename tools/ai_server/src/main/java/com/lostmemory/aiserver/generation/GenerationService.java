package com.lostmemory.aiserver.generation;

import java.time.Instant;
import java.util.Map;
import java.util.UUID;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Service;
import org.springframework.web.client.RestClientException;
import org.springframework.web.client.RestClientResponseException;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lostmemory.aiserver.comfyui.ComfyUiClient;
import com.lostmemory.aiserver.common.exception.ApiRequestException;

@Service
public class GenerationService {

    private static final Logger log = LoggerFactory.getLogger(GenerationService.class);

    private final PromptAssemblyService promptAssemblyService;
    private final ComfyUiClient comfyUiClient;
    private final ObjectMapper objectMapper;

    public GenerationService(
            PromptAssemblyService promptAssemblyService,
            ComfyUiClient comfyUiClient,
            ObjectMapper objectMapper
    ) {
        this.promptAssemblyService = promptAssemblyService;
        this.comfyUiClient = comfyUiClient;
        this.objectMapper = objectMapper;
    }

    public GenerationRequestAcceptedResponse accept(CreateGenerationRequest request) {
        String requestId = UUID.randomUUID().toString();
        Map<String, Object> promptRequest = promptAssemblyService.assemble(request, requestId);

        log.debug("Submitting ComfyUI prompt. requestId={}, workflowId={}, body={}",
                requestId, request.workflowId(), toJson(promptRequest));

        Map<String, Object> promptResponse;
        try {
            promptResponse = comfyUiClient.submitPrompt(promptRequest);
        } catch (RestClientResponseException exception) {
            log.warn("ComfyUI /prompt request failed. requestId={}, workflowId={}, status={}, body={}",
                    requestId,
                    request.workflowId(),
                    exception.getStatusCode().value(),
                    exception.getResponseBodyAsString());
            throw new ApiRequestException(
                    HttpStatus.BAD_GATEWAY,
                    "COMFYUI_SUBMIT_FAILED",
                    "ComfyUI /prompt request failed.");
        } catch (RestClientException exception) {
            log.warn("ComfyUI /prompt request failed before receiving a response. requestId={}, workflowId={}, message={}",
                    requestId,
                    request.workflowId(),
                    exception.getMessage());
            throw new ApiRequestException(
                    HttpStatus.BAD_GATEWAY,
                    "COMFYUI_SUBMIT_FAILED",
                    "ComfyUI /prompt request failed.");
        }

        log.debug("ComfyUI prompt submitted. requestId={}, workflowId={}, response={}",
                requestId, request.workflowId(), toJson(promptResponse));

        return new GenerationRequestAcceptedResponse(
                requestId,
                extractPromptId(promptResponse),
                GenerationRequestStatus.SUBMITTED,
                request.workflowId(),
                normalizeUserId(request.userId()),
                request.prompt(),
                Instant.now());
    }

    private String normalizeUserId(String userId) {
        if (userId == null || userId.isBlank()) {
            return null;
        }

        return userId;
    }

    private String extractPromptId(Map<String, Object> responseBody) {
        Object promptId = responseBody.get("prompt_id");

        if (!(promptId instanceof String value) || value.isBlank()) {
            throw new ApiRequestException(
                    HttpStatus.BAD_GATEWAY,
                    "INVALID_COMFYUI_RESPONSE",
                    "ComfyUI /prompt response did not include prompt_id.");
        }

        return value;
    }

    private String toJson(Object value) {
        try {
            return objectMapper.writeValueAsString(value);
        } catch (JsonProcessingException exception) {
            return String.valueOf(value);
        }
    }
}
