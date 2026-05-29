package com.lostmemory.server.llm.context;

import com.lostmemory.server.llm.dto.ChatMessage;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.stereotype.Component;

import java.util.List;

/**
 * 기본 ContextProvider — 원본 messages 그대로 반환 (augment 없음).
 *
 * {@code llm.context-provider=none} (운영 default) 일 때 활성. 기존 동작 유지 — 추가 context 주입 X.
 * LlmContextProvider 도입 이전과 동일한 prompt 흐름.
 */
@Component
@ConditionalOnProperty(name = "llm.context-provider", havingValue = "none")
public class NoOpContextProvider implements LlmContextProvider {

    @Override
    public AugmentationResult augment(List<ChatMessage> messages) {
        return new AugmentationResult(messages, 0);
    }
}
