package com.lostmemory.server.auth.service;

import com.lostmemory.server.auth.entity.AuthRefreshToken;
import com.lostmemory.server.auth.dto.EmailResendRequest;
import com.lostmemory.server.auth.dto.EmailVerifyRequest;
import com.lostmemory.server.auth.dto.LoginRequest;
import com.lostmemory.server.auth.dto.LogoutRequest;
import com.lostmemory.server.auth.dto.PasswordResetConfirmRequest;
import com.lostmemory.server.auth.dto.PasswordResetRequest;
import com.lostmemory.server.auth.dto.RefreshRequest;
import com.lostmemory.server.auth.dto.SignupRequest;
import com.lostmemory.server.auth.dto.SignupResponse;
import com.lostmemory.server.auth.dto.TokenResponse;
import com.lostmemory.server.auth.repository.AuthRefreshTokenRepository;
import com.lostmemory.server.global.exception.BusinessException;
import com.lostmemory.server.global.exception.ErrorCode;
import com.lostmemory.server.global.security.JwtProperties;
import com.lostmemory.server.global.security.JwtProvider;
import com.lostmemory.server.global.security.TokenIssueResult;
import com.lostmemory.server.memory.entity.MemoryFrame;
import com.lostmemory.server.memory.entity.UserMemoryProgress;
import com.lostmemory.server.memory.repository.MemoryFrameRepository;
import com.lostmemory.server.memory.repository.UserMemoryProgressRepository;
import com.lostmemory.server.user.entity.User;
import com.lostmemory.server.user.entity.UserCurrency;
import com.lostmemory.server.user.entity.UserRecord;
import com.lostmemory.server.user.entity.UserStatus;
import com.lostmemory.server.user.entity.UserTalentAllocation;
import com.lostmemory.server.user.repository.UserCurrencyRepository;
import com.lostmemory.server.user.repository.UserRecordRepository;
import com.lostmemory.server.user.repository.UserRepository;
import com.lostmemory.server.user.repository.UserTalentAllocationRepository;
import com.lostmemory.server.weapon.entity.UserWeaponSelection;
import com.lostmemory.server.weapon.entity.UserWeaponUnlock;
import com.lostmemory.server.weapon.entity.Weapon;
import com.lostmemory.server.weapon.repository.UserWeaponSelectionRepository;
import com.lostmemory.server.weapon.repository.UserWeaponUnlockRepository;
import com.lostmemory.server.weapon.repository.WeaponRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.security.crypto.password.PasswordEncoder;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.time.Instant;
import java.util.List;
import java.util.Optional;

@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class AuthService {

    private final UserRepository userRepository;
    private final AuthRefreshTokenRepository refreshTokenRepository;
    private final PasswordEncoder passwordEncoder;
    private final JwtProvider jwtProvider;
    private final JwtProperties jwtProperties;
    private final UserCurrencyRepository userCurrencyRepository;
    private final UserRecordRepository userRecordRepository;
    private final MemoryFrameRepository memoryFrameRepository;
    private final UserMemoryProgressRepository userMemoryProgressRepository;
    private final WeaponRepository weaponRepository;
    private final UserWeaponUnlockRepository userWeaponUnlockRepository;
    private final UserTalentAllocationRepository userTalentAllocationRepository;
    private final UserWeaponSelectionRepository userWeaponSelectionRepository;
    private final EmailVerificationService emailVerificationService;
    private final PasswordResetService passwordResetService;

    /** 회원가입 시 default 장착 무기 ID — 검(weapon_id=1). */
    private static final long DEFAULT_SELECTED_WEAPON_ID = 1L;

    /**
     * 회원가입(요청 단계): loginId/email/nickname 중복 검증 후 BCrypt 해시한 비밀번호와 함께
     * Redis 에 가입 정보를 staging 만 한다. 인증 코드 메일 발송 후 종료.
     *
     * 이 시점엔 DB 에 user row 를 만들지 않는다 — verify 통과 시점에 비로소 INSERT.
     * 사용자가 가입 도중 이탈하거나 코드 만료 / 시도 초과로 invalidate 되면 Redis TTL 로 자동 정리된다.
     */
    @Transactional(readOnly = true)
    public SignupResponse signup(SignupRequest request) {
        if (userRepository.existsByLoginId(request.loginId())) {
            throw new BusinessException(ErrorCode.USER_LOGIN_ID_DUPLICATED);
        }
        if (userRepository.existsByEmail(request.email())) {
            throw new BusinessException(ErrorCode.USER_EMAIL_DUPLICATED);
        }
        if (userRepository.existsByNickname(request.nickname())) {
            throw new BusinessException(ErrorCode.USER_NICKNAME_DUPLICATED);
        }

        // 비밀번호가 loginId·이메일 로컬파트·닉네임과 동일하면 거부 (cross-field — Bean Validation 으로 못 잡음)
        PasswordIdentityChecker.assertNotIdentifierLookalike(
                request.password(), request.loginId(), request.email(), request.nickname());

        String passwordHash = passwordEncoder.encode(request.password());
        StagedSignup staged = new StagedSignup(request.loginId(), passwordHash, request.nickname());
        emailVerificationService.sendCodeForSignup(request.email(), staged);

        return new SignupResponse(null, "pending");
    }

    /**
     * 이메일 인증 코드 검증 = 회원가입 확정.
     * 코드 통과 → Redis staging 조회 → uniqueness 재검증 → User row INSERT (ACTIVE) + 도메인 default 초기화 +
     * staging 정리 + 토큰 발급. 모두 한 트랜잭션.
     *
     * staging 만료/없음 → AUTH_VERIFICATION_CODE_EXPIRED.
     */
    @Transactional
    public TokenResponse verifyEmail(EmailVerifyRequest request) {
        // 1. 코드 검증부터 (실패 시 try 카운터 증가, 5회 초과 시 staging 도 같이 무효화됨)
        emailVerificationService.verifyCode(request.email(), request.code());

        // 2. staging 조회 — 정상 흐름이면 반드시 존재. 동시 만료 이중 안전망.
        StagedSignup staged = emailVerificationService.readStagedSignup(request.email())
                .orElseThrow(() -> new BusinessException(ErrorCode.AUTH_VERIFICATION_CODE_EXPIRED));

        // 3. uniqueness 재검증 — staging 사이에 다른 ACTIVE 가 생겼을 수 있다 (다른 유저의 가입 동시 진행)
        if (userRepository.existsByLoginId(staged.loginId())) {
            throw new BusinessException(ErrorCode.USER_LOGIN_ID_DUPLICATED);
        }
        if (userRepository.existsByEmail(request.email())) {
            throw new BusinessException(ErrorCode.USER_EMAIL_DUPLICATED);
        }
        if (userRepository.existsByNickname(staged.nickname())) {
            throw new BusinessException(ErrorCode.USER_NICKNAME_DUPLICATED);
        }

        // 4. User 생성 (ACTIVE 시작) + 도메인 default 초기화
        User user = userRepository.save(
                User.create(staged.loginId(), request.email(), staged.passwordHash(), staged.nickname()));
        initializeUserDomainDefaults(user);

        // 5. staging 정리 + 토큰 발급
        emailVerificationService.clearStagedSignup(request.email());
        user.markLoggedIn();
        return issueTokens(user);
    }

    /**
     * 인증 코드 재발송. staging 이 살아있어야만 가능 — 없으면 가입을 다시 진행해야 한다.
     * 재발송 시 새 코드 생성·발송, staging TTL 도 재시작.
     */
    @Transactional(readOnly = true)
    public void resendVerificationCode(EmailResendRequest request) {
        StagedSignup staged = emailVerificationService.readStagedSignup(request.email())
                .orElseThrow(() -> new BusinessException(ErrorCode.AUTH_VERIFICATION_CODE_EXPIRED));
        emailVerificationService.sendCodeForSignup(request.email(), staged);
    }

    private void initializeUserDomainDefaults(User user) {
        // 1. 재화 0
        userCurrencyRepository.save(UserCurrency.create(user, 0));

        // 2. 최고 전적 0/0
        userRecordRepository.save(UserRecord.create(user, 0, 0));

        // 3. 프레임별 진행도 — 모든 frame 에 unlocked_mask=0 row 생성
        List<MemoryFrame> frames = memoryFrameRepository.findAll();
        for (MemoryFrame frame : frames) {
            userMemoryProgressRepository.save(UserMemoryProgress.create(user.getId(), frame.getId()));
        }

        // 4. 기본 무기 자동 해금 — 트리 루트(parent_weapon_id IS NULL)
        List<Weapon> rootWeapons = weaponRepository.findAllByParentWeaponIdIsNull();
        for (Weapon weapon : rootWeapons) {
            userWeaponUnlockRepository.save(UserWeaponUnlock.of(user, weapon.getId()));
        }

        // 5. 재능 분배 초기값 — 4 slot 모두 0
        userTalentAllocationRepository.save(UserTalentAllocation.create(user));

        // 6. default 장착 무기 — 검(weapon_id=1)
        userWeaponSelectionRepository.save(
                UserWeaponSelection.create(user, DEFAULT_SELECTED_WEAPON_ID));
    }

    /**
     * 로그인: loginId 로 유저 조회 후 비밀번호 검증 → access/refresh 발급, refresh 해시 DB 저장, lastLoginAt 갱신.
     * PENDING 계정은 이메일 인증을 먼저 요구. SUSPENDED/DELETED 등 비활성 계정은 AUTH_INVALID_CREDENTIALS 로 동일 응답.
     */
    @Transactional
    public TokenResponse login(LoginRequest request) {
        User user = userRepository.findByLoginId(request.loginId())
                .orElseThrow(() -> new BusinessException(ErrorCode.AUTH_INVALID_CREDENTIALS));

        if (!passwordEncoder.matches(request.password(), user.getPasswordHash())) {
            throw new BusinessException(ErrorCode.AUTH_INVALID_CREDENTIALS);
        }
        if (user.getStatus() == UserStatus.PENDING) {
            throw new BusinessException(ErrorCode.AUTH_EMAIL_NOT_VERIFIED);
        }
        if (user.getStatus() != UserStatus.ACTIVE) {
            throw new BusinessException(ErrorCode.AUTH_INVALID_CREDENTIALS);
        }

        user.markLoggedIn();
        return issueTokens(user);
    }

    /**
     * 리프레시: 서명/만료 검증 → DB hash 매칭(SELECT FOR UPDATE) → active 검증.
     * reuse(이미 revoke 된 토큰 재사용) 감지 시 해당 user 의 refresh family 전체를 revoke 하고 예외.
     * 정상이면 기존 토큰 markUsed + revoke 후 신규 토큰 페어 발급(rotation).
     * 비관적 락으로 동시 요청에 의한 두 쌍 동시 발급 race 차단.
     */
    @Transactional
    public TokenResponse refresh(RefreshRequest request) {
        String refreshToken = request.refreshToken();

        if (!jwtProvider.isValid(refreshToken)) {
            throw new BusinessException(ErrorCode.AUTH_TOKEN_INVALID);
        }

        String tokenHash = jwtProvider.hashForStorage(refreshToken);
        AuthRefreshToken stored = refreshTokenRepository.findByTokenHashForUpdate(tokenHash)
                .orElseThrow(() -> new BusinessException(ErrorCode.AUTH_REFRESH_NOT_FOUND));

        if (!stored.isActive(Instant.now())) {
            refreshTokenRepository.revokeAllActiveByUserId(stored.getUser().getId(), Instant.now());
            throw new BusinessException(ErrorCode.AUTH_REFRESH_REUSED);
        }

        stored.markUsed();
        stored.revoke();

        return issueTokens(stored.getUser());
    }

    /**
     * 비밀번호 재설정 코드 발송 요청.
     * 계정 열거 방지를 위해 결과는 항상 동일 — 존재하지 않는 이메일 / ACTIVE 가 아닌 계정은 조용히 종료.
     * 실제 메일은 ACTIVE 사용자에게만 발송된다.
     */
    @Transactional(readOnly = true)
    public void requestPasswordReset(PasswordResetRequest request) {
        Optional<User> user = userRepository.findByEmail(request.email());
        if (user.isEmpty() || user.get().getStatus() != UserStatus.ACTIVE) {
            return;
        }
        passwordResetService.sendCode(user.get().getEmail());
    }

    /**
     * 비밀번호 재설정 확정.
     * 식별자(이메일·닉네임)와 동일한 새 비번은 거부한다. 코드 검증 성공 시 비밀번호를 갱신하고
     * 해당 user 의 active refresh 토큰 패밀리를 전체 revoke — 다른 디바이스의 세션을 강제 종료.
     *
     * 식별자/정책 위반은 코드를 소비하지 않아 같은 코드로 재시도 가능 (UX 측면).
     */
    @Transactional
    public void confirmPasswordReset(PasswordResetConfirmRequest request) {
        User user = userRepository.findByEmail(request.email())
                .orElseThrow(() -> new BusinessException(ErrorCode.AUTH_PASSWORD_RESET_CODE_EXPIRED));
        if (user.getStatus() != UserStatus.ACTIVE) {
            throw new BusinessException(ErrorCode.AUTH_PASSWORD_RESET_CODE_EXPIRED);
        }
        PasswordIdentityChecker.assertNotIdentifierLookalike(
                request.newPassword(), user.getLoginId(), request.email(), user.getNickname());

        passwordResetService.verifyCode(request.email(), request.code());

        user.changePassword(passwordEncoder.encode(request.newPassword()));
        refreshTokenRepository.revokeAllActiveByUserId(user.getId(), Instant.now());
    }

    /**
     * 로그아웃: 전달된 refresh 토큰 해시로 row 조회 후 revoke (멱등 — 모르는 토큰은 조용히 무시).
     * refresh 와 동일하게 비관적 락을 사용해 동시 logout/refresh 간 정책 일관성 유지.
     */
    @Transactional
    public void logout(LogoutRequest request) {
        String tokenHash = jwtProvider.hashForStorage(request.refreshToken());
        Optional<AuthRefreshToken> stored = refreshTokenRepository.findByTokenHashForUpdate(tokenHash);
        stored.ifPresent(AuthRefreshToken::revoke);
    }

    private TokenResponse issueTokens(User user) {
        String accessToken = jwtProvider.createAccessToken(user.getId());
        TokenIssueResult refresh = jwtProvider.createRefreshToken(user.getId());

        String refreshHash = jwtProvider.hashForStorage(refresh.token());
        Instant expiresAt = Instant.now().plusSeconds(jwtProperties.refreshExpiration());
        refreshTokenRepository.save(AuthRefreshToken.issue(user, refreshHash, expiresAt));

        return new TokenResponse(accessToken, refresh.token(), jwtProperties.accessExpiration());
    }
}
