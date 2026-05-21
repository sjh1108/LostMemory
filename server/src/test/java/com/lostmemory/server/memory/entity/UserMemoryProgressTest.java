package com.lostmemory.server.memory.entity;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

class UserMemoryProgressTest {

    @Test
    @DisplayName("create — 신규 row 는 unlockedMask=0 으로 시작")
    void create_startsWithMaskZero() {
        UserMemoryProgress p = UserMemoryProgress.create(1L, 1L);

        assertThat(p.getUnlockedMask()).isZero();
        assertThat(MemoryProgressState.fromMask(p.getUnlockedMask()))
                .isEqualTo(MemoryProgressState.LOCKED);
    }

    @ParameterizedTest
    @ValueSource(ints = {0, 1, 2, 3, 4, 5})
    @DisplayName("unlockSlot — 유효 범위 (0~5) 호출 시 해당 bit set + true 반환")
    void unlockSlot_validIndex_setsBitAndReturnsTrue(int slotIndex) {
        UserMemoryProgress p = UserMemoryProgress.create(1L, 1L);

        boolean changed = p.unlockSlot(slotIndex);

        assertThat(changed).isTrue();
        assertThat(p.getUnlockedMask()).isEqualTo(1 << slotIndex);
    }

    @Test
    @DisplayName("unlockSlot — 이미 해금된 slot 재호출 시 false 반환 (idempotent, mask 변화 없음)")
    void unlockSlot_alreadyUnlocked_isIdempotent() {
        UserMemoryProgress p = UserMemoryProgress.create(1L, 1L);
        p.unlockSlot(2); // mask = 0b000100 = 4
        int beforeMask = p.getUnlockedMask();

        boolean changed = p.unlockSlot(2);

        assertThat(changed).isFalse();
        assertThat(p.getUnlockedMask()).isEqualTo(beforeMask);
    }

    @Test
    @DisplayName("unlockSlot — 여러 slot 누적 시 OR 결합")
    void unlockSlot_multipleSlots_orsTogether() {
        UserMemoryProgress p = UserMemoryProgress.create(1L, 1L);

        p.unlockSlot(0); // 0b000001
        p.unlockSlot(2); // 0b000100
        p.unlockSlot(5); // 0b100000

        assertThat(p.getUnlockedMask()).isEqualTo(0b100101); // = 37
        assertThat(MemoryProgressState.fromMask(p.getUnlockedMask()))
                .isEqualTo(MemoryProgressState.IN_PROGRESS);
    }

    @Test
    @DisplayName("unlockSlot — 6칸 모두 해금 시 mask=63 (FULL_MASK), state=DONE")
    void unlockSlot_allSlots_reachesFullMask() {
        UserMemoryProgress p = UserMemoryProgress.create(1L, 1L);

        for (int i = 0; i < UserMemoryProgress.SLOT_COUNT; i++) {
            p.unlockSlot(i);
        }

        assertThat(p.getUnlockedMask()).isEqualTo(UserMemoryProgress.FULL_MASK).isEqualTo(63);
        assertThat(MemoryProgressState.fromMask(p.getUnlockedMask()))
                .isEqualTo(MemoryProgressState.DONE);
    }

    @ParameterizedTest
    @ValueSource(ints = {-1, 6, 7, 100})
    @DisplayName("unlockSlot — 0~5 범위 밖 slot 인덱스는 IllegalArgumentException")
    void unlockSlot_invalidIndex_throws(int invalidIndex) {
        UserMemoryProgress p = UserMemoryProgress.create(1L, 1L);

        assertThatThrownBy(() -> p.unlockSlot(invalidIndex))
                .isInstanceOf(IllegalArgumentException.class);
    }
}
