package com.lostmemory.server.llm.config;

import org.springframework.boot.context.properties.ConfigurationProperties;

/**
 * vLLM 프록시 설정 + 검증 임시 dev bypass 스위치.
 * dev-bypass 분기는 클라 로그인 미완성 단계에서 endpoint 검증 막힘 해소용.
 * 정식 단계에서 본 record 의 dev-bypass 서브 record + JwtAuthenticationFilter 분기 제거 예정.
 */
@ConfigurationProperties(prefix = "llm")
public record LlmProperties(
        String baseUrl,
        String apiKey,
        DevBypass devBypass
) {

    public record DevBypass(boolean enabled, String secret) {
    }
}
