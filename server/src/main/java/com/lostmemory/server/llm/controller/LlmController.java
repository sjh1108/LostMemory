package com.lostmemory.server.llm.controller;

import com.lostmemory.server.llm.dto.ChatRequest;
import com.lostmemory.server.llm.scripted.ScriptedResponder;
import com.lostmemory.server.llm.service.LlmProxyService;
import com.lostmemory.server.llm.service.LlmRateLimitService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.Authentication;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;
import org.springframework.web.servlet.mvc.method.annotation.ResponseBodyEmitter;

@Tag(name = "LLM", description = "LLM 채팅 (SSE 스트리밍 프록시)")
@RestController
@RequestMapping("/llm")
@RequiredArgsConstructor
public class LlmController {

    private final LlmProxyService llmProxyService;
    private final LlmRateLimitService rateLimitService;
    private final ScriptedResponder scriptedResponder;

    // TODO(S14P31C201-XXX): remove dev bypass description when client login lands
    @Operation(
            summary = "LLM 채팅 (SSE 스트리밍 프록시)",
            description = """
                    OpenAI Chat Completions 호환 body 를 LLM 백엔드로 forward + SSE 응답 pass-through.
                    - 인증: Authorization Bearer JWT
                    - provider: vllm (현재 운영 — OpenAI 호환) / anthropic (별도 task 의 stub — 501 반환)
                    - scripted-mode: 시연 대본 발화 매칭 시 LLM 호출 우회 + 미리 정의된 응답 SSE emit (운영 default OFF)
                    - 화이트리스트 8개 필드만 수용 — model, messages, temperature, max_tokens, stream,
                      frequency_penalty (-2~2), presence_penalty (-2~2), repetition_penalty (1~2). 그 외 400.
                    - Rate limit: 사용자별 10 req/min (in-memory fixed window) — 초과 시 503
                    """
    )
    @PostMapping(value = "/chat", produces = MediaType.TEXT_EVENT_STREAM_VALUE)
    public ResponseEntity<ResponseBodyEmitter> chat(@RequestBody @Valid ChatRequest body) {
        rateLimitService.checkOrThrow(resolveUserId());

        ResponseBodyEmitter emitter = new ResponseBodyEmitter(0L);

        // scripted hook — 시연 대본 매치 시 LLM 우회 + SSE 직접 emit + emitter.complete().
        // 운영에선 llm.scripted-mode.enabled=false 라 항상 false 반환 (=호출자 LLM 호출 진행).
        if (!scriptedResponder.tryRespond(body.messages(), emitter)) {
            llmProxyService.streamChat(body, emitter);
        }
        return ResponseEntity.ok()
                .contentType(MediaType.TEXT_EVENT_STREAM)
                .header("X-Accel-Buffering", "no")
                .body(emitter);
    }

    /**
     * rate limit key 용 사용자 식별자. /llm/chat 가 인증 필수 경로라 SecurityFilterChain 통과 시점에
     * Authentication 이 null 일 가능성은 거의 없지만 안전 차원에서 "anonymous" fallback.
     */
    private String resolveUserId() {
        Authentication auth = SecurityContextHolder.getContext().getAuthentication();
        return auth != null ? auth.getName() : "anonymous";
    }
}
