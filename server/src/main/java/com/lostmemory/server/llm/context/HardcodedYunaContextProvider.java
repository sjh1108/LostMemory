package com.lostmemory.server.llm.context;

import com.lostmemory.server.llm.dto.ChatMessage;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.stereotype.Component;

import java.util.ArrayList;
import java.util.List;
import java.util.Set;

/**
 * 시연용 hardcoded ContextProvider — user 마지막 발화의 keyword 기반으로 chunk 를 select 해서
 * 추가 system message 로 append.
 *
 * 향후 VectorDbContextProvider (정식 RAG, embedding + vector DB top-K) 로 swap 시 같은
 * {@link LlmContextProvider} interface 그대로 활용 — 데이터 형식 (chunk 의 content) 도 호환.
 *
 * {@code llm.context-provider=hardcoded} 일 때 활성. 운영에선 default=none 으로 두고, 시연용 EC2
 * .env 에서만 hardcoded 토글 권장.
 *
 * <h3>Chunk 카테고리</h3>
 * <ul>
 *   <li>기억 / 회상 — 친구의 기억 발화에 조심스러운 톤 가이드</li>
 *   <li>위로 / 깊은 감정 — 무조건 응원 X, 인정 + 함께 있어줌</li>
 *   <li>정체 / AI 질문 — 모호 + 메타 X</li>
 *   <li>관계 깊이 유도 — 과거 직답 X, 친구가 스스로 떠올리도록</li>
 * </ul>
 */
@Component
@ConditionalOnProperty(name = "llm.context-provider", havingValue = "hardcoded")
public class HardcodedYunaContextProvider implements LlmContextProvider {

    /**
     * Yuna 컨텍스트 chunk 리스트.
     * keyword 하나라도 user 발화에 포함되면 해당 chunk 의 content 가 system message 로 주입.
     */
    private static final List<Chunk> CHUNKS = List.of(
            new Chunk(
                    Set.of("기억", "잊", "예전", "옛날", "전에", "이전"),
                    "[유나의 기억 관련 추가 가이드]\n"
                            + "친구의 기억 관련 발화에 조심스럽게 접근. 공유한 과거를 직접 노출하지 말 것.\n"
                            + "\"그건 네가 떠올려야 해\" / \"천천히 생각해도 돼\" 류로 친구가 스스로 알아가도록 짧게 유도."),
            new Chunk(
                    Set.of("힘들", "지치", "포기", "그만", "의미", "무의미", "버겁"),
                    "[유나의 위로 톤 강조]\n"
                            + "무조건 응원 금지. 친구의 감정을 먼저 인정 + 함께 있어줌.\n"
                            + "강요 / 설교 / \"힘내\" 류 가벼운 응원 금지. \"그래도 돼\" / \"잠깐만 더 얘기하자\" 류 부드러운 인정. 2~3문장."),
            new Chunk(
                    Set.of("누구", "정체", "AI", "사람이야", "왜 잘", "이름"),
                    "[정체 회피 톤 강조]\n"
                            + "이름은 \"유나\" 까지만. 그 이상 모호. 메타 발언 / \"나는 AI\" 절대 금지.\n"
                            + "\"...유나. 그 정도면 됐어\" / \"내가 누군지 꼭 알아야 해?\" 류 짧은 모호 응답."),
            new Chunk(
                    Set.of("우리", "친", "사이", "관계", "둘"),
                    "[관계 깊이 회피]\n"
                            + "과거 관계 직답 금지. \"친구\" / \"소꿉친구\" / \"오래된 사이\" 단어 자체 노출 X.\n"
                            + "\"그건 네가 떠올려야 해\" / \"글쎄, 어땠을 것 같아?\" 류 모호 응답으로 친구가 스스로 회상하도록.")
    );

    @Override
    public AugmentationResult augment(List<ChatMessage> messages) {
        if (messages == null || messages.isEmpty()) {
            return new AugmentationResult(messages, 0);
        }
        String lastUser = lastUserMessage(messages);
        if (lastUser == null) {
            return new AugmentationResult(messages, 0);
        }

        List<String> selected = new ArrayList<>();
        for (Chunk chunk : CHUNKS) {
            if (chunk.matches(lastUser)) {
                selected.add(chunk.content());
            }
        }
        if (selected.isEmpty()) {
            return new AugmentationResult(messages, 0);
        }

        // 원본 messages 복사 + retrieved chunk 를 추가 system message 로 삽입.
        // 위치: 기존 system message(들) 뒤, 첫 user/assistant message 앞.
        // 이유: LLM 이 system 누적을 일관된 컨텍스트로 처리 (multiple system 호환).
        ChatMessage retrievedMsg = new ChatMessage("system", String.join("\n\n", selected));
        List<ChatMessage> augmented = new ArrayList<>(messages.size() + 1);
        boolean inserted = false;
        for (ChatMessage msg : messages) {
            if (!inserted && !"system".equals(msg.role())) {
                augmented.add(retrievedMsg);
                inserted = true;
            }
            augmented.add(msg);
        }
        if (!inserted) {
            // 모두 system 만 있는 경우 (실제로는 거의 없음) — 끝에 append
            augmented.add(retrievedMsg);
        }

        return new AugmentationResult(augmented, selected.size());
    }

    /**
     * messages 뒤에서부터 가장 가까운 user 메시지의 content 반환. 없으면 null.
     */
    private String lastUserMessage(List<ChatMessage> messages) {
        for (int i = messages.size() - 1; i >= 0; i--) {
            ChatMessage msg = messages.get(i);
            if ("user".equals(msg.role()) && msg.content() != null) {
                return msg.content();
            }
        }
        return null;
    }

    /**
     * 단일 chunk — keyword 집합 + content (LLM 에 주입될 텍스트).
     * 향후 VectorDbContextProvider 의 chunk 와 동일 형식 → 데이터 마이그레이션 단순.
     */
    private record Chunk(Set<String> keywords, String content) {
        boolean matches(String userMessage) {
            for (String keyword : keywords) {
                if (userMessage.contains(keyword)) {
                    return true;
                }
            }
            return false;
        }
    }
}
