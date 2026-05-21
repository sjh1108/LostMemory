package com.lostmemory.server.memory.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import jakarta.persistence.UniqueConstraint;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;

/**
 * 유저별 액자 칸 해금 상태. (user_id, frame_id) UNIQUE — 한 유저는 한 프레임에 row 1개.
 *
 * unlockedMask: 6칸 비트마스크 (0~63). bit n=1 이면 slot n 해금.
 *   0  → Locked
 *   63 → Done (0b111111, 6칸 다 해금)
 *   그 외 → In Progress
 *
 * state 는 DB 컬럼이 아니라 unlockedMask 에서 derive (응답 시점 계산).
 */
@Entity
@Table(
        name = "user_memory_progress",
        uniqueConstraints = @UniqueConstraint(name = "uq_user_frame", columnNames = {"user_id", "frame_id"})
)
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class UserMemoryProgress {

    public static final int SLOT_COUNT = 6;
    public static final int FULL_MASK = (1 << SLOT_COUNT) - 1; // 0b111111 = 63

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    @Column(name = "memory_progress_id")
    private Long id;

    @Column(name = "user_id", nullable = false)
    private Long userId;

    @Column(name = "frame_id", nullable = false)
    private Long frameId;

    @Column(name = "unlocked_mask", nullable = false)
    private Integer unlockedMask;

    private UserMemoryProgress(Long userId, Long frameId, Integer unlockedMask) {
        this.userId = userId;
        this.frameId = frameId;
        this.unlockedMask = unlockedMask;
    }

    /** 신규 row — 첫 unlock-slot 호출 시 자동 생성. mask=0 으로 시작. */
    public static UserMemoryProgress create(Long userId, Long frameId) {
        return new UserMemoryProgress(userId, frameId, 0);
    }

    /**
     * slot 해금 (idempotent — 이미 set 된 bit 면 false 반환, 변화 없음).
     *
     * @return true 면 mask 변경됨 (호출자가 shards 차감 진행), false 면 이미 해금 (차감 skip)
     * @throws IllegalArgumentException slotIndex 가 0~5 범위 밖
     */
    public boolean unlockSlot(int slotIndex) {
        if (slotIndex < 0 || slotIndex >= SLOT_COUNT) {
            throw new IllegalArgumentException("slotIndex must be in [0, " + SLOT_COUNT + ")");
        }
        int bit = 1 << slotIndex;
        if ((this.unlockedMask & bit) != 0) {
            return false; // 이미 해금
        }
        this.unlockedMask |= bit;
        return true;
    }
}
