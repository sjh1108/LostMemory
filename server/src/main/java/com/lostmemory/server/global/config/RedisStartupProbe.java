package com.lostmemory.server.global.config;

import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.boot.ApplicationArguments;
import org.springframework.boot.ApplicationRunner;
import org.springframework.data.redis.connection.RedisConnection;
import org.springframework.data.redis.connection.RedisConnectionFactory;
import org.springframework.stereotype.Component;

/**
 * 부팅 직후 Redis PING 1회 — 연결 상태를 로그로 가시화.
 * */
@Slf4j
@Component
@RequiredArgsConstructor
public class RedisStartupProbe implements ApplicationRunner {

    private final RedisConnectionFactory connectionFactory;

    @Override
    public void run(ApplicationArguments args) {
        try (RedisConnection conn = connectionFactory.getConnection()) {
            String pong = conn.ping();
            log.info("[Redis] PING → {}", pong);
        } catch (Exception ex) {
            log.warn("[Redis] PING 실패 — 연결 설정/서버 상태 확인 필요. message={}", ex.getMessage());
        }
    }
}
