package com.lostmemory.server.auth.service;

import com.lostmemory.server.global.exception.BusinessException;
import com.lostmemory.server.global.exception.ErrorCode;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import static org.assertj.core.api.Assertions.assertThatCode;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

/**
 * {@link PasswordIdentityChecker#assertNotIdentifierLookalike} 의 동일성 검사 분기 커버.
 */
class PasswordIdentityCheckerTest {

    private static final String LOGIN_ID = "testuser";
    private static final String EMAIL    = "tester@example.com";
    private static final String NICKNAME = "테스터1";

    @Test
    @DisplayName("정상 — 비밀번호가 어떤 식별자와도 다르면 통과")
    void notIdentical_pass() {
        assertThatCode(() ->
                PasswordIdentityChecker.assertNotIdentifierLookalike("Pass123!", LOGIN_ID, EMAIL, NICKNAME))
                .doesNotThrowAnyException();
    }

    @Test
    @DisplayName("실패 — 비밀번호가 loginId 와 동일 (대소문자 무관)")
    void identicalToLoginId_throws() {
        assertThatThrownBy(() ->
                PasswordIdentityChecker.assertNotIdentifierLookalike(LOGIN_ID, LOGIN_ID, EMAIL, NICKNAME))
                .isInstanceOf(BusinessException.class)
                .extracting(e -> ((BusinessException) e).errorCode())
                .isEqualTo(ErrorCode.AUTH_PASSWORD_POLICY_VIOLATION);

        assertThatThrownBy(() ->
                PasswordIdentityChecker.assertNotIdentifierLookalike("TESTUSER", LOGIN_ID, EMAIL, NICKNAME))
                .isInstanceOf(BusinessException.class);
    }

    @Test
    @DisplayName("실패 — 비밀번호가 이메일 로컬파트와 동일 (대소문자 무관)")
    void identicalToEmailLocalPart_throws() {
        assertThatThrownBy(() ->
                PasswordIdentityChecker.assertNotIdentifierLookalike("tester", LOGIN_ID, EMAIL, NICKNAME))
                .isInstanceOf(BusinessException.class)
                .extracting(e -> ((BusinessException) e).errorCode())
                .isEqualTo(ErrorCode.AUTH_PASSWORD_POLICY_VIOLATION);
    }

    @Test
    @DisplayName("실패 — 비밀번호가 닉네임과 동일")
    void identicalToNickname_throws() {
        assertThatThrownBy(() ->
                PasswordIdentityChecker.assertNotIdentifierLookalike(NICKNAME, LOGIN_ID, EMAIL, NICKNAME))
                .isInstanceOf(BusinessException.class)
                .extracting(e -> ((BusinessException) e).errorCode())
                .isEqualTo(ErrorCode.AUTH_PASSWORD_POLICY_VIOLATION);
    }

    @Test
    @DisplayName("정상 — 부분 일치(substring) 는 차단하지 않음")
    void substringMatch_pass() {
        assertThatCode(() ->
                PasswordIdentityChecker.assertNotIdentifierLookalike("tester1234!", LOGIN_ID, EMAIL, NICKNAME))
                .doesNotThrowAnyException();
    }

    @Test
    @DisplayName("정상 — 이메일에 @ 없으면 전체를 로컬파트로 간주")
    void emailWithoutAt_usesWholeString() {
        assertThatThrownBy(() ->
                PasswordIdentityChecker.assertNotIdentifierLookalike("rawid", LOGIN_ID, "rawid", NICKNAME))
                .isInstanceOf(BusinessException.class);
    }

    @Test
    @DisplayName("정상 — null 입력은 그 항목 검사만 건너뜀")
    void nullInputs_skipSilently() {
        assertThatCode(() ->
                PasswordIdentityChecker.assertNotIdentifierLookalike(null, LOGIN_ID, EMAIL, NICKNAME))
                .doesNotThrowAnyException();
        assertThatCode(() ->
                PasswordIdentityChecker.assertNotIdentifierLookalike("Pass123!", null, null, null))
                .doesNotThrowAnyException();
    }
}
