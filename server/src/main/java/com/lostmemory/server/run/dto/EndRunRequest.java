package com.lostmemory.server.run.dto;

import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotNull;

/**
 * 런 종료 요청. MVP 3 필드 받음 — 클라 RunResultData 에 직접 매핑되는 값만.
 *   - durationSeconds: 플레이 타임 (Time.time 누적)
 *   - chapterReached: 도달 챕터 = BossKillCount (1챕터 = 1보스 정책. 클라 합의 결과)
 *   - memoryShardsEarned: 획득 파편 총량 (모든 멤버에게 동일 양 적립)
 *
 * `result` (clear/death/surrender) 는 클라가 안 보내고 백엔드가 chapterReached 로 추론:
 *   chapterReached > 0 → clear, 0 → death. surrender 는 추후 신호 들어오면 분기 추가.
 */
public record EndRunRequest(
        @NotNull @Min(0) Integer durationSeconds,
        @NotNull @Min(0) Integer chapterReached,
        @NotNull @Min(0) Integer memoryShardsEarned
) {
}
