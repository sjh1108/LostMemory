package com.lostmemory.server.global.security;

import com.lostmemory.server.global.exception.BusinessException;
import com.lostmemory.server.global.exception.ErrorCode;
import io.jsonwebtoken.Claims;
import io.jsonwebtoken.ExpiredJwtException;
import io.jsonwebtoken.JwtException;
import io.jsonwebtoken.Jwts;
import io.jsonwebtoken.security.Keys;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Component;

import javax.crypto.SecretKey;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.Date;
import java.util.HexFormat;
import java.util.UUID;

@Slf4j
@Component
public class JwtProvider {

    private static final String TYPE_CLAIM = "type";
    private static final String TYPE_ACCESS = "access";
    private static final String TYPE_REFRESH = "refresh";

    private final JwtProperties properties;
    private final SecretKey key;

    public JwtProvider(JwtProperties properties) {
        this.properties = properties;
        this.key = Keys.hmacShaKeyFor(properties.secret().getBytes(StandardCharsets.UTF_8));
        log.debug("[JWT] accessExp=" + properties.accessExpiration()
                + "s, refreshExp=" + properties.refreshExpiration()
                + "s, secretLen=" + properties.secret().length());
    }

    /** userId를 sub에 담은 Access 토큰 발급 */
    public String createAccessToken(Long userId) {
        long nowMs = System.currentTimeMillis();
        return Jwts.builder()
                .subject(String.valueOf(userId))
                .issuedAt(new Date(nowMs))
                .expiration(new Date(nowMs + properties.accessExpiration() * 1000L))
                .claim(TYPE_CLAIM, TYPE_ACCESS)
                .signWith(key, Jwts.SIG.HS256)
                .compact();
    }

    /** Refresh 토큰 발급: 내부에서 jti(UUID) 생성, 토큰 + jti 반환 (호출자가 DB 저장에 사용) */
    public TokenIssueResult createRefreshToken(Long userId) {
        String jti = UUID.randomUUID().toString();
        long nowMs = System.currentTimeMillis();
        String token = Jwts.builder()
                .subject(String.valueOf(userId))
                .id(jti)
                .issuedAt(new Date(nowMs))
                .expiration(new Date(nowMs + properties.refreshExpiration() * 1000L))
                .claim(TYPE_CLAIM, TYPE_REFRESH)
                .signWith(key, Jwts.SIG.HS256)
                .compact();
        return new TokenIssueResult(token, jti);
    }

    /** 토큰 검증 후 sub 를 Long userId 로 반환 */
    public Long getUserId(String token) {
        return Long.valueOf(parse(token).getSubject());
    }

    /** 토큰 검증 후 jti 반환 (refresh 검증 시 DB 매칭용) */
    public String getJti(String token) {
        return parse(token).getId();
    }

    /** 서명·만료 등 검증만 수행 (예외 없이 boolean) */
    public boolean isValid(String token) {
        try {
            parse(token);
            return true;
        } catch (JwtException | IllegalArgumentException e) {
            return false;
        }
    }

    /**
     * Access 토큰을 파싱·검증하고 userId(sub) 반환.
     * 만료 시 AUTH_TOKEN_EXPIRED, 서명 불일치/형식 오류/refresh type 등 그 외 무효 시 AUTH_TOKEN_INVALID.
     * 인증 필터가 catch 해서 ErrorResponseWriter 로 401 응답을 직접 작성한다.
     */
    public Long parseAccessToken(String token) {
        Claims claims;
        try {
            claims = parse(token);
        } catch (ExpiredJwtException e) {
            throw new BusinessException(ErrorCode.AUTH_TOKEN_EXPIRED);
        } catch (JwtException | IllegalArgumentException e) {
            throw new BusinessException(ErrorCode.AUTH_TOKEN_INVALID);
        }

        if (!TYPE_ACCESS.equals(claims.get(TYPE_CLAIM))) {
            throw new BusinessException(ErrorCode.AUTH_TOKEN_INVALID);
        }
        return Long.valueOf(claims.getSubject());
    }

    /** refresh 토큰 DB 저장용 SHA-256 해시 (원문 저장 금지) */
    public String hashForStorage(String token) {
        try {
            MessageDigest md = MessageDigest.getInstance("SHA-256");
            byte[] digest = md.digest(token.getBytes(StandardCharsets.UTF_8));
            return HexFormat.of().formatHex(digest);
        } catch (NoSuchAlgorithmException e) {
            throw new IllegalStateException("SHA-256 unavailable", e);
        }
    }

    private Claims parse(String token) {
        return Jwts.parser()
                .verifyWith(key)
                .build()
                .parseSignedClaims(token)
                .getPayload();
    }
}
