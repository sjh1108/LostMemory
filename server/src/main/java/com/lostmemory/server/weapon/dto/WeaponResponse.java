package com.lostmemory.server.weapon.dto;

import com.lostmemory.server.weapon.entity.Weapon;
import io.swagger.v3.oas.annotations.media.Schema;

/**
 * 무기 마스터 응답. parentWeaponId 가 null 이면 트리 루트.
 * 클라가 parentWeaponId 로 트리 재구성.
 */
public record WeaponResponse(
        @Schema(description = "무기 PK", example = "1")
        Long weaponId,

        @Schema(description = "무기 이름", example = "기본 검")
        String weaponName,

        @Schema(description = "무기 타입 (MELEE / RANGED / ... )", example = "MELEE")
        String weaponType,

        @Schema(description = "부모 무기 ID. null 이면 트리 루트, 값 있으면 해당 weaponId 의 자식",
                example = "null", nullable = true)
        Long parentWeaponId,

        @Schema(description = "정렬 순서 (오름차순 — 응답이 이 기준으로 정렬됨)", example = "1")
        Integer displayOrder
) {
    public static WeaponResponse from(Weapon weapon) {
        return new WeaponResponse(
                weapon.getId(),
                weapon.getWeaponName(),
                weapon.getWeaponType(),
                weapon.getParentWeaponId(),
                weapon.getDisplayOrder()
        );
    }
}
