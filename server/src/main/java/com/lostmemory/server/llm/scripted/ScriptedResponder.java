package com.lostmemory.server.llm.scripted;

import com.lostmemory.server.llm.config.LlmProperties;
import com.lostmemory.server.llm.dto.ChatMessage;
import lombok.extern.slf4j.Slf4j;
import org.springframework.http.MediaType;
import org.springframework.stereotype.Service;
import org.springframework.web.servlet.mvc.method.annotation.ResponseBodyEmitter;

import java.io.IOException;
import java.util.List;

/**
 * 시연용 scripted responder — user 발화 의 keyword 매치 시 LLM 호출 우회 + 미리 정의된 응답을
 * SSE chunk (OpenAI chat.completion.chunk 호환) 형식으로 emitter 에 직접 emit.
 *
 * <h3>흐름</h3>
 * <ol>
 *   <li>{@link #tryRespond(List, ResponseBodyEmitter)} 호출</li>
 *   <li>{@code llm.scripted-mode.enabled=false} 면 즉시 false 반환 → 호출자 LLM 호출 진행</li>
 *   <li>마지막 user 메시지 추출 → {@link YunaScriptedDialogues#DIALOGUES} 순서대로 keyword AND 매치</li>
 *   <li>매치: 응답을 글자 단위 SSE chunk 로 emit + {@code [DONE]} + emitter.complete() → true 반환</li>
 *   <li>미매치: false 반환 → 호출자 LLM 호출 진행 (vLLM fallback)</li>
 * </ol>
 *
 * <h3>SSE chunk 형식 (OpenAI chat.completion.chunk 호환)</h3>
 * <pre>
 * data: {"choices":[{"index":0,"delta":{"content":"안"},"finish_reason":null}]}\n\n
 * data: {"choices":[{"index":0,"delta":{"content":"녕"},"finish_reason":null}]}\n\n
 * ...
 * data: {"choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}\n\n
 * data: [DONE]\n\n
 * </pre>
 *
 * Unity 클라 {@code LlmStreamingDownloadHandler} 가 기존 vLLM SSE 와 동일한 파서로 처리.
 */
@Slf4j
@Service
public class ScriptedResponder {

    /**
     * 글자당 emit delay (ms). 너무 빠르면 클라 측 typing 효과 안 보임. 너무 느리면 시연 지루.
     */
    private static final long PER_CHAR_DELAY_MS = 60L;

    private static final String AUDIT_PREFIX = "[LLM_SCRIPTED]";

    private final LlmProperties properties;

    public ScriptedResponder(LlmProperties properties) {
        this.properties = properties;
    }

    /**
     * messages 의 마지막 user 발화에 매칭되는 scripted dialogue 가 있으면 SSE emit + emitter.complete()
     * 후 true 반환. 매치 없으면 emitter 안 건드리고 false 반환.
     *
     * @return true: 매치 + emit 완료 (호출자 LLM 호출 X). false: 미매치 (호출자 LLM 호출 진행)
     */
    public boolean tryRespond(List<ChatMessage> messages, ResponseBodyEmitter emitter) {
        if (properties.scriptedMode() == null || !properties.scriptedMode().enabled()) {
            return false;
        }
        String lastUser = lastUserMessage(messages);
        if (lastUser == null) {
            return false;
        }

        for (int i = 0; i < YunaScriptedDialogues.DIALOGUES.size(); i++) {
            ScriptedDialogue dialogue = YunaScriptedDialogues.DIALOGUES.get(i);
            if (dialogue.matches(lastUser)) {
                log.info("{} matched index={} keywords={}",
                        AUDIT_PREFIX, i, dialogue.userKeywords());
                emitScriptedResponse(dialogue.assistantResponse(), emitter);
                return true;
            }
        }
        return false;
    }

    /**
     * 응답을 글자 단위 SSE chunk 로 emit. 글자 당 {@link #PER_CHAR_DELAY_MS} ms delay.
     * 별도 thread 에서 실행 — emitter 가 servlet async 라 호출 스레드 blocking X.
     */
    private void emitScriptedResponse(String response, ResponseBodyEmitter emitter) {
        // 별도 thread — Thread.sleep delay 가 호출자 (LlmController) blocking 안 되도록.
        // Java 17 환경이라 virtual thread 대신 platform thread (단발 사용, pool 불요).
        Thread emitter_thread = new Thread(() -> {
            try {
                for (int i = 0; i < response.length(); i++) {
                    char ch = response.charAt(i);
                    String chunk = buildDeltaChunk(escapeJson(String.valueOf(ch)));
                    emitter.send("data: " + chunk + "\n\n", MediaType.TEXT_EVENT_STREAM);
                    Thread.sleep(PER_CHAR_DELAY_MS);
                }
                // 종료 chunk (finish_reason=stop) + [DONE]
                emitter.send("data: " + buildStopChunk() + "\n\n", MediaType.TEXT_EVENT_STREAM);
                emitter.send("data: [DONE]\n\n", MediaType.TEXT_EVENT_STREAM);
                emitter.complete();
                log.info("{} completed length={}chars", AUDIT_PREFIX, response.length());
            } catch (IOException ex) {
                log.warn("{} client connection lost during emit", AUDIT_PREFIX);
                emitter.completeWithError(ex);
            } catch (InterruptedException ex) {
                Thread.currentThread().interrupt();
                emitter.completeWithError(ex);
            }
        }, "scripted-emitter");
        emitter_thread.setDaemon(true);
        emitter_thread.start();
    }

    private String buildDeltaChunk(String contentEscaped) {
        return "{\"choices\":[{\"index\":0,\"delta\":{\"content\":\"" + contentEscaped
                + "\"},\"finish_reason\":null}]}";
    }

    private String buildStopChunk() {
        return "{\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}";
    }

    /**
     * 최소 JSON escape — chunk 한 글자 단위 emit 에 충분.
     * 줄바꿈 \n / 따옴표 / 백슬래시 처리.
     */
    private String escapeJson(String s) {
        StringBuilder sb = new StringBuilder(s.length() + 2);
        for (int i = 0; i < s.length(); i++) {
            char c = s.charAt(i);
            switch (c) {
                case '"' -> sb.append("\\\"");
                case '\\' -> sb.append("\\\\");
                case '\n' -> sb.append("\\n");
                case '\r' -> sb.append("\\r");
                case '\t' -> sb.append("\\t");
                default -> sb.append(c);
            }
        }
        return sb.toString();
    }

    private String lastUserMessage(List<ChatMessage> messages) {
        if (messages == null) return null;
        for (int i = messages.size() - 1; i >= 0; i--) {
            ChatMessage msg = messages.get(i);
            if ("user".equals(msg.role()) && msg.content() != null) {
                return msg.content();
            }
        }
        return null;
    }
}
