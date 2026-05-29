package com.lostmemory.server.global.validation;

import jakarta.validation.Constraint;
import jakarta.validation.Payload;

import java.lang.annotation.Documented;
import java.lang.annotation.ElementType;
import java.lang.annotation.Retention;
import java.lang.annotation.RetentionPolicy;
import java.lang.annotation.Target;

/**
 * 비밀번호 정책 Bean Validation 어노테이션.
 *
 * 검증 룰:
 *  - 길이: {@link #min()} ~ {@link #max()} (기본 8~100)
 *  - 종류: 영문(대소 무관) + 숫자 + 특수문자 — 세 종류 모두 포함
 *
 * 위반 시 ConstraintViolationException → 400 응답 (Spring 기본 처리).
 * 닉네임·이메일 로컬파트와의 동일 여부는 다른 필드 참조가 필요해서 본 단계에서 잡지 않고,
 * 서비스 계층(PasswordIdentityChecker)에서 별도 검사한다.
 */
@Documented
@Constraint(validatedBy = PasswordPolicyValidator.class)
@Target({ElementType.FIELD, ElementType.PARAMETER})
@Retention(RetentionPolicy.RUNTIME)
public @interface PasswordPolicy {

    String message() default "비밀번호는 영문·숫자·특수문자를 모두 포함한 8~100자여야 합니다";

    Class<?>[] groups() default {};

    Class<? extends Payload>[] payload() default {};

    int min() default 8;

    int max() default 100;
}
