package com.lostmemory.server.user.controller;

import com.lostmemory.server.global.response.ApiResponse;
import com.lostmemory.server.user.dto.UserResponse;
import com.lostmemory.server.user.service.UserService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.tags.Tag;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@Tag(name = "User", description = "유저 정보")
@RestController
@RequestMapping("/users")
@RequiredArgsConstructor
public class UserController {

    private final UserService userService;

    @Operation(summary = "내 정보 조회 (Bearer 토큰의 userId 기반)")
    @GetMapping("/me")
    public ApiResponse<UserResponse> getMyInfo(@AuthenticationPrincipal Long userId) {
        return ApiResponse.of(userService.getMyInfo(userId));
    }
}
