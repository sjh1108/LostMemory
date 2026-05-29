package com.lostmemory.server.llm.scripted;

import com.lostmemory.server.llm.config.LlmProperties;
import com.lostmemory.server.llm.dto.ChatMessage;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.web.servlet.mvc.method.annotation.ResponseBodyEmitter;

import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;

/**
 * ScriptedResponder 의 매치 분기 검증 (boolean 반환 위주).
 *
 * SSE emit 내용 / 글자당 delay / complete() 흐름은 별도 thread + ResponseBodyEmitter 의 spring container
 * 외부 동작 차이가 있어 단위 테스트로 검증 안정성 낮음 — EC2 가빌드 통합 검증으로 별도.
 *
 * 본 단위 테스트는:
 * <ul>
 *   <li>enabled=false 면 모든 발화에 false (LLM fallback)</li>
 *   <li>대본 발화 5개 정확 매치 (true 반환)</li>
 *   <li>대본 외 발화 false 반환</li>
 *   <li>최근 user 발화만 검사</li>
 * </ul>
 */
class ScriptedResponderTest {

    private LlmProperties properties(boolean scriptedEnabled) {
        return new LlmProperties(
                LlmProperties.Provider.VLLM,
                LlmProperties.ContextProvider.NONE,
                new LlmProperties.ScriptedMode(scriptedEnabled),
                "http://localhost:9999/v1",
                "test-key",
                new LlmProperties.DevBypass(false, ""));
    }

    private List<ChatMessage> withUser(String userMsg) {
        return List.of(
                new ChatMessage("system", "유나 페르소나..."),
                new ChatMessage("user", userMsg));
    }

    @Test
    @DisplayName("enabled=false 면 어떤 발화에도 false 반환 — LLM fallback")
    void disabled_alwaysFalse() {
        ScriptedResponder responder = new ScriptedResponder(properties(false));
        assertThat(responder.tryRespond(withUser("오늘 뭐 했어?"), new ResponseBodyEmitter())).isFalse();
        assertThat(responder.tryRespond(withUser("나는 너한테 어떤 사람이야?"), new ResponseBodyEmitter())).isFalse();
    }

    @Test
    @DisplayName("대본 1번 keyword (\"오늘\" + \"뭐\") 매치 — true 반환")
    void scripted1_matches() {
        ScriptedResponder responder = new ScriptedResponder(properties(true));
        assertThat(responder.tryRespond(withUser("오늘 뭐 하고 있었어?"), new ResponseBodyEmitter())).isTrue();
    }

    @Test
    @DisplayName("대본 2번 keyword (\"전에\" + \"무리\") 매치")
    void scripted2_matches() {
        ScriptedResponder responder = new ScriptedResponder(properties(true));
        assertThat(responder.tryRespond(withUser("전에도 무리한 적 있었나?"), new ResponseBodyEmitter())).isTrue();
    }

    @Test
    @DisplayName("대본 3번 keyword (\"어떻게\" + \"기억\") 매치")
    void scripted3_matches() {
        ScriptedResponder responder = new ScriptedResponder(properties(true));
        assertThat(responder.tryRespond(withUser("그런 걸 어떻게 기억해?"), new ResponseBodyEmitter())).isTrue();
    }

    @Test
    @DisplayName("대본 4번 keyword (\"같은\" + \"느낌\") 매치")
    void scripted4_matches() {
        ScriptedResponder responder = new ScriptedResponder(properties(true));
        assertThat(responder.tryRespond(withUser("같은 일을 계속 겪는 느낌이야"), new ResponseBodyEmitter())).isTrue();
    }

    @Test
    @DisplayName("대본 5번 keyword (\"나는\" + \"어떤\") 매치 — 시연 절정")
    void scripted5_matches() {
        ScriptedResponder responder = new ScriptedResponder(properties(true));
        assertThat(responder.tryRespond(withUser("나는 너한테 어떤 사람이야?"), new ResponseBodyEmitter())).isTrue();
    }

    @Test
    @DisplayName("대본 외 발화 → false (LLM fallback)")
    void unmatched_returnsFalse() {
        ScriptedResponder responder = new ScriptedResponder(properties(true));
        assertThat(responder.tryRespond(withUser("오늘 날씨 어때?"), new ResponseBodyEmitter())).isFalse();
        assertThat(responder.tryRespond(withUser("안녕"), new ResponseBodyEmitter())).isFalse();
    }

    @Test
    @DisplayName("user 메시지 없으면 false")
    void noUser_returnsFalse() {
        ScriptedResponder responder = new ScriptedResponder(properties(true));
        assertThat(responder.tryRespond(
                List.of(new ChatMessage("system", "system 만")), new ResponseBodyEmitter())).isFalse();
    }

    @Test
    @DisplayName("null / 빈 messages → false")
    void nullOrEmpty_returnsFalse() {
        ScriptedResponder responder = new ScriptedResponder(properties(true));
        assertThat(responder.tryRespond(null, new ResponseBodyEmitter())).isFalse();
        assertThat(responder.tryRespond(List.of(), new ResponseBodyEmitter())).isFalse();
    }

    @Test
    @DisplayName("뒤쪽 user 발화 (가장 최근) 의 keyword 만 매치")
    void onlyLastUserChecked() {
        ScriptedResponder responder = new ScriptedResponder(properties(true));
        List<ChatMessage> messages = List.of(
                new ChatMessage("system", "system"),
                new ChatMessage("user", "오늘 뭐 했어?"),  // 매치 가능 발화 — but 최근 아님
                new ChatMessage("assistant", "..."),
                new ChatMessage("user", "그냥 인사")        // 최근 — 매치 X
        );
        assertThat(responder.tryRespond(messages, new ResponseBodyEmitter())).isFalse();
    }
}
