package com.lostmemory.server.memory.service;

import com.lostmemory.server.global.exception.BusinessException;
import com.lostmemory.server.global.exception.ErrorCode;
import com.lostmemory.server.memory.dto.MemoryProgressView;
import com.lostmemory.server.memory.dto.UnlockSlotRequest;
import com.lostmemory.server.memory.entity.UserMemoryProgress;
import com.lostmemory.server.memory.repository.MemoryFrameRepository;
import com.lostmemory.server.memory.repository.UserMemoryProgressRepository;
import com.lostmemory.server.user.repository.UserCurrencyRepository;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.util.Optional;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.catchThrowableOfType;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyInt;
import static org.mockito.ArgumentMatchers.anyLong;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

@ExtendWith(MockitoExtension.class)
class MemoryServiceTest {

    @Mock private MemoryFrameRepository frameRepository;
    @Mock private UserMemoryProgressRepository progressRepository;
    @Mock private UserCurrencyRepository currencyRepository;

    @InjectMocks private MemoryService memoryService;

    private static final Long USER_ID = 1L;
    private static final Long FRAME_ID = 1L;

    @Test
    @DisplayName("unlockSlot — 정상 흐름: frame 존재 + 새 slot + shards 충분 → mask set + 차감 1회")
    void unlockSlot_success() {
        UnlockSlotRequest req = new UnlockSlotRequest(FRAME_ID, 0, 10);
        UserMemoryProgress newProgress = UserMemoryProgress.create(USER_ID, FRAME_ID);

        when(frameRepository.existsById(FRAME_ID)).thenReturn(true);
        when(progressRepository.findByUserIdAndFrameId(USER_ID, FRAME_ID)).thenReturn(Optional.empty());
        when(progressRepository.save(any(UserMemoryProgress.class))).thenReturn(newProgress);
        when(currencyRepository.subtractShards(USER_ID, 10)).thenReturn(1);

        MemoryProgressView view = memoryService.unlockSlot(USER_ID, req);

        assertThat(view.unlockedMask()).isEqualTo(1); // 0b000001
        verify(currencyRepository).subtractShards(USER_ID, 10);
    }

    @Test
    @DisplayName("unlockSlot — frame 없으면 MEMORY_FRAME_NOT_FOUND, 후속 호출 없음")
    void unlockSlot_frameNotFound_throws() {
        UnlockSlotRequest req = new UnlockSlotRequest(99L, 0, 10);

        when(frameRepository.existsById(99L)).thenReturn(false);

        BusinessException ex = catchThrowableOfType(
                () -> memoryService.unlockSlot(USER_ID, req), BusinessException.class);
        assertThat(ex.errorCode()).isEqualTo(ErrorCode.MEMORY_FRAME_NOT_FOUND);

        verify(progressRepository, never()).findByUserIdAndFrameId(anyLong(), anyLong());
        verify(currencyRepository, never()).subtractShards(anyLong(), anyInt());
    }

    @Test
    @DisplayName("unlockSlot — 이미 해금된 slot 재호출 시 idempotent (shards 차감 안 함)")
    void unlockSlot_alreadyUnlocked_isIdempotent() {
        UnlockSlotRequest req = new UnlockSlotRequest(FRAME_ID, 2, 15);
        UserMemoryProgress existing = UserMemoryProgress.create(USER_ID, FRAME_ID);
        existing.unlockSlot(2); // 미리 해금

        when(frameRepository.existsById(FRAME_ID)).thenReturn(true);
        when(progressRepository.findByUserIdAndFrameId(USER_ID, FRAME_ID)).thenReturn(Optional.of(existing));

        MemoryProgressView view = memoryService.unlockSlot(USER_ID, req);

        assertThat(view.unlockedMask()).isEqualTo(0b000100); // mask 변화 없음
        verify(currencyRepository, never()).subtractShards(anyLong(), anyInt());
    }

    @Test
    @DisplayName("unlockSlot — shards 부족 시 MEMORY_SHARDS_INSUFFICIENT (트랜잭션 롤백 의도)")
    void unlockSlot_insufficientShards_throws() {
        UnlockSlotRequest req = new UnlockSlotRequest(FRAME_ID, 0, 1000);
        UserMemoryProgress newProgress = UserMemoryProgress.create(USER_ID, FRAME_ID);

        when(frameRepository.existsById(FRAME_ID)).thenReturn(true);
        when(progressRepository.findByUserIdAndFrameId(USER_ID, FRAME_ID)).thenReturn(Optional.empty());
        when(progressRepository.save(any(UserMemoryProgress.class))).thenReturn(newProgress);
        when(currencyRepository.subtractShards(USER_ID, 1000)).thenReturn(0); // 0 row affected = 부족

        BusinessException ex = catchThrowableOfType(
                () -> memoryService.unlockSlot(USER_ID, req), BusinessException.class);
        assertThat(ex.errorCode()).isEqualTo(ErrorCode.MEMORY_SHARDS_INSUFFICIENT);
    }

    @Test
    @DisplayName("unlockSlot — slot 인덱스 범위 밖이면 MEMORY_SLOT_INDEX_OUT_OF_RANGE")
    void unlockSlot_invalidSlotIndex_throws() {
        UnlockSlotRequest req = new UnlockSlotRequest(FRAME_ID, 99, 10);
        UserMemoryProgress newProgress = UserMemoryProgress.create(USER_ID, FRAME_ID);

        when(frameRepository.existsById(FRAME_ID)).thenReturn(true);
        when(progressRepository.findByUserIdAndFrameId(USER_ID, FRAME_ID)).thenReturn(Optional.empty());
        when(progressRepository.save(any(UserMemoryProgress.class))).thenReturn(newProgress);

        BusinessException ex = catchThrowableOfType(
                () -> memoryService.unlockSlot(USER_ID, req), BusinessException.class);
        assertThat(ex.errorCode()).isEqualTo(ErrorCode.MEMORY_SLOT_INDEX_OUT_OF_RANGE);

        verify(currencyRepository, never()).subtractShards(anyLong(), anyInt());
    }
}
