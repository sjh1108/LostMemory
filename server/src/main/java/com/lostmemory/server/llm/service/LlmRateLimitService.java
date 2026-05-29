package com.lostmemory.server.llm.service;

import com.lostmemory.server.global.exception.BusinessException;
import com.lostmemory.server.global.exception.ErrorCode;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Component;

import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.ConcurrentMap;

/**
 * LLM 채팅 API 사용자별 in-memory rate limit (fixed window).
 *
 * 단일 app 컨테이너 + 게임 규모 작아 in-memory 충분 — Redis/AOP 의존성 미사용 결정 (2026-05-23).
 * 스케일 아웃 시 Redis token bucket 도입 검토 (별도 티켓).
 *
 * Fixed window 패턴:
 * <ul>
 *   <li>사용자별 (windowStartMs, count) 보관</li>
 *   <li>새 요청 시 now - windowStart {@code >=} WINDOW_MS 면 새 window 로 덮어쓰기</li>
 *   <li>count {@code >} MAX 면 {@link BusinessException}(LLM_UPSTREAM_RATE_LIMIT) → 503</li>
 *   <li>만료된 window 는 다음 호출 시 자연스럽게 덮어쓰기 — 별도 cleanup 불요</li>
 * </ul>
 */
@Slf4j
@Component
public class LlmRateLimitService {

    /** 사용자별 분당 요청 한도. 운영 관찰 후 조정. */
    static final int MAX_REQUESTS_PER_WINDOW = 10;

    /** Fixed window 길이 (ms). */
    static final long WINDOW_MS = 60_000L;

    private final ConcurrentMap<String, Window> windows = new ConcurrentHashMap<>();

    /**
     * 사용자 카운트 +1 후 한도 초과 시 BusinessException 던짐.
     * GlobalExceptionHandler 가 503 SERVICE_UNAVAILABLE + LLM_UPSTREAM_RATE_LIMIT 코드로 응답.
     */
    public void checkOrThrow(String userId) {
        long now = System.currentTimeMillis();
        Window updated = windows.compute(userId, (id, existing) -> {
            if (existing == null || now - existing.windowStartMs >= WINDOW_MS) {
                return new Window(now, 1);
            }
            return new Window(existing.windowStartMs, existing.count + 1);
        });
        if (updated.count > MAX_REQUESTS_PER_WINDOW) {
            log.warn("[LLM_RATE_LIMIT] userId={} count={} max={}",
                    userId, updated.count, MAX_REQUESTS_PER_WINDOW);
            throw new BusinessException(ErrorCode.LLM_UPSTREAM_RATE_LIMIT);
        }
    }

    /** {@code windows} 의 현재 크기 — 모니터링/테스트용. */
    public int trackedUserCount() {
        return windows.size();
    }

    private record Window(long windowStartMs, int count) {
    }
}
