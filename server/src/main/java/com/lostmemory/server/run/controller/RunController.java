package com.lostmemory.server.run.controller;

import com.lostmemory.server.global.response.ApiResponse;
import com.lostmemory.server.run.dto.EndRunRequest;
import com.lostmemory.server.run.dto.RunDetailResponse;
import com.lostmemory.server.run.dto.RunResponse;
import com.lostmemory.server.run.dto.RunResultResponse;
import com.lostmemory.server.run.dto.StartRunRequest;
import com.lostmemory.server.run.service.RunService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.media.Content;
import io.swagger.v3.oas.annotations.media.ExampleObject;
import io.swagger.v3.oas.annotations.responses.ApiResponses;
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

@Tag(name = "Run", description = "던전 런 — 시작 / 종료 / 결과 조회. 결과 저장 시 모든 멤버에게 파편 적립 + 전적 갱신")
@RestController
@RequestMapping("/runs")
@RequiredArgsConstructor
public class RunController {

    private final RunService runService;

    @Operation(
            summary = "런 시작 (호스트 전용)",
            description = """
                    호스트가 던전 진입 시점에 호출. 다음 처리:
                    - `runs` row 1 개 생성 (session_id, status = IN_PROGRESS)
                    - 해당 세션의 모든 `session_joins` 멤버를 `run_members` 로 일괄 등록
                    - 솔로 / 멀티 동일 흐름 — 솔로도 1인 세션 + 1 RunMember 로 진행

                    제약:
                    - 호스트만 호출 가능 (게스트면 403 RUN_NOT_HOST)
                    - 같은 세션에 이미 진행 중인 런이 있으면 409 RUN_ALREADY_IN_PROGRESS
                    - 세션이 없으면 404 SESSION_NOT_FOUND
                    """
    )
    @ApiResponses({
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "200", description = "런 시작 성공",
                    content = @Content(examples = @ExampleObject(value = """
                            {
                              "success": true,
                              "data": {
                                "runId": 42,
                                "sessionId": 56,
                                "status": "IN_PROGRESS",
                                "startedAt": "2026-05-14T10:50:00Z",
                                "endedAt": null
                              }
                            }
                            """))),
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "403", description = "호스트 아님",
                    content = @Content(examples = @ExampleObject(value = """
                            { "success": false, "error": { "code": "RUN_NOT_HOST", "message": "호스트만 런을 시작/종료할 수 있습니다" } }
                            """))),
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "409", description = "이미 진행 중인 런 있음",
                    content = @Content(examples = @ExampleObject(value = """
                            { "success": false, "error": { "code": "RUN_ALREADY_IN_PROGRESS", "message": "해당 세션에 이미 진행 중인 런이 있습니다" } }
                            """)))
    })
    @PostMapping
    public ApiResponse<RunResponse> startRun(
            @AuthenticationPrincipal Long userId,
            @Valid @RequestBody StartRunRequest request) {
        return ApiResponse.of(runService.startRun(userId, request.sessionId()));
    }

    @Operation(
            summary = "런 종료 (호스트 전용)",
            description = """
                    호스트가 런 종료 시점에 호출. MVP 3 필드만 받음:
                    - `durationSeconds`: 플레이 타임 (Unity 의 Time.time 누적)
                    - `chapterReached`: 도달 챕터 = BossKillCount (1챕터 = 1보스. 0 이면 사망)
                    - `memoryShardsEarned`: 획득 파편 총량 (모든 멤버 동일 적립)

                    백엔드 처리:
                    - `runs.status` = ENDED, `ended_at` 기록
                    - `run_results` row 생성 (result 는 chapterReached 로 추론: >0 → CLEAR, 0 → DEATH)
                    - 멤버별 `user_currencies.memory_shards` UPSERT (race-safe ON CONFLICT 사용)
                    - 멤버별 `user_records` 갱신 (총 런 수, 최고 챕터 등)

                    제약:
                    - 호스트만 호출 가능 (403 RUN_NOT_HOST)
                    - 이미 종료된 런이면 409 RUN_ALREADY_ENDED
                    - 런 없으면 404 RUN_NOT_FOUND
                    """
    )
    @ApiResponses({
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "200", description = "런 종료 + 결과 저장 성공",
                    content = @Content(examples = @ExampleObject(value = """
                            {
                              "success": true,
                              "data": {
                                "runId": 42,
                                "result": "CLEAR",
                                "durationSeconds": 1234,
                                "chapterReached": 1,
                                "memoryShardsEarned": 25,
                                "savedAt": "2026-05-14T11:10:34Z"
                              }
                            }
                            """))),
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "403", description = "호스트 아님",
                    content = @Content(examples = @ExampleObject(value = """
                            { "success": false, "error": { "code": "RUN_NOT_HOST", "message": "호스트만 런을 시작/종료할 수 있습니다" } }
                            """))),
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "409", description = "이미 종료된 런",
                    content = @Content(examples = @ExampleObject(value = """
                            { "success": false, "error": { "code": "RUN_ALREADY_ENDED", "message": "이미 종료된 런입니다" } }
                            """)))
    })
    @PostMapping("/{runId}/end")
    public ApiResponse<RunResultResponse> endRun(
            @AuthenticationPrincipal Long userId,
            @PathVariable Long runId,
            @Valid @RequestBody EndRunRequest request) {
        return ApiResponse.of(runService.endRun(userId, runId, request));
    }

    @Operation(
            summary = "런 단건 조회",
            description = """
                    `runId` 로 런 메타 + 결과 한 번에 조회.
                    - 본인이 RunMember 로 참여한 런만 허용 (그 외 403 RUN_NOT_MEMBER)
                    - 결과 row 없으면 (= 진행 중) `result` 필드 null
                    - 런 없으면 404 RUN_NOT_FOUND
                    """
    )
    @ApiResponses({
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "200", description = "조회 성공 (진행 중 / 종료 둘 다)",
                    content = @Content(examples = @ExampleObject(value = """
                            {
                              "success": true,
                              "data": {
                                "run": {
                                  "runId": 42,
                                  "sessionId": 56,
                                  "status": "ENDED",
                                  "startedAt": "2026-05-14T10:50:00Z",
                                  "endedAt": "2026-05-14T11:10:34Z"
                                },
                                "result": {
                                  "runId": 42,
                                  "result": "CLEAR",
                                  "durationSeconds": 1234,
                                  "chapterReached": 1,
                                  "memoryShardsEarned": 25,
                                  "savedAt": "2026-05-14T11:10:34Z"
                                }
                              }
                            }
                            """))),
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "403", description = "본인이 참여한 런 아님",
                    content = @Content(examples = @ExampleObject(value = """
                            { "success": false, "error": { "code": "RUN_NOT_MEMBER", "message": "본인이 참여한 런만 조회할 수 있습니다" } }
                            """)))
    })
    @GetMapping("/{runId}")
    public ApiResponse<RunDetailResponse> getRun(
            @AuthenticationPrincipal Long userId,
            @PathVariable Long runId) {
        return ApiResponse.of(runService.getRun(userId, runId));
    }
}
