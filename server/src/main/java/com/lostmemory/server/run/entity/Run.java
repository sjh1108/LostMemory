package com.lostmemory.server.run.entity;

import com.lostmemory.server.session.entity.Session;
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
 * 한 판의 플레이 인스턴스. 한 세션 안에서 여러 런 가능 (실패 후 재도전 등).
 */
@Entity
@Table(name = "runs")
@EntityListeners(AuditingEntityListener.class)
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class Run {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    @Column(name = "run_id")
    private Long id;

    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "session_id", nullable = false)
    @OnDelete(action = OnDeleteAction.CASCADE)
    private Session session;

    @Column(nullable = false, length = 20)
    private RunStatus status;

    @CreatedDate
    @Column(name = "started_at", nullable = false, updatable = false)
    private Instant startedAt;

    @Column(name = "ended_at")
    private Instant endedAt;

    private Run(Session session) {
        this.session = session;
        this.status = RunStatus.PROGRESS;
    }

    /** 신규 런 시작 — status=PROGRESS, started_at 자동. */
    public static Run start(Session session) {
        return new Run(session);
    }

    /** 런 종료 처리 — status=END + ended_at=now. RunService 가 호출. */
    public void markEnded() {
        this.status = RunStatus.END;
        this.endedAt = Instant.now();
    }

    public boolean isEnded() {
        return this.status == RunStatus.END;
    }
}
