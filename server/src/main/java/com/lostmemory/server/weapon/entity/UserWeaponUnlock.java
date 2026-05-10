package com.lostmemory.server.weapon.entity;

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
import jakarta.persistence.UniqueConstraint;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;
import org.hibernate.annotations.OnDelete;
import org.hibernate.annotations.OnDeleteAction;
import org.springframework.data.annotation.CreatedDate;
import org.springframework.data.jpa.domain.support.AuditingEntityListener;

import java.time.Instant;

/**
 * 유저별 무기 해금 기록. 같은 유저가 같은 무기를 중복 해금하지 못하도록 UNIQUE 제약.
 *
 * 컬럼명 unlock_node_id 는 schema.sql 그대로 따름 — 무기 강화 트리의 노드 단위 해금 의미.
 */
@Entity
@Table(
        name = "user_weapon_unlocks",
        uniqueConstraints = {
                @UniqueConstraint(name = "uq_user_weapon", columnNames = {"user_id", "unlock_node_id"})
        }
)
@EntityListeners(AuditingEntityListener.class)
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class UserWeaponUnlock {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    @Column(name = "user_weapon_unlocked_id")
    private Long id;

    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "user_id", nullable = false)
    @OnDelete(action = OnDeleteAction.CASCADE)
    private User user;

    @Column(name = "unlock_node_id", nullable = false)
    private Long unlockNodeId;

    @CreatedDate
    @Column(name = "unlocked_at", nullable = false, updatable = false)
    private Instant unlockedAt;

    private UserWeaponUnlock(User user, Long unlockNodeId) {
        this.user = user;
        this.unlockNodeId = unlockNodeId;
    }

    /** 신규 해금 row 생성. 호출자가 중복 검증 책임. */
    public static UserWeaponUnlock of(User user, Long unlockNodeId) {
        return new UserWeaponUnlock(user, unlockNodeId);
    }
}
