package com.lostmemory.server.weapon.dto;

import com.lostmemory.server.weapon.entity.UserWeaponUnlock;
import io.swagger.v3.oas.annotations.media.Schema;

import java.time.Instant;

/**
 * 본인 무기 해금 응답. 마을 화면에서 해금 여부 + 시점 표시용.
 */
public record UserWeaponUnlockedResponse(
        @Schema(description = "해금한 무기 트리 노드 ID. /master/weapons 응답의 weaponId 와 매칭",
                example = "2")
        Long unlockNodeId,

        @Schema(description = "해금 시각 (UTC)", example = "2026-05-10T15:30:00Z")
        Instant unlockedAt
) {
    public static UserWeaponUnlockedResponse from(UserWeaponUnlock unlock) {
        return new UserWeaponUnlockedResponse(
                unlock.getUnlockNodeId(),
                unlock.getUnlockedAt()
        );
    }
}
