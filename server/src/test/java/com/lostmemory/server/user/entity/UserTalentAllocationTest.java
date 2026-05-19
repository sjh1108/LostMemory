package com.lostmemory.server.user.entity;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import static org.assertj.core.api.Assertions.assertThat;

class UserTalentAllocationTest {

    @Test
    @DisplayName("create — 초기 totalPoint 로 row 생성, 분배 모두 0")
    void create_initializesAllSlotsToZero() {
        UserTalentAllocation allocation = UserTalentAllocation.create(null, 5);

        assertThat(allocation.getTotalPoint()).isEqualTo(5);
        assertThat(allocation.getCritRatePoints()).isZero();
        assertThat(allocation.getAttackSpeedPoints()).isZero();
        assertThat(allocation.getDefensePoints()).isZero();
        assertThat(allocation.getManaRegenPoints()).isZero();
        assertThat(allocation.getMaxHpPoints()).isZero();
    }

    @Test
    @DisplayName("investedSum — 5개 slot 합산")
    void investedSum_sumsAllFiveSlots() {
        UserTalentAllocation allocation = UserTalentAllocation.create(null, 10);
        allocation.replaceAllocation(2, 2, 1, 1, 0);

        assertThat(allocation.investedSum()).isEqualTo(6);
    }

    @Test
    @DisplayName("remainingPoint — total - sum")
    void remainingPoint_derivedFromTotalMinusSum() {
        UserTalentAllocation allocation = UserTalentAllocation.create(null, 10);
        allocation.replaceAllocation(2, 2, 1, 1, 0);

        assertThat(allocation.remainingPoint()).isEqualTo(4);
    }

    @Test
    @DisplayName("remainingPoint — 신규 생성 직후엔 total 과 동일 (분배 0)")
    void remainingPoint_freshAllocation_equalsTotal() {
        UserTalentAllocation allocation = UserTalentAllocation.create(null, 5);

        assertThat(allocation.remainingPoint()).isEqualTo(5);
    }

    @Test
    @DisplayName("replaceAllocation — 5개 slot 통째로 교체")
    void replaceAllocation_replacesAllFiveSlots() {
        UserTalentAllocation allocation = UserTalentAllocation.create(null, 5);
        allocation.replaceAllocation(3, 2, 0, 0, 0);

        allocation.replaceAllocation(0, 0, 0, 2, 3);

        assertThat(allocation.getCritRatePoints()).isZero();
        assertThat(allocation.getAttackSpeedPoints()).isZero();
        assertThat(allocation.getDefensePoints()).isZero();
        assertThat(allocation.getManaRegenPoints()).isEqualTo(2);
        assertThat(allocation.getMaxHpPoints()).isEqualTo(3);
    }
}
