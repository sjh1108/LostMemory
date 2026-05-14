package com.lostmemory.server.auth.controller;

import com.lostmemory.server.auth.dto.LoginRequest;
import com.lostmemory.server.auth.dto.LogoutRequest;
import com.lostmemory.server.auth.dto.RefreshRequest;
import com.lostmemory.server.auth.dto.SignupRequest;
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

@Tag(name = "Auth", description = "회원가입 / 로그인 / 토큰 갱신 / 로그아웃")
@RestController
@RequestMapping("/auth")
@RequiredArgsConstructor
public class AuthController {

    private final AuthService authService;

    @Operation(
            summary = "회원가입",
            description = """
                    신규 유저 등록. 성공 시 데이터 없이 200 반환 — 별도 로그인 호출 필요.

                    - loginId: 영숫자 4~30자 (UNIQUE)
                    - password: 8~100자 평문 전송 → 서버에서 BCrypt 해시 저장
                    - nickname: 2~30자 (UNIQUE)

                    실패 케이스:
                    - 400 COMMON_INVALID_INPUT — 길이/형식 위반
                    - 409 USER_LOGIN_ID_DUPLICATED — loginId 중복
                    - 409 USER_NICKNAME_DUPLICATED — nickname 중복
                    """
    )
    @ApiResponses({
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "200", description = "회원가입 성공",
                    content = @Content(examples = @ExampleObject(value = """
                            { "success": true }
                            """))),
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "409", description = "loginId 또는 nickname 중복",
                    content = @Content(examples = @ExampleObject(value = """
                            {
                              "success": false,
                              "error": { "code": "USER_LOGIN_ID_DUPLICATED", "message": "이미 사용 중인 아이디입니다" }
                            }
                            """)))
    })
    @SecurityRequirements // 인증 불요
    @PostMapping("/signup")
    public ApiResponse<Void> signup(@Valid @RequestBody SignupRequest request) {
        authService.signup(request);
        return ApiResponse.ok();
    }

    @Operation(
            summary = "로그인",
            description = """
                    loginId + password 인증. 성공 시 accessToken + refreshToken 발급.

                    - accessToken: 후속 API 호출 시 `Authorization: Bearer <token>` 헤더에 부착
                    - refreshToken: accessToken 만료 시 /auth/refresh 로 갱신 (rotation)
                    - accessTokenExpiresIn: 초 단위 만료 시간 (default 1800 = 30분)

                    실패 케이스:
                    - 401 AUTH_INVALID_CREDENTIALS — ID/비밀번호 불일치
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
                    responseCode = "401", description = "ID 또는 비밀번호 불일치",
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
    @ApiResponses({
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "200", description = "토큰 갱신 성공",
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
                    responseCode = "401", description = "refresh 토큰 무효/만료/재사용",
                    content = @Content(examples = @ExampleObject(value = """
                            {
                              "success": false,
                              "error": { "code": "AUTH_REFRESH_REUSED", "message": "이미 사용되었거나 무효화된 리프레시 토큰입니다" }
                            }
                            """)))
    })
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
    @ApiResponses({
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "200", description = "로그아웃 성공",
                    content = @Content(examples = @ExampleObject(value = """
                            { "success": true }
                            """)))
    })
    @SecurityRequirements // 인증 불요 (refresh 토큰 자체로 식별)
    @PostMapping("/logout")
    public ApiResponse<Void> logout(@Valid @RequestBody LogoutRequest request) {
        authService.logout(request);
        return ApiResponse.ok();
    }
}
