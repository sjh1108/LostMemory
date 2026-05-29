package com.lostmemory.server.user.dto;

import com.lostmemory.server.user.entity.User;
import com.lostmemory.server.user.entity.UserStatus;
import io.swagger.v3.oas.annotations.media.Schema;

import java.time.Instant;

public record UserResponse(
        @Schema(description = "유저 PK (users.user_id)", example = "1")
        Long userId,

        @Schema(description = "로그인 ID", example = "testuser")
        String loginId,

        @Schema(description = "연동 이메일", example = "tester@example.com")
        String email,

        @Schema(description = "인게임 닉네임", example = "테스터1")
        String nickname,

        @Schema(description = "계정 상태 (PENDING / ACTIVE / SUSPENDED / DELETED)", example = "ACTIVE")
        UserStatus status,

        @Schema(description = "가입 시각 (UTC)", example = "2026-04-30T08:00:00Z")
        Instant createdAt,

        @Schema(description = "마지막 로그인 시각 (UTC). 미로그인 시 null", example = "2026-05-14T10:42:42Z",
                nullable = true)
        Instant lastLoginAt
) {
    /** User 엔티티에서 외부 노출용 응답 DTO 변환. passwordHash·updatedAt 은 의도적으로 제외. */
    public static UserResponse from(User user) {
        return new UserResponse(
                user.getId(),
                user.getLoginId(),
                user.getEmail(),
                user.getNickname(),
                user.getStatus(),
                user.getCreatedAt(),
                user.getLastLoginAt()
        );
    }
}
