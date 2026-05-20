package com.lostmemory.server.weapon.dto;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.NotNull;

/**
 * 장착 무기 갱신 요청. 본인이 해금한 무기여야 가능.
 */
public record WeaponSelectRequest(
        @Schema(description = "장착할 무기 ID — 본인이 해금한 상태여야 함", example = "2")
        @NotNull
        Long weaponId
) {
}
