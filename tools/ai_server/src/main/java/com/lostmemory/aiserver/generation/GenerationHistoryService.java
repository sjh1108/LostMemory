package com.lostmemory.aiserver.generation;

import java.time.Duration;
import java.time.Instant;
import java.util.Map;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Service;
import org.springframework.web.client.RestClientException;
import org.springframework.web.client.RestClientResponseException;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lostmemory.aiserver.comfyui.ComfyUiClient;
import com.lostmemory.aiserver.common.exception.ApiRequestException;

@Service
public class GenerationHistoryService {

    private static final Logger log = LoggerFactory.getLogger(GenerationHistoryService.class);

    static final Duration DEFAULT_POLL_INTERVAL = Duration.ofSeconds(2);
    static final Duration DEFAULT_SOFT_WARNING = Duration.ofSeconds(60);
    static final Duration DEFAULT_HARD_TIMEOUT = Duration.ofSeconds(120);

    private final ComfyUiClient comfyUiClient;
    private final GenerationHistoryParser generationHistoryParser;
    private final ObjectMapper objectMapper;
    private final Duration pollInterval;
    private final Duration softWarning;
    private final Duration hardTimeout;

    @Autowired
    public GenerationHistoryService(
            ComfyUiClient comfyUiClient,
            GenerationHistoryParser generationHistoryParser,
            ObjectMapper objectMapper
    ) {
        this(comfyUiClient, generationHistoryParser, objectMapper,
                DEFAULT_POLL_INTERVAL, DEFAULT_SOFT_WARNING, DEFAULT_HARD_TIMEOUT);
    }

    GenerationHistoryService(
            ComfyUiClient comfyUiClient,
            GenerationHistoryParser generationHistoryParser,
            ObjectMapper objectMapper,
            Duration pollInterval,
            Duration softWarning,
            Duration hardTimeout
    ) {
        this.comfyUiClient = comfyUiClient;
        this.generationHistoryParser = generationHistoryParser;
        this.objectMapper = objectMapper;
        this.pollInterval = pollInterval;
        this.softWarning = softWarning;
        this.hardTimeout = hardTimeout;
    }

    public GenerationHistoryResponse pollUntilCompleted(String promptId) {
        Instant startedAt = Instant.now();
        boolean softWarningLogged = false;

        while (Duration.between(startedAt, Instant.now()).compareTo(hardTimeout) < 0) {
            Map<String, Object> rawResponse = fetchHistory(promptId);
            GenerationHistorySnapshot snapshot = generationHistoryParser.parse(promptId, rawResponse);

            log.debug("Fetched ComfyUI history. promptId={}, historyFound={}, completed={}, response={}",
                    promptId,
                    snapshot.historyFound(),
                    snapshot.completed(),
                    toJson(rawResponse));

            if (snapshot.historyFound() && snapshot.completed()) {
                return new GenerationHistoryResponse(
                        snapshot.promptId(),
                        true,
                        snapshot.statusText(),
                        snapshot.outputImage(),
                        snapshot.messageTypes());
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

        throw new ApiRequestException(
                HttpStatus.GATEWAY_TIMEOUT,
                "HISTORY_POLL_TIMEOUT",
                "ComfyUI history polling timed out.");
    }

    private Map<String, Object> fetchHistory(String promptId) {
        try {
            return comfyUiClient.fetchHistory(promptId);
        } catch (RestClientResponseException exception) {
            log.warn("ComfyUI /history request failed. promptId={}, status={}, body={}",
                    promptId,
                    exception.getStatusCode().value(),
                    exception.getResponseBodyAsString());
            throw new ApiRequestException(
                    HttpStatus.BAD_GATEWAY,
                    "COMFYUI_HISTORY_FETCH_FAILED",
                    "ComfyUI /history request failed.");
        } catch (RestClientException exception) {
            log.warn("ComfyUI /history request failed before receiving a response. promptId={}, message={}",
                    promptId,
                    exception.getMessage());
            throw new ApiRequestException(
                    HttpStatus.BAD_GATEWAY,
                    "COMFYUI_HISTORY_FETCH_FAILED",
                    "ComfyUI /history request failed.");
        }
    }

    private void sleep() {
        try {
            Thread.sleep(pollInterval.toMillis());
        } catch (InterruptedException exception) {
            Thread.currentThread().interrupt();
            throw new ApiRequestException(
                    HttpStatus.INTERNAL_SERVER_ERROR,
                    "HISTORY_POLL_INTERRUPTED",
                    "ComfyUI history polling was interrupted.");
        }
    }

    private String toJson(Object value) {
        try {
            return objectMapper.writeValueAsString(value);
        } catch (JsonProcessingException exception) {
            return String.valueOf(value);
        }
    }
}
