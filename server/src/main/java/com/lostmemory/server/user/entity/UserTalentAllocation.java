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
 * 유저별 재능 포인트 분배. user_id 가 PK 이자 FK (sharedPK with users).
 *
 * 정책: 백엔드는 5개 영역별 투자 포인트 값만 저장/조회. 총량 / 잔여 포인트는 관리 X.
 *   기획상 기억 액자 해금 보너스 등으로 재능 총량이 늘어날 수 있는데, 보너스 이벤트가 백엔드로
 *   들어오지 않는 현 설계에선 백엔드 측 total_point 가 stale 해질 수 있어 검증 자체를 포기.
 *   total_point 컬럼은 schema NOT NULL 제약 때문에 의미 없는 0 으로 유지 (legacy).
 *
 * updated_at 은 PostgreSQL 트리거가 자동 갱신 — JPA 는 read-only 매핑.
 */
@Entity
@Table(name = "user_talent_allocations")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class UserTalentAllocation {

    @Id
    @Column(name = "user_id")
    private Long userId;

    @OneToOne(fetch = FetchType.LAZY, optional = false)
    @MapsId
    @JoinColumn(name = "user_id")
    @OnDelete(action = OnDeleteAction.CASCADE)
    private User user;

    @Column(name = "total_point", nullable = false)
    private Integer totalPoint;

    @Column(name = "crit_rate_points", nullable = false)
    private Integer critRatePoints;

    @Column(name = "attack_speed_points", nullable = false)
    private Integer attackSpeedPoints;

    @Column(name = "defense_points", nullable = false)
    private Integer defensePoints;

    @Column(name = "mana_regen_points", nullable = false)
    private Integer manaRegenPoints;

    @Column(name = "max_hp_points", nullable = false)
    private Integer maxHpPoints;

    @Column(name = "updated_at", insertable = false, updatable = false)
    private Instant updatedAt;

    private UserTalentAllocation(User user) {
        this.user = user;
        this.totalPoint = 0;        // legacy no-op
        this.critRatePoints = 0;
        this.attackSpeedPoints = 0;
        this.defensePoints = 0;
        this.manaRegenPoints = 0;
        this.maxHpPoints = 0;
    }

    /** 신규 row — 회원가입 시 5개 slot 모두 0 으로 생성. */
    public static UserTalentAllocation create(User user) {
        return new UserTalentAllocation(user);
    }

    /**
     * 분배 통째로 교체. 모든 값은 음수 X (validation 은 호출자 책임 — @Min(0)).
     */
    public void replaceAllocation(int critRate, int attackSpeed, int defense, int manaRegen, int maxHp) {
        this.critRatePoints = critRate;
        this.attackSpeedPoints = attackSpeed;
        this.defensePoints = defense;
        this.manaRegenPoints = manaRegen;
        this.maxHpPoints = maxHp;
    }
}
