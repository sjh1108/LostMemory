package com.lostmemory.server.auth.controller;

import com.lostmemory.server.auth.dto.PasswordResetConfirmRequest;
import com.lostmemory.server.auth.dto.PasswordResetRequest;
import com.lostmemory.server.auth.service.AuthService;
import com.lostmemory.server.global.response.ApiResponse;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.security.SecurityRequirements;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

/**
 * 비밀번호 재설정 흐름 — 코드 발송 요청 / 코드 + 새 비번 확정 두 단계.
 */
@Tag(name = "Auth (Password Reset)", description = "비밀번호 재설정 코드 발송 / 코드 확정")
@RestController
@RequestMapping("/auth/password-reset")
@RequiredArgsConstructor
public class PasswordResetController {

    private final AuthService authService;

    @Operation(
            summary = "비밀번호 재설정 코드 발송",
            description = """
                    입력한 이메일로 6자리 재설정 코드를 발송한다. 코드는 10분간 유효.

                    계정 열거 방지 정책 — **결과는 계정 존재 여부와 무관하게 항상 200**.
                    실제 메일은 ACTIVE 상태의 계정에 한해서만 발송된다(존재하지 않는 이메일 / PENDING /
                    SUSPENDED / DELETED 인 경우 응답만 200, 메일 발송은 일어나지 않음).

                    실패 케이스:
                    - 429 AUTH_VERIFICATION_SEND_TOO_FREQUENT — 60초 쿨다운 미경과
                    - 429 AUTH_VERIFICATION_SEND_DAILY_LIMIT — 일일 발송 한도 초과
                    """
    )
    @SecurityRequirements // 인증 불요
    @PostMapping("/request")
    public ApiResponse<Void> request(@Valid @RequestBody PasswordResetRequest request) {
        authService.requestPasswordReset(request);
        return ApiResponse.ok();
    }

    @Operation(
            summary = "비밀번호 재설정 확정",
            description = """
                    재설정 코드와 새 비밀번호를 받아 비밀번호를 갱신한다. 성공 시 해당 계정의 모든
                    refresh 토큰을 무효화 — 다른 디바이스의 세션이 즉시 종료된다.

                    새 비밀번호는 정책(영문·숫자·특수문자 + 8~100자)을 만족해야 하며,
                    이메일 로컬파트·닉네임과 동일하면 거부된다.

                    실패 케이스:
                    - 400 COMMON_INVALID_INPUT — 형식 위반(이메일·코드 패턴·비밀번호 정책)
                    - 400 AUTH_PASSWORD_POLICY_VIOLATION — 새 비번이 이메일 로컬파트·닉네임과 동일
                    - 401 AUTH_PASSWORD_RESET_CODE_INVALID — 코드 불일치
                    - 410 AUTH_PASSWORD_RESET_CODE_EXPIRED — 코드 없음/만료 / 비활성 계정
                    - 429 AUTH_VERIFICATION_ATTEMPTS_EXCEEDED — 시도 횟수 초과 (코드 무효화됨)
                    """
    )
    @SecurityRequirements // 인증 불요
    @PostMapping("/confirm")
    public ApiResponse<Void> confirm(@Valid @RequestBody PasswordResetConfirmRequest request) {
        authService.confirmPasswordReset(request);
        return ApiResponse.ok();
    }
}
