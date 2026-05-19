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
 * total_point: 누적 획득한 재능 포인트 총량. 회원가입 시 5 로 시작.
 *   추후 기획에 따라 프레임 칸 해금 보너스 등으로 증가 가능.
 * 5개 slot 분배 합 + remaining = total_point 항상 성립 (schema CHECK).
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

    private UserTalentAllocation(User user, int totalPoint) {
        this.user = user;
        this.totalPoint = totalPoint;
        this.critRatePoints = 0;
        this.attackSpeedPoints = 0;
        this.defensePoints = 0;
        this.manaRegenPoints = 0;
        this.maxHpPoints = 0;
    }

    /** 신규 row — 회원가입 시 초기 totalPoint 로 생성. 분배는 모두 0. */
    public static UserTalentAllocation create(User user, int initialTotalPoint) {
        return new UserTalentAllocation(user, initialTotalPoint);
    }

    /** 5개 slot 합. */
    public int investedSum() {
        return critRatePoints + attackSpeedPoints + defensePoints + manaRegenPoints + maxHpPoints;
    }

    /** 잔여 = total - sum. */
    public int remainingPoint() {
        return totalPoint - investedSum();
    }

    /**
     * 분배 통째로 교체. 호출자가 server total 과 입력 sum+remaining 일치 검증 후 호출.
     * 모든 값은 음수 X (validation 은 호출자 책임).
     */
    public void replaceAllocation(int critRate, int attackSpeed, int defense, int manaRegen, int maxHp) {
        this.critRatePoints = critRate;
        this.attackSpeedPoints = attackSpeed;
        this.defensePoints = defense;
        this.manaRegenPoints = manaRegen;
        this.maxHpPoints = maxHp;
    }
}
