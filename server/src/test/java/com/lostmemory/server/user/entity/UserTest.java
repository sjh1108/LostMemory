package com.lostmemory.server.user.entity;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import static org.assertj.core.api.Assertions.assertThat;

/**
 * User 엔티티의 팩토리·상태 메서드 단위 검증.
 *
 * 이메일 인증 통과 시점에 비로소 User row 가 INSERT 되는 흐름이므로 create() 는 ACTIVE 로 시작한다.
 */
class UserTest {

    private static final String LOGIN_ID = "testuser";
    private static final String EMAIL = "tester@example.com";
    private static final String HASH  = "bcrypt-hash";
    private static final String NICK  = "테스터1";

    @Test
    @DisplayName("create — 신규 유저는 ACTIVE 로 시작 + 4개 필드 모두 채워짐")
    void create_initialStatusIsActive() {
        User user = User.create(LOGIN_ID, EMAIL, HASH, NICK);

        assertThat(user.getLoginId()).isEqualTo(LOGIN_ID);
        assertThat(user.getEmail()).isEqualTo(EMAIL);
        assertThat(user.getPasswordHash()).isEqualTo(HASH);
        assertThat(user.getNickname()).isEqualTo(NICK);
        assertThat(user.getStatus()).isEqualTo(UserStatus.ACTIVE);
        assertThat(user.getLastLoginAt()).isNull();
    }

    @Test
    @DisplayName("markLoggedIn — lastLoginAt 갱신")
    void markLoggedIn_setsLastLoginAt() {
        User user = User.create(LOGIN_ID, EMAIL, HASH, NICK);

        user.markLoggedIn();

        assertThat(user.getLastLoginAt()).isNotNull();
    }

    @Test
    @DisplayName("changePassword — passwordHash 교체")
    void changePassword_replacesHash() {
        User user = User.create(LOGIN_ID, EMAIL, HASH, NICK);

        user.changePassword("new-bcrypt-hash");

        assertThat(user.getPasswordHash()).isEqualTo("new-bcrypt-hash");
    }
}
