package com.lostmemory.aiserver.generation;

import static org.hamcrest.Matchers.containsString;
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
                        .value("Submit a generation request to ComfyUI"));
    }
}
