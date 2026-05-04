package com.lostmemory.server.global.security;

import com.lostmemory.server.global.exception.BusinessException;
import jakarta.servlet.FilterChain;
import jakarta.servlet.ServletException;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.http.HttpHeaders;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.stereotype.Component;
import org.springframework.web.filter.OncePerRequestFilter;

import java.io.IOException;
import java.util.Collections;

@Slf4j
@Component
@RequiredArgsConstructor
public class JwtAuthenticationFilter extends OncePerRequestFilter {

    private static final String BEARER_PREFIX = "Bearer ";

    private final JwtProvider jwtProvider;
    private final ErrorResponseWriter errorResponseWriter;

    /**
     * Authorization: Bearer 헤더가 있으면 access 토큰으로 파싱·검증해 SecurityContext 에 인증 주입.
     * 헤더 자체가 없으면 그냥 통과(SecurityFilterChain 의 authorizeHttpRequests 가 401 로 떨굼).
     * 헤더는 있으나 토큰이 무효/만료/잘못된 type 이면 ErrorResponseWriter 로 401 직접 응답하고 체인 중단.
     */
    @Override
    protected void doFilterInternal(HttpServletRequest request,
                                    HttpServletResponse response,
                                    FilterChain filterChain) throws ServletException, IOException {
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
}
