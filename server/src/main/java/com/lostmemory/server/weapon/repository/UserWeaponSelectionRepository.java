package com.lostmemory.server.weapon.repository;

import com.lostmemory.server.weapon.entity.UserWeaponSelection;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

@Repository
public interface UserWeaponSelectionRepository extends JpaRepository<UserWeaponSelection, Long> {
    // PK = user_id 이므로 findById(userId) 가 곧 findByUserId
}
