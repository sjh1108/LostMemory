package com.lostmemory.server.llm.controller;

import com.fasterxml.jackson.databind.JsonNode;
import com.lostmemory.server.llm.service.LlmProxyService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.tags.Tag;
import lombok.RequiredArgsConstructor;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;
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

    // TODO(S14P31C201-XXX): remove dev bypass description when client login lands
    @Operation(
            summary = "LLM 채팅 (SSE 스트리밍 프록시)",
            description = """
                    OpenAI Chat Completions 호환 body 를 vLLM 으로 forward + SSE 응답 pass-through.
                    - 인증: Authorization Bearer JWT
                    """
    )
    @PostMapping(value = "/chat", produces = MediaType.TEXT_EVENT_STREAM_VALUE)
    public ResponseEntity<ResponseBodyEmitter> chat(@RequestBody JsonNode body) {
        ResponseBodyEmitter emitter = new ResponseBodyEmitter(0L);
        llmProxyService.streamChat(body, emitter);
        return ResponseEntity.ok()
                .contentType(MediaType.TEXT_EVENT_STREAM)
                .header("X-Accel-Buffering", "no")
                .body(emitter);
    }
}
