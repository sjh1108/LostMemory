package com.lostmemory.server.global.security;

/**
 * Session JWT 에서 추출한 신원 + 권한 정보.
 * 자체 Relay 가 토큰 검증 후 in-memory routing table 에 매핑할 때 사용.
 */
public record SessionPrincipal(Long userId, Long sessionId, String role) {
}
