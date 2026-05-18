package com.lostmemory.aiserver.generation;

import org.springframework.stereotype.Component;

/**
 * 사용자 노출 문구와 내부 로그 문구를 분리한다.
 */
@Component
public class GenerationFailureMessageResolver {

    public String successMessage(GenerationExecutionStatus status) {
        return switch (status) {
            case SUBMITTED -> "생성 요청을 접수했습니다.";
            case RUNNING -> "이미지 생성 중입니다.";
            case SUCCEEDED -> "이미지 생성이 완료되었습니다.";
            case FAILED, TIMED_OUT -> throw new IllegalArgumentException("Terminal failure status requires a failure reason.");
        };
    }

    public GenerationFailureMessages failureMessages(
            GenerationFailureReason reason,
            String promptId,
            String statusText
    ) {
        return switch (reason) {
            case HISTORY_FETCH_FAILED -> new GenerationFailureMessages(
                    "생성 결과를 확인하는 중 문제가 발생했습니다. 잠시 후 다시 시도해주세요.",
                    "ComfyUI /history request failed repeatedly while polling promptId=" + promptId + ".");
            case COMFYUI_REPORTED_FAILURE -> new GenerationFailureMessages(
                    "이미지 생성에 실패했습니다.",
                    "ComfyUI reported a non-success terminal status. promptId=" + promptId + ", statusText=" + statusText);
            case OUTPUT_MISSING -> new GenerationFailureMessages(
                    "생성은 완료되었지만 결과 파일을 찾지 못했습니다.",
                    "ComfyUI history completed without a discoverable output image. promptId=" + promptId);
            case POLL_TIMEOUT -> new GenerationFailureMessages(
                    "생성 시간이 예상보다 오래 걸려 요청을 종료했습니다.",
                    "ComfyUI history polling timed out before reaching a terminal success state. promptId=" + promptId);
            case UNKNOWN_FAILURE -> new GenerationFailureMessages(
                    "이미지 생성에 실패했습니다.",
                    "Generation failed for an unknown reason. promptId=" + promptId + ", statusText=" + statusText);
        };
    }
}
