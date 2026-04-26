package com.lostmemory.server.global.security;

import io.jsonwebtoken.Jwts;
import io.jsonwebtoken.security.Keys;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import javax.crypto.SecretKey;
import java.nio.charset.StandardCharsets;

import static org.assertj.core.api.Assertions.assertThat;

class JwtProviderTest {

    private static final String SECRET = "test-secret-should-be-at-least-32-bytes-long!!";
    private static final long ACCESS_EXP = 3600L;
    private static final long REFRESH_EXP = 1_209_600L;

    private JwtProvider provider;

    @BeforeEach
    void setUp() {
        provider = new JwtProvider(new JwtProperties(SECRET, ACCESS_EXP, REFRESH_EXP));
    }

    @Test
    @DisplayName("access 토큰 발급 후 getUserId 가 원본 userId 를 반환한다")
    void createAccessToken_roundTripsUserId() {
        String token = provider.createAccessToken(42L);

        assertThat(provider.getUserId(token)).isEqualTo(42L);
    }

    @Test
    @DisplayName("refresh 토큰 발급 시 반환된 jti 가 토큰에 담긴 jti 와 일치한다")
    void createRefreshToken_jtiMatches() {
        TokenIssueResult result = provider.createRefreshToken(42L);

        assertThat(result.jti()).isNotBlank();
        assertThat(provider.getJti(result.token())).isEqualTo(result.jti());
        assertThat(provider.getUserId(result.token())).isEqualTo(42L);
    }

    @Test
    @DisplayName("access / refresh 토큰의 type 클레임이 각각 \"access\", \"refresh\" 로 설정된다")
    void typeClaim_differsBetweenAccessAndRefresh() {
        String access = provider.createAccessToken(1L);
        TokenIssueResult refresh = provider.createRefreshToken(1L);

        assertThat(parseType(access)).isEqualTo("access");
        assertThat(parseType(refresh.token())).isEqualTo("refresh");
    }

    @Test
    @DisplayName("이미 만료된 토큰은 isValid 가 false 를 반환한다")
    void expiredToken_isInvalid() {
        // expiration 을 음수로 주면 생성 즉시 과거 시점이 되어 만료 상태
        JwtProvider expiredProvider = new JwtProvider(new JwtProperties(SECRET, -1L, -1L));
        String token = expiredProvider.createAccessToken(1L);

        assertThat(expiredProvider.isValid(token)).isFalse();
    }

    @Test
    @DisplayName("다른 secret 으로 만든 provider 는 원본 토큰을 검증하지 못한다")
    void differentSecret_isInvalid() {
        String token = provider.createAccessToken(1L);
        String otherSecret = "different-secret-also-at-least-32-bytes-long-!!";
        JwtProvider otherProvider = new JwtProvider(new JwtProperties(otherSecret, ACCESS_EXP, REFRESH_EXP));

        assertThat(otherProvider.isValid(token)).isFalse();
    }

    @Test
    @DisplayName("변조된 토큰은 isValid 가 false 를 반환한다")
    void tamperedToken_isInvalid() {
        String token = provider.createAccessToken(1L);
        String tampered = token.substring(0, token.length() - 2) + "XY";

        assertThat(provider.isValid(tampered)).isFalse();
    }

    @Test
    @DisplayName("hashForStorage 는 동일 입력에 동일한 SHA-256 hex(길이 64)를 반환한다")
    void hashForStorage_isDeterministicAndHex() {
        String token = "some-refresh-token-value";

        String h1 = provider.hashForStorage(token);
        String h2 = provider.hashForStorage(token);

        assertThat(h1)
                .isEqualTo(h2)
                .hasSize(64)
                .matches("[0-9a-f]{64}");
    }

    /** 테스트 헬퍼: JwtProvider 내부 파서를 우회해서 type 클레임을 독립적으로 파싱 */
    private String parseType(String token) {
        SecretKey key = Keys.hmacShaKeyFor(SECRET.getBytes(StandardCharsets.UTF_8));
        return Jwts.parser()
                .verifyWith(key)
                .build()
                .parseSignedClaims(token)
                .getPayload()
                .get("type", String.class);
    }
}
