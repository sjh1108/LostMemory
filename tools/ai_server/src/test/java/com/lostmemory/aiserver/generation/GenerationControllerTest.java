package com.lostmemory.aiserver.generation;

import static org.hamcrest.Matchers.containsString;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.http.MediaType;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.web.servlet.MockMvc;

@SpringBootTest
@AutoConfigureMockMvc
@ActiveProfiles("test")
class GenerationControllerTest {

    @Autowired
    private MockMvc mockMvc;

    @Test
    void createGenerationRequestReturnsAcceptedResponse() throws Exception {
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
                .andExpect(jsonPath("$.data.status").value("RECEIVED"))
                .andExpect(jsonPath("$.data.requestId").isNotEmpty());
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
    void openApiSpecContainsGenerationRequestEndpoint() throws Exception {
        mockMvc.perform(get("/v3/api-docs"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.paths['/generation-requests'].post").exists())
                .andExpect(jsonPath("$.paths['/generation-requests'].post.summary")
                        .value("Accept a minimal generation request"));
    }
}
