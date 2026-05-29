package com.lostmemory.server.auth.service;

import com.lostmemory.server.global.exception.BusinessException;
import com.lostmemory.server.global.exception.ErrorCode;
import com.lostmemory.server.global.mail.EmailSender;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.data.redis.core.ValueOperations;

import java.time.Duration;

import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.ArgumentMatchers.matches;
import static org.mockito.Mockito.doThrow;
import static org.mockito.Mockito.lenient;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

/**
 * PasswordResetService 의 Redis 키 흐름 / 정책 분기 검증.
 * 이메일 인증과 namespace 가 다르고 TTL 이 10분이라는 점이 핵심 차이.
 */
@ExtendWith(MockitoExtension.class)
class PasswordResetServiceTest {

    @Mock private StringRedisTemplate redis;
    @Mock private ValueOperations<String, String> ops;
    @Mock private EmailSender emailSender;

    @InjectMocks private PasswordResetService service;

    private static final String EMAIL         = "tester@example.com";
    private static final String CODE_KEY      = "auth:password:reset:code:" + EMAIL;
    private static final String ATTEMPTS_KEY  = "auth:password:reset:attempts:" + EMAIL;
    private static final String COOLDOWN_KEY  = "auth:password:reset:resend:cooldown:" + EMAIL;
    private static final String DAILY_KEY     = "auth:password:reset:resend:daily:" + EMAIL;

    @BeforeEach
    void setUp() {
        lenient().when(redis.opsForValue()).thenReturn(ops);
    }

    @Test
    @DisplayName("sendCode — 정상: 코드 TTL 10분으로 저장, 쿨다운 60초, 메일 발송")
    void sendCode_happyPath_usesTenMinuteTtl() {
        when(redis.hasKey(COOLDOWN_KEY)).thenReturn(false);
        when(ops.get(DAILY_KEY)).thenReturn(null);
        when(ops.increment(DAILY_KEY)).thenReturn(1L);

        service.sendCode(EMAIL);

        verify(ops).set(eq(CODE_KEY), matches("\\d{6}"), eq(Duration.ofMinutes(10)));
        verify(redis).delete(ATTEMPTS_KEY);
        verify(ops).set(COOLDOWN_KEY, "1", Duration.ofSeconds(60));
        verify(redis).expire(DAILY_KEY, Duration.ofDays(1));
        verify(emailSender).send(eq(EMAIL), anyString(), anyString());
    }

    @Test
    @DisplayName("sendCode — SMTP 실패: code/cooldown 보상 삭제 후 예외 전파")
    void sendCode_smtpFails_rollbackRedisState() {
        when(redis.hasKey(COOLDOWN_KEY)).thenReturn(false);
        when(ops.get(DAILY_KEY)).thenReturn(null);
        when(ops.increment(DAILY_KEY)).thenReturn(1L);
        doThrow(new BusinessException(ErrorCode.AUTH_MAIL_DELIVERY_FAILED))
                .when(emailSender).send(eq(EMAIL), anyString(), anyString());

        assertThatThrownBy(() -> service.sendCode(EMAIL))
                .isInstanceOf(BusinessException.class)
                .extracting(e -> ((BusinessException) e).errorCode())
                .isEqualTo(ErrorCode.AUTH_MAIL_DELIVERY_FAILED);

        verify(redis).delete(CODE_KEY);
        verify(redis).delete(COOLDOWN_KEY);
    }

    @Test
    @DisplayName("sendCode — 쿨다운 미경과: SEND_TOO_FREQUENT")
    void sendCode_cooldownActive_throws() {
        when(redis.hasKey(COOLDOWN_KEY)).thenReturn(true);

        assertThatThrownBy(() -> service.sendCode(EMAIL))
                .isInstanceOf(BusinessException.class)
                .extracting(e -> ((BusinessException) e).errorCode())
                .isEqualTo(ErrorCode.AUTH_VERIFICATION_SEND_TOO_FREQUENT);
        verify(emailSender, never()).send(anyString(), anyString(), anyString());
    }

    @Test
    @DisplayName("sendCode — 일일 한도 초과: SEND_DAILY_LIMIT")
    void sendCode_dailyLimitReached_throws() {
        when(redis.hasKey(COOLDOWN_KEY)).thenReturn(false);
        when(ops.get(DAILY_KEY)).thenReturn("5");

        assertThatThrownBy(() -> service.sendCode(EMAIL))
                .isInstanceOf(BusinessException.class)
                .extracting(e -> ((BusinessException) e).errorCode())
                .isEqualTo(ErrorCode.AUTH_VERIFICATION_SEND_DAILY_LIMIT);
    }

    @Test
    @DisplayName("verifyCode — 정확한 코드: 키 삭제")
    void verifyCode_correctCode_clearsKeys() {
        when(ops.get(CODE_KEY)).thenReturn("123456");

        service.verifyCode(EMAIL, "123456");

        verify(redis).delete(CODE_KEY);
        verify(redis).delete(ATTEMPTS_KEY);
    }

    @Test
    @DisplayName("verifyCode — 저장된 코드 없음: PASSWORD_RESET_CODE_EXPIRED")
    void verifyCode_noStoredCode_throwsExpired() {
        when(ops.get(CODE_KEY)).thenReturn(null);

        assertThatThrownBy(() -> service.verifyCode(EMAIL, "000000"))
                .isInstanceOf(BusinessException.class)
                .extracting(e -> ((BusinessException) e).errorCode())
                .isEqualTo(ErrorCode.AUTH_PASSWORD_RESET_CODE_EXPIRED);
    }

    @Test
    @DisplayName("verifyCode — 틀린 코드(1번째): TTL 10분 부여 + PASSWORD_RESET_CODE_INVALID")
    void verifyCode_firstWrong_setsTtl() {
        when(ops.get(CODE_KEY)).thenReturn("123456");
        when(ops.increment(ATTEMPTS_KEY)).thenReturn(1L);

        assertThatThrownBy(() -> service.verifyCode(EMAIL, "000000"))
                .isInstanceOf(BusinessException.class)
                .extracting(e -> ((BusinessException) e).errorCode())
                .isEqualTo(ErrorCode.AUTH_PASSWORD_RESET_CODE_INVALID);

        verify(redis).expire(ATTEMPTS_KEY, Duration.ofMinutes(10));
    }

    @Test
    @DisplayName("verifyCode — 5번째 틀린 시도: 코드/카운터 무효화 + ATTEMPTS_EXCEEDED (이메일 인증과 공유)")
    void verifyCode_fifthWrong_invalidates() {
        when(ops.get(CODE_KEY)).thenReturn("123456");
        when(ops.increment(ATTEMPTS_KEY)).thenReturn(5L);

        assertThatThrownBy(() -> service.verifyCode(EMAIL, "000000"))
                .isInstanceOf(BusinessException.class)
                .extracting(e -> ((BusinessException) e).errorCode())
                .isEqualTo(ErrorCode.AUTH_VERIFICATION_ATTEMPTS_EXCEEDED);

        verify(redis).delete(CODE_KEY);
        verify(redis).delete(ATTEMPTS_KEY);
    }
}
