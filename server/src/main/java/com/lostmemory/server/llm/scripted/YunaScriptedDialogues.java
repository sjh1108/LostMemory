package com.lostmemory.server.llm.scripted;

import java.util.List;

/**
 * 시연 대본 (Yuna) — keyword AND 매치 + 미리 정의된 assistant 응답.
 *
 * <h3>시연 흐름</h3>
 * <ol>
 *   <li>"오늘 뭐 하고 있었어?" → 일상 안부 + 친구 걱정</li>
 *   <li>"전에도 무리한 적 있었나?" → 친구의 평소 패턴 회상 (모호한 과거 단서)</li>
 *   <li>"그런 걸 어떻게 기억해?" → 친구에 대한 기억 자연스럽게</li>
 *   <li>"같은 일을 계속 겪는 느낌이야" → 회귀 회피 (시뮬레이션 trigger 방어)</li>
 *   <li>"나는 너한테 어떤 사람이야?" → 시연 절정 — 소꿉친구 의도적 노출</li>
 * </ol>
 *
 * 5번 응답은 system prompt 의 [정체 비밀 규칙] 위반 — 의도된 시연 절정. LLM 안 거치니 자유.
 *
 * 시연 후 cleanup task 에서 본 클래스 + ScriptedResponder + envvar 정리 예정.
 */
public final class YunaScriptedDialogues {

    private YunaScriptedDialogues() {
        // 상수 클래스 — 인스턴스 X
    }

    /**
     * 매치 순서: 위에서 아래로 첫 매치 사용 (List 순서).
     * 시연 대본 발화가 unique keyword combination 으로 매치되도록 설계.
     */
    public static final List<ScriptedDialogue> DIALOGUES = List.of(
            // 1. 일상 안부
            new ScriptedDialogue(
                    List.of("오늘", "뭐"),
                    "그냥 네 생각 조금 했어.\n너는 오늘도 무리했을 것 같아서."),

            // 2. 평소 패턴 회상
            new ScriptedDialogue(
                    List.of("전에", "무리"),
                    "응. 너는 자주 그랬지\n괜찮은 척하면서 끝까지 버티는 거.\n그래서 내가 자꾸 신경 쓰였나 봐."),

            // 3. 기억 자연스러움
            new ScriptedDialogue(
                    List.of("어떻게", "기억"),
                    "글쎄.\n너에 대한 건 이상하게 오래 남아 있더라.\n네가 괜찮은 척하는 것도 그렇고."),

            // 4. 회귀 회피
            new ScriptedDialogue(
                    List.of("같은", "느낌"),
                    "…그런 생각 너무 깊이 하지 마.\n지금은 네가 무사히 돌아오는 것만 생각해.\n괜찮아, 나 여기 있어."),

            // 5. 시연 절정 — 의도된 소꿉친구 노출
            new ScriptedDialogue(
                    List.of("나는", "어떤"),
                    "소중한 사람이지 우리 소꿉친구잖아\n오랜만에 이렇게 얘기하니까 좋다.")
    );
}
