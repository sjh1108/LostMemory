package com.lostmemory.server.weapon.dto;

import com.lostmemory.server.weapon.entity.UserWeaponUnlock;

import java.time.Instant;

/**
 * 본인 무기 해금 응답. 마을 화면에서 해금 여부 + 시점 표시용.
 */
public record UserWeaponUnlockedResponse(
        Long unlockNodeId,
        Instant unlockedAt
) {
    public static UserWeaponUnlockedResponse from(UserWeaponUnlock unlock) {
        return new UserWeaponUnlockedResponse(
                unlock.getUnlockNodeId(),
                unlock.getUnlockedAt()
        );
    }
}
