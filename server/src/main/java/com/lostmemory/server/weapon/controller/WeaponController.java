package com.lostmemory.server.weapon.controller;

import com.lostmemory.server.global.response.ApiResponse;
import com.lostmemory.server.weapon.dto.UserWeaponUnlockedResponse;
import com.lostmemory.server.weapon.dto.WeaponInventoryResponse;
import com.lostmemory.server.weapon.dto.WeaponResponse;
import com.lostmemory.server.weapon.dto.WeaponSelectRequest;
import com.lostmemory.server.weapon.dto.WeaponSelectionResponse;
import com.lostmemory.server.weapon.dto.WeaponUnlockRequest;
import com.lostmemory.server.weapon.service.WeaponService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.media.Content;
import io.swagger.v3.oas.annotations.media.ExampleObject;
import io.swagger.v3.oas.annotations.responses.ApiResponses;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import java.util.List;

@Tag(name = "Weapon", description = "무기 마스터 데이터 + 본인 해금/장착 상태")
@RestController
@RequiredArgsConstructor
@RequestMapping
public class WeaponController {

    private final WeaponService weaponService;

    @Operation(
            summary = "무기 마스터 전체 조회",
            description = """
                    전체 무기 마스터 데이터 반환. `display_order` 오름차순.

                    트리 구조: `parentWeaponId` 가 null 이면 루트, 값 있으면 해당 weaponId 의 자식.
                    클라는 응답 받은 평면 리스트를 `parentWeaponId` 로 트리 재구성.

                    해금에 필요한 파편 수량(cost)은 클라가 관리. 백엔드는 트리 구조 + 식별자만.
                    """
    )
    @ApiResponses({
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "200", description = "조회 성공",
                    content = @Content(examples = @ExampleObject(value = """
                            {
                              "success": true,
                              "data": [
                                {
                                  "weaponId": 1,
                                  "weaponName": "검",
                                  "weaponType": "Sword",
                                  "parentWeaponId": null,
                                  "displayOrder": 1
                                },
                                {
                                  "weaponId": 2,
                                  "weaponName": "단검",
                                  "weaponType": "Dagger",
                                  "parentWeaponId": 1,
                                  "displayOrder": 2
                                }
                              ]
                            }
                            """)))
    })
    @GetMapping("/master/weapons")
    public ApiResponse<List<WeaponResponse>> getAllWeapons() {
        return ApiResponse.of(weaponService.getAllWeapons());
    }

    @Operation(
            summary = "본인 무기 인벤토리 (해금 목록 + 장착 무기)",
            description = """
                    본인의 해금 노드 목록 + 현재 장착 무기 ID 를 한 번에 반환. 마을 진입 시 1회 호출.

                    `selectedWeaponId`: 다음 런 시작 시 자동 쥐어질 무기. 회원가입 직후 기본값 1 (검).
                    `unlocks`: 본인이 해금한 모든 무기 노드. 회원가입 시 트리 루트 (검/활/스태프, weapon_id 1/3/5) 자동 해금.
                    """
    )
    @ApiResponses({
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "200", description = "조회 성공",
                    content = @Content(examples = @ExampleObject(value = """
                            {
                              "success": true,
                              "data": {
                                "selectedWeaponId": 1,
                                "unlocks": [
                                  { "unlockNodeId": 1, "unlockedAt": "2026-05-01T10:00:00Z" },
                                  { "unlockNodeId": 3, "unlockedAt": "2026-05-01T10:00:00Z" },
                                  { "unlockNodeId": 5, "unlockedAt": "2026-05-01T10:00:00Z" }
                                ]
                              }
                            }
                            """)))
    })
    @GetMapping("/users/me/weapons")
    public ApiResponse<WeaponInventoryResponse> getMyInventory(@AuthenticationPrincipal Long userId) {
        return ApiResponse.of(weaponService.getMyInventory(userId));
    }

    @Operation(
            summary = "무기 해금 (파편 차감)",
            description = """
                    요청 무기를 해금 — 파편을 `consumedShards` 만큼 차감 + `user_weapon_unlocks` insert. 한 트랜잭션.

                    cost (consumedShards) 는 클라가 계산해서 보냄 — 백엔드는 보유량 충분 여부만 검증.

                    이미 해금된 무기인 경우 idempotent: shards 차감 없이 기존 unlockedAt 반환.

                    parent 가 있는 무기 (예: 단검 ← 검) 는 parent 가 본인 해금 상태여야 가능.

                    실패:
                    - 404 WEAPON_NOT_FOUND — weaponId 존재하지 않음
                    - 409 WEAPON_PARENT_NOT_UNLOCKED — 상위 무기 미해금
                    - 409 WEAPON_SHARDS_INSUFFICIENT — 보유 파편 부족
                    """
    )
    @ApiResponses({
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "200", description = "해금 성공 (또는 idempotent — 이미 해금된 무기)",
                    content = @Content(examples = @ExampleObject(value = """
                            {
                              "success": true,
                              "data": {
                                "unlockNodeId": 2,
                                "unlockedAt": "2026-05-20T10:30:00Z"
                              }
                            }
                            """)))
    })
    @PostMapping("/users/me/weapons/unlock")
    public ApiResponse<UserWeaponUnlockedResponse> unlock(
            @AuthenticationPrincipal Long userId,
            @Valid @RequestBody WeaponUnlockRequest request) {
        return ApiResponse.of(weaponService.unlockWeapon(userId, request));
    }

    @Operation(
            summary = "장착 무기 갱신",
            description = """
                    다음 런 시작 시 자동 쥐어질 무기 갱신. 본인이 해금한 무기만 장착 가능.

                    실패:
                    - 404 WEAPON_NOT_FOUND — weaponId 존재하지 않음
                    - 409 WEAPON_NOT_UNLOCKED — 해금되지 않은 무기
                    """
    )
    @ApiResponses({
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "200", description = "갱신 성공",
                    content = @Content(examples = @ExampleObject(value = """
                            {
                              "success": true,
                              "data": {
                                "selectedWeaponId": 2
                              }
                            }
                            """)))
    })
    @PutMapping("/users/me/weapons/selected")
    public ApiResponse<WeaponSelectionResponse> selectWeapon(
            @AuthenticationPrincipal Long userId,
            @Valid @RequestBody WeaponSelectRequest request) {
        return ApiResponse.of(weaponService.selectWeapon(userId, request));
    }
}
