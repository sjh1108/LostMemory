package com.lostmemory.server.auth.service;

import com.lostmemory.server.auth.entity.AuthRefreshToken;
import com.lostmemory.server.auth.dto.LoginRequest;
import com.lostmemory.server.auth.dto.LogoutRequest;
import com.lostmemory.server.auth.dto.RefreshRequest;
import com.lostmemory.server.auth.dto.SignupRequest;
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

    /** 회원가입 시 default 장착 무기 ID — 검(weapon_id=1). */
    private static final long DEFAULT_SELECTED_WEAPON_ID = 1L;

    /**
     * 회원가입: loginId/nickname 중복 검증 후 BCrypt 해시한 비밀번호로 User 저장 +
     * 본 유저의 도메인 default 초기화 — 모두 한 트랜잭션 (실패 시 rollback).
     *  - user_currencies: memory_shards=0
     *  - user_record: cleared_chapter=0, cleared_stage=0
     *  - user_memory_progress: 모든 frame 에 대해 unlocked_mask=0 (Locked)
     *  - user_weapon_unlocks: 트리 루트 무기(parent IS NULL) 자동 해금 — 검·활·스태프
     *  - user_talent_allocations: 5개 slot 분배 모두 0 (총량/잔여는 백엔드 미관리 — 클라가 보유)
     *  - user_weapon_selection: 검(weapon_id=1) default 장착
     */
    @Transactional
    public void signup(SignupRequest request) {
        if (userRepository.existsByLoginId(request.loginId())) {
            throw new BusinessException(ErrorCode.USER_LOGIN_ID_DUPLICATED);
        }
        if (userRepository.existsByNickname(request.nickname())) {
            throw new BusinessException(ErrorCode.USER_NICKNAME_DUPLICATED);
        }

        String passwordHash = passwordEncoder.encode(request.password());
        User user = userRepository.save(User.create(request.loginId(), passwordHash, request.nickname()));

        initializeUserDomainDefaults(user);
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

        // 5. 재능 분배 초기값 — 5개 slot 모두 0 (총량/잔여는 클라 관리)
        userTalentAllocationRepository.save(UserTalentAllocation.create(user));

        // 6. default 장착 무기 — 검(weapon_id=1)
        userWeaponSelectionRepository.save(
                UserWeaponSelection.create(user, DEFAULT_SELECTED_WEAPON_ID));
    }

    /** 로그인: 비밀번호 검증 후 access/refresh 발급, refresh 해시 DB 저장, lastLoginAt 갱신 */
    @Transactional
    public TokenResponse login(LoginRequest request) {
        User user = userRepository.findByLoginId(request.loginId())
                .orElseThrow(() -> new BusinessException(ErrorCode.AUTH_INVALID_CREDENTIALS));

        if (!passwordEncoder.matches(request.password(), user.getPasswordHash())) {
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
