package com.lostmemory.server.user.service;

import com.lostmemory.server.global.exception.BusinessException;
import com.lostmemory.server.global.exception.ErrorCode;
import com.lostmemory.server.user.dto.TalentSaveRequest;
import com.lostmemory.server.user.dto.UserTalentAllocationResponse;
import com.lostmemory.server.user.entity.UserTalentAllocation;
import com.lostmemory.server.user.repository.UserTalentAllocationRepository;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.util.Optional;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.catchThrowableOfType;
import static org.mockito.Mockito.when;

@ExtendWith(MockitoExtension.class)
class TalentServiceTest {

    @Mock private UserTalentAllocationRepository talentRepository;

    @InjectMocks private TalentService talentService;

    private static final Long USER_ID = 1L;

    @Test
    @DisplayName("getMyAllocation — row 존재 시 entity 매핑")
    void getMyAllocation_rowExists_returnsMappedResponse() {
        UserTalentAllocation allocation = UserTalentAllocation.create(null, 5);
        allocation.replaceAllocation(1, 1, 1, 1, 1);
        when(talentRepository.findById(USER_ID)).thenReturn(Optional.of(allocation));

        UserTalentAllocationResponse view = talentService.getMyAllocation(USER_ID);

        assertThat(view.totalPoint()).isEqualTo(5);
        assertThat(view.critRatePoints()).isEqualTo(1);
        assertThat(view.remainingPoint()).isZero();
    }

    @Test
    @DisplayName("getMyAllocation — row 미존재 시 default 응답 (모두 0)")
    void getMyAllocation_rowMissing_returnsDefault() {
        when(talentRepository.findById(USER_ID)).thenReturn(Optional.empty());

        UserTalentAllocationResponse view = talentService.getMyAllocation(USER_ID);

        assertThat(view.userId()).isEqualTo(USER_ID);
        assertThat(view.totalPoint()).isZero();
        assertThat(view.remainingPoint()).isZero();
        assertThat(view.updatedAt()).isNull();
    }

    @Test
    @DisplayName("save — sum+remaining == total 정상 흐름 → 분배 교체")
    void save_validSum_replacesAllocation() {
        UserTalentAllocation allocation = UserTalentAllocation.create(null, 5);
        when(talentRepository.findById(USER_ID)).thenReturn(Optional.of(allocation));

        TalentSaveRequest req = new TalentSaveRequest(2, 2, 0, 1, 0, 0);
        UserTalentAllocationResponse view = talentService.save(USER_ID, req);

        assertThat(view.critRatePoints()).isEqualTo(2);
        assertThat(view.attackSpeedPoints()).isEqualTo(2);
        assertThat(view.manaRegenPoints()).isEqualTo(1);
        assertThat(view.remainingPoint()).isZero();
    }

    @Test
    @DisplayName("save — sum+remaining 합이 서버 total 과 불일치 시 TALENT_POINTS_SUM_MISMATCH")
    void save_sumMismatch_throws() {
        UserTalentAllocation allocation = UserTalentAllocation.create(null, 5);
        when(talentRepository.findById(USER_ID)).thenReturn(Optional.of(allocation));

        // sum(2+2+0+1+0)=5 + remaining=2 = 7 ≠ total 5
        TalentSaveRequest req = new TalentSaveRequest(2, 2, 0, 1, 0, 2);

        BusinessException ex = catchThrowableOfType(
                () -> talentService.save(USER_ID, req), BusinessException.class);
        assertThat(ex.errorCode()).isEqualTo(ErrorCode.TALENT_POINTS_SUM_MISMATCH);
    }

    @Test
    @DisplayName("save — row 미존재 시 USER_NOT_FOUND (회원가입 보강 미적용 마이그레이션 edge)")
    void save_rowMissing_throwsUserNotFound() {
        when(talentRepository.findById(USER_ID)).thenReturn(Optional.empty());

        TalentSaveRequest req = new TalentSaveRequest(1, 1, 1, 1, 1, 0);

        BusinessException ex = catchThrowableOfType(
                () -> talentService.save(USER_ID, req), BusinessException.class);
        assertThat(ex.errorCode()).isEqualTo(ErrorCode.USER_NOT_FOUND);
    }
}
