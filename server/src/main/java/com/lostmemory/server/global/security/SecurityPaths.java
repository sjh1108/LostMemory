package com.lostmemory.server.global.security;

/**
 * Security 공통 경로 상수.
 * SecurityConfig 화이트리스트와 JwtAuthenticationFilter shouldNotFilter 가 동일한 패턴을 참조한다.
 * 매처는 context-path 제거 후 경로로 동작하므로 모든 패턴은 /api 접두사 없이 작성.
 */
public final class SecurityPaths {

    private SecurityPaths() {
    }

    /** 인증 없이 접근 가능한 경로(공개 API + Swagger + 헬스체크 + Prometheus scrape + favicon) */
    public static final String[] WHITELIST = {
            "/auth/**",
            "/swagger-ui/**",
            "/swagger-ui.html",
            "/v3/api-docs/**",
            "/actuator/health",
            "/actuator/prometheus",
            "/favicon.ico"
    };
}
