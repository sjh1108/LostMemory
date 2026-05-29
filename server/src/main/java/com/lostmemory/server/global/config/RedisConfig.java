package com.lostmemory.server.global.config;

import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.data.redis.connection.RedisConnectionFactory;
import org.springframework.data.redis.core.StringRedisTemplate;

/**
 * Redis 클라이언트 빈 명시 노출.
 *
 * spring-boot-starter-data-redis 의 autoconfig 도 StringRedisTemplate 를 노출하지만,
 * 본 프로젝트의 컴포넌트 어노테이션 명시 컨벤션에 맞춰 의존을 가시화하고,
 * 커스텀 serializer (JSON / ObjectMapper 공유 등) 를 추가할 진입점도 보존한다.
 *
 * 연결 host/port/password 는 application.yaml 의 spring.data.redis.* 에서 주입
 * (LettuceConnectionFactory autoconfig).
 */
@Configuration
public class RedisConfig {

    /**
     * 문자열 키/값 전용 RedisTemplate.
     * autoconfig 의 동명 빈은 @ConditionalOnMissingBean 라 본 명시 정의가 우선 적용된다.
     */
    @Bean
    public StringRedisTemplate stringRedisTemplate(RedisConnectionFactory connectionFactory) {
        return new StringRedisTemplate(connectionFactory);
    }
}
