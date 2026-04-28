package com.lostmemory.aiserver.generation;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.BDDMockito.given;
import static org.mockito.Mockito.verify;

import java.time.Duration;
import java.util.List;
import java.util.Map;

import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lostmemory.aiserver.comfyui.ComfyUiClient;
import com.lostmemory.aiserver.common.audit.AuditRecorder;

@ExtendWith(MockitoExtension.class)
class GenerationHistoryServiceTest {

    @Mock
    private ComfyUiClient comfyUiClient;

    @Mock
    private AuditRecorder auditRecorder;

    private final GenerationHistoryParser parser = new GenerationHistoryParser();
    private final GenerationStatusResolver statusResolver =
            new GenerationStatusResolver(new GenerationFailureMessageResolver());
    private final ObjectMapper objectMapper = new ObjectMapper();

    @Test
    void pollUntilCompletedReturnsSucceededResponseAfterPendingHistory() {
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
                statusResolver,
                auditRecorder,
                objectMapper,
                Duration.ZERO,
                Duration.ofSeconds(1),
                Duration.ofSeconds(5),
                3);

        GenerationHistoryResponse response = service.pollUntilCompleted("prompt-001");

        assertThat(response.promptId()).isEqualTo("prompt-001");
        assertThat(response.executionStatus()).isEqualTo(GenerationExecutionStatus.SUCCEEDED);
        assertThat(response.failureReason()).isNull();
        assertThat(response.completed()).isTrue();
        assertThat(response.statusText()).isEqualTo("success");
        assertThat(response.message()).isEqualTo("이미지 생성이 완료되었습니다.");
        assertThat(response.outputImage()).isNotNull();
        assertThat(response.outputImage().filename()).isEqualTo("ready.png");
    }

    @Test
    void pollUntilCompletedReturnsTimedOutResponseWhenHistoryNeverCompletes() {
        given(comfyUiClient.fetchHistory("prompt-002")).willReturn(Map.of());

        GenerationHistoryService service = new GenerationHistoryService(
                comfyUiClient,
                parser,
                statusResolver,
                auditRecorder,
                objectMapper,
                Duration.ZERO,
                Duration.ZERO,
                Duration.ofMillis(5),
                3);

        GenerationHistoryResponse response = service.pollUntilCompleted("prompt-002");

        assertThat(response.executionStatus()).isEqualTo(GenerationExecutionStatus.TIMED_OUT);
        assertThat(response.failureReason()).isEqualTo(GenerationFailureReason.POLL_TIMEOUT);
        assertThat(response.completed()).isFalse();
        assertThat(response.message()).isEqualTo("생성 시간이 예상보다 오래 걸려 요청을 종료했습니다.");
        verify(auditRecorder).record(org.mockito.ArgumentMatchers.eq("generation"),
                org.mockito.ArgumentMatchers.eq("terminal_failure"),
                org.mockito.ArgumentMatchers.anyMap());
    }

    @Test
    void pollUntilCompletedReturnsFailedResponseAfterThreeConsecutiveFetchFailures() {
        given(comfyUiClient.fetchHistory("prompt-003"))
                .willThrow(new org.springframework.web.client.RestClientException("temporary network failure"));

        GenerationHistoryService service = new GenerationHistoryService(
                comfyUiClient,
                parser,
                statusResolver,
                auditRecorder,
                objectMapper,
                Duration.ZERO,
                Duration.ofSeconds(1),
                Duration.ofSeconds(5),
                3);

        GenerationHistoryResponse response = service.pollUntilCompleted("prompt-003");

        assertThat(response.executionStatus()).isEqualTo(GenerationExecutionStatus.FAILED);
        assertThat(response.failureReason()).isEqualTo(GenerationFailureReason.HISTORY_FETCH_FAILED);
        assertThat(response.message()).isEqualTo("생성 결과를 확인하는 중 문제가 발생했습니다. 잠시 후 다시 시도해주세요.");
    }

    @Test
    void pollUntilCompletedReturnsFailedResponseWhenTerminalSuccessHasNoOutput() {
        given(comfyUiClient.fetchHistory("prompt-004"))
                .willReturn(Map.of(
                        "prompt-004", Map.of(
                                "outputs", Map.of(),
                                "status", Map.of(
                                        "completed", true,
                                        "status_str", "success",
                                        "messages", List.of(List.of("execution_success", Map.of()))))));

        GenerationHistoryService service = new GenerationHistoryService(
                comfyUiClient,
                parser,
                statusResolver,
                auditRecorder,
                objectMapper,
                Duration.ZERO,
                Duration.ofSeconds(1),
                Duration.ofSeconds(5),
                3);

        GenerationHistoryResponse response = service.pollUntilCompleted("prompt-004");

        assertThat(response.executionStatus()).isEqualTo(GenerationExecutionStatus.FAILED);
        assertThat(response.failureReason()).isEqualTo(GenerationFailureReason.OUTPUT_MISSING);
        assertThat(response.completed()).isTrue();
        assertThat(response.outputImage()).isNull();
        assertThat(response.message()).isEqualTo("생성은 완료되었지만 결과 파일을 찾지 못했습니다.");
    }

    @Test
    void pollUntilCompletedReturnsFailedResponseWhenCompletedStatusIsNotSuccess() {
        given(comfyUiClient.fetchHistory("prompt-005"))
                .willReturn(Map.of(
                        "prompt-005", Map.of(
                                "outputs", Map.of(
                                        "8", Map.of("images", List.of(
                                                Map.of(
                                                        "filename", "broken.png",
                                                        "subfolder", "",
                                                        "type", "output")))),
                                "status", Map.of(
                                        "completed", true,
                                        "status_str", "error",
                                        "messages", List.of(List.of("execution_error", Map.of()))))));

        GenerationHistoryService service = new GenerationHistoryService(
                comfyUiClient,
                parser,
                statusResolver,
                auditRecorder,
                objectMapper,
                Duration.ZERO,
                Duration.ofSeconds(1),
                Duration.ofSeconds(5),
                3);

        GenerationHistoryResponse response = service.pollUntilCompleted("prompt-005");

        assertThat(response.executionStatus()).isEqualTo(GenerationExecutionStatus.FAILED);
        assertThat(response.failureReason()).isEqualTo(GenerationFailureReason.COMFYUI_REPORTED_FAILURE);
        assertThat(response.completed()).isTrue();
        assertThat(response.message()).isEqualTo("이미지 생성에 실패했습니다.");
    }

    @Test
    void pollUntilCompletedReturnsUnknownFailureWhenStatusTextIsMissing() {
        given(comfyUiClient.fetchHistory("prompt-006"))
                .willReturn(Map.of(
                        "prompt-006", Map.of(
                                "outputs", Map.of(
                                        "8", Map.of("images", List.of(
                                                Map.of(
                                                        "filename", "mystery.png",
                                                        "subfolder", "",
                                                        "type", "output")))),
                                "status", Map.of(
                                        "completed", true,
                                        "messages", List.of(List.of("execution_success", Map.of()))))));

        GenerationHistoryService service = new GenerationHistoryService(
                comfyUiClient,
                parser,
                statusResolver,
                auditRecorder,
                objectMapper,
                Duration.ZERO,
                Duration.ofSeconds(1),
                Duration.ofSeconds(5),
                3);

        GenerationHistoryResponse response = service.pollUntilCompleted("prompt-006");

        assertThat(response.executionStatus()).isEqualTo(GenerationExecutionStatus.FAILED);
        assertThat(response.failureReason()).isEqualTo(GenerationFailureReason.UNKNOWN_FAILURE);
        assertThat(response.completed()).isTrue();
        assertThat(response.message()).isEqualTo("이미지 생성에 실패했습니다.");
    }
}
