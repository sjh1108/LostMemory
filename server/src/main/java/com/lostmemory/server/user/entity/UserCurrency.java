package com.lostmemory.server.user.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.FetchType;
import jakarta.persistence.Id;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.MapsId;
import jakarta.persistence.OneToOne;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;
import org.hibernate.annotations.OnDelete;
import org.hibernate.annotations.OnDeleteAction;

import java.time.Instant;

/**
 * 유저 재화 (기억의 파편). user_id 가 PK 이자 FK (sharedPK with users).
 * updated_at 은 PostgreSQL 트리거가 자동 갱신 — JPA 는 read-only 매핑.
 */
@Entity
@Table(name = "user_currencies")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class UserCurrency {

    @Id
    @Column(name = "user_id")
    private Long userId;

    @OneToOne(fetch = FetchType.LAZY, optional = false)
    @MapsId
    @JoinColumn(name = "user_id")
    @OnDelete(action = OnDeleteAction.CASCADE)
    private User user;

    @Column(name = "memory_shards", nullable = false)
    private Integer memoryShards;

    @Column(name = "updated_at", insertable = false, updatable = false)
    private Instant updatedAt;

    private UserCurrency(User user, Integer memoryShards) {
        this.user = user;
        this.memoryShards = memoryShards;
    }

    /** 신규 row — 회원가입 후 첫 적립 시점에 호출. initialShards 는 보통 런 결과의 earned 양. */
    public static UserCurrency create(User user, Integer initialShards) {
        return new UserCurrency(user, initialShards);
    }

    /** 파편 적립 (양수만 허용). 런 종료 시 RunService 가 호출. */
    public void addShards(int delta) {
        if (delta < 0) {
            throw new IllegalArgumentException("delta must be >= 0");
        }
        this.memoryShards += delta;
    }
}
