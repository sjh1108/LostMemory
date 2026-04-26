package com.lostmemory.aiserver.health;

import java.time.Instant;
import java.util.List;

import org.springframework.core.env.Environment;
import org.springframework.stereotype.Service;

@Service
public class HealthService {

    private final Environment environment;

    public HealthService(Environment environment) {
        this.environment = environment;
    }

    public HealthStatusResponse currentStatus() {
        return new HealthStatusResponse(
                environment.getProperty("spring.application.name", "ai-server"),
                "UP",
                List.of(environment.getActiveProfiles()),
                Instant.now());
    }
}
