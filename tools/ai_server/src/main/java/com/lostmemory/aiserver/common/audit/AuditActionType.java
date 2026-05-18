package com.lostmemory.aiserver.common.audit;

/**
 * audit_logs.action_type 표준값.
 * 1차 MVP에서는 login, generate, upload, download 네 종류만 고정한다.
 */
public enum AuditActionType {
    /** 로그인 성공/실패 추적 */
    LOGIN,
    /** 이미지 생성 요청과 polling 결과 추적 */
    GENERATE,
    /** S3 업로드 단계 추적 */
    UPLOAD,
    /** 다운로드 또는 workflow export 추적 */
    DOWNLOAD
}
