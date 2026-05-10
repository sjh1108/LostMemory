package com.lostmemory.server.weapon.controller;

import com.lostmemory.server.global.response.ApiResponse;
import com.lostmemory.server.weapon.dto.UserWeaponUnlockedResponse;
import com.lostmemory.server.weapon.dto.WeaponResponse;
import com.lostmemory.server.weapon.service.WeaponService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.tags.Tag;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import java.util.List;

@Tag(name = "Weapon", description = "무기 마스터 + 본인 해금 정보")
@RestController
@RequiredArgsConstructor
@RequestMapping
public class WeaponController {

    private final WeaponService weaponService;

    @Operation(summary = "무기 마스터 전체 조회 — display_order 오름차순. parentWeaponId 로 트리 재구성")
    @GetMapping("/master/weapons")
    public ApiResponse<List<WeaponResponse>> getAllWeapons() {
        return ApiResponse.of(weaponService.getAllWeapons());
    }

    @Operation(summary = "본인 무기 해금 정보 — 마을 화면에서 해금 여부 표시")
    @GetMapping("/users/me/weapons")
    public ApiResponse<List<UserWeaponUnlockedResponse>> getMyUnlocks(
            @AuthenticationPrincipal Long userId) {
        return ApiResponse.of(weaponService.getMyUnlocks(userId));
    }
}
