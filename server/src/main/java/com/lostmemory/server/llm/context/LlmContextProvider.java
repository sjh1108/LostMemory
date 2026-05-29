package com.lostmemory.server.llm.context;

import com.lostmemory.server.llm.dto.ChatMessage;

import java.util.List;

/**
 * LLM 호출 전 messages 에 추가 context 를 주입하는 추상화 layer.
 *
 * 구현체 (Spring {@code @ConditionalOnProperty(name="llm.context-provider", havingValue=...)} 분기):
 * <ul>
 *   <li>{@link NoOpContextProvider} — 원본 그대로 반환 (provider=none)</li>
 *   <li>{@link HardcodedYunaContextProvider} — keyword 기반 chunk select 후 system message append (provider=hardcoded)</li>
 *   <li>(향후) VectorDbContextProvider — 정식 RAG (provider=vector-db). 별도 task</li>
 * </ul>
 *
 * 추상화 의도: 시연용 hardcoded 와 정식 RAG (embedding + vector DB) 를 같은 hook 으로 swap.
 * LoRA fine-tune 은 모델 weight 측면이라 본 추상화 X — model 이름 swap 으로 처리.
 */
public interface LlmContextProvider {

    /**
     * 원본 messages 에 컨텍스트를 augment 해서 새 messages + 메타데이터 반환.
     * 호출자 ({@code LlmProxyService}) 는 결과를 그대로 vLLM 으로 forward.
     *
     * @param messages 원본 messages (system 고정 + user/assistant 교대)
     * @return augmented messages + audit 메타데이터
     */
    AugmentationResult augment(List<ChatMessage> messages);

    /**
     * augment 결과 + audit 메타데이터.
     *
     * @param messages    vLLM 으로 forward 할 새 messages. NoOp 의 경우 원본과 동일 참조
     * @param chunkCount  주입된 chunk 수 (audit log 용). NoOp 면 0
     */
    record AugmentationResult(List<ChatMessage> messages, int chunkCount) {
    }
}
