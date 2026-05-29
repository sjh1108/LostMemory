package com.lostmemory.server.analytics.entity;

import com.lostmemory.server.user.entity.User;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
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
import org.hibernate.annotations.JdbcTypeCode;
import org.hibernate.annotations.OnDelete;
import org.hibernate.annotations.OnDeleteAction;
import org.hibernate.type.SqlTypes;

import java.time.Instant;
import java.util.UUID;

/**
 * 플레이어 행동 분석 이벤트 (append-only).
 * 한 게임 실행 안에서 발생하는 모든 의미있는 행동 — session_start/end, stage_entered/cleared, player_died 등.
 *
 * payload 는 event_type 별 자유 JSON (예: player_died 면 cause_enemy_id, cause_pattern_id 등).
 */
@Entity
@Table(name = "player_events")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class PlayerEvent {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    @Column(name = "event_id")
    private Long id;

    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "user_id", nullable = false)
    @OnDelete(action = OnDeleteAction.CASCADE)
    private User user;

    @Column(name = "analytics_session_id", nullable = false)
    private UUID analyticsSessionId;

    @Column(name = "event_type", nullable = false, length = 48)
    private EventType eventType;

    @Column(name = "event_time", nullable = false)
    private Instant eventTime;

    @Column(name = "received_time", nullable = false, insertable = false, updatable = false,
            columnDefinition = "TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP")
    private Instant receivedTime;

    @Column(name = "stage_id", length = 64)
    private String stageId;

    @JdbcTypeCode(SqlTypes.JSON)
    @Column(name = "payload", columnDefinition = "jsonb")
    private String payload;

    @Column(name = "client_version", length = 16)
    private String clientVersion;

    private PlayerEvent(
            User user,
            UUID analyticsSessionId,
            EventType eventType,
            Instant eventTime,
            String stageId,
            String payload,
            String clientVersion
    ) {
        this.user = user;
        this.analyticsSessionId = analyticsSessionId;
        this.eventType = eventType;
        this.eventTime = eventTime;
        this.stageId = stageId;
        this.payload = payload;
        this.clientVersion = clientVersion;
    }

    /**
     * batch 안의 단일 event 를 entity 로 생성.
     *
     * @param user                JWT 에서 추출한 user (클라 payload 의 user_id 는 무시)
     * @param analyticsSessionId  클라 발행 UUID (한 게임 실행 단위)
     * @param eventType           화이트리스트 통과한 event 종류
     * @param eventTime           클라 시계 기준 발생 시점
     * @param stageId             nullable — session_start/end 에선 null 가능
     * @param payload             직렬화된 JSON 문자열
     * @param clientVersion       클라 빌드 버전
     */
    public static PlayerEvent create(
            User user,
            UUID analyticsSessionId,
            EventType eventType,
            Instant eventTime,
            String stageId,
            String payload,
            String clientVersion
    ) {
        return new PlayerEvent(user, analyticsSessionId, eventType, eventTime, stageId, payload, clientVersion);
    }
}
