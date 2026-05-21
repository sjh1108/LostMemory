package com.lostmemory.server.weapon.entity;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import static org.assertj.core.api.Assertions.assertThat;

class UserWeaponSelectionTest {

    @Test
    @DisplayName("create — 초기 weaponId 로 row 생성")
    void create_initializesSelectedWeapon() {
        UserWeaponSelection selection = UserWeaponSelection.create(null, 1L);

        assertThat(selection.getSelectedWeaponId()).isEqualTo(1L);
    }

    @Test
    @DisplayName("changeWeapon — selectedWeaponId 갱신")
    void changeWeapon_updatesSelectedWeapon() {
        UserWeaponSelection selection = UserWeaponSelection.create(null, 1L);

        selection.changeWeapon(2L);

        assertThat(selection.getSelectedWeaponId()).isEqualTo(2L);
    }
}
