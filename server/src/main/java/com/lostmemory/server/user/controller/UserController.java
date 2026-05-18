package com.lostmemory.server.user.controller;

import com.lostmemory.server.global.response.ApiResponse;
import com.lostmemory.server.user.dto.UserCurrencyResponse;
import com.lostmemory.server.user.dto.UserRecordResponse;
import com.lostmemory.server.user.dto.UserResponse;
import com.lostmemory.server.user.service.UserService;
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

@Tag(name = "User", description = "유저 본인 정보 / 재화 / 전적 — Bearer 토큰의 userId 기반 조회")
@RestController
@RequestMapping("/users")
@RequiredArgsConstructor
public class UserController {

    private final UserService userService;

    @Operation(
            summary = "내 정보 조회",
            description = """
                    Bearer 토큰에서 추출한 userId 로 본인 정보 조회.
                    `passwordHash` / `updatedAt` 은 응답에서 의도적으로 제외.

                    실패 케이스:
                    - 404 USER_NOT_FOUND — 토큰의 userId 에 해당하는 row 없음 (탈퇴 등)
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
                                "loginId": "testuser",
                                "nickname": "테스터1",
                                "status": "ACTIVE",
                                "createdAt": "2026-04-30T08:00:00Z",
                                "lastLoginAt": "2026-05-14T10:42:42Z"
                              }
                            }
                            """)))
    })
    @GetMapping("/me")
    public ApiResponse<UserResponse> getMyInfo(@AuthenticationPrincipal Long userId) {
        return ApiResponse.of(userService.getMyInfo(userId));
    }

    @Operation(
            summary = "본인 재화 (기억의 파편) 조회",
            description = """
                    `user_currencies.memory_shards` 조회. row 미존재 시 default (0) 응답 — 신규 유저 대응.

                    런 종료 시 적립되며, 본 endpoint 는 마을 / 상점 진입 시 잔액 표시용.
                    """
    )
    @ApiResponses({
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "200", description = "조회 성공 (row 있음)",
                    content = @Content(examples = @ExampleObject(value = """
                            {
                              "success": true,
                              "data": {
                                "userId": 1,
                                "memoryShards": 125,
                                "updatedAt": "2026-05-14T11:10:34Z"
                              }
                            }
                            """)))
    })
    @GetMapping("/me/currency")
    public ApiResponse<UserCurrencyResponse> getMyCurrency(@AuthenticationPrincipal Long userId) {
        return ApiResponse.of(userService.getMyCurrency(userId));
    }

    @Operation(
            summary = "본인 최고 전적 조회",
            description = """
                    `user_records` 의 `cleared_chapter` (최고 도달 챕터) / `cleared_stage` (최고 도달 스테이지) 조회.
                    row 미존재 시 default (0/0) 응답.

                    런 종료 시 백엔드가 `GREATEST` 로 max-update 처리. 본 endpoint 는 마을 / 결과창 표시용.
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
                                "clearedChapter": 1,
                                "clearedStage": 0,
                                "updatedAt": "2026-05-14T11:10:34Z"
                              }
                            }
                            """)))
    })
    @GetMapping("/me/record")
    public ApiResponse<UserRecordResponse> getMyRecord(@AuthenticationPrincipal Long userId) {
        return ApiResponse.of(userService.getMyRecord(userId));
    }
}
