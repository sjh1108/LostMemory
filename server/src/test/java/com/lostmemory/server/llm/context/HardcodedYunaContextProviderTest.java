package com.lostmemory.server.llm.context;

import com.lostmemory.server.llm.dto.ChatMessage;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;

/**
 * HardcodedYunaContextProvider 의 keyword 기반 chunk select 검증.
 * Spring context 없이 단독 인스턴스로 단위 테스트.
 */
class HardcodedYunaContextProviderTest {

    private final HardcodedYunaContextProvider provider = new HardcodedYunaContextProvider();

    private List<ChatMessage> withUser(String userMsg) {
        return List.of(
                new ChatMessage("system", "유나 페르소나..."),
                new ChatMessage("user", userMsg)
        );
    }

    @Test
    @DisplayName("기억 관련 발화 (\"기억\" / \"잊\" / \"예전\") → 기억 chunk 1개 주입")
    void memoryKeyword_injectsOneChunk() {
        var result = provider.augment(withUser("나 예전에 무슨 일이 있었지?"));
        assertThat(result.chunkCount()).isEqualTo(1);
        assertThat(result.messages()).hasSize(3); // system + retrieved + user
        assertThat(result.messages().get(1).role()).isEqualTo("system");
        assertThat(result.messages().get(1).content()).contains("기억 관련");
    }

    @Test
    @DisplayName("위로 발화 (\"힘들\" / \"포기\") → 위로 톤 chunk 주입")
    void comfortKeyword_injectsComfortChunk() {
        var result = provider.augment(withUser("다 포기하고 싶어"));
        assertThat(result.chunkCount()).isEqualTo(1);
        assertThat(result.messages().get(1).content()).contains("위로 톤");
    }

    @Test
    @DisplayName("정체 발화 (\"누구\" / \"AI\") → 정체 회피 chunk 주입")
    void identityKeyword_injectsIdentityChunk() {
        var result = provider.augment(withUser("넌 누구야?"));
        assertThat(result.chunkCount()).isEqualTo(1);
        assertThat(result.messages().get(1).content()).contains("정체 회피");
    }

    @Test
    @DisplayName("관계 발화 (\"우리\" / \"친\") → 관계 깊이 회피 chunk 주입")
    void relationshipKeyword_injectsRelationshipChunk() {
        var result = provider.augment(withUser("우리 친한 사이였어?"));
        // "우리", "친", "사이" 다 매치 — 같은 chunk 1개만 add (Chunk 단위 매치)
        assertThat(result.chunkCount()).isEqualTo(1);
        assertThat(result.messages().get(1).content()).contains("관계 깊이 회피");
    }

    @Test
    @DisplayName("여러 카테고리 keyword 동시 등장 → 여러 chunk 주입 (concatenated)")
    void multipleCategories_injectsMultipleChunks() {
        var result = provider.augment(withUser("기억이 안 나서 너무 힘들어"));
        // "기억" + "힘들" → 2개 chunk
        assertThat(result.chunkCount()).isEqualTo(2);
        String content = result.messages().get(1).content();
        assertThat(content).contains("기억 관련");
        assertThat(content).contains("위로 톤");
    }

    @Test
    @DisplayName("매치되는 keyword 없으면 chunkCount=0, 원본 그대로 반환")
    void noMatch_returnsOriginalMessages() {
        List<ChatMessage> original = withUser("오늘 날씨 어때?");
        var result = provider.augment(original);
        assertThat(result.chunkCount()).isEqualTo(0);
        assertThat(result.messages()).isSameAs(original);
    }

    @Test
    @DisplayName("user 메시지 없으면 (system 만) chunkCount=0")
    void noUserMessage_returnsZeroChunks() {
        var result = provider.augment(List.of(new ChatMessage("system", "유나 페르소나...")));
        assertThat(result.chunkCount()).isEqualTo(0);
    }

    @Test
    @DisplayName("null/빈 messages 면 chunkCount=0, 원본 그대로")
    void nullOrEmpty_returnsZeroChunks() {
        var result1 = provider.augment(null);
        assertThat(result1.chunkCount()).isEqualTo(0);

        var result2 = provider.augment(List.of());
        assertThat(result2.chunkCount()).isEqualTo(0);
    }

    @Test
    @DisplayName("retrieved chunk 가 기존 system 뒤 + 첫 user 앞에 삽입됨")
    void chunkInsertedBetweenSystemAndUser() {
        List<ChatMessage> messages = List.of(
                new ChatMessage("system", "system1"),
                new ChatMessage("system", "system2"),
                new ChatMessage("user", "기억이 잘 안 나"),
                new ChatMessage("assistant", "괜찮아"),
                new ChatMessage("user", "왜 기억이 안 나지")
        );
        var result = provider.augment(messages);

        // 원본 5개 + retrieved 1개 = 6개
        assertThat(result.messages()).hasSize(6);
        // 0, 1: 기존 system
        assertThat(result.messages().get(0).content()).isEqualTo("system1");
        assertThat(result.messages().get(1).content()).isEqualTo("system2");
        // 2: retrieved (새 system)
        assertThat(result.messages().get(2).role()).isEqualTo("system");
        assertThat(result.messages().get(2).content()).contains("기억 관련");
        // 3, 4, 5: user/assistant 순서 유지
        assertThat(result.messages().get(3).role()).isEqualTo("user");
        assertThat(result.messages().get(5).role()).isEqualTo("user");
    }

    @Test
    @DisplayName("뒤쪽 user 메시지 (가장 최근) 의 keyword 만 검사")
    void onlyLastUserMessageChecked() {
        List<ChatMessage> messages = List.of(
                new ChatMessage("system", "system"),
                new ChatMessage("user", "기억이 안 나"),       // 이전 — 무시
                new ChatMessage("assistant", "괜찮아"),
                new ChatMessage("user", "오늘 날씨 어때?")    // 최근 — 매치 없음
        );
        var result = provider.augment(messages);
        assertThat(result.chunkCount()).isEqualTo(0);
    }
}
