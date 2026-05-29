package com.lostmemory.server.llm.service;

import com.lostmemory.server.global.exception.BusinessException;
import com.lostmemory.server.global.exception.ErrorCode;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatCode;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

/**
 * LlmRateLimitService unit test — 외부 의존 X 라 단순 단위.
 * window 만료/리셋 검증은 Clock 추상화 필요 (별도 후속 작업).
 */
class LlmRateLimitServiceTest {

    private LlmRateLimitService service;

    @BeforeEach
    void setUp() {
        service = new LlmRateLimitService();
    }

    @Test
    @DisplayName("첫 호출은 항상 통과한다")
    void firstCall_passes() {
        assertThatCode(() -> service.checkOrThrow("user-1"))
                .doesNotThrowAnyException();
    }

    @Test
    @DisplayName("MAX_REQUESTS_PER_WINDOW 까지는 통과한다")
    void uptoMax_passes() {
        for (int i = 0; i < LlmRateLimitService.MAX_REQUESTS_PER_WINDOW; i++) {
            int callIndex = i;
            assertThatCode(() -> service.checkOrThrow("user-1"))
                    .as("call %d should pass", callIndex)
                    .doesNotThrowAnyException();
        }
    }

    @Test
    @DisplayName("MAX_REQUESTS_PER_WINDOW + 1 번째 호출은 BusinessException(LLM_UPSTREAM_RATE_LIMIT) 던진다")
    void overMax_throwsRateLimit() {
        for (int i = 0; i < LlmRateLimitService.MAX_REQUESTS_PER_WINDOW; i++) {
            service.checkOrThrow("user-1");
        }
        assertThatThrownBy(() -> service.checkOrThrow("user-1"))
                .isInstanceOf(BusinessException.class)
                .extracting(ex -> ((BusinessException) ex).errorCode())
                .isEqualTo(ErrorCode.LLM_UPSTREAM_RATE_LIMIT);
    }

    @Test
    @DisplayName("다른 userId 의 카운트는 독립적이다 (한쪽이 한도 초과해도 다른쪽 영향 없음)")
    void differentUsers_isolated() {
        for (int i = 0; i < LlmRateLimitService.MAX_REQUESTS_PER_WINDOW + 5; i++) {
            try {
                service.checkOrThrow("user-1");
            } catch (BusinessException ignored) {
                // user-1 은 한도 초과 — 무시
            }
        }
        assertThatCode(() -> service.checkOrThrow("user-2"))
                .doesNotThrowAnyException();
        assertThat(service.trackedUserCount()).isEqualTo(2);
    }

    @Test
    @DisplayName("trackedUserCount 가 신규 호출마다 증가한다")
    void trackedUserCount_growsWithNewUsers() {
        assertThat(service.trackedUserCount()).isZero();
        service.checkOrThrow("user-1");
        assertThat(service.trackedUserCount()).isEqualTo(1);
        service.checkOrThrow("user-2");
        assertThat(service.trackedUserCount()).isEqualTo(2);
        service.checkOrThrow("user-1");
        assertThat(service.trackedUserCount()).isEqualTo(2);
    }
}
