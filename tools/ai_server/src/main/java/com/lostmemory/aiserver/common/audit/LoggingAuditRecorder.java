package com.lostmemory.aiserver.common.audit;

import java.util.Map;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Component;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.ObjectMapper;

/**
 * 1차 구현에서는 audit log를 구조화된 애플리케이션 로그로만 남긴다.
 */
@Component
public class LoggingAuditRecorder implements AuditRecorder {

    private static final Logger log = LoggerFactory.getLogger(LoggingAuditRecorder.class);

    private final ObjectMapper objectMapper;

    public LoggingAuditRecorder(ObjectMapper objectMapper) {
        this.objectMapper = objectMapper;
    }

    @Override
    public void record(AuditActionType actionType, AuditStatus status, Map<String, Object> payload) {
        log.info("AUDIT actionType={}, status={}, payload={}", actionType, status, toJson(payload));
    }

    private String toJson(Map<String, Object> payload) {
        try {
            return objectMapper.writeValueAsString(payload);
        } catch (JsonProcessingException exception) {
            return String.valueOf(payload);
        }
    }
}
