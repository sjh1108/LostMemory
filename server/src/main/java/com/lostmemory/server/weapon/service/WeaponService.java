package com.lostmemory.server.weapon.service;

import com.lostmemory.server.global.exception.BusinessException;
import com.lostmemory.server.global.exception.ErrorCode;
import com.lostmemory.server.user.entity.User;
import com.lostmemory.server.user.repository.UserCurrencyRepository;
import com.lostmemory.server.user.repository.UserRepository;
import com.lostmemory.server.weapon.dto.UserWeaponUnlockedResponse;
import com.lostmemory.server.weapon.dto.WeaponInventoryResponse;
import com.lostmemory.server.weapon.dto.WeaponResponse;
import com.lostmemory.server.weapon.dto.WeaponSelectRequest;
import com.lostmemory.server.weapon.dto.WeaponSelectionResponse;
import com.lostmemory.server.weapon.dto.WeaponUnlockRequest;
import com.lostmemory.server.weapon.entity.UserWeaponSelection;
import com.lostmemory.server.weapon.entity.UserWeaponUnlock;
import com.lostmemory.server.weapon.entity.Weapon;
import com.lostmemory.server.weapon.repository.UserWeaponSelectionRepository;
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
    private final UserWeaponSelectionRepository userWeaponSelectionRepository;
    private final UserCurrencyRepository userCurrencyRepository;
    private final UserRepository userRepository;

    /** 무기 마스터 전체 — display_order 오름차순. 클라가 parent_weapon_id 로 트리 재구성. */
    public List<WeaponResponse> getAllWeapons() {
        return weaponRepository.findAllByOrderByDisplayOrderAsc()
                .stream()
                .map(WeaponResponse::from)
                .toList();
    }

    /**
     * 본인 무기 인벤토리 — 해금 목록 + 장착 무기.
     * row 미존재 (마이그레이션 edge) 시 selectedWeaponId=1 (검) default.
     */
    public WeaponInventoryResponse getMyInventory(Long userId) {
        List<UserWeaponUnlockedResponse> unlocks = userWeaponUnlockRepository.findAllByUserId(userId)
                .stream()
                .map(UserWeaponUnlockedResponse::from)
                .toList();
        Long selectedWeaponId = userWeaponSelectionRepository.findById(userId)
                .map(UserWeaponSelection::getSelectedWeaponId)
                .orElse(1L); // 마이그레이션 edge — 회원가입 보강 적용된 정상 흐름엔 발생 X
        return new WeaponInventoryResponse(selectedWeaponId, unlocks);
    }

    /**
     * 무기 해금 + 파편 차감. 한 트랜잭션:
     *   1. weapon 존재 확인
     *   2. 이미 해금 → idempotent 반환 (차감 없음)
     *   3. parent 선행 해금 검증 (parent IS NOT NULL 이면)
     *   4. shards atomic 차감 (보유량 부족 시 409)
     *   5. user_weapon_unlocks insert
     */
    @Transactional
    public UserWeaponUnlockedResponse unlockWeapon(Long userId, WeaponUnlockRequest req) {
        Weapon weapon = weaponRepository.findById(req.weaponId())
                .orElseThrow(() -> new BusinessException(ErrorCode.WEAPON_NOT_FOUND));

        // 이미 해금 — idempotent
        if (userWeaponUnlockRepository.existsByUserIdAndUnlockNodeId(userId, weapon.getId())) {
            return userWeaponUnlockRepository.findAllByUserId(userId).stream()
                    .filter(u -> u.getUnlockNodeId().equals(weapon.getId()))
                    .findFirst()
                    .map(UserWeaponUnlockedResponse::from)
                    .orElseThrow(() -> new BusinessException(ErrorCode.COMMON_INTERNAL_ERROR));
        }

        // parent 선행 해금 검증
        if (weapon.getParentWeaponId() != null
                && !userWeaponUnlockRepository.existsByUserIdAndUnlockNodeId(userId, weapon.getParentWeaponId())) {
            throw new BusinessException(ErrorCode.WEAPON_PARENT_NOT_UNLOCKED);
        }

        // shards atomic 차감 — 부족 시 0 row affected
        int updated = userCurrencyRepository.subtractShards(userId, req.consumedShards());
        if (updated == 0) {
            throw new BusinessException(ErrorCode.WEAPON_SHARDS_INSUFFICIENT);
        }

        // unlock 저장
        User user = userRepository.findById(userId)
                .orElseThrow(() -> new BusinessException(ErrorCode.USER_NOT_FOUND));
        UserWeaponUnlock saved = userWeaponUnlockRepository.save(UserWeaponUnlock.of(user, weapon.getId()));
        return UserWeaponUnlockedResponse.from(saved);
    }

    /**
     * 장착 무기 갱신. 한 트랜잭션:
     *   1. weapon 존재 확인
     *   2. 본인 해금 여부 확인
     *   3. user_weapon_selection 갱신 (row 미존재 시 신규 — 마이그레이션 edge 대응)
     */
    @Transactional
    public WeaponSelectionResponse selectWeapon(Long userId, WeaponSelectRequest req) {
        if (!weaponRepository.existsById(req.weaponId())) {
            throw new BusinessException(ErrorCode.WEAPON_NOT_FOUND);
        }

        if (!userWeaponUnlockRepository.existsByUserIdAndUnlockNodeId(userId, req.weaponId())) {
            throw new BusinessException(ErrorCode.WEAPON_NOT_UNLOCKED);
        }

        UserWeaponSelection selection = userWeaponSelectionRepository.findById(userId)
                .orElseGet(() -> {
                    User user = userRepository.findById(userId)
                            .orElseThrow(() -> new BusinessException(ErrorCode.USER_NOT_FOUND));
                    return userWeaponSelectionRepository.save(UserWeaponSelection.create(user, req.weaponId()));
                });
        selection.changeWeapon(req.weaponId());
        return WeaponSelectionResponse.from(selection);
    }
}
