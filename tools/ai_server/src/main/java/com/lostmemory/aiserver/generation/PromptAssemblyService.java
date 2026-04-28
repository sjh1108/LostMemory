package com.lostmemory.aiserver.generation;

import java.io.IOException;
import java.io.InputStream;
import java.io.UncheckedIOException;
import java.util.Map;
import java.util.concurrent.ThreadLocalRandom;

import org.springframework.core.io.ClassPathResource;
import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Service;

import com.fasterxml.jackson.core.type.TypeReference;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ObjectNode;
import com.lostmemory.aiserver.common.exception.ApiRequestException;

@Service
public class PromptAssemblyService {

    private static final TypeReference<Map<String, Object>> MAP_TYPE = new TypeReference<>() {
    };

    private static final String SUPPORTED_WORKFLOW_ID = "pixel-art-character-v1";
    private static final String TEMPLATE_PATH = "comfyui/prompt-templates/pixel-art-character-v1.json";
    private static final String PROMPT_NODE_ID = "4";
    private static final String SEED_NODE_ID = "6";
    private static final String SAVE_IMAGE_NODE_ID = "8";

    private final ObjectMapper objectMapper;

    public PromptAssemblyService(ObjectMapper objectMapper) {
        this.objectMapper = objectMapper;
    }

    public Map<String, Object> assemble(CreateGenerationRequest request, String requestId) {
        if (!SUPPORTED_WORKFLOW_ID.equals(request.workflowId())) {
            throw new ApiRequestException(
                    HttpStatus.BAD_REQUEST,
                    "UNSUPPORTED_WORKFLOW",
                    "workflowId is not supported yet: " + request.workflowId());
        }

        JsonNode template = loadTemplate();
        updatePrompt(template, request.prompt());
        updateSeed(template);
        updateFilenamePrefix(template, request.workflowId(), requestId);
        updateClientId(template, requestId);

        return objectMapper.convertValue(template, MAP_TYPE);
    }

    private JsonNode loadTemplate() {
        ClassPathResource resource = new ClassPathResource(TEMPLATE_PATH);

        try (InputStream inputStream = resource.getInputStream()) {
            return objectMapper.readTree(inputStream);
        } catch (IOException exception) {
            throw new UncheckedIOException("Failed to load ComfyUI prompt template.", exception);
        }
    }

    private void updatePrompt(JsonNode template, String prompt) {
        ObjectNode promptInputs = (ObjectNode) template.at("/prompt/" + PROMPT_NODE_ID + "/inputs");
        promptInputs.put("text", prompt);
    }

    private void updateSeed(JsonNode template) {
        ObjectNode seedInputs = (ObjectNode) template.at("/prompt/" + SEED_NODE_ID + "/inputs");
        seedInputs.put("seed", ThreadLocalRandom.current().nextLong(1L, Long.MAX_VALUE));
    }

    private void updateFilenamePrefix(JsonNode template, String workflowId, String requestId) {
        ObjectNode saveInputs = (ObjectNode) template.at("/prompt/" + SAVE_IMAGE_NODE_ID + "/inputs");
        saveInputs.put("filename_prefix", "AI404_" + sanitize(workflowId) + "_" + requestId.substring(0, 8));
    }

    private void updateClientId(JsonNode template, String requestId) {
        ((ObjectNode) template).put("client_id", "ai-server-" + requestId);
    }

    private String sanitize(String value) {
        return value.replaceAll("[^A-Za-z0-9_-]", "_");
    }
}
