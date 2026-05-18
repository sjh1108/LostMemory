package com.lostmemory.aiserver.generation;

import io.swagger.v3.oas.annotations.media.Schema;

/**
 * 생성 실패 사유 분류.
 * 사용자 메시지와 내부 감사 로그를 분리할 때 공통 키로 사용한다.
 */
@Schema(description = "Failure reason for a generation execution")
public enum GenerationFailureReason {
    /** /history 조회가 반복적으로 실패한 경우 */
    HISTORY_FETCH_FAILED,
    /** ComfyUI가 terminal 상태를 success가 아닌 값으로 보고한 경우 */
    COMFYUI_REPORTED_FAILURE,
    /** 완료는 되었지만 결과 output image를 찾지 못한 경우 */
    OUTPUT_MISSING,
    /** polling 제한 시간을 초과한 경우 */
    POLL_TIMEOUT,
    /** 명시적 분류가 어려운 기타 실패 */
    UNKNOWN_FAILURE
}
