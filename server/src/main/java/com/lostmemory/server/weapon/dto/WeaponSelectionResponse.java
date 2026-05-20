package com.lostmemory.server.weapon.dto;

import com.lostmemory.server.weapon.entity.UserWeaponSelection;
import io.swagger.v3.oas.annotations.media.Schema;

/**
 * 장착 무기 갱신 응답 (PUT /users/me/weapons/selected).
 */
public record WeaponSelectionResponse(
        @Schema(description = "갱신된 장착 무기 ID", example = "2")
        Long selectedWeaponId
) {
    public static WeaponSelectionResponse from(UserWeaponSelection entity) {
        return new WeaponSelectionResponse(entity.getSelectedWeaponId());
    }
}
