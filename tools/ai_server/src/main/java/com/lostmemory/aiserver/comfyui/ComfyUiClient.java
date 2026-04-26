package com.lostmemory.aiserver.comfyui;

import java.util.Map;

import org.springframework.core.ParameterizedTypeReference;
import org.springframework.http.MediaType;
import org.springframework.stereotype.Component;
import org.springframework.web.client.RestClient;

@Component
public class ComfyUiClient {

    private static final ParameterizedTypeReference<Map<String, Object>> MAP_TYPE =
            new ParameterizedTypeReference<>() {
            };

    private final RestClient restClient;

    public ComfyUiClient(RestClient comfyUiRestClient) {
        this.restClient = comfyUiRestClient;
    }

    public Map<String, Object> submitPrompt(Map<String, Object> requestBody) {
        return restClient.post()
                .uri("/prompt")
                .contentType(MediaType.APPLICATION_JSON)
                .body(requestBody)
                .retrieve()
                .body(MAP_TYPE);
    }

    public Map<String, Object> fetchHistory(String promptId) {
        return restClient.get()
                .uri("/history/{promptId}", promptId)
                .retrieve()
                .body(MAP_TYPE);
    }
}
