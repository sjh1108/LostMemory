package com.lostmemory.server.user.dto;

import com.lostmemory.server.user.entity.UserTalentAllocation;
import io.swagger.v3.oas.annotations.media.Schema;

import java.time.Instant;

/**
 * 본인 재능 분배 응답. row 가 없으면 default (5개 slot 모두 0).
 *
 * 정책: 백엔드는 5개 영역별 투자 포인트만 응답. 총량 / 잔여 포인트는 클라가 관리.
 */
public record UserTalentAllocationResponse(
        @Schema(description = "유저 PK", example = "1")
        Long userId,

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

        @Schema(description = "최종 갱신 시각 (UTC). row 미존재 시 null", example = "2026-05-20T10:00:00Z",
                nullable = true)
        Instant updatedAt
) {
    public static UserTalentAllocationResponse from(UserTalentAllocation entity) {
        return new UserTalentAllocationResponse(
                entity.getUserId(),
                entity.getCritRatePoints(),
                entity.getAttackSpeedPoints(),
                entity.getDefensePoints(),
                entity.getManaRegenPoints(),
                entity.getMaxHpPoints(),
                entity.getUpdatedAt()
        );
    }

    /** row 미존재 시 기본 응답 — 모두 0. */
    public static UserTalentAllocationResponse defaultFor(Long userId) {
        return new UserTalentAllocationResponse(userId, 0, 0, 0, 0, 0, null);
    }
}
