package com.lostmemory.server.weapon.service;

import com.lostmemory.server.weapon.dto.UserWeaponUnlockedResponse;
import com.lostmemory.server.weapon.dto.WeaponResponse;
import com.lostmemory.server.weapon.repository.UserWeaponUnlockRepository;
import com.lostmemory.server.weapon.repository.WeaponRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.List;

@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class WeaponService {

    private final WeaponRepository weaponRepository;
    private final UserWeaponUnlockRepository userWeaponUnlockRepository;

    /** 무기 마스터 전체 — display_order 오름차순. 클라가 parent_weapon_id 로 트리 재구성. */
    public List<WeaponResponse> getAllWeapons() {
        return weaponRepository.findAllByOrderByDisplayOrderAsc()
                .stream()
                .map(WeaponResponse::from)
                .toList();
    }

    /** 본인이 해금한 무기 노드 list. 빈 결과면 빈 list 반환. */
    public List<UserWeaponUnlockedResponse> getMyUnlocks(Long userId) {
        return userWeaponUnlockRepository.findAllByUserId(userId)
                .stream()
                .map(UserWeaponUnlockedResponse::from)
                .toList();
    }
}
