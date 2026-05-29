package com.lostmemory.server.global.mail;

import com.lostmemory.server.global.exception.BusinessException;
import com.lostmemory.server.global.exception.ErrorCode;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.mail.MailException;
import org.springframework.mail.SimpleMailMessage;
import org.springframework.mail.javamail.JavaMailSender;
import org.springframework.mail.javamail.JavaMailSenderImpl;
import org.springframework.stereotype.Component;

/**
 * Gmail SMTP 기반 메일 발송 구현.
 *
 * 설정: application.yaml 의 spring.mail.* (host=smtp.gmail.com / port=587 / STARTTLS).
 * 인증: 발송 계정의 Gmail "앱 비밀번호" (계정 비번 아님). 환경변수 MAIL_USERNAME / MAIL_PASSWORD 로 주입.
 *
 * From 주소는 JavaMailSenderImpl 에 이미 바인딩된 username 을 그대로 재사용한다 — 별도 @Value 주입을 피해
 * 환경변수 미설정 환경(테스트·로컬)에서도 빈 생성이 실패하지 않도록 한다.
 *
 * 발송 실패는 BusinessException(AUTH_MAIL_DELIVERY_FAILED) 으로 변환해 호출자가 일관된 에러 응답을 내려보내도록 한다.
 */
@Slf4j
@Component
@RequiredArgsConstructor
public class GmailEmailSender implements EmailSender {

    private final JavaMailSender mailSender;

    @Override
    public void send(String to, String subject, String text) {
        SimpleMailMessage message = new SimpleMailMessage();
        String from = resolveFromAddress();
        if (from != null && !from.isBlank()) {
            message.setFrom(from);
        }
        message.setTo(to);
        message.setSubject(subject);
        message.setText(text);
        try {
            mailSender.send(message);
            log.info("[Mail] 발송 OK: to={}, subject={}", to, subject);
        } catch (MailException e) {
            log.warn("[Mail] 발송 실패: to={}, subject={}, msg={}", to, subject, e.getMessage());
            throw new BusinessException(ErrorCode.AUTH_MAIL_DELIVERY_FAILED, e);
        }
    }

    private String resolveFromAddress() {
        if (mailSender instanceof JavaMailSenderImpl impl) {
            return impl.getUsername();
        }
        return null;
    }
}
