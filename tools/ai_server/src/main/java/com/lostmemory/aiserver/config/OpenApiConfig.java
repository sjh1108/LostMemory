package com.lostmemory.aiserver.config;

import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

import io.swagger.v3.oas.models.OpenAPI;
import io.swagger.v3.oas.models.info.Info;

@Configuration
public class OpenApiConfig {

    @Bean
    public OpenAPI aiToolOpenApi() {
        return new OpenAPI()
                .info(new Info()
                        .title("LostMemory AI Tool API")
                        .description("ComfyUI internal tool backend bootstrap and draft generation request API")
                        .version("v0.1.0-draft"));
    }
}
