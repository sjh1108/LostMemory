package com.lostmemory.server.memory.repository;

import com.lostmemory.server.memory.entity.UserMemoryProgress;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.Optional;

@Repository
public interface UserMemoryProgressRepository extends JpaRepository<UserMemoryProgress, Long> {

    /** 본인 모든 프레임 진행도 — 응답 조립용. */
    List<UserMemoryProgress> findByUserId(Long userId);

    /** unlock-slot 시 lookup. 없으면 신규 생성. */
    Optional<UserMemoryProgress> findByUserIdAndFrameId(Long userId, Long frameId);
}
