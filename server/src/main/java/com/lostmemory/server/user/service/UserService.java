package com.lostmemory.server.user.service;

import com.lostmemory.server.global.exception.BusinessException;
import com.lostmemory.server.global.exception.ErrorCode;
import com.lostmemory.server.user.dto.UserResponse;
import com.lostmemory.server.user.repository.UserRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class UserService {

    private final UserRepository userRepository;

    /**
     * 내 정보 조회. 토큰 검증을 통과해 진입한 userId 가 DB 에 없는 희소 케이스(삭제된 계정 등)는 USER_NOT_FOUND.
     */
    public UserResponse getMyInfo(Long userId) {
        return userRepository.findById(userId)
                .map(UserResponse::from)
                .orElseThrow(() -> new BusinessException(ErrorCode.USER_NOT_FOUND));
    }
}
