package com.lostmemory.server.user.service;

import com.lostmemory.server.global.exception.BusinessException;
import com.lostmemory.server.global.exception.ErrorCode;
import com.lostmemory.server.user.dto.TalentSaveRequest;
import com.lostmemory.server.user.dto.UserTalentAllocationResponse;
import com.lostmemory.server.user.entity.UserTalentAllocation;
import com.lostmemory.server.user.repository.UserTalentAllocationRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class TalentService {

    private final UserTalentAllocationRepository talentRepository;

    /**
     * 본인 재능 분배 조회. row 미존재 시 (회원가입 보강 미적용 마이그레이션 edge) default 응답.
     */
    public UserTalentAllocationResponse getMyAllocation(Long userId) {
        return talentRepository.findById(userId)
                .map(UserTalentAllocationResponse::from)
                .orElseGet(() -> UserTalentAllocationResponse.defaultFor(userId));
    }

    /**
     * 분배 통째로 저장 — "장착" 클릭 시. invest 가 아닌 replace.
     *
     * 정책: 5개 slot 의 투자 포인트만 검증 후 저장. 총량 / 잔여 포인트는 검증하지 않음 (클라가 관리).
     * row 미존재 (마이그레이션 edge) 시 USER_NOT_FOUND — 회원가입 보강이 적용된 정상 흐름에선 발생 X.
     */
    @Transactional
    public UserTalentAllocationResponse save(Long userId, TalentSaveRequest req) {
        UserTalentAllocation allocation = talentRepository.findById(userId)
                .orElseThrow(() -> new BusinessException(ErrorCode.USER_NOT_FOUND));

        allocation.replaceAllocation(
                req.critRatePoints(),
                req.attackSpeedPoints(),
                req.defensePoints(),
                req.manaRegenPoints(),
                req.maxHpPoints()
        );

        return UserTalentAllocationResponse.from(allocation);
    }
}
