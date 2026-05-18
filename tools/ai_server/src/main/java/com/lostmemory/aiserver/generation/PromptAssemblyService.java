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
    private static final String WORKFLOW_NAME = "Z-Image-turbo-test3-ksampler-change";
    private static final String WORKFLOW_VERSION = "v1";
    private static final String SOURCE_FILENAME = "Z-Image-turbo-test3-ksampler-change.json";
    private static final String TEMPLATE_PATH = "comfyui/prompt-templates/pixel-art-character-v1.json";
    private static final String PROMPT_NODE_ID = "4";
    private static final String SEED_NODE_ID = "6";
    private static final String SAVE_IMAGE_NODE_ID = "8";
    private static final String UNET_NODE_ID = "1";
    private static final String LORA_NODE_ID = "3";
    private static final String CLIP_NODE_ID = "10";
    private static final String VAE_NODE_ID = "9";

    private final ObjectMapper objectMapper;

    public PromptAssemblyService(ObjectMapper objectMapper) {
        this.objectMapper = objectMapper;
    }

    public PromptAssemblyResult assemble(CreateGenerationRequest request, String requestId) {
        if (!SUPPORTED_WORKFLOW_ID.equals(request.workflowId())) {
            throw new ApiRequestException(
                    HttpStatus.BAD_REQUEST,
                    "UNSUPPORTED_WORKFLOW",
                    "workflowId is not supported yet: " + request.workflowId());
        }

        // sourceTemplate은 DB snapshot/hash 기준으로 쓴다.
        // requestId, random seed, filename_prefix를 섞지 않은 원본 template여야 같은 workflow를 재사용할 수 있다.
        JsonNode sourceTemplate = loadTemplate();

        // ComfyUI submit용 payload는 source template를 복사한 뒤 request별 실행값만 주입한다.
        JsonNode submitPayload = sourceTemplate.deepCopy();
        updatePrompt(submitPayload, request.prompt());
        updateSeed(submitPayload);
        updateFilenamePrefix(submitPayload, request.workflowId(), requestId);
        updateClientId(submitPayload, requestId);

        return new PromptAssemblyResult(
                objectMapper.convertValue(submitPayload, MAP_TYPE),
                sourceTemplate,
                WORKFLOW_NAME,
                WORKFLOW_VERSION,
                SOURCE_FILENAME,
                extractPrimaryModelName(sourceTemplate),
                extractModelMetadata(sourceTemplate));
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

    private String extractPrimaryModelName(JsonNode template) {
        return readText(template, "/prompt/" + UNET_NODE_ID + "/inputs/unet_name");
    }

    private JsonNode extractModelMetadata(JsonNode template) {
        ObjectNode metadata = objectMapper.createObjectNode();
        metadata.put("unetName", readText(template, "/prompt/" + UNET_NODE_ID + "/inputs/unet_name"));
        metadata.put("loraName", readText(template, "/prompt/" + LORA_NODE_ID + "/inputs/lora_name"));
        metadata.put("clipName", readText(template, "/prompt/" + CLIP_NODE_ID + "/inputs/clip_name"));
        metadata.put("vaeName", readText(template, "/prompt/" + VAE_NODE_ID + "/inputs/vae_name"));
        return metadata;
    }

    private String readText(JsonNode template, String pointer) {
        JsonNode node = template.at(pointer);
        return node.isMissingNode() || node.isNull() ? null : node.asText();
    }

    private String sanitize(String value) {
        return value.replaceAll("[^A-Za-z0-9_-]", "_");
    }
}
