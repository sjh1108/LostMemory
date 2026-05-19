package com.lostmemory.server.weapon.controller;

import com.lostmemory.server.global.response.ApiResponse;
import com.lostmemory.server.weapon.dto.UserWeaponUnlockedResponse;
import com.lostmemory.server.weapon.dto.WeaponResponse;
import com.lostmemory.server.weapon.service.WeaponService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.media.Content;
import io.swagger.v3.oas.annotations.media.ExampleObject;
import io.swagger.v3.oas.annotations.responses.ApiResponses;
import io.swagger.v3.oas.annotations.tags.Tag;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import java.util.List;

@Tag(name = "Weapon", description = "무기 마스터 데이터 + 본인 해금 상태")
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
                    무기 해금 endpoint 는 별도 (MVP 범위 외).
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
            summary = "본인 무기 해금 정보",
            description = """
                    본인이 해금한 무기 트리 노드 ID 목록. 마을 화면에서 해금 / 미해금 표시용.

                    응답이 비어있으면 신규 유저 / 미해금 상태. 해당 endpoint 는 마스터 데이터 (`/master/weapons`) 와 같이 호출해서 클라가 매칭.
                    """
    )
    @ApiResponses({
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "200", description = "조회 성공",
                    content = @Content(examples = @ExampleObject(value = """
                            {
                              "success": true,
                              "data": [
                                { "unlockNodeId": 1, "unlockedAt": "2026-05-01T10:00:00Z" },
                                { "unlockNodeId": 2, "unlockedAt": "2026-05-10T15:30:00Z" }
                              ]
                            }
                            """)))
    })
    @GetMapping("/users/me/weapons")
    public ApiResponse<List<UserWeaponUnlockedResponse>> getMyUnlocks(
            @AuthenticationPrincipal Long userId) {
        return ApiResponse.of(weaponService.getMyUnlocks(userId));
    }
}
