package com.lostmemory.server.run.repository;

import com.lostmemory.server.run.entity.RunResult;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

@Repository
public interface RunResultRepository extends JpaRepository<RunResult, Long> {
    // PK = run_id 이므로 findById(runId) 가 곧 findByRunId
}
