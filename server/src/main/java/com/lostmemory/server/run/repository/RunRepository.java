package com.lostmemory.server.run.repository;

import com.lostmemory.server.run.entity.Run;
import com.lostmemory.server.run.entity.RunStatus;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

@Repository
public interface RunRepository extends JpaRepository<Run, Long> {

    /** 같은 세션에 진행 중인 런이 이미 있는지 — 새 런 시작 시 중복 방지 */
    boolean existsBySessionIdAndStatus(Long sessionId, RunStatus status);
}
