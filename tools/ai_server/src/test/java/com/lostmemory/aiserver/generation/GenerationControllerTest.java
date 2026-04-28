package com.lostmemory.aiserver.generation;

import static org.hamcrest.Matchers.containsString;
import static org.hamcrest.Matchers.nullValue;
import static org.mockito.ArgumentMatchers.anyMap;
import static org.mockito.BDDMockito.given;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import java.util.Map;

import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.test.mock.mockito.MockBean;
import org.springframework.http.HttpStatus;
import org.springframework.http.MediaType;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.web.client.HttpClientErrorException;

import com.lostmemory.aiserver.comfyui.ComfyUiClient;

@SpringBootTest
@AutoConfigureMockMvc
@ActiveProfiles("test")
class GenerationControllerTest {

    @Autowired
    private MockMvc mockMvc;

    @MockBean
    private ComfyUiClient comfyUiClient;

    @Test
    void createGenerationRequestReturnsAcceptedResponse() throws Exception {
        given(comfyUiClient.submitPrompt(anyMap()))
                .willReturn(Map.of(
                        "prompt_id", "test-prompt-id-001",
                        "number", 1,
                        "node_errors", Map.of()));

        mockMvc.perform(post("/generation-requests")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "workflowId": "pixel-art-character-v1",
                                  "prompt": "pixel art mage girl, blue robe, idle pose",
                                  "userId": "ssafy-user-01"
                                }
                                """))
                .andExpect(status().isAccepted())
                .andExpect(jsonPath("$.success").value(true))
                .andExpect(jsonPath("$.data.workflowId").value("pixel-art-character-v1"))
                .andExpect(jsonPath("$.data.userId").value("ssafy-user-01"))
                .andExpect(jsonPath("$.data.status").value("SUBMITTED"))
                .andExpect(jsonPath("$.data.promptId").value("test-prompt-id-001"))
                .andExpect(jsonPath("$.data.requestId").isNotEmpty())
                .andExpect(jsonPath("$.data.submittedAt").isNotEmpty());
    }

    @Test
    void getGenerationHistoryReturnsCompletedHistoryResponse() throws Exception {
        given(comfyUiClient.fetchHistory("test-prompt-id-001"))
                .willReturn(Map.of(
                        "test-prompt-id-001", Map.of(
                                "outputs", Map.of(
                                        "8", Map.of("images", java.util.List.of(
                                                Map.of(
                                                        "filename", "AI405_Test_00001_.png",
                                                        "subfolder", "",
                                                        "type", "output")))),
                                "status", Map.of(
                                        "completed", true,
                                        "status_str", "success",
                                        "messages", java.util.List.of(
                                                java.util.List.of("execution_start", Map.of()),
                                                java.util.List.of("execution_success", Map.of()))))));

        mockMvc.perform(get("/generation-requests/test-prompt-id-001"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.success").value(true))
                .andExpect(jsonPath("$.data.promptId").value("test-prompt-id-001"))
                .andExpect(jsonPath("$.data.executionStatus").value("SUCCEEDED"))
                .andExpect(jsonPath("$.data.failureReason").value(nullValue()))
                .andExpect(jsonPath("$.data.completed").value(true))
                .andExpect(jsonPath("$.data.statusText").value("success"))
                .andExpect(jsonPath("$.data.message").value("이미지 생성이 완료되었습니다."))
                .andExpect(jsonPath("$.data.outputImage.filename").value("AI405_Test_00001_.png"))
                .andExpect(jsonPath("$.data.messageTypes[0]").value("execution_start"))
                .andExpect(jsonPath("$.data.messageTypes[1]").value("execution_success"));
    }

    @Test
    void getGenerationHistoryReturnsBusinessFailureResponseWhenOutputIsMissing() throws Exception {
        given(comfyUiClient.fetchHistory("test-prompt-id-002"))
                .willReturn(Map.of(
                        "test-prompt-id-002", Map.of(
                                "outputs", Map.of(),
                                "status", Map.of(
                                        "completed", true,
                                        "status_str", "success",
                                        "messages", java.util.List.of(
                                                java.util.List.of("execution_success", Map.of()))))));

        mockMvc.perform(get("/generation-requests/test-prompt-id-002"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.success").value(true))
                .andExpect(jsonPath("$.data.promptId").value("test-prompt-id-002"))
                .andExpect(jsonPath("$.data.executionStatus").value("FAILED"))
                .andExpect(jsonPath("$.data.failureReason").value("OUTPUT_MISSING"))
                .andExpect(jsonPath("$.data.completed").value(true))
                .andExpect(jsonPath("$.data.message").value("생성은 완료되었지만 결과 파일을 찾지 못했습니다."))
                .andExpect(jsonPath("$.data.outputImage").isEmpty());
    }

    @Test
    void getGenerationHistoryReturnsTimedOutBusinessResponse() throws Exception {
        given(comfyUiClient.fetchHistory("test-prompt-id-003"))
                .willReturn(Map.of());

        mockMvc.perform(get("/generation-requests/test-prompt-id-003"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.success").value(true))
                .andExpect(jsonPath("$.data.promptId").value("test-prompt-id-003"))
                .andExpect(jsonPath("$.data.executionStatus").value("TIMED_OUT"))
                .andExpect(jsonPath("$.data.failureReason").value("POLL_TIMEOUT"))
                .andExpect(jsonPath("$.data.completed").value(false))
                .andExpect(jsonPath("$.data.message").value("생성 시간이 예상보다 오래 걸려 요청을 종료했습니다."));
    }

    @Test
    void createGenerationRequestReturnsValidationErrorWhenWorkflowIdIsMissing() throws Exception {
        mockMvc.perform(post("/generation-requests")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "prompt": "pixel art mage girl, blue robe, idle pose"
                                }
                                """))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.success").value(false))
                .andExpect(jsonPath("$.error.code").value("INVALID_REQUEST"))
                .andExpect(jsonPath("$.error.message", containsString("workflowId")));
    }

    @Test
    void createGenerationRequestReturnsInvalidBodyWhenJsonIsMalformed() throws Exception {
        mockMvc.perform(post("/generation-requests")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "workflowId":
                                }
                                """))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.success").value(false))
                .andExpect(jsonPath("$.error.code").value("INVALID_REQUEST_BODY"));
    }

    @Test
    void createGenerationRequestReturnsBadRequestWhenWorkflowIsUnsupported() throws Exception {
        mockMvc.perform(post("/generation-requests")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "workflowId": "unknown-workflow",
                                  "prompt": "pixel art mage girl, blue robe, idle pose"
                                }
                                """))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.success").value(false))
                .andExpect(jsonPath("$.error.code").value("UNSUPPORTED_WORKFLOW"));
    }

    @Test
    void createGenerationRequestReturnsBadGatewayWhenComfyUiSubmitFails() throws Exception {
        given(comfyUiClient.submitPrompt(anyMap()))
                .willThrow(new HttpClientErrorException(HttpStatus.BAD_REQUEST));

        mockMvc.perform(post("/generation-requests")
                        .contentType(MediaType.APPLICATION_JSON)
                        .content("""
                                {
                                  "workflowId": "pixel-art-character-v1",
                                  "prompt": "pixel art mage girl, blue robe, idle pose"
                                }
                                """))
                .andExpect(status().isBadGateway())
                .andExpect(jsonPath("$.success").value(false))
                .andExpect(jsonPath("$.error.code").value("COMFYUI_SUBMIT_FAILED"));
    }

    @Test
    void openApiSpecContainsGenerationRequestEndpoint() throws Exception {
        mockMvc.perform(get("/v3/api-docs"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.paths['/generation-requests'].post").exists())
                .andExpect(jsonPath("$.paths['/generation-requests'].post.summary")
                        .value("Submit a generation request to ComfyUI"))
                .andExpect(jsonPath("$.paths['/generation-requests/{promptId}'].get.summary")
                        .value("Poll ComfyUI history and resolve generation state"));
    }
}
