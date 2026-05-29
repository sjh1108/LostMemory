package com.lostmemory.server.global.config;

import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

import java.time.Clock;

/**
 * 시간 관련 의존성을 주입 가능한 형태로 노출. 테스트에서 시계 조작을 위해 Clock 을 빈으로 둔다.
 */
@Configuration
public class ClockConfig {

    @Bean
    public Clock systemClock() {
        return Clock.systemUTC();
    }
}
