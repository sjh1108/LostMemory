package com.lostmemory.server.global.exception;

public class BusinessException extends RuntimeException {

    private final ErrorCode errorCode;

    /** ErrorCode 기반 비즈니스 예외 생성 */
    public BusinessException(ErrorCode errorCode) {
        super(errorCode.message());
        this.errorCode = errorCode;
    }

    /** ErrorCode + 원인 예외(cause)를 포함한 비즈니스 예외 생성 */
    public BusinessException(ErrorCode errorCode, Throwable cause) {
        super(errorCode.message(), cause);
        this.errorCode = errorCode;
    }

    /** 이 예외가 지시하는 ErrorCode 반환 (예외 핸들러가 상태/메시지 매핑에 사용) */
    public ErrorCode errorCode() {
        return errorCode;
    }
}
