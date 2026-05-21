package com.lostmemory.relay;

import org.springframework.boot.WebApplicationType;
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
 * 비활성:
 *   - 웹 (Tomcat/MVC) — UDP 만 듣기 때문에 불필요. ServerApplication 의 8080 충돌 회피 + Relay
 *     단독 기동 단순성. 462 인프라의 healthcheck 는 `pgrep -f RelayApplication` 으로 처리.
 *   - DB / Redis 자동 설정 — Relay 는 외부 자원 의존 없음 (stateless)
 *   - Spring Security — JWT 검증은 RelayHandler 에서 수동 수행
 */
@SpringBootApplication(
        scanBasePackages = {
                "com.lostmemory.relay",
                "com.lostmemory.server.global.security",
                // JwtAuthenticationFilter 의 LlmProperties 의존성 — Relay 가 실제 LLM 기능은 안 쓰지만
                // security 패키지의 JwtAuthenticationFilter bean 생성 시 LlmProperties 가 필요해 scan 확장.
                // controller/service 가 아닌 config 패키지만 잡아 LlmController/LlmProxyService 는 제외.
                // lesson reference_relay_udp_infra.md — security 패키지에 새 의존 추가 시 Relay scan 동기화 필수.
                "com.lostmemory.server.llm.config"
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
        "com.lostmemory.server.global.security",
        "com.lostmemory.server.llm.config"
})
public class RelayApplication {

    public static void main(String[] args) {
        new SpringApplicationBuilder(RelayApplication.class)
                .web(WebApplicationType.NONE)
                .run(args);
    }
}
