package com.lostmemory.aiserver.common.audit;

import java.util.Map;

/**
 * 공통 감사 로그 기록 지점.
 * 지금은 logger로만 남기고, 이후 DB audit_logs 적재로 교체할 수 있게 계약만 분리한다.
 */
public interface AuditRecorder {

    void record(String category, String eventType, Map<String, Object> payload);
}
