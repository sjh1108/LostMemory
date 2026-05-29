package com.lostmemory.server.llm.service;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lostmemory.server.global.exception.BusinessException;
import com.lostmemory.server.global.exception.ErrorCode;
import com.lostmemory.server.llm.config.LlmProperties;
import com.lostmemory.server.llm.context.LlmContextProvider;
import com.lostmemory.server.llm.context.NoOpContextProvider;
import com.lostmemory.server.llm.dto.ChatMessage;
import com.lostmemory.server.llm.dto.ChatRequest;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.web.servlet.mvc.method.annotation.ResponseBodyEmitter;

import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

/**
 * LlmProxyService 의 provider 분기 hook 검증.
 *
 * 본 PR 은 ANTHROPIC 분기를 stub 으로 두고 즉시 LLM_PROVIDER_NOT_SUPPORTED throw —
 * 향후 GMS Claude swap 작업 시 body/SSE 변환 layer 채울 예정.
 *
 * VLLM 분기 전체 (WebClient + SSE forward) 는 WebClient mock 비용이 커서 본 테스트엔 없음 —
 * 분기 통과 후 webClient.post() 시도까지만 검증하거나, EC2 가빌드 curl 로 통합 검증.
 *
 * context provider 분기 hook 도 같이 검증 — NoOp 기본 사용.
 */
class LlmProxyServiceProviderTest {

    private final ObjectMapper objectMapper = new ObjectMapper();
    private final LlmContextProvider noopContextProvider = new NoOpContextProvider();

    private ChatRequest sampleRequest() {
        return new ChatRequest(
                "bllossom-8b",
                List.of(new ChatMessage("user", "안녕")),
                0.75, 250, true,
                null, null, null);
    }

    private LlmProperties properties(LlmProperties.Provider provider,
                                     LlmProperties.ContextProvider ctxProvider) {
        return new LlmProperties(
                provider,
                ctxProvider,
                new LlmProperties.ScriptedMode(false),
                "http://localhost:9999/v1",
                "test-key",
                new LlmProperties.DevBypass(false, "")
        );
    }

    private LlmProperties properties(LlmProperties.Provider provider) {
        return properties(provider, LlmProperties.ContextProvider.NONE);
    }

    @Test
    @DisplayName("provider=ANTHROPIC 일 때 streamChat 은 LLM_PROVIDER_NOT_SUPPORTED throw — body/SSE 변환 layer 채우기 전 stub")
    void anthropicProvider_throwsNotSupported() {
        LlmProxyService service = new LlmProxyService(
                properties(LlmProperties.Provider.ANTHROPIC), objectMapper, noopContextProvider);
        ResponseBodyEmitter emitter = new ResponseBodyEmitter();

        assertThatThrownBy(() -> service.streamChat(sampleRequest(), emitter))
                .isInstanceOf(BusinessException.class)
                .extracting(e -> ((BusinessException) e).errorCode())
                .isEqualTo(ErrorCode.LLM_PROVIDER_NOT_SUPPORTED);
    }

    @Test
    @DisplayName("LLM_PROVIDER_NOT_SUPPORTED 는 501 NOT_IMPLEMENTED 로 매핑된다")
    void notSupportedErrorCode_maps501() {
        assertThat(ErrorCode.LLM_PROVIDER_NOT_SUPPORTED.status().value()).isEqualTo(501);
    }

    @Test
    @DisplayName("provider=VLLM 일 때 streamChat 은 ANTHROPIC stub 분기에서 멈추지 않는다 — context cap 가드까지 진입")
    void vllmProvider_doesNotShortCircuit() {
        LlmProxyService service = new LlmProxyService(
                properties(LlmProperties.Provider.VLLM), objectMapper, noopContextProvider);
        ResponseBodyEmitter emitter = new ResponseBodyEmitter();

        // VLLM 분기는 WebClient 까지 가지만 baseUrl 이 localhost:9999 라 connect refused 가 emitter 안에서 처리됨 (예외 propagate X).
        // 즉 메서드는 throw 없이 정상 리턴 — provider 분기 통과 검증 목적.
        service.streamChat(sampleRequest(), emitter);

        // 여기까지 도달하면 ANTHROPIC stub 분기를 안 탔다는 뜻 (PROVIDER_NOT_SUPPORTED 였으면 throw 됐을 것).
        assertThat(true).isTrue();
    }

    @Test
    @DisplayName("context provider 가 spy 라 augment 호출 받음 — VLLM 분기에서 진입 확인")
    void vllmProvider_invokesContextProviderAugment() {
        // augment 호출 횟수 count 하는 simple spy
        int[] callCount = {0};
        LlmContextProvider spy = messages -> {
            callCount[0]++;
            return new LlmContextProvider.AugmentationResult(messages, 0);
        };
        LlmProxyService service = new LlmProxyService(
                properties(LlmProperties.Provider.VLLM), objectMapper, spy);
        ResponseBodyEmitter emitter = new ResponseBodyEmitter();

        service.streamChat(sampleRequest(), emitter);

        assertThat(callCount[0]).isEqualTo(1);
    }

    @Test
    @DisplayName("ANTHROPIC 분기 시 context provider augment 호출 안 됨 — 빠른 거절")
    void anthropicProvider_skipsContextProvider() {
        int[] callCount = {0};
        LlmContextProvider spy = messages -> {
            callCount[0]++;
            return new LlmContextProvider.AugmentationResult(messages, 0);
        };
        LlmProxyService service = new LlmProxyService(
                properties(LlmProperties.Provider.ANTHROPIC), objectMapper, spy);
        ResponseBodyEmitter emitter = new ResponseBodyEmitter();

        assertThatThrownBy(() -> service.streamChat(sampleRequest(), emitter))
                .isInstanceOf(BusinessException.class);

        assertThat(callCount[0]).isEqualTo(0);
    }
}
