package com.lostmemory.server.user.entity;

/**
 * 계정 상태.
 *  - PENDING   : 가입 직후, 이메일 미인증 — 로그인·서비스 이용 차단
 *  - ACTIVE    : 정상 사용 가능
 *  - SUSPENDED : 운영 차원 정지
 *  - DELETED   : 탈퇴 처리
 *
 * DB 매핑은 UserStatusConverter 가 소문자 문자열로 영속화한다.
 */
public enum UserStatus {
    PENDING,
    ACTIVE,
    SUSPENDED,
    DELETED
}
