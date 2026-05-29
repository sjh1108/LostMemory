package com.lostmemory.server.analytics.dto;

/**
 * batch ingest 응답 — 큐에 적재한 이벤트 개수.
 */
public record AnalyticsBatchResponse(int accepted) {}
