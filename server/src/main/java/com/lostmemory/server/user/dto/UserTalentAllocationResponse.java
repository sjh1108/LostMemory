package com.lostmemory.server.user.dto;

import com.lostmemory.server.user.entity.UserTalentAllocation;
import io.swagger.v3.oas.annotations.media.Schema;

import java.time.Instant;

/**
 * 본인 재능 분배 응답. row 가 없으면 default (totalPoint=0, 분배 모두 0).
 * remainingPoint 는 entity 의 derive (total - sum).
 */
public record UserTalentAllocationResponse(
        @Schema(description = "유저 PK", example = "1")
        Long userId,

        @Schema(description = "누적 획득한 재능 포인트 총량", example = "5")
        Integer totalPoint,

        @Schema(description = "치명타율에 투자된 포인트", example = "1")
        Integer critRatePoints,

        @Schema(description = "공격속도에 투자된 포인트", example = "1")
        Integer attackSpeedPoints,

        @Schema(description = "방어력에 투자된 포인트", example = "1")
        Integer defensePoints,

        @Schema(description = "마나재생에 투자된 포인트", example = "1")
        Integer manaRegenPoints,

        @Schema(description = "최대체력에 투자된 포인트", example = "1")
        Integer maxHpPoints,

        @Schema(description = "잔여 포인트 (= totalPoint - 5개 합)", example = "0")
        Integer remainingPoint,

        @Schema(description = "최종 갱신 시각 (UTC). row 미존재 시 null", example = "2026-05-20T10:00:00Z",
                nullable = true)
        Instant updatedAt
) {
    public static UserTalentAllocationResponse from(UserTalentAllocation entity) {
        return new UserTalentAllocationResponse(
                entity.getUserId(),
                entity.getTotalPoint(),
                entity.getCritRatePoints(),
                entity.getAttackSpeedPoints(),
                entity.getDefensePoints(),
                entity.getManaRegenPoints(),
                entity.getMaxHpPoints(),
                entity.remainingPoint(),
                entity.getUpdatedAt()
        );
    }

    /** row 미존재 시 기본 응답 — 모두 0. */
    public static UserTalentAllocationResponse defaultFor(Long userId) {
        return new UserTalentAllocationResponse(userId, 0, 0, 0, 0, 0, 0, 0, null);
    }
}
