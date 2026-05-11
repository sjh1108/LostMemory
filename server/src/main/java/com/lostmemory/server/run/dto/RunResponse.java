package com.lostmemory.server.run.dto;

import com.lostmemory.server.run.entity.Run;
import com.lostmemory.server.run.entity.RunStatus;

import java.time.Instant;

/**
 * 런 정보 응답. 시작 직후 / 진행 중 조회 시 사용. 결과 데이터는 별도 RunResultResponse.
 */
public record RunResponse(
        Long runId,
        Long sessionId,
        RunStatus status,
        Instant startedAt,
        Instant endedAt
) {
    public static RunResponse from(Run run) {
        return new RunResponse(
                run.getId(),
                run.getSession().getId(),
                run.getStatus(),
                run.getStartedAt(),
                run.getEndedAt()
        );
    }
}
