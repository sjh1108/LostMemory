package com.lostmemory.server.auth.controller;

import com.lostmemory.server.auth.dto.EmailResendRequest;
import com.lostmemory.server.auth.dto.EmailVerifyRequest;
import com.lostmemory.server.auth.dto.LoginRequest;
import com.lostmemory.server.auth.dto.LogoutRequest;
import com.lostmemory.server.auth.dto.RefreshRequest;
import com.lostmemory.server.auth.dto.SignupRequest;
import com.lostmemory.server.auth.dto.SignupResponse;
import com.lostmemory.server.auth.dto.TokenResponse;
import com.lostmemory.server.auth.service.AuthService;
import com.lostmemory.server.global.response.ApiResponse;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.media.Content;
import io.swagger.v3.oas.annotations.media.ExampleObject;
import io.swagger.v3.oas.annotations.responses.ApiResponses;
import io.swagger.v3.oas.annotations.security.SecurityRequirements;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@Tag(name = "Auth", description = "회원가입 / 이메일 인증 / 로그인 / 토큰 갱신 / 로그아웃")
@RestController
@RequestMapping("/auth")
@RequiredArgsConstructor
public class AuthController {

    private final AuthService authService;

    @Operation(
            summary = "회원가입",
            description = """
                    신규 유저 등록. 가입 직후 status=pending 으로 저장되며 토큰은 발급하지 않는다.
                    가입 직후 자동으로 인증 코드 메일이 발송되며, /auth/email/verify 로 검증을 마쳐야 로그인 가능.

                    - loginId: 4~30자 로그인 식별자 (UNIQUE)
                    - password: 8~100자 + 영문·숫자·특수문자 모두 포함 → 서버에서 BCrypt 해시 저장
                    - email: 인증·비밀번호 재설정용 메일 주소 (UNIQUE)
                    - nickname: 2~30자 (UNIQUE)

                    실패 케이스:
                    - 400 COMMON_INVALID_INPUT — 길이/형식 위반
                    - 400 AUTH_PASSWORD_POLICY_VIOLATION — 비번이 loginId·이메일 로컬파트·닉네임과 동일
                    - 409 USER_LOGIN_ID_DUPLICATED — loginId 중복
                    - 409 USER_EMAIL_DUPLICATED — email 중복
                    - 409 USER_NICKNAME_DUPLICATED — nickname 중복
                    - 502 AUTH_MAIL_DELIVERY_FAILED — 인증 메일 발송 실패 (SMTP 장애)
                    """
    )
    @ApiResponses({
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "200", description = "회원가입 성공 — 인증 메일 발송됨",
                    content = @Content(examples = @ExampleObject(value = """
                            {
                              "success": true,
                              "data": { "userId": 42, "status": "pending" }
                            }
                            """))),
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "409", description = "loginId / email / nickname 중복",
                    content = @Content(examples = @ExampleObject(value = """
                            {
                              "success": false,
                              "error": { "code": "USER_LOGIN_ID_DUPLICATED", "message": "이미 사용 중인 아이디입니다" }
                            }
                            """)))
    })
    @SecurityRequirements // 인증 불요
    @PostMapping("/signup")
    public ApiResponse<SignupResponse> signup(@Valid @RequestBody SignupRequest request) {
        return ApiResponse.of(authService.signup(request));
    }

    @Operation(
            summary = "이메일 인증 코드 검증",
            description = """
                    가입 시 발송된 6자리 인증 코드를 검증. 성공 시 status 가 pending → active 로 전이되며,
                    accessToken + refreshToken 이 즉시 발급된다.

                    실패 케이스:
                    - 401 AUTH_VERIFICATION_CODE_INVALID — 코드 불일치
                    - 410 AUTH_VERIFICATION_CODE_EXPIRED — 코드 없음/만료
                    - 409 AUTH_EMAIL_ALREADY_VERIFIED — 이미 인증된 계정
                    - 429 AUTH_VERIFICATION_ATTEMPTS_EXCEEDED — 시도 횟수 초과 (코드 무효화됨, 재발급 필요)
                    """
    )
    @ApiResponses({
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "200", description = "인증 성공 — 토큰 발급",
                    content = @Content(examples = @ExampleObject(value = """
                            {
                              "success": true,
                              "data": {
                                "accessToken": "eyJhbGciOiJIUzI1NiJ9...",
                                "refreshToken": "eyJhbGciOiJIUzI1NiJ9...",
                                "accessTokenExpiresIn": 1800
                              }
                            }
                            """)))
    })
    @SecurityRequirements // 인증 불요
    @PostMapping("/email/verify")
    public ApiResponse<TokenResponse> verifyEmail(@Valid @RequestBody EmailVerifyRequest request) {
        return ApiResponse.of(authService.verifyEmail(request));
    }

    @Operation(
            summary = "이메일 인증 코드 재발송",
            description = """
                    가입 진행 중(Redis staging 살아있음)인 이메일에 한해 인증 코드를 새로 발송.
                    staging TTL 30분 이내에 호출돼야 하며, 만료 후에는 회원가입을 다시 진행해야 한다.

                    실패 케이스:
                    - 410 AUTH_VERIFICATION_CODE_EXPIRED — staging 만료/없음 (가입 다시 진행 필요)
                    - 429 AUTH_VERIFICATION_SEND_TOO_FREQUENT — 60초 쿨다운 미경과
                    - 429 AUTH_VERIFICATION_SEND_DAILY_LIMIT — 일일 5회 한도 초과
                    - 502 AUTH_MAIL_DELIVERY_FAILED — SMTP 발송 실패
                    """
    )
    @SecurityRequirements // 인증 불요
    @PostMapping("/email/resend")
    public ApiResponse<Void> resendVerificationCode(@Valid @RequestBody EmailResendRequest request) {
        authService.resendVerificationCode(request);
        return ApiResponse.ok();
    }

    @Operation(
            summary = "로그인",
            description = """
                    loginId + password 인증. 성공 시 accessToken + refreshToken 발급.

                    실패 케이스:
                    - 401 AUTH_INVALID_CREDENTIALS — loginId/비밀번호 불일치
                    - 403 AUTH_EMAIL_NOT_VERIFIED — 이메일 인증 미완료 (PENDING 상태)
                    """
    )
    @ApiResponses({
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "200", description = "로그인 성공",
                    content = @Content(examples = @ExampleObject(value = """
                            {
                              "success": true,
                              "data": {
                                "accessToken": "eyJhbGciOiJIUzI1NiJ9...",
                                "refreshToken": "eyJhbGciOiJIUzI1NiJ9...",
                                "accessTokenExpiresIn": 1800
                              }
                            }
                            """))),
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "401", description = "loginId 또는 비밀번호 불일치",
                    content = @Content(examples = @ExampleObject(value = """
                            {
                              "success": false,
                              "error": { "code": "AUTH_INVALID_CREDENTIALS", "message": "아이디 또는 비밀번호가 올바르지 않습니다" }
                            }
                            """)))
    })
    @SecurityRequirements // 인증 불요
    @PostMapping("/login")
    public ApiResponse<TokenResponse> login(@Valid @RequestBody LoginRequest request) {
        return ApiResponse.of(authService.login(request));
    }

    @Operation(
            summary = "토큰 갱신 (refresh rotation)",
            description = """
                    accessToken 만료 시 refreshToken 으로 새 토큰 페어 발급.

                    - rotation 정책: 호출 즉시 **기존 refreshToken 무효화** + 새 refreshToken 발급. 이전 토큰 재사용 시 401 발생
                    - accessToken / refreshToken 둘 다 새 값으로 응답

                    실패 케이스:
                    - 401 AUTH_TOKEN_INVALID — 토큰 형식 깨짐
                    - 401 AUTH_TOKEN_EXPIRED — refresh 자체도 만료
                    - 401 AUTH_REFRESH_NOT_FOUND — 서버 측에 해당 토큰 기록 없음
                    - 401 AUTH_REFRESH_REUSED — 이미 사용/무효화된 토큰 재사용
                    """
    )
    @SecurityRequirements // 인증 불요 (refresh 토큰 자체가 인증 수단)
    @PostMapping("/refresh")
    public ApiResponse<TokenResponse> refresh(@Valid @RequestBody RefreshRequest request) {
        return ApiResponse.of(authService.refresh(request));
    }

    @Operation(
            summary = "로그아웃",
            description = """
                    refreshToken 을 즉시 revoke. 이후 해당 토큰으로 /auth/refresh 호출 시 401.

                    accessToken 은 만료 시점까지는 그대로 유효 (서버 측 무효화 X) — 클라가 로컬에서 폐기.
                    """
    )
    @SecurityRequirements // 인증 불요 (refresh 토큰 자체로 식별)
    @PostMapping("/logout")
    public ApiResponse<Void> logout(@Valid @RequestBody LogoutRequest request) {
        authService.logout(request);
        return ApiResponse.ok();
    }
}
