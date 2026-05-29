package com.lostmemory.server.llm.service;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ObjectNode;
import com.lostmemory.server.global.exception.BusinessException;
import com.lostmemory.server.global.exception.ErrorCode;
import com.lostmemory.server.llm.config.LlmProperties;
import com.lostmemory.server.llm.context.LlmContextProvider;
import com.lostmemory.server.llm.dto.ChatMessage;
import com.lostmemory.server.llm.dto.ChatRequest;
import lombok.extern.slf4j.Slf4j;
import org.springframework.core.ParameterizedTypeReference;
import org.springframework.http.HttpHeaders;
import org.springframework.http.HttpStatusCode;
import org.springframework.http.MediaType;
import org.springframework.http.codec.ServerSentEvent;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.stereotype.Service;
import org.springframework.web.reactive.function.client.WebClient;
import org.springframework.web.servlet.mvc.method.annotation.ResponseBodyEmitter;
import reactor.core.publisher.Mono;

import java.io.IOException;
import java.time.Duration;
import java.util.List;

/**
 * vLLM Chat Completions API 로 forward + SSE 스트리밍 pass-through.
 * Spring WebClient (reactor-netty) 사용 — JDK HttpClient 보다 SSE / HTTP/2 처리 안정적.
 * 응답은 {@link ResponseBodyEmitter} 로 클라에 forward (servlet MVC 환경 유지).
 *
 * Audit log 형식 ({@code [LLM_AUDIT]} prefix 로 grep):
 * <ul>
 *   <li>{@code start} — 진입 시점. userId / model / msgCount / totalChars 박음</li>
 *   <li>{@code rejected} — 컨텍스트 가드에서 stream 시작 전 거부. userId / model / reason / duration</li>
 *   <li>{@code end result=success|timeout|error} — emitter 종료 시점. userId / model / duration / (error class)</li>
 * </ul>
 * 메시지 본문(content) 은 의도적으로 박지 않음 (PII 보호 — 후속 정책 결정 시 sample 토글 검토).
 */
@Slf4j
@Service
public class LlmProxyService {

    /**
     * 입력 prompt 누적 길이 char cap.
     * bllossom-8b 모델 context window 8192 토큰 + 한국어 1.5 char/token 기준 약 6,600 토큰.
     * 출력 max_tokens ~1.4K 토큰 여유 확보 위해 입력은 ~10K char 까지만 허용.
     */
    private static final int CONTEXT_INPUT_CHAR_CAP = 10_000;

    private static final String AUDIT_PREFIX = "[LLM_AUDIT]";

    private static final Duration UPSTREAM_TIMEOUT = Duration.ofSeconds(60);

    /** vLLM 의 OpenAI 호환 SSE event 를 String payload 로 받기 위한 type reference. */
    private static final ParameterizedTypeReference<ServerSentEvent<String>> SSE_TYPE_REF =
            new ParameterizedTypeReference<>() {
            };

    private final LlmProperties properties;
    private final ObjectMapper objectMapper;
    private final LlmContextProvider contextProvider;
    private final WebClient webClient;

    public LlmProxyService(LlmProperties properties,
                           ObjectMapper objectMapper,
                           LlmContextProvider contextProvider) {
        this.properties = properties;
        this.objectMapper = objectMapper;
        this.contextProvider = contextProvider;
        this.webClient = WebClient.builder()
                .baseUrl(properties.baseUrl())
                .defaultHeader(HttpHeaders.AUTHORIZATION, "Bearer " + properties.apiKey())
                .defaultHeader(HttpHeaders.CONTENT_TYPE, MediaType.APPLICATION_JSON_VALUE)
                .build();
    }

    public void streamChat(ChatRequest body, ResponseBodyEmitter emitter) {
        long startMs = System.currentTimeMillis();
        String userId = resolveAuditUserId();
        String model = body.model();

        // provider 가드 — anthropic 분기는 별도 task 에서 body/SSE 변환 layer 채울 예정.
        // augment 전에 빠른 거절. 불필요한 contextProvider 호출 회피.
        if (properties.provider() == LlmProperties.Provider.ANTHROPIC) {
            log.warn("{} rejected userId={} model={} reason={} duration={}ms",
                    AUDIT_PREFIX, userId, model, ErrorCode.LLM_PROVIDER_NOT_SUPPORTED,
                    System.currentTimeMillis() - startMs);
            throw new BusinessException(ErrorCode.LLM_PROVIDER_NOT_SUPPORTED);
        }

        // context augment — hardcoded chunk / RAG / no-op 분기. messages 가 augmented 로 교체됨.
        // 결과를 audit log + cap 가드 + vLLM forward 모두에 사용.
        LlmContextProvider.AugmentationResult augResult = contextProvider.augment(body.messages());
        List<ChatMessage> augmentedMessages = augResult.messages();
        int chunkCount = augResult.chunkCount();
        int msgCount = augmentedMessages != null ? augmentedMessages.size() : 0;
        long totalChars = countContextChars(augmentedMessages);

        log.info("{} start userId={} model={} msgCount={} totalChars={} provider={} ctxProvider={} chunks={}",
                AUDIT_PREFIX, userId, model, msgCount, totalChars,
                properties.provider(), properties.contextProvider(), chunkCount);

        try {
            ensureContextWithinCap(totalChars);
        } catch (BusinessException ex) {
            log.warn("{} rejected userId={} model={} reason={} duration={}ms",
                    AUDIT_PREFIX, userId, model, ex.errorCode(),
                    System.currentTimeMillis() - startMs);
            throw ex;
        }

        emitter.onCompletion(() -> log.info(
                "{} end userId={} model={} duration={}ms result=success",
                AUDIT_PREFIX, userId, model, System.currentTimeMillis() - startMs));
        emitter.onTimeout(() -> log.warn(
                "{} end userId={} model={} duration={}ms result=timeout",
                AUDIT_PREFIX, userId, model, System.currentTimeMillis() - startMs));
        emitter.onError(ex -> log.warn(
                "{} end userId={} model={} duration={}ms result=error class={}",
                AUDIT_PREFIX, userId, model, System.currentTimeMillis() - startMs,
                ex.getClass().getSimpleName()));

        String json;
        try {
            // stream 강제: 클라가 false 보내도 백엔드는 true 로 forward.
            ObjectNode mutable = objectMapper.valueToTree(body);
            mutable.put("stream", true);
            // augment 결과로 messages 교체 — context provider 가 chunk 주입했으면 그 결과 forward.
            // NoOp 의 경우 augmentedMessages == body.messages() 라 효과 없음 (같은 참조).
            mutable.set("messages", objectMapper.valueToTree(augmentedMessages));
            json = objectMapper.writeValueAsString(mutable);
        } catch (JsonProcessingException e) {
            log.warn("LLM proxy body serialization failed", e);
            emitter.completeWithError(new BusinessException(ErrorCode.COMMON_INVALID_INPUT, e));
            return;
        }

        webClient.post()
                .uri("/chat/completions")
                .accept(MediaType.TEXT_EVENT_STREAM)
                .bodyValue(json)
                .retrieve()
                .onStatus(HttpStatusCode::isError, resp -> Mono.error(
                        new BusinessException(mapUpstreamError(resp.statusCode().value()))))
                .bodyToFlux(SSE_TYPE_REF)
                .timeout(UPSTREAM_TIMEOUT)
                .subscribe(
                        event -> forwardSseEvent(event, emitter),
                        ex -> handleUpstreamError(ex, emitter),
                        emitter::complete);
    }

    /**
     * vLLM 의 SSE event 한 개를 OpenAI 호환 형식({@code data: <payload>\n\n}) 으로 재조립해 emitter 로 forward.
     * 클라 {@code LlmStreamingDownloadHandler} 가 기존 그대로 {@code data:} prefix 파싱.
     */
    private void forwardSseEvent(ServerSentEvent<String> event, ResponseBodyEmitter emitter) {
        String payload = event.data();
        if (payload == null) return;
        try {
            emitter.send("data: " + payload + "\n\n", MediaType.TEXT_EVENT_STREAM);
        } catch (IOException ex) {
            log.warn("SSE forward interrupted", ex);
            emitter.completeWithError(ex);
        }
    }

    /**
     * WebClient 흐름에서 발생한 모든 예외를 emitter 로 전달. BusinessException 은 그대로,
     * 그 외는 mapUpstreamException 으로 ErrorCode 매핑 후 wrap.
     */
    private void handleUpstreamError(Throwable ex, ResponseBodyEmitter emitter) {
        log.error("vLLM call failed", ex);
        if (ex instanceof BusinessException be) {
            emitter.completeWithError(be);
        } else {
            emitter.completeWithError(new BusinessException(mapUpstreamException(ex), ex));
        }
    }

    /**
     * vLLM 가 응답을 보냈으나 4xx/5xx 인 경우 status 별 ErrorCode 매핑.
     * - 400 / 401 / 403 → 백엔드 측 forward 요청 또는 API key 설정 문제 → 502 BAD_GATEWAY
     * - 429 → 클라에게 재시도 안내 위해 503 SERVICE_UNAVAILABLE
     * - 503 / 504 → 그대로 503 / 504
     * - 그 외 → 502 BAD_GATEWAY (alias COMMON_INTERNAL_ERROR 회피 — 디버깅 가능성 향상)
     */
    private ErrorCode mapUpstreamError(int status) {
        return switch (status) {
            case 400 -> ErrorCode.LLM_UPSTREAM_BAD_REQUEST;
            case 401, 403 -> ErrorCode.LLM_UPSTREAM_AUTH_FAILED;
            case 429 -> ErrorCode.LLM_UPSTREAM_RATE_LIMIT;
            case 503 -> ErrorCode.LLM_UPSTREAM_UNAVAILABLE;
            case 504 -> ErrorCode.LLM_UPSTREAM_TIMEOUT;
            default -> ErrorCode.LLM_UPSTREAM_INTERNAL_ERROR;
        };
    }

    /**
     * WebClient/Reactor 가 던지는 예외 유형별 ErrorCode 매핑.
     * Reactor TimeoutException / Netty ConnectException 등.
     */
    private ErrorCode mapUpstreamException(Throwable ex) {
        Throwable cause = ex.getCause() != null ? ex.getCause() : ex;
        if (cause instanceof java.util.concurrent.TimeoutException) {
            return ErrorCode.LLM_UPSTREAM_TIMEOUT;
        }
        if (cause instanceof java.net.ConnectException
                || cause instanceof java.nio.channels.UnresolvedAddressException) {
            return ErrorCode.LLM_UPSTREAM_UNAVAILABLE;
        }
        return ErrorCode.LLM_UPSTREAM_INTERNAL_ERROR;
    }

    /**
     * messages 배열의 누적 content 길이 합산.
     * audit log + ensureContextWithinCap 가 같은 값을 재사용하도록 분리.
     */
    private long countContextChars(List<ChatMessage> messages) {
        if (messages == null) return 0;
        long total = 0;
        for (ChatMessage msg : messages) {
            if (msg == null || msg.content() == null) continue;
            total += msg.content().length();
        }
        return total;
    }

    /**
     * 누적 char 가 cap 안인지만 검증. 초과 시 BusinessException(LLM_CONTEXT_TOO_LONG) 던짐 —
     * stream 시작 전이라 GlobalExceptionHandler 가 정상 413 JSON 응답.
     */
    private void ensureContextWithinCap(long totalChars) {
        if (totalChars > CONTEXT_INPUT_CHAR_CAP) {
            log.warn("LLM context too long: chars={} cap={}", totalChars, CONTEXT_INPUT_CHAR_CAP);
            throw new BusinessException(ErrorCode.LLM_CONTEXT_TOO_LONG);
        }
    }

    /**
     * audit 용 사용자 식별자. SecurityContext 의 Authentication 이름 (JWT subject = userId 또는 dev bypass 의 "-1").
     * 인증 미설정 (보통은 일어나지 않음 — /llm/chat 가 인증 필수 경로) 시 "anonymous".
     */
    private String resolveAuditUserId() {
        try {
            var auth = SecurityContextHolder.getContext().getAuthentication();
            return auth != null ? auth.getName() : "anonymous";
        } catch (Exception e) {
            return "(error)";
        }
    }
}
