package com.lostmemory.server.session.controller;

import com.lostmemory.server.global.response.ApiResponse;
import com.lostmemory.server.session.dto.CreateSessionRequest;
import com.lostmemory.server.session.dto.JoinSessionRequest;
import com.lostmemory.server.session.dto.SessionFindResponse;
import com.lostmemory.server.session.dto.SessionResponse;
import com.lostmemory.server.session.service.SessionService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.media.Content;
import io.swagger.v3.oas.annotations.media.ExampleObject;
import io.swagger.v3.oas.annotations.responses.ApiResponses;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;
import org.springframework.validation.annotation.Validated;

@Tag(name = "Session", description = "매칭룸 — 호스트가 생성, 게스트가 코드로 입장. 자체 Relay 핸드셰이크용 sessionToken 발급")
@RestController
@RequestMapping("/sessions")
@RequiredArgsConstructor
@Validated
public class SessionController {

    private final SessionService sessionService;

    @Operation(
            summary = "세션 생성 (호스트)",
            description = """
                    호스트가 새 매칭룸을 만든다. 다음이 동시 처리됨:
                    - `sessions` row 1 개 생성 (host_id = 호출자, private_code = 입력값)
                    - `session_joins` row 1 개 생성 (role = HOST)
                    - `sessionToken` (단명 JWT) 발급 — 자체 Relay 의 HELLO 핸드셰이크에 사용
                    - `members` 배열에 호스트 본인 1명

                    제약:
                    - 호출자가 이미 활성 세션 (sessions row 존재) 에 참여 중이면 409 USER_ALREADY_IN_SESSION
                    - private_code 가 다른 활성 세션과 중복이면 409 SESSION_PRIVATE_CODE_DUPLICATED
                    """
    )
    @ApiResponses({
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "200", description = "세션 생성 성공",
                    content = @Content(examples = @ExampleObject(value = """
                            {
                              "success": true,
                              "data": {
                                "sessionId": 56,
                                "hostId": 1,
                                "maxPlayers": 2,
                                "privateCode": "GKTYF6",
                                "sessionToken": "eyJhbGciOiJIUzI1NiJ9...",
                                "sessionTokenExpiresIn": 3600,
                                "members": [
                                  { "userId": 1, "nickname": "테스터1", "role": "host", "joinedAt": "2026-05-14T10:42:42Z" }
                                ]
                              }
                            }
                            """))),
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "409", description = "이미 다른 세션에 참여 중 또는 입장 코드 중복",
                    content = @Content(examples = @ExampleObject(value = """
                            {
                              "success": false,
                              "error": { "code": "USER_ALREADY_IN_SESSION", "message": "이미 다른 세션에 참여 중입니다" }
                            }
                            """)))
    })
    @PostMapping
    public ApiResponse<SessionResponse> createSession(
            @AuthenticationPrincipal Long userId,
            @Valid @RequestBody CreateSessionRequest request) {
        return ApiResponse.of(sessionService.createSession(userId, request));
    }

    @Operation(
            summary = "세션 참가 (게스트)",
            description = """
                    게스트가 `sessionId` + `privateCode` 로 세션에 참가.

                    - `session_joins` row 1 개 추가 (role = GUEST)
                    - 호스트와 동일하게 `sessionToken` 발급 — Relay 핸드셰이크용
                    - `members` 배열에 모든 참가자 (호스트 + 기존 게스트 + 본인) 반환

                    제약:
                    - 호출자가 이미 다른 활성 세션 참여 중이면 409 USER_ALREADY_IN_SESSION
                    - `privateCode` 가 path 의 sessionId 와 일치하지 않으면 400 SESSION_INVALID_PRIVATE_CODE
                    - 정원 가득 차면 409 SESSION_FULL
                    - 본인이 이미 해당 세션 참가자면 409 SESSION_ALREADY_JOINED
                    """
    )
    @ApiResponses({
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "200", description = "참가 성공",
                    content = @Content(examples = @ExampleObject(value = """
                            {
                              "success": true,
                              "data": {
                                "sessionId": 56,
                                "hostId": 1,
                                "maxPlayers": 2,
                                "privateCode": "GKTYF6",
                                "sessionToken": "eyJhbGciOiJIUzI1NiJ9...",
                                "sessionTokenExpiresIn": 3600,
                                "members": [
                                  { "userId": 1, "nickname": "테스터1", "role": "host", "joinedAt": "..." },
                                  { "userId": 2, "nickname": "테스터2", "role": "guest", "joinedAt": "..." }
                                ]
                              }
                            }
                            """))),
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "404", description = "세션 없음",
                    content = @Content(examples = @ExampleObject(value = """
                            { "success": false, "error": { "code": "SESSION_NOT_FOUND", "message": "세션을 찾을 수 없습니다" } }
                            """))),
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "409", description = "정원 마감 / 이미 참가 / 다른 세션 참여 중",
                    content = @Content(examples = @ExampleObject(value = """
                            { "success": false, "error": { "code": "SESSION_FULL", "message": "정원이 가득 찼습니다" } }
                            """)))
    })
    @PostMapping("/{sessionId}/join")
    public ApiResponse<SessionResponse> joinSession(
            @AuthenticationPrincipal Long userId,
            @PathVariable Long sessionId,
            @Valid @RequestBody JoinSessionRequest request) {
        return ApiResponse.of(sessionService.joinSession(userId, sessionId, request));
    }

    @Operation(
            summary = "코드로 세션 조회",
            description = """
                    `privateCode` 로 sessionId 만 찾는 입장 전 조회. sessionToken 미발급.

                    응답으로 sessionId / 현재 인원 / 정원 / isFull 만 노출. 클라는 이걸로 입장 가능 여부 확인 후 /sessions/{id}/join 호출.

                    실패 케이스:
                    - 404 SESSION_NOT_FOUND — 코드에 해당하는 세션 없음
                    """
    )
    @ApiResponses({
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "200", description = "조회 성공",
                    content = @Content(examples = @ExampleObject(value = """
                            {
                              "success": true,
                              "data": {
                                "sessionId": 56,
                                "hostId": 1,
                                "maxPlayers": 2,
                                "currentMembers": 1,
                                "isFull": false
                              }
                            }
                            """))),
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "404", description = "세션 없음",
                    content = @Content(examples = @ExampleObject(value = """
                            { "success": false, "error": { "code": "SESSION_NOT_FOUND", "message": "세션을 찾을 수 없습니다" } }
                            """)))
    })
    @GetMapping("/find")
    public ApiResponse<SessionFindResponse> findByCode(
            @AuthenticationPrincipal Long userId,
            @RequestParam("code") @NotBlank String code) {
        return ApiResponse.of(sessionService.findByPrivateCode(code));
    }

    @Operation(
            summary = "세션 종료 (호스트 전용)",
            description = """
                    호스트가 세션을 강제 종료. `sessions` row 삭제 → CASCADE 로 `session_joins` 도 일괄 정리.

                    제약:
                    - 호출자가 호스트 아니면 403 SESSION_NOT_HOST
                    - 세션이 없으면 404 SESSION_NOT_FOUND (멱등하게 무시해도 OK 한 케이스)

                    클라 측 흐름: NetworkManager.Shutdown() 호출 후 본 endpoint 로 backend row 정리.
                    """
    )
    @ApiResponses({
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "200", description = "종료 성공",
                    content = @Content(examples = @ExampleObject(value = """
                            { "success": true }
                            """))),
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "403", description = "호스트 아님",
                    content = @Content(examples = @ExampleObject(value = """
                            { "success": false, "error": { "code": "SESSION_NOT_HOST", "message": "호스트만 수행할 수 있습니다" } }
                            """)))
    })
    @DeleteMapping("/{sessionId}")
    public ApiResponse<Void> deleteSession(
            @AuthenticationPrincipal Long userId,
            @PathVariable Long sessionId) {
        sessionService.deleteSession(userId, sessionId);
        return ApiResponse.ok();
    }

    @Operation(
            summary = "세션 이탈 (게스트 전용)",
            description = """
                    게스트가 자발 이탈. 본인의 `session_joins` row 만 삭제 → 정원 카운트 회복.

                    제약:
                    - 호스트가 호출하면 409 SESSION_HOST_CANNOT_LEAVE (호스트는 /sessions/{id} DELETE 사용)
                    - 본인이 해당 세션 참가자가 아니면 404 또는 무시

                    클라 측 흐름: NetworkManager.Shutdown() 후 본 endpoint 로 본인 row 정리.
                    """
    )
    @ApiResponses({
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "200", description = "이탈 성공",
                    content = @Content(examples = @ExampleObject(value = """
                            { "success": true }
                            """))),
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "409", description = "호스트는 이탈 대신 종료 사용",
                    content = @Content(examples = @ExampleObject(value = """
                            { "success": false, "error": { "code": "SESSION_HOST_CANNOT_LEAVE", "message": "호스트는 세션 종료를 사용해야 합니다" } }
                            """)))
    })
    @PostMapping("/{sessionId}/leave")
    public ApiResponse<Void> leaveSession(
            @AuthenticationPrincipal Long userId,
            @PathVariable Long sessionId) {
        sessionService.leaveSession(userId, sessionId);
        return ApiResponse.ok();
    }
}
