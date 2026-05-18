package com.lostmemory.server.auth.entity;

import com.lostmemory.server.user.entity.User;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EntityListeners;
import jakarta.persistence.FetchType;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Index;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;
import org.hibernate.annotations.OnDelete;
import org.hibernate.annotations.OnDeleteAction;
import org.springframework.data.annotation.CreatedDate;
import org.springframework.data.jpa.domain.support.AuditingEntityListener;

import java.time.Instant;

@Entity
@Table(
        name = "auth_refresh_tokens",
        indexes = {
                @Index(name = "idx_auth_refresh_tokens_user_id", columnList = "user_id"),
                @Index(name = "uk_auth_refresh_tokens_token_hash", columnList = "token_hash", unique = true)
        }
)
@EntityListeners(AuditingEntityListener.class)
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class AuthRefreshToken {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    @Column(name = "refresh_token_id")
    private Long id;

    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "user_id", nullable = false)
    @OnDelete(action = OnDeleteAction.CASCADE)
    private User user;

    @Column(name = "token_hash", nullable = false, length = 64)
    private String tokenHash;

    @Column(name = "expires_at", nullable = false)
    private Instant expiresAt;

    @Column(name = "revoked_at")
    private Instant revokedAt;

    @Column(name = "last_used_at")
    private Instant lastUsedAt;

    @CreatedDate
    @Column(name = "created_at", nullable = false, updatable = false)
    private Instant createdAt;

    private AuthRefreshToken(User user, String tokenHash, Instant expiresAt) {
        this.user = user;
        this.tokenHash = tokenHash;
        this.expiresAt = expiresAt;
    }

    /** 새 refresh 토큰 발급 row 생성 — token 원문은 저장하지 않고 SHA-256 해시(token_hash)만 보관한다. */
    public static AuthRefreshToken issue(User user, String tokenHash, Instant expiresAt) {
        return new AuthRefreshToken(user, tokenHash, expiresAt);
    }

    /** 로그아웃 / rotation / reuse 감지 시 호출. 이미 revoke 된 토큰은 다시 revoke 하지 않는다. */
    public void revoke() {
        if (this.revokedAt == null) {
            this.revokedAt = Instant.now();
        }
    }

    /** rotation 직전 사용 흔적 기록용. */
    public void markUsed() {
        this.lastUsedAt = Instant.now();
    }

    /** 만료/revoke 둘 다 통과해야 사용 가능. */
    public boolean isActive(Instant now) {
        return revokedAt == null && expiresAt.isAfter(now);
    }
}
