package com.lostmemory.aiserver.generation;

import java.time.Instant;
import java.util.Map;
import java.util.UUID;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.dao.DataAccessException;
import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Service;
import org.springframework.web.client.RestClientException;
import org.springframework.web.client.RestClientResponseException;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lostmemory.aiserver.comfyui.ComfyUiClient;
import com.lostmemory.aiserver.common.audit.AuditActionType;
import com.lostmemory.aiserver.common.audit.AuditRecorder;
import com.lostmemory.aiserver.common.audit.AuditStatus;
import com.lostmemory.aiserver.common.exception.ApiRequestException;

@Service
public class GenerationService {

    private static final Logger log = LoggerFactory.getLogger(GenerationService.class);

    private final PromptAssemblyService promptAssemblyService;
    private final WorkflowSnapshotService workflowSnapshotService;
    private final GenerationMetadataService generationMetadataService;
    private final ComfyUiClient comfyUiClient;
    private final AuditRecorder auditRecorder;
    private final ObjectMapper objectMapper;

    public GenerationService(
            PromptAssemblyService promptAssemblyService,
            WorkflowSnapshotService workflowSnapshotService,
            GenerationMetadataService generationMetadataService,
            ComfyUiClient comfyUiClient,
            AuditRecorder auditRecorder,
            ObjectMapper objectMapper
    ) {
        this.promptAssemblyService = promptAssemblyService;
        this.workflowSnapshotService = workflowSnapshotService;
        this.generationMetadataService = generationMetadataService;
        this.comfyUiClient = comfyUiClient;
        this.auditRecorder = auditRecorder;
        this.objectMapper = objectMapper;
    }

    public GenerationRequestAcceptedResponse accept(CreateGenerationRequest request) {
        String requestId = UUID.randomUUID().toString();
        PromptAssemblyResult assemblyResult = promptAssemblyService.assemble(request, requestId);
        WorkflowSnapshotEntity workflowSnapshot = persistWorkflowSnapshot(requestId, assemblyResult);

        log.debug("Submitting ComfyUI prompt. requestId={}, workflowId={}, body={}",
                requestId, request.workflowId(), toJson(assemblyResult.promptRequest()));

        Map<String, Object> promptResponse;
        try {
            promptResponse = comfyUiClient.submitPrompt(assemblyResult.promptRequest());
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

        String promptId = extractPromptId(promptResponse);
        persistGenerationMetadata(requestId, request, promptId, workflowSnapshot);

        return new GenerationRequestAcceptedResponse(
                requestId,
                promptId,
                GenerationRequestStatus.SUBMITTED,
                request.workflowId(),
                normalizeUserId(request.userId()),
                request.prompt(),
                Instant.now());
    }

    private WorkflowSnapshotEntity persistWorkflowSnapshot(String requestId, PromptAssemblyResult assemblyResult) {
        try {
            return workflowSnapshotService.getOrCreateSnapshot(assemblyResult);
        } catch (DataAccessException exception) {
            log.error("Failed to save workflow snapshot before ComfyUI submit. requestId={}, workflowName={}, message={}",
                    requestId,
                    assemblyResult.workflowName(),
                    exception.getMessage(),
                    exception);

            auditRecorder.record(
                    AuditActionType.GENERATE,
                    AuditStatus.FAILED,
                    Map.of(
                            "requestId", requestId,
                            "failedStage", "METADATA_SAVE",
                            "workflowName", assemblyResult.workflowName(),
                            "failureReason", "WORKFLOW_SNAPSHOT_SAVE_FAILED"));

            throw new ApiRequestException(
                    HttpStatus.INTERNAL_SERVER_ERROR,
                    "WORKFLOW_SNAPSHOT_SAVE_FAILED",
                    "Failed to save workflow metadata before generation submit.");
        }
    }

    private void persistGenerationMetadata(
            String requestId,
            CreateGenerationRequest request,
            String promptId,
            WorkflowSnapshotEntity workflowSnapshot
    ) {
        try {
            generationMetadataService.saveSubmittedGeneration(request, promptId, workflowSnapshot);
        } catch (DataAccessException exception) {
            log.error("Failed to save generation metadata after ComfyUI submit. requestId={}, promptId={}, message={}",
                    requestId,
                    promptId,
                    exception.getMessage(),
                    exception);

            auditRecorder.record(
                    AuditActionType.GENERATE,
                    AuditStatus.FAILED,
                    Map.of(
                            "requestId", requestId,
                            "promptId", promptId,
                            "failedStage", "METADATA_SAVE",
                            "failureReason", "GENERATION_METADATA_SAVE_FAILED"));

            throw new ApiRequestException(
                    HttpStatus.INTERNAL_SERVER_ERROR,
                    "GENERATION_METADATA_SAVE_FAILED",
                    "Failed to save generation metadata after ComfyUI submit.");
        }
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
