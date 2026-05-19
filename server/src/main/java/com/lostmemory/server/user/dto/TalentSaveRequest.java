package com.lostmemory.server.user.dto;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotNull;

/**
 * 재능 분배 통째로 저장 요청.
 *
 * 검증 (서비스 측):
 *   - 모든 필드 음수 X (@Min(0))
 *   - 서버 측 total_point == sum(5개) + remainingPoint 엄격 일치 — 위반 시 TALENT_POINTS_SUM_MISMATCH
 *
 * userId 는 Bearer 토큰에서 추출 (body 미포함).
 */
public record TalentSaveRequest(
        @Schema(description = "치명타율 투자 포인트", example = "1")
        @NotNull @Min(0)
        Integer critRatePoints,

        @Schema(description = "공격속도 투자 포인트", example = "1")
        @NotNull @Min(0)
        Integer attackSpeedPoints,

        @Schema(description = "방어력 투자 포인트", example = "1")
        @NotNull @Min(0)
        Integer defensePoints,

        @Schema(description = "마나재생 투자 포인트", example = "1")
        @NotNull @Min(0)
        Integer manaRegenPoints,

        @Schema(description = "최대체력 투자 포인트", example = "1")
        @NotNull @Min(0)
        Integer maxHpPoints,

        @Schema(description = "잔여 포인트", example = "0")
        @NotNull @Min(0)
        Integer remainingPoint
) {
    public int sumAllocations() {
        return critRatePoints + attackSpeedPoints + defensePoints + manaRegenPoints + maxHpPoints;
    }
}
