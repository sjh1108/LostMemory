package com.lostmemory.server.llm.scripted;

import java.util.List;

/**
 * 시연 대본 단일 dialogue — user 발화 keyword (AND 매치) 와 미리 정의된 assistant 응답.
 *
 * @param userKeywords      모두 포함되어야 매치 (AND). 빈 리스트면 매치 X.
 * @param assistantResponse SSE chunk 로 emit 할 응답 본문. LLM 안 거치므로 자유롭게 작성.
 */
public record ScriptedDialogue(
        List<String> userKeywords,
        String assistantResponse
) {

    /**
     * userMessage 에 {@link #userKeywords} 가 모두 포함되는지 검사 (AND).
     * 빈 keywords 리스트는 false (안전 — 모든 발화 매치 방지).
     */
    public boolean matches(String userMessage) {
        if (userMessage == null || userKeywords == null || userKeywords.isEmpty()) {
            return false;
        }
        for (String keyword : userKeywords) {
            if (!userMessage.contains(keyword)) {
                return false;
            }
        }
        return true;
    }
}
