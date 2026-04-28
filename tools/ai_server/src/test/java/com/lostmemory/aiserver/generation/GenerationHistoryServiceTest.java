package com.lostmemory.aiserver.generation;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.BDDMockito.given;

import java.time.Duration;
import java.util.List;
import java.util.Map;

import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lostmemory.aiserver.comfyui.ComfyUiClient;
import com.lostmemory.aiserver.common.exception.ApiRequestException;

@ExtendWith(MockitoExtension.class)
class GenerationHistoryServiceTest {

    @Mock
    private ComfyUiClient comfyUiClient;

    private final GenerationHistoryParser parser = new GenerationHistoryParser();
    private final ObjectMapper objectMapper = new ObjectMapper();

    @Test
    void pollUntilCompletedReturnsParsedOutputAfterPendingHistory() {
        given(comfyUiClient.fetchHistory("prompt-001"))
                .willReturn(Map.of())
                .willReturn(Map.of(
                        "prompt-001", Map.of(
                                "outputs", Map.of(
                                        "8", Map.of("images", List.of(
                                                Map.of(
                                                        "filename", "ready.png",
                                                        "subfolder", "",
                                                        "type", "output")))),
                                "status", Map.of(
                                        "completed", true,
                                        "status_str", "success",
                                        "messages", List.of(List.of("execution_success", Map.of()))))));

        GenerationHistoryService service = new GenerationHistoryService(
                comfyUiClient,
                parser,
                objectMapper,
                Duration.ZERO,
                Duration.ofSeconds(1),
                Duration.ofSeconds(5));

        GenerationHistoryResponse response = service.pollUntilCompleted("prompt-001");

        assertThat(response.promptId()).isEqualTo("prompt-001");
        assertThat(response.completed()).isTrue();
        assertThat(response.statusText()).isEqualTo("success");
        assertThat(response.outputImage()).isNotNull();
        assertThat(response.outputImage().filename()).isEqualTo("ready.png");
    }

    @Test
    void pollUntilCompletedThrowsTimeoutWhenHistoryNeverCompletes() {
        given(comfyUiClient.fetchHistory("prompt-002")).willReturn(Map.of());

        GenerationHistoryService service = new GenerationHistoryService(
                comfyUiClient,
                parser,
                objectMapper,
                Duration.ZERO,
                Duration.ZERO,
                Duration.ofMillis(5));

        assertThatThrownBy(() -> service.pollUntilCompleted("prompt-002"))
                .isInstanceOf(ApiRequestException.class)
                .hasMessage("ComfyUI history polling timed out.");
    }
}
