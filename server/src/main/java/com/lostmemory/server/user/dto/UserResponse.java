package com.lostmemory.server.user.dto;

import com.lostmemory.server.user.entity.User;
import com.lostmemory.server.user.entity.UserStatus;

import java.time.Instant;

public record UserResponse(
        Long userId,
        String loginId,
        String nickname,
        UserStatus status,
        Instant createdAt,
        Instant lastLoginAt
) {
    /** User 엔티티에서 외부 노출용 응답 DTO 변환. passwordHash·updatedAt 은 의도적으로 제외. */
    public static UserResponse from(User user) {
        return new UserResponse(
                user.getId(),
                user.getLoginId(),
                user.getNickname(),
                user.getStatus(),
                user.getCreatedAt(),
                user.getLastLoginAt()
        );
    }
}
