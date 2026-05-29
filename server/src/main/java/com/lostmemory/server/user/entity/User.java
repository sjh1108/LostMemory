package com.lostmemory.server.user.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EntityListeners;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import jakarta.persistence.UniqueConstraint;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;
import org.springframework.data.annotation.CreatedDate;
import org.springframework.data.annotation.LastModifiedDate;
import org.springframework.data.jpa.domain.support.AuditingEntityListener;

import java.time.Instant;

@Entity
@Table(
        name = "users",
        uniqueConstraints = {
                @UniqueConstraint(name = "uk_users_login_id", columnNames = "login_id"),
                @UniqueConstraint(name = "uk_users_email", columnNames = "email"),
                @UniqueConstraint(name = "uk_users_nickname", columnNames = "nickname")
        }
)
@EntityListeners(AuditingEntityListener.class)
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class User {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    @Column(name = "user_id")
    private Long id;

    @Column(name = "login_id", nullable = false, length = 50)
    private String loginId;

    @Column(name = "email", nullable = false, length = 255)
    private String email;

    @Column(name = "password_hash", nullable = false, length = 255)
    private String passwordHash;

    @Column(nullable = false, length = 50)
    private String nickname;

    @Column(nullable = false, length = 20)
    private UserStatus status;

    @CreatedDate
    @Column(name = "created_at", nullable = false, updatable = false)
    private Instant createdAt;

    @LastModifiedDate
    @Column(name = "updated_at", nullable = false)
    private Instant updatedAt;

    @Column(name = "last_login_at")
    private Instant lastLoginAt;

    private User(String loginId, String email, String passwordHash, String nickname) {
        this.loginId = loginId;
        this.email = email;
        this.passwordHash = passwordHash;
        this.nickname = nickname;
        this.status = UserStatus.ACTIVE;
    }

    /**
     * 신규 사용자 생성. 이메일 인증 통과 후 호출되므로 status 는 ACTIVE 로 시작.
     * 로그인은 loginId 로, 비밀번호 재설정은 email 로 진행된다.
     * 비밀번호는 호출자가 BCrypt 로 해시한 결과를 전달해야 한다.
     */
    public static User create(String loginId, String email, String passwordHash, String nickname) {
        return new User(loginId, email, passwordHash, nickname);
    }

    /** 로그인 성공 시 last_login_at 갱신. */
    public void markLoggedIn() {
        this.lastLoginAt = Instant.now();
    }

    /** 비밀번호 변경 — 호출자가 BCrypt 로 해시한 결과를 전달해야 한다. updated_at 은 Auditing 으로 자동 갱신. */
    public void changePassword(String newPasswordHash) {
        this.passwordHash = newPasswordHash;
    }
}
