package com.lostmemory.server.session.entity;

import com.lostmemory.server.user.entity.User;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EntityListeners;
import jakarta.persistence.FetchType;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;
import org.hibernate.annotations.OnDelete;
import org.hibernate.annotations.OnDeleteAction;
import org.springframework.data.annotation.CreatedDate;
import org.springframework.data.jpa.domain.support.AuditingEntityListener;

import java.time.Instant;

/**
 * 매칭룸 한 단위. private_code 가 null 이 가능하지만 본 MVP 에선 항상 발급해 사용한다.
 * (schema 는 nullable, 애플리케이션 계층에서 강제)
 */
@Entity
@Table(name = "sessions")
@EntityListeners(AuditingEntityListener.class)
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class Session {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    @Column(name = "session_id")
    private Long id;

    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "host_id", nullable = false)
    @OnDelete(action = OnDeleteAction.CASCADE)
    private User host;

    @Column(name = "max_players", nullable = false)
    private Integer maxPlayers;

    @Column(name = "private_code", length = 20)
    private String privateCode;

    @CreatedDate
    @Column(name = "created_at", nullable = false, updatable = false)
    private Instant createdAt;

    private Session(User host, Integer maxPlayers, String privateCode) {
        this.host = host;
        this.maxPlayers = maxPlayers;
        this.privateCode = privateCode;
    }

    /** 신규 세션 생성. host 는 함께 session_joins 에 HOST 로 추가하는 책임은 호출자(SessionService)에 있다. */
    public static Session create(User host, Integer maxPlayers, String privateCode) {
        return new Session(host, maxPlayers, privateCode);
    }
}
