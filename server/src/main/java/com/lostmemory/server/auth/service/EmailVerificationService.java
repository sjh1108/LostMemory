package com.lostmemory.server.auth.service;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lostmemory.server.global.exception.BusinessException;
import com.lostmemory.server.global.exception.ErrorCode;
import com.lostmemory.server.global.mail.EmailSender;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.stereotype.Service;

import java.security.SecureRandom;
import java.time.Duration;
import java.util.Optional;

/**
 * 이메일 인증 코드의 생성·발송·검증 + 회원가입 staging 보관.
 *
 * Redis 키 스키마:
 *   auth:email:verify:code:{email}             — 6자리 숫자 코드, TTL 5분
 *   auth:email:verify:attempts:{email}         — 시도 카운터, code 와 동일 TTL
 *   auth:email:verify:resend:cooldown:{email}  — 재발송 쿨다운, TTL 60초
 *   auth:email:verify:resend:daily:{email}     — 일일 발송 카운터, TTL 24시간
 *   auth:signup:staging:{email}                — 가입 정보 JSON, TTL 30분
 *
 * 정책:
 *   - 코드 TTL 5분, 시도 5회 초과 시 코드 무효화 + 재요청 강제
 *   - 재발송 쿨다운 60초 + 일일 5회 한도
 *   - 가입 staging TTL 30분 — 이 안에 인증 통과 못하면 가입 정보 자동 폐기
 */
@Slf4j
@Service
@RequiredArgsConstructor
public class EmailVerificationService {

    private final StringRedisTemplate redis;
    private final EmailSender emailSender;
    private final ObjectMapper objectMapper;

    private static final Duration CODE_TTL          = Duration.ofMinutes(5);
    private static final Duration STAGING_TTL       = Duration.ofMinutes(30);
    private static final Duration RESEND_COOLDOWN   = Duration.ofSeconds(60);
    private static final Duration DAILY_LIMIT_TTL   = Duration.ofDays(1);
    private static final int      MAX_DAILY_SENDS   = 5;
    private static final int      MAX_ATTEMPTS      = 5;

    private static final SecureRandom RNG = new SecureRandom();

    private static String codeKey(String email)     { return "auth:email:verify:code:" + email; }
    private static String attemptsKey(String email) { return "auth:email:verify:attempts:" + email; }
    private static String cooldownKey(String email) { return "auth:email:verify:resend:cooldown:" + email; }
    private static String dailyKey(String email)    { return "auth:email:verify:resend:daily:" + email; }
    private static String stagingKey(String email)  { return "auth:signup:staging:" + email; }

    /**
     * 회원가입 staging — 입력 정보(StagedSignup)를 Redis 에 저장하고 인증 코드를 생성·발송한다.
     *
     * 보안 — 기존 staging 이 다른 loginId/nickname 으로 들어있으면 hijack 시도로 간주하고 **조용히 무시**한다.
     *   응답은 정상 흐름과 동일해 외부에 staging 존재 여부를 누설하지 않는다.
     *   같은 (loginId, nickname) 의 정상 재시도는 그대로 덮어쓰며 TTL 도 재시작.
     *
     * SMTP 실패 시 cooldown / code / staging Redis 키를 보상 삭제해 사용자가 메일도 못 받고 cooldown 에
     * 묶이는 상황을 피한다. (daily 카운터는 +1 그대로 — 일일 한도 5회 중 1회 차감되는 정도는 수용.)
     */
    public void sendCodeForSignup(String email, StagedSignup staged) {
        Optional<StagedSignup> existing = readStagedSignup(email);
        if (existing.isPresent()) {
            StagedSignup prev = existing.get();
            if (!prev.loginId().equals(staged.loginId()) || !prev.nickname().equals(staged.nickname())) {
                log.warn("[Signup] Staging identity mismatch — silently ignored to prevent hijack. email={}", email);
                return;
            }
        }

        enforceResendPolicy(email);

        String code = generateCode();
        redis.opsForValue().set(codeKey(email), code, CODE_TTL);
        redis.opsForValue().set(stagingKey(email), serialize(staged), STAGING_TTL);
        redis.delete(attemptsKey(email));
        redis.opsForValue().set(cooldownKey(email), "1", RESEND_COOLDOWN);
        incrementDailyCounter(email);

        try {
            emailSender.send(
                    email,
                    "[LostMemory] 가입 인증 코드",
                    "인증 코드: " + code + "\n5분 이내에 입력해주세요.\n\n본인이 요청하지 않았다면 이 메일을 무시해주세요."
            );
        } catch (RuntimeException e) {
            redis.delete(codeKey(email));
            redis.delete(stagingKey(email));
            redis.delete(cooldownKey(email));
            throw e;
        }
    }

    /** staging 조회 — verify 통과 시점에 호출자가 사용 (DB 에 user INSERT). 만료/미존재면 빈 Optional. */
    public Optional<StagedSignup> readStagedSignup(String email) {
        String json = redis.opsForValue().get(stagingKey(email));
        if (json == null) return Optional.empty();
        try {
            return Optional.of(objectMapper.readValue(json, StagedSignup.class));
        } catch (JsonProcessingException e) {
            log.warn("StagedSignup deserialization failed for email={}", email, e);
            return Optional.empty();
        }
    }

    /** verify 성공 후 staging 삭제 — 호출자(AuthService)가 user INSERT 직후 호출. */
    public void clearStagedSignup(String email) {
        redis.delete(stagingKey(email));
    }

    /**
     * 코드 검증. 성공 시 코드/시도 카운터를 삭제 (staging 은 호출자가 별도로 지운다 — DB INSERT 후).
     * 실패 시 시도 카운터 증가, MAX_ATTEMPTS 초과 시 코드·staging 모두 무효화.
     */
    public void verifyCode(String email, String submitted) {
        String stored = redis.opsForValue().get(codeKey(email));
        if (stored == null) {
            throw new BusinessException(ErrorCode.AUTH_VERIFICATION_CODE_EXPIRED);
        }
        if (!stored.equals(submitted)) {
            Long attempts = redis.opsForValue().increment(attemptsKey(email));
            if (attempts != null && attempts == 1L) {
                redis.expire(attemptsKey(email), CODE_TTL);
            }
            if (attempts != null && attempts >= MAX_ATTEMPTS) {
                redis.delete(codeKey(email));
                redis.delete(attemptsKey(email));
                redis.delete(stagingKey(email));   // staging 도 같이 무효화 — 가입 다시 진행 강제
                throw new BusinessException(ErrorCode.AUTH_VERIFICATION_ATTEMPTS_EXCEEDED);
            }
            throw new BusinessException(ErrorCode.AUTH_VERIFICATION_CODE_INVALID);
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

    private String serialize(StagedSignup staged) {
        try {
            return objectMapper.writeValueAsString(staged);
        } catch (JsonProcessingException e) {
            throw new IllegalStateException("StagedSignup serialization failed", e);
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
