package com.lostmemory.server.memory.entity;

/**
 * 액자 진행 상태. DB 컬럼이 아니라 unlocked_mask 로부터 derive (응답 시점 계산).
 *
 *   mask == 0  → LOCKED
 *   mask == 63 → DONE (0b111111, 6칸 다 해금)
 *   그 외      → IN_PROGRESS
 */
public enum MemoryProgressState {
    LOCKED("Locked"),
    IN_PROGRESS("In Progress"),
    DONE("Done");

    private final String displayValue;

    MemoryProgressState(String displayValue) {
        this.displayValue = displayValue;
    }

    /** API 응답에 노출할 표시값 (frontend 와 합의된 string). */
    public String displayValue() {
        return displayValue;
    }

    /** 비트마스크로부터 derive. */
    public static MemoryProgressState fromMask(int mask) {
        if (mask == 0) return LOCKED;
        if (mask == UserMemoryProgress.FULL_MASK) return DONE;
        return IN_PROGRESS;
    }
}
