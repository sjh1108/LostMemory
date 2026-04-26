package com.lostmemory.aiserver.health;

import java.time.Instant;
import java.util.List;

public record HealthStatusResponse(
        String service,
        String status,
        List<String> activeProfiles,
        Instant timestamp
) {
}
