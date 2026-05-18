package com.lostmemory.aiserver.generation;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import java.util.Map;

import org.junit.jupiter.api.Test;
import org.springframework.http.HttpStatus;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lostmemory.aiserver.common.exception.ApiRequestException;

class PromptAssemblyServiceTest {

    private final PromptAssemblyService promptAssemblyService = new PromptAssemblyService(new ObjectMapper());

    @Test
    void assembleReplacesPromptAndGeneratedFields() {
        CreateGenerationRequest request = new CreateGenerationRequest(
                "pixel-art-character-v1",
                "pixel art archer, green hood, idle pose",
                "ssafy-user-01");

        PromptAssemblyResult assembled = promptAssemblyService.assemble(request, "test-request-1234");

        Map<String, Object> prompt = nestedMap(assembled.promptRequest(), "prompt");
        Map<String, Object> promptNode = nestedMap(prompt, "4");
        Map<String, Object> promptInputs = nestedMap(promptNode, "inputs");
        Map<String, Object> seedNode = nestedMap(prompt, "6");
        Map<String, Object> seedInputs = nestedMap(seedNode, "inputs");
        Map<String, Object> saveNode = nestedMap(prompt, "8");
        Map<String, Object> saveInputs = nestedMap(saveNode, "inputs");

        assertThat(promptInputs.get("text")).isEqualTo("pixel art archer, green hood, idle pose");
        assertThat(assembled.promptRequest().get("client_id")).isEqualTo("ai-server-test-request-1234");
        assertThat(saveInputs.get("filename_prefix")).isEqualTo("AI404_pixel-art-character-v1_test-req");
        assertThat(((Number) seedInputs.get("seed")).longValue()).isPositive();
        assertThat(assembled.workflowName()).isEqualTo("Z-Image-turbo-test3-ksampler-change");
        assertThat(assembled.sourceFilename()).isEqualTo("Z-Image-turbo-test3-ksampler-change.json");
        assertThat(assembled.primaryModelName()).isEqualTo("z_image_turbo_bf16.safetensors");
        assertThat(assembled.modelMetadataJson().get("clipName").asText()).isEqualTo("qwen_3_4b.safetensors");
    }

    @Test
    void assembleRejectsUnsupportedWorkflow() {
        CreateGenerationRequest request = new CreateGenerationRequest(
                "unsupported-workflow",
                "pixel art archer, green hood, idle pose",
                null);

        assertThatThrownBy(() -> promptAssemblyService.assemble(request, "test-request-1234"))
                .isInstanceOf(ApiRequestException.class)
                .satisfies(exception -> {
                    ApiRequestException apiException = (ApiRequestException) exception;
                    assertThat(apiException.getStatus()).isEqualTo(HttpStatus.BAD_REQUEST);
                    assertThat(apiException.getCode()).isEqualTo("UNSUPPORTED_WORKFLOW");
                });
    }

    @SuppressWarnings("unchecked")
    private Map<String, Object> nestedMap(Map<String, Object> source, String key) {
        return (Map<String, Object>) source.get(key);
    }
}
