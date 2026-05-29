package com.lostmemory.server.global.validation;

import jakarta.validation.Validation;
import jakarta.validation.Validator;
import jakarta.validation.ValidatorFactory;
import org.junit.jupiter.api.AfterAll;
import org.junit.jupiter.api.BeforeAll;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

import static org.assertj.core.api.Assertions.assertThat;

/**
 * {@link PasswordPolicy} + {@link PasswordPolicyValidator} 통합 검증.
 * Validator 직접 호출 대신 jakarta Validation 팩토리를 거쳐 어노테이션과 함께 자연스러운 흐름으로 테스트한다.
 */
class PasswordPolicyValidatorTest {

    private static ValidatorFactory factory;
    private static Validator validator;

    @BeforeAll
    static void setUp() {
        factory = Validation.buildDefaultValidatorFactory();
        validator = factory.getValidator();
    }

    @AfterAll
    static void tearDown() {
        factory.close();
    }

    private static class Holder {
        @PasswordPolicy
        String value;
        Holder(String v) { this.value = v; }
    }

    private boolean isValid(String password) {
        return validator.validate(new Holder(password)).isEmpty();
    }

    @Test
    @DisplayName("정상 — 영문·숫자·특수문자 모두 포함 + 8자 이상")
    void valid_allThreeCategories_pass() {
        assertThat(isValid("Pass123!")).isTrue();
        assertThat(isValid("abcdef1@")).isTrue();
        assertThat(isValid("XYZxyz9#")).isTrue();
    }

    @ParameterizedTest
    @ValueSource(strings = {
            "abcdefgh",      // 영문만
            "12345678",      // 숫자만
            "!@#$%^&*",      // 특수만
            "abcd1234",      // 영문+숫자 (특수 누락)
            "abcd!@#$",      // 영문+특수 (숫자 누락)
            "1234!@#$"       // 숫자+특수 (영문 누락)
    })
    @DisplayName("실패 — 세 종류 중 하나라도 결여")
    void invalid_missingCategory_fail(String password) {
        assertThat(isValid(password)).isFalse();
    }

    @Test
    @DisplayName("실패 — 길이 8자 미만")
    void invalid_tooShort_fail() {
        assertThat(isValid("Aa1!")).isFalse();
        assertThat(isValid("Aa1!Bb2")).isFalse();   // 7자
    }

    @Test
    @DisplayName("실패 — 길이 100자 초과")
    void invalid_tooLong_fail() {
        String base = "Aa1!";
        String tooLong = base + "a".repeat(100);   // 104자
        assertThat(isValid(tooLong)).isFalse();
    }

    @Test
    @DisplayName("경계 — 정확히 8자")
    void valid_boundaryMinLength_pass() {
        assertThat(isValid("Aa1!Bb2@")).isTrue();   // 8자, 세 종류
    }

    @Test
    @DisplayName("경계 — 정확히 100자")
    void valid_boundaryMaxLength_pass() {
        String pw = "Aa1!" + "b".repeat(96);   // 100자, 세 종류
        assertThat(pw).hasSize(100);
        assertThat(isValid(pw)).isTrue();
    }

    @Test
    @DisplayName("실패 — null")
    void invalid_null_fail() {
        assertThat(isValid(null)).isFalse();
    }

    @Test
    @DisplayName("실패 — 공백만")
    void invalid_blank_fail() {
        assertThat(isValid("        ")).isFalse();   // 공백은 특수문자 아님
    }
}
