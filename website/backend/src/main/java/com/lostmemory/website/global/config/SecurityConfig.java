package com.lostmemory.website.global.config;

import lombok.RequiredArgsConstructor;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.security.config.Customizer;
import org.springframework.security.config.annotation.web.builders.HttpSecurity;
import org.springframework.security.crypto.bcrypt.BCryptPasswordEncoder;
import org.springframework.security.crypto.password.PasswordEncoder;
import org.springframework.security.web.SecurityFilterChain;

/**
 * Session-based form login (admin 만).
 * - 공개 페이지: /, /notices, /patch-notes, /faq, /feedback + 정적 자산 + actuator/health + adminBase + "/login"
 * - 인증 필요: adminBase + "/**"
 * - admin URL base path 는 .env 의 ADMIN_BASE_PATH 환경변수 동적 결정 (S14P31C201-621).
 * - CSRF 활성화 (form login). Thymeleaf 의 th:action 사용 시 자동 hidden field 삽입.
 */
@Configuration
@RequiredArgsConstructor
public class SecurityConfig {

    @Value("${app.admin.base-path}")
    private String adminBase;

    @Bean
    public SecurityFilterChain securityFilterChain(HttpSecurity http) throws Exception {
        final String loginPath = adminBase + "/login";
        final String dashboardPath = adminBase + "/dashboard";
        final String logoutPath = adminBase + "/logout";

        http
            .csrf(Customizer.withDefaults())
            .authorizeHttpRequests(auth -> auth
                .requestMatchers(
                    "/", "/notices", "/notices/**",
                    "/patch-notes", "/patch-notes/**",
                    "/faq", "/faq/**",
                    "/feedback", "/feedback/**",
                    "/downloads", "/downloads/**",
                    "/css/**", "/js/**", "/images/**", "/favicon.ico",
                    loginPath,
                    "/actuator/health"
                ).permitAll()
                .requestMatchers(adminBase + "/**").authenticated()
                .anyRequest().denyAll())
            .formLogin(form -> form
                .loginPage(loginPath)
                .loginProcessingUrl(loginPath)
                .usernameParameter("username")
                .passwordParameter("password")
                .defaultSuccessUrl(dashboardPath, true)
                .failureUrl(loginPath + "?error")
                .permitAll())
            .logout(logout -> logout
                .logoutUrl(logoutPath)
                .logoutSuccessUrl(loginPath + "?logout")
                .invalidateHttpSession(true)
                .deleteCookies("JSESSIONID"))
            .sessionManagement(sm -> sm
                .sessionFixation().migrateSession()
                .maximumSessions(1).maxSessionsPreventsLogin(false));
        return http.build();
    }

    @Bean
    public PasswordEncoder passwordEncoder() {
        return new BCryptPasswordEncoder();
    }
}
