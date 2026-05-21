package com.lostmemory.server.weapon.entity;

import com.lostmemory.server.user.entity.User;
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
 * 유저별 현재 장착 무기. user_id 가 PK 이자 FK (sharedPK with users).
 * 다음 런 시작 시 자동으로 쥐어줄 무기. 회원가입 시 weapon_id=1 (검) 으로 초기화.
 *
 * updated_at 은 PostgreSQL 트리거가 자동 갱신.
 */
@Entity
@Table(name = "user_weapon_selection")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class UserWeaponSelection {

    @Id
    @Column(name = "user_id")
    private Long userId;

    @OneToOne(fetch = FetchType.LAZY, optional = false)
    @MapsId
    @JoinColumn(name = "user_id")
    @OnDelete(action = OnDeleteAction.CASCADE)
    private User user;

    @Column(name = "selected_weapon_id", nullable = false)
    private Long selectedWeaponId;

    @Column(name = "updated_at", insertable = false, updatable = false)
    private Instant updatedAt;

    private UserWeaponSelection(User user, Long selectedWeaponId) {
        this.user = user;
        this.selectedWeaponId = selectedWeaponId;
    }

    /** 신규 row — 회원가입 시 weapon_id=1 (검) 으로 호출. */
    public static UserWeaponSelection create(User user, Long initialWeaponId) {
        return new UserWeaponSelection(user, initialWeaponId);
    }

    /** 장착 무기 갱신. 호출자가 weapon 존재 + 본인 해금 여부 검증 후 호출. */
    public void changeWeapon(Long newWeaponId) {
        this.selectedWeaponId = newWeaponId;
    }
}
