package com.lostmemory.server.auth.service;

import com.lostmemory.server.auth.dto.EmailResendRequest;
import com.lostmemory.server.auth.dto.EmailVerifyRequest;
import com.lostmemory.server.auth.dto.LoginRequest;
import com.lostmemory.server.auth.dto.PasswordResetConfirmRequest;
import com.lostmemory.server.auth.dto.PasswordResetRequest;
import com.lostmemory.server.auth.dto.SignupRequest;
import com.lostmemory.server.auth.dto.SignupResponse;
import com.lostmemory.server.auth.dto.TokenResponse;
import com.lostmemory.server.auth.repository.AuthRefreshTokenRepository;
import com.lostmemory.server.global.exception.BusinessException;
import com.lostmemory.server.global.exception.ErrorCode;
import com.lostmemory.server.global.security.JwtProperties;
import com.lostmemory.server.global.security.JwtProvider;
import com.lostmemory.server.global.security.TokenIssueResult;
import com.lostmemory.server.memory.repository.MemoryFrameRepository;
import com.lostmemory.server.memory.repository.UserMemoryProgressRepository;
import com.lostmemory.server.user.entity.User;
import com.lostmemory.server.user.entity.UserStatus;
import com.lostmemory.server.user.repository.UserCurrencyRepository;
import com.lostmemory.server.user.repository.UserRecordRepository;
import com.lostmemory.server.user.repository.UserRepository;
import com.lostmemory.server.user.repository.UserTalentAllocationRepository;
import com.lostmemory.server.weapon.repository.UserWeaponSelectionRepository;
import com.lostmemory.server.weapon.repository.UserWeaponUnlockRepository;
import com.lostmemory.server.weapon.repository.WeaponRepository;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.security.crypto.password.PasswordEncoder;

import java.util.List;
import java.util.Optional;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.doThrow;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

/**
 * AuthService 분기 — signup(staging) / verifyEmail(확정) / login / resend / password reset.
 *
 * 흐름 핵심:
 *   - signup 은 DB 에 user 를 만들지 않고 Redis 에 staging 만 한다
 *   - verifyEmail 시점에 비로소 ACTIVE User row 가 INSERT 된다
 */
@ExtendWith(MockitoExtension.class)
class AuthServiceTest {

    @Mock private UserRepository userRepository;
    @Mock private AuthRefreshTokenRepository refreshTokenRepository;
    @Mock private PasswordEncoder passwordEncoder;
    @Mock private JwtProvider jwtProvider;
    @Mock private JwtProperties jwtProperties;
    @Mock private UserCurrencyRepository userCurrencyRepository;
    @Mock private UserRecordRepository userRecordRepository;
    @Mock private MemoryFrameRepository memoryFrameRepository;
    @Mock private UserMemoryProgressRepository userMemoryProgressRepository;
    @Mock private WeaponRepository weaponRepository;
    @Mock private UserWeaponUnlockRepository userWeaponUnlockRepository;
    @Mock private UserTalentAllocationRepository userTalentAllocationRepository;
    @Mock private UserWeaponSelectionRepository userWeaponSelectionRepository;
    @Mock private EmailVerificationService emailVerificationService;
    @Mock private PasswordResetService passwordResetService;

    @InjectMocks private AuthService authService;

    private static final String LOGIN_ID = "testuser";
    private static final String EMAIL    = "tester@example.com";
    private static final String PASSWORD = "Pass123!";
    private static final String NICK     = "테스터1";
    private static final String HASH     = "bcrypt-hash";

    private SignupRequest signupReq() {
        return new SignupRequest(LOGIN_ID, PASSWORD, EMAIL, NICK);
    }

    private StagedSignup staged() {
        return new StagedSignup(LOGIN_ID, HASH, NICK);
    }

    // ── signup (staging) ─────────────────────────────────────

    @Test
    @DisplayName("signup — loginId 중복: USER_LOGIN_ID_DUPLICATED + staging 호출 안 함")
    void signup_loginIdDuplicated_throws() {
        when(userRepository.existsByLoginId(LOGIN_ID)).thenReturn(true);

        assertThatThrownBy(() -> authService.signup(signupReq()))
                .isInstanceOf(BusinessException.class)
                .extracting(e -> ((BusinessException) e).errorCode())
                .isEqualTo(ErrorCode.USER_LOGIN_ID_DUPLICATED);
        verify(emailVerificationService, never()).sendCodeForSignup(anyString(), any());
    }

    @Test
    @DisplayName("signup — email 중복: USER_EMAIL_DUPLICATED")
    void signup_emailDuplicated_throws() {
        when(userRepository.existsByLoginId(LOGIN_ID)).thenReturn(false);
        when(userRepository.existsByEmail(EMAIL)).thenReturn(true);

        assertThatThrownBy(() -> authService.signup(signupReq()))
                .isInstanceOf(BusinessException.class)
                .extracting(e -> ((BusinessException) e).errorCode())
                .isEqualTo(ErrorCode.USER_EMAIL_DUPLICATED);
    }

    @Test
    @DisplayName("signup — 닉네임 중복: USER_NICKNAME_DUPLICATED")
    void signup_nicknameDuplicated_throws() {
        when(userRepository.existsByLoginId(LOGIN_ID)).thenReturn(false);
        when(userRepository.existsByEmail(EMAIL)).thenReturn(false);
        when(userRepository.existsByNickname(NICK)).thenReturn(true);

        assertThatThrownBy(() -> authService.signup(signupReq()))
                .isInstanceOf(BusinessException.class)
                .extracting(e -> ((BusinessException) e).errorCode())
                .isEqualTo(ErrorCode.USER_NICKNAME_DUPLICATED);
    }

    @Test
    @DisplayName("signup — 정상: DB INSERT 없이 staging + 코드 발송 + status=pending 응답")
    void signup_success_stagesAndSendsCode_noDbInsert() {
        when(userRepository.existsByLoginId(LOGIN_ID)).thenReturn(false);
        when(userRepository.existsByEmail(EMAIL)).thenReturn(false);
        when(userRepository.existsByNickname(NICK)).thenReturn(false);
        when(passwordEncoder.encode(PASSWORD)).thenReturn(HASH);

        SignupResponse resp = authService.signup(signupReq());

        ArgumentCaptor<StagedSignup> stagedCaptor = ArgumentCaptor.forClass(StagedSignup.class);
        verify(emailVerificationService).sendCodeForSignup(eq(EMAIL), stagedCaptor.capture());
        StagedSignup captured = stagedCaptor.getValue();
        assertThat(captured.loginId()).isEqualTo(LOGIN_ID);
        assertThat(captured.passwordHash()).isEqualTo(HASH);
        assertThat(captured.nickname()).isEqualTo(NICK);

        verify(userRepository, never()).save(any(User.class));
        assertThat(resp.userId()).isNull();
        assertThat(resp.status()).isEqualTo("pending");
    }

    // ── verifyEmail (확정 — DB INSERT 시점) ─────────────────

    @Test
    @DisplayName("verifyEmail — 코드 검증 실패: verifyCode 가 예외 던지면 그대로 전파, user INSERT 안 됨")
    void verifyEmail_codeInvalid_propagates() {
        doThrow(new BusinessException(ErrorCode.AUTH_VERIFICATION_CODE_INVALID))
                .when(emailVerificationService).verifyCode(EMAIL, "000000");

        assertThatThrownBy(() ->
                authService.verifyEmail(new EmailVerifyRequest(EMAIL, "000000")))
                .extracting(e -> ((BusinessException) e).errorCode())
                .isEqualTo(ErrorCode.AUTH_VERIFICATION_CODE_INVALID);
        verify(userRepository, never()).save(any(User.class));
    }

    @Test
    @DisplayName("verifyEmail — staging 없음(만료): CODE_EXPIRED")
    void verifyEmail_noStaged_throwsExpired() {
        when(emailVerificationService.readStagedSignup(EMAIL)).thenReturn(Optional.empty());

        assertThatThrownBy(() ->
                authService.verifyEmail(new EmailVerifyRequest(EMAIL, "123456")))
                .extracting(e -> ((BusinessException) e).errorCode())
                .isEqualTo(ErrorCode.AUTH_VERIFICATION_CODE_EXPIRED);
        verify(userRepository, never()).save(any(User.class));
    }

    @Test
    @DisplayName("verifyEmail — staging 중에 동일 loginId 가 ACTIVE 로 가입돼버리면: USER_LOGIN_ID_DUPLICATED")
    void verifyEmail_staleStagedLoginIdNowTaken_throws() {
        when(emailVerificationService.readStagedSignup(EMAIL)).thenReturn(Optional.of(staged()));
        when(userRepository.existsByLoginId(LOGIN_ID)).thenReturn(true);

        assertThatThrownBy(() ->
                authService.verifyEmail(new EmailVerifyRequest(EMAIL, "123456")))
                .extracting(e -> ((BusinessException) e).errorCode())
                .isEqualTo(ErrorCode.USER_LOGIN_ID_DUPLICATED);
        verify(userRepository, never()).save(any(User.class));
    }

    @Test
    @DisplayName("verifyEmail — 정상: User ACTIVE 로 INSERT + 도메인 default 초기화 + staging 정리 + 토큰 발급")
    void verifyEmail_success_createsActiveUserAndIssuesTokens() {
        when(emailVerificationService.readStagedSignup(EMAIL)).thenReturn(Optional.of(staged()));
        when(userRepository.existsByLoginId(LOGIN_ID)).thenReturn(false);
        when(userRepository.existsByEmail(EMAIL)).thenReturn(false);
        when(userRepository.existsByNickname(NICK)).thenReturn(false);
        when(memoryFrameRepository.findAll()).thenReturn(List.of());
        when(weaponRepository.findAllByParentWeaponIdIsNull()).thenReturn(List.of());
        ArgumentCaptor<User> userCaptor = ArgumentCaptor.forClass(User.class);
        when(userRepository.save(userCaptor.capture())).thenAnswer(inv -> inv.getArgument(0));
        stubTokenIssuance();

        TokenResponse resp = authService.verifyEmail(new EmailVerifyRequest(EMAIL, "123456"));

        User saved = userCaptor.getValue();
        assertThat(saved.getLoginId()).isEqualTo(LOGIN_ID);
        assertThat(saved.getEmail()).isEqualTo(EMAIL);
        assertThat(saved.getPasswordHash()).isEqualTo(HASH);
        assertThat(saved.getStatus()).isEqualTo(UserStatus.ACTIVE);
        verify(emailVerificationService).verifyCode(EMAIL, "123456");
        verify(emailVerificationService).clearStagedSignup(EMAIL);
        assertThat(resp.accessToken()).isEqualTo("access-tk");
    }

    // ── login ────────────────────────────────────────────────

    @Test
    @DisplayName("login — loginId 없음: AUTH_INVALID_CREDENTIALS")
    void login_userNotFound_throws() {
        when(userRepository.findByLoginId(LOGIN_ID)).thenReturn(Optional.empty());

        assertThatThrownBy(() -> authService.login(new LoginRequest(LOGIN_ID, PASSWORD)))
                .extracting(e -> ((BusinessException) e).errorCode())
                .isEqualTo(ErrorCode.AUTH_INVALID_CREDENTIALS);
    }

    @Test
    @DisplayName("login — 비번 불일치: AUTH_INVALID_CREDENTIALS")
    void login_passwordMismatch_throws() {
        User user = User.create(LOGIN_ID, EMAIL, HASH, NICK);   // ACTIVE
        when(userRepository.findByLoginId(LOGIN_ID)).thenReturn(Optional.of(user));
        when(passwordEncoder.matches(PASSWORD, HASH)).thenReturn(false);

        assertThatThrownBy(() -> authService.login(new LoginRequest(LOGIN_ID, PASSWORD)))
                .extracting(e -> ((BusinessException) e).errorCode())
                .isEqualTo(ErrorCode.AUTH_INVALID_CREDENTIALS);
    }

    @Test
    @DisplayName("login — ACTIVE: 토큰 발급 + lastLoginAt 갱신")
    void login_active_returnsTokens() {
        User user = User.create(LOGIN_ID, EMAIL, HASH, NICK);
        when(userRepository.findByLoginId(LOGIN_ID)).thenReturn(Optional.of(user));
        when(passwordEncoder.matches(PASSWORD, HASH)).thenReturn(true);
        stubTokenIssuance();

        TokenResponse resp = authService.login(new LoginRequest(LOGIN_ID, PASSWORD));

        assertThat(resp.accessToken()).isEqualTo("access-tk");
        assertThat(user.getLastLoginAt()).isNotNull();
    }

    // ── resendVerificationCode ───────────────────────────────

    @Test
    @DisplayName("resendVerificationCode — staging 없음: CODE_EXPIRED + 재발송 호출 안 함")
    void resendVerificationCode_noStaged_throws() {
        when(emailVerificationService.readStagedSignup(EMAIL)).thenReturn(Optional.empty());

        assertThatThrownBy(() ->
                authService.resendVerificationCode(new EmailResendRequest(EMAIL)))
                .extracting(e -> ((BusinessException) e).errorCode())
                .isEqualTo(ErrorCode.AUTH_VERIFICATION_CODE_EXPIRED);
        verify(emailVerificationService, never()).sendCodeForSignup(anyString(), any());
    }

    @Test
    @DisplayName("resendVerificationCode — staging 있음: sendCodeForSignup 호출 (코드 재발급 + TTL 재시작)")
    void resendVerificationCode_staged_resends() {
        StagedSignup s = staged();
        when(emailVerificationService.readStagedSignup(EMAIL)).thenReturn(Optional.of(s));

        authService.resendVerificationCode(new EmailResendRequest(EMAIL));

        verify(emailVerificationService).sendCodeForSignup(EMAIL, s);
    }

    // ── requestPasswordReset ─────────────────────────────────

    @Test
    @DisplayName("requestPasswordReset — 이메일 없음: 조용히 종료 (계정 열거 방지)")
    void requestPasswordReset_userNotFound_silentlyReturns() {
        when(userRepository.findByEmail(EMAIL)).thenReturn(Optional.empty());

        authService.requestPasswordReset(new PasswordResetRequest(EMAIL));

        verify(passwordResetService, never()).sendCode(anyString());
    }

    @Test
    @DisplayName("requestPasswordReset — ACTIVE 유저: sendCode 호출")
    void requestPasswordReset_active_callsSendCode() {
        User user = User.create(LOGIN_ID, EMAIL, HASH, NICK);   // ACTIVE
        when(userRepository.findByEmail(EMAIL)).thenReturn(Optional.of(user));

        authService.requestPasswordReset(new PasswordResetRequest(EMAIL));

        verify(passwordResetService).sendCode(EMAIL);
    }

    // ── confirmPasswordReset ─────────────────────────────────

    @Test
    @DisplayName("confirmPasswordReset — 이메일 없음: CODE_EXPIRED + verifyCode 호출 안 함")
    void confirmPasswordReset_userNotFound_throwsExpired() {
        when(userRepository.findByEmail(EMAIL)).thenReturn(Optional.empty());

        assertThatThrownBy(() ->
                authService.confirmPasswordReset(
                        new PasswordResetConfirmRequest(EMAIL, "123456", "NewPass123!")))
                .extracting(e -> ((BusinessException) e).errorCode())
                .isEqualTo(ErrorCode.AUTH_PASSWORD_RESET_CODE_EXPIRED);
        verify(passwordResetService, never()).verifyCode(anyString(), anyString());
    }

    @Test
    @DisplayName("confirmPasswordReset — 새 비번이 loginId 와 동일: AUTH_PASSWORD_POLICY_VIOLATION + 코드 미소비")
    void confirmPasswordReset_newPasswordEqualsLoginId_throwsBeforeCodeVerify() {
        User user = User.create(LOGIN_ID, EMAIL, HASH, NICK);
        when(userRepository.findByEmail(EMAIL)).thenReturn(Optional.of(user));

        assertThatThrownBy(() ->
                authService.confirmPasswordReset(
                        new PasswordResetConfirmRequest(EMAIL, "123456", LOGIN_ID)))
                .extracting(e -> ((BusinessException) e).errorCode())
                .isEqualTo(ErrorCode.AUTH_PASSWORD_POLICY_VIOLATION);
        verify(passwordResetService, never()).verifyCode(anyString(), anyString());
    }

    @Test
    @DisplayName("confirmPasswordReset — 코드 불일치: verifyCode 예외 전파, 비번 미변경")
    void confirmPasswordReset_wrongCode_propagatesAndDoesNotChangePassword() {
        User user = User.create(LOGIN_ID, EMAIL, HASH, NICK);
        when(userRepository.findByEmail(EMAIL)).thenReturn(Optional.of(user));
        doThrow(new BusinessException(ErrorCode.AUTH_PASSWORD_RESET_CODE_INVALID))
                .when(passwordResetService).verifyCode(EMAIL, "000000");

        assertThatThrownBy(() ->
                authService.confirmPasswordReset(
                        new PasswordResetConfirmRequest(EMAIL, "000000", "NewPass123!")))
                .extracting(e -> ((BusinessException) e).errorCode())
                .isEqualTo(ErrorCode.AUTH_PASSWORD_RESET_CODE_INVALID);
        verify(passwordEncoder, never()).encode(anyString());
        verify(refreshTokenRepository, never()).revokeAllActiveByUserId(any(), any());
    }

    @Test
    @DisplayName("confirmPasswordReset — 정상: 비번 갱신 + 모든 refresh 토큰 revoke")
    void confirmPasswordReset_success_updatesPasswordAndRevokesTokens() {
        User user = User.create(LOGIN_ID, EMAIL, HASH, NICK);
        when(userRepository.findByEmail(EMAIL)).thenReturn(Optional.of(user));
        when(passwordEncoder.encode("NewPass123!")).thenReturn("new-bcrypt-hash");

        authService.confirmPasswordReset(
                new PasswordResetConfirmRequest(EMAIL, "123456", "NewPass123!"));

        verify(passwordResetService).verifyCode(EMAIL, "123456");
        assertThat(user.getPasswordHash()).isEqualTo("new-bcrypt-hash");
        verify(refreshTokenRepository).revokeAllActiveByUserId(any(), any());
    }

    // ── helpers ──────────────────────────────────────────────

    /** issueTokens 흐름에 필요한 JWT/Properties stub. */
    private void stubTokenIssuance() {
        when(jwtProvider.createAccessToken(any())).thenReturn("access-tk");
        when(jwtProvider.createRefreshToken(any()))
                .thenReturn(new TokenIssueResult("refresh-tk", "jti-1"));
        when(jwtProvider.hashForStorage("refresh-tk")).thenReturn("refresh-hash");
        when(jwtProperties.refreshExpiration()).thenReturn(86400L);
        when(jwtProperties.accessExpiration()).thenReturn(1800L);
    }
}
