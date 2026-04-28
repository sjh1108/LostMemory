package com.lostmemory.aiserver.generation;

import java.time.Duration;
import java.time.Instant;
import java.util.List;
import java.util.Map;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.stereotype.Service;
import org.springframework.web.client.RestClientException;
import org.springframework.web.client.RestClientResponseException;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lostmemory.aiserver.comfyui.ComfyUiClient;
import com.lostmemory.aiserver.common.audit.AuditActionType;
import com.lostmemory.aiserver.common.audit.AuditRecorder;
import com.lostmemory.aiserver.common.audit.AuditStatus;
import com.lostmemory.aiserver.common.exception.ApiRequestException;

@Service
public class GenerationHistoryService {

    private static final Logger log = LoggerFactory.getLogger(GenerationHistoryService.class);

    static final Duration DEFAULT_POLL_INTERVAL = Duration.ofSeconds(2);
    static final Duration DEFAULT_SOFT_WARNING = Duration.ofSeconds(60);
    static final Duration DEFAULT_HARD_TIMEOUT = Duration.ofSeconds(120);
    static final int DEFAULT_MAX_FETCH_FAILURES = 3;

    private final ComfyUiClient comfyUiClient;
    private final GenerationHistoryParser generationHistoryParser;
    private final GenerationStatusResolver generationStatusResolver;
    private final AuditRecorder auditRecorder;
    private final ObjectMapper objectMapper;
    private final Duration pollInterval;
    private final Duration softWarning;
    private final Duration hardTimeout;
    private final int maxFetchFailures;

    @Autowired
    public GenerationHistoryService(
            ComfyUiClient comfyUiClient,
            GenerationHistoryParser generationHistoryParser,
            GenerationStatusResolver generationStatusResolver,
            AuditRecorder auditRecorder,
            ObjectMapper objectMapper
    ) {
        this(
                comfyUiClient,
                generationHistoryParser,
                generationStatusResolver,
                auditRecorder,
                objectMapper,
                DEFAULT_POLL_INTERVAL,
                DEFAULT_SOFT_WARNING,
                DEFAULT_HARD_TIMEOUT,
                DEFAULT_MAX_FETCH_FAILURES);
    }

    GenerationHistoryService(
            ComfyUiClient comfyUiClient,
            GenerationHistoryParser generationHistoryParser,
            GenerationStatusResolver generationStatusResolver,
            AuditRecorder auditRecorder,
            ObjectMapper objectMapper,
            Duration pollInterval,
            Duration softWarning,
            Duration hardTimeout,
            int maxFetchFailures
    ) {
        this.comfyUiClient = comfyUiClient;
        this.generationHistoryParser = generationHistoryParser;
        this.generationStatusResolver = generationStatusResolver;
        this.auditRecorder = auditRecorder;
        this.objectMapper = objectMapper;
        this.pollInterval = pollInterval;
        this.softWarning = softWarning;
        this.hardTimeout = hardTimeout;
        this.maxFetchFailures = maxFetchFailures;
    }

    public GenerationHistoryResponse pollUntilCompleted(String promptId) {
        Instant startedAt = Instant.now();
        boolean softWarningLogged = false;
        int consecutiveFetchFailures = 0;
        GenerationHistorySnapshot lastSnapshot = new GenerationHistorySnapshot(
                promptId,
                false,
                false,
                null,
                null,
                List.of());

        while (Duration.between(startedAt, Instant.now()).compareTo(hardTimeout) < 0) {
            try {
                Map<String, Object> rawResponse = comfyUiClient.fetchHistory(promptId);
                consecutiveFetchFailures = 0;

                GenerationHistorySnapshot snapshot = generationHistoryParser.parse(promptId, rawResponse);
                lastSnapshot = snapshot;
                GenerationHistoryDecision progressDecision = generationStatusResolver.resolveInProgress(snapshot);

                log.debug("Fetched ComfyUI history. promptId={}, historyFound={}, completed={}, executionStatus={}, response={}",
                        promptId,
                        snapshot.historyFound(),
                        snapshot.completed(),
                        progressDecision.executionStatus(),
                        toJson(rawResponse));

                if (snapshot.historyFound() && snapshot.completed()) {
                    GenerationHistoryDecision completedDecision = generationStatusResolver.resolveCompleted(snapshot);

                    if (completedDecision.executionStatus() != GenerationExecutionStatus.SUCCEEDED) {
                        recordTerminalFailure(snapshot, completedDecision);
                    }

                    return toResponse(snapshot, completedDecision);
                }
            } catch (RestClientResponseException exception) {
                consecutiveFetchFailures = handleFetchFailure(
                        promptId,
                        consecutiveFetchFailures,
                        exception.getStatusCode().value(),
                        exception.getResponseBodyAsString(),
                        exception.getMessage());
            } catch (RestClientException exception) {
                consecutiveFetchFailures = handleFetchFailure(
                        promptId,
                        consecutiveFetchFailures,
                        null,
                        null,
                        exception.getMessage());
            }

            if (consecutiveFetchFailures >= maxFetchFailures) {
                GenerationHistoryDecision failureDecision = generationStatusResolver.resolveHistoryFetchFailure(promptId);
                recordTerminalFailure(lastSnapshot, failureDecision);
                return toResponse(lastSnapshot, failureDecision);
            }

            Duration elapsed = Duration.between(startedAt, Instant.now());
            if (!softWarningLogged && elapsed.compareTo(softWarning) >= 0) {
                log.warn("ComfyUI history polling is still waiting. promptId={}, elapsedSeconds={}",
                        promptId,
                        elapsed.toSeconds());
                softWarningLogged = true;
            }

            sleep();
        }

        GenerationHistoryDecision timeoutDecision = generationStatusResolver.resolveTimeout(promptId);
        recordTerminalFailure(lastSnapshot, timeoutDecision);
        return toResponse(lastSnapshot, timeoutDecision);
    }

    private void sleep() {
        try {
            Thread.sleep(pollInterval.toMillis());
        } catch (InterruptedException exception) {
            Thread.currentThread().interrupt();
            throw new ApiRequestException(
                    org.springframework.http.HttpStatus.INTERNAL_SERVER_ERROR,
                    "HISTORY_POLL_INTERRUPTED",
                    "ComfyUI history polling was interrupted.");
        }
    }

    /**
     * 일시적인 /history 호출 실패는 바로 terminal failure로 보지 않고 누적 횟수를 센다.
     */
    private int handleFetchFailure(
            String promptId,
            int consecutiveFetchFailures,
            Integer statusCode,
            String responseBody,
            String message
    ) {
        int updatedFailures = consecutiveFetchFailures + 1;

        log.warn("ComfyUI /history request failed. promptId={}, consecutiveFailures={}, status={}, body={}, message={}",
                promptId,
                updatedFailures,
                statusCode,
                responseBody,
                message);

        return updatedFailures;
    }

    /**
     * API 응답은 사용자용 message만 담고, 내부 로그 상세는 audit recorder에서 분리 보관한다.
     */
    private GenerationHistoryResponse toResponse(
            GenerationHistorySnapshot snapshot,
            GenerationHistoryDecision decision
    ) {
        return new GenerationHistoryResponse(
                snapshot.promptId(),
                decision.executionStatus(),
                decision.failureReason(),
                snapshot.completed(),
                snapshot.statusText(),
                decision.userMessage(),
                snapshot.outputImage(),
                snapshot.messageTypes());
    }

    private void recordTerminalFailure(
            GenerationHistorySnapshot snapshot,
            GenerationHistoryDecision decision
    ) {
        auditRecorder.record(
                AuditActionType.GENERATE,
                AuditStatus.FAILED,
                Map.of(
                        "promptId", snapshot.promptId(),
                        "executionStatus", decision.executionStatus().name(),
                        "failureReason", decision.failureReason() != null ? decision.failureReason().name() : "NONE",
                        "completed", snapshot.completed(),
                        "statusText", snapshot.statusText() != null ? snapshot.statusText() : "",
                        "messageTypes", snapshot.messageTypes(),
                        "internalMessage", decision.internalMessage(),
                        "outputFound", snapshot.outputImage() != null));

        log.warn("Generation ended without success. promptId={}, executionStatus={}, failureReason={}, detail={}",
                snapshot.promptId(),
                decision.executionStatus(),
                decision.failureReason(),
                decision.internalMessage());
    }

    private String toJson(Object value) {
        try {
            return objectMapper.writeValueAsString(value);
        } catch (JsonProcessingException exception) {
            return String.valueOf(value);
        }
    }
}
