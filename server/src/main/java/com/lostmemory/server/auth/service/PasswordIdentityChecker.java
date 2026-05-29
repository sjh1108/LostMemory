package com.lostmemory.server.auth.service;

import com.lostmemory.server.global.exception.BusinessException;
import com.lostmemory.server.global.exception.ErrorCode;

/**
 * 비밀번호가 본인 식별자(로그인 ID · 이메일 로컬파트 · 닉네임)와 동일한지 검사.
 *
 * 회원가입·비밀번호 재설정 시 서비스 계층에서 호출. cross-field 검사이므로 Bean Validation 으로 처리하지 않는다.
 *
 * 비교는 대소문자 무관(equalsIgnoreCase). 부분 일치(substring)는 검사하지 않는다 —
 * 짧은 식별자가 무관한 비번을 모두 거절시키는 부작용을 피하기 위함.
 */
public final class PasswordIdentityChecker {

    private PasswordIdentityChecker() {
    }

    /**
     * 비밀번호가 loginId · 이메일 로컬파트 · 닉네임 중 어느 하나와 동일하면
     * {@link ErrorCode#AUTH_PASSWORD_POLICY_VIOLATION} 발생.
     * 입력 중 null 인 항목은 그 항목의 검사를 건너뛴다.
     */
    public static void assertNotIdentifierLookalike(String password, String loginId, String email, String nickname) {
        if (password == null) return;
        String emailLocal = extractEmailLocalPart(email);
        if (equalsIgnoreCase(password, loginId)
                || equalsIgnoreCase(password, emailLocal)
                || equalsIgnoreCase(password, nickname)) {
            throw new BusinessException(ErrorCode.AUTH_PASSWORD_POLICY_VIOLATION);
        }
    }

    private static String extractEmailLocalPart(String email) {
        if (email == null) return null;
        int at = email.indexOf('@');
        return at < 0 ? email : email.substring(0, at);
    }

    private static boolean equalsIgnoreCase(String a, String b) {
        return a != null && b != null && a.equalsIgnoreCase(b);
    }
}
