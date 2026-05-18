package com.lostmemory.server.global.config;

import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.web.cors.CorsConfiguration;
import org.springframework.web.cors.CorsConfigurationSource;
import org.springframework.web.cors.UrlBasedCorsConfigurationSource;

import java.util.Arrays;
import java.util.List;

/**
 * CORS 정책. dev 는 와일드카드, prod 는 도메인 화이트리스트.
 * 환경변수 CORS_ALLOWED_ORIGIN_PATTERNS 로 환경별 분기 (콤마 구분).
 *
 * 예:
 *   dev:  CORS_ALLOWED_ORIGIN_PATTERNS=*
 *   prod: CORS_ALLOWED_ORIGIN_PATTERNS=https://k14c201.p.ssafy.io
 *
 * Unity Standalone/Mobile 빌드면 SOP 적용 안 돼서 사실상 무의미하지만,
 * WebGL 빌드 또는 외부 도구(Swagger UI 의 "Try it out" 등) 호출에 영향 있음.
 *
 * setAllowedOriginPatterns 사용 — 와일드카드 + allowCredentials=true 조합 허용 (setAllowedOrigins 는 둘 동시 불가).
 */
@Configuration
public class CorsConfig {

    private final List<String> allowedOriginPatterns;

    public CorsConfig(@Value("${app.cors.allowed-origin-patterns}") String patterns) {
        this.allowedOriginPatterns = Arrays.asList(patterns.split("\\s*,\\s*"));
    }

    /** 전역 CORS 정책 빈. SecurityConfig 의 cors() 가 자동 픽업 */
    @Bean
    public CorsConfigurationSource corsConfigurationSource() {
        CorsConfiguration config = new CorsConfiguration();
        config.setAllowedOriginPatterns(allowedOriginPatterns);
        config.setAllowedMethods(List.of("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"));
        config.setAllowedHeaders(List.of("*"));
        config.setExposedHeaders(List.of("Authorization"));
        config.setAllowCredentials(true);
        config.setMaxAge(3600L);

        UrlBasedCorsConfigurationSource source = new UrlBasedCorsConfigurationSource();
        source.registerCorsConfiguration("/**", config);
        return source;
    }
}
