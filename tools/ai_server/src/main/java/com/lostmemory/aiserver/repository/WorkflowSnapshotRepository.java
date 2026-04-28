package com.lostmemory.aiserver.repository;

import java.util.Optional;

import org.springframework.data.jpa.repository.JpaRepository;

import com.lostmemory.aiserver.generation.WorkflowSnapshotEntity;

public interface WorkflowSnapshotRepository extends JpaRepository<WorkflowSnapshotEntity, Long> {

    Optional<WorkflowSnapshotEntity> findByWorkflowHash(String workflowHash);
}
