package com.lostmemory.aiserver.config;

import static org.assertj.core.api.Assertions.assertThat;

import com.lostmemory.aiserver.comfyui.ComfyUiClient;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.web.client.RestClient;

@SpringBootTest
@ActiveProfiles("test")
class ComfyUiClientContextTest {

    @Autowired
    private RestClient comfyUiRestClient;

    @Autowired
    private ComfyUiClient comfyUiClient;

    @Autowired
    private ComfyUiProperties comfyUiProperties;

    @Autowired
    private PostgresProperties postgresProperties;

    @Test
    void configBeansLoadWithExpectedValues() {
        assertThat(comfyUiRestClient).isNotNull();
        assertThat(comfyUiClient).isNotNull();
        assertThat(comfyUiProperties.getHost()).isEqualTo("localhost");
        assertThat(comfyUiProperties.getPort()).isEqualTo(8188);
        assertThat(comfyUiProperties.getConnectTimeoutMs()).isEqualTo(5000);
        assertThat(comfyUiProperties.getReadTimeoutSeconds()).isEqualTo(30);
        assertThat(postgresProperties.getJdbcUrl()).isEqualTo("jdbc:postgresql://localhost:5432/ai_tool");
    }
}
