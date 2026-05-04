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
import com.lostmemory.server.user.entity.User;
import com.lostmemory.server.user.repository.UserRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.security.crypto.password.PasswordEncoder;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.time.Instant;
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

    /** 회원가입: loginId/nickname 중복 검증 후 BCrypt 해시한 비밀번호로 User 저장 */
    @Transactional
    public void signup(SignupRequest request) {
        if (userRepository.existsByLoginId(request.loginId())) {
            throw new BusinessException(ErrorCode.USER_LOGIN_ID_DUPLICATED);
        }
        if (userRepository.existsByNickname(request.nickname())) {
            throw new BusinessException(ErrorCode.USER_NICKNAME_DUPLICATED);
        }

        String passwordHash = passwordEncoder.encode(request.password());
        User user = User.create(request.loginId(), passwordHash, request.nickname());
        userRepository.save(user);
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

    /** 로그아웃: 전달된 refresh 토큰 해시로 row 조회 후 revoke (멱등 — 모르는 토큰은 조용히 무시) */
    @Transactional
    public void logout(LogoutRequest request) {
        String tokenHash = jwtProvider.hashForStorage(request.refreshToken());
        Optional<AuthRefreshToken> stored = refreshTokenRepository.findByTokenHash(tokenHash);
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
