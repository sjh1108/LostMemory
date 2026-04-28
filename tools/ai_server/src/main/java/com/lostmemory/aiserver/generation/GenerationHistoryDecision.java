package com.lostmemory.aiserver.generation;

/**
 * /history polling 결과를 상태/사유/메시지로 정리한 내부 판단 결과.
 */
record GenerationHistoryDecision(
        GenerationExecutionStatus executionStatus,
        GenerationFailureReason failureReason,
        String userMessage,
        String internalMessage
) {
}
