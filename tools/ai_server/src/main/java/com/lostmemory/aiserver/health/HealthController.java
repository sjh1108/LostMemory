package com.lostmemory.aiserver.health;

import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RestController;

import com.lostmemory.aiserver.common.response.ApiResponse;

@RestController
public class HealthController {

    private final HealthService healthService;

    public HealthController(HealthService healthService) {
        this.healthService = healthService;
    }

    @GetMapping("/health")
    public ResponseEntity<ApiResponse<HealthStatusResponse>> health() {
        return ResponseEntity.ok(ApiResponse.success(healthService.currentStatus()));
    }
}
