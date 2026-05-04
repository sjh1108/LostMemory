package com.lostmemory.server.global.exception;

import com.lostmemory.server.global.response.ApiResponse;
import com.lostmemory.server.global.response.ErrorDetail;
import lombok.extern.slf4j.Slf4j;
import org.springframework.http.ResponseEntity;
import org.springframework.http.converter.HttpMessageNotReadableException;
import org.springframework.security.access.AccessDeniedException;
import org.springframework.web.bind.MethodArgumentNotValidException;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.RestControllerAdvice;
import org.springframework.web.servlet.resource.NoResourceFoundException;

import java.util.stream.Collectors;

@Slf4j
@RestControllerAdvice
public class GlobalExceptionHandler {

    /** 서비스 계층에서 명시적으로 던진 비즈니스 예외를 ErrorCode에 매핑해 응답 */
    @ExceptionHandler(BusinessException.class)
    public ResponseEntity<ApiResponse<Void>> handleBusiness(BusinessException e) {
        ErrorCode code = e.errorCode();
        log.warn("BusinessException: {} - {}", code.name(), e.getMessage());
        return toResponse(code, code.message());
    }

    /** @Valid 바디 검증 실패 시 필드별 메시지를 합쳐 400으로 응답 */
    @ExceptionHandler(MethodArgumentNotValidException.class)
    public ResponseEntity<ApiResponse<Void>> handleValidation(MethodArgumentNotValidException e) {
        String details = e.getBindingResult().getFieldErrors().stream()
                .map(err -> err.getField() + ": " + err.getDefaultMessage())
                .collect(Collectors.joining("; "));
        log.warn("Validation failed: {}", details);
        return toResponse(ErrorCode.COMMON_INVALID_INPUT, details);
    }

    /** 요청 본문 JSON 파싱 실패(깨진 JSON, 타입 불일치 등)를 400으로 응답 */
    @ExceptionHandler(HttpMessageNotReadableException.class)
    public ResponseEntity<ApiResponse<Void>> handleUnreadable(HttpMessageNotReadableException e) {
        log.warn("Unreadable request body: {}", e.getMessage());
        return toResponse(ErrorCode.COMMON_INVALID_INPUT, "요청 본문을 읽을 수 없습니다");
    }

    /** 메서드 보안(@PreAuthorize 등) 권한 부족 시 403 응답 (필터단 AccessDenied는 핸들러가 처리) */
    @ExceptionHandler(AccessDeniedException.class)
    public ResponseEntity<ApiResponse<Void>> handleAccessDenied(AccessDeniedException e) {
        log.warn("Access denied: {}", e.getMessage());
        return toResponse(ErrorCode.COMMON_FORBIDDEN, ErrorCode.COMMON_FORBIDDEN.message());
    }

    /** 매핑된 핸들러/정적 리소스가 없을 때(잘못된 경로 호출 등) 404 응답 */
    @ExceptionHandler(NoResourceFoundException.class)
    public ResponseEntity<ApiResponse<Void>> handleNoResourceFound(NoResourceFoundException e) {
        log.warn("No resource found: {}", e.getMessage());
        return toResponse(ErrorCode.COMMON_RESOURCE_NOT_FOUND, ErrorCode.COMMON_RESOURCE_NOT_FOUND.message());
    }

    /** 처리되지 않은 모든 예외를 500으로 응답 + 스택 트레이스 ERROR 로깅 */
    @ExceptionHandler(Exception.class)
    public ResponseEntity<ApiResponse<Void>> handleUnknown(Exception e) {
        log.error("Unhandled exception", e);
        return toResponse(ErrorCode.COMMON_INTERNAL_ERROR, ErrorCode.COMMON_INTERNAL_ERROR.message());
    }

    /** ErrorCode + 메시지를 ApiResponse.error 로 래핑해 ResponseEntity 로 변환하는 공통 헬퍼 */
    private ResponseEntity<ApiResponse<Void>> toResponse(ErrorCode code, String message) {
        ErrorDetail detail = new ErrorDetail(code.name(), message);
        ApiResponse<Void> body = ApiResponse.error(detail);
        return ResponseEntity.status(code.status()).body(body);
    }
}
