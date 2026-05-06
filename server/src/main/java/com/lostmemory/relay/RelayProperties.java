package com.lostmemory.relay;

import org.springframework.boot.context.properties.ConfigurationProperties;

/**
 * Relay 설정.
 * - port: UDP listen 포트. 외부 노출 (인프라 Security Group 에서 UDP 허용 필요)
 * - handshakeTimeoutMs: 핸드셰이크 미완 peer 가 보낸 데이터 패킷 drop 처리용 (현재 미사용 — 향후)
 */
@ConfigurationProperties(prefix = "relay")
public record RelayProperties(
        int port,
        long handshakeTimeoutMs
) {
}
