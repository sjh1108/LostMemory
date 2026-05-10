package com.lostmemory.server.run.dto;

import jakarta.validation.constraints.NotNull;

/**
 * 런 시작 요청. 호스트가 호출.
 * 솔로 모드도 1인 세션 자동 생성 흐름이라 sessionId 항상 필요.
 */
public record StartRunRequest(
        @NotNull(message = "sessionId 는 필수입니다") Long sessionId
) {
}
