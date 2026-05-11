package com.lostmemory.server.weapon.repository;

import com.lostmemory.server.weapon.entity.Weapon;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.List;

@Repository
public interface WeaponRepository extends JpaRepository<Weapon, Long> {

    /** 무기 마스터 — display_order 오름차순으로 전체 조회. 클라가 트리 재구성에 사용 */
    List<Weapon> findAllByOrderByDisplayOrderAsc();
}
