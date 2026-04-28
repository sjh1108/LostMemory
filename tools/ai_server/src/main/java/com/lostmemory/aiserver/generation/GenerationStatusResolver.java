package com.lostmemory.aiserver.generation;

import org.springframework.stereotype.Component;

/**
 * /history snapshot을 실행 상태로 해석한다.
 */
@Component
public class GenerationStatusResolver {

    private final GenerationFailureMessageResolver messageResolver;

    public GenerationStatusResolver(GenerationFailureMessageResolver messageResolver) {
        this.messageResolver = messageResolver;
    }

    public GenerationHistoryDecision resolveInProgress(GenerationHistorySnapshot snapshot) {
        // history entry가 아직 없으면 submit 직후, entry는 있지만 completed가 아니면 running으로 본다.
        GenerationExecutionStatus status = snapshot.historyFound()
                ? GenerationExecutionStatus.RUNNING
                : GenerationExecutionStatus.SUBMITTED;

        return new GenerationHistoryDecision(
                status,
                null,
                messageResolver.successMessage(status),
                "ComfyUI history has not reached a terminal state yet.");
    }

    public GenerationHistoryDecision resolveCompleted(GenerationHistorySnapshot snapshot) {
        // completed=true는 실행 종료 의미일 뿐 성공 의미가 아니므로 success + output 존재를 함께 확인한다.
        if (snapshot.statusText() == null || snapshot.statusText().isBlank()) {
            return failureDecision(GenerationFailureReason.UNKNOWN_FAILURE, snapshot.promptId(), snapshot.statusText());
        }

        if (!"success".equalsIgnoreCase(snapshot.statusText())) {
            return failureDecision(GenerationFailureReason.COMFYUI_REPORTED_FAILURE, snapshot.promptId(), snapshot.statusText());
        }

        if (snapshot.outputImage() == null) {
            return failureDecision(GenerationFailureReason.OUTPUT_MISSING, snapshot.promptId(), snapshot.statusText());
        }

        return new GenerationHistoryDecision(
                GenerationExecutionStatus.SUCCEEDED,
                null,
                messageResolver.successMessage(GenerationExecutionStatus.SUCCEEDED),
                "Generation completed successfully.");
    }

    public GenerationHistoryDecision resolveHistoryFetchFailure(String promptId) {
        return failureDecision(GenerationFailureReason.HISTORY_FETCH_FAILED, promptId, null);
    }

    public GenerationHistoryDecision resolveTimeout(String promptId) {
        return failureDecision(GenerationFailureReason.POLL_TIMEOUT, promptId, null);
    }

    private GenerationHistoryDecision failureDecision(
            GenerationFailureReason reason,
            String promptId,
            String statusText
    ) {
        GenerationFailureMessages messages = messageResolver.failureMessages(reason, promptId, statusText);

        return new GenerationHistoryDecision(
                reason == GenerationFailureReason.POLL_TIMEOUT
                        ? GenerationExecutionStatus.TIMED_OUT
                        : GenerationExecutionStatus.FAILED,
                reason,
                messages.userMessage(),
                messages.internalMessage());
    }
}
