package com.lostmemory.website.global.config;

import lombok.RequiredArgsConstructor;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.security.config.Customizer;
import org.springframework.security.config.annotation.web.builders.HttpSecurity;
import org.springframework.security.crypto.bcrypt.BCryptPasswordEncoder;
import org.springframework.security.crypto.password.PasswordEncoder;
import org.springframework.security.web.SecurityFilterChain;

/**
 * Session-based form login (admin 만).
 * - 공개 페이지: /, /notices, /patch-notes, /faq, /admin/login + 정적 자산 + actuator/health
 * - 인증 필요: /admin/**
 * - CSRF 활성화 (form login). Thymeleaf 의 th:action 사용 시 자동 hidden field 삽입.
 */
@Configuration
@RequiredArgsConstructor
public class SecurityConfig {

    @Bean
    public SecurityFilterChain securityFilterChain(HttpSecurity http) throws Exception {
        http
            .csrf(Customizer.withDefaults())
            .authorizeHttpRequests(auth -> auth
                .requestMatchers(
                    "/", "/notices", "/notices/**",
                    "/patch-notes", "/patch-notes/**",
                    "/faq", "/faq/**",
                    "/css/**", "/js/**", "/images/**", "/favicon.ico",
                    "/admin/login",
                    "/actuator/health"
                ).permitAll()
                .requestMatchers("/admin/**").authenticated()
                .anyRequest().denyAll())
            .formLogin(form -> form
                .loginPage("/admin/login")
                .loginProcessingUrl("/admin/login")
                .usernameParameter("username")
                .passwordParameter("password")
                .defaultSuccessUrl("/admin/dashboard", true)
                .failureUrl("/admin/login?error")
                .permitAll())
            .logout(logout -> logout
                .logoutUrl("/admin/logout")
                .logoutSuccessUrl("/admin/login?logout")
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
