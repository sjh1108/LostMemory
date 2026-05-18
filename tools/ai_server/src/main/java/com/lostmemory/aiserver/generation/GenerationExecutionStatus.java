package com.lostmemory.aiserver.generation;

import io.swagger.v3.oas.annotations.media.Schema;

/**
 * 생성 실행 상태.
 * 제출 상태와 분리해서 /history 기준 실행 진행 상황만 표현한다.
 */
@Schema(description = "Current execution status for a submitted generation request")
public enum GenerationExecutionStatus {
    /** ComfyUI에 제출은 됐지만 아직 실행 진행 흔적을 확인하지 못한 상태 */
    SUBMITTED,
    /** /history 기준으로 실행이 시작되었고 아직 완료되지 않은 상태 */
    RUNNING,
    /** 실행 완료, success 상태, output image 확인까지 끝난 상태 */
    SUCCEEDED,
    /** 실행은 끝났지만 실패로 판정된 상태 */
    FAILED,
    /** polling 제한 시간 안에 terminal state로 끝나지 않은 상태 */
    TIMED_OUT
}
