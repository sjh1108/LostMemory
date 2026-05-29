package com.lostmemory.server.llm.config;

import org.springframework.boot.context.properties.ConfigurationProperties;

/**
 * LLM 프록시 설정 + 검증 임시 dev bypass + 시연용 scripted mode 스위치.
 *
 * <h3>provider 분기</h3>
 * 현재 운영은 {@link Provider#VLLM} (RunPod bllossom-8b 등 OpenAI 호환).
 * {@link Provider#ANTHROPIC} 분기는 향후 GMS Claude swap 작업 시 채울 stub —
 * LlmProxyService 안에서 즉시 {@code LLM_PROVIDER_NOT_SUPPORTED} throw.
 *
 * <h3>contextProvider 분기</h3>
 * LLM 호출 전 messages 에 추가 context 를 주입하는 추상화 layer 선택.
 * {@code com.lostmemory.server.llm.context} 패키지의 LlmContextProvider 구현체와
 * {@code @ConditionalOnProperty(name="llm.context-provider", havingValue=...)} 로 매칭.
 *
 * <h3>scripted-mode</h3>
 * 시연용 — user 발화 keyword 매치 시 LLM 호출 우회 + 미리 정의된 응답을 SSE 로 직접 emit
 * ({@code com.lostmemory.server.llm.scripted.ScriptedResponder}).
 * 운영 default false. 시연 EC2 만 잠시 true 토글.
 *
 * <h3>dev-bypass</h3>
 * 클라 로그인 미완성 단계 endpoint 검증용. 정식 단계에서 본 record 의 dev-bypass 서브 record +
 * JwtAuthenticationFilter 분기 제거 예정.
 */
@ConfigurationProperties(prefix = "llm")
public record LlmProperties(
        Provider provider,
        ContextProvider contextProvider,
        ScriptedMode scriptedMode,
        String baseUrl,
        String apiKey,
        DevBypass devBypass
) {

    /**
     * LLM 백엔드 종류.
     * <ul>
     *   <li>VLLM: OpenAI Chat Completions 호환 (RunPod vLLM, OpenAI 등). 현재 운영.</li>
     *   <li>ANTHROPIC: Claude Messages API. 별도 body/SSE 변환 layer 필요 — 현재 stub.</li>
     * </ul>
     */
    public enum Provider {
        VLLM, ANTHROPIC
    }

    /**
     * Context 보강 방식.
     * <ul>
     *   <li>NONE: 원본 messages 그대로 (운영 default)</li>
     *   <li>HARDCODED: keyword 기반 hardcoded chunk 주입 (시연용)</li>
     *   <li>VECTOR_DB: embedding + vector DB top-K (정식 RAG, 별도 task 에서 채울 stub)</li>
     * </ul>
     */
    public enum ContextProvider {
        NONE, HARDCODED, VECTOR_DB
    }

    /**
     * 시연용 scripted mode 스위치.
     * enabled=true 시 ScriptedResponder 가 keyword 매치 발화에 미리 정의된 응답 emit (LLM 호출 우회).
     * false 시 모든 호출이 vLLM 으로 forward (운영 default).
     */
    public record ScriptedMode(boolean enabled) {
    }

    public record DevBypass(boolean enabled, String secret) {
    }
}
