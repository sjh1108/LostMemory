package com.lostmemory.server.auth.service;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lostmemory.server.global.exception.BusinessException;
import com.lostmemory.server.global.exception.ErrorCode;
import com.lostmemory.server.global.mail.EmailSender;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.Spy;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.data.redis.core.ValueOperations;

import java.time.Duration;
import java.util.Optional;

import static org.assertj.core.api.Assertions.assertThat;
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
 * EmailVerificationService — Redis 흐름 + 회원가입 staging.
 */
@ExtendWith(MockitoExtension.class)
class EmailVerificationServiceTest {

    @Mock private StringRedisTemplate redis;
    @Mock private ValueOperations<String, String> ops;
    @Mock private EmailSender emailSender;
    @Spy  private ObjectMapper objectMapper = new ObjectMapper();

    @InjectMocks private EmailVerificationService service;

    private static final String EMAIL        = "tester@example.com";
    private static final String CODE_KEY     = "auth:email:verify:code:" + EMAIL;
    private static final String ATTEMPTS_KEY = "auth:email:verify:attempts:" + EMAIL;
    private static final String COOLDOWN_KEY = "auth:email:verify:resend:cooldown:" + EMAIL;
    private static final String DAILY_KEY    = "auth:email:verify:resend:daily:" + EMAIL;
    private static final String STAGING_KEY  = "auth:signup:staging:" + EMAIL;

    private static final StagedSignup STAGED = new StagedSignup("testuser", "bcrypt-hash", "테스터1");

    @BeforeEach
    void setUp() {
        lenient().when(redis.opsForValue()).thenReturn(ops);
    }

    @Test
    @DisplayName("sendCodeForSignup — 정상(첫 호출): staging 없음 → 코드 + staging 저장, TTL 5분/30분, 메일 발송")
    void sendCodeForSignup_happyPath_storesCodeAndStagingAndSendsMail() {
        when(ops.get(STAGING_KEY)).thenReturn(null);
        when(redis.hasKey(COOLDOWN_KEY)).thenReturn(false);
        when(ops.get(DAILY_KEY)).thenReturn(null);
        when(ops.increment(DAILY_KEY)).thenReturn(1L);

        service.sendCodeForSignup(EMAIL, STAGED);

        verify(ops).set(eq(CODE_KEY), matches("\\d{6}"), eq(Duration.ofMinutes(5)));
        verify(ops).set(eq(STAGING_KEY), anyString(), eq(Duration.ofMinutes(30)));
        verify(redis).delete(ATTEMPTS_KEY);
        verify(ops).set(COOLDOWN_KEY, "1", Duration.ofSeconds(60));
        verify(redis).expire(DAILY_KEY, Duration.ofDays(1));
        verify(emailSender).send(eq(EMAIL), anyString(), anyString());
    }

    @Test
    @DisplayName("sendCodeForSignup — 동일 identity 재시도: staging 덮어쓰기 + 새 코드 발송")
    void sendCodeForSignup_sameIdentity_overwrites() throws Exception {
        String existingJson = objectMapper.writeValueAsString(STAGED);
        when(ops.get(STAGING_KEY)).thenReturn(existingJson);
        when(redis.hasKey(COOLDOWN_KEY)).thenReturn(false);
        when(ops.get(DAILY_KEY)).thenReturn(null);
        when(ops.increment(DAILY_KEY)).thenReturn(1L);

        service.sendCodeForSignup(EMAIL, STAGED);

        verify(ops).set(eq(STAGING_KEY), anyString(), eq(Duration.ofMinutes(30)));
        verify(emailSender).send(eq(EMAIL), anyString(), anyString());
    }

    @Test
    @DisplayName("sendCodeForSignup — identity 불일치(hijack 시도): 조용히 무시 — Redis/메일 변경 0")
    void sendCodeForSignup_identityMismatch_silentlyIgnored() throws Exception {
        StagedSignup victim = new StagedSignup("victimUser", "victim-hash", "피해자닉");
        String victimJson = objectMapper.writeValueAsString(victim);
        when(ops.get(STAGING_KEY)).thenReturn(victimJson);

        StagedSignup attacker = new StagedSignup("attackerUser", "attacker-hash", "공격자닉");
        service.sendCodeForSignup(EMAIL, attacker);

        verify(ops, never()).set(eq(STAGING_KEY), anyString(), any(Duration.class));
        verify(ops, never()).set(eq(CODE_KEY), anyString(), any(Duration.class));
        verify(emailSender, never()).send(anyString(), anyString(), anyString());
        verify(redis, never()).hasKey(COOLDOWN_KEY);   // enforceResendPolicy 자체에 도달 안 함
    }

    @Test
    @DisplayName("sendCodeForSignup — SMTP 실패: code/staging/cooldown Redis 키 보상 삭제 후 예외 전파")
    void sendCodeForSignup_smtpFails_rollbackRedisState() {
        when(ops.get(STAGING_KEY)).thenReturn(null);
        when(redis.hasKey(COOLDOWN_KEY)).thenReturn(false);
        when(ops.get(DAILY_KEY)).thenReturn(null);
        when(ops.increment(DAILY_KEY)).thenReturn(1L);
        doThrow(new BusinessException(ErrorCode.AUTH_MAIL_DELIVERY_FAILED))
                .when(emailSender).send(eq(EMAIL), anyString(), anyString());

        assertThatThrownBy(() -> service.sendCodeForSignup(EMAIL, STAGED))
                .isInstanceOf(BusinessException.class)
                .extracting(e -> ((BusinessException) e).errorCode())
                .isEqualTo(ErrorCode.AUTH_MAIL_DELIVERY_FAILED);

        // 보상 삭제 검증
        verify(redis).delete(CODE_KEY);
        verify(redis).delete(STAGING_KEY);
        verify(redis).delete(COOLDOWN_KEY);
    }

    @Test
    @DisplayName("sendCodeForSignup — 쿨다운 미경과: SEND_TOO_FREQUENT + 메일/staging 변경 없음")
    void sendCodeForSignup_cooldownActive_throws() {
        when(redis.hasKey(COOLDOWN_KEY)).thenReturn(true);

        assertThatThrownBy(() -> service.sendCodeForSignup(EMAIL, STAGED))
                .extracting(e -> ((BusinessException) e).errorCode())
                .isEqualTo(ErrorCode.AUTH_VERIFICATION_SEND_TOO_FREQUENT);
        verify(emailSender, never()).send(anyString(), anyString(), anyString());
        verify(ops, never()).set(eq(STAGING_KEY), anyString(), any(Duration.class));
    }

    @Test
    @DisplayName("sendCodeForSignup — 일일 한도 초과: SEND_DAILY_LIMIT")
    void sendCodeForSignup_dailyLimitReached_throws() {
        when(ops.get(STAGING_KEY)).thenReturn(null);   // readStagedSignup 통과용
        when(redis.hasKey(COOLDOWN_KEY)).thenReturn(false);
        when(ops.get(DAILY_KEY)).thenReturn("5");

        assertThatThrownBy(() -> service.sendCodeForSignup(EMAIL, STAGED))
                .extracting(e -> ((BusinessException) e).errorCode())
                .isEqualTo(ErrorCode.AUTH_VERIFICATION_SEND_DAILY_LIMIT);
    }

    @Test
    @DisplayName("readStagedSignup — 키 있으면 역직렬화 성공")
    void readStagedSignup_existing_returnsRecord() throws Exception {
        String json = objectMapper.writeValueAsString(STAGED);
        when(ops.get(STAGING_KEY)).thenReturn(json);

        Optional<StagedSignup> read = service.readStagedSignup(EMAIL);

        assertThat(read).isPresent();
        assertThat(read.get().loginId()).isEqualTo("testuser");
        assertThat(read.get().passwordHash()).isEqualTo("bcrypt-hash");
        assertThat(read.get().nickname()).isEqualTo("테스터1");
    }

    @Test
    @DisplayName("readStagedSignup — 키 없으면 empty")
    void readStagedSignup_missing_returnsEmpty() {
        when(ops.get(STAGING_KEY)).thenReturn(null);

        assertThat(service.readStagedSignup(EMAIL)).isEmpty();
    }

    @Test
    @DisplayName("clearStagedSignup — staging 키 삭제")
    void clearStagedSignup_deletesStagingKey() {
        service.clearStagedSignup(EMAIL);

        verify(redis).delete(STAGING_KEY);
    }

    @Test
    @DisplayName("verifyCode — 정확한 코드: code/attempts 삭제 (staging 은 호출자가 별도 처리)")
    void verifyCode_correctCode_clearsCodeAndAttempts() {
        when(ops.get(CODE_KEY)).thenReturn("123456");

        service.verifyCode(EMAIL, "123456");

        verify(redis).delete(CODE_KEY);
        verify(redis).delete(ATTEMPTS_KEY);
        verify(redis, never()).delete(STAGING_KEY);   // verify 단계에선 staging 손대지 않음
    }

    @Test
    @DisplayName("verifyCode — 저장된 코드 없음: CODE_EXPIRED")
    void verifyCode_noStoredCode_throwsExpired() {
        when(ops.get(CODE_KEY)).thenReturn(null);

        assertThatThrownBy(() -> service.verifyCode(EMAIL, "000000"))
                .extracting(e -> ((BusinessException) e).errorCode())
                .isEqualTo(ErrorCode.AUTH_VERIFICATION_CODE_EXPIRED);
    }

    @Test
    @DisplayName("verifyCode — 틀린 코드(1번째): TTL 5분 부여 + CODE_INVALID")
    void verifyCode_firstWrong_incrementsAndSetsTtl() {
        when(ops.get(CODE_KEY)).thenReturn("123456");
        when(ops.increment(ATTEMPTS_KEY)).thenReturn(1L);

        assertThatThrownBy(() -> service.verifyCode(EMAIL, "000000"))
                .extracting(e -> ((BusinessException) e).errorCode())
                .isEqualTo(ErrorCode.AUTH_VERIFICATION_CODE_INVALID);
        verify(redis).expire(ATTEMPTS_KEY, Duration.ofMinutes(5));
        verify(redis, never()).delete(STAGING_KEY);
    }

    @Test
    @DisplayName("verifyCode — 5번째 틀린 시도: code/attempts/staging 전부 무효화 + ATTEMPTS_EXCEEDED")
    void verifyCode_fifthWrong_invalidatesCodeAttemptsAndStaging() {
        when(ops.get(CODE_KEY)).thenReturn("123456");
        when(ops.increment(ATTEMPTS_KEY)).thenReturn(5L);

        assertThatThrownBy(() -> service.verifyCode(EMAIL, "000000"))
                .extracting(e -> ((BusinessException) e).errorCode())
                .isEqualTo(ErrorCode.AUTH_VERIFICATION_ATTEMPTS_EXCEEDED);
        verify(redis).delete(CODE_KEY);
        verify(redis).delete(ATTEMPTS_KEY);
        verify(redis).delete(STAGING_KEY);
    }
}
