package com.lostmemory.server.weapon.dto;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotNull;

/**
 * 무기 해금 요청.
 *
 * 처리: 보유 shards >= consumedShards 검증 → 차감 → user_weapon_unlocks insert.
 * 이미 해금된 무기는 idempotent (변화 없음, shards 차감 없음).
 */
public record WeaponUnlockRequest(
        @Schema(description = "해금할 무기 ID", example = "2")
        @NotNull
        Long weaponId,

        @Schema(description = "해금에 소비할 파편 수량 (양수). 클라가 계산해서 보냄.", example = "50")
        @NotNull @Min(1)
        Integer consumedShards
) {
}
