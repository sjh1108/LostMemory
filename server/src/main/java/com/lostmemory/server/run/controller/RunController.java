package com.lostmemory.server.run.controller;

import com.lostmemory.server.global.response.ApiResponse;
import com.lostmemory.server.run.dto.EndRunRequest;
import com.lostmemory.server.run.dto.RunDetailResponse;
import com.lostmemory.server.run.dto.RunResponse;
import com.lostmemory.server.run.dto.RunResultResponse;
import com.lostmemory.server.run.dto.StartRunRequest;
import com.lostmemory.server.run.service.RunService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@Tag(name = "Run", description = "런 시작·종료·결과 조회")
@RestController
@RequestMapping("/runs")
@RequiredArgsConstructor
public class RunController {

    private final RunService runService;

    @Operation(summary = "런 시작 — 호스트만. session_joins 의 모든 멤버를 RunMember 로 등록")
    @PostMapping
    public ApiResponse<RunResponse> startRun(
            @AuthenticationPrincipal Long userId,
            @Valid @RequestBody StartRunRequest request) {
        return ApiResponse.of(runService.startRun(userId, request.sessionId()));
    }

    @Operation(summary = "런 종료 — 호스트만. result/duration/chapter/shards 4 필드 + 멤버별 파편 적립 + 전적 갱신")
    @PostMapping("/{runId}/end")
    public ApiResponse<RunResultResponse> endRun(
            @AuthenticationPrincipal Long userId,
            @PathVariable Long runId,
            @Valid @RequestBody EndRunRequest request) {
        return ApiResponse.of(runService.endRun(userId, runId, request));
    }

    @Operation(summary = "런 단건 조회 — 호스트만 허용. 결과 row 없으면 result 필드 null")
    @GetMapping("/{runId}")
    public ApiResponse<RunDetailResponse> getRun(
            @AuthenticationPrincipal Long userId,
            @PathVariable Long runId) {
        return ApiResponse.of(runService.getRun(userId, runId));
    }
}
