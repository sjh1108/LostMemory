package com.lostmemory.server.weapon.service;

import com.lostmemory.server.global.exception.BusinessException;
import com.lostmemory.server.global.exception.ErrorCode;
import com.lostmemory.server.user.entity.User;
import com.lostmemory.server.user.repository.UserCurrencyRepository;
import com.lostmemory.server.user.repository.UserRepository;
import com.lostmemory.server.weapon.dto.WeaponInventoryResponse;
import com.lostmemory.server.weapon.dto.WeaponSelectRequest;
import com.lostmemory.server.weapon.dto.WeaponSelectionResponse;
import com.lostmemory.server.weapon.dto.WeaponUnlockRequest;
import com.lostmemory.server.weapon.entity.UserWeaponSelection;
import com.lostmemory.server.weapon.entity.UserWeaponUnlock;
import com.lostmemory.server.weapon.entity.Weapon;
import com.lostmemory.server.weapon.repository.UserWeaponSelectionRepository;
import com.lostmemory.server.weapon.repository.UserWeaponUnlockRepository;
import com.lostmemory.server.weapon.repository.WeaponRepository;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.util.List;
import java.util.Optional;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.catchThrowableOfType;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyInt;
import static org.mockito.ArgumentMatchers.anyLong;
import static org.mockito.Mockito.lenient;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

@ExtendWith(MockitoExtension.class)
class WeaponServiceTest {

    @Mock private WeaponRepository weaponRepository;
    @Mock private UserWeaponUnlockRepository userWeaponUnlockRepository;
    @Mock private UserWeaponSelectionRepository userWeaponSelectionRepository;
    @Mock private UserCurrencyRepository userCurrencyRepository;
    @Mock private UserRepository userRepository;

    @InjectMocks private WeaponService weaponService;

    private static final Long USER_ID = 1L;
    private static final Long ROOT_WEAPON_ID = 1L;   // 검
    private static final Long CHILD_WEAPON_ID = 2L;  // 단검 (parent=검)

    private Weapon mockWeapon(Long id, Long parentId) {
        Weapon weapon = mock(Weapon.class);
        lenient().when(weapon.getId()).thenReturn(id);
        lenient().when(weapon.getParentWeaponId()).thenReturn(parentId);
        return weapon;
    }

    // ===== getMyInventory =====

    @Test
    @DisplayName("getMyInventory — selection 존재 시 selectedWeaponId 반환")
    void getMyInventory_selectionExists() {
        UserWeaponSelection selection = UserWeaponSelection.create(null, CHILD_WEAPON_ID);
        when(userWeaponUnlockRepository.findAllByUserId(USER_ID)).thenReturn(List.of());
        when(userWeaponSelectionRepository.findById(USER_ID)).thenReturn(Optional.of(selection));

        WeaponInventoryResponse view = weaponService.getMyInventory(USER_ID);

        assertThat(view.selectedWeaponId()).isEqualTo(CHILD_WEAPON_ID);
        assertThat(view.unlocks()).isEmpty();
    }

    @Test
    @DisplayName("getMyInventory — selection row 미존재 시 default 1 (검)")
    void getMyInventory_selectionMissing_defaultsToSword() {
        when(userWeaponUnlockRepository.findAllByUserId(USER_ID)).thenReturn(List.of());
        when(userWeaponSelectionRepository.findById(USER_ID)).thenReturn(Optional.empty());

        WeaponInventoryResponse view = weaponService.getMyInventory(USER_ID);

        assertThat(view.selectedWeaponId()).isEqualTo(1L);
    }

    // ===== unlockWeapon =====

    @Test
    @DisplayName("unlockWeapon — root 무기 + shards 충분 → 차감 1회 + insert")
    void unlockWeapon_root_success() {
        Weapon weapon = mockWeapon(ROOT_WEAPON_ID, null);
        WeaponUnlockRequest req = new WeaponUnlockRequest(ROOT_WEAPON_ID, 50);
        UserWeaponUnlock saved = UserWeaponUnlock.of(null, ROOT_WEAPON_ID);

        when(weaponRepository.findById(ROOT_WEAPON_ID)).thenReturn(Optional.of(weapon));
        when(userWeaponUnlockRepository.existsByUserIdAndUnlockNodeId(USER_ID, ROOT_WEAPON_ID))
                .thenReturn(false);
        when(userCurrencyRepository.subtractShards(USER_ID, 50)).thenReturn(1);
        when(userRepository.findById(USER_ID)).thenReturn(Optional.of(mock(User.class)));
        when(userWeaponUnlockRepository.save(any(UserWeaponUnlock.class))).thenReturn(saved);

        weaponService.unlockWeapon(USER_ID, req);

        verify(userCurrencyRepository).subtractShards(USER_ID, 50);
        verify(userWeaponUnlockRepository).save(any(UserWeaponUnlock.class));
    }

    @Test
    @DisplayName("unlockWeapon — 이미 해금된 무기 재호출 시 idempotent (shards 차감 안 함)")
    void unlockWeapon_alreadyUnlocked_isIdempotent() {
        Weapon weapon = mockWeapon(ROOT_WEAPON_ID, null);
        WeaponUnlockRequest req = new WeaponUnlockRequest(ROOT_WEAPON_ID, 50);
        UserWeaponUnlock existing = UserWeaponUnlock.of(null, ROOT_WEAPON_ID);

        when(weaponRepository.findById(ROOT_WEAPON_ID)).thenReturn(Optional.of(weapon));
        when(userWeaponUnlockRepository.existsByUserIdAndUnlockNodeId(USER_ID, ROOT_WEAPON_ID))
                .thenReturn(true);
        when(userWeaponUnlockRepository.findAllByUserId(USER_ID)).thenReturn(List.of(existing));

        weaponService.unlockWeapon(USER_ID, req);

        verify(userCurrencyRepository, never()).subtractShards(anyLong(), anyInt());
        verify(userWeaponUnlockRepository, never()).save(any(UserWeaponUnlock.class));
    }

    @Test
    @DisplayName("unlockWeapon — weaponId 존재 안 함 → WEAPON_NOT_FOUND, 후속 호출 없음")
    void unlockWeapon_weaponNotFound_throws() {
        WeaponUnlockRequest req = new WeaponUnlockRequest(99L, 50);
        when(weaponRepository.findById(99L)).thenReturn(Optional.empty());

        BusinessException ex = catchThrowableOfType(
                () -> weaponService.unlockWeapon(USER_ID, req), BusinessException.class);
        assertThat(ex.errorCode()).isEqualTo(ErrorCode.WEAPON_NOT_FOUND);

        verify(userCurrencyRepository, never()).subtractShards(anyLong(), anyInt());
    }

    @Test
    @DisplayName("unlockWeapon — child 무기 + parent 미해금 → WEAPON_PARENT_NOT_UNLOCKED")
    void unlockWeapon_parentNotUnlocked_throws() {
        Weapon weapon = mockWeapon(CHILD_WEAPON_ID, ROOT_WEAPON_ID);
        WeaponUnlockRequest req = new WeaponUnlockRequest(CHILD_WEAPON_ID, 50);

        when(weaponRepository.findById(CHILD_WEAPON_ID)).thenReturn(Optional.of(weapon));
        when(userWeaponUnlockRepository.existsByUserIdAndUnlockNodeId(USER_ID, CHILD_WEAPON_ID))
                .thenReturn(false);
        when(userWeaponUnlockRepository.existsByUserIdAndUnlockNodeId(USER_ID, ROOT_WEAPON_ID))
                .thenReturn(false); // parent 미해금

        BusinessException ex = catchThrowableOfType(
                () -> weaponService.unlockWeapon(USER_ID, req), BusinessException.class);
        assertThat(ex.errorCode()).isEqualTo(ErrorCode.WEAPON_PARENT_NOT_UNLOCKED);

        verify(userCurrencyRepository, never()).subtractShards(anyLong(), anyInt());
    }

    @Test
    @DisplayName("unlockWeapon — shards 부족 (0 row affected) → WEAPON_SHARDS_INSUFFICIENT")
    void unlockWeapon_insufficientShards_throws() {
        Weapon weapon = mockWeapon(ROOT_WEAPON_ID, null);
        WeaponUnlockRequest req = new WeaponUnlockRequest(ROOT_WEAPON_ID, 1000);

        when(weaponRepository.findById(ROOT_WEAPON_ID)).thenReturn(Optional.of(weapon));
        when(userWeaponUnlockRepository.existsByUserIdAndUnlockNodeId(USER_ID, ROOT_WEAPON_ID))
                .thenReturn(false);
        when(userCurrencyRepository.subtractShards(USER_ID, 1000)).thenReturn(0);

        BusinessException ex = catchThrowableOfType(
                () -> weaponService.unlockWeapon(USER_ID, req), BusinessException.class);
        assertThat(ex.errorCode()).isEqualTo(ErrorCode.WEAPON_SHARDS_INSUFFICIENT);

        verify(userWeaponUnlockRepository, never()).save(any(UserWeaponUnlock.class));
    }

    // ===== selectWeapon =====

    @Test
    @DisplayName("selectWeapon — 해금된 무기로 갱신 → selection.changeWeapon 호출")
    void selectWeapon_success() {
        WeaponSelectRequest req = new WeaponSelectRequest(CHILD_WEAPON_ID);
        UserWeaponSelection selection = UserWeaponSelection.create(null, ROOT_WEAPON_ID);

        when(weaponRepository.existsById(CHILD_WEAPON_ID)).thenReturn(true);
        when(userWeaponUnlockRepository.existsByUserIdAndUnlockNodeId(USER_ID, CHILD_WEAPON_ID))
                .thenReturn(true);
        when(userWeaponSelectionRepository.findById(USER_ID)).thenReturn(Optional.of(selection));

        WeaponSelectionResponse view = weaponService.selectWeapon(USER_ID, req);

        assertThat(view.selectedWeaponId()).isEqualTo(CHILD_WEAPON_ID);
        assertThat(selection.getSelectedWeaponId()).isEqualTo(CHILD_WEAPON_ID);
    }

    @Test
    @DisplayName("selectWeapon — weaponId 존재 안 함 → WEAPON_NOT_FOUND")
    void selectWeapon_weaponNotFound_throws() {
        WeaponSelectRequest req = new WeaponSelectRequest(99L);
        when(weaponRepository.existsById(99L)).thenReturn(false);

        BusinessException ex = catchThrowableOfType(
                () -> weaponService.selectWeapon(USER_ID, req), BusinessException.class);
        assertThat(ex.errorCode()).isEqualTo(ErrorCode.WEAPON_NOT_FOUND);
    }

    @Test
    @DisplayName("selectWeapon — 본인 미해금 무기 → WEAPON_NOT_UNLOCKED")
    void selectWeapon_notUnlocked_throws() {
        WeaponSelectRequest req = new WeaponSelectRequest(CHILD_WEAPON_ID);
        when(weaponRepository.existsById(CHILD_WEAPON_ID)).thenReturn(true);
        when(userWeaponUnlockRepository.existsByUserIdAndUnlockNodeId(USER_ID, CHILD_WEAPON_ID))
                .thenReturn(false);

        BusinessException ex = catchThrowableOfType(
                () -> weaponService.selectWeapon(USER_ID, req), BusinessException.class);
        assertThat(ex.errorCode()).isEqualTo(ErrorCode.WEAPON_NOT_UNLOCKED);
    }
}
