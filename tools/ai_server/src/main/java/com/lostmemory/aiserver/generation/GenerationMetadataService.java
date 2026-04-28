package com.lostmemory.aiserver.generation;

import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import com.lostmemory.aiserver.repository.GenerationRepository;

/**
 * generation row 저장은 "submit 성공 직후의 최소 메타데이터"만 담당한다.
 *
 * 아직 여기서 하지 않는 것:
 * - output image 저장
 * - completed_at / failure_reason / failed_stage update
 * - storage_status update
 *
 * 그 값들은 /history polling과 output discovery가 붙는 후속 작업에서 갱신한다.
 */
@Service
public class GenerationMetadataService {

    static final String SUBMITTED_USER_MESSAGE = "생성 요청을 접수했습니다.";
    static final String SUBMITTED_INTERNAL_MESSAGE = "Prompt submitted to ComfyUI.";

    private final GenerationRepository generationRepository;
    private final PromptSummaryExtractor promptSummaryExtractor;

    public GenerationMetadataService(
            GenerationRepository generationRepository,
            PromptSummaryExtractor promptSummaryExtractor
    ) {
        this.generationRepository = generationRepository;
        this.promptSummaryExtractor = promptSummaryExtractor;
    }

    @Transactional
    public GenerationEntity saveSubmittedGeneration(
            CreateGenerationRequest request,
            String promptId,
            WorkflowSnapshotEntity workflowSnapshot
    ) {
        GenerationEntity entity = new GenerationEntity();
        entity.setPromptId(promptId);
        entity.setWorkflowSnapshot(workflowSnapshot);
        entity.setCreatedBy(null);
        entity.setExecutionStatus(GenerationExecutionStatus.SUBMITTED);
        entity.setPromptSummary(promptSummaryExtractor.extract(request.prompt()));
        entity.setFullPrompt(request.prompt());
        entity.setUserMessage(SUBMITTED_USER_MESSAGE);
        entity.setInternalMessage(SUBMITTED_INTERNAL_MESSAGE);

        return generationRepository.save(entity);
    }
}
