package com.lostmemory.server.weapon.dto;

import io.swagger.v3.oas.annotations.media.Schema;

import java.util.List;

/**
 * `GET /users/me/weapons` 통합 응답 — 본인 해금 목록 + 현재 장착 무기.
 * 마을 진입 시 1회 호출로 두 정보 모두 받음.
 */
public record WeaponInventoryResponse(
        @Schema(description = "현재 장착 무기 ID. 회원가입 직후 기본값 1 (검).", example = "1")
        Long selectedWeaponId,

        @Schema(description = "본인 해금 노드 목록")
        List<UserWeaponUnlockedResponse> unlocks
) {
}
