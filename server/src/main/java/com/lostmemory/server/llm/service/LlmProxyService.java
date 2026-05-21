package com.lostmemory.server.llm.service;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ObjectNode;
import com.lostmemory.server.global.exception.BusinessException;
import com.lostmemory.server.global.exception.ErrorCode;
import com.lostmemory.server.llm.config.LlmProperties;
import lombok.extern.slf4j.Slf4j;
import org.springframework.http.MediaType;
import org.springframework.stereotype.Service;
import org.springframework.web.servlet.mvc.method.annotation.ResponseBodyEmitter;

import java.io.IOException;
import java.io.UncheckedIOException;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.time.Duration;
import java.util.stream.Stream;

/**
 * vLLM Chat Completions API 로 forward + SSE 스트리밍 pass-through.
 * JDK HttpClient (java.net.http) 사용 — webflux 추가 없이 servlet 환경에서 동작.
 * 단일 사용자 가정 — 동시 SSE 부하 / async thread pool 격리는 후속 티켓 (TODO: webflux + WebClient + Flux 교체).
 */
@Slf4j
@Service
public class LlmProxyService {

    /**
     * 입력 prompt 누적 길이 char cap.
     * bllossom-8b 모델 context window 8192 토큰 + 한국어 1.5 char/token 기준 약 6,600 토큰.
     * 출력 max_tokens ~1.4K 토큰 여유 확보 위해 입력은 ~10K char 까지만 허용.
     * 정식 단계에선 jtokkit 등 토큰 추정 + body.max_tokens 합산으로 교체.
     */
    private static final int CONTEXT_INPUT_CHAR_CAP = 10_000;

    private final LlmProperties properties;
    private final ObjectMapper objectMapper;
    private final HttpClient httpClient;

    public LlmProxyService(LlmProperties properties, ObjectMapper objectMapper) {
        this.properties = properties;
        this.objectMapper = objectMapper;
        this.httpClient = HttpClient.newBuilder()
                .connectTimeout(Duration.ofSeconds(10))
                .build();
    }

    public void streamChat(JsonNode body, ResponseBodyEmitter emitter) {
        // 컨텍스트 윈도우 가드 — 누적 messages 길이 cap 초과 시 stream 시작 전에 413 JSON 응답.
        // 2026-05-20 인시던트: 클라가 messages history 전체 누적해 vLLM 400. 사전 차단.
        ensureContextWithinCap(body);

        String json;
        try {
            ObjectNode mutable = body != null && body.isObject()
                    ? (ObjectNode) body
                    : objectMapper.createObjectNode();
            mutable.put("stream", true);
            json = objectMapper.writeValueAsString(mutable);
        } catch (IOException e) {
            log.warn("LLM proxy body serialization failed", e);
            emitter.completeWithError(new BusinessException(ErrorCode.COMMON_INVALID_INPUT, e));
            return;
        }

        HttpRequest req = HttpRequest.newBuilder(URI.create(properties.baseUrl() + "/chat/completions"))
                .timeout(Duration.ofSeconds(60))
                .header("Authorization", "Bearer " + properties.apiKey())
                .header("Content-Type", "application/json")
                .header("Accept", "text/event-stream")
                .POST(HttpRequest.BodyPublishers.ofString(json))
                .build();

        httpClient.sendAsync(req, HttpResponse.BodyHandlers.ofLines())
                .thenAccept(resp -> forwardLines(resp, emitter))
                .exceptionally(ex -> {
                    log.error("vLLM call failed", ex);
                    emitter.completeWithError(new BusinessException(ErrorCode.COMMON_INTERNAL_ERROR, ex));
                    return null;
                });
    }

    private void forwardLines(HttpResponse<Stream<String>> resp, ResponseBodyEmitter emitter) {
        int status = resp.statusCode();
        if (status >= 400) {
            log.warn("vLLM upstream returned {}", status);
            emitter.completeWithError(new BusinessException(mapUpstreamError(status)));
            return;
        }
        try (Stream<String> lines = resp.body()) {
            lines.forEach(line -> {
                try {
                    emitter.send(line + "\n", MediaType.TEXT_EVENT_STREAM);
                } catch (IOException e) {
                    throw new UncheckedIOException(e);
                }
            });
            emitter.complete();
        } catch (UncheckedIOException e) {
            log.warn("SSE forward interrupted", e);
            emitter.completeWithError(e.getCause());
        }
    }

    private ErrorCode mapUpstreamError(int status) {
        if (status == 400) return ErrorCode.COMMON_INVALID_INPUT;
        return ErrorCode.COMMON_INTERNAL_ERROR;
    }

    /**
     * messages 배열의 누적 content 길이가 cap 안에 있는지 검증.
     * content 가 textual(string) 이면 asText() 길이, multi-modal array 면 toString() 길이로 추정.
     * cap 초과 시 BusinessException(LLM_CONTEXT_TOO_LONG) 던짐 — stream 시작 전이라 GlobalExceptionHandler 가 정상 413 JSON 응답.
     */
    private void ensureContextWithinCap(JsonNode body) {
        if (body == null) return;
        JsonNode messages = body.path("messages");
        if (!messages.isArray()) return;

        long totalChars = 0;
        for (JsonNode msg : messages) {
            JsonNode content = msg.path("content");
            if (content.isMissingNode() || content.isNull()) continue;
            String text = content.isTextual() ? content.asText() : content.toString();
            totalChars += text.length();
            if (totalChars > CONTEXT_INPUT_CHAR_CAP) {
                log.warn("LLM context too long: chars={} cap={}", totalChars, CONTEXT_INPUT_CHAR_CAP);
                throw new BusinessException(ErrorCode.LLM_CONTEXT_TOO_LONG);
            }
        }
    }
}
