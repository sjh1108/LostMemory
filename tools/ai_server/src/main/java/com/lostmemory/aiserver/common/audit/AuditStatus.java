package com.lostmemory.aiserver.common.audit;

/**
 * audit_logs.status 표준값.
 * timeout도 audit 관점에서는 FAILED로 기록하고, 세부 실패 사유는 payload로 남긴다.
 */
public enum AuditStatus {
    /** 작업이 성공적으로 끝난 경우 */
    SUCCESS,
    /** 작업이 실패했거나 timeout 등 비정상 종료된 경우 */
    FAILED
}
