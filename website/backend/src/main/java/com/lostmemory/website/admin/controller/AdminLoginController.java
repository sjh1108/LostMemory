package com.lostmemory.website.admin.controller;

import org.springframework.stereotype.Controller;
import org.springframework.web.bind.annotation.GetMapping;

@Controller
public class AdminLoginController {

    /**
     * SecurityConfig 의 .formLogin().loginPage(${app.admin.base-path}/login) 가 가리키는 GET 페이지.
     * 같은 URL 의 POST 는 Spring Security 의 UsernamePasswordAuthenticationFilter 가 가로채 처리.
     * URL 은 .env 의 ADMIN_BASE_PATH 환경변수 동적 결정 (S14P31C201-621).
     */
    @GetMapping("${app.admin.base-path}/login")
    public String loginPage() {
        return "admin/login";
    }
}
