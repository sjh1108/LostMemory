package com.lostmemory.server.memory.service;

import com.lostmemory.server.global.exception.BusinessException;
import com.lostmemory.server.global.exception.ErrorCode;
import com.lostmemory.server.memory.dto.MemoryFrameView;
import com.lostmemory.server.memory.dto.MemoryProgressSummaryView;
import com.lostmemory.server.memory.dto.MemoryProgressView;
import com.lostmemory.server.memory.dto.UnlockSlotRequest;
import com.lostmemory.server.memory.entity.MemoryFrame;
import com.lostmemory.server.memory.entity.UserMemoryProgress;
import com.lostmemory.server.memory.repository.MemoryFrameRepository;
import com.lostmemory.server.memory.repository.UserMemoryProgressRepository;
import com.lostmemory.server.user.entity.UserCurrency;
import com.lostmemory.server.user.repository.UserCurrencyRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.List;
import java.util.Map;
import java.util.stream.Collectors;

@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class MemoryService {

    private final MemoryFrameRepository frameRepository;
    private final UserMemoryProgressRepository progressRepository;
    private final UserCurrencyRepository currencyRepository;

    /** 액자 마스터 전체 (display_order ASC). */
    public List<MemoryFrameView> getAllFrames() {
        return frameRepository.findAllByOrderByDisplayOrderAsc().stream()
                .map(MemoryFrameView::from)
                .toList();
    }

    /**
     * 본인 메모리 종합 — 보유 파편 + 모든 프레임 진행도.
     * 진행도 row 없는 프레임은 mask=0 + Locked 으로 채움.
     */
    public MemoryProgressSummaryView getProgressSummary(Long userId) {
        int shards = currencyRepository.findById(userId)
                .map(UserCurrency::getMemoryShards)
                .orElse(0);

        List<MemoryFrame> frames = frameRepository.findAllByOrderByDisplayOrderAsc();
        Map<Long, UserMemoryProgress> progressByFrameId = progressRepository.findByUserId(userId).stream()
                .collect(Collectors.toMap(UserMemoryProgress::getFrameId, p -> p));

        List<MemoryProgressView> views = frames.stream()
                .map(frame -> {
                    UserMemoryProgress p = progressByFrameId.get(frame.getId());
                    return p != null ? MemoryProgressView.from(p) : MemoryProgressView.lockedFor(frame.getId());
                })
                .toList();

        return new MemoryProgressSummaryView(shards, views);
    }

    /**
     * 프레임 내 slot 해금. 한 트랜잭션 안에서:
     *   1. frame 존재 확인
     *   2. shards 차감 (atomic UPDATE, 부족 시 4xx)
     *   3. progress row lookup / 신규 생성 + unlockSlot (mask OR)
     *
     * 이미 해금된 slot 인 경우 — shards 차감하지 않고 idempotent 응답.
     *
     * @throws BusinessException MEMORY_FRAME_NOT_FOUND / MEMORY_SLOT_INDEX_OUT_OF_RANGE / MEMORY_SHARDS_INSUFFICIENT
     */
    @Transactional
    public MemoryProgressView unlockSlot(Long userId, UnlockSlotRequest req) {
        // 1. frame 존재 확인
        if (!frameRepository.existsById(req.frameId())) {
            throw new BusinessException(ErrorCode.MEMORY_FRAME_NOT_FOUND);
        }

        // 2. progress row lookup / 신규 생성
        UserMemoryProgress progress = progressRepository.findByUserIdAndFrameId(userId, req.frameId())
                .orElseGet(() -> progressRepository.save(UserMemoryProgress.create(userId, req.frameId())));

        // 3. slot 해금 시도 — idempotent (이미 해금된 slot 이면 shards 차감 skip)
        boolean changed;
        try {
            changed = progress.unlockSlot(req.slotIndex());
        } catch (IllegalArgumentException e) {
            throw new BusinessException(ErrorCode.MEMORY_SLOT_INDEX_OUT_OF_RANGE);
        }

        if (!changed) {
            // 이미 해금된 slot — 응답만 (mask 변화 없음)
            return MemoryProgressView.from(progress);
        }

        // 4. shards 차감 (atomic UPDATE — 보유량 부족 시 0 row affected)
        int updated = currencyRepository.subtractShards(userId, req.consumedShards());
        if (updated == 0) {
            // 보유량 부족 — 트랜잭션 롤백으로 progress 도 원복
            throw new BusinessException(ErrorCode.MEMORY_SHARDS_INSUFFICIENT);
        }

        return MemoryProgressView.from(progress);
    }
}
