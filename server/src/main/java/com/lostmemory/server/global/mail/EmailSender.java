package com.lostmemory.server.global.mail;

/**
 * 메일 발송 추상화. 인증 코드·비밀번호 재설정 등 시스템 메일 발송 진입점.
 * 구현체는 SMTP / mock / 무발송 등으로 교체 가능.
 */
public interface EmailSender {

    /**
     * 단순 텍스트 메일 발송.
     * @param to      수신자 주소
     * @param subject 제목
     * @param text    본문 (plain text)
     */
    void send(String to, String subject, String text);
}
