package com.lostmemory.server.user.service;

import com.lostmemory.server.global.exception.BusinessException;
import com.lostmemory.server.global.exception.ErrorCode;
import com.lostmemory.server.user.dto.UserCurrencyResponse;
import com.lostmemory.server.user.dto.UserRecordResponse;
import com.lostmemory.server.user.dto.UserResponse;
import com.lostmemory.server.user.repository.UserCurrencyRepository;
import com.lostmemory.server.user.repository.UserRecordRepository;
import com.lostmemory.server.user.repository.UserRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class UserService {

    private final UserRepository userRepository;
    private final UserCurrencyRepository userCurrencyRepository;
    private final UserRecordRepository userRecordRepository;

    /**
     * 내 정보 조회. 토큰 검증을 통과해 진입한 userId 가 DB 에 없는 희소 케이스(삭제된 계정 등)는 USER_NOT_FOUND.
     */
    public UserResponse getMyInfo(Long userId) {
        return userRepository.findById(userId)
                .map(UserResponse::from)
                .orElseThrow(() -> new BusinessException(ErrorCode.USER_NOT_FOUND));
    }

    /**
     * 본인 재화 조회. row 없으면 (신규 유저 또는 첫 런 전) memory_shards 0 으로 응답.
     * row 자동 생성은 첫 런 종료 시 RunService 가 처리.
     */
    public UserCurrencyResponse getMyCurrency(Long userId) {
        return userCurrencyRepository.findById(userId)
                .map(UserCurrencyResponse::from)
                .orElseGet(() -> UserCurrencyResponse.defaultFor(userId));
    }

    /**
     * 본인 최고 전적 조회. row 없으면 (첫 런 전) cleared_chapter/stage 모두 0.
     */
    public UserRecordResponse getMyRecord(Long userId) {
        return userRecordRepository.findByUserId(userId)
                .map(UserRecordResponse::from)
                .orElseGet(() -> UserRecordResponse.defaultFor(userId));
    }
}
