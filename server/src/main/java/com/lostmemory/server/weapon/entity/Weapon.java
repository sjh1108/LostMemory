package com.lostmemory.server.weapon.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;

/**
 * 무기 마스터. parent_weapon_id 로 강화 트리를 구성한다 (루트면 NULL).
 *
 * 트리 navigation 은 클라이언트 책임 — 백엔드는 평탄한 list 만 반환하고 클라가 parent_weapon_id 로 재구성.
 */
@Entity
@Table(name = "weapons")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class Weapon {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    @Column(name = "weapon_id")
    private Long id;

    @Column(name = "weapon_name", nullable = false, length = 100)
    private String weaponName;

    @Column(name = "weapon_type", nullable = false, length = 50)
    private String weaponType;

    /** 상위(트리) 무기 ID. 루트면 null */
    @Column(name = "parent_weapon_id")
    private Long parentWeaponId;

    @Column(name = "cost_memory_shards", nullable = false)
    private Integer costMemoryShards;

    @Column(name = "display_order", nullable = false)
    private Integer displayOrder;
}
