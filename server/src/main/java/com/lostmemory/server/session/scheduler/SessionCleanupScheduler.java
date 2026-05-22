package com.lostmemory.server.session.scheduler;

import com.lostmemory.server.session.repository.SessionRepository;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Component;
import org.springframework.transaction.annotation.Transactional;

import java.time.Duration;
import java.time.Instant;

/**
 * 좀비 세션 정리 스케줄러 (안전망).
 *
 * 배경: 세션 종료는 클라가 DELETE/leave 를 호출해야 정리되는데, 비정상 종료
 *   (앱 크래시 · 네트워크 단절 · refresh 토큰 만료) 시 그 호출이 안 나가면
 *   sessions / session_joins 가 DB 에 영구 잔존 → USER_ALREADY_IN_SESSION 가드로
 *   해당 유저가 새 세션 생성/참가 불가.
 *
 * 1차 방어는 클라의 401 자동 refresh (대부분의 토큰 만료 케이스 해결).
 * 본 스케줄러는 그래도 새는 극단 케이스(앱 사망 등)를 잡는 2차 안전망.
 *
 * 정책:
 *   - 주기: {@code session.cleanup.interval-ms} (기본 5분)
 *   - TTL : {@code session.cleanup.ttl-hours} (기본 2시간) — 생성 후 이 시간 경과한 세션이 대상
 *   - 단, 진행 중(progress) run 이 있으면 = 실제 플레이 중이므로 보존 (장시간 플레이 유저 오판 방지)
 *
 * 정공법(Relay 연결 상태 통지)으로의 전환은 발표 후 별도 작업.
 */
@Slf4j
@Component
@RequiredArgsConstructor
public class SessionCleanupScheduler {

    private final SessionRepository sessionRepository;

    @Value("${session.cleanup.ttl-hours:2}")
    private long ttlHours;

    @Scheduled(fixedDelayString = "${session.cleanup.interval-ms:300000}")
    @Transactional
    public void cleanupStaleSessions() {
        Instant threshold = Instant.now().minus(Duration.ofHours(ttlHours));
        int deleted = sessionRepository.deleteStaleSessions(threshold);
        if (deleted > 0) {
            log.info("[SessionCleanup] 좀비 세션 {}건 정리 (생성 {}시간 경과 + 진행 중 run 없음)",
                    deleted, ttlHours);
        }
    }
}
