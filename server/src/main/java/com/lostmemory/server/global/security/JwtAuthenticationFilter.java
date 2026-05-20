package com.lostmemory.server.global.security;

import com.lostmemory.server.global.exception.BusinessException;
import com.lostmemory.server.llm.config.LlmProperties;
import jakarta.servlet.FilterChain;
import jakarta.servlet.ServletException;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.http.HttpHeaders;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.security.web.servlet.util.matcher.PathPatternRequestMatcher;
import org.springframework.security.web.util.matcher.OrRequestMatcher;
import org.springframework.security.web.util.matcher.RequestMatcher;
import org.springframework.stereotype.Component;
import org.springframework.web.filter.OncePerRequestFilter;

import java.io.IOException;
import java.util.Arrays;
import java.util.Collections;

@Slf4j
@Component
@RequiredArgsConstructor
public class JwtAuthenticationFilter extends OncePerRequestFilter {

    private static final String BEARER_PREFIX = "Bearer ";
    // TODO(S14P31C201-XXX): remove dev bypass when client login lands
    private static final String DEV_BYPASS_HEADER = "X-Dev-Bypass";
    private static final Long DEV_BYPASS_USER_ID = -1L;

    private static final RequestMatcher WHITELIST_MATCHER = new OrRequestMatcher(
            Arrays.stream(SecurityPaths.WHITELIST)
                    .map(p -> PathPatternRequestMatcher.withDefaults().matcher(p))
                    .toArray(RequestMatcher[]::new));

    private final JwtProvider jwtProvider;
    private final ErrorResponseWriter errorResponseWriter;
    private final LlmProperties llmProperties;

    /**
     * 화이트리스트(공개 API/Swagger/헬스체크 등) 경로는 필터 자체를 스킵.
     * 클라가 모든 요청에 Bearer 헤더를 박는 패턴에서 만료/잘못된 토큰이 와도
     * 공개 경로는 401 로 떨구지 않고 정상 응답이 나가도록.
     */
    @Override
    protected boolean shouldNotFilter(HttpServletRequest request) {
        return WHITELIST_MATCHER.matches(request);
    }

    /**
     * Authorization: Bearer 헤더가 있으면 access 토큰으로 파싱·검증해 SecurityContext 에 인증 주입.
     * 헤더 자체가 없으면 그냥 통과(SecurityFilterChain 의 authorizeHttpRequests 가 401 로 떨굼).
     * 헤더는 있으나 토큰이 무효/만료/잘못된 type 이면 ErrorResponseWriter 로 401 직접 응답하고 체인 중단.
     *
     * 검증 임시: DEV_BYPASS_ENABLED=true + X-Dev-Bypass 헤더 + secret 일치 시 가짜 인증 주입 후 체인 통과.
     * shouldNotFilter 만 true 로 두면 다음 단계 AuthorizationFilter 가 anonymous 로 보고 401 떨구므로,
     * SecurityContext 에 Authentication 명시 주입이 필요하다. 정식 단계에서 본 분기 + LlmProperties.DevBypass 제거 예정.
     */
    @Override
    protected void doFilterInternal(HttpServletRequest request,
                                    HttpServletResponse response,
                                    FilterChain filterChain) throws ServletException, IOException {
        // TODO(S14P31C201-XXX): remove dev bypass when client login lands
        if (matchesDevBypass(request)) {
            log.info("[DEV-BYPASS] hit: {} {}", request.getMethod(), request.getRequestURI());
            UsernamePasswordAuthenticationToken bypassAuth =
                    new UsernamePasswordAuthenticationToken(DEV_BYPASS_USER_ID, null, Collections.emptyList());
            SecurityContextHolder.getContext().setAuthentication(bypassAuth);
            filterChain.doFilter(request, response);
            return;
        }

        String token = extractBearerToken(request);
        if (token == null) {
            filterChain.doFilter(request, response);
            return;
        }

        Long userId;
        try {
            userId = jwtProvider.parseAccessToken(token);
        } catch (BusinessException e) {
            SecurityContextHolder.clearContext();
            log.warn("JWT auth failed: {} - {}", e.errorCode().name(), e.getMessage());
            errorResponseWriter.write(response, e.errorCode());
            return;
        }

        UsernamePasswordAuthenticationToken authentication =
                new UsernamePasswordAuthenticationToken(userId, null, Collections.emptyList());
        SecurityContextHolder.getContext().setAuthentication(authentication);

        filterChain.doFilter(request, response);
    }

    private String extractBearerToken(HttpServletRequest request) {
        String header = request.getHeader(HttpHeaders.AUTHORIZATION);
        if (header == null || !header.startsWith(BEARER_PREFIX)) {
            return null;
        }
        String token = header.substring(BEARER_PREFIX.length()).trim();
        return token.isEmpty() ? null : token;
    }

    // TODO(S14P31C201-XXX): remove dev bypass when client login lands
    private boolean matchesDevBypass(HttpServletRequest request) {
        LlmProperties.DevBypass bypass = llmProperties.devBypass();
        if (bypass == null || !bypass.enabled()) {
            return false;
        }
        String secret = bypass.secret();
        if (secret == null || secret.isBlank()) {
            return false;
        }
        return secret.equals(request.getHeader(DEV_BYPASS_HEADER));
    }
}
