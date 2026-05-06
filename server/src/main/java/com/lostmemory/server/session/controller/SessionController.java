package com.lostmemory.server.session.controller;

import com.lostmemory.server.global.response.ApiResponse;
import com.lostmemory.server.session.dto.CreateSessionRequest;
import com.lostmemory.server.session.dto.JoinSessionRequest;
import com.lostmemory.server.session.dto.SessionResponse;
import com.lostmemory.server.session.service.SessionService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@Tag(name = "Session", description = "매칭룸 — 호스트가 만들고 게스트가 코드로 입장")
@RestController
@RequestMapping("/sessions")
@RequiredArgsConstructor
public class SessionController {

    private final SessionService sessionService;

    @Operation(summary = "세션 생성 (호스트)")
    @PostMapping
    public ApiResponse<SessionResponse> createSession(
            @AuthenticationPrincipal Long userId,
            @Valid @RequestBody CreateSessionRequest request) {
        return ApiResponse.of(sessionService.createSession(userId, request));
    }

    @Operation(summary = "세션 참가 (게스트, privateCode 일치 필요)")
    @PostMapping("/{sessionId}/join")
    public ApiResponse<SessionResponse> joinSession(
            @AuthenticationPrincipal Long userId,
            @PathVariable Long sessionId,
            @Valid @RequestBody JoinSessionRequest request) {
        return ApiResponse.of(sessionService.joinSession(userId, sessionId, request));
    }

    @Operation(summary = "세션 종료 (호스트만)")
    @DeleteMapping("/{sessionId}")
    public ApiResponse<Void> deleteSession(
            @AuthenticationPrincipal Long userId,
            @PathVariable Long sessionId) {
        sessionService.deleteSession(userId, sessionId);
        return ApiResponse.ok();
    }
}
