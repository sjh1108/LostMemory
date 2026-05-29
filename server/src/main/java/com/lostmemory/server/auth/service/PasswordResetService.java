package com.lostmemory.server.auth.service;

import com.lostmemory.server.global.exception.BusinessException;
import com.lostmemory.server.global.exception.ErrorCode;
import com.lostmemory.server.global.mail.EmailSender;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.stereotype.Service;

import java.security.SecureRandom;
import java.time.Duration;

/**
 * 비밀번호 재설정 코드의 생성·발송·검증.
 *
 * Redis 키 스키마 (이메일 인증과 namespace 분리):
 *   auth:password:reset:code:{email}            — 6자리 숫자 코드, TTL 10분
 *   auth:password:reset:attempts:{email}        — 시도 카운터, code 와 동일 TTL
 *   auth:password:reset:resend:cooldown:{email} — 재발송 쿨다운, TTL 60초
 *   auth:password:reset:resend:daily:{email}    — 일일 발송 카운터, TTL 24시간
 *
 * 정책:
 *   - 코드 TTL 10분, 시도 5회 초과 시 코드 무효화 + 재요청 강제
 *   - 재발송 쿨다운 60초 + 일일 5회 한도
 *   - 시도/쿨다운/일일한도 위반은 이메일 인증과 동일한 ErrorCode 를 공유(AUTH_VERIFICATION_*) —
 *     클라가 두 흐름을 같은 컴포넌트로 처리 가능
 */
@Slf4j
@Service
@RequiredArgsConstructor
public class PasswordResetService {

    private final StringRedisTemplate redis;
    private final EmailSender emailSender;

    private static final Duration CODE_TTL          = Duration.ofMinutes(10);
    private static final Duration RESEND_COOLDOWN   = Duration.ofSeconds(60);
    private static final Duration DAILY_LIMIT_TTL   = Duration.ofDays(1);
    private static final int      MAX_DAILY_SENDS   = 5;
    private static final int      MAX_ATTEMPTS      = 5;

    private static final SecureRandom RNG = new SecureRandom();

    private static String codeKey(String email)     { return "auth:password:reset:code:" + email; }
    private static String attemptsKey(String email) { return "auth:password:reset:attempts:" + email; }
    private static String cooldownKey(String email) { return "auth:password:reset:resend:cooldown:" + email; }
    private static String dailyKey(String email)    { return "auth:password:reset:resend:daily:" + email; }

    /**
     * 신규 재설정 코드 발급 + Redis 저장(TTL) + 메일 발송.
     * 재발송 쿨다운/일일 한도 위반 시 BusinessException 으로 거부.
     *
     * SMTP 실패 시 code / cooldown 키를 보상 삭제해 사용자가 메일도 못 받고 cooldown 에 묶이는 상황을 피한다.
     * (daily 카운터는 +1 그대로 — 일일 한도 5회 중 1회 차감은 수용.)
     */
    public void sendCode(String email) {
        enforceResendPolicy(email);

        String code = generateCode();
        redis.opsForValue().set(codeKey(email), code, CODE_TTL);
        redis.delete(attemptsKey(email));
        redis.opsForValue().set(cooldownKey(email), "1", RESEND_COOLDOWN);
        incrementDailyCounter(email);

        try {
            emailSender.send(
                    email,
                    "[LostMemory] 비밀번호 재설정 코드",
                    "재설정 코드: " + code + "\n10분 이내에 입력해주세요.\n\n요청한 적이 없다면 이 메일을 무시해주세요."
            );
        } catch (RuntimeException e) {
            redis.delete(codeKey(email));
            redis.delete(cooldownKey(email));
            throw e;
        }
    }

    /**
     * 코드 검증. 성공 시 코드/시도 카운터를 삭제. 실패 시 시도 카운터 증가, 5회 초과 시 코드 무효화.
     */
    public void verifyCode(String email, String submitted) {
        String stored = redis.opsForValue().get(codeKey(email));
        if (stored == null) {
            throw new BusinessException(ErrorCode.AUTH_PASSWORD_RESET_CODE_EXPIRED);
        }
        if (!stored.equals(submitted)) {
            Long attempts = redis.opsForValue().increment(attemptsKey(email));
            if (attempts != null && attempts == 1L) {
                redis.expire(attemptsKey(email), CODE_TTL);
            }
            if (attempts != null && attempts >= MAX_ATTEMPTS) {
                redis.delete(codeKey(email));
                redis.delete(attemptsKey(email));
                throw new BusinessException(ErrorCode.AUTH_VERIFICATION_ATTEMPTS_EXCEEDED);
            }
            throw new BusinessException(ErrorCode.AUTH_PASSWORD_RESET_CODE_INVALID);
        }
        redis.delete(codeKey(email));
        redis.delete(attemptsKey(email));
    }

    private void enforceResendPolicy(String email) {
        if (Boolean.TRUE.equals(redis.hasKey(cooldownKey(email)))) {
            throw new BusinessException(ErrorCode.AUTH_VERIFICATION_SEND_TOO_FREQUENT);
        }
        String daily = redis.opsForValue().get(dailyKey(email));
        if (daily != null && parseIntSafe(daily) >= MAX_DAILY_SENDS) {
            throw new BusinessException(ErrorCode.AUTH_VERIFICATION_SEND_DAILY_LIMIT);
        }
    }

    private void incrementDailyCounter(String email) {
        Long count = redis.opsForValue().increment(dailyKey(email));
        if (count != null && count == 1L) {
            redis.expire(dailyKey(email), DAILY_LIMIT_TTL);
        }
    }

    private static String generateCode() {
        int n = RNG.nextInt(1_000_000);
        return String.format("%06d", n);
    }

    private static int parseIntSafe(String s) {
        try {
            return Integer.parseInt(s);
        } catch (NumberFormatException e) {
            return 0;
        }
    }
}
