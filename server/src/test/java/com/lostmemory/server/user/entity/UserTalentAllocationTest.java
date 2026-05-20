package com.lostmemory.server.user.entity;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import static org.assertj.core.api.Assertions.assertThat;

class UserTalentAllocationTest {

    @Test
    @DisplayName("create — 신규 row 는 4개 slot 모두 0")
    void create_initializesAllSlotsToZero() {
        UserTalentAllocation allocation = UserTalentAllocation.create(null);

        assertThat(allocation.getCritRatePoints()).isZero();
        assertThat(allocation.getAttackSpeedPoints()).isZero();
        assertThat(allocation.getDefensePoints()).isZero();
        assertThat(allocation.getMaxHpPoints()).isZero();
    }

    @Test
    @DisplayName("replaceAllocation — 4개 slot 통째로 교체")
    void replaceAllocation_replacesAllFourSlots() {
        UserTalentAllocation allocation = UserTalentAllocation.create(null);
        allocation.replaceAllocation(3, 2, 0, 0);

        allocation.replaceAllocation(0, 0, 2, 3);

        assertThat(allocation.getCritRatePoints()).isZero();
        assertThat(allocation.getAttackSpeedPoints()).isZero();
        assertThat(allocation.getDefensePoints()).isEqualTo(2);
        assertThat(allocation.getMaxHpPoints()).isEqualTo(3);
    }
}
