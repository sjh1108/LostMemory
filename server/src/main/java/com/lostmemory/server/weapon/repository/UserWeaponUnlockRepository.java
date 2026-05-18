package com.lostmemory.server.weapon.repository;

import com.lostmemory.server.weapon.entity.UserWeaponUnlock;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.List;

@Repository
public interface UserWeaponUnlockRepository extends JpaRepository<UserWeaponUnlock, Long> {

    /** 본인이 해금한 모든 무기 노드 — 마을 화면에서 해금 표시용 */
    List<UserWeaponUnlock> findAllByUserId(Long userId);
}
