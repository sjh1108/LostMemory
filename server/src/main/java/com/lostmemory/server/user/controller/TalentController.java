package com.lostmemory.server.user.controller;

import com.lostmemory.server.global.response.ApiResponse;
import com.lostmemory.server.user.dto.TalentSaveRequest;
import com.lostmemory.server.user.dto.UserTalentAllocationResponse;
import com.lostmemory.server.user.service.TalentService;
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
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@Tag(name = "Talent", description = "유저 재능 포인트 분배 — 5 영역별 투자 포인트만 송수신 (총량/잔여는 클라 관리)")
@RestController
@RequestMapping("/users/me/talents")
@RequiredArgsConstructor
public class TalentController {

    private final TalentService talentService;

    @Operation(
            summary = "본인 재능 분배 조회",
            description = """
                    `user_talent_allocations` 의 본인 row 에서 5개 영역별 투자 포인트만 반환.
                    row 미존재 시 default (모두 0) 응답.

                    정책: 총량 / 잔여 포인트는 백엔드가 관리하지 않음 — 클라가 보유 정보로 계산.

                    마을 진입 시 1회 호출 — 재능 트리 UI 채우기용.
                    """
    )
    @ApiResponses({
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "200", description = "조회 성공",
                    content = @Content(examples = @ExampleObject(value = """
                            {
                              "success": true,
                              "data": {
                                "userId": 1,
                                "critRatePoints": 1,
                                "attackSpeedPoints": 1,
                                "defensePoints": 1,
                                "manaRegenPoints": 1,
                                "maxHpPoints": 1,
                                "updatedAt": "2026-05-20T10:00:00Z"
                              }
                            }
                            """)))
    })
    @GetMapping
    public ApiResponse<UserTalentAllocationResponse> getMyTalents(@AuthenticationPrincipal Long userId) {
        return ApiResponse.of(talentService.getMyAllocation(userId));
    }

    @Operation(
            summary = "재능 분배 통째로 저장 (장착)",
            description = """
                    유저가 UI 에서 분배를 확정 (장착 버튼) 한 후 통째로 저장.
                    invest 가 아닌 replace — 5개 slot 의 새 값을 한 번에 보냄.

                    정책: 백엔드는 5개 slot 값만 저장. 총량 / 잔여 검증 없음 (클라 책임).
                    각 값은 음수 X (@Min(0)) 만 검증.

                    실패:
                    - 400 COMMON_INVALID_INPUT — validation (음수 등)
                    - 404 USER_NOT_FOUND — 토큰 userId 에 해당 row 없음 (회원가입 보강 미적용 마이그레이션 edge)
                    """
    )
    @ApiResponses({
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "200", description = "저장 성공 — 최신 상태 응답",
                    content = @Content(examples = @ExampleObject(value = """
                            {
                              "success": true,
                              "data": {
                                "userId": 1,
                                "critRatePoints": 2,
                                "attackSpeedPoints": 2,
                                "defensePoints": 0,
                                "manaRegenPoints": 1,
                                "maxHpPoints": 0,
                                "updatedAt": "2026-05-20T10:30:00Z"
                              }
                            }
                            """)))
    })
    @PostMapping("/save")
    public ApiResponse<UserTalentAllocationResponse> save(
            @AuthenticationPrincipal Long userId,
            @Valid @RequestBody TalentSaveRequest request) {
        return ApiResponse.of(talentService.save(userId, request));
    }
}
