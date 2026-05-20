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
    USER_ALREADY_IN_SESSION(HttpStatus.CONFLICT, "이미 다른 세션에 참여 중입니다"),

    SESSION_NOT_FOUND(HttpStatus.NOT_FOUND, "세션을 찾을 수 없습니다"),
    SESSION_FULL(HttpStatus.CONFLICT, "정원이 가득 찼습니다"),
    SESSION_ALREADY_JOINED(HttpStatus.CONFLICT, "이미 참가한 세션입니다"),
    SESSION_NOT_HOST(HttpStatus.FORBIDDEN, "호스트만 수행할 수 있습니다"),
    SESSION_HOST_CANNOT_LEAVE(HttpStatus.CONFLICT, "호스트는 세션 종료를 사용해야 합니다"),
    SESSION_INVALID_PRIVATE_CODE(HttpStatus.BAD_REQUEST, "비공개 코드가 일치하지 않습니다"),
    SESSION_PRIVATE_CODE_DUPLICATED(HttpStatus.CONFLICT, "이미 사용 중인 입장 코드입니다"),

    RUN_NOT_FOUND(HttpStatus.NOT_FOUND, "런을 찾을 수 없습니다"),
    RUN_NOT_HOST(HttpStatus.FORBIDDEN, "호스트만 런을 시작/종료할 수 있습니다"),
    RUN_ALREADY_ENDED(HttpStatus.CONFLICT, "이미 종료된 런입니다"),
    RUN_ALREADY_IN_PROGRESS(HttpStatus.CONFLICT, "해당 세션에 이미 진행 중인 런이 있습니다"),
    RUN_NOT_MEMBER(HttpStatus.FORBIDDEN, "본인이 참여한 런만 조회할 수 있습니다"),

    MEMORY_FRAME_NOT_FOUND(HttpStatus.NOT_FOUND, "기억 액자를 찾을 수 없습니다"),
    MEMORY_SLOT_INDEX_OUT_OF_RANGE(HttpStatus.BAD_REQUEST, "슬롯 인덱스는 0~5 사이여야 합니다"),
    MEMORY_SHARDS_INSUFFICIENT(HttpStatus.CONFLICT, "보유한 기억의 파편이 부족합니다"),

    WEAPON_NOT_FOUND(HttpStatus.NOT_FOUND, "무기를 찾을 수 없습니다"),
    WEAPON_PARENT_NOT_UNLOCKED(HttpStatus.CONFLICT, "상위 무기를 먼저 해금해야 합니다"),
    WEAPON_SHARDS_INSUFFICIENT(HttpStatus.CONFLICT, "무기 해금에 필요한 파편이 부족합니다"),
    WEAPON_NOT_UNLOCKED(HttpStatus.CONFLICT, "해금하지 않은 무기는 장착할 수 없습니다");

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
