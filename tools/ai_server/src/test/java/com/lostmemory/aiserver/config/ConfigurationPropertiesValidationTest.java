package com.lostmemory.aiserver.config;

import static org.assertj.core.api.Assertions.assertThat;

import com.lostmemory.aiserver.AiServerApplication;
import org.junit.jupiter.api.Test;
import org.springframework.boot.test.context.runner.WebApplicationContextRunner;

class ConfigurationPropertiesValidationTest {

    private final WebApplicationContextRunner contextRunner = new WebApplicationContextRunner()
            .withUserConfiguration(AiServerApplication.class)
            .withPropertyValues(
                    "server.port=0",
                    "comfyui.base-url=http://localhost:8188",
                    "database.postgres.host=localhost",
                    "database.postgres.port=5432",
                    "database.postgres.name=ai_tool",
                    "database.postgres.username=ai_tool",
                    "database.postgres.password=test-password",
                    "storage.s3.region=ap-northeast-2",
                    "storage.s3.bucket=test-bucket",
                    "storage.s3.access-key-id=test-access-key",
                    "storage.s3.secret-access-key=test-secret-key"
            );

    @Test
    void contextLoadsWhenRequiredPropertiesExist() {
        contextRunner.run(context -> assertThat(context.getStartupFailure()).isNull());
    }

    @Test
    void contextFailsWhenComfyUiBaseUrlIsMissing() {
        new WebApplicationContextRunner()
                .withUserConfiguration(AiServerApplication.class)
                .withPropertyValues(
                        "server.port=0",
                        "database.postgres.host=localhost",
                        "database.postgres.port=5432",
                        "database.postgres.name=ai_tool",
                        "database.postgres.username=ai_tool",
                        "database.postgres.password=test-password",
                        "storage.s3.region=ap-northeast-2",
                        "storage.s3.bucket=test-bucket",
                        "storage.s3.access-key-id=test-access-key",
                        "storage.s3.secret-access-key=test-secret-key"
                )
                .run(context -> {
                    assertThat(context.getStartupFailure())
                            .isNotNull()
                            .hasStackTraceContaining("Binding validation errors on comfyui")
                            .hasStackTraceContaining("baseUrl");
                });
    }
}
