package com.lostmemory.server.weapon.dto;

import com.lostmemory.server.weapon.entity.Weapon;

/**
 * 무기 마스터 응답. parentWeaponId 가 null 이면 트리 루트.
 * 클라가 parentWeaponId 로 트리 재구성.
 */
public record WeaponResponse(
        Long weaponId,
        String weaponName,
        String weaponType,
        Long parentWeaponId,
        Integer costMemoryShards,
        Integer displayOrder
) {
    public static WeaponResponse from(Weapon weapon) {
        return new WeaponResponse(
                weapon.getId(),
                weapon.getWeaponName(),
                weapon.getWeaponType(),
                weapon.getParentWeaponId(),
                weapon.getCostMemoryShards(),
                weapon.getDisplayOrder()
        );
    }
}
