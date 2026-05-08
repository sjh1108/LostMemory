package com.lostmemory.server.global.exception;

import org.springframework.http.HttpStatus;

public enum ErrorCode {

    COMMON_INTERNAL_ERROR(HttpStatus.INTERNAL_SERVER_ERROR, "서버 내부 오류가 발생했습니다"),
    COMMON_INVALID_INPUT(HttpStatus.BAD_REQUEST, "요청 값이 올바르지 않습니다"),
    COMMON_RESOURCE_NOT_FOUND(HttpStatus.NOT_FOUND, "요청한 리소스를 찾을 수 없습니다"),
    COMMON_UNAUTHORIZED(HttpStatus.UNAUTHORIZED, "인증이 필요합니다"),
    COMMON_FORBIDDEN(HttpStatus.FORBIDDEN, "접근 권한이 없습니다"),

    AUTH_INVALID_CREDENTIALS(HttpStatus.UNAUTHORIZED, "아이디 또는 비밀번호가 올바르지 않습니다"),
    AUTH_TOKEN_INVALID(HttpStatus.UNAUTHORIZED, "유효하지 않은 토큰입니다"),
    AUTH_TOKEN_EXPIRED(HttpStatus.UNAUTHORIZED, "토큰이 만료되었습니다"),
    AUTH_REFRESH_NOT_FOUND(HttpStatus.UNAUTHORIZED, "리프레시 토큰을 찾을 수 없습니다"),
    AUTH_REFRESH_REUSED(HttpStatus.UNAUTHORIZED, "이미 사용되었거나 무효화된 리프레시 토큰입니다"),

    USER_LOGIN_ID_DUPLICATED(HttpStatus.CONFLICT, "이미 사용 중인 아이디입니다"),
    USER_NICKNAME_DUPLICATED(HttpStatus.CONFLICT, "이미 사용 중인 닉네임입니다"),
    USER_NOT_FOUND(HttpStatus.NOT_FOUND, "사용자를 찾을 수 없습니다"),

    SESSION_NOT_FOUND(HttpStatus.NOT_FOUND, "세션을 찾을 수 없습니다"),
    SESSION_FULL(HttpStatus.CONFLICT, "정원이 가득 찼습니다"),
    SESSION_ALREADY_JOINED(HttpStatus.CONFLICT, "이미 참가한 세션입니다"),
    SESSION_NOT_HOST(HttpStatus.FORBIDDEN, "호스트만 수행할 수 있습니다"),
    SESSION_HOST_CANNOT_LEAVE(HttpStatus.CONFLICT, "호스트는 세션 종료를 사용해야 합니다"),
    SESSION_INVALID_PRIVATE_CODE(HttpStatus.BAD_REQUEST, "비공개 코드가 일치하지 않습니다"),
    SESSION_PRIVATE_CODE_DUPLICATED(HttpStatus.CONFLICT, "이미 사용 중인 입장 코드입니다");

    private final HttpStatus status;
    private final String message;

    ErrorCode(HttpStatus status, String message) {
        this.status = status;
        this.message = message;
    }

    /** 해당 에러에 매핑된 HTTP 상태 코드 */
    public HttpStatus status() {
        return status;
    }

    /** 클라이언트에 노출할 기본 에러 메시지 */
    public String message() {
        return message;
    }
}
