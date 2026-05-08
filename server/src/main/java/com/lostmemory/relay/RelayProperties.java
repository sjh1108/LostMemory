package com.lostmemory.relay;

import org.springframework.boot.context.properties.ConfigurationProperties;

/**
 * Relay 설정.
 * - port: UDP listen 포트. 외부 노출 (인프라 Security Group 에서 UDP 허용 필요)
 * - handshakeTimeoutMs: 핸드셰이크 미완 peer 가 보낸 데이터 패킷 drop 처리용 (현재 미사용 — 향후)
 * - peerIdleTimeoutMs: 등록된 peer 가 이 시간 이상 패킷 안 보내면 dead 처리 (BYE 누락 fallback)
 * - cleanupIntervalMs: cleanup 스케줄러 실행 주기. peerIdleTimeoutMs 보다 충분히 짧게.
 */
@ConfigurationProperties(prefix = "relay")
public record RelayProperties(
        int port,
        long handshakeTimeoutMs,
        long peerIdleTimeoutMs,
        long cleanupIntervalMs
) {
}
