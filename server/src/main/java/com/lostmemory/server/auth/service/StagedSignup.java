package com.lostmemory.server.auth.service;

/**
 * 회원가입 정보 임시 보관 — 가입 요청 시 Redis 에 저장되어 이메일 인증 통과 시점에 꺼내 DB 에 INSERT 한다.
 *
 * Redis 키: auth:signup:staging:{email}, TTL 30분.
 * 이 시간 안에 인증을 마치지 못하면 자동 만료 — 사용자가 도중에 이탈해도 DB 에 쓰레기 row 가 남지 않는다.
 *
 * passwordHash 는 BCrypt 결과를 보관 (평문 보관 금지).
 */
public record StagedSignup(
        String loginId,
        String passwordHash,
        String nickname
) {
}
