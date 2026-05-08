package com.lostmemory.relay;

import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.boot.autoconfigure.data.redis.RedisAutoConfiguration;
import org.springframework.boot.autoconfigure.data.redis.RedisRepositoriesAutoConfiguration;
import org.springframework.boot.autoconfigure.jdbc.DataSourceAutoConfiguration;
import org.springframework.boot.autoconfigure.jdbc.DataSourceTransactionManagerAutoConfiguration;
import org.springframework.boot.autoconfigure.orm.jpa.HibernateJpaAutoConfiguration;
import org.springframework.boot.autoconfigure.security.servlet.SecurityAutoConfiguration;
import org.springframework.boot.builder.SpringApplicationBuilder;
import org.springframework.boot.context.properties.ConfigurationPropertiesScan;

/**
 * 자체 Relay 서버 진입점. Spring Boot 미니 컨텍스트로 띄워 JwtProvider 재사용.
 *
 * 같은 jar 안의 ServerApplication(REST API) 와는 별개의 JVM 프로세스로 실행된다.
 * docker-compose 에서 별도 서비스로 띄움.
 *
 * 실행 예 (로컬):
 *   gradle bootRun --args='--spring.main.sources=com.lostmemory.relay.RelayApplication'
 * 또는 IntelliJ Run Config 에서 main class 를 RelayApplication 으로 지정.
 *
 * 실행 예 (운영):
 *   java -cp app.jar com.lostmemory.relay.RelayApplication
 *
 * 활성:
 *   - 웹 (Tomcat) — actuator/health 노출용. /api/actuator/health 가 컨테이너 healthcheck 진입점.
 *     UDP listener (Netty) 와 별개 thread 로 동작. 외부 노출은 docker-compose ports 에서 차단.
 *
 * 비활성:
 *   - DB / Redis 자동 설정 — Relay 는 외부 자원 의존 없음 (stateless)
 *   - Spring Security — JWT 검증은 RelayHandler 에서 수동 수행. actuator 도 그대로 노출되지만
 *     컨테이너 내부 (backend 네트워크) 한정이라 위험 X.
 */
@SpringBootApplication(
        scanBasePackages = {
                "com.lostmemory.relay",
                "com.lostmemory.server.global.security"
        },
        exclude = {
                DataSourceAutoConfiguration.class,
                HibernateJpaAutoConfiguration.class,
                DataSourceTransactionManagerAutoConfiguration.class,
                RedisAutoConfiguration.class,
                RedisRepositoriesAutoConfiguration.class,
                SecurityAutoConfiguration.class
        }
)
@ConfigurationPropertiesScan(basePackages = {
        "com.lostmemory.relay",
        "com.lostmemory.server.global.security"
})
public class RelayApplication {

    public static void main(String[] args) {
        new SpringApplicationBuilder(RelayApplication.class)
                .run(args);
    }
}
