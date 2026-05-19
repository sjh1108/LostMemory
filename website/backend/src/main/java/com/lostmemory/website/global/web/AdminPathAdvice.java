package com.lostmemory.website.global.web;

import org.springframework.beans.factory.annotation.Value;
import org.springframework.web.bind.annotation.ControllerAdvice;
import org.springframework.web.bind.annotation.ModelAttribute;

/**
 * Thymeleaf template 의 admin URL 을 동적으로 만들기 위해 `${adminBase}` model attribute 전역 주입.
 * 사용: `<a th:href="@{|${adminBase}/notices|}">`
 *
 * 실제 path 는 .env 의 ADMIN_BASE_PATH 환경변수가 결정 (.env.example placeholder 그대로 사용 X).
 */
@ControllerAdvice
public class AdminPathAdvice {

    @Value("${app.admin.base-path}")
    private String adminBase;

    @ModelAttribute("adminBase")
    public String adminBase() {
        return adminBase;
    }
}
