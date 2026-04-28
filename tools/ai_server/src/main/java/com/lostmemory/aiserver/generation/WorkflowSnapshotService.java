package com.lostmemory.aiserver.generation;

import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import com.lostmemory.aiserver.repository.WorkflowSnapshotRepository;

/**
 * workflow snapshot 저장은 "같은 template면 재사용"이 기본이다.
 *
 * hash unique 제약이 있기 때문에 race가 나더라도:
 * - 먼저 저장한 row를 재사용하거나
 * - unique 위반 시 다시 조회
 * 하는 방식으로 정리한다.
 */
@Service
public class WorkflowSnapshotService {

    private final WorkflowSnapshotRepository workflowSnapshotRepository;
    private final WorkflowHashCalculator workflowHashCalculator;

    public WorkflowSnapshotService(
            WorkflowSnapshotRepository workflowSnapshotRepository,
            WorkflowHashCalculator workflowHashCalculator
    ) {
        this.workflowSnapshotRepository = workflowSnapshotRepository;
        this.workflowHashCalculator = workflowHashCalculator;
    }

    @Transactional
    public WorkflowSnapshotEntity getOrCreateSnapshot(PromptAssemblyResult assemblyResult) {
        String workflowHash = workflowHashCalculator.calculate(assemblyResult.workflowSnapshotJson());

        return workflowSnapshotRepository.findByWorkflowHash(workflowHash)
                .orElseGet(() -> createSnapshot(assemblyResult, workflowHash));
    }

    private WorkflowSnapshotEntity createSnapshot(PromptAssemblyResult assemblyResult, String workflowHash) {
        WorkflowSnapshotEntity entity = new WorkflowSnapshotEntity();
        entity.setWorkflowName(assemblyResult.workflowName());
        entity.setWorkflowVersion(assemblyResult.workflowVersion());
        entity.setWorkflowHash(workflowHash);
        entity.setSourceFilename(assemblyResult.sourceFilename());
        entity.setPrimaryModelName(assemblyResult.primaryModelName());
        entity.setModelMetadataJson(assemblyResult.modelMetadataJson());
        entity.setWorkflowJson(assemblyResult.workflowSnapshotJson());

        try {
            return workflowSnapshotRepository.save(entity);
        } catch (DataIntegrityViolationException exception) {
            // unique(workflow_hash) 경쟁 상황이면 이미 다른 요청이 같은 snapshot을 만든 것이다.
            return workflowSnapshotRepository.findByWorkflowHash(workflowHash)
                    .orElseThrow(() -> exception);
        }
    }
}
